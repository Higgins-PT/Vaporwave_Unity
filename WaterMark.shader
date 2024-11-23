Shader"Vaporwave/WaterMark"
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
sampler2D _MarkTexture;
float4 _MarkTextureRect;
float _MarkTextureAlpha;
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

v2f vert(appdata v) 
{
    v2f o;
    o.vertex = UnityObjectToClipPos(v.vertex);
    o.uv = v.uv;
    return o;
}

float4 frag(v2f i) : SV_Target
{
    float2 newUV = (i.uv-_MarkTextureRect.xy) / _MarkTextureRect.zw;
    float4 colBase = tex2D(_MainTex, i.uv);
    float4 colAdd = tex2D(_MarkTexture, newUV);
    float blendAlpha=colAdd.a*_MarkTextureAlpha*step(newUV.x,1)*step(0,newUV.x)*step(newUV.y,1)*step(0,newUV.y);
    return colBase*(1-blendAlpha)+ float4(colAdd.xyz,1)*blendAlpha;
}
            ENDCG
        }
    }
}
