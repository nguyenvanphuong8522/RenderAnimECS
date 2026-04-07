Shader "Custom/IndirectSprite"
{
    Properties
    {
        _MainTex ("Texture", 2D)="white"{}
    }
    SubShader
    {
        Tags{"RenderType"="Transparent"
             "Queue"="Transparent"}

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha

            ZWrite On
            ZTest LEqual

            Cull Off
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #include "UnityCG.cginc"

            StructuredBuffer<float4x4> _Matrices;
            StructuredBuffer<float4> _UVData;

            sampler2D _MainTex;

            struct appdata
            {
                float4 vertex:POSITION;
                float2 uv:TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex:SV_POSITION;
                float2 uv:TEXCOORD0;
            };

            v2f vert(appdata v, uint id:SV_InstanceID)
            {
                v2f o;

                float4x4 m = _Matrices[id];

                float4 pos = mul(m,v.vertex);

                o.vertex = UnityObjectToClipPos(pos);

                float4 uv = _UVData[id];

                o.uv = v.uv*uv.xy+uv.zw;

                return o;
            }

            fixed4 frag(v2f i):SV_Target
            {
                fixed4 col = tex2D(_MainTex,i.uv);

                clip(col.a - 0.01);

                return col;
            }

            ENDHLSL
        }
    }
}