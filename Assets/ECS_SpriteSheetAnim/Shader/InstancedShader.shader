Shader "Custom/IndirectSprite"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On // Thường Sprite Transparent nên để ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #include "UnityCG.cginc"

            // Các Buffer chứa dữ liệu từ ECS
            StructuredBuffer<float4x4> _Matrices;
            StructuredBuffer<float4> _UVData;
            
            sampler2D _MainTex;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                uint id : SV_InstanceID; // ID của thực thể
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;

                // 1. Lấy ma trận TRS từ buffer dựa trên Instance ID
                float4x4 m = _Matrices[v.id];
                
                // 2. Chuyển tọa độ từ Local -> World -> Clip Space
                float4 worldPos = mul(m, v.vertex);
                o.vertex = mul(UNITY_MATRIX_VP, worldPos);

                // 3. Lấy dữ liệu UV (x, y là offset, zw là scale)
                float4 uvData = _UVData[v.id];

                // 4. Tính toán UV chuẩn cho Frame ngay tại đây
                // v.uv là tọa độ 0->1 của Quad mesh
                o.uv = v.uv * uvData.zw + uvData.xy;

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Chỉ việc lấy màu từ Texture với UV đã tính sẵn ở Vertex
                fixed4 col = tex2D(_MainTex, i.uv);
                
                // Bỏ qua các pixel trong suốt
                clip(col.a - 0.01);
                
                return col;
            }
            ENDHLSL
        }
    }
}