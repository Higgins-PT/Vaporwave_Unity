Shader"Vaporwave/Convolute"
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
float2 _TexDeltaSize;
float4x4 _ConvoluteCore;

v2f vert(appdata v)
{
    v2f o;
    o.vertex = UnityObjectToClipPos(v.vertex);
    o.uv = TRANSFORM_TEX(v.uv, _MainTex);
    return o;
}

float4 frag(v2f i) : SV_Target
{
    float3 result = float3(0.0, 0.0, 0.0);

    float2 offsets[9] =
    {
        float2(-_TexDeltaSize.x, -_TexDeltaSize.y), 
                    float2(0.0, -_TexDeltaSize.y),
                    float2(_TexDeltaSize.x, -_TexDeltaSize.y), 
                    float2(-_TexDeltaSize.x, 0.0),
                    float2(0.0, 0.0), 
                    float2(_TexDeltaSize.x, 0.0),
                    float2(-_TexDeltaSize.x, _TexDeltaSize.y), 
                    float2(0.0, _TexDeltaSize.y), 
                    float2(_TexDeltaSize.x, _TexDeltaSize.y) 
    };

    int index = 0;
    for (int y = 0; y < 3; y++)
    {
        for (int x = 0; x < 3; x++)
        {
            float2 sampleUV = i.uv + offsets[index];
            float3 sampleColor = tex2D(_MainTex, sampleUV).rgb;
            result += sampleColor * _ConvoluteCore[y][x];
            index++;
        }
    }

    return float4(result, 1.0);
}
            ENDCG
        }
    }
}
