Shader "Custom/VoxelInstanced"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct VoxelInstance
            {
                int3 pos;
                uint blockType;
            };

            StructuredBuffer<VoxelInstance> _VoxelBuffer;

            // Up to 16 block type colors; set from C# via Material.SetVectorArray
            float4 _BlockColors[16];

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
            };

            Varyings vert(Attributes IN, uint instanceID : SV_InstanceID)
            {
                VoxelInstance inst = _VoxelBuffer[instanceID];
                float3 worldPos   = IN.positionOS + float3(inst.pos);

                // Simple diffuse-like tint using the normal to add shading depth
                float ndotUp = dot(normalize(IN.normalOS), float3(0, 1, 0)) * 0.5 + 0.5;
                float shade  = lerp(0.55, 1.0, ndotUp);

                uint  ci    = min(inst.blockType, 15u);
                float4 base = _BlockColors[ci];

                Varyings OUT;
                OUT.positionCS = TransformWorldToHClip(worldPos);
                OUT.color      = float4(base.rgb * shade, 1.0);
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                return IN.color;
            }
            ENDHLSL
        }
    }
}
