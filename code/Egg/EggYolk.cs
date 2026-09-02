using System;
using System.Collections.Generic;
using System.Linq;

namespace Stark.Food;

/// <summary>
/// The yolk, as a pressurised elastic sac rather than as fluid.
///
/// A yolk is bounded by the vitelline membrane - an elastic skin under
/// tension. That is why a fresh yolk stands up in a taut dome and resists a
/// prod, why an old one slumps, and why it can <i>rupture</i>. None of those
/// behaviours come out of a fluid solver, and all three come out of a
/// mass-spring shell for about a tenth of a millisecond.
///
/// The rig:
///   - a <see cref="Rigidbody"/> with a sphere collider carries the yolk's
///     gross motion, so it lands, rolls and gets pushed around by the world
///   - ~90 surface points hang off it as a spring lattice, giving the shape
///   - radial springs hold the sphere out, surface springs hold the skin
///     taut, and an explicit volume term makes it bulge sideways when it is
///     squashed flat, which springs alone will not do
///
/// Overstretch a surface spring past its break length and the membrane goes.
/// The sac is destroyed and its volume is handed to the
/// <see cref="EggFluidField"/> as <see cref="EggPhase.Yolk"/> - a broken yolk,
/// out of the same model, with no second system.
/// </summary>
[Title( "Egg Yolk" )]
[Category( "Food" )]
[Icon( "brightness_1" )]
public sealed class EggYolk : Component
{
	/// <summary>US large yolk: ~17g, ~1.05in across.</summary>
	[Property, Range( 0.3f, 1.2f )]
	public float Radius { get; set; } = 0.52f;

	/// <summary>
	/// Membrane tension. This one number is the egg's age: 1.0 is
	/// straight-from-the-hen taut, 0.25 is a fortnight old and will slump into
	/// a flat disc and break if you look at it.
	/// </summary>
	[Property, Range( 0.1f, 1.0f )]
	public float Freshness { get; set; } = 0.85f;

	/// <summary>Fraction of rest length a surface spring may stretch before it tears.</summary>
	[Property, Range( 1.2f, 3.0f )]
	public float BreakStretch { get; set; } = 1.9f;

	[Property]
	public EggFluidField Field { get; set; }

	[Property]
	public Action OnRupture { get; set; }

	public bool IsIntact { get; private set; } = true;

	struct Node
	{
		public Vector3 Rest;      // unit direction from centre
		public Vector3 Local;     // current offset from centre, local space
		public Vector3 Velocity;
	}

	Node[] _nodes;
	int[] _links;                 // pairs of node indices
	float[] _linkRest;
	int[] _tris;

	Rigidbody _body;
	SceneObject _so;
	Mesh _mesh;
	Vertex[] _vertices;

	float _restVolume;

	protected override void OnEnabled()
	{
		Field ??= Scene.GetAllComponents<EggFluidField>().FirstOrDefault();

		_body = Components.GetOrCreate<Rigidbody>();
		_body.LinearDamping = 1.4f;
		_body.AngularDamping = 6.0f;
		_body.MassOverride = 0.017f;   // 17g. Surface densities are kg/m3, so mass is kg.

		var collider = Components.GetOrCreate<SphereCollider>();
		collider.Radius = Radius * 0.82f;   // sits slightly inside the skin
		collider.Friction = 1.2f;
		collider.Elasticity = 0.02f;

		BuildSphere( subdivisions: 2 );
		BuildRenderMesh();

		_restVolume = 4.0f / 3.0f * MathF.PI * Radius * Radius * Radius;
	}

	protected override void OnDisabled()
	{
		_so?.Delete();
		_so = null;
	}

	// ------------------------------------------------------------ topology

	/// <summary>
	/// Subdivided icosahedron. Even vertex spacing matters here - a UV sphere
	/// bunches nodes at the poles and the spring lattice then behaves
	/// differently depending on which way up the yolk landed.
	/// </summary>
	void BuildSphere( int subdivisions )
	{
		var t = (1.0f + MathF.Sqrt( 5.0f )) * 0.5f;

		var verts = new List<Vector3>
		{
			new( -1, t, 0 ), new( 1, t, 0 ), new( -1, -t, 0 ), new( 1, -t, 0 ),
			new( 0, -1, t ), new( 0, 1, t ), new( 0, -1, -t ), new( 0, 1, -t ),
			new( t, 0, -1 ), new( t, 0, 1 ), new( -t, 0, -1 ), new( -t, 0, 1 )
		};

		var tris = new List<int>
		{
			0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11,
			1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
			3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9,
			4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
		};

		for ( int s = 0; s < subdivisions; s++ )
		{
			var next = new List<int>( tris.Count * 4 );
			var cache = new Dictionary<long, int>();

			for ( int i = 0; i < tris.Count; i += 3 )
			{
				var a = tris[i];
				var b = tris[i + 1];
				var c = tris[i + 2];

				var ab = Midpoint( verts, cache, a, b );
				var bc = Midpoint( verts, cache, b, c );
				var ca = Midpoint( verts, cache, c, a );

				next.AddRange( new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca } );
			}

			tris = next;
		}

		_nodes = new Node[verts.Count];
		for ( int i = 0; i < verts.Count; i++ )
		{
			var dir = verts[i].Normal;
			_nodes[i] = new Node { Rest = dir, Local = dir * Radius };
		}

		_tris = tris.ToArray();

		// Unique edges become the surface springs.
		var edges = new HashSet<long>();
		var links = new List<int>();

		for ( int i = 0; i < _tris.Length; i += 3 )
		{
			AddEdge( edges, links, _tris[i], _tris[i + 1] );
			AddEdge( edges, links, _tris[i + 1], _tris[i + 2] );
			AddEdge( edges, links, _tris[i + 2], _tris[i] );
		}

		_links = links.ToArray();
		_linkRest = new float[_links.Length / 2];

		for ( int i = 0; i < _linkRest.Length; i++ )
		{
			_linkRest[i] = (_nodes[_links[i * 2]].Local - _nodes[_links[i * 2 + 1]].Local).Length;
		}
	}

	static void AddEdge( HashSet<long> seen, List<int> links, int a, int b )
	{
		var key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
		if ( !seen.Add( key ) ) return;

		links.Add( a );
		links.Add( b );
	}

	static int Midpoint( List<Vector3> verts, Dictionary<long, int> cache, int a, int b )
	{
		var key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
		if ( cache.TryGetValue( key, out var found ) ) return found;

		verts.Add( ((verts[a] + verts[b]) * 0.5f).Normal );
		var index = verts.Count - 1;
		cache[key] = index;
		return index;
	}

	// -------------------------------------------------------------- physics

	protected override void OnFixedUpdate()
	{
		if ( !IsIntact || _nodes is null )
			return;

		var dt = Time.Delta;
		var tx = WorldTransform;

		// Membrane stiffness scales hard with freshness - this is the whole
		// fresh/old axis, and it is deliberately non-linear because the
		// perceptual difference between a fresh and a week-old yolk is large.
		var surfaceK = MathX.Lerp( 60.0f, 900.0f, Freshness * Freshness );
		var radialK = MathX.Lerp( 40.0f, 420.0f, Freshness );
		var damping = MathF.Pow( 0.0016f, dt );   // frame-rate independent

		// Gravity in the yolk's local frame. The sac slumps under its own
		// weight - a fresh one holds a dome against it, an old one does not.
		var gravity = tx.Rotation.Inverse * (Scene.PhysicsWorld.Gravity * 0.00035f);

		var currentVolume = EstimateVolume();
		var volumeError = (_restVolume - currentVolume) / _restVolume;

		// Pressure term. Springs alone let a squashed sac lose volume; this
		// pushes every node outward when it does, so flattening it makes it
		// bulge at the sides. That bulge is most of why it reads as a sac.
		var pressure = volumeError * MathX.Lerp( 120.0f, 700.0f, Freshness );

		for ( int i = 0; i < _nodes.Length; i++ )
		{
			ref var n = ref _nodes[i];

			var dist = n.Local.Length;
			var dir = dist > 0.0001f ? n.Local / dist : n.Rest;

			var force = dir * ((Radius - dist) * radialK + pressure);
			force += gravity;

			n.Velocity += force * dt;
		}

		// Surface springs, and the rupture test.
		for ( int i = 0; i < _linkRest.Length; i++ )
		{
			var ia = _links[i * 2];
			var ib = _links[i * 2 + 1];

			var delta = _nodes[ib].Local - _nodes[ia].Local;
			var dist = delta.Length;
			if ( dist < 0.0001f ) continue;

			var rest = _linkRest[i];

			if ( dist > rest * BreakStretch )
			{
				Rupture();
				return;
			}

			var f = delta / dist * ((dist - rest) * surfaceK * 0.5f);
			_nodes[ia].Velocity += f * dt;
			_nodes[ib].Velocity -= f * dt;
		}

		for ( int i = 0; i < _nodes.Length; i++ )
		{
			ref var n = ref _nodes[i];
			n.Velocity *= damping;
			n.Local += n.Velocity * dt;

			// Do not let a node pass through the collider that carries the
			// body, or the skin turns itself inside out on a hard landing.
			var dist = n.Local.Length;
			var floor = Radius * 0.45f;
			if ( dist < floor && dist > 0.0001f )
			{
				n.Local = n.Local / dist * floor;
				n.Velocity = Vector3.Zero;
			}
		}
	}

	/// <summary>
	/// Signed volume via the divergence theorem over the closed hull. Exact,
	/// and cheap enough at ~160 triangles to run every step.
	/// </summary>
	float EstimateVolume()
	{
		var total = 0.0f;

		for ( int i = 0; i < _tris.Length; i += 3 )
		{
			var a = _nodes[_tris[i]].Local;
			var b = _nodes[_tris[i + 1]].Local;
			var c = _nodes[_tris[i + 2]].Local;

			total += Vector3.Dot( a, Vector3.Cross( b, c ) );
		}

		return MathF.Abs( total ) / 6.0f;
	}

	/// <summary>
	/// The membrane tore. Hand the volume to the pool and delete the sac.
	/// Deposited across the sac's own footprint rather than at a point, so a
	/// yolk that breaks while spread out stays spread out.
	/// </summary>
	public void Rupture()
	{
		if ( !IsIntact ) return;

		IsIntact = false;

		if ( Field.IsValid() )
		{
			var tx = WorldTransform;
			var per = _restVolume / _nodes.Length;

			foreach ( var n in _nodes )
			{
				Field.Deposit( tx.PointToWorld( n.Local ), per, EggPhase.Yolk, Radius * 0.5f );
			}
		}

		Sound.Play( "sounds/food/yolk_break.sound", WorldPosition );

		OnRupture?.Invoke();
		GameObject.Destroy();
	}

	/// <summary>
	/// Impulse into the membrane - a fork, a spatula, the shell landing on it.
	/// Nodes near the hit take the hit, which is what lets a careless poke
	/// break it and a gentle one just wobble it.
	/// </summary>
	public void Poke( Vector3 worldPos, Vector3 worldImpulse, float radius = 0.3f )
	{
		if ( !IsIntact || _nodes is null ) return;

		var tx = WorldTransform;
		var local = tx.PointToLocal( worldPos );
		var impulse = tx.Rotation.Inverse * worldImpulse;
		var r2 = radius * radius;

		for ( int i = 0; i < _nodes.Length; i++ )
		{
			var d2 = (_nodes[i].Local - local).LengthSquared;
			if ( d2 > r2 ) continue;

			_nodes[i].Velocity += impulse * (1.0f - d2 / r2);
		}
	}

	// -------------------------------------------------------------- render

	void BuildRenderMesh()
	{
		_vertices = new Vertex[_nodes.Length];

		_mesh = new Mesh( Material.Load( "materials/food/egg_yolk.vmat" ) );
		_mesh.CreateVertexBuffer<Vertex>( _nodes.Length, Vertex.Layout );
		_mesh.CreateIndexBuffer( _tris.Length, _tris );

		var model = Model.Builder
			.WithName( "egg_yolk_sac" )
			.AddMesh( _mesh )
			.Create();

		_so = new SceneObject( Scene.SceneWorld, model, WorldTransform );
	}

	protected override void OnPreRender()
	{
		if ( _so is null || !IsIntact ) return;

		_so.Transform = WorldTransform;

		for ( int i = 0; i < _nodes.Length; i++ )
		{
			// Local length relative to rest radius is a good stand-in for how
			// much yolk is behind this point. The shader uses it to drive
			// subsurface scattering, which is what stops it looking like an
			// orange rubber ball.
			var thickness = (_nodes[i].Local.Length / Radius).Clamp( 0.0f, 2.0f );

			_vertices[i] = new Vertex
			{
				Position = _nodes[i].Local,
				Normal = _nodes[i].Local.Normal,
				Tangent = new Vector4( 1, 0, 0, -1 ),
				TexCoord0 = new Vector4( _nodes[i].Rest.x, _nodes[i].Rest.y, thickness, Freshness ),
				Color = Color32.White
			};
		}

		_mesh.SetVertexBufferData<Vertex>( _vertices );
	}
}
