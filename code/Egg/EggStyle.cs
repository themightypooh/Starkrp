using System;

namespace Stark.Food;

/// <summary>
/// How far the egg is allowed to lie.
///
/// The simulation underneath this is built from measurements - real albumen
/// fractions, real yield stresses, a real 17g yolk. That was the right way to
/// build it and it is the wrong way to ship it. A physically honest egg in a
/// first-person kitchen reads as a small grey-ish smear about two inches
/// across that does almost nothing for a quarter of a second, because that is
/// what one actually is. Nobody looks at it.
///
/// So the sim stays honest and this sits on top of it as a single dial. Every
/// number here is a multiplier on a physically-derived constant, never a
/// replacement for one, which means the dial can always be wound back to 0 to
/// see what the truth was.
///
/// The exaggerations are the classic ones, and they are chosen because they
/// survive being seen for half a second at arm's length:
///
///   - <b>bigger</b>. The egg is oversized; the mound is taller than it has
///     any right to be. Volume is still conserved, it is just carried in a
///     shape that reads.
///   - <b>slower where it matters, faster where it doesn't</b>. A beat of
///     hitstop on the crack, then a pour that is over before the player's
///     interest is.
///   - <b>wobblier</b>. A real yolk's wobble is two or three low-amplitude
///     cycles and it is gone. Ours is soft, underdamped, and rings.
///   - <b>harder edges</b>. A fat beaded rim and a dark meniscus, because the
///     silhouette is the only part of a puddle a player can read at speed.
///   - <b>more saturated</b>. Real yolk is duller than anyone remembers and
///     real white is nearly colourless. Both get pushed toward the colour
///     people think they saw.
///   - <b>more generous</b>. The clean-crack window widens, because losing a
///     breakfast to a physically correct 4 in/s overswing is not a mechanic,
///     it is a bug with a rationale.
///
/// Put one of these in the scene to set the house style. Without one every
/// system falls back to <see cref="Default"/>, which is not the honest
/// setting - shipping default is <see cref="EggStylePreset.Cookbook"/>.
/// </summary>
[Title( "Egg Style" )]
[Category( "Food" )]
[Icon( "auto_awesome" )]
public sealed class EggStyle : Component
{
	[Property]
	public EggStylePreset Preset { get; set; } = EggStylePreset.Cookbook;

	/// <summary>
	/// 0 is the measured egg. 1 is a cartoon. Set <see cref="Preset"/> to
	/// <see cref="EggStylePreset.Custom"/> to drive it by hand.
	/// </summary>
	[Property, Range( 0.0f, 1.0f ), ShowIf( nameof( Preset ), EggStylePreset.Custom )]
	public float Caricature { get; set; } = 0.55f;

	static EggStyle _active;

	protected override void OnEnabled() => _active = this;

	protected override void OnDisabled()
	{
		if ( _active == this ) _active = null;
	}

	/// <summary>The style the whole egg system reads from.</summary>
	public static EggProfile Current
		=> _active.IsValid() ? _active.Profile : Default;

	/// <summary>Shipping default when no <see cref="EggStyle"/> is in the scene.</summary>
	public static EggProfile Default => EggProfile.For( 0.55f );

	public EggProfile Profile => EggProfile.For( Preset switch
	{
		EggStylePreset.Documentary => 0.0f,
		EggStylePreset.Cookbook => 0.55f,
		EggStylePreset.SaturdayMorning => 1.0f,
		_ => Caricature
	} );
}

public enum EggStylePreset
{
	/// <summary>No lies. The sim as measured - useful for checking the sim, not for playing.</summary>
	Documentary,
	/// <summary>Shipping default. Reads as an egg from across a kitchen and still behaves like one.</summary>
	Cookbook,
	/// <summary>Everything at once. Big, slow, wobbly, luridly yellow.</summary>
	SaturdayMorning,
	Custom
}

/// <summary>
/// The dial, expanded into the multipliers each system actually wants. All of
/// them are 1.0 (or 0.0, for the additive ones) at <c>Caricature = 0</c>, so
/// the honest egg is exactly the egg this system was built as.
/// </summary>
public readonly struct EggProfile
{
	public float Caricature { get; init; }

	/// <summary>Linear scale on the egg, the yolk and the pool. Cooking games all do this.</summary>
	public float Size { get; init; }

	/// <summary>Multiplier on thick albumen's yield slope. Taller, tighter mound.</summary>
	public float Mound { get; init; }

	/// <summary>Multiplier on thin albumen's flow. A skirt you can actually see.</summary>
	public float Skirt { get; init; }

	/// <summary>Multiplier on cohesion. Fat beaded rim instead of a feathered one.</summary>
	public float Rim { get; init; }

	/// <summary>Multiplier on per-cell flow noise. More fingering on the contact line.</summary>
	public float Fingering { get; init; }

	/// <summary>Extra vertical exaggeration on the rendered surface, on top of the renderer's own.</summary>
	public float Height { get; init; }

	/// <summary>Multiplier on membrane stiffness. Below 1 the yolk goes soft and rings.</summary>
	public float MembraneStiffness { get; init; }

	/// <summary>Multiplier on node damping exponent. Below 1 the wobble outlives the landing.</summary>
	public float WobbleRing { get; init; }

	/// <summary>Multiplier on how far a squash bulges sideways.</summary>
	public float Squash { get; init; }

	/// <summary>Seconds of hitstop on a crack. The beat that sells the impact.</summary>
	public float HitStop { get; init; }

	/// <summary>Multiplier on the pour's duration. Under 1 is snappier.</summary>
	public float PourTime { get; init; }

	/// <summary>Multiplier on pour particle size. Fatter strings read at distance.</summary>
	public float PourBody { get; init; }

	/// <summary>Widens the clean-crack window. 1.0 is the measured one.</summary>
	public float Forgiveness { get; init; }

	/// <summary>Colour saturation push, handed to both egg shaders.</summary>
	public float ColourPunch { get; init; }

	/// <summary>Meniscus darkening at the contact line, handed to the white shader.</summary>
	public float Meniscus { get; init; }

	/// <summary>
	/// Build a profile. Every curve here is a straight lerp except where a
	/// straight lerp gave something that either did nothing until 0.7 or fell
	/// apart before 0.3 - those are noted.
	/// </summary>
	public static EggProfile For( float caricature )
	{
		var t = caricature.Clamp( 0.0f, 1.0f );

		return new EggProfile
		{
			Caricature = t,

			// 1.2x is the near-universal cooking-game fudge and it is already
			// baked into the model; this is on top of that. 1.55 total is
			// about where an egg stops looking like a pebble in first person.
			Size = MathX.Lerp( 1.0f, 1.3f, t ),

			// The mound is the shape the whole thing is recognised by, so it
			// gets the largest single push in the system.
			Mound = MathX.Lerp( 1.0f, 2.2f, t ),
			Skirt = MathX.Lerp( 1.0f, 1.4f, t ),
			Rim = MathX.Lerp( 1.0f, 2.4f, t ),
			Fingering = MathX.Lerp( 1.0f, 1.6f, t ),
			Height = MathX.Lerp( 1.0f, 1.7f, t ),

			// Softening the membrane and unwinding the damping together is
			// what turns two dead cycles into a wobble. Doing either alone
			// gets you jelly or a metronome.
			MembraneStiffness = MathX.Lerp( 1.0f, 0.45f, t ),
			WobbleRing = MathX.Lerp( 1.0f, 0.35f, t ),
			Squash = MathX.Lerp( 1.0f, 1.8f, t ),

			// Squared: a short hitstop is imperceptible, so there is no point
			// spending the first half of the dial on frames nobody feels.
			HitStop = 0.10f * t * t,

			PourTime = MathX.Lerp( 1.0f, 0.7f, t ),
			PourBody = MathX.Lerp( 1.0f, 1.45f, t ),
			Forgiveness = MathX.Lerp( 1.0f, 1.6f, t ),
			ColourPunch = MathX.Lerp( 0.0f, 1.0f, t ),
			Meniscus = MathX.Lerp( 1.0f, 2.0f, t )
		};
	}
}

/// <summary>
/// A beat of hitstop. Dips <see cref="Scene.TimeScale"/> and winds it back
/// over unscaled time, so the crack lands as an impact rather than as an
/// event that merely occurred. Self-deleting; spawn one and forget it.
/// </summary>
[Title( "Egg Hit Stop" )]
[Category( "Food" )]
[Icon( "hourglass_bottom" )]
public sealed class EggHitStop : Component
{
	float _remaining;
	float _restore = 1.0f;

	/// <summary>Dip time on <paramref name="scene"/>. Does nothing if a dip is already running.</summary>
	public static void Play( Scene scene, float duration, float scale = 0.08f )
	{
		if ( duration <= 0.0f || !scene.IsValid() )
			return;

		if ( scene.Components.Get<EggHitStop>( FindMode.EverythingInSelfAndDescendants ).IsValid() )
			return;

		var go = scene.CreateObject();
		go.Name = "egg_hitstop";

		var stop = go.Components.Create<EggHitStop>();
		stop._restore = scene.TimeScale;
		stop._remaining = duration;

		scene.TimeScale = stop._restore * scale;
	}

	protected override void OnUpdate()
	{
		// Unscaled, or the dip would slow down its own recovery.
		_remaining -= RealTime.Delta;

		if ( _remaining > 0.0f )
			return;

		Scene.TimeScale = _restore;
		GameObject.Destroy();
	}
}
