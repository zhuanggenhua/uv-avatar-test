Shader "EquipmentSystem/HQ4xFilter"
{
    Properties
    {
        [HideInInspector] _BlitTexture ("Source", 2D) = "white" {}
        _PixelScale ("Pixel Scale", Float) = 4
        _LUT ("LUT", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "HQ4x Filter"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment HQ4xFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _PixelScale;
            TEXTURE2D(_LUT);

            float3 SampleSrc(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0).rgb;
            }

            float4 SampleLUT(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_LUT, sampler_PointClamp, uv);
            }

            float3 RGBtoYUV(float3 rgb)
            {
                return float3(
                    dot(rgb, float3(0.299, 0.587, 0.114)),
                    dot(rgb, float3(-0.169, -0.331, 0.5)),
                    dot(rgb, float3(0.5, -0.419, -0.081))
                );
            }

            bool DiffYUV(float3 a, float3 b)
            {
                float3 d = abs(a - b);
                return (d.x > (48.0 / 255.0)) || (d.y > (7.0 / 255.0)) || (d.z > (6.0 / 255.0));
            }

            float4 HQ4xFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                float2 texelSize = _BlitTexture_TexelSize.xy;

                float2 srcTexelSize = texelSize * _PixelScale;
                float2 srcPixelPos = uv / srcTexelSize;
                float2 srcPixelCenter = floor(srcPixelPos) + 0.5;
                float2 texCoord = srcPixelCenter * srcTexelSize;
                float2 fp = frac(srcPixelPos);

                float2 quad = sign(fp - 0.5);

                float3 p1 = SampleSrc(texCoord);
                float3 p2 = SampleSrc(texCoord + srcTexelSize * quad);
                float3 p3 = SampleSrc(texCoord + float2(srcTexelSize.x * quad.x, 0.0));
                float3 p4 = SampleSrc(texCoord + float2(0.0, srcTexelSize.y * quad.y));

                float dx = srcTexelSize.x;
                float dy = srcTexelSize.y;

                float3 w1 = RGBtoYUV(SampleSrc(texCoord + float2(-dx, -dy)));
                float3 w2 = RGBtoYUV(SampleSrc(texCoord + float2(0.0, -dy)));
                float3 w3 = RGBtoYUV(SampleSrc(texCoord + float2(dx, -dy)));

                float3 w4 = RGBtoYUV(SampleSrc(texCoord + float2(-dx, 0.0)));
                float3 w5 = RGBtoYUV(p1);
                float3 w6 = RGBtoYUV(SampleSrc(texCoord + float2(dx, 0.0)));

                float3 w7 = RGBtoYUV(SampleSrc(texCoord + float2(-dx, dy)));
                float3 w8 = RGBtoYUV(SampleSrc(texCoord + float2(0.0, dy)));
                float3 w9 = RGBtoYUV(SampleSrc(texCoord + float2(dx, dy)));

                bool p00 = DiffYUV(w5, w1);
                bool p01 = DiffYUV(w5, w2);
                bool p02 = DiffYUV(w5, w3);
                bool p10 = DiffYUV(w5, w4);
                bool p12 = DiffYUV(w5, w6);
                bool p20 = DiffYUV(w5, w7);
                bool p21 = DiffYUV(w5, w8);
                bool p22 = DiffYUV(w5, w9);

                bool c0 = DiffYUV(w4, w2);
                bool c1 = DiffYUV(w2, w6);
                bool c2 = DiffYUV(w8, w4);
                bool c3 = DiffYUV(w6, w8);

                float indexX = (p00 ? 1.0 : 0.0) + (p01 ? 2.0 : 0.0) + (p02 ? 4.0 : 0.0) +
                               (p10 ? 8.0 : 0.0) + (p12 ? 16.0 : 0.0) +
                               (p20 ? 32.0 : 0.0) + (p21 ? 64.0 : 0.0) + (p22 ? 128.0 : 0.0);

                float crossIndex = (c0 ? 1.0 : 0.0) + (c1 ? 2.0 : 0.0) + (c2 ? 4.0 : 0.0) + (c3 ? 8.0 : 0.0);

                const float k_Scale = 4.0;
                float2 sub = floor(fp * k_Scale);
                float subIndex = sub.x + sub.y * k_Scale;

                float indexY = crossIndex * (k_Scale * k_Scale) + subIndex;

                float2 lutUV = (float2(indexX, indexY) + 0.5) / 256.0;
                float4 weights = SampleLUT(lutUV);

                float sum = dot(weights, float4(1, 1, 1, 1));
                sum = max(sum, 1e-5);

                float3 res = (p1 * weights.x + p2 * weights.y + p3 * weights.z + p4 * weights.w) / sum;
                return float4(res, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
