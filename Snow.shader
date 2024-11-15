Shader"Vaporwave/Snow"
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
float ScaledGaussianRandom(float3 seed, float mean, float standardDeviation)
{
    seed = frac(seed * float3(12.9898, 78.233, 37.719));
    float u1 = frac(sin(dot(seed, float3(1.0, 57.0, 113.0))) * 43758.5453);
    float u2 = frac(sin(dot(seed, float3(31.0, 97.0, 23.0))) * 43758.5453);

    float r = sqrt(-2.0 * log(u1));
    float theta = 6.28318530718 * u2;
    float gaussianRandom = r * cos(theta);


    gaussianRandom = gaussianRandom * standardDeviation + mean;

    return saturate(gaussianRandom);
}


v2f vert(appdata v)
{
    v2f o;
    o.vertex = UnityObjectToClipPos(v.vertex);
    o.uv = TRANSFORM_TEX(v.uv, _MainTex);

    return o;
}

float4 frag(v2f i) : SV_Target
{

    return ScaledGaussianRandom(float3(i.uv * 10, _Time.x), 0.5, 0.4);
}
            ENDCG
        }
    }
}
