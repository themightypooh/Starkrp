using System;
using System.Collections.Generic;
using System.Linq;

namespace Stark.Food;

/// <summary>
/// The half-second of airborne egg between the shell and the pan.
///
/// This is the one place particles genuinely win. Falling albumen is stringy
/// and stretched and none of that is a height field; a stretched billboard is
/// exactly the right tool. But the moment a particle touches down it stops
/// being a particle - it dies and hands its volume to the
/// <see cref="EggFluidField"/>, which is the thing that can actually hold a
/// pool for the next ten minutes without costing anything.
///
/// Thick and thin albumen pour differently and are emitted differently:
/// thin is fast, low-drag and lands wide; thick is slow, heavily damped, and
/// lands nearly where it left. That difference at pour time is what puts the
/// skirt outside the mound before the field solver has done anything at all.
/// </summary>
[Title( "Egg Pour" )]
[Category( "Food" )]
[Icon( "water_drop" )]
public sealed class EggPour : Component
{
	[Property] public EggFluidField Field { get; set; }

	/// <summary>
	/// Cubic inches of white in a US large egg: ~33g at roughly water
	/// density, which is a bit over 2 in³.
	/// </summary>
	[Property, Range( 0.5f, 6.0f )]
	public float WhiteVolume { get; set; } = 2.05f;

	/// <summary>
	/// Fraction of the white that is thick albumen. ~0.6 in a fresh egg,
	/// dropping toward 0.3 in an old one as thick breaks down into thin. This
	/// is the single number that separates "tight mound" from "flat puddle".
	/// </summary>
	[Property, Range( 0.15f, 0.8f )]
	public float ThickFraction { get; set; } = 0.6f;

	[Property, Range( 0.2f, 3.0f )]
	public float PourDuration { get; set; } = 0.55f;

	ParticleEffect _thin;
	ParticleEffect _thick;

	float _thinVolumePerParticle;
	float _thickVolumePerParticle;

	bool _pouring;
	float _elapsed;
	Vector3 _source;
	float _fraction = 1.0f;

	const int ThinParticles = 90;
	const int ThickParticles = 55;

	/// <summary>
	/// Start pouring. <paramref name="fraction"/> is how much of the contents
	/// actually made it - a shattered egg loses some over the side.
	/// </summary>
	public void Begin( Vector3 worldSource, float fraction = 1.0f )
	{
		Field ??= Scene.GetAllComponents<EggFluidField>().FirstOrDefault();

		_source = worldSource;
		_fraction = fraction.Clamp( 0.0f, 1.0f );
		_elapsed = 0.0f;
		_pouring = true;

		var white = WhiteVolume * _fraction;
		_thickVolumePerParticle = white * ThickFraction / ThickParticles;
		_thinVolumePerParticle = white * (1.0f - ThickFraction) / ThinParticles;

		_thin = CreatePhase( EggPhase.Thin );
		_thick = CreatePhase( EggPhase.Thick );
	}

	ParticleEffect CreatePhase( EggPhase phase )
	{
		var go = new GameObject( true, phase == EggPhase.Thin ? "egg_thin" : "egg_thick" );
		go.SetParent( GameObject );
		go.WorldPosition = _source;

		var fx = go.Components.Create<ParticleEffect>();
		fx.MaxParticles = phase == EggPhase.Thin ? ThinParticles : ThickParticles;

		// Thin runs off the shell quickly and is gone; thick strings out and
		// takes its time. Lifetimes here are generous - the collision handler
		// kills them the moment they land, so these are only a backstop for
		// anything that misses the pan entirely.
		fx.Lifetime = phase == EggPhase.Thin ? 1.6f : 2.6f;

		fx.Damping = phase == EggPhase.Thin ? 0.4f : 3.2f;
		fx.Force = true;
		fx.ForceDirection = Vector3.Down * 386.0f;   // inches/s^2
		fx.ForceScale = phase == EggPhase.Thin ? 1.0f : 0.55f;

		// The engine traces these for us and writes HitPos/HitNormal onto the
		// particle, which is exactly the handoff the field wants.
		fx.Collision = true;
		fx.CollisionRadius = phase == EggPhase.Thin ? 0.05f : 0.09f;
		fx.Bounce = 0.0f;
		fx.Friction = 1.0f;

		// Kill on contact. The engine sets Age to max on a fatal collision,
		// which lands the particle in Terminate on the next PostStep - and
		// that is a single-threaded callback with HitPos still populated,
		// which is exactly the handoff EggDepositOnLand wants.
		fx.DieOnCollisionChance = 1.0f;

		var renderer = go.Components.Create<ParticleSpriteRenderer>();
		renderer.Additive = false;
		renderer.Lighting = true;
		renderer.Shadows = false;
		renderer.FaceVelocity = true;
		renderer.DepthFeather = 4.0f;
		renderer.Scale = phase == EggPhase.Thin ? 0.35f : 0.6f;

		var deposit = go.Components.Create<EggDepositOnLand>();
		deposit.Field = Field;
		deposit.Phase = phase;
		deposit.VolumePerParticle = phase == EggPhase.Thin
			? _thinVolumePerParticle
			: _thickVolumePerParticle;

		return fx;
	}

	protected override void OnUpdate()
	{
		if ( !_pouring ) return;

		_elapsed += Time.Delta;

		// Thin leaves first and fastest - it is already running down the shell
		// while the thick albumen is still deciding to move. Emitting on that
		// schedule is what lays the skirt down before the mound arrives, which
		// is the correct order and looks wrong if you reverse it.
		Emit( _thin, ThinParticles, 0.0f, PourDuration * 0.55f, 18.0f );
		Emit( _thick, ThickParticles, PourDuration * 0.2f, PourDuration, 7.0f );

		if ( _elapsed > PourDuration + 3.0f )
			_pouring = false;
	}

	int _thinEmitted;
	int _thickEmitted;

	void Emit( ParticleEffect fx, int total, float start, float end, float speed )
	{
		if ( !fx.IsValid() ) return;

		ref var emitted = ref (fx == _thin ? ref _thinEmitted : ref _thickEmitted);

		var t = ((_elapsed - start) / MathF.Max( end - start, 0.0001f )).Clamp( 0.0f, 1.0f );
		var want = (int)(t * total);

		while ( emitted < want )
		{
			emitted++;

			var p = fx.Emit( _source + Vector3.Random * 0.12f, 0.0f );
			if ( p is null ) continue;

			p.Velocity = (Vector3.Down + Vector3.Random * 0.35f).Normal
				* speed * Game.Random.Float( 0.6f, 1.4f );

			p.Size = Vector3.One * Game.Random.Float( 0.08f, 0.2f );
		}
	}
}

/// <summary>
/// Converts particle landings into field deposits.
///
/// Deliberately hooked to <c>OnParticleDestroyed</c> and not to the per-particle
/// step. The step callback runs on the particle worker threads - the engine
/// parallelises it hard, and the field is not remotely thread safe. Terminate
/// runs during PostStep on the main thread with <c>HitPos</c> still populated,
/// so this is both the correct place and the cheap one.
/// </summary>
[Title( "Egg Deposit On Land" )]
[Category( "Food" )]
[Icon( "south" )]
public sealed class EggDepositOnLand : ParticleController
{
	[Property] public EggFluidField Field { get; set; }
	[Property] public EggPhase Phase { get; set; }
	[Property] public float VolumePerParticle { get; set; } = 0.01f;

	protected override void OnParticleDestroyed( Particle p )
	{
		// Also fires for particles that simply timed out in midair - those
		// missed the pan and are not the field's problem.
		if ( p.HitTime <= 0.0f || !Field.IsValid() )
			return;

		// Thick lands where it fell. Thin arrives with momentum still in it and
		// smears, so it goes down over a wider footprint - which is half of why
		// the skirt ends up outside the mound before the solver does anything.
		var radius = Phase == EggPhase.Thin ? 0.22f : 0.13f;

		Field.Deposit( p.HitPos, VolumePerParticle, Phase, radius );
	}
}
