using Sandbox.Rendering;

namespace Stark.Food;

/// <summary>
/// Copies the opaque frame into a texture the fluid shader can read, so the
/// white can refract what is underneath it.
///
/// Nothing exotic - one command list scheduled after the opaque pass and
/// before anything transparent draws. The pan, the hob and the counter are all
/// opaque and so all present; the egg itself is not, which is correct, because
/// fluid refracting its own far surface is not a thing worth paying for at
/// this scale.
///
/// Put this on the camera. Without it <c>egg_white.shader</c> samples an empty
/// texture and the pool renders as flat tinted grey - which is the usual
/// explanation when someone says the fluid "looks like plastic".
/// </summary>
[Title( "Egg Frame Grab" )]
[Category( "Food" )]
[Icon( "photo_camera" )]
public sealed class EggFrameGrab : Component, Component.DontExecuteOnServer
{
	[RequireComponent]
	public CameraComponent Camera { get; set; }

	/// <summary>
	/// Must match the Attribute() name in egg_white.shader.
	/// </summary>
	[Property]
	public string TargetName { get; set; } = "FrameTexture";

	CommandList _commands;

	protected override void OnEnabled()
	{
		_commands = new CommandList( "EggFrameGrab" );
		_commands.GrabFrameTexture( TargetName );

		// After opaque, before transparent. Order is low so that anything else
		// wanting a grab this frame gets a scene that already contains ours.
		Camera.AddCommandList( _commands, Stage.AfterOpaque, -100 );
	}

	protected override void OnDisabled()
	{
		if ( _commands is not null )
			Camera.RemoveCommandList( _commands );

		_commands = null;
	}
}
