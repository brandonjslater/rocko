Shader "Rocko/Glass"
{
    Properties { _Color ("Tint", Color) = (0.22,0.37,0.40,0.12) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        CGPROGRAM
        #pragma surface surf Standard alpha:fade
        #pragma target 3.0
        fixed4 _Color;
        struct Input { float3 worldPos; };
        // @description - Shade lightly tinted two-sided cabin glass
        // @param {Input} IN - Interpolated surface data
        // @param {SurfaceOutputStandard} o - Output lighting parameters
        // @return {Void} - Writes transparent glass parameters
        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            o.Albedo = _Color.rgb;
            o.Smoothness = 0.9;
            o.Alpha = _Color.a;
        }
        ENDCG
    }
    FallBack "Transparent/Diffuse"
}
