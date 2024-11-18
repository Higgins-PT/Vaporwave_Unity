Shader"Vaporwave/FishEye"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
       [HDR] _Color("Color",Color)=(1,1,1,1)
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
float2 _FishEyeIntensity;
float _FishEyePow;
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

v2f vert(appdata v) 
{
    v2f o;
    o.vertex = UnityObjectToClipPos(v.vertex);
    o.uv = TRANSFORM_TEX(v.uv, _MainTex);
    return o;
}

float4 frag(v2f i) : SV_Target
{
    float2 newUV = (i.uv - 0.5) * 2; // -1 --- 1
    float2 resultUV = (1 - pow(clamp(length(newUV),0,1), _FishEyePow)) * _FishEyeIntensity * newUV;
    resultUV = i.uv - resultUV;
    float4 col = tex2D(_MainTex, resultUV);
    return col;
}
            ENDCG
        }
    }
}
