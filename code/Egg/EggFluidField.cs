using System;
using System.Collections.Generic;

namespace Stark.Food;

/// <summary>
/// Which of the three fluids a deposit is made of. They share a solver but
/// not a rheology - see <see cref="EggFluidField"/> for why that matters.
/// </summary>
public enum EggPhase
{
	/// <summary>Watery albumen. Runs, spreads wide, forms the skirt.</summary>
	Thin,
	/// <summary>Gel albumen. Has a yield stress, holds the mound.</summary>
	Thick,
	/// <summary>Ruptured yolk. Thickest of the three, barely flows.</summary>
	Yolk
}

/// <summary>
/// A two-and-a-half dimensional pool of egg, solved on a regular grid lying
/// in a plane.
///
/// The whole design rests on one observation: a cracked egg reads as an egg
/// because the thick albumen has a <b>yield stress</b> and the thin albumen
/// does not. Thin runs out into a wide fingered skirt; thick refuses to flow
/// until the local slope beats a threshold, so it stands up in a mound and
/// then stops. Model that one difference and you get an egg. Model it with a
/// single viscosity and you get slime, no matter how good the solver is.
///
/// Everything is in inches. Heights are inches of depth, so a cell's volume
/// is <c>h * CellSize * CellSize</c> cubic inches, and the sum over the grid
/// is conserved to floating point.
/// </summary>
[Title( "Egg Fluid Field" )]
[Category( "Food" )]
[Icon( "waves" )]
public sealed class EggFluidField : Component
{
	// ---------------------------------------------------------------- setup

	[Property, Range( 16, 192 )]
	public int Resolution { get; set; } = 96;

	/// <summary>Width of one cell. Resolution * CellSize is the pan you can fill.</summary>
	[Property, Range( 0.02f, 0.5f )]
	public float CellSize { get; set; } = 0.09f;

	/// <summary>
	/// Below this depth a cell is treated as empty: it renders nothing and it
	/// will not donate to its neighbours. This is contact-line pinning, and
	/// without it the puddle diffuses outward forever and never settles.
	/// </summary>
	[Property, Range( 0.0005f, 0.02f )]
	public float MinDepth { get; set; } = 0.004f;

	// ----------------------------------------------------------- rheology

	/// <summary>
	/// Flow rate for thin albumen. Bounded by 0.25 for stability - each cell
	/// has four neighbours and must not be able to empty itself in one step.
	/// </summary>
	[Property, Range( 0.01f, 0.25f ), Group( "Rheology" )]
	public float ThinFlow { get; set; } = 0.22f;

	[Property, Range( 0.01f, 0.25f ), Group( "Rheology" )]
	public float ThickFlow { get; set; } = 0.10f;

	[Property, Range( 0.01f, 0.25f ), Group( "Rheology" )]
	public float YolkFlow { get; set; } = 0.05f;

	/// <summary>
	/// Yield slope for thick albumen: it will not flow into a neighbour until
	/// the height difference exceeds <c>Yield * CellSize</c>. This single term
	/// is the difference between egg white and water. Raise it for a fresher
	/// egg (taller, tighter mound), drop it toward zero for an old one.
	///
	/// It is also an <i>angle of repose</i>: the mound stands at a slope of
	/// <c>Yield</c>, so 0.85 is a 40 degree cone - a gel that would not pour
	/// out of a shell in the first place. A fresh mound measures nearer 0.35,
	/// and the style dial multiplies up from there.
	/// </summary>
	[Property, Range( 0.0f, 3.0f ), Group( "Rheology" )]
	public float ThickYield { get; set; } = 0.38f;

	[Property, Range( 0.0f, 4.0f ), Group( "Rheology" )]
	public float YolkYield { get; set; } = 0.85f;

	/// <summary>
	/// Surface tension, as an inward pull on cells that have an empty
	/// neighbour. Gives the pool a beaded rim instead of feathering out to
	/// nothing at the edge.
	/// </summary>
	[Property, Range( 0.0f, 0.4f ), Group( "Rheology" )]
	public float Cohesion { get; set; } = 0.12f;

	/// <summary>
	/// Per-cell multiplier on flow rate, drawn from static noise. Zero gives a
	/// perfect circle, which never happens in a pan. 0.3 gives the irregular
	/// fingered contact line that real thin albumen makes.
	/// </summary>
	[Property, Range( 0.0f, 0.8f ), Group( "Rheology" )]
	public float FlowVariance { get; set; } = 0.32f;

	// ------------------------------------------------------------- storage

	float[] _thin;
	float[] _thick;
	float[] _yolk;

	/// <summary>Static height of the surface under the fluid, in inches.</summary>
	float[] _ground;

	/// <summary>Per-cell flow multiplier. Baked once, never changes.</summary>
	float[] _variance;

	float[] _delta;

	int _w;
	float _cell;
	float _invCell;

	/// <summary>Plane origin: the grid's (0,0) corner in world space.</summary>
	Vector3 _origin;
	Vector3 _right;
	Vector3 _forward;
	Vector3 _up;

	/// <summary>Cells touched since the last rebuild, as an inclusive box.</summary>
	int _dirtyMinX, _dirtyMinY, _dirtyMaxX, _dirtyMaxY;

	public bool IsDirty { get; private set; }

	public int Width => _w;
	public float Cell => _cell;
	public Vector3 Origin => _origin;
	public Vector3 Up => _up;

	public ReadOnlySpan<float> Thin => _thin;
	public ReadOnlySpan<float> Thick => _thick;
	public ReadOnlySpan<float> Yolk => _yolk;
	public ReadOnlySpan<float> Ground => _ground;

	protected override void OnEnabled()
	{
		Allocate();
	}

	void Allocate()
	{
		_w = Resolution;
		_cell = CellSize;
		_invCell = 1.0f / _cell;

		var n = _w * _w;
		_thin = new float[n];
		_thick = new float[n];
		_yolk = new float[n];
		_ground = new float[n];
		_delta = new float[n];
		_variance = new float[n];

		// Static per-cell flow noise. Two octaves of value noise is enough -
		// we only need the contact line to be irregular, not interesting.
		var seed = System.HashCode.Combine( GameObject.Id );
		for ( int y = 0; y < _w; y++ )
		for ( int x = 0; x < _w; x++ )
		{
			var n0 = Noise.Perlin( x * 0.35f + seed, y * 0.35f );
			var n1 = Noise.Perlin( x * 1.10f - seed, y * 1.10f );
			_variance[y * _w + x] = MathX.Lerp( n0, n1, 0.35f ) * 2.0f - 1.0f;
		}

		var half = _w * _cell * 0.5f;
		var tx = WorldTransform;
		_right = tx.Rotation.Right;
		_forward = tx.Rotation.Forward;
		_up = tx.Rotation.Up;
		_origin = tx.Position - _right * half - _forward * half;

		ClearDirty();
	}

	// ------------------------------------------------------------ addressing

	public bool WorldToCell( Vector3 world, out int x, out int y )
	{
		var local = world - _origin;
		x = (int)MathF.Floor( Vector3.Dot( local, _right ) * _invCell );
		y = (int)MathF.Floor( Vector3.Dot( local, _forward ) * _invCell );
		return x >= 0 && y >= 0 && x < _w && y < _w;
	}

	public Vector3 CellToWorld( int x, int y, float height )
	{
		return _origin
			+ _right * ((x + 0.5f) * _cell)
			+ _forward * ((y + 0.5f) * _cell)
			+ _up * (_ground[y * _w + x] + height);
	}

	/// <summary>Total fluid depth at a cell, all three phases.</summary>
	public float DepthAt( int x, int y )
	{
		var i = y * _w + x;
		return _thin[i] + _thick[i] + _yolk[i];
	}

	/// <summary>
	/// Is this world point sitting in the pool? This is the shell-in-the-food
	/// test - a fragment that comes to rest over an occupied cell is in it.
	/// </summary>
	public bool IsInPool( Vector3 world, float minDepth = -1.0f )
	{
		if ( !WorldToCell( world, out var x, out var y ) )
			return false;

		return DepthAt( x, y ) > (minDepth < 0 ? MinDepth : minDepth);
	}

	/// <summary>Cubic inches currently held, per phase.</summary>
	public float Volume( EggPhase phase )
	{
		var src = Source( phase );
		var total = 0.0f;
		for ( int i = 0; i < src.Length; i++ )
			total += src[i];

		return total * _cell * _cell;
	}

	float[] Source( EggPhase phase ) => phase switch
	{
		EggPhase.Thin => _thin,
		EggPhase.Thick => _thick,
		_ => _yolk
	};

	// -------------------------------------------------------------- deposit

	/// <summary>
	/// Add fluid at a world position. <paramref name="volume"/> is cubic
	/// inches; <paramref name="radius"/> spreads it over a disc so a single
	/// large deposit does not arrive as a spike the solver then has to chew
	/// through. Called by <see cref="EggPour"/> when a particle lands, and by
	/// <see cref="EggYolk"/> when a membrane ruptures.
	/// </summary>
	public void Deposit( Vector3 world, float volume, EggPhase phase, float radius = 0.12f )
	{
		if ( volume <= 0.0f || _thin is null )
			return;

		if ( !WorldToCell( world, out var cx, out var cy ) )
			return;

		var target = Source( phase );
		var r = MathF.Max( radius * _invCell, 0.5f );
		var ri = (int)MathF.Ceiling( r );
		var r2 = r * r;

		// Cosine falloff, normalised so the deposit adds exactly `volume`.
		var weightSum = 0.0f;
		for ( int y = -ri; y <= ri; y++ )
		for ( int x = -ri; x <= ri; x++ )
		{
			var d2 = x * x + y * y;
			if ( d2 > r2 ) continue;
			if ( !InBounds( cx + x, cy + y ) ) continue;

			weightSum += Falloff( d2, r2 );
		}

		if ( weightSum <= 0.0f )
			return;

		var depthPerWeight = volume / (weightSum * _cell * _cell);

		for ( int y = -ri; y <= ri; y++ )
		for ( int x = -ri; x <= ri; x++ )
		{
			var d2 = x * x + y * y;
			if ( d2 > r2 ) continue;

			var px = cx + x;
			var py = cy + y;
			if ( !InBounds( px, py ) ) continue;

			target[py * _w + px] += Falloff( d2, r2 ) * depthPerWeight;
		}

		MarkDirty( cx - ri, cy - ri, cx + ri, cy + ri );
	}

	static float Falloff( float d2, float r2 )
	{
		var t = 1.0f - MathF.Sqrt( d2 / r2 );
		return t * t;
	}

	bool InBounds( int x, int y ) => x >= 0 && y >= 0 && x < _w && y < _w;

	/// <summary>
	/// Bake a static surface profile under the fluid - a pan's dish, a
	/// chopping board's tilt. The fluid pools into the low spots for free.
	/// </summary>
	public void SetGround( Func<int, int, float> height )
	{
		for ( int y = 0; y < _w; y++ )
		for ( int x = 0; x < _w; x++ )
			_ground[y * _w + x] = height( x, y );

		MarkDirty( 0, 0, _w - 1, _w - 1 );
	}

	// --------------------------------------------------------------- solver

	protected override void OnFixedUpdate()
	{
		if ( _thin is null )
			return;

		// The style dial. Everything below is still the measured rheology -
		// these are multipliers on it, and at Documentary they are all 1.
		var style = EggStyle.Current;
		_styleFingering = FlowVariance * style.Fingering;

		var rim = Cohesion * style.Rim;

		// Solved in this order deliberately. Thin runs first and furthest, so
		// the skirt is laid down before the mound settles on top of it.
		Relax( _thin, MathF.Min( ThinFlow * style.Skirt, 0.25f ), 0.0f, rim );
		Relax( _thick, ThickFlow, ThickYield * style.Mound, rim * 1.6f );
		Relax( _yolk, YolkFlow, YolkYield * style.Mound, rim * 2.2f );
	}

	/// <summary>Style-scaled <see cref="FlowVariance"/>, refreshed each step.</summary>
	float _styleFingering;

	/// <summary>
	/// One relaxation sweep of a single phase.
	///
	/// For each cell we look at the four neighbours and move fluid downhill in
	/// total head (ground + all phases stacked), because the phases sit on top
	/// of each other and a mound of thick white does push the thin white out
	/// from under it. But only <i>this</i> phase's depth actually moves, and
	/// only past its own yield slope.
	///
	/// Outflow is gathered into a delta buffer and applied afterwards, so the
	/// sweep is order-independent and cannot drive a cell negative.
	/// </summary>
	void Relax( float[] h, float flow, float yield, float cohesion )
	{
		Array.Clear( _delta );

		// Only sweep the region that has fluid in it. An egg occupies maybe a
		// fifth of the grid, so this is most of the saving.
		var x0 = Math.Max( _dirtyMinX - 1, 0 );
		var y0 = Math.Max( _dirtyMinY - 1, 0 );
		var x1 = Math.Min( _dirtyMaxX + 1, _w - 1 );
		var y1 = Math.Min( _dirtyMaxY + 1, _w - 1 );

		if ( x1 < x0 || y1 < y0 )
			return;

		var yieldHeight = yield * _cell;
		var expandMinX = _dirtyMinX;
		var expandMinY = _dirtyMinY;
		var expandMaxX = _dirtyMaxX;
		var expandMaxY = _dirtyMaxY;

		for ( int y = y0; y <= y1; y++ )
		for ( int x = x0; x <= x1; x++ )
		{
			var i = y * _w + x;
			var depth = h[i];

			// Pinned: too little to overcome its own contact line.
			if ( depth <= MinDepth )
				continue;

			var headI = _ground[i] + _thin[i] + _thick[i] + _yolk[i];
			var k = flow * (1.0f + _variance[i] * _styleFingering);

			// Cap total outflow at a quarter of the cell per neighbour so four
			// simultaneous donations can never exceed what is there - and
			// budget them against a running total, because the cohesion pull
			// below is a fifth outflow. Without the budget a cell can be asked
			// for more than it holds, the clamp at zero absorbs the shortfall,
			// and the field quietly *gains* volume - which the style dial's
			// rim multiplier is more than large enough to trigger.
			var maxPerNeighbour = depth * 0.25f;
			var budget = depth;
			var emptyNeighbours = 0;

			for ( int d = 0; d < 4; d++ )
			{
				var nx = x + (d == 0 ? 1 : d == 1 ? -1 : 0);
				var ny = y + (d == 2 ? 1 : d == 3 ? -1 : 0);

				if ( !InBounds( nx, ny ) )
					continue;

				var j = ny * _w + nx;

				if ( _thin[j] + _thick[j] + _yolk[j] <= MinDepth )
					emptyNeighbours++;

				var headJ = _ground[j] + _thin[j] + _thick[j] + _yolk[j];
				var dH = headI - headJ;

				if ( dH <= 0.0f )
					continue;

				// The yield stress. Everything above is bookkeeping; this is
				// the line that makes it egg white instead of water.
				if ( yield > 0.0f )
				{
					dH -= yieldHeight;
					if ( dH <= 0.0f )
						continue;
				}

				var amount = MathF.Min( MathF.Min( k * dH, maxPerNeighbour ), budget );
				if ( amount <= 0.0f )
					continue;

				budget -= amount;
				_delta[i] -= amount;
				_delta[j] += amount;

				// The pool just grew into a cell we were not tracking.
				if ( nx < expandMinX ) expandMinX = nx;
				if ( ny < expandMinY ) expandMinY = ny;
				if ( nx > expandMaxX ) expandMaxX = nx;
				if ( ny > expandMaxY ) expandMaxY = ny;
			}

			// Surface tension. A cell exposed on several sides is at the rim,
			// and the rim beads up rather than feathering away. Pulling depth
			// off the rim cells and back inward is a cheap stand-in for a real
			// curvature term and reads correctly at this scale.
			if ( cohesion > 0.0f && emptyNeighbours > 0 )
			{
				var pull = MathF.Min( MathF.Min( depth * cohesion * 0.25f * emptyNeighbours, maxPerNeighbour ), budget );
				if ( pull <= 0.0f )
					continue;

				budget -= pull;
				_delta[i] -= pull;

				// Push it to the wettest neighbour - that is "inward".
				var best = -1;
				var bestDepth = 0.0f;

				for ( int d = 0; d < 4; d++ )
				{
					var nx = x + (d == 0 ? 1 : d == 1 ? -1 : 0);
					var ny = y + (d == 2 ? 1 : d == 3 ? -1 : 0);
					if ( !InBounds( nx, ny ) ) continue;

					var j = ny * _w + nx;
					if ( h[j] > bestDepth )
					{
						bestDepth = h[j];
						best = j;
					}
				}

				if ( best >= 0 ) _delta[best] += pull;
				else _delta[i] += pull;
			}
		}

		var moved = false;
		for ( int y = y0; y <= y1; y++ )
		for ( int x = x0; x <= x1; x++ )
		{
			var i = y * _w + x;
			if ( _delta[i] == 0.0f ) continue;

			h[i] = MathF.Max( h[i] + _delta[i], 0.0f );
			moved = true;
		}

		if ( moved )
		{
			MarkDirty( expandMinX, expandMinY, expandMaxX, expandMaxY );
		}
	}

	// ---------------------------------------------------------------- dirty

	void MarkDirty( int x0, int y0, int x1, int y1 )
	{
		_dirtyMinX = Math.Clamp( Math.Min( _dirtyMinX, x0 ), 0, _w - 1 );
		_dirtyMinY = Math.Clamp( Math.Min( _dirtyMinY, y0 ), 0, _w - 1 );
		_dirtyMaxX = Math.Clamp( Math.Max( _dirtyMaxX, x1 ), 0, _w - 1 );
		_dirtyMaxY = Math.Clamp( Math.Max( _dirtyMaxY, y1 ), 0, _w - 1 );
		IsDirty = true;
	}

	void ClearDirty()
	{
		_dirtyMinX = _w;
		_dirtyMinY = _w;
		_dirtyMaxX = -1;
		_dirtyMaxY = -1;
		IsDirty = false;
	}

	/// <summary>Called by the renderer once it has consumed the current state.</summary>
	public void ConsumeDirty() => IsDirty = false;
}
