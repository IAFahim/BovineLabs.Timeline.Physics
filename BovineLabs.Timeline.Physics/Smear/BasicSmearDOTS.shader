Shader "Custom/BasicSmearDOTS"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (1,1,1,1)
        _SmearMultiplier("Smear Multiplier", Float) = 0.1
        _Velocity("Velocity (Set by ECS)", Vector) = (0,0,0,0)
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // --- REQUIRED CBUFFER FOR DOTS FALLBACK & STANDARD RENDERING ---
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _SmearMultiplier;
                float4 _Velocity;
            CBUFFER_END

            // --- DOTS INSTANCING REGISTRATION ---
            #ifdef UNITY_DOTS_INSTANCING_ENABLED
            UNITY_DOTS_INSTANCING_START (MaterialPropertyMetadata)
            UNITY_DOTS_INSTANCED_PROP(float4, _BaseColor)
            UNITY_DOTS_INSTANCED_PROP(float, _SmearMultiplier)
            UNITY_DOTS_INSTANCED_PROP(float4, _Velocity)
            UNITY_DOTS_INSTANCING_END (MaterialPropertyMetadata)

            #define _BaseColor       UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _BaseColor)
            #define _SmearMultiplier UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _SmearMultiplier)
            #define _Velocity        UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _Velocity)
            #endif

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                // 1. Get Velocity from ECS and convert to Object Space
                float3 velocityWS = _Velocity.xyz;

                // Multiply by Inverse Model Matrix to get Object Space velocity
                float3 velocityOS = mul(GetWorldToObjectMatrix(), float4(velocityWS, 0.0)).xyz;
                float speedOS = length(velocityOS);

                // 2. Only deform if actually moving
                if (speedOS > 0.01)
                {
                    float3 moveDirOS = velocityOS / speedOS;

                    // 3. Mask trailing edges
                    // dot() is positive for front faces, negative for back faces.
                    // We only want to stretch the back faces.
                    float NdV = dot(input.normalOS, moveDirOS);
                    float trailMask = saturate(-NdV);

                    // Square the mask so it pinches nicely towards the absolute back
                    trailMask = trailMask * trailMask;

                    // 4. Apply Smear offset (subtract to leave a trail behind)
                    input.positionOS.xyz -= velocityOS * trailMask * _SmearMultiplier;
                }

                // Transform the newly stretched Object Space position to Clip Space
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return _BaseColor;
            }
            ENDHLSL
        }
    }
}