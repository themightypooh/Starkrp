//
// Egg yolk.
//
// The standard PBR path renders a yolk as an orange rubber ball, every time.
// A yolk is not a rubber ball: it is a dense translucent sphere inside a thin
// wet membrane, and it is strongly forward-scattering. Three departures from
// standard shading fix it:
//
//   1. wrapped diffuse - light wraps past the terminator, so the shaded side
//      stays warm and glowing instead of falling off to black
//   2. a back-lit transmission term driven by local thickness, which is what
//      makes a yolk with a lamp behind it light up like it does
//   3. a tight specular lobe for the membrane, which is much smoother than
//      the yolk body underneath it
//
// Vertex feed, written by EggYolk:
//   TexCoord0.xy  rest direction (stable UV under deformation)
//   TexCoord0.z   local thickness, 0..2, 1 = undeformed
//   TexCoord0.w   freshness 0..1
//
HEADER
{
	Description = "Egg yolk - subsurface-scattering membrane sac";
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

	// Yolk colour varies enormously with the hen's diet - pale yellow on
	// commodity feed, deep orange on pasture. Worth exposing: it is free
	// visual variety across eggs and players notice it.
	float3 g_vYolkColor  < UiType( Color ); Default3( 0.95, 0.62, 0.10 ); >;
	float3 g_vDeepColor  < UiType( Color ); Default3( 0.72, 0.26, 0.03 ); >;

	// How far light wraps past the terminator. 0 is Lambert, 1 is fully
	// wrapped. Yolk is high - around 0.6 - and it is most of the "glow".
	float g_flWrap < Default( 0.62 ); Range( 0.0, 1.0 ); >;

	float g_flTransmission < Default( 1.35 ); Range( 0.0, 4.0 ); >;

	// The membrane, not the yolk. Very smooth, very thin.
	float g_flMembraneRoughness < Default( 0.09 ); Range( 0.0, 1.0 ); >;

	float3 MainPs( PixelInput i ) : SV_Target0
	{
		float thickness = i.vTextureCoords.z;
		float freshness = i.vTextureCoords.w;

		Material m = Material::Init( i );

		float3 normal  = normalize( m.Normal );
		float3 viewDir = normalize( g_vCameraPositionWs - m.WorldPosition );
		float  ndotv   = saturate( dot( normal, viewDir ) );

		// Where the sac is squashed thin it goes lighter and more saturated,
		// where it bulges it goes deep. Deformation therefore shades itself,
		// which is what stops a wobbling yolk looking like a wobbling ball.
		float3 albedo = lerp( g_vDeepColor, g_vYolkColor, saturate( thickness ) );

		m.Albedo    = albedo;
		m.Roughness = g_flMembraneRoughness;
		m.Metalness = 0.0;

		float3 shaded = ShadingModelStandard::Shade( m ).rgb;

		// --- wrapped diffuse ------------------------------------------------
		//
		// Re-lit against the dominant light rather than replacing the standard
		// shade, so the yolk still sits in the scene's lighting but keeps the
		// soft terminator that dense scattering media have.

		// Index 0 is the brightest light in this pixel's cluster, which for a
		// yolk on a hob is the one that matters. Light::Count( m.ScreenPosition )
		// would let you loop the rest if the scene ever needs it.
		Light light = Light::From( m.WorldPosition, m.ScreenPosition, 0 );

		float ndotl   = dot( normal, light.Direction );
		float wrapped = saturate( ( ndotl + g_flWrap ) / ( 1.0 + g_flWrap ) );

		shaded += albedo * light.Color * light.Visibility * wrapped * 0.55;

		// --- transmission ---------------------------------------------------
		//
		// Light entering the far side and coming out at the camera. Falls off
		// with thickness (Beer-Lambert again) and peaks when the camera is
		// looking roughly into the light through the yolk.

		float3 backDir  = normalize( light.Direction + normal * 0.25 );
		float  backLobe = pow( saturate( dot( viewDir, -backDir ) ), 3.0 );
		float  through  = exp( -thickness * 2.4 );

		shaded += g_vYolkColor * light.Color * light.Visibility
		        * backLobe * through * g_flTransmission;

		// --- membrane -------------------------------------------------------
		//
		// Rim brightening from the wet skin. A fresh membrane is taut and
		// glossy; an old one is slack and dull, so freshness drives it.

		float fresnel = pow( 1.0 - ndotv, 4.0 );
		shaded += fresnel * lerp( 0.08, 0.45, freshness );

		return shaded;
	}
}
