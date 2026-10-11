Shader "Rocko/WorldText"
{
    Properties
    {
        _MainTex ("Font atlas", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        // Render before glass so signs outside receive the windscreen tint.
        Tags { "Queue"="Transparent-10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZTest LEqual
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            fixed4 _Color;
            struct VertexInput
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };
            struct VertexOutput
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                UNITY_FOG_COORDS(1)
            };
            //
            // @description - Project world lettering using the gameplay camera
            // @param {VertexInput} v - Text mesh position, atlas coordinates, and tint
            // @return {VertexOutput} - Clip position and interpolated glyph data
            //
            VertexOutput vert(VertexInput v)
            {
                VertexOutput o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }
            //
            // @description - Shade atlas glyphs with transparency and world fog
            // @param {VertexOutput} i - Interpolated glyph data
            // @return {fixed4} - Depth-tested text color
            //
            fixed4 frag(VertexOutput i) : SV_Target
            {
                fixed4 color = i.color;
                color.a *= tex2D(_MainTex, i.uv).a;
                UNITY_APPLY_FOG(i.fogCoord, color);
                return color;
            }
            ENDCG
        }
    }
}
