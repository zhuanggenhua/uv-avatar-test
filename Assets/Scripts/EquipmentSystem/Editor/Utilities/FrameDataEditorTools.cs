using System;
using System.Collections.Generic;
using System.Linq;
 using UnityEditor;
using UnityEngine;

namespace EquipmentSystem.Editor
{
    /// <summary>
    /// 帧数据编辑器业务工具类
    /// 
    /// 功能：
    /// 1. 自动检测 - 从精灵表中自动识别身体部位
    /// 2. 批量操作 - 对所有帧进行扩展、收缩、修复等操作
    /// 3. 方向生成 - 从SE方向自动生成其他三个方向的数据
    /// 
    /// 注意：这些功能都是高级业务逻辑，依赖于FrameDataAlgorithms中的核心算法
    /// </summary>
    public static class FrameDataEditorTools
    {
        #region 检测参数

        /// <summary>
        /// 自动检测参数类
        /// 保存检测过程中需要的所有上下文信息
        /// </summary>
        public class DetectParams
        {
            public Color32[] pixels;
            public DetectConfig cfg;
            public bool facingRight;
            public Vector2Int firstPixel;
            public Vector2Int? torsoStart;
            public int headLeft, headRight, footMinY;
            public Vector2Int frameSize;
            public Texture2D sprite;
            public int frame;
            public int row;

            // 颜色映射（SE方向固定，其他方向由SE生成）
            public Color32 GetLeftHandColor() => cfg.leftHandColor;
            public Color32 GetRightHandColor() => cfg.rightHandColor;
            public Color32 GetLeftFootColor() => cfg.leftFootColor;
            public Color32 GetRightFootColor() => cfg.rightFootColor;
        }

        #endregion

        #region 自动检测

        /// <summary>
        /// 获取自动检测所需的参数
        /// 
        /// 检测流程：
        /// 1. 查找第一个皮肤色像素（作为头部开始位置）
        /// 2. 根据头部位置推算躯干位置
        /// 3. 计算手脚的搜索范围
        /// </summary>
        /// <param name="sprite">精灵表纹理</param>
        /// <param name="row">当前行索引（方向）</param>
        /// <param name="frame">当前帧索引</param>
        /// <param name="frameSize">单帧尺寸</param>
        /// <param name="data">角色帧数据（包含检测配置）</param>
        /// <returns>检测参数，如果找不到皮肤色则返回null</returns>
        public static DetectParams GetDetectParams(Texture2D sprite, int row, int frame, Vector2Int frameSize, CharacterFrameData data)
        {
            if (sprite == null || !sprite.isReadable || data == null)
                return null;

            var cfg = data.detectConfig;
            var pixels = sprite.GetPixels32();

            // 判断朝向
            bool facingRight = (row == 0 || row == 2);  // SE/NE

            // 查找第一个皮肤色像素（排除武器等非皮肤颜色）
            Vector2Int? firstPixel = null;
            for (int y = 0; y < frameSize.y && !firstPixel.HasValue; y++)
            {
                for (int x = 0; x < frameSize.x; x++)
                {
                    var c = GetPixelAt(pixels, x, y, frame, row, frameSize, sprite);
                    if (cfg.IsSkinLike(c))
                    {
                        firstPixel = new Vector2Int(x, y);
                        break;
                    }
                }
            }

            if (!firstPixel.HasValue) return null;

            // 查找躯干起始点（在头部下面，使用配置的头部高度）
            var headDetectSize = data.headDetectSize;
            int torsoRowY = firstPixel.Value.y + headDetectSize.y;
            int headLeft = firstPixel.Value.x;  // 头部最左列
            Vector2Int? torsoStart = null;

            if (torsoRowY < frameSize.y)
            {
                // 从头部最左列开始查找（躯干起点 >= 头部最左列）
                for (int x = headLeft; x < frameSize.x; x++)
                {
                    if (cfg.IsColoredPixel(GetPixelAt(pixels, x, torsoRowY, frame, row, frameSize, sprite)))
                    {
                        torsoStart = new Vector2Int(x, torsoRowY);
                        break;
                    }
                }
            }

            return new DetectParams
            {
                pixels = pixels,
                cfg = cfg,
                facingRight = facingRight,
                firstPixel = firstPixel.Value,
                torsoStart = torsoStart,
                headLeft = headLeft,
                headRight = headLeft + headDetectSize.x,
                footMinY = torsoStart.HasValue ? torsoStart.Value.y + data.torsoDetectSize.y : frameSize.y,
                frameSize = frameSize,
                sprite = sprite,
                frame = frame,
                row = row
            };
        }

        static Color32 GetPixelAt(Color32[] pixels, int x, int y, int frame, int row, Vector2Int frameSize, Texture2D sprite)
        {
            int gx = frame * frameSize.x + x;
            int gy = sprite.height - 1 - (row * frameSize.y + y);

            if (gx < 0 || gx >= sprite.width || gy < 0 || gy >= sprite.height)
                return default;

            return pixels[gy * sprite.width + gx];
        }

        /// <summary>
        /// 检测手脚部位
        /// 
        /// 检测逻辑：
        /// - 手部：从有色像素块的边缘开始，按列扫描找到指定颜色
        /// - 脚部：在最底部行扫描找到指定颜色
        /// </summary>
        public static HashSet<Vector2Int> DetectLimb(DetectParams p, CharacterBodyPart part, Color32 color, CharacterFrameData data)
        {
            var result = new HashSet<Vector2Int>();
            bool isHand = part == CharacterBodyPart.LeftHand || part == CharacterBodyPart.RightHand;
            bool isLeft = part == CharacterBodyPart.LeftHand || part == CharacterBodyPart.LeftFoot;

            // 先找到有色像素块的边界
            int minX = p.frameSize.x, maxX = -1, maxY = -1;
            for (int y = 0; y < p.frameSize.y; y++)
            {
                for (int x = 0; x < p.frameSize.x; x++)
                {
                    if (p.cfg.IsColoredPixel(GetPixelFromParams(p, x, y)))
                    {
                        minX = Mathf.Min(minX, x);
                        maxX = Mathf.Max(maxX, x);
                        maxY = Mathf.Max(maxY, y);
                    }
                }
            }

            if (maxX < 0) return result;  // 没有有色像素

            int colCount = 2;  // 搜索两列/行

            if (isHand)
            {
                // 手部检测逻辑
                int xStart, xEnd, xStep;

                if (p.facingRight == isLeft)
                {
                    xStart = maxX;
                    xEnd = Mathf.Max(0, maxX - colCount);
                    xStep = -1;
                }
                else
                {
                    xStart = minX;
                    xEnd = Mathf.Min(p.frameSize.x, minX + colCount);
                    xStep = 1;
                }

                var headDetectSize = data.headDetectSize;
                int headBottomY = p.firstPixel.y + headDetectSize.y;

                for (int x = xStart; x != xEnd; x += xStep)
                {
                    var matchedYs = new List<int>();
                    for (int y = p.frameSize.y - 1; y >= 0; y--)
                    {
                        if (p.cfg.IsLimbColorMatch(GetPixelFromParams(p, x, y), color))
                            matchedYs.Add(y);
                    }

                    if (matchedYs.Count > 0)
                    {
                        foreach (int y in matchedYs)
                        {
                            if (y >= headBottomY)
                            {
                                result.Add(new Vector2Int(x, y));
                                break;
                            }
                        }
                        if (result.Count > 0) break;
                    }
                }
            }
            else
            {
                // 脚部检测逻辑
                int footY = maxY;
                int xStart = isLeft ? maxX : minX;
                int xEnd = isLeft ? minX - 1 : maxX + 1;
                int xStep = isLeft ? -1 : 1;

                for (int x = xStart; x != xEnd; x += xStep)
                {
                    if (p.cfg.IsLimbColorMatch(GetPixelFromParams(p, x, footY), color))
                        result.Add(new Vector2Int(x, footY));
                }
            }

            return result;
        }

#region 找眼睛
        /// <summary>
        /// 检测头部区域内的眼睛
        /// 
        /// 检测原理：
        /// 1. 在头部检测区域中间一行扫描黑色眼睛像素（优先使用黑色）
        /// 2. 若该行没有任何黑色眼睛像素，则使用闭眼颜色再次扫描
        /// 3. 根据头部中心划分左右眼：SE朝向时，画面左边是角色右眼，画面右边是角色左眼
        /// </summary>
        public static void DetectEyes(DetectParams p, Vector2Int headDetectSize,
            out HashSet<Vector2Int> leftEye, out HashSet<Vector2Int> rightEye,
            out bool leftEyeClosed, out bool rightEyeClosed)
        {
            leftEye = new HashSet<Vector2Int>();
            rightEye = new HashSet<Vector2Int>();
            leftEyeClosed = false;
            rightEyeClosed = false;

            int width = headDetectSize.x;
            int height = headDetectSize.y;
            if (width <= 0 || height <= 0)
                return;

            float headLeft = p.firstPixel.x;
            float headRight = p.firstPixel.x + width - 1;
            float headCenterX = (headLeft + headRight) * 0.5f;

            var outlineClusters = CollectEyeClusters(p, headDetectSize, c => p.cfg.IsOutline(c));
            if (AssignEyesFromClusters(outlineClusters, headCenterX, leftEye, rightEye, false,
                out leftEyeClosed, out rightEyeClosed))
                return;

            var closedClusters = CollectEyeClusters(p, headDetectSize, c => p.cfg.IsClosedEyeColor(c));
            AssignEyesFromClusters(closedClusters, headCenterX, leftEye, rightEye, true,
                out leftEyeClosed, out rightEyeClosed);
        }

        static List<List<Vector2Int>> CollectEyeClusters(DetectParams p, Vector2Int headDetectSize, Func<Color32, bool> matchPredicate)
        {
            var result = new List<List<Vector2Int>>();

            int width = headDetectSize.x;
            int height = headDetectSize.y;
            if (width <= 0 || height <= 0)
                return result;

            int midLocalY = height / 2;
            int py = p.firstPixel.y + midLocalY;
            if (py < 0 || py >= p.frameSize.y)
                return result;

            List<Vector2Int> current = null;
            for (int dx = 0; dx < width; dx++)
            {
                int px = p.firstPixel.x + dx;

                bool match = false;
                if (px >= 0 && px < p.frameSize.x)
                {
                    var c = GetPixelFromParams(p, px, py);
                    match = matchPredicate(c);
                }

                if (!match)
                {
                    if (current != null && current.Count > 0)
                    {
                        result.Add(current);
                        current = null;
                    }
                    continue;
                }

                if (current == null)
                    current = new List<Vector2Int>();
                current.Add(new Vector2Int(px, py));
            }

            if (current != null && current.Count > 0)
                result.Add(current);

            return result;
        }

        static bool AssignEyesFromClusters(List<List<Vector2Int>> clusters, float headCenterX,
            HashSet<Vector2Int> leftEye, HashSet<Vector2Int> rightEye,
            bool closed,
            out bool leftEyeClosed, out bool rightEyeClosed)
        {
            leftEyeClosed = false;
            rightEyeClosed = false;

            if (clusters == null || clusters.Count == 0)
                return false;

            int clusterCount = clusters.Count;
            var centersX = new float[clusterCount];
            for (int i = 0; i < clusterCount; i++)
            {
                float sumX = 0f;
                var list = clusters[i];
                for (int j = 0; j < list.Count; j++)
                    sumX += list[j].x;
                centersX[i] = sumX / Mathf.Max(1, list.Count);
            }

            var candidateIndices = new List<int>();
            for (int i = 0; i < clusterCount; i++)
                candidateIndices.Add(i);

            candidateIndices.Sort((a, b) =>
            {
                float da = Mathf.Abs(centersX[a] - headCenterX);
                float db = Mathf.Abs(centersX[b] - headCenterX);
                return da.CompareTo(db);
            });

            if (candidateIndices.Count == 1)
            {
                int idx = candidateIndices[0];
                float cx = centersX[idx];

                if (cx < headCenterX)
                {
                    foreach (var pos in clusters[idx])
                        rightEye.Add(pos);
                    rightEyeClosed = closed;
                }
                else
                {
                    foreach (var pos in clusters[idx])
                        leftEye.Add(pos);
                    leftEyeClosed = closed;
                }

                return leftEye.Count > 0 || rightEye.Count > 0;
            }

            int idx0 = candidateIndices[0];
            int idx1 = candidateIndices[1];

            // 屏幕左边的是角色右眼，右边的是角色左眼
            if (centersX[idx0] <= centersX[idx1])
            {
                foreach (var pos in clusters[idx0])
                    rightEye.Add(pos);
                foreach (var pos in clusters[idx1])
                    leftEye.Add(pos);
            }
            else
            {
                foreach (var pos in clusters[idx1])
                    rightEye.Add(pos);
                foreach (var pos in clusters[idx0])
                    leftEye.Add(pos);
            }

            if (rightEye.Count > 0)
                rightEyeClosed = closed;
            if (leftEye.Count > 0)
                leftEyeClosed = closed;

            return leftEye.Count > 0 || rightEye.Count > 0;
        }
        #endregion

        static Color32 GetPixelFromParams(DetectParams p, int x, int y)
        {
            if (x < 0 || x >= p.frameSize.x || y < 0 || y >= p.frameSize.y)
                return default;

            // 使用与 GetPixelAt 相同的逻辑
            return GetPixelAt(p.pixels, x, y, p.frame, p.row, p.frameSize, p.sprite);
        }

        #endregion

        #region 批量操作

        /// <summary>
        /// 批量扩展所有帧的部位区域
        /// 
        /// 功能：为所有动画帧的头部和身体区域添加额外像素
        /// 用途：适应更大的装备贴图，避免装备被裁剪
        /// 
        /// 支持并行处理以提高性能
        /// </summary>
        /// <param name="data">要处理的帧数据</param>
        public static void ExpandAllPartsForAllFrames(CharacterFrameData data, AnimationData anim, RegionExpandPose pose)
        {
            if (data == null || anim == null) return;

            var paletteSize = data.paletteSize;
            var expandParams = new ExpandParams
            {
                headUp = data.headExpandUp,
                headDown = data.headExpandDown,
                headSide = data.headExpandSide,
                bodyUp = data.bodyExpandUp,
                bodyDown = data.bodyExpandDown,
                bodySide = data.bodyExpandSide,
                bodyUpStartStep = data.bodyExpandUpStartStep <= 0 ? 1 : data.bodyExpandUpStartStep,
                bodyDownStartStep = data.bodyExpandDownStartStep <= 0 ? 1 : data.bodyExpandDownStartStep
            };

            int expandedCount = 0;

            foreach (var frame in anim.frames)
            {
                bool expanded = false;

                    // 扩展身体
                    var torsoRegion = frame.bodyRegions.FirstOrDefault(r => r.part == CharacterBodyPart.Torso);
                    if (torsoRegion != null)
                    {
                        var originalPixels = torsoRegion.pixels;
                        var originalPositions = new HashSet<Vector2Int>(originalPixels.Select(px => px.position));
                        var originalCore = new HashSet<Vector2Int>(originalPixels.Where(px => px.isCore).Select(px => px.position));

                        var pixels = new HashSet<Vector2Int>(originalPositions);
                        var uvs = originalPixels.Where(px => px.HasUV).ToDictionary(px => px.position, px => px.uv);
                        int before = pixels.Count;

                        var frameSize = anim.frameSize;

                        // 根据姿态把“身体坐标系”的扩展量映射到屏幕坐标的 up/down/left/right
                        int bodyUp = expandParams.bodyUp;
                        int bodyDown = expandParams.bodyDown;
                        int bodyLeft = expandParams.bodySide;
                        int bodyRight = expandParams.bodySide;
                        FrameDataAlgorithms.MapExpandByPose(pose, expandParams.bodyUp, expandParams.bodyDown, expandParams.bodySide,
                            out bodyUp, out bodyDown, out bodyLeft, out bodyRight);

                        FrameDataAlgorithms.ExpandRegionWithBoundaryUV(pixels, uvs, 
                            bodyUp, bodyDown, bodyLeft, bodyRight, 
                            frameSize, paletteSize, expandParams.bodyUpStartStep, expandParams.bodyDownStartStep, pose);

                        if (pixels.Count != before)
                        {
                            torsoRegion.pixels.Clear();
                            foreach (var pos in pixels)
                            {
                                bool wasOriginal = originalPositions.Contains(pos);
                                bool isCore = wasOriginal && originalCore.Contains(pos);
                                var pixel = new BodyPartPixel
                                {
                                    part = CharacterBodyPart.Torso,
                                    position = pos,
                                    isCore = isCore
                                };
                                if (uvs.TryGetValue(pos, out var uv))
                                    pixel.uv = uv;
                                torsoRegion.pixels.Add(pixel);
                            }
                            expanded = true;
                        }
                    }

                    // 扩展头部
                    var headRegion = frame.bodyRegions.FirstOrDefault(r => r.part == CharacterBodyPart.Head);
                    if (headRegion != null)
                    {
                        var originalPixels = headRegion.pixels;
                        var originalPositions = new HashSet<Vector2Int>(originalPixels.Select(px => px.position));
                        var originalCore = new HashSet<Vector2Int>(originalPixels.Where(px => px.isCore).Select(px => px.position));

                        var pixels = new HashSet<Vector2Int>(originalPositions);
                        var uvs = originalPixels.Where(px => px.HasUV).ToDictionary(px => px.position, px => px.uv);
                        int before = pixels.Count;

                        var frameSize = anim.frameSize;

                        // 根据姿态把“头部坐标系”的扩展量映射到屏幕坐标的 up/down/left/right
                        int headUp = expandParams.headUp;
                        int headDown = expandParams.headDown;
                        int headLeft = expandParams.headSide;
                        int headRight = expandParams.headSide;
                        FrameDataAlgorithms.MapExpandByPose(pose, expandParams.headUp, expandParams.headDown, expandParams.headSide,
                            out headUp, out headDown, out headLeft, out headRight);

                        FrameDataAlgorithms.ExpandRegionWithBoundaryUV(pixels, uvs,
                            headUp, headDown, headLeft, headRight, 
                            frameSize, paletteSize, 1, 1, pose);

                        if (pixels.Count > before)
                        {
                            headRegion.pixels.Clear();
                            foreach (var pos in pixels)
                            {
                                bool wasOriginal = originalPositions.Contains(pos);
                                bool isCore = wasOriginal && originalCore.Contains(pos);
                                var pixel = new BodyPartPixel
                                {
                                    part = CharacterBodyPart.Head,
                                    position = pos,
                                    isCore = isCore
                                };
                                if (uvs.TryGetValue(pos, out var uv))
                                    pixel.uv = uv;
                                headRegion.pixels.Add(pixel);
                            }
                            expanded = true;
                        }
                    }

                    if (expanded) expandedCount++;
                }

            EditorUtility.SetDirty(data);
            Debug.Log($"[扩展] 已扩展 {expandedCount} 帧的区域");
        }

        /// <summary>
        /// 批量收缩所有帧的部位区域
        /// 
        /// 功能：从边缘向内删除像素，减小涂色范围
        /// 用途：清理过大的涂色区域，优化渲染性能
        /// 
        /// 支持并行处理以提高性能
        /// </summary>
        /// <param name="data">要处理的帧数据</param>
        public static void ShrinkAllPartsForAllFrames(CharacterFrameData data, AnimationData anim, RegionExpandPose pose)
        {
            if (data == null || anim == null) return;

            int shrunkCount = 0;

            foreach (var frame in anim.frames)
            {
                bool shrunk = false;

                foreach (var region in frame.bodyRegions)
                {
                    if (region.part != CharacterBodyPart.Head && region.part != CharacterBodyPart.Torso)
                        continue;

                    var originalPixels = region.pixels;
                    if (originalPixels == null || originalPixels.Count == 0)
                        continue;

                    // 优先：若存在核心像素，则按 isCore 进行收缩
                    bool hasCore = originalPixels.Any(px => px.isCore);
                    if (hasCore)
                    {
                        int beforeCount = originalPixels.Count;
                        region.pixels = originalPixels.Where(px => px.isCore).ToList();
                        if (region.pixels.Count < beforeCount)
                            shrunk = true;
                        continue;
                    }

                    // 否则退回旧的几何收缩逻辑，兼容无 isCore 的旧数据
                    int shrinkUp, shrinkDown, shrinkSide;
                    if (region.part == CharacterBodyPart.Head)
                    {
                        shrinkUp = data.headExpandUp;
                        shrinkDown = data.headExpandDown;
                        shrinkSide = data.headExpandSide;
                    }
                    else
                    {
                        shrinkUp = data.bodyExpandUp;
                        shrinkDown = data.bodyExpandDown;
                        shrinkSide = data.bodyExpandSide;
                    }

                    if (shrinkUp == 0 && shrinkDown == 0 && shrinkSide == 0)
                        continue;

                    var positions = new HashSet<Vector2Int>(originalPixels.Select(px => px.position));
                    var uvs = originalPixels.Where(px => px.HasUV).ToDictionary(px => px.position, px => px.uv);
                    int before = positions.Count;

                    Vector2Int detectSize = region.part == CharacterBodyPart.Head
                        ? data.headDetectSize
                        : data.torsoDetectSize;

                    FrameDataAlgorithms.ShrinkRegionByPoseAndDetectSize(
                        positions, uvs,
                        detectSize,
                        pose,
                        shrinkUp, shrinkDown, shrinkSide);

                    if (positions.Count < before)
                    {
                        region.pixels.Clear();
                        foreach (var pos in positions)
                        {
                            var pixel = new BodyPartPixel
                            {
                                part = region.part,
                                position = pos
                            };
                            if (uvs.TryGetValue(pos, out var uv))
                                pixel.uv = uv;
                            region.pixels.Add(pixel);
                        }
                        shrunk = true;
                    }
                }

                if (shrunk) shrunkCount++;
            }

            EditorUtility.SetDirty(data);
            Debug.Log($"[收缩] 已收缩 {shrunkCount} 帧的区域");
        }

        /// <summary>
        /// 从 SE 方向生成所有行的部位数据
        /// </summary>
        public static void GenerateAllRowsFromSE(CharacterFrameData data, AnimationData anim, int framesPerRow, Vector2Int frameSize)
        {
            if (data == null || anim == null) return;

            Undo.RecordObject(data, "从SE生成所有行");

            
            int totalGenerated = 0;
            
            for (int f = 0; f < framesPerRow; f++)
            {
                var seFrame = anim.GetFrame(f, 0);
                if (seFrame == null || seFrame.bodyRegions.Count == 0) continue;
                
                GenerateSWFrame(anim, f, seFrame, frameSize);
                GenerateNEFrame(anim, f, seFrame, frameSize);
                GenerateNWFrame(anim, f, seFrame, frameSize);
                
                totalGenerated++;
            }
            
            EditorUtility.SetDirty(data);
            Debug.Log($"从SE生成所有行完成: 共处理 {totalGenerated} 帧 × 3行 = {totalGenerated * 3} 帧数据");
        }

        /// <summary>
        /// 修复所有帧的贴图朝向
        /// 
        /// 功能：确保每个部位的spriteFacing与其所在行对应
        /// 行0=SE, 行1=SW, 行2=NE, 行3=NW
        /// 
        /// 用途：修复导入或手动编辑后的错误朝向
        /// </summary>
        /// <returns>修复的区域数量</returns>
        public static int FixAllFramesSpriteFacing(CharacterFrameData data)
        {
            if (data == null) return 0;
            
            int fixedCount = 0;
            
            foreach (var anim in data.animations)
            {
                foreach (var frame in anim.frames)
                {
                    CharacterFacing correctFacing = (CharacterFacing)frame.rowIndex;
                    if (frame.rowIndex < 0 || frame.rowIndex > 3)
                        correctFacing = CharacterFacing.SouthEast;
                    
                    foreach (var region in frame.bodyRegions)
                    {
                        if (region.spriteFacing != correctFacing)
                        {
                            region.spriteFacing = correctFacing;
                            fixedCount++;
                        }
                    }
                }
            }
            
            return fixedCount;
        }

        #endregion

        #region 帧生成辅助

        static void GenerateSWFrame(AnimationData anim, int f, FrameData seFrame, Vector2Int frameSize)
            => GenerateFrame(anim, f, seFrame, 1, true, true, frameSize);
        
        static void GenerateNEFrame(AnimationData anim, int f, FrameData seFrame, Vector2Int frameSize)
            => GenerateFrame(anim, f, seFrame, 2, false, false, frameSize);
        
        static void GenerateNWFrame(AnimationData anim, int f, FrameData seFrame, Vector2Int frameSize)
            => GenerateFrame(anim, f, seFrame, 3, true, false, frameSize);

        static void GenerateFrame(AnimationData anim, int frameIndex, FrameData sourceFrame, int targetRow,
            bool translatePos, bool includeEyes, Vector2Int frameSize)
        {
            var targetFrame = anim.GetOrCreateFrame(frameIndex, targetRow);
            targetFrame.bodyRegions.Clear();
            targetFrame.anchors.Clear();
            
            // 生成 UV 部位区域（头/身体）
            foreach (var sourceRegion in sourceFrame.bodyRegions)
            {
                var targetFacing = MapFacingForGeneratedFrame(sourceRegion.spriteFacing, targetRow);
                
                var newRegion = new BodyPartRegion
                {
                    part = sourceRegion.part,
                    orientation = sourceRegion.orientation,
                    spriteFacing = targetFacing,
                    variant = sourceRegion.variant
                };
                
                int offsetX = 0;
                if (translatePos && sourceRegion.pixels.Count > 0)
                {
                    var positions = sourceRegion.pixels.Select(p => p.position);
                    offsetX = FrameDataAlgorithms.CalculateMirrorTranslateOffset(positions, frameSize.x);
                }
                
                foreach (var px in sourceRegion.pixels)
                {
                    var newPos = translatePos 
                        ? FrameDataAlgorithms.TranslatePosition(px.position, offsetX)
                        : px.position;
                    newRegion.pixels.Add(new BodyPartPixel
                    {
                        part = sourceRegion.part,
                        position = newPos,
                        color = px.color,
                        uv = px.uv,
                        isCore = px.isCore
                    });
                }
                
                targetFrame.bodyRegions.Add(newRegion);
            }
            
            // 生成手脚蒙版
            if (sourceFrame.limbMask != null)
            {
                if (targetFrame.limbMask == null)
                    targetFrame.limbMask = new LimbMask();
                else
                    targetFrame.limbMask.Clear();
                
                CopyLimbMask(sourceFrame.limbMask.leftHand, targetFrame.limbMask.leftHand, translatePos, frameSize);
                CopyLimbMask(sourceFrame.limbMask.rightHand, targetFrame.limbMask.rightHand, translatePos, frameSize);
                CopyLimbMask(sourceFrame.limbMask.leftFoot, targetFrame.limbMask.leftFoot, translatePos, frameSize);
                CopyLimbMask(sourceFrame.limbMask.rightFoot, targetFrame.limbMask.rightFoot, translatePos, frameSize);
                if (includeEyes)
                {
                    // 眼睛：位置镜像 + 交换类型，保证 SE/SW 都是"右眼在屏幕左边"
                    if (translatePos)
                    {
                        CopyLimbMask(sourceFrame.limbMask.leftEye, targetFrame.limbMask.rightEye, true, frameSize);
                        CopyLimbMask(sourceFrame.limbMask.rightEye, targetFrame.limbMask.leftEye, true, frameSize);
                    }
                    else
                    {
                        CopyLimbMask(sourceFrame.limbMask.leftEye, targetFrame.limbMask.leftEye, false, frameSize);
                        CopyLimbMask(sourceFrame.limbMask.rightEye, targetFrame.limbMask.rightEye, false, frameSize);
                    }
                }
            }

            if (includeEyes)
            {
                if (translatePos)
                {
                    targetFrame.leftEyeClosed = sourceFrame.rightEyeClosed;
                    targetFrame.rightEyeClosed = sourceFrame.leftEyeClosed;
                }
                else
                {
                    targetFrame.leftEyeClosed = sourceFrame.leftEyeClosed;
                    targetFrame.rightEyeClosed = sourceFrame.rightEyeClosed;
                }
            }
            else
            {
                targetFrame.leftEyeClosed = false;
                targetFrame.rightEyeClosed = false;
            }
            
            // 受击描边帧：直接拷贝
            targetFrame.hitOutlineFrame = sourceFrame.hitOutlineFrame;
            
            // 生成锚点
            foreach (var anchor in sourceFrame.anchors)
            {
                targetFrame.anchors.Add(new AnchorPoint
                {
                    type = anchor.type,
                    position = translatePos ? FrameDataAlgorithms.MirrorPosition(anchor.position, frameSize.x) : anchor.position,
                    direction = anchor.direction
                });
            }
        }

        /// <summary>
        /// 根据 SE 行的 spriteFacing 和目标行索引，计算目标行的 spriteFacing
        /// 
        /// 规则（以 SE=0, SW=1, NE=2, NW=3 为编号）：
        /// - SE 填 SE(0)：恒等 → SW=SW, NE=NE, NW=NW
        /// - SE 填 SW(1)：左右对称 → SW=SE, NE=NW, NW=NE
        /// - SE 填 NE(2)：北向对位 → SW=NE, NE=SW, NW=SE
        /// - SE 填 NW(3)：北向对位 → SW=NE, NE=SW, NW=SE
        /// </summary>
        static CharacterFacing MapFacingForGeneratedFrame(CharacterFacing sourceFacing, int targetRow)
        {
            // 变换表：transformTable[变换类型][行索引] = 目标朝向
            // 行索引：SE=0, SW=1, NE=2, NW=3
            // 
            // 规则总结：
            // - SE 填南向（SE/SW）：用"恒等"或"左右对称"
            // - SE 填北向（NE/NW）：SW/NE/NW 三行都按"对位"取相对
            int[][] transformTable = 
            {
                new[] {0, 1, 2, 3}, // 恒等：SE→SE, SW→SW, NE→NE, NW→NW
                new[] {1, 0, 3, 2}, // 左右对称：SE→SW, SW→SE, NE→NW, NW→NE
                new[] {2, 2, 1, 0}, // SE填NE：SE→NE, SW→NE, NE→SW, NW→SE
                new[] {3, 2, 1, 0}, // SE填NW：SE→NW, SW→NE, NE→SW, NW→SE
            };
            
            int transformType = (int)sourceFacing;
            int rowIndex = targetRow;
            
            // 边界检查
            if (transformType < 0 || transformType > 3) transformType = 0;
            if (rowIndex < 0 || rowIndex > 3) rowIndex = 0;
            
            return (CharacterFacing)transformTable[transformType][rowIndex];
        }

        static void CopyLimbMask(List<Vector2Int> source, List<Vector2Int> target, bool mirror, Vector2Int frameSize)
        {
            target.Clear();
            foreach (var pos in source)
                target.Add(mirror ? FrameDataAlgorithms.MirrorPosition(pos, frameSize.x) : pos);
        }

        #endregion

        #region 辅助结构

        struct ExpandParams
        {
            public int headUp, headDown, headSide;
            public int bodyUp, bodyDown, bodySide, bodyUpStartStep, bodyDownStartStep;
        }

        #endregion
    }
}
