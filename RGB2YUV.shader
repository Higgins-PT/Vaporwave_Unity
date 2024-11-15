/*******************************************************
 * 项目来源: 开源项目 [Vaporwave](https://github.com/itorr/vaporwave?tab=readme-ov-file)
 * 
 * Project Source: Open-source project [Vaporwave](https://github.com/itorr/vaporwave?tab=readme-ov-file)
 *******************************************************/

Shader"Vaporwave/RGB2YUV"
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
float3 rgb2yuv(float3 rgb)
{
    float y = rgb.r * 0.299 + rgb.g * 0.587 + rgb.b * 0.114;
    float u = rgb.r * -0.168736 + rgb.g * -0.331264 + rgb.b * 0.5 + 128.0;
    float v = rgb.r * 0.5 + rgb.g * -0.418688 + rgb.b * -0.081312 + 128.0;


    y = floor(y);
    u = floor(u);
    v = floor(v);

    return float3(y, u, v);
}

float4 frag(v2f i) : SV_Target
{

    float4 rgb = tex2D(_MainTex, float2(i.uv.x, i.uv.y));
    return float4(rgb2yuv(rgb.rgb * 255), 0);
   
}
            ENDCG
        }
    }
}