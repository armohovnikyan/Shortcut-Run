// Inverted-hull outline for box-shaped meshes (the boards).
// Used as an extra material slot on the board's MeshRenderer, so it is hidden/shown together with the board.
// Each vertex is pushed out along the object's own axes (not its normal): a hard-edged box has split normals,
// and normal extrusion would leave gaps at the corners. Assumes the mesh pivot is at its center.
Shader "ShortcutRun/BoardOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0.12, 0.06, 0.03, 1)
        _OutlineWidth ("Outline Width (meters)", Range(0, 0.1)) = 0.025
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half fogFactor : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                // Normalized object axes in world space: the width stays in meters despite the board's non-uniform scale.
                float4x4 objectToWorld = GetObjectToWorldMatrix();
                float3 axisX = normalize(objectToWorld._m00_m10_m20);
                float3 axisY = normalize(objectToWorld._m01_m11_m21);
                float3 axisZ = normalize(objectToWorld._m02_m12_m22);

                float3 corner = sign(input.positionOS.xyz);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                positionWS += (corner.x * axisX + corner.y * axisY + corner.z * axisZ) * _OutlineWidth;

                output.positionCS = TransformWorldToHClip(positionWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 color = MixFog(_OutlineColor.rgb, input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
