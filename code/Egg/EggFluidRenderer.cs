using System;
using System.Collections.Generic;

namespace Stark.Food;

/// <summary>
/// Turns an <see cref="EggFluidField"/> into a surface mesh.
///
/// A grid mesh over the occupied cells, rebuilt into a pre-allocated dynamic
/// <see cref="Mesh"/>. Cells below the field's contact-line threshold are
/// skipped entirely, so the silhouette of the mesh <i>is</i> the silhouette of
/// the puddle - which is the thing the eye actually reads.
///
/// Per-vertex we hand the shader everything it needs to shade three fluids
/// with one material:
///   TexCoord0.xy  planar UV
///   TexCoord0.z   total depth, inches (drives absorption and refraction)
///   TexCoord0.w   thick fraction 0..1 (milky vs clear)
///   Color.r       yolk fraction 0..1
///   Color.a       edge proximity 0..1 (the meniscus darkening)
/// </summary>
[Title( "Egg Fluid Renderer" )]
[Category( "Food" )]
[Icon( "blur_on" )]
public sealed class EggFluidRenderer : Component, Component.ExecuteInEditor
{
	[RequireComponent]
	public EggFluidField Field { get; set; }

	[Property]
	public Material Material { get; set; }

	/// <summary>
	/// Rebuild every N fixed steps. The field settles fast and the surface is
	/// smooth; 2 is invisible and halves the cost.
	/// </summary>
	[Property, Range( 1, 6 )]
	public int RebuildInterval { get; set; } = 2;

	/// <summary>
	/// Vertical exaggeration. Real depths are a tenth of an inch and read as
	/// flat in first person; a little lift restores the mound without making
	/// it look like jelly.
	/// </summary>
	[Property, Range( 1.0f, 3.0f )]
	public float HeightScale { get; set; } = 1.45f;

	SceneObject _so;
	Mesh _mesh;
	Model _model;

	Vertex[] _vertices;
	int[] _indices;
	int[] _vertexIndex;      // cell corner -> vertex slot, -1 if unused
	int _vertexCount;
	int _indexCount;
	int _tick;

	protected override void OnEnabled()
	{
		Field ??= Components.Get<EggFluidField>();
		if ( !Field.IsValid() ) return;

		// One corner per cell corner, so (w+1)^2. Allocated once for the whole
		// grid - a dynamic mesh that never resizes is worth the memory.
		var corners = (Field.Width + 1) * (Field.Width + 1);

		_vertices = new Vertex[corners];
		_indices = new int[Field.Width * Field.Width * 6];
		_vertexIndex = new int[corners];

		_mesh = new Mesh( Material ?? Material.Load( "materials/food/egg_white.vmat" ) );
		_mesh.CreateVertexBuffer<Vertex>( corners, Vertex.Layout );
		_mesh.CreateIndexBuffer( _indices.Length );
		_mesh.SetVertexRange( 0, 0 );
		_mesh.SetIndexRange( 0, 0 );

		_model = Model.Builder
			.WithName( "egg_fluid_surface" )
			.AddMesh( _mesh )
			.Create();

		_so = new SceneObject( Scene.SceneWorld, _model, WorldTransform )
		{
			Flags = { CastShadows = false, IsTranslucent = true }
		};
	}

	protected override void OnDisabled()
	{
		_so?.Delete();
		_so = null;
		_mesh = null;
		_model = null;
	}

	protected override void OnPreRender()
	{
		if ( _so is null || !Field.IsValid() )
			return;

		_so.Transform = WorldTransform;

		if ( !Field.IsDirty )
			return;

		if ( ++_tick < RebuildInterval )
			return;

		_tick = 0;
		Rebuild();
		Field.ConsumeDirty();
	}

	void Rebuild()
	{
		var w = Field.Width;
		var stride = w + 1;
		var cell = Field.Cell;
		var minDepth = Field.MinDepth;

		Array.Fill( _vertexIndex, -1 );
		_vertexCount = 0;
		_indexCount = 0;

		var thin = Field.Thin;
		var thick = Field.Thick;
		var yolk = Field.Yolk;

		// Corner values are the average of the (up to) four cells that touch
		// them. Sampling cell centres straight into corners is what makes
		// height-field fluid look faceted; averaging costs nothing and the
		// surface comes out smooth.
		for ( int cy = 0; cy < w; cy++ )
		for ( int cx = 0; cx < w; cx++ )
		{
			var i = cy * w + cx;
			if ( thin[i] + thick[i] + yolk[i] <= minDepth )
				continue;

			var c00 = EmitCorner( cx, cy, stride );
			var c10 = EmitCorner( cx + 1, cy, stride );
			var c11 = EmitCorner( cx + 1, cy + 1, stride );
			var c01 = EmitCorner( cx, cy + 1, stride );

			_indices[_indexCount++] = c00;
			_indices[_indexCount++] = c10;
			_indices[_indexCount++] = c11;
			_indices[_indexCount++] = c00;
			_indices[_indexCount++] = c11;
			_indices[_indexCount++] = c01;
		}

		if ( _indexCount == 0 )
		{
			_mesh.SetVertexRange( 0, 0 );
			_mesh.SetIndexRange( 0, 0 );
			return;
		}

		ComputeNormals();

		_mesh.SetVertexBufferData<Vertex>( _vertices.AsSpan( 0, _vertexCount ) );
		_mesh.SetIndexBufferData( _indices.AsSpan( 0, _indexCount ) );
		_mesh.SetVertexRange( 0, _vertexCount );
		_mesh.SetIndexRange( 0, _indexCount );
	}

	/// <summary>
	/// Emit (or reuse) the vertex at a cell corner, sampling the four cells
	/// around it. Returns the vertex slot.
	/// </summary>
	int EmitCorner( int gx, int gy, int stride )
	{
		var key = gy * stride + gx;
		if ( _vertexIndex[key] >= 0 )
			return _vertexIndex[key];

		var w = Field.Width;
		var thin = Field.Thin;
		var thick = Field.Thick;
		var yolk = Field.Yolk;
		var ground = Field.Ground;

		float sThin = 0, sThick = 0, sYolk = 0, sGround = 0;
		int samples = 0;
		int wet = 0;

		for ( int oy = -1; oy <= 0; oy++ )
		for ( int ox = -1; ox <= 0; ox++ )
		{
			var cx = gx + ox;
			var cy = gy + oy;
			if ( cx < 0 || cy < 0 || cx >= w || cy >= w ) continue;

			var i = cy * w + cx;
			sThin += thin[i];
			sThick += thick[i];
			sYolk += yolk[i];
			sGround += ground[i];
			samples++;

			if ( thin[i] + thick[i] + yolk[i] > Field.MinDepth )
				wet++;
		}

		if ( samples == 0 ) samples = 1;

		var inv = 1.0f / samples;
		sThin *= inv;
		sThick *= inv;
		sYolk *= inv;
		sGround *= inv;

		var depth = sThin + sThick + sYolk;
		var thickFrac = depth > 0.0f ? (sThick + sYolk) / depth : 0.0f;
		var yolkFrac = depth > 0.0f ? sYolk / depth : 0.0f;

		// A corner touched by fewer than four wet cells is on the contact
		// line. The shader darkens it - that dark rim is what makes the pool
		// sit on the pan instead of looking painted onto it.
		var edge = 1.0f - (wet / 4.0f);

		var pos = new Vector3(
			gx * Field.Cell - w * Field.Cell * 0.5f,
			gy * Field.Cell - w * Field.Cell * 0.5f,
			sGround + depth * HeightScale );

		var slot = _vertexCount++;
		_vertices[slot] = new Vertex
		{
			Position = pos,
			Normal = Vector3.Up,
			Tangent = new Vector4( 1, 0, 0, -1 ),
			TexCoord0 = new Vector4( gx / (float)w, gy / (float)w, depth, thickFrac ),
			Color = new Color32( (byte)(yolkFrac * 255), 0, 0, (byte)(edge * 255) )
		};

		_vertexIndex[key] = slot;
		return slot;
	}

	/// <summary>
	/// Area-weighted face normals accumulated onto vertices. The surface is
	/// nearly flat, so normals carry almost all of the shading - it is worth
	/// doing properly rather than differencing the height field.
	/// </summary>
	void ComputeNormals()
	{
		for ( int i = 0; i < _vertexCount; i++ )
			_vertices[i].Normal = Vector3.Zero;

		for ( int i = 0; i < _indexCount; i += 3 )
		{
			var a = _indices[i];
			var b = _indices[i + 1];
			var c = _indices[i + 2];

			var n = Vector3.Cross(
				_vertices[b].Position - _vertices[a].Position,
				_vertices[c].Position - _vertices[a].Position );

			_vertices[a].Normal += n;
			_vertices[b].Normal += n;
			_vertices[c].Normal += n;
		}

		for ( int i = 0; i < _vertexCount; i++ )
		{
			var n = _vertices[i].Normal;
			_vertices[i].Normal = n.LengthSquared > 0.0001f ? n.Normal : Vector3.Up;
		}
	}
}
