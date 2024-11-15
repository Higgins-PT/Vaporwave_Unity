Shader"Vaporwave/GraphNoise"
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
float _DrakNoise;
float _LightNoise;
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
    float3 color = tex2D(_MainTex, i.uv).rgb;
    float light = (ScaledGaussianRandom(float3(i.uv * 10, _Time.x), 0.5, 0.4) * _LightNoise - _LightNoise / 2);
    color = color + float3(light, light, light);
    float dark = (ScaledGaussianRandom(float3(i.uv * 5, _Time.x * 2), 0.5, 0.4) * _DrakNoise - _DrakNoise / 2) * (1 - (color.x + color.y + color.z) / 3);
    color = color + float3(dark, dark, dark);
    return float4(color, 0);
}
            ENDCG
        }
    }
}
