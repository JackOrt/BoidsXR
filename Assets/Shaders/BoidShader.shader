Shader "Custom/BoidShader"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile __ UNITY_SINGLE_PASS_STEREO

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID // For instancing
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normal : TEXCOORD0;
                fixed4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO // For stereo output
            };

            struct InstanceData
            {
                float4x4 transform;
                float4 color;
            };

            StructuredBuffer<InstanceData> instanceTransforms;

            fixed4 _Color;
            float4x4 _SwarmTransform;
            float _StartInstanceOffset;

            v2f vert (appdata v, uint instanceID : SV_InstanceID)
            {
                UNITY_SETUP_INSTANCE_ID(v);               // Setup for instancing
                v2f o;
                UNITY_INITIALIZE_OUTPUT(v2f, o);         // Initialize output
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); // Setup stereo

                // Map every two instanceIDs to one actual index
                int actualIndex = (int)(_StartInstanceOffset + (instanceID / 2));
                InstanceData instance = instanceTransforms[actualIndex];

                // Apply Swarm Transform
                float4 localPos = mul(_SwarmTransform, v.vertex);
                float3 localNormal = normalize(mul((float3x3)_SwarmTransform, v.normal));

                // Apply instance transform
                float4x4 instanceMat = transpose(instance.transform);
                float4 worldPos = mul(instanceMat, localPos);
                float3 worldNormal = normalize(mul((float3x3)instanceMat, localNormal));

                // Apply view-projection matrix for the current eye
                o.pos = UnityObjectToClipPos(worldPos);
                o.normal = worldNormal;
                o.color = instance.color;

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i); // Enable stereo eye index
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                float diff = max(dot(i.normal, lightDir), 0.0);
                fixed4 c = i.color;
                c.rgb *= diff;
                return c;
            }
            ENDCG
        }
    }
}
