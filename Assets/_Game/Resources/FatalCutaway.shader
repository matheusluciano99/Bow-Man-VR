Shader "BowMan/Fatal Cutaway"
{
    Properties { _Color ("Color", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END

            struct Vertex
            {
                float4 position : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varying
            {
                float4 clip : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 world : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // Transforms each mesh for the current eye.
            Varying Vert(Vertex v)
            {
                Varying o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.world = TransformObjectToWorld(v.position.xyz);
                o.clip = TransformWorldToHClip(o.world);
                o.normal = TransformObjectToWorldNormal(v.normal);
                return o;
            }

            // Shades tissue contours while leaving the shot path visible through the surface.
            half4 Frag(Varying i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half rim = 1 - saturate(abs(dot(normalize(i.normal), GetWorldSpaceNormalizeViewDir(i.world))));
                return half4(_Color.rgb * (0.72h + 0.55h * rim), saturate(_Color.a * (1 + rim)));
            }
            ENDHLSL
        }
    }
}
