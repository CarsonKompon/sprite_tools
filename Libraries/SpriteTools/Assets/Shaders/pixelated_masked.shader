
HEADER
{
	Description = "";
}

FEATURES
{
	#include "common/features.hlsl"
}

MODES
{
	VrForward();
	Depth(); 
	ToolsVis( S_MODE_TOOLS_VIS );
	ToolsWireframe( "vr_tools_wireframe.shader" );
	ToolsShadingComplexity( "tools_shading_complexity.shader" );
}

COMMON
{
	#ifndef S_ALPHA_TEST
	#define S_ALPHA_TEST 1
	#endif
	#ifndef S_TRANSLUCENT
	#define S_TRANSLUCENT 0
	#endif
	
	#include "common/shared.hlsl"
	#include "procedural.hlsl"

	#define S_UV2 1
	#define CUSTOM_MATERIAL_INPUTS
}

struct VertexInput
{
	#include "common/vertexinput.hlsl"
	float4 vColor : COLOR0 < Semantic( Color ); >;
};

struct PixelInput
{
	#include "common/pixelinput.hlsl"
	float3 vPositionOs : TEXCOORD14;
	float3 vNormalOs : TEXCOORD15;
	float4 vTangentUOs_flTangentVSign : TANGENT	< Semantic( TangentU_SignV ); >;
	float4 vColor : COLOR0;
	float4 vTintColor : COLOR1;
};

VS
{
	#include "common/vertex.hlsl"

	PixelInput MainVs( VertexInput v )
	{
		PixelInput i = ProcessVertex( v );
		i.vPositionOs = v.vPositionOs.xyz;
		i.vColor = v.vColor;

		ExtraShaderData_t extraShaderData = GetExtraPerInstanceShaderData( v );
		i.vTintColor = extraShaderData.vTint;

		VS_DecodeObjectSpaceNormalAndTangent( v, i.vNormalOs, i.vTangentUOs_flTangentVSign );

		return FinalizeVertex( i );
	}
}

PS
{
	#include "common/pixel.hlsl"
	
	SamplerState g_sSampler0 < Filter( POINT ); AddressU( CLAMP ); AddressV( CLAMP ); >;
	CreateInputTexture2D( Texture, Srgb, 8, "None", "_color", "Texture,1/,0/0", Default4( 1.00, 1.00, 1.00, 1.00 ) );
	Texture2D g_tTexture < Channel( RGBA, Box( Texture ), Srgb ); OutputFormat( BC7 ); SrgbRead( True ); >;
	float g_flHueshift < UiGroup( "Hue,2/,0/0" ); Default1( 0 ); Range1( 0, 1 ); >;
	float2 g_vTiling < UiGroup( "Texture Coordinates,5/,0/0" ); Default2( 1,1 ); Range2( 0,0, 1,1 ); >;
	float2 g_vOffset < UiGroup( "Texture Coordinates,5/,0/0" ); Default2( 0,0 ); Range2( 0,0, 1,1 ); >;
	float g_flFill < UiGroup( ",0/,0/0" ); Default1( 0 ); Range1( 0, 1 ); >;
	float g_flSmoothStepMin < UiGroup( "Translucent,1/,0/1" ); Default1( 0 ); Range1( 0, 1 ); >;
	float g_flSmoothStepMax < UiGroup( "Translucent,1/,0/2" ); Default1( 1 ); Range1( 0, 1 ); >;
		
	float3 RGB2HSV( float3 c )
	{
	    float4 K = float4( 0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0 );
	    float4 p = lerp( float4( c.bg, K.wz ), float4( c.gb, K.xy ), step( c.b, c.g ) );
	    float4 q = lerp( float4( p.xyw, c.r ), float4( c.r, p.yzx ), step( p.x, c.r ) );
	
	    float d = q.x - min( q.w, q.y );
	    float e = 1.0e-10;
	    return float3( abs( q.z + ( q.w - q.y ) / ( 6.0 * d + e ) ), d / ( q.x + e ), q.x );
	}
	
	float3 HSV2RGB( float3 c )
	{
	    float4 K = float4( 1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0 );
	    float3 p = abs( frac( c.xxx + K.xyz ) * 6.0 - K.www );
	    return c.z * lerp( K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y );
	}
	
	float4 MainPs( PixelInput i ) : SV_Target0
	{
		Material m = Material::Init();
		m.Albedo = float3( 1, 1, 1 );
		m.Normal = float3( 0, 0, 1 );
		m.Roughness = 1;
		m.Metalness = 0;
		m.AmbientOcclusion = 1;
		m.TintMask = 1;
		m.Opacity = 1;
		m.Emission = float3( 0, 0, 0 );
		m.Transmission = 0;
		
		float l_0 = g_flHueshift;
		float4 l_1 = i.vTintColor;
		float2 l_2 = i.vTextureCoords.xy * float2( 1, 1 );
		float2 l_3 = g_vTiling;
		float2 l_4 = g_vOffset;
		float2 l_5 = TileAndOffsetUv( l_2, l_3, l_4 );
		float4 l_6 = Tex2DS( g_tTexture, g_sSampler0, l_5 );
		float4 l_7 = float4( 1, 1, 1, 1 );
		float l_8 = g_flFill;
		float4 l_9 = saturate( lerp( l_6, min( 1.0f, (l_6) + (l_7) ), l_8 ) );
		float4 l_10 = l_1 * l_9;
		float3 l_11 = RGB2HSV( l_10 );
		float l_12 = l_11.x;
		float l_13 = l_0 + l_12;
		float l_14 = l_11.y;
		float l_15 = l_11.z;
		float4 l_16 = float4( l_13, l_14, l_15, 0 );
		float3 l_17 = HSV2RGB( l_16 );
		float l_18 = g_flSmoothStepMin;
		float l_19 = g_flSmoothStepMax;
		float l_20 = smoothstep( l_18, l_19, l_6.a );
		float l_21 = saturate( l_20 );
		
		m.Albedo = l_17;
		m.Opacity = l_21;
		m.Roughness = 1;
		m.Metalness = 0;
		m.AmbientOcclusion = 1;
		
		m.AmbientOcclusion = saturate( m.AmbientOcclusion );
		m.Roughness = saturate( m.Roughness );
		m.Metalness = saturate( m.Metalness );
		m.Opacity = saturate( m.Opacity );

		// Result node takes normal as tangent space, convert it to world space now
		m.Normal = TransformNormal( m.Normal, i.vNormalWs, i.vTangentUWs, i.vTangentVWs );

		// for some toolvis shit
		m.WorldTangentU = i.vTangentUWs;
		m.WorldTangentV = i.vTangentVWs;
        m.TextureCoords = i.vTextureCoords.xy;
		
		return ShadingModelStandard::Shade( i, m );
	}
}
