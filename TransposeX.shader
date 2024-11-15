/*******************************************************
 * 项目来源: 开源项目 [Vaporwave](https://github.com/itorr/vaporwave?tab=readme-ov-file)
 * 
 * Project Source: Open-source project [Vaporwave](https://github.com/itorr/vaporwave?tab=readme-ov-file)
 *******************************************************/

Shader"Vaporwave/TransposeX"
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
float _Interlaced;
float _TransposeX;
float _TransposePow;
float _TransposeNoise;
float2 _TexSize;
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
    float wLeft = floor(_TexSize.x * _TransposeX * pow((1 - i.uv.y), _TransposePow) * (1 + _TransposeNoise * (ScaledGaussianRandom(float3(i.uv, _Time.y), 0.5, 0.2) - 0.5))) * 4;
    float4 col = tex2D(_MainTex, float2(i.uv.x + (wLeft / _TexSize.x), i.uv.y));
    return col;
   
}
            ENDCG
        }
    }
}
