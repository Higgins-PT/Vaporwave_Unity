/*******************************************************
 * 项目来源: 开源项目 [Vaporwave](https://github.com/itorr/vaporwave?tab=readme-ov-file)
 * 
 * Project Source: Open-source project [Vaporwave](https://github.com/itorr/vaporwave?tab=readme-ov-file)
 *******************************************************/

Shader"Vaporwave/YUV2RGB"
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

v2f vert(appdata v)
{
    v2f o;
    o.vertex = UnityObjectToClipPos(v.vertex);
    o.uv = TRANSFORM_TEX(v.uv, _MainTex);

    return o;
}

float3 yuv2rgb(float3 yuv)
{
    float y = yuv.x;
    float u = yuv.y - 128.0;
    float v = yuv.z - 128.0;

    float r = y + 1.4075 * v;
    float g = y - 0.3455 * u - 0.7169 * v;
    float b = y + 1.7790 * u;


    r = floor(r);
    g = floor(g);
    b = floor(b);

    r = clamp(r, 0.0, 255.0);
    g = clamp(g, 0.0, 255.0);
    b = clamp(b, 0.0, 255.0);

    return float3(r, g, b);
}
float4 frag(v2f i) : SV_Target
{


    float4 yuv = tex2D(_MainTex, float2(i.uv.x, i.uv.y));
    return float4(yuv2rgb(yuv.rgb) / 255, 0);
   
}
            ENDCG
        }
    }
}