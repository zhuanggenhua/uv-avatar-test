// ============================================================================
// Depixelizing Pixel Art Filter Shader
// ============================================================================
// 
// 实现 Kopf & Lischinski 2011 论文 "Depixelizing Pixel Art" 的核心算法
// GPU 实现版本，在单个 Pass 中完成：
// 1. 相似性图构建与对角线歧义解决
// 2. 像素单元重塑
// 3. 平滑曲线拟合与渲染
//
// 参考论文：https://johanneskopf.de/publications/pixelart/
// ============================================================================
Shader "EquipmentSystem/DepixelizeFilter"
{
    Properties
    {
        [HideInInspector] _BlitTexture ("Source", 2D) = "white" {}
        
        [Header(Pixel Scale)]
        _PixelScale ("Pixel Scale", Float) = 4
        
        [Header(Algorithm Parameters)]
        _ColorThreshold ("Color Threshold", Float) = 0.1176
        _ContourThreshold ("Contour Threshold", Float) = 0.392
        _Smoothness ("Smoothness", Float) = 1.0
        _Antialiasing ("Antialiasing", Float) = 0.5
        
        [Header(Heuristic Weights)]
        _CurveWeight ("Curve Weight", Float) = 1.0
        _SparseWeight ("Sparse Weight", Float) = 1.0
        _IslandWeight ("Island Weight", Float) = 5.0
        
        [Header(Debug)]
        _DebugMode ("Debug Mode", Float) = 0
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
            Name "Depixelize Filter"
            
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment DepixelizeFrag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            
            // ================================================================
            // 参数
            // ================================================================
            float _PixelScale;
            float _ColorThreshold;
            float _ContourThreshold;
            float _Smoothness;
            float _Antialiasing;
            float _CurveWeight;
            float _SparseWeight;
            float _IslandWeight;
            float _DebugMode;
            
            // ================================================================
            // 工具函数
            // ================================================================
            
            // Point 采样
            float4 SampleSrc(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0);
            }
            
            // RGB 转 YUV（论文使用 YUV 空间比较颜色）
            float3 RGBtoYUV(float3 rgb)
            {
                float Y = dot(rgb, float3(0.299, 0.587, 0.114));
                float U = 0.492 * (rgb.b - Y);
                float V = 0.877 * (rgb.r - Y);
                return float3(Y, U, V);
            }
            
            // YCbCr 颜色距离（论文公式）
            float DistYCbCr(float3 pixA, float3 pixB)
            {
                const float3 w = float3(0.2627, 0.6780, 0.0593);
                const float scaleB = 0.5 / (1.0 - w.b);
                const float scaleR = 0.5 / (1.0 - w.r);
                float3 diff = pixA - pixB;
                float Y = dot(diff, w);
                float Cb = scaleB * (diff.b - Y);
                float Cr = scaleR * (diff.r - Y);
                return sqrt(Y * Y + Cb * Cb + Cr * Cr);
            }
            
            // 判断两个像素是否相似（论文标准）
            bool IsPixelSimilar(float3 a, float3 b)
            {
                float3 yuvA = RGBtoYUV(a);
                float3 yuvB = RGBtoYUV(b);
                float3 diff = abs(yuvA - yuvB);
                // 论文阈值: Y < 48/255, U < 7/255, V < 6/255
                return diff.x < 0.188 && diff.y < 0.027 && diff.z < 0.024;
            }
            
            // 简化版颜色相似性检测
            bool IsColorSimilar(float3 a, float3 b, float threshold)
            {
                return DistYCbCr(a, b) < threshold;
            }
            
            // ================================================================
            // 相似性图与连接性判断
            // ================================================================
            
            // 采样 3x3 邻域
            void Sample3x3(float2 texCoord, float2 ps, out float3 p[9])
            {
                // 布局:
                // 0 1 2
                // 3 4 5
                // 6 7 8
                p[0] = SampleSrc(texCoord + float2(-ps.x, -ps.y)).rgb;
                p[1] = SampleSrc(texCoord + float2(    0, -ps.y)).rgb;
                p[2] = SampleSrc(texCoord + float2( ps.x, -ps.y)).rgb;
                p[3] = SampleSrc(texCoord + float2(-ps.x,     0)).rgb;
                p[4] = SampleSrc(texCoord).rgb; // 中心
                p[5] = SampleSrc(texCoord + float2( ps.x,     0)).rgb;
                p[6] = SampleSrc(texCoord + float2(-ps.x,  ps.y)).rgb;
                p[7] = SampleSrc(texCoord + float2(    0,  ps.y)).rgb;
                p[8] = SampleSrc(texCoord + float2( ps.x,  ps.y)).rgb;
            }
            
            // 采样 5x5 邻域用于曲线长度计算
            void Sample5x5(float2 texCoord, float2 ps, out float3 p[25])
            {
                [unroll]
                for (int y = -2; y <= 2; y++)
                {
                    [unroll]
                    for (int x = -2; x <= 2; x++)
                    {
                        int idx = (y + 2) * 5 + (x + 2);
                        p[idx] = SampleSrc(texCoord + float2(x * ps.x, y * ps.y)).rgb;
                    }
                }
            }
            
            // 计算曲线长度（论文 Curves 启发式）
            // 从给定方向追踪相似颜色像素的长度
            float TraceCurveLength(float2 texCoord, float2 ps, float3 refColor, float2 dir, float threshold)
            {
                float length = 0;
                float2 pos = texCoord;
                
                [unroll]
                for (int i = 0; i < 8; i++)
                {
                    pos += dir * ps;
                    float3 col = SampleSrc(pos).rgb;
                    if (!IsColorSimilar(refColor, col, threshold))
                        break;
                    length += 1.0;
                }
                
                return length;
            }
            
            // 计算连通分量大小（论文 Sparse Pixels 启发式）
            // 简化版：统计 8x8 窗口内相似颜色像素数量
            float CountConnectedComponent(float2 texCoord, float2 ps, float3 refColor, float threshold)
            {
                float count = 0;
                
                [unroll]
                for (int y = -4; y <= 3; y++)
                {
                    [unroll]
                    for (int x = -4; x <= 3; x++)
                    {
                        float3 col = SampleSrc(texCoord + float2(x * ps.x, y * ps.y)).rgb;
                        if (IsColorSimilar(refColor, col, threshold))
                            count += 1.0;
                    }
                }
                
                return count;
            }
            
            // ================================================================
            // 对角线歧义解决（论文核心）
            // ================================================================
            
            // 判断应该连接哪条对角线
            // 返回值: 1 = 连接主对角线 (左上-右下), -1 = 连接副对角线 (右上-左下), 0 = 都不连接
            float ResolveDiagonalAmbiguity(
                float2 texCoord, float2 ps,
                float3 p[9], // 3x3 邻域
                float threshold
            )
            {
                // 检查是否存在对角线歧义
                // 2x2 块中只有对角线连接，没有水平/垂直连接
                
                // 主对角线: p[0]-p[8] (左上-右下)
                // 副对角线: p[2]-p[6] (右上-左下)
                
                bool sim04 = IsColorSimilar(p[0], p[4], threshold); // 左上-中心
                bool sim14 = IsColorSimilar(p[1], p[4], threshold); // 上-中心
                bool sim24 = IsColorSimilar(p[2], p[4], threshold); // 右上-中心
                bool sim34 = IsColorSimilar(p[3], p[4], threshold); // 左-中心
                bool sim54 = IsColorSimilar(p[5], p[4], threshold); // 右-中心
                bool sim64 = IsColorSimilar(p[6], p[4], threshold); // 左下-中心
                bool sim74 = IsColorSimilar(p[7], p[4], threshold); // 下-中心
                bool sim84 = IsColorSimilar(p[8], p[4], threshold); // 右下-中心
                
                // 检查右下 2x2 块的歧义: p[4], p[5], p[7], p[8]
                bool h45 = sim54; // 水平连接 4-5
                bool h78 = IsColorSimilar(p[7], p[8], threshold);
                bool v47 = sim74; // 垂直连接 4-7
                bool v58 = IsColorSimilar(p[5], p[8], threshold);
                bool d48 = sim84; // 主对角线 4-8
                bool d57 = IsColorSimilar(p[5], p[7], threshold); // 副对角线 5-7
                
                // 如果有水平或垂直连接，不是歧义情况
                if (h45 || h78 || v47 || v58)
                {
                    // 全连通区域，两条对角线都可以安全移除
                    if (d48 && d57) return 0;
                    if (d48) return 1;
                    if (d57) return -1;
                    return 0;
                }
                
                // 只有对角线连接 - 这是歧义情况
                if (!d48 && !d57) return 0; // 没有对角线连接
                if (d48 && !d57) return 1;  // 只有主对角线
                if (!d48 && d57) return -1; // 只有副对角线
                
                // 两条对角线都存在 - 需要用启发式解决
                float vote = 0;
                
                // ========================================
                // 启发式 1: Curves (曲线连续性)
                // ========================================
                // 计算两条对角线各自所在曲线的长度
                float curve48 = 1.0; // 主对角线的曲线长度
                float curve57 = 1.0; // 副对角线的曲线长度
                
                // 追踪主对角线方向的曲线
                curve48 += TraceCurveLength(texCoord, ps, p[4], float2(-1, -1), threshold);
                curve48 += TraceCurveLength(texCoord + ps, ps, p[8], float2(1, 1), threshold);
                
                // 追踪副对角线方向的曲线
                curve57 += TraceCurveLength(texCoord + float2(ps.x, 0), ps, p[5], float2(1, -1), threshold);
                curve57 += TraceCurveLength(texCoord + float2(0, ps.y), ps, p[7], float2(-1, 1), threshold);
                
                vote += (curve48 - curve57) * _CurveWeight;
                
                // ========================================
                // 启发式 2: Sparse Pixels (稀疏性)
                // ========================================
                // 稀疏的颜色更可能是前景
                float sparse4 = CountConnectedComponent(texCoord, ps, p[4], threshold);
                float sparse5 = CountConnectedComponent(texCoord + float2(ps.x, 0), ps, p[5], threshold);
                float sparse7 = CountConnectedComponent(texCoord + float2(0, ps.y), ps, p[7], threshold);
                float sparse8 = CountConnectedComponent(texCoord + ps, ps, p[8], threshold);
                
                float sparseMain = (sparse4 + sparse8) * 0.5;
                float sparseAnti = (sparse5 + sparse7) * 0.5;
                
                // 更稀疏的应该保持连接
                vote += (sparseAnti - sparseMain) * _SparseWeight * 0.1;
                
                // ========================================
                // 启发式 3: Islands (避免孤岛)
                // ========================================
                // 检查断开连接是否会产生单像素孤岛
                
                // 检查 p[4] 的连接数
                float valence4 = (sim04 ? 1 : 0) + (sim14 ? 1 : 0) + (sim24 ? 1 : 0) + 
                                 (sim34 ? 1 : 0) + (sim54 ? 1 : 0) + 
                                 (sim64 ? 1 : 0) + (sim74 ? 1 : 0) + (sim84 ? 1 : 0);
                
                // 如果只有对角线连接，断开会产生孤岛
                if (valence4 <= 1)
                {
                    if (sim84) vote += _IslandWeight;
                }
                
                // 检查 p[8] 的连接数
                float3 p8neighbors[9];
                Sample3x3(texCoord + ps, ps, p8neighbors);
                float valence8 = 0;
                [unroll]
                for (int i = 0; i < 9; i++)
                {
                    if (i != 4 && IsColorSimilar(p[8], p8neighbors[i], threshold))
                        valence8 += 1;
                }
                
                if (valence8 <= 1)
                {
                    if (sim84) vote += _IslandWeight;
                }
                
                // 类似检查副对角线的 p[5] 和 p[7]
                float3 p5neighbors[9];
                Sample3x3(texCoord + float2(ps.x, 0), ps, p5neighbors);
                float valence5 = 0;
                [unroll]
                for (int j = 0; j < 9; j++)
                {
                    if (j != 4 && IsColorSimilar(p[5], p5neighbors[j], threshold))
                        valence5 += 1;
                }
                
                if (valence5 <= 1)
                {
                    if (d57) vote -= _IslandWeight;
                }
                
                // 返回决策
                if (abs(vote) < 0.001) return 0; // 平局，都断开
                return vote > 0 ? 1 : -1;
            }
            
            // ================================================================
            // 像素单元重塑与平滑插值（改进版）
            // ================================================================
            // 
            // 基于论文的 Voronoi 图概念，但使用更激进的混合策略
            // 在边缘处产生明显的抗锯齿效果
            //
            
            // 计算重塑后的子像素颜色
            float4 ComputeReshapedColor(
                float2 f, // 子像素位置 [0,1]
                float3 p[9],
                float diag, // 对角线决策: >0 主对角线, <0 副对角线, =0 无
                float threshold
            )
            {
                // 中心像素
                float3 E = p[4];
                
                // 8 邻居
                float3 A = p[0], B = p[1], C = p[2];
                float3 D = p[3],          F = p[5];
                float3 G = p[6], H = p[7], I = p[8];
                
                // 计算与邻居的相似性
                bool simB = IsColorSimilar(B, E, threshold);
                bool simD = IsColorSimilar(D, E, threshold);
                bool simF = IsColorSimilar(F, E, threshold);
                bool simH = IsColorSimilar(H, E, threshold);
                bool simI = IsColorSimilar(I, E, threshold);
                
                // 抗锯齿宽度
                float aaWidth = _Antialiasing * 0.5;
                float smoothWidth = _Smoothness * 0.3;
                
                // 初始化结果为中心像素
                float3 result = E;
                
                // ========================================
                // 处理四个象限的边缘混合
                // ========================================
                
                // 右下象限 (f.x >= 0.5 && f.y >= 0.5)
                if (f.x >= 0.5 && f.y >= 0.5)
                {
                    float2 lf = (f - 0.5) * 2.0; // 局部坐标 [0,1]
                    
                    // 主对角线连接 (E-I)
                    if (diag > 0.5)
                    {
                        // 沿对角线平滑，但在垂直于对角线方向上保持锐利边缘
                        float diagPos = (lf.x + lf.y) * 0.5; // 沿对角线的位置
                        float perpDist = abs(lf.x - lf.y) * 0.707; // 垂直于对角线的距离
                        
                        // 对角线上的颜色渐变
                        float3 diagColor = lerp(E, I, diagPos);
                        
                        // 根据对角线决策，F 和 H 被"切断"
                        // 使用软边缘
                        float edgeFade = smoothstep(0, aaWidth, perpDist);
                        
                        if (lf.x > lf.y)
                        {
                            // 靠近 F 侧
                            if (!simF)
                            {
                                // F 是不同颜色，需要在边缘处做抗锯齿
                                float edgeDist = perpDist;
                                float aa = smoothstep(0, aaWidth, edgeDist);
                                result = lerp(F, diagColor, aa);
                            }
                            else
                            {
                                result = lerp(E, lerp(F, I, diagPos), lf.x);
                            }
                        }
                        else
                        {
                            // 靠近 H 侧
                            if (!simH)
                            {
                                float edgeDist = perpDist;
                                float aa = smoothstep(0, aaWidth, edgeDist);
                                result = lerp(H, diagColor, aa);
                            }
                            else
                            {
                                result = lerp(E, lerp(H, I, diagPos), lf.y);
                            }
                        }
                    }
                    // 副对角线连接 (F-H)
                    else if (diag < -0.5)
                    {
                        float antiDiagPos = (lf.x + (1.0 - lf.y)) * 0.5;
                        float perpDist = abs(lf.x - (1.0 - lf.y)) * 0.707;
                        
                        // 副对角线切断了 E-I 连接
                        if (lf.x + lf.y < 1.0)
                        {
                            // E 侧
                            result = E;
                        }
                        else
                        {
                            // 超过副对角线，混合 F 和 H
                            float t = (lf.x + lf.y - 1.0);
                            if (lf.x > 1.0 - lf.y)
                            {
                                result = lerp(E, F, t * (1.0 + smoothWidth));
                            }
                            else
                            {
                                result = lerp(E, H, t * (1.0 + smoothWidth));
                            }
                        }
                    }
                    // 无对角线连接
                    else
                    {
                        // 检查水平和垂直连接
                        if (simF && simH && simI)
                        {
                            // 全连通，双线性插值
                            result = lerp(lerp(E, F, lf.x), lerp(H, I, lf.x), lf.y);
                        }
                        else if (simF && simH)
                        {
                            // F 和 H 连通，但 I 不同
                            float corner = max(lf.x, lf.y);
                            float aa = smoothstep(1.0 - aaWidth, 1.0, corner);
                            float3 edge = lerp(lerp(E, F, lf.x), lerp(H, I, lf.x), lf.y);
                            result = lerp(lerp(E, F, lf.x * 0.5) + lerp(E, H, lf.y * 0.5) - E, edge, aa);
                            result = lerp(lerp(E, F, lf.x), lerp(E, H, lf.y), lf.y / (lf.x + lf.y + 0.001));
                        }
                        else if (simF)
                        {
                            float aa = smoothstep(0, aaWidth, 1.0 - lf.x);
                            result = lerp(F, E, aa);
                        }
                        else if (simH)
                        {
                            float aa = smoothstep(0, aaWidth, 1.0 - lf.y);
                            result = lerp(H, E, aa);
                        }
                        else
                        {
                            // 孤立像素，边缘抗锯齿
                            float edgeDist = min(1.0 - lf.x, 1.0 - lf.y);
                            if (edgeDist < aaWidth)
                            {
                                float aa = edgeDist / aaWidth;
                                float3 edgeColor = (lf.x > lf.y) ? F : H;
                                result = lerp(edgeColor, E, aa);
                            }
                        }
                    }
                }
                // 左上象限
                else if (f.x < 0.5 && f.y < 0.5)
                {
                    float2 lf = f * 2.0;
                    
                    bool simA = IsColorSimilar(A, E, threshold);
                    if (simD && simB && simA)
                    {
                        result = lerp(lerp(A, B, lf.x), lerp(D, E, lf.x), lf.y);
                    }
                    else if (simD && simB)
                    {
                        result = lerp(lerp(D, E, 1.0 - lf.y), lerp(B, E, lf.y), lf.x);
                    }
                    else if (simD)
                    {
                        float aa = smoothstep(0, aaWidth, lf.x);
                        result = lerp(D, E, aa);
                    }
                    else if (simB)
                    {
                        float aa = smoothstep(0, aaWidth, lf.y);
                        result = lerp(B, E, aa);
                    }
                    else
                    {
                        float edgeDist = min(lf.x, lf.y);
                        if (edgeDist < aaWidth)
                        {
                            float aa = edgeDist / aaWidth;
                            float3 edgeColor = (lf.x < lf.y) ? D : B;
                            result = lerp(edgeColor, E, aa);
                        }
                    }
                }
                // 右上象限
                else if (f.x >= 0.5 && f.y < 0.5)
                {
                    float2 lf = float2((f.x - 0.5) * 2.0, f.y * 2.0);
                    
                    bool simC = IsColorSimilar(C, E, threshold);
                    if (simF && simB && simC)
                    {
                        result = lerp(lerp(B, C, lf.x), lerp(E, F, lf.x), lf.y);
                    }
                    else if (simF && simB)
                    {
                        result = lerp(lerp(B, E, lf.y), lerp(E, F, 1.0 - lf.y), lf.x);
                    }
                    else if (simF)
                    {
                        float aa = smoothstep(0, aaWidth, 1.0 - lf.x);
                        result = lerp(F, E, aa);
                    }
                    else if (simB)
                    {
                        float aa = smoothstep(0, aaWidth, lf.y);
                        result = lerp(B, E, aa);
                    }
                    else
                    {
                        float edgeDist = min(1.0 - lf.x, lf.y);
                        if (edgeDist < aaWidth)
                        {
                            float aa = edgeDist / aaWidth;
                            float3 edgeColor = (lf.x > 1.0 - lf.y) ? F : B;
                            result = lerp(edgeColor, E, aa);
                        }
                    }
                }
                // 左下象限
                else
                {
                    float2 lf = float2(f.x * 2.0, (f.y - 0.5) * 2.0);
                    
                    bool simG = IsColorSimilar(G, E, threshold);
                    if (simD && simH && simG)
                    {
                        result = lerp(lerp(D, E, lf.x), lerp(G, H, lf.x), lf.y);
                    }
                    else if (simD && simH)
                    {
                        result = lerp(lerp(D, E, 1.0 - lf.y), lerp(E, H, lf.y), 1.0 - lf.x);
                    }
                    else if (simD)
                    {
                        float aa = smoothstep(0, aaWidth, lf.x);
                        result = lerp(D, E, aa);
                    }
                    else if (simH)
                    {
                        float aa = smoothstep(0, aaWidth, 1.0 - lf.y);
                        result = lerp(H, E, aa);
                    }
                    else
                    {
                        float edgeDist = min(lf.x, 1.0 - lf.y);
                        if (edgeDist < aaWidth)
                        {
                            float aa = edgeDist / aaWidth;
                            float3 edgeColor = (lf.x < 1.0 - lf.y) ? D : H;
                            result = lerp(edgeColor, E, aa);
                        }
                    }
                }
                
                return float4(result, 1.0);
            }
            
            // ================================================================
            // B-Spline 曲线拟合（简化版）
            // ================================================================
            
            // 二次 B-Spline 基函数
            float3 QuadraticBSpline(float t, float3 p0, float3 p1, float3 p2)
            {
                float t2 = t * t;
                float mt = 1.0 - t;
                float mt2 = mt * mt;
                
                return 0.5 * (mt2 * p0 + (2.0 * mt * t + 0.5) * p1 + t2 * p2);
            }
            
            // 计算边缘的平滑混合
            float ComputeEdgeBlend(float2 f, float2 edgeDir, float edgeDist)
            {
                // 到边缘的距离
                float d = abs(dot(f - float2(0.5, 0.5), edgeDir));
                
                // 平滑过渡
                float blend = smoothstep(0.0, _Antialiasing * 0.5, edgeDist - d);
                return blend;
            }
            
            // ================================================================
            // 主片段着色器
            // ================================================================
            
            float4 DepixelizeFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                
                float2 uv = input.texcoord;
                float4 texelSize = _BlitTexture_TexelSize;
                
                // 调试模式 2: 强制输出品红色确认 shader 在运行
                if (_DebugMode > 1.5)
                {
                    return float4(1, 0, 1, 1); // 品红色
                }
                
                // 计算源像素网格的纹素大小
                float2 srcTexelSize = texelSize.xy * _PixelScale;
                float2 ps = srcTexelSize;
                
                // 计算当前像素在源像素网格中的位置
                float2 srcPixelPos = uv / srcTexelSize;
                float2 srcPixelCenter = floor(srcPixelPos) + 0.5;
                float2 texCoord = srcPixelCenter * srcTexelSize;
                
                // 子像素位置 (0-1)
                float2 f = frac(srcPixelPos);
                
                // 采样 3x3 邻域
                float3 p[9];
                Sample3x3(texCoord, ps, p);
                
                // 解决对角线歧义
                float diag = ResolveDiagonalAmbiguity(texCoord, ps, p, _ColorThreshold);
                
                // 计算重塑后的颜色
                float4 result = ComputeReshapedColor(f, p, diag, _ColorThreshold);
                
                // 调试模式
                if (_DebugMode > 0.5)
                {
                    // 显示对角线决策
                    if (abs(diag) > 0.5)
                    {
                        // 在边缘显示红色（主对角线）或蓝色（副对角线）
                        float edgeDist = min(min(f.x, 1.0 - f.x), min(f.y, 1.0 - f.y));
                        if (edgeDist < 0.1)
                        {
                            if (diag > 0)
                                result.rgb = lerp(result.rgb, float3(1, 0, 0), 0.5);
                            else
                                result.rgb = lerp(result.rgb, float3(0, 0, 1), 0.5);
                        }
                    }
                    
                    // 显示像素边界
                    float gridLine = max(
                        step(0.95, f.x) + step(f.x, 0.05),
                        step(0.95, f.y) + step(f.y, 0.05)
                    );
                    result.rgb = lerp(result.rgb, float3(0.3, 0.3, 0.3), gridLine * 0.3);
                }
                
                return result;
            }
            ENDHLSL
        }
    }
    
    FallBack Off
}
