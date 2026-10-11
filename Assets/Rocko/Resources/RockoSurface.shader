Shader "Rocko/Surface"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Glossiness ("Smoothness", Range(0,1)) = 0.25
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        fixed4 _Color;
        half _Glossiness;
        struct Input { float3 worldPos; };
        // @description - Shade the prototype's shared opaque surfaces
        // @param {Input} IN - Interpolated surface data
        // @param {SurfaceOutputStandard} o - Output lighting parameters
        // @return {Void} - Writes albedo, roughness, and opacity
        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            o.Albedo = _Color.rgb;
            o.Metallic = 0.05;
            o.Smoothness = _Glossiness;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
