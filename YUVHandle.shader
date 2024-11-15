/*******************************************************
 * 项目来源: 开源项目 [Vaporwave](https://github.com/itorr/vaporwave?tab=readme-ov-file)
 * 
 * Project Source: Open-source project [Vaporwave](https://github.com/itorr/vaporwave?tab=readme-ov-file)
 *******************************************************/

Shader "Vaporwave/YUVHandle"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

#include "UnityCG.cginc"

struct appdata
{
    float4 vertex : POSITION;
    float2 uv : TEXCOORD0;
};

struct v2f
{
    float2 uv : TEXCOORD0;

    float4 vertex : SV_POSITION;
};

sampler2D _MainTex;
float4 _MainTex_ST;
float _ShiftX;
float _ShiftY;
float _ShiftU;
float _ShiftV;
float _Level;
float _Contrast;
float _Light;
float _DarkFade;
float _BrightFade;
float _VividU;
float _VividV;

v2f vert(appdata v)
{
    v2f o;
    o.vertex = UnityObjectToClipPos(v.vertex);
    o.uv = TRANSFORM_TEX(v.uv, _MainTex);

    return o;
}
float LevelLow(float origin)
{
    return round(origin / _Level) * _Level;

}

void UVshifting(inout float3 yuv)
{
    yuv.x = ((yuv.x - 128) * _Contrast + 128);
    yuv.x *= _Light;
    yuv.x = max(yuv.x, _DarkFade);
    yuv.x = min(yuv.x, 255 - _BrightFade);
    yuv.y = LevelLow(min(255, (yuv.y - 128) * _VividU + _ShiftU + 128));
    yuv.z = LevelLow(min(255, (yuv.z - 128) * _VividV + _ShiftV + 128));

}

float4 frag(v2f i) : SV_Target
{

    float3 yuv = float3(tex2D(_MainTex, float2(i.uv.x, i.uv.y)).x, tex2D(_MainTex, float2(i.uv.x + _ShiftX, i.uv.y)).y, tex2D(_MainTex, float2(i.uv.x, i.uv.y - _ShiftY)).z);
    UVshifting(yuv);
    
    return float4(yuv, 0);
   
}
            ENDCG
        }
    }
}