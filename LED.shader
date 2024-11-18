Shader"Vaporwave/EvLED"
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
            // make fog work
            #pragma multi_compile_fog

#include "UnityCG.cginc"
float _LEDLightLevel;
float _LEDResolutionLevel;
half4 _Color;
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
    int group = 4 * max(_LEDResolutionLevel, 1);
    half2 index = float2(i.vertex.x % group, i.vertex.y % group);
    float2 onePiece = float2(group / _ScreenParams.x, group / _ScreenParams.y);
    float flag = step(_LEDResolutionLevel, 0.5);
    float2 newUV = lerp(float2(round(i.uv.x / onePiece.x) * onePiece.x, round(i.uv.y / onePiece.y) * onePiece.y), i.uv, flag);
    float4 col = tex2D(_MainTex, newUV);
    half2 disTo = float2(index.x / group, index.y / group);
    float4 col_temp = col * float4(step(disTo.x, 0.25), step(disTo.x, 0.5) * step(0.25, disTo.x), step(0.5, disTo.x) * step(disTo.x, 0.75), 1) * step(0.25, disTo.y);
    col = lerp(col_temp, col, flag);
    return col;
}
            ENDCG
        }
    }
}
