//
// Egg white.
//
// Not opaque, and not glass. Fresh albumen is a thin refractive layer with a
// faint blue-green cast that goes milky and forward-scattering wherever the
// thick phase is deep. Almost all of the readability comes from three things
// and none of them are the diffuse colour:
//
//   1. absorption over thickness, so the skirt is nearly clear and the mound
//      is cloudy - this alone separates thin from thick without any extra data
//   2. a strong Fresnel specular sheet, because it is wet
//   3. a dark meniscus at the contact line, which is what makes the pool sit
//      ON the pan instead of looking painted onto it
//
// Vertex feed, written by EggFluidRenderer:
//   TexCoord0.xy  planar UV
//   TexCoord0.z   total depth, inches
//   TexCoord0.w   thick fraction 0..1
//   Color.r       yolk fraction 0..1
//   Color.a       edge proximity 0..1
//
HEADER
{
	Description = "Egg white - thickness-absorbing translucent fluid";
}

FEATURES
{
	#include "common/features.hlsl"
}

MODES
{
	Forward();
	Depth();
}

COMMON
{
	#include "common/shared.hlsl"
}

struct VertexInput
{
	#include "common/vertexinput.hlsl"
};

struct PixelInput
{
	#include "common/pixelinput.hlsl"
};

VS
{
	#include "common/vertex.hlsl"

	PixelInput MainVs( VertexInput i )
	{
		PixelInput o = ProcessVertex( i );
		return FinalizeVertex( o );
	}
}

PS
{
	#include "common/pixel.hlsl"

	// Standard alpha blend. Depth write off - the pool is a translucent
	// surface and must not occlude the yolk sitting in it.
	RenderState( BlendEnable, true );
	RenderState( SrcBlend, SRC_ALPHA );
	RenderState( DstBlend, INV_SRC_ALPHA );
	RenderState( BlendOp, ADD );
	RenderState( DepthWriteEnable, false );

	// Beer-Lambert absorption coefficients, per inch. Egg white absorbs red
	// slightly faster than blue, which is why a deep pool of it reads faintly
	// green rather than neutral grey. The effect is subtle and it is the
	// difference between "egg" and "milk".
	float3 g_vAbsorption < UiType( Color ); Default3( 0.85, 0.55, 0.62 ); >;

	// How fast the thick phase turns milky with depth. This is the mound.
	float g_flScatterDensity < Default( 5.2 ); Range( 0.0, 20.0 ); >;

	float3 g_vScatterTint < UiType( Color ); Default3( 0.97, 0.96, 0.90 ); >;

	// Refraction offset in screen texels per inch of depth.
	float g_flRefraction < Default( 22.0 ); Range( 0.0, 80.0 ); >;

	float g_flRoughness < Default( 0.06 ); Range( 0.0, 1.0 ); >;

	// How dark the contact line goes. The meniscus is a real optical effect -
	// light entering the wedge at the rim gets trapped - and eyes are very
	// good at spotting when it is missing.
	float g_flMeniscus < Default( 0.55 ); Range( 0.0, 1.0 ); >;

	CreateTexture2D( g_tFrameBuffer ) < Attribute( "FrameTexture" ); SrgbRead( true ); Filter( BILINEAR ); AddressU( CLAMP ); AddressV( CLAMP ); >;

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		float depth      = max( i.vTextureCoords.z, 0.0 );
		float thickFrac  = saturate( i.vTextureCoords.w );
		float edge       = saturate( i.vVertexColor.a );

		Material m = Material::Init( i );
		m.Roughness = g_flRoughness;
		m.Metalness = 0.0;

		// Material::Init has already resolved world and screen position for
		// us - no need to reassemble them from the offset position.
		float3 normal   = normalize( m.Normal );
		float3 viewDir  = normalize( g_vCameraPositionWs - m.WorldPosition );
		float  ndotv    = saturate( dot( normal, viewDir ) );

		// --- what is behind the fluid -----------------------------------
		//
		// Offset the background read by the surface normal, scaled by how much
		// fluid the ray has to cross. A flat film barely bends anything; the
		// side of the mound bends a lot, and that warping of the pan beneath
		// is most of what makes it look like there is volume there.

		float2 screenUv = m.ScreenPosition.xy * g_vFrameBufferCopyInvSizeAndUvScale.xy;
		float2 offset   = normal.xy * depth * g_flRefraction * g_vFrameBufferCopyInvSizeAndUvScale.xy;

		float3 behind = Tex2D( g_tFrameBuffer, screenUv + offset ).rgb;

		// --- absorption --------------------------------------------------
		//
		// Beer-Lambert over the path length. The path is longer at grazing
		// angles, which is why the near edge of a puddle is more saturated
		// than the middle even though it is shallower.

		float pathLength = depth / max( ndotv, 0.15 );
		float3 transmitted = behind * exp( -g_vAbsorption * pathLength );

		// --- scattering ---------------------------------------------------
		//
		// Only the thick phase scatters. Thin albumen is essentially clear,
		// and letting it stay clear is what makes the skirt read as a
		// separate substance rather than as a thin bit of the same one.

		float scatter = 1.0 - exp( -pathLength * g_flScatterDensity * thickFrac );
		float3 body   = lerp( transmitted, g_vScatterTint, scatter * 0.85 );

		// --- meniscus -----------------------------------------------------
		body *= lerp( 1.0, 1.0 - g_flMeniscus, edge * edge );

		// --- the wet sheet -------------------------------------------------
		//
		// Fresnel specular on top of everything. This is the single biggest
		// contributor to "fresh egg" and it is worth being generous with it.

		m.Albedo = body;
		m.Opacity = 1.0;

		float3 shaded = ShadingModelStandard::Shade( m ).rgb;

		float fresnel = pow( 1.0 - ndotv, 5.0 );
		shaded += fresnel * 0.35;

		// Fade the very shallowest film out rather than ending it on a hard
		// polygon edge - the field's contact-line threshold is a grid cell
		// wide and would otherwise be visible as a staircase.
		float alpha = saturate( depth * 90.0 );

		return float4( shaded, alpha );
	}
}
