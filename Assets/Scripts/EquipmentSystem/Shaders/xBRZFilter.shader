// ============================================================================
// 4xBRZ Pixel Art Upscaling Shader for Unity URP
// ============================================================================
// 
// 完整移植自 libretro/common-shaders 4xBRZ
// Copyright (C) 2014-2016 DeSmuME team (GPLv2)
// xBR-vertex code by Hyllian (MIT)
//
// 用于像素画放大时产生平滑曲线边缘
// ============================================================================
Shader "EquipmentSystem/xBRZFilter"
{
    Properties
    {
        [HideInInspector] _BlitTexture ("Source", 2D) = "white" {}
        
        [Header(Pixel Scale)]
        _PixelScale ("Pixel Scale (e.g. 4, 8, 16)", Float) = 4
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
            Name "xBRZ Filter"
            
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment xBRZFrag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            
            // 4xBRZ 常量
            #define BLEND_NONE 0
            #define BLEND_NORMAL 1
            #define BLEND_DOMINANT 2
            #define LUMINANCE_WEIGHT 1.0
            #define EQUAL_COLOR_TOLERANCE 0.1176470588 // 30.0/255.0
            #define STEEP_DIRECTION_THRESHOLD 2.2
            #define DOMINANT_DIRECTION_THRESHOLD 3.6
            
            float _PixelScale;
            
            // 获取像素 (使用 Point 采样)
            float3 SampleSrc(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0).rgb;
            }
            
            // 用于快速比较的 reduce 函数
            float reduce(float3 color)
            {
                return dot(color, float3(65536.0, 256.0, 1.0));
            }
            
            // YCbCr 颜色差异计算
            float DistYCbCr(float3 pixA, float3 pixB)
            {
                const float3 w = float3(0.2627, 0.6780, 0.0593);
                const float scaleB = 0.5 / (1.0 - w.b);
                const float scaleR = 0.5 / (1.0 - w.r);
                float3 diff = pixA - pixB;
                float Y = dot(diff, w);
                float Cb = scaleB * (diff.b - Y);
                float Cr = scaleR * (diff.r - Y);
                return sqrt(((LUMINANCE_WEIGHT * Y) * (LUMINANCE_WEIGHT * Y)) + (Cb * Cb) + (Cr * Cr));
            }
            
            // 判断像素是否相等
            bool IsPixEqual(float3 pixA, float3 pixB)
            {
                return (DistYCbCr(pixA, pixB) < EQUAL_COLOR_TOLERANCE);
            }
            
            // 判断是否需要混合
            bool IsBlendingNeeded(int4 blend)
            {
                return any(blend != int4(BLEND_NONE, BLEND_NONE, BLEND_NONE, BLEND_NONE));
            }
            
            float4 xBRZFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                
                float2 uv = input.texcoord;
                float4 texelSize = _BlitTexture_TexelSize;
                
                // 计算源像素网格的纹素大小
                float2 srcTexelSize = texelSize.xy * _PixelScale;
                float2 ps = srcTexelSize;
                float dx = ps.x;
                float dy = ps.y;
                
                // 计算当前像素在源像素网格中的位置
                float2 srcPixelPos = uv / srcTexelSize;
                float2 srcPixelCenter = floor(srcPixelPos) + 0.5;
                float2 texCoord = srcPixelCenter * srcTexelSize;
                
                // 子像素位置 (0-1)
                float2 f = frac(srcPixelPos);
                
                // 采样 5x5 邻域 (实际使用 21 个像素)
                //  A1 B1 C1
                // A0 A  B  C C4
                // D0 D  E  F F4
                // G0 G  H  I I4
                //  G5 H5 I5
                
                float3 src[25];
                
                // 第一行 (y-2)
                src[21] = SampleSrc(texCoord + float2(-dx, -2.0*dy));
                src[22] = SampleSrc(texCoord + float2(  0, -2.0*dy));
                src[23] = SampleSrc(texCoord + float2( dx, -2.0*dy));
                
                // 第二行 (y-1)
                src[6] = SampleSrc(texCoord + float2(-dx, -dy));
                src[7] = SampleSrc(texCoord + float2(  0, -dy));
                src[8] = SampleSrc(texCoord + float2( dx, -dy));
                
                // 第三行 (y=0) - 中心行
                src[5] = SampleSrc(texCoord + float2(-dx, 0));
                src[0] = SampleSrc(texCoord);  // 中心像素
                src[1] = SampleSrc(texCoord + float2( dx, 0));
                
                // 第四行 (y+1)
                src[4] = SampleSrc(texCoord + float2(-dx, dy));
                src[3] = SampleSrc(texCoord + float2(  0, dy));
                src[2] = SampleSrc(texCoord + float2( dx, dy));
                
                // 第五行 (y+2)
                src[15] = SampleSrc(texCoord + float2(-dx, 2.0*dy));
                src[14] = SampleSrc(texCoord + float2(  0, 2.0*dy));
                src[13] = SampleSrc(texCoord + float2( dx, 2.0*dy));
                
                // 额外的左右列
                src[19] = SampleSrc(texCoord + float2(-2.0*dx, -dy));
                src[18] = SampleSrc(texCoord + float2(-2.0*dx,   0));
                src[17] = SampleSrc(texCoord + float2(-2.0*dx,  dy));
                src[9]  = SampleSrc(texCoord + float2( 2.0*dx, -dy));
                src[10] = SampleSrc(texCoord + float2( 2.0*dx,   0));
                src[11] = SampleSrc(texCoord + float2( 2.0*dx,  dy));
                
                // 计算 reduce 值用于快速比较
                float v[9];
                v[0] = reduce(src[0]);
                v[1] = reduce(src[1]);
                v[2] = reduce(src[2]);
                v[3] = reduce(src[3]);
                v[4] = reduce(src[4]);
                v[5] = reduce(src[5]);
                v[6] = reduce(src[6]);
                v[7] = reduce(src[7]);
                v[8] = reduce(src[8]);
                
                int4 blendResult = int4(BLEND_NONE, BLEND_NONE, BLEND_NONE, BLEND_NONE);
                
                // 预处理四个角的混合决策
                // Corner (1, 1) - 右下
                if (!((v[0] == v[1] && v[3] == v[2]) || (v[0] == v[3] && v[1] == v[2])))
                {
                    float dist_03_01 = DistYCbCr(src[4], src[0]) + DistYCbCr(src[0], src[8]) + DistYCbCr(src[14], src[2]) + DistYCbCr(src[2], src[10]) + (4.0 * DistYCbCr(src[3], src[1]));
                    float dist_00_02 = DistYCbCr(src[5], src[3]) + DistYCbCr(src[3], src[13]) + DistYCbCr(src[7], src[1]) + DistYCbCr(src[1], src[11]) + (4.0 * DistYCbCr(src[0], src[2]));
                    bool dominantGradient = (DOMINANT_DIRECTION_THRESHOLD * dist_03_01) < dist_00_02;
                    blendResult[2] = ((dist_03_01 < dist_00_02) && (v[0] != v[1]) && (v[0] != v[3])) ? ((dominantGradient) ? BLEND_DOMINANT : BLEND_NORMAL) : BLEND_NONE;
                }
                
                // Corner (0, 1) - 左下
                if (!((v[5] == v[0] && v[4] == v[3]) || (v[5] == v[4] && v[0] == v[3])))
                {
                    float dist_04_00 = DistYCbCr(src[17], src[5]) + DistYCbCr(src[5], src[7]) + DistYCbCr(src[15], src[3]) + DistYCbCr(src[3], src[1]) + (4.0 * DistYCbCr(src[4], src[0]));
                    float dist_05_03 = DistYCbCr(src[18], src[4]) + DistYCbCr(src[4], src[14]) + DistYCbCr(src[6], src[0]) + DistYCbCr(src[0], src[2]) + (4.0 * DistYCbCr(src[5], src[3]));
                    bool dominantGradient = (DOMINANT_DIRECTION_THRESHOLD * dist_05_03) < dist_04_00;
                    blendResult[3] = ((dist_04_00 > dist_05_03) && (v[0] != v[5]) && (v[0] != v[3])) ? ((dominantGradient) ? BLEND_DOMINANT : BLEND_NORMAL) : BLEND_NONE;
                }
                
                // Corner (1, 0) - 右上
                if (!((v[7] == v[8] && v[0] == v[1]) || (v[7] == v[0] && v[8] == v[1])))
                {
                    float dist_00_08 = DistYCbCr(src[5], src[7]) + DistYCbCr(src[7], src[23]) + DistYCbCr(src[3], src[1]) + DistYCbCr(src[1], src[9]) + (4.0 * DistYCbCr(src[0], src[8]));
                    float dist_07_01 = DistYCbCr(src[6], src[0]) + DistYCbCr(src[0], src[2]) + DistYCbCr(src[22], src[8]) + DistYCbCr(src[8], src[10]) + (4.0 * DistYCbCr(src[7], src[1]));
                    bool dominantGradient = (DOMINANT_DIRECTION_THRESHOLD * dist_07_01) < dist_00_08;
                    blendResult[1] = ((dist_00_08 > dist_07_01) && (v[0] != v[7]) && (v[0] != v[1])) ? ((dominantGradient) ? BLEND_DOMINANT : BLEND_NORMAL) : BLEND_NONE;
                }
                
                // Corner (0, 0) - 左上
                if (!((v[6] == v[7] && v[5] == v[0]) || (v[6] == v[5] && v[7] == v[0])))
                {
                    float dist_05_07 = DistYCbCr(src[18], src[6]) + DistYCbCr(src[6], src[22]) + DistYCbCr(src[4], src[0]) + DistYCbCr(src[0], src[8]) + (4.0 * DistYCbCr(src[5], src[7]));
                    float dist_06_00 = DistYCbCr(src[19], src[5]) + DistYCbCr(src[5], src[3]) + DistYCbCr(src[21], src[7]) + DistYCbCr(src[7], src[1]) + (4.0 * DistYCbCr(src[6], src[0]));
                    bool dominantGradient = (DOMINANT_DIRECTION_THRESHOLD * dist_05_07) < dist_06_00;
                    blendResult[0] = ((dist_05_07 < dist_06_00) && (v[0] != v[5]) && (v[0] != v[7])) ? ((dominantGradient) ? BLEND_DOMINANT : BLEND_NORMAL) : BLEND_NONE;
                }
                
                // 初始化 16 个输出子像素 (4x4)
                float3 dst[16];
                [unroll] for (int i = 0; i < 16; i++) dst[i] = src[0];
                
                // 缩放像素 - 应用混合
                if (IsBlendingNeeded(blendResult))
                {
                    float dist_01_04 = DistYCbCr(src[1], src[4]);
                    float dist_03_08 = DistYCbCr(src[3], src[8]);
                    bool haveShallowLine = (STEEP_DIRECTION_THRESHOLD * dist_01_04 <= dist_03_08) && (v[0] != v[4]) && (v[5] != v[4]);
                    bool haveSteepLine = (STEEP_DIRECTION_THRESHOLD * dist_03_08 <= dist_01_04) && (v[0] != v[8]) && (v[7] != v[8]);
                    bool needBlend = (blendResult[2] != BLEND_NONE);
                    bool doLineBlend = (blendResult[2] >= BLEND_DOMINANT ||
                        !((blendResult[1] != BLEND_NONE && !IsPixEqual(src[0], src[4])) ||
                          (blendResult[3] != BLEND_NONE && !IsPixEqual(src[0], src[8])) ||
                          (IsPixEqual(src[4], src[3]) && IsPixEqual(src[3], src[2]) && IsPixEqual(src[2], src[1]) && IsPixEqual(src[1], src[8]) && !IsPixEqual(src[0], src[2]))));
                    
                    float3 blendPix = (DistYCbCr(src[0], src[1]) <= DistYCbCr(src[0], src[3])) ? src[1] : src[3];
                    dst[2]  = lerp(dst[2],  blendPix, (needBlend && doLineBlend) ? ((haveShallowLine) ? ((haveSteepLine) ? 0.333 : 0.25) : ((haveSteepLine) ? 0.25 : 0.0)) : 0.0);
                    dst[9]  = lerp(dst[9],  blendPix, (needBlend && doLineBlend && haveSteepLine) ? 0.25 : 0.0);
                    dst[10] = lerp(dst[10], blendPix, (needBlend && doLineBlend && haveSteepLine) ? 0.75 : 0.0);
                    dst[11] = lerp(dst[11], blendPix, (needBlend) ? ((doLineBlend) ? ((haveSteepLine) ? 1.0 : ((haveShallowLine) ? 0.75 : 0.5)) : 0.08677704501) : 0.0);
                    dst[12] = lerp(dst[12], blendPix, (needBlend) ? ((doLineBlend) ? 1.0 : 0.6848532563) : 0.0);
                    dst[13] = lerp(dst[13], blendPix, (needBlend) ? ((doLineBlend) ? ((haveShallowLine) ? 1.0 : ((haveSteepLine) ? 0.75 : 0.5)) : 0.08677704501) : 0.0);
                    dst[14] = lerp(dst[14], blendPix, (needBlend && doLineBlend && haveShallowLine) ? 0.75 : 0.0);
                    dst[15] = lerp(dst[15], blendPix, (needBlend && doLineBlend && haveShallowLine) ? 0.25 : 0.0);
                    
                    // 第二个角
                    dist_01_04 = DistYCbCr(src[7], src[2]);
                    dist_03_08 = DistYCbCr(src[1], src[6]);
                    haveShallowLine = (STEEP_DIRECTION_THRESHOLD * dist_01_04 <= dist_03_08) && (v[0] != v[2]) && (v[3] != v[2]);
                    haveSteepLine = (STEEP_DIRECTION_THRESHOLD * dist_03_08 <= dist_01_04) && (v[0] != v[6]) && (v[5] != v[6]);
                    needBlend = (blendResult[1] != BLEND_NONE);
                    doLineBlend = (blendResult[1] >= BLEND_DOMINANT ||
                        !((blendResult[0] != BLEND_NONE && !IsPixEqual(src[0], src[2])) ||
                          (blendResult[2] != BLEND_NONE && !IsPixEqual(src[0], src[6])) ||
                          (IsPixEqual(src[2], src[1]) && IsPixEqual(src[1], src[8]) && IsPixEqual(src[8], src[7]) && IsPixEqual(src[7], src[6]) && !IsPixEqual(src[0], src[8]))));
                    
                    blendPix = (DistYCbCr(src[0], src[7]) <= DistYCbCr(src[0], src[1])) ? src[7] : src[1];
                    dst[1] = lerp(dst[1], blendPix, (needBlend && doLineBlend) ? ((haveShallowLine) ? ((haveSteepLine) ? 0.333 : 0.25) : ((haveSteepLine) ? 0.25 : 0.0)) : 0.0);
                    dst[6] = lerp(dst[6], blendPix, (needBlend && doLineBlend && haveSteepLine) ? 0.25 : 0.0);
                    dst[7] = lerp(dst[7], blendPix, (needBlend && doLineBlend && haveSteepLine) ? 0.75 : 0.0);
                    dst[8] = lerp(dst[8], blendPix, (needBlend) ? ((doLineBlend) ? ((haveSteepLine) ? 1.0 : ((haveShallowLine) ? 0.75 : 0.5)) : 0.08677704501) : 0.0);
                    dst[9] = lerp(dst[9], blendPix, (needBlend) ? ((doLineBlend) ? 1.0 : 0.6848532563) : 0.0);
                    dst[10] = lerp(dst[10], blendPix, (needBlend) ? ((doLineBlend) ? ((haveShallowLine) ? 1.0 : ((haveSteepLine) ? 0.75 : 0.5)) : 0.08677704501) : 0.0);
                    dst[11] = lerp(dst[11], blendPix, (needBlend && doLineBlend && haveShallowLine) ? 0.75 : 0.0);
                    dst[12] = lerp(dst[12], blendPix, (needBlend && doLineBlend && haveShallowLine) ? 0.25 : 0.0);
                    
                    // 第三个角
                    dist_01_04 = DistYCbCr(src[5], src[8]);
                    dist_03_08 = DistYCbCr(src[7], src[4]);
                    haveShallowLine = (STEEP_DIRECTION_THRESHOLD * dist_01_04 <= dist_03_08) && (v[0] != v[8]) && (v[1] != v[8]);
                    haveSteepLine = (STEEP_DIRECTION_THRESHOLD * dist_03_08 <= dist_01_04) && (v[0] != v[4]) && (v[3] != v[4]);
                    needBlend = (blendResult[0] != BLEND_NONE);
                    doLineBlend = (blendResult[0] >= BLEND_DOMINANT ||
                        !((blendResult[3] != BLEND_NONE && !IsPixEqual(src[0], src[8])) ||
                          (blendResult[1] != BLEND_NONE && !IsPixEqual(src[0], src[4])) ||
                          (IsPixEqual(src[8], src[7]) && IsPixEqual(src[7], src[6]) && IsPixEqual(src[6], src[5]) && IsPixEqual(src[5], src[4]) && !IsPixEqual(src[0], src[6]))));
                    
                    blendPix = (DistYCbCr(src[0], src[5]) <= DistYCbCr(src[0], src[7])) ? src[5] : src[7];
                    dst[0] = lerp(dst[0], blendPix, (needBlend && doLineBlend) ? ((haveShallowLine) ? ((haveSteepLine) ? 0.333 : 0.25) : ((haveSteepLine) ? 0.25 : 0.0)) : 0.0);
                    dst[15] = lerp(dst[15], blendPix, (needBlend && doLineBlend && haveSteepLine) ? 0.25 : 0.0);
                    dst[4] = lerp(dst[4], blendPix, (needBlend && doLineBlend && haveSteepLine) ? 0.75 : 0.0);
                    dst[5] = lerp(dst[5], blendPix, (needBlend) ? ((doLineBlend) ? ((haveSteepLine) ? 1.0 : ((haveShallowLine) ? 0.75 : 0.5)) : 0.08677704501) : 0.0);
                    dst[6] = lerp(dst[6], blendPix, (needBlend) ? ((doLineBlend) ? 1.0 : 0.6848532563) : 0.0);
                    dst[7] = lerp(dst[7], blendPix, (needBlend) ? ((doLineBlend) ? ((haveShallowLine) ? 1.0 : ((haveSteepLine) ? 0.75 : 0.5)) : 0.08677704501) : 0.0);
                    dst[8] = lerp(dst[8], blendPix, (needBlend && doLineBlend && haveShallowLine) ? 0.75 : 0.0);
                    dst[9] = lerp(dst[9], blendPix, (needBlend && doLineBlend && haveShallowLine) ? 0.25 : 0.0);
                    
                    // 第四个角
                    dist_01_04 = DistYCbCr(src[3], src[6]);
                    dist_03_08 = DistYCbCr(src[5], src[2]);
                    haveShallowLine = (STEEP_DIRECTION_THRESHOLD * dist_01_04 <= dist_03_08) && (v[0] != v[6]) && (v[7] != v[6]);
                    haveSteepLine = (STEEP_DIRECTION_THRESHOLD * dist_03_08 <= dist_01_04) && (v[0] != v[2]) && (v[1] != v[2]);
                    needBlend = (blendResult[3] != BLEND_NONE);
                    doLineBlend = (blendResult[3] >= BLEND_DOMINANT ||
                        !((blendResult[2] != BLEND_NONE && !IsPixEqual(src[0], src[6])) ||
                          (blendResult[0] != BLEND_NONE && !IsPixEqual(src[0], src[2])) ||
                          (IsPixEqual(src[6], src[5]) && IsPixEqual(src[5], src[4]) && IsPixEqual(src[4], src[3]) && IsPixEqual(src[3], src[2]) && !IsPixEqual(src[0], src[4]))));
                    
                    blendPix = (DistYCbCr(src[0], src[3]) <= DistYCbCr(src[0], src[5])) ? src[3] : src[5];
                    dst[3] = lerp(dst[3], blendPix, (needBlend && doLineBlend) ? ((haveShallowLine) ? ((haveSteepLine) ? 0.333 : 0.25) : ((haveSteepLine) ? 0.25 : 0.0)) : 0.0);
                    dst[12] = lerp(dst[12], blendPix, (needBlend && doLineBlend && haveSteepLine) ? 0.25 : 0.0);
                    dst[13] = lerp(dst[13], blendPix, (needBlend && doLineBlend && haveSteepLine) ? 0.75 : 0.0);
                    dst[14] = lerp(dst[14], blendPix, (needBlend) ? ((doLineBlend) ? ((haveSteepLine) ? 1.0 : ((haveShallowLine) ? 0.75 : 0.5)) : 0.08677704501) : 0.0);
                    dst[15] = lerp(dst[15], blendPix, (needBlend) ? ((doLineBlend) ? 1.0 : 0.6848532563) : 0.0);
                    dst[4] = lerp(dst[4], blendPix, (needBlend) ? ((doLineBlend) ? ((haveShallowLine) ? 1.0 : ((haveSteepLine) ? 0.75 : 0.5)) : 0.08677704501) : 0.0);
                    dst[5] = lerp(dst[5], blendPix, (needBlend && doLineBlend && haveShallowLine) ? 0.75 : 0.0);
                    dst[6] = lerp(dst[6], blendPix, (needBlend && doLineBlend && haveShallowLine) ? 0.25 : 0.0);
                }
                
                // 从 16 个子像素中选择正确的输出
                // 输出像素映射:  06|07|08|09
                //               05|00|01|10
                //               04|03|02|11
                //               15|14|13|12
                float3 res = lerp(
                    lerp(
                        lerp(lerp(dst[6], dst[7], step(0.25, f.x)), lerp(dst[8], dst[9], step(0.75, f.x)), step(0.5, f.x)),
                        lerp(lerp(dst[5], dst[0], step(0.25, f.x)), lerp(dst[1], dst[10], step(0.75, f.x)), step(0.5, f.x)),
                        step(0.25, f.y)
                    ),
                    lerp(
                        lerp(lerp(dst[4], dst[3], step(0.25, f.x)), lerp(dst[2], dst[11], step(0.75, f.x)), step(0.5, f.x)),
                        lerp(lerp(dst[15], dst[14], step(0.25, f.x)), lerp(dst[13], dst[12], step(0.75, f.x)), step(0.5, f.x)),
                        step(0.75, f.y)
                    ),
                    step(0.5, f.y)
                );
                
                return float4(res, 1.0);
            }
            ENDHLSL
        }
    }
    
    FallBack Off
}
