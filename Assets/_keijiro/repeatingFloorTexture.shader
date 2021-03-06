// Standard shader with triplanar mapping
// https://github.com/keijiro/StandardTriplanar

// See also StandardTriplanarInspector.cs
Shader "Standard Triplanar"
{
    Properties
    {
        _Color("", Color) = (1, 1, 1, 1)
        _MainTex("", 2D) = "white" {}

        _Glossiness("", Range(0, 1)) = 0.5
        [Gamma] _Metallic("", Range(0, 1)) = 0

        //_BumpScale("", Float) = 1
        //_BumpMap("", 2D) = "bump" {}
        //
        //_OcclusionStrength("", Range(0, 1)) = 1
        //_OcclusionMap("", 2D) = "white" {}

        _MapScale("", Float) = 1
        [ShowAsVector2] _MapOffset("Mapping Offset", Vector) = (0, 0, 0) // Third Vector component is unused.
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        
        Blend SrcAlpha OneMinusSrcAlpha 
        ZWrite On

        CGPROGRAM

        #pragma surface surf Standard vertex:vert fullforwardshadows addshadow alpha

        //#pragma shader_feature _NORMALMAP
        //#pragma shader_feature _OCCLUSIONMAP

        #pragma target 3.0

        half4 _Color;
        sampler2D _MainTex;

        half _Glossiness;
        half _Metallic;

        //half _BumpScale;
        //sampler2D _BumpMap;
        //
        //half _OcclusionStrength;
        //sampler2D _OcclusionMap;

        half _MapScale;
        half2 _MapOffset;

        struct Input
        {
            //float3 localCoord;
            //float3 localNormal;
            float3 worldCoord;
            float3 worldNormal;
        };

        void vert(inout appdata_full v, out Input data)
        {
            UNITY_INITIALIZE_OUTPUT(Input, data);
            //data.localCoord = v.vertex.xyz;
            //data.localNormal = v.normal.xyz;
            data.worldCoord = mul(unity_ObjectToWorld, v.vertex);
            data.worldCoord.x -= _MapOffset.x;
            data.worldCoord.z -= _MapOffset.y;
            data.worldNormal = mul(unity_ObjectToWorld, v.normal);
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            // Blending factor of triplanar mapping
            //float3 bf = normalize(abs(IN.localNormal));
            float3 bf = normalize(abs(IN.worldNormal));
            bf /= dot(bf, (float3)1);

            // Triplanar mapping
            // NOPE NOPE NOPE - tkeene
            //float2 tx = IN.localCoord.yz * _MapScale;
            //float2 ty = IN.localCoord.zx * _MapScale;
            //float2 tz = IN.localCoord.xy * _MapScale;
            float2 tx = IN.worldCoord.yz * _MapScale;
            float2 ty = IN.worldCoord.zx * _MapScale;
            float2 tz = IN.worldCoord.xy * _MapScale;

            // Base color
            half4 cx = tex2D(_MainTex, tx) * bf.x;
            half4 cy = tex2D(_MainTex, ty) * bf.y;
            half4 cz = tex2D(_MainTex, tz) * bf.z;
            half4 color = (cx + cy + cz) * _Color;
            o.Albedo = color.rgba;
            o.Alpha = color.a;

        //#ifdef _NORMALMAP
        //    // Normal map
        //    half4 nx = tex2D(_BumpMap, tx) * bf.x;
        //    half4 ny = tex2D(_BumpMap, ty) * bf.y;
        //    half4 nz = tex2D(_BumpMap, tz) * bf.z;
        //    o.Normal = UnpackScaleNormal(nx + ny + nz, _BumpScale);
        //#endif
        //
        //#ifdef _OCCLUSIONMAP
        //    // Occlusion map
        //    half ox = tex2D(_OcclusionMap, tx).g * bf.x;
        //    half oy = tex2D(_OcclusionMap, ty).g * bf.y;
        //    half oz = tex2D(_OcclusionMap, tz).g * bf.z;
        //    o.Occlusion = lerp((half4)1, ox + oy + oz, _OcclusionStrength);
        //#endif

            // Misc parameters
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
        }
        ENDCG
    }
    FallBack "Diffuse"
    CustomEditor "StandardTriplanarInspector"
}