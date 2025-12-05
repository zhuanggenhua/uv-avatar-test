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
                    xEnd = Mathf.Min(p.frameSize.x, minX + colCount + 1);
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

                    if (matchedYs.Count == 1)
                    {
                        result.Add(new Vector2Int(x, matchedYs[0]));
                        break;
                    }
                    else if (matchedYs.Count > 1)
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

        /// <summary>
        /// 检测头部区域内的眼睛
        /// 
        /// 检测原理：
        /// 1. 在头部区域内扫描黑色/描边像素
        /// 2. 根据头部中心划分左右眼
        /// 3. SE朝向时：画面左边是角色右眼，画面右边是角色左眼
        /// </summary>
        public static void DetectEyes(DetectParams p, Vector2Int headDetectSize, 
            out HashSet<Vector2Int> leftEye, out HashSet<Vector2Int> rightEye)
        {
            leftEye = new HashSet<Vector2Int>();
            rightEye = new HashSet<Vector2Int>();

            float headCenterX = p.firstPixel.x + headDetectSize.x / 2.0f;

            for (int dy = 0; dy < headDetectSize.y; dy++)
            {
                for (int dx = 0; dx < headDetectSize.x; dx++)
                {
                    int px = p.firstPixel.x + dx;
                    int py = p.firstPixel.y + dy;

                    if (px < 0 || px >= p.frameSize.x || py < 0 || py >= p.frameSize.y)
                        continue;

                    var c = GetPixelFromParams(p, px, py);

                    if (p.cfg.IsOutline(c))
                    {
                        // SE朝向：画面左边（x < 中心）是角色右眼，画面右边（x >= 中心）是角色左眼
                        if (px < headCenterX)
                            rightEye.Add(new Vector2Int(px, py));
                        else
                            leftEye.Add(new Vector2Int(px, py));
                    }
                }
            }
        }

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
        public static void ExpandAllPartsForAllFrames(CharacterFrameData data)
        {
            if (data == null) return;

            Undo.RecordObject(data, "扩展所有帧");

            var paletteSize = data.paletteSize;
            var expandParams = new ExpandParams
            {
                headUp = data.headExpandUp,
                headDown = data.headExpandDown,
                headSide = data.headExpandSide,
                bodyUp = data.bodyExpandUp,
                bodyDown = data.bodyExpandDown,
                bodySide = data.bodyExpandSide,
                bodyUpStartStep = data.bodyExpandUpStartStep <= 0 ? 1 : data.bodyExpandUpStartStep
            };

            int expandedCount = 0;

            foreach (var anim in data.animations)
            {
                foreach (var frame in anim.frames)
                {
                    bool expanded = false;

                    // 扩展身体
                    var torsoRegion = frame.bodyRegions.FirstOrDefault(r => r.part == CharacterBodyPart.Torso);
                    if (torsoRegion != null)
                    {
                        var pixels = new HashSet<Vector2Int>(torsoRegion.pixels.Select(px => px.position));
                        var uvs = torsoRegion.pixels.Where(px => px.HasUV).ToDictionary(px => px.position, px => px.uv);
                        int before = pixels.Count;

                        var frameSize = anim.frameSize;
                        FrameDataAlgorithms.ExpandRegionWithBoundaryUV(pixels, uvs, 
                            expandParams.bodyUp, expandParams.bodyDown, expandParams.bodySide, expandParams.bodySide, 
                            frameSize, paletteSize, expandParams.bodyUpStartStep);

                        if (pixels.Count != before)
                        {
                            torsoRegion.pixels.Clear();
                            foreach (var pos in pixels)
                            {
                                var pixel = new BodyPartPixel { part = CharacterBodyPart.Torso, position = pos };
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
                        var pixels = new HashSet<Vector2Int>(headRegion.pixels.Select(px => px.position));
                        var uvs = headRegion.pixels.Where(px => px.HasUV).ToDictionary(px => px.position, px => px.uv);
                        int before = pixels.Count;

                        var frameSize = anim.frameSize;
                        FrameDataAlgorithms.ExpandRegionWithBoundaryUV(pixels, uvs,
                            expandParams.headUp, expandParams.headDown, expandParams.headSide, expandParams.headSide, 
                            frameSize, paletteSize);

                        if (pixels.Count > before)
                        {
                            headRegion.pixels.Clear();
                            foreach (var pos in pixels)
                            {
                                var pixel = new BodyPartPixel { part = CharacterBodyPart.Head, position = pos };
                                if (uvs.TryGetValue(pos, out var uv))
                                    pixel.uv = uv;
                                headRegion.pixels.Add(pixel);
                            }
                            expanded = true;
                        }
                    }

                    if (expanded) expandedCount++;
                }
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
        public static void ShrinkAllPartsForAllFrames(CharacterFrameData data)
        {
            if (data == null) return;

            Undo.RecordObject(data, "收缩所有帧");

            int shrunkCount = 0;

            foreach (var anim in data.animations)
            {
                foreach (var frame in anim.frames)
                {
                    bool shrunk = false;

                    foreach (var region in frame.bodyRegions)
                    {
                        if (region.part != CharacterBodyPart.Head && region.part != CharacterBodyPart.Torso)
                            continue;

                        var pixels = new HashSet<Vector2Int>(region.pixels.Select(px => px.position));
                        var uvs = region.pixels.Where(px => px.HasUV).ToDictionary(px => px.position, px => px.uv);
                        int before = pixels.Count;

                        // 固定收缩1像素
                        FrameDataAlgorithms.ShrinkRegion(pixels, uvs, 1, 1, 1, 1);

                        if (pixels.Count < before)
                        {
                            region.pixels.Clear();
                            foreach (var pos in pixels)
                            {
                                var pixel = new BodyPartPixel { part = region.part, position = pos };
                                if (uvs.TryGetValue(pos, out var uv))
                                    pixel.uv = uv;
                                region.pixels.Add(pixel);
                            }
                            shrunk = true;
                        }
                    }

                    if (shrunk) shrunkCount++;
                }
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
            => GenerateFrame(anim, f, seFrame, 1, true, false, true, true, frameSize);
        
        static void GenerateNEFrame(AnimationData anim, int f, FrameData seFrame, Vector2Int frameSize)
            => GenerateFrame(anim, f, seFrame, 2, false, true, false, false, frameSize);
        
        static void GenerateNWFrame(AnimationData anim, int f, FrameData seFrame, Vector2Int frameSize)
            => GenerateFrame(anim, f, seFrame, 3, true, true, true, true, frameSize);

        static void GenerateFrame(AnimationData anim, int frameIndex, FrameData sourceFrame, int targetRow,
            bool mirrorFacing, bool toNorth, bool translatePos, bool includeEyes, Vector2Int frameSize)
        {
            var targetFrame = anim.GetOrCreateFrame(frameIndex, targetRow);
            targetFrame.bodyRegions.Clear();
            targetFrame.anchors.Clear();
            
            // 生成 UV 部位区域（头/身体）
            foreach (var sourceRegion in sourceFrame.bodyRegions)
            {
                var targetFacing = sourceRegion.spriteFacing;
                if (mirrorFacing) targetFacing = FrameDataAlgorithms.MirrorSpriteFacing(targetFacing);
                if (toNorth) targetFacing = FrameDataAlgorithms.SouthToNorth(targetFacing);
                
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
                        uv = px.uv
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
                    CopyLimbMask(sourceFrame.limbMask.leftEye, targetFrame.limbMask.leftEye, translatePos, frameSize);
                    CopyLimbMask(sourceFrame.limbMask.rightEye, targetFrame.limbMask.rightEye, translatePos, frameSize);
                }
            }
            
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
            public int bodyUp, bodyDown, bodySide, bodyUpStartStep;
        }

        #endregion
    }
}
