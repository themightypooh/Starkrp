using System;
using System.Linq;

namespace Stark.Food;

/// <summary>
/// An intact egg: pick it up, slam it on something, open it.
///
/// Three things happen in here.
///
/// <b>Slosh.</b> An intact egg is not simulated inside. It does not need to
/// be - what a player feels when they shake an egg is the mass centre moving,
/// so we move the mass centre and simulate nothing. Fifteen lines,
/// indistinguishable from the real thing.
///
/// <b>The crack test.</b> Impact outcome is a function of three things, and
/// the second one is what turns this from a timing minigame into a skill:
///
///   1. impact speed along the contact normal
///   2. <i>where on the egg</i> it landed. A shell is an arch: it is
///      dramatically stronger end-on than side-on. Hitting the equator opens
///      it cleanly with far less force. Real, free, and it gives players
///      something to actually get good at.
///   3. what it hit. A flat surface crushes a wide zone and drives fragments
///      inward - which is exactly why chefs are taught to crack on a flat
///      counter and not on the pan rim. Encoding it makes the skill honest
///      rather than arbitrary.
///
/// <b>Fragments.</b> The two big halves are authored break pieces. The small
/// bits are not rigidbodies - see the scale note in docs/02-egg-simulation.md.
/// </summary>
[Title( "Egg" )]
[Category( "Food" )]
[Icon( "egg" )]
public sealed class EggShell : Component, Component.ICollisionListener, Component.IPressable
{
	[Property] public EggFluidField Field { get; set; }
	[Property] public GameObject YolkPrefab { get; set; }

	/// <summary>Below this contact speed nothing happens but a knock.</summary>
	[Property, Range( 10f, 120f ), Group( "Crack" )]
	public float CrackThreshold { get; set; } = 42.0f;

	/// <summary>Above this it shatters and you are picking shell out of the pan.</summary>
	[Property, Range( 40f, 400f ), Group( "Crack" )]
	public float ShatterThreshold { get; set; } = 145.0f;

	/// <summary>
	/// How much easier a side-on hit is than an end-on one. 0.45 means hitting
	/// the equator needs 45% of the force that hitting the point does, which
	/// is about right and is a wide enough gap for players to feel it.
	/// </summary>
	[Property, Range( 0.2f, 1.0f ), Group( "Crack" )]
	public float EquatorAdvantage { get; set; } = 0.45f;

	/// <summary>Accumulated damage. A soft tap does not open an egg, but two do.</summary>
	[Property, ReadOnly, Group( "Crack" )]
	public float Damage { get; private set; }

	[Property] public Action<CrackResult> OnCracked { get; set; }

	public enum CrackQuality { Knock, Clean, Messy, Shattered }

	public readonly record struct CrackResult(
		CrackQuality Quality,
		float Speed,
		float EquatorDot,
		int Fragments,
		bool ShellInFood );

	Rigidbody _body;
	Vector3 _sloshOffset;
	Vector3 _sloshVelocity;
	Vector3 _lastVelocity;
	bool _opened;

	protected override void OnEnabled()
	{
		_body = Components.GetOrCreate<Rigidbody>();
		_body.OverrideMassCenter = true;
		Field ??= Scene.GetAllComponents<EggFluidField>().FirstOrDefault();
	}

	// ---------------------------------------------------------------- slosh

	protected override void OnFixedUpdate()
	{
		if ( _opened || !_body.IsValid() )
			return;

		var dt = Time.Delta;

		// The contents lag the shell. Acceleration of the shell pushes the
		// mass centre the other way; a spring pulls it back to the middle.
		var accel = (_body.Velocity - _lastVelocity) / MathF.Max( dt, 0.0001f );
		_lastVelocity = _body.Velocity;

		var local = WorldRotation.Inverse * accel;

		_sloshVelocity += (-local * 0.0009f - _sloshOffset * 210.0f) * dt;
		_sloshVelocity *= MathF.Pow( 0.02f, dt );
		_sloshOffset += _sloshVelocity * dt;

		// An egg's contents cannot travel more than the shell they are in.
		_sloshOffset = _sloshOffset.ClampLength( 0.22f );

		_body.MassCenterOverride = _sloshOffset;
	}

	// --------------------------------------------------------------- pickup

	bool Component.IPressable.CanPress( Component.IPressable.Event e ) => !_opened;

	Component.IPressable.Tooltip? Component.IPressable.GetTooltip( Component.IPressable.Event e )
		=> new( "Egg", "egg", "Hold to pick up, then strike a flat surface side-on", !_opened );

	bool Component.IPressable.Press( Component.IPressable.Event e )
	{
		if ( _opened ) return false;

		// Handing off to whatever carry system the game uses; the egg only
		// cares that it is now being held, because a held egg must not accrue
		// damage from being set down.
		GameObject.Tags.Add( "held" );
		return true;
	}

	void Component.IPressable.Release( Component.IPressable.Event e )
	{
		GameObject.Tags.Remove( "held" );
	}

	// ---------------------------------------------------------------- crack

	void Component.ICollisionListener.OnCollisionStart( Collision c )
	{
		if ( _opened )
			return;

		var speed = MathF.Abs( c.Contact.NormalSpeed );
		if ( speed < 12.0f )
			return;

		// The egg's long axis is its local forward. How side-on was the hit?
		// 1 = square on the equator (weakest, cleanest), 0 = dead on an end
		// (strongest, and it takes a real swing).
		var longAxis = WorldRotation.Forward;
		var equatorDot = 1.0f - MathF.Abs( Vector3.Dot( longAxis, c.Contact.Normal.Normal ) );

		// Effective strength: side-on is easier, and a hard edge concentrates
		// the load so it needs less again - but see below, an edge is worse
		// for what it does to the fragments.
		var onEdge = c.Other.GameObject.IsValid() && c.Other.GameObject.Tags.Has( "edge", true );
		var strength = MathX.Lerp( 1.0f, EquatorAdvantage, equatorDot ) * (onEdge ? 0.7f : 1.0f);

		var need = CrackThreshold * strength;
		var shatterAt = ShatterThreshold * strength;

		Damage += speed / need;

		if ( Damage < 1.0f )
		{
			// A knock. Leave a mark and a sound so the player can read that
			// they were close, rather than having to guess.
			Sound.Play( "sounds/food/egg_knock.sound", c.Contact.Point );
			return;
		}

		Open( c, speed, equatorDot, shatterAt, onEdge );
	}

	void Open( Collision c, float speed, float equatorDot, float shatterAt, bool onEdge )
	{
		_opened = true;

		CrackQuality quality;
		int fragments;

		if ( speed > shatterAt )
		{
			// Way too hard. Shell everywhere, and some of the contents never
			// make it into the pan.
			quality = CrackQuality.Shattered;
			fragments = Game.Random.Int( 6, 12 );
		}
		else if ( speed > shatterAt * 0.72f || equatorDot < 0.35f )
		{
			// Either overcooked the swing or hit it end-on and had to force
			// it. Both crush shell inward.
			quality = CrackQuality.Messy;
			fragments = Game.Random.Int( 2, 5 );
		}
		else
		{
			quality = CrackQuality.Clean;
			fragments = Game.Random.Int( 0, 2 );
		}

		// A flat surface drives fragments inward; an edge punches a hole and
		// drops the pieces straight in. This is the real reason the flat
		// counter is the taught technique.
		if ( onEdge ) fragments += 2;

		var contentsLost = quality == CrackQuality.Shattered ? 0.35f : 0.0f;

		SpawnHalves( c );
		var shellInFood = SpawnFragments( c, fragments );
		SpawnContents( c, 1.0f - contentsLost );

		Sound.Play( quality == CrackQuality.Clean
			? "sounds/food/egg_crack_clean.sound"
			: "sounds/food/egg_crack_messy.sound", c.Contact.Point );

		OnCracked?.Invoke( new CrackResult( quality, speed, equatorDot, fragments, shellInFood ) );

		Components.Get<ModelRenderer>()?.Destroy();
		Components.Get<Collider>()?.Destroy();
		_body?.Destroy();
	}

	/// <summary>
	/// The two big halves, from authored break pieces in ModelDoc. These are
	/// the pieces the player looks at, so they get to be real objects with
	/// real art rather than anything procedural.
	/// </summary>
	void SpawnHalves( Collision c )
	{
		var prop = Components.Get<Prop>();
		if ( prop.IsValid() )
		{
			prop.CreateGibs( wasImpact: true );
			return;
		}

		Log.Warning( $"{this}: no Prop component, so no authored shell halves. " +
			"Add break_list_piece entries to the egg model in ModelDoc." );
	}

	/// <summary>
	/// Small fragments as collision-enabled particles, not rigidbodies - at
	/// inch scale a 0.15in rigidbody jitters and tunnels and costs more than
	/// it is worth. Only the ones that come to rest in the pool matter, and
	/// that is an exact grid lookup rather than a guess.
	/// </summary>
	bool SpawnFragments( Collision c, int count )
	{
		if ( count <= 0 ) return false;

		var effect = Components.GetOrCreate<ParticleEffect>();
		effect.MaxParticles = 24;
		effect.Lifetime = 20.0f;
		effect.Collision = true;
		effect.CollisionRadius = 0.06f;
		effect.Bounce = 0.25f;
		effect.Friction = 0.9f;
		effect.ApplyRotation = true;

		var renderer = Components.GetOrCreate<ParticleModelRenderer>();

		var landedInFood = false;

		for ( int i = 0; i < count; i++ )
		{
			var p = effect.Emit( c.Contact.Point + Vector3.Random * 0.15f, 0.0f );
			if ( p is null ) continue;

			// Fragments come off along the impact normal, scattered. Shatters
			// throw them further, which is how the pan ends up contaminated.
			p.Velocity = (c.Contact.Normal + Vector3.Random * 0.8f).Normal
				* Game.Random.Float( 8.0f, 34.0f );

			p.Size = Vector3.One * Game.Random.Float( 0.05f, 0.16f );

			// Predictive only - the definitive test is done on rest, below.
			if ( Field.IsValid() && Field.IsInPool( c.Contact.Point ) )
				landedInFood = true;
		}

		return landedInFood;
	}

	/// <summary>
	/// Hand the contents over: the yolk becomes its own sac, the white becomes
	/// pour particles that land in the field.
	/// </summary>
	void SpawnContents( Collision c, float fraction )
	{
		var spawn = WorldPosition + Vector3.Up * 0.2f;

		if ( YolkPrefab.IsValid() )
		{
			var yolk = YolkPrefab.Clone( spawn );
			var sac = yolk.Components.Get<EggYolk>( true );

			if ( sac.IsValid() )
			{
				sac.Field = Field;

				// A hard crack stresses the membrane on the way out. This is
				// why slamming an egg gets you a broken yolk even when the
				// shell comes off cleanly.
				sac.Freshness *= MathX.Lerp( 1.0f, 0.55f,
					(MathF.Abs( c.Contact.NormalSpeed ) / ShatterThreshold).Clamp( 0, 1 ) );
			}
		}

		var pour = Components.GetOrCreate<EggPour>();
		pour.Field = Field;
		pour.Begin( spawn, fraction );
	}
}
