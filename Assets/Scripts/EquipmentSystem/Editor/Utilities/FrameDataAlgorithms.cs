using System.Collections.Generic;
using System.Linq;
using EquipmentSystem;
using UnityEngine;

namespace EquipmentSystem.Editor
{
    /// <summary>
    /// 帧数据编辑器的核心算法工具类
    /// 
    /// 功能：
    /// 1. 方向镜像算法 - 处理角色朝向和部位互换
    /// 2. 区域扩展/收缩算法 - 修改涂色范围
    /// 3. UV映射算法 - 处理换装系统UV坐标
    /// 4. 位置转换算法 - 处理像素坐标变换
    /// 
    /// 注意：所有算法都是纯函数，不依赖外部状态
    /// </summary>
    public static class FrameDataAlgorithms
    {
        #region 方向镜像算法
        
        /// <summary>
        /// 水平镜像时左右部位互换
        /// 用于从SE生成SW方向时的部位映射
        /// </summary>
        /// <param name="part">原始部位</param>
        /// <returns>镜像后的部位</returns>
        public static CharacterBodyPart MirrorBodyPart(CharacterBodyPart part)
        {
            switch (part)
            {
                case CharacterBodyPart.LeftHand: return CharacterBodyPart.RightHand;
                case CharacterBodyPart.RightHand: return CharacterBodyPart.LeftHand;
                case CharacterBodyPart.LeftFoot: return CharacterBodyPart.RightFoot;
                case CharacterBodyPart.RightFoot: return CharacterBodyPart.LeftFoot;
                default: return part;  // Head, Torso 不变
            }
        }
        
        /// <summary>
        /// 水平镜像时主副手锚点互换
        /// </summary>
        public static AnchorType MirrorAnchorType(AnchorType type)
        {
            switch (type)
            {
                case AnchorType.MainHandWeapon: return AnchorType.OffHandWeapon;
                case AnchorType.OffHandWeapon: return AnchorType.MainHandWeapon;
                default: return type;
            }
        }
        
        /// <summary>
        /// 水平镜像时贴图方向互换：SE↔SW, NE↔NW
        /// </summary>
        public static CharacterFacing MirrorSpriteFacing(CharacterFacing facing)
        {
            switch (facing)
            {
                case CharacterFacing.SouthEast: return CharacterFacing.SouthWest;
                case CharacterFacing.SouthWest: return CharacterFacing.SouthEast;
                case CharacterFacing.NorthEast: return CharacterFacing.NorthWest;
                case CharacterFacing.NorthWest: return CharacterFacing.NorthEast;
                default: return facing;
            }
        }
        
        /// <summary>
        /// South转为North（用于NE复制）：SE->NE, SW->NW
        /// </summary>
        public static CharacterFacing SouthToNorth(CharacterFacing facing)
        {
            switch (facing)
            {
                case CharacterFacing.SouthEast: return CharacterFacing.NorthEast;
                case CharacterFacing.SouthWest: return CharacterFacing.NorthWest;
                default: return facing;
            }
        }
        
        /// <summary>
        /// 水平镜像像素位置（翻转）
        /// </summary>
        public static Vector2Int MirrorPosition(Vector2Int pos, int frameWidth)
        {
            return new Vector2Int(frameWidth - 1 - pos.x, pos.y);
        }
        
        /// <summary>
        /// 计算区域镜像平移偏移量
        /// 将区域整体平移到中线对称位置（形状不变，只平移）
        /// </summary>
        /// <param name="positions">区域像素位置</param>
        /// <param name="frameWidth">帧宽度</param>
        /// <returns>X方向偏移量</returns>
        public static int CalculateMirrorTranslateOffset(IEnumerable<Vector2Int> positions, int frameWidth)
        {
            if (!positions.Any()) return 0;
            
            int minX = positions.Min(p => p.x);
            int maxX = positions.Max(p => p.x);
            float centerX = (minX + maxX) / 2f;
            // 像素索引 0 到 frameWidth-1，中线在 (frameWidth-1)/2
            float midLine = (frameWidth - 1) / 2f;
            
            // 偏移量 = 2 * (中线 - 区域中心)
            return Mathf.RoundToInt(2 * (midLine - centerX));
        }
        
        /// <summary>
        /// 平移位置（不翻转形状）
        /// </summary>
        public static Vector2Int TranslatePosition(Vector2Int pos, int offsetX)
        {
            return new Vector2Int(pos.x + offsetX, pos.y);
        }
        
        /// <summary>
        /// 判断是否为手脚/眼睛部位（不需要UV，直接上色）
        /// </summary>
        public static bool IsLimbPart(CharacterBodyPart part)
        {
            return part == CharacterBodyPart.LeftHand || 
                   part == CharacterBodyPart.RightHand ||
                   part == CharacterBodyPart.LeftFoot || 
                   part == CharacterBodyPart.RightFoot ||
                   part == CharacterBodyPart.LeftEye ||
                   part == CharacterBodyPart.RightEye;
        }
        
        #endregion
        
        #region 区域扩展/收缩算法

        public static void MapExpandByPose(RegionExpandPose pose, int logicalUp, int logicalDown, int logicalSide,
            out int up, out int down, out int left, out int right)
        {
            switch (pose)
            {
                case RegionExpandPose.HeadLeft:
                    // 头在左：身体坐标系逆时针旋转 90°
                    up = logicalSide;
                    down = logicalSide;
                    left = logicalUp;
                    right = logicalDown;
                    break;
                case RegionExpandPose.HeadRight:
                    // 头在右：身体坐标系顺时针旋转 90°
                    up = logicalSide;
                    down = logicalSide;
                    left = logicalDown;
                    right = logicalUp;
                    break;
                case RegionExpandPose.HeadDown:
                    // 头在下：身体坐标系旋转 180°（上下对调，左右不变）
                    up = logicalDown;
                    down = logicalUp;
                    left = logicalSide;
                    right = logicalSide;
                    break;
                default:
                    // HeadUp：不旋转
                    up = logicalUp;
                    down = logicalDown;
                    left = logicalSide;
                    right = logicalSide;
                    break;
            }
        }

        public static void ShrinkRegionByPoseAndDetectSize(
            HashSet<Vector2Int> regionPixels,
            Dictionary<Vector2Int, Vector2> pixelUVs,
            Vector2Int detectSize,
            RegionExpandPose pose,
            int logicalUp, int logicalDown, int logicalSide)
        {
            if (regionPixels == null || regionPixels.Count == 0)
                return;

            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;
            foreach (var p in regionPixels)
            {
                if (p.x < minX) minX = p.x;
                if (p.x > maxX) maxX = p.x;
                if (p.y < minY) minY = p.y;
                if (p.y > maxY) maxY = p.y;
            }

            int width = maxX - minX + 1;
            int height = maxY - minY + 1;

            int up, down, left, right;
            MapExpandByPose(pose, logicalUp, logicalDown, logicalSide, out up, out down, out left, out right);

            bool hasDetect = detectSize.x > 0 && detectSize.y > 0;
            int expectedWidth = detectSize.x + left + right;
            int expectedHeight = detectSize.y + up + down;

            if (hasDetect && width == expectedWidth && height == expectedHeight)
            {
                int coreMinX = minX + left;
                int coreMaxX = coreMinX + detectSize.x - 1;
                int coreMinY = minY + up;
                int coreMaxY = coreMinY + detectSize.y - 1;

                var toRemove = new List<Vector2Int>();
                foreach (var p in regionPixels)
                {
                    if (p.x < coreMinX || p.x > coreMaxX || p.y < coreMinY || p.y > coreMaxY)
                        toRemove.Add(p);
                }

                foreach (var p in toRemove)
                {
                    regionPixels.Remove(p);
                    pixelUVs?.Remove(p);
                }
            }
            else
            {
                ShrinkRegion(regionPixels, pixelUVs, up, down, left, right);
            }
        }

        /// <summary>
        /// 基于边界像素 UV 进行四边扩展（优化版：单次遍历获取边界）
        /// </summary>
        public static void ExpandRegionWithBoundaryUV(
            HashSet<Vector2Int> regionPixels, 
            Dictionary<Vector2Int, Vector2> pixelUVs,
            int expandUp, int expandDown, int expandLeft, int expandRight,
            Vector2Int frameSize, Vector2Int paletteSize,
            int upStartStep = 1, int downStartStep = 1,
            RegionExpandPose pose = RegionExpandPose.HeadUp)
        {
            if (regionPixels.Count == 0) return;
            
            float stepU = 1f / paletteSize.x;
            float stepV = 1f / paletteSize.y;
            
            // 优化：单次遍历获取边界（避免4次 LINQ Min/Max）
            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;
            foreach (var p in regionPixels)
            {
                if (p.x < minX) minX = p.x;
                if (p.x > maxX) maxX = p.x;
                if (p.y < minY) minY = p.y;
                if (p.y > maxY) maxY = p.y;
            }
            
            // 优化：预先缓存边界行/列的像素UV，避免重复查找
            // 上边界（y = minY）
            var topBoundary = new Dictionary<int, Vector2>();
            // 下边界（y = maxY）
            var bottomBoundary = new Dictionary<int, Vector2>();
            // 左边界（x = minX）
            var leftBoundary = new Dictionary<int, Vector2>();
            // 右边界（x = maxX）
            var rightBoundary = new Dictionary<int, Vector2>();
            
            foreach (var p in regionPixels)
            {
                if (pixelUVs.TryGetValue(p, out var uv))
                {
                    if (p.y == minY) topBoundary[p.x] = uv;
                    if (p.y == maxY) bottomBoundary[p.x] = uv;
                    if (p.x == minX) leftBoundary[p.y] = uv;
                    if (p.x == maxX) rightBoundary[p.y] = uv;
                }
            }
            
            // 向上扩展：支持自定义起始步长（upStartStep）
            // upStartStep 只影响 UV 采样的“起始行”，几何仍然从 minY-1 连续向上扩展
            int clampedUpStartStep = Mathf.Max(1, upStartStep);
            int upUvOffset = clampedUpStartStep - 1;
            for (int i = 1; i <= expandUp; i++)
            {
                int newY = minY - i;
                if (newY < 0) break;
                for (int x = minX; x <= maxX; x++)
                {
                    var newPos = new Vector2Int(x, newY);
                    if (!regionPixels.Contains(newPos) && topBoundary.TryGetValue(x, out var uv))
                    {
                        int uvStep = i + upUvOffset;
                        float u = uv.x;
                        float v = uv.y;
                        switch (pose)
                        {
                            case RegionExpandPose.HeadLeft:
                                // 头在左：屏幕向上使用原来的“向右”UV 步长
                                u = uv.x + uvStep * stepU;
                                v = uv.y;
                                break;
                            case RegionExpandPose.HeadRight:
                                // 头在右：屏幕向上使用原来的“向左”UV 步长
                                u = uv.x - uvStep * stepU;
                                v = uv.y;
                                break;
                            case RegionExpandPose.HeadDown:
                                // 头在下：屏幕向上对应纹理向下（v 减小）
                                u = uv.x;
                                v = uv.y - uvStep * stepV;
                                break;
                            default:
                                // 头在上：屏幕向上对应纹理向上（v 增大）
                                u = uv.x;
                                v = uv.y + uvStep * stepV;
                                break;
                        }
                        regionPixels.Add(newPos);
                        pixelUVs[newPos] = new Vector2(u, v);
                    }
                }
            }
            
            // 向下扩展
            int clampedDownStartStep = Mathf.Max(1, downStartStep);
            int downUvOffset = clampedDownStartStep - 1;
            for (int i = 1; i <= expandDown; i++)
            {
                int newY = maxY + i;
                if (newY >= frameSize.y) break;
                for (int x = minX; x <= maxX; x++)
                {
                    var newPos = new Vector2Int(x, newY);
                    if (!regionPixels.Contains(newPos) && bottomBoundary.TryGetValue(x, out var uv))
                    {
                        int uvStep = i + downUvOffset;
                        float u = uv.x;
                        float v = uv.y;
                        switch (pose)
                        {
                            case RegionExpandPose.HeadLeft:
                                // 头在左：屏幕向下使用原来的“向左”UV 步长
                                u = uv.x - uvStep * stepU;
                                v = uv.y;
                                break;
                            case RegionExpandPose.HeadRight:
                                // 头在右：屏幕向下使用原来的“向右”UV 步长
                                u = uv.x + uvStep * stepU;
                                v = uv.y;
                                break;
                            case RegionExpandPose.HeadDown:
                                // 头在下：屏幕向下对应纹理向上（v 增大）
                                u = uv.x;
                                v = uv.y + uvStep * stepV;
                                break;
                            default:
                                // 头在上：屏幕向下对应纹理向下（v 减小）
                                u = uv.x;
                                v = uv.y - uvStep * stepV;
                                break;
                        }
                        regionPixels.Add(newPos);
                        pixelUVs[newPos] = new Vector2(u, v);
                    }
                }
            }
            
            // 更新Y范围（用于左右扩展）——只与几何扩展高度有关，和 UV 偏移无关
            int expandedMinY = Mathf.Max(0, minY - expandUp);
            int expandedMaxY = Mathf.Min(frameSize.y - 1, maxY + expandDown);

            // 按行收集局部左右边界（包含新扩展的像素），而不是只用全局 minX/maxX
            var rowLeftX = new Dictionary<int, int>();
            var rowLeftUV = new Dictionary<int, Vector2>();
            var rowRightX = new Dictionary<int, int>();
            var rowRightUV = new Dictionary<int, Vector2>();

            foreach (var p in regionPixels)
            {
                if (p.y < expandedMinY || p.y > expandedMaxY)
                    continue;

                if (!pixelUVs.TryGetValue(p, out var uv))
                    continue;

                // 行内最左
                if (!rowLeftX.TryGetValue(p.y, out var lx) || p.x < lx)
                {
                    rowLeftX[p.y] = p.x;
                    rowLeftUV[p.y] = uv;
                }

                // 行内最右
                if (!rowRightX.TryGetValue(p.y, out var rx) || p.x > rx)
                {
                    rowRightX[p.y] = p.x;
                    rowRightUV[p.y] = uv;
                }
            }

            // 向左扩展（按每一行的局部左边界）
            for (int i = 1; i <= expandLeft; i++)
            {
                for (int y = expandedMinY; y <= expandedMaxY; y++)
                {
                    if (!rowLeftX.TryGetValue(y, out var boundaryX))
                        continue;

                    int newX = boundaryX - i;
                    if (newX < 0) continue;

                    var newPos = new Vector2Int(newX, y);
                    if (!regionPixels.Contains(newPos) && rowLeftUV.TryGetValue(y, out var uv))
                    {
                        float u = uv.x;
                        float v = uv.y;
                        switch (pose)
                        {
                            case RegionExpandPose.HeadLeft:
                                // 头在左：屏幕向左使用原来的“向上”UV 步长
                                u = uv.x;
                                v = uv.y + i * stepV;
                                break;
                            case RegionExpandPose.HeadRight:
                                // 头在右：屏幕向左使用原来的“向下”UV 步长
                                u = uv.x;
                                v = uv.y - i * stepV;
                                break;
                            case RegionExpandPose.HeadDown:
                                // 头在下：屏幕向左对应纹理向右（u 增大）
                                u = uv.x + i * stepU;
                                v = uv.y;
                                break;
                            default:
                                // 头在上：屏幕向左对应纹理向左（u 减小）
                                u = uv.x - i * stepU;
                                v = uv.y;
                                break;
                        }
                        regionPixels.Add(newPos);
                        pixelUVs[newPos] = new Vector2(u, v);
                    }
                }
            }

            // 向右扩展（按每一行的局部右边界）
            for (int i = 1; i <= expandRight; i++)
            {
                for (int y = expandedMinY; y <= expandedMaxY; y++)
                {
                    if (!rowRightX.TryGetValue(y, out var boundaryX))
                        continue;

                    int newX = boundaryX + i;
                    if (newX >= frameSize.x) continue;

                    var newPos = new Vector2Int(newX, y);
                    if (!regionPixels.Contains(newPos) && rowRightUV.TryGetValue(y, out var uv))
                    {
                        float u = uv.x;
                        float v = uv.y;
                        switch (pose)
                        {
                            case RegionExpandPose.HeadLeft:
                                // 头在左：屏幕向右使用原来的“向下”UV 步长
                                u = uv.x;
                                v = uv.y - i * stepV;
                                break;
                            case RegionExpandPose.HeadRight:
                                // 头在右：屏幕向右使用原来的“向上”UV 步长
                                u = uv.x;
                                v = uv.y + i * stepV;
                                break;
                            case RegionExpandPose.HeadDown:
                                // 头在下：屏幕向右对应纹理向左（u 减小）
                                u = uv.x - i * stepU;
                                v = uv.y;
                                break;
                            default:
                                // 头在上：屏幕向右对应纹理向右（u 增大）
                                u = uv.x + i * stepU;
                                v = uv.y;
                                break;
                        }
                        regionPixels.Add(newPos);
                        pixelUVs[newPos] = new Vector2(u, v);
                    }
                }
            }
        }
        
        /// <summary>
        /// 收缩区域：移除边界像素，但保护核心区域不被删除（优化版）
        /// </summary>
        public static void ShrinkRegion(
            HashSet<Vector2Int> regionPixels, 
            Dictionary<Vector2Int, Vector2> pixelUVs,
            int shrinkUp, int shrinkDown, int shrinkLeft, int shrinkRight)
        {
            if (regionPixels.Count == 0) return;
            
            // 优化：单次遍历获取边界
            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;
            foreach (var p in regionPixels)
            {
                if (p.x < minX) minX = p.x;
                if (p.x > maxX) maxX = p.x;
                if (p.y < minY) minY = p.y;
                if (p.y > maxY) maxY = p.y;
            }
            
            int currentWidth = maxX - minX + 1;
            int currentHeight = maxY - minY + 1;
            
            // 限制收缩量，确保不会把区域完全删除（至少保留 1x1）
            int totalShrinkX = shrinkLeft + shrinkRight;
            int totalShrinkY = shrinkUp + shrinkDown;
            
            if (totalShrinkX > 0 && currentWidth - totalShrinkX < 1)
            {
                float ratio = (float)(currentWidth - 1) / totalShrinkX;
                shrinkLeft = Mathf.FloorToInt(shrinkLeft * ratio);
                shrinkRight = Mathf.FloorToInt(shrinkRight * ratio);
            }
            
            if (totalShrinkY > 0 && currentHeight - totalShrinkY < 1)
            {
                float ratio = (float)(currentHeight - 1) / totalShrinkY;
                shrinkUp = Mathf.FloorToInt(shrinkUp * ratio);
                shrinkDown = Mathf.FloorToInt(shrinkDown * ratio);
            }
            
            // 优化：单次遍历收集要删除的像素
            var toRemove = new List<Vector2Int>();
            int topLimit = minY + shrinkUp;
            int bottomLimit = maxY - shrinkDown;
            int leftLimit = minX + shrinkLeft;
            int rightLimit = maxX - shrinkRight;
            
            foreach (var p in regionPixels)
            {
                if (p.y < topLimit || p.y > bottomLimit || p.x < leftLimit || p.x > rightLimit)
                    toRemove.Add(p);
            }
            
            foreach (var p in toRemove)
            {
                regionPixels.Remove(p);
                pixelUVs?.Remove(p);
            }
        }
        
        #endregion
        
        #region UV 填充算法
        
        /// <summary>
        /// 用UV区域填充检测区域，支持不同大小的映射
        /// 检测区域 > UV区域时，尽量居中，边缘像素复制边界UV
        /// </summary>
        /// <param name="startPos">检测区域起始位置</param>
        /// <param name="detectSize">检测区域大小</param>
        /// <param name="uvRegion">UV源区域（画板上）</param>
        /// <param name="frameSize">帧尺寸</param>
        /// <param name="paletteSize">调色板尺寸</param>
        /// <param name="outPixels">输出的像素集合</param>
        /// <param name="outUVs">输出的UV字典</param>
        public static void FillPartWithUV(
            Vector2Int startPos, Vector2Int detectSize, RectInt uvRegion,
            Vector2Int frameSize, Vector2Int paletteSize,
            HashSet<Vector2Int> outPixels, Dictionary<Vector2Int, Vector2> outUVs)
        {
            int detectW = detectSize.x, detectH = detectSize.y;
            int uvW = uvRegion.width, uvH = uvRegion.height;
            int palW = paletteSize.x, palH = paletteSize.y;
            
            // 尽量居中，无法居中时多出的放左边/上边（优先右边/下边对齐）
            int extraLeftX = Mathf.Max(0, (detectW - uvW + 1) / 2);
            int extraTopY = Mathf.Max(0, (detectH - uvH + 1) / 2);
            
            for (int dy = 0; dy < detectH; dy++)
            {
                for (int dx = 0; dx < detectW; dx++)
                {
                    int px = startPos.x + dx, py = startPos.y + dy;
                    if (px >= frameSize.x || py >= frameSize.y) continue;
                    
                    var pos = new Vector2Int(px, py);
                    outPixels.Add(pos);
                    
                    // 计算对应的UV坐标（居中对齐，边缘复制）
                    int uvDx;
                    if (dx < extraLeftX)
                        uvDx = 0;  // 左边界复制
                    else if (dx >= extraLeftX + uvW)
                        uvDx = uvW - 1;  // 右边界复制
                    else
                        uvDx = dx - extraLeftX;  // 正常映射
                    
                    int uvDy;
                    if (dy < extraTopY)
                        uvDy = 0;  // 上边界复制
                    else if (dy >= extraTopY + uvH)
                        uvDy = uvH - 1;  // 下边界复制
                    else
                        uvDy = dy - extraTopY;  // 正常映射
                    
                    // UV是画板上的绝对坐标
                    int uvX = uvRegion.x + uvDx;
                    int uvY = uvRegion.y + uvDy;
                    float u = (uvX + 0.5f) / palW;
                    float v = 1f - (uvY + 0.5f) / palH;
                    outUVs[pos] = new Vector2(u, v);
                }
            }
        }
        
        /// <summary>
        /// 镜像UV坐标 - 用于装备贴图的水平或垂直翻转
        /// </summary>
        /// <param name="pixels">像素集合</param>
        /// <param name="uvs">UV映射表（会被修改）</param>
        /// <param name="horizontal">是否水平镜像，false表示垂直镜像</param>
        public static void MirrorUV(HashSet<Vector2Int> pixels, Dictionary<Vector2Int, Vector2> uvs, bool horizontal)
        {
            if (pixels.Count == 0) return;
            
            // 找到像素区域的边界
            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;
            foreach (var p in pixels)
            {
                minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
            }
            
            // 交换UV
            var newUVs = new Dictionary<Vector2Int, Vector2>();
            foreach (var p in pixels)
            {
                Vector2Int mirrorPos;
                if (horizontal)
                    mirrorPos = new Vector2Int(maxX - (p.x - minX), p.y);
                else
                    mirrorPos = new Vector2Int(p.x, maxY - (p.y - minY));
                
                if (uvs.TryGetValue(mirrorPos, out var uv))
                    newUVs[p] = uv;
                else if (uvs.TryGetValue(p, out var selfUv))
                    newUVs[p] = selfUv;
            }
            
            // 更新原字典
            uvs.Clear();
            foreach (var kv in newUVs)
                uvs[kv.Key] = kv.Value;
        }
        
        /// <summary>
        /// 将 UV 在像素区域包围盒内旋转 90 度
        /// 顺时针或逆时针，仅重排 UV，对像素位置本身不做变动
        /// </summary>
        public static void RotateUV90(HashSet<Vector2Int> pixels, Dictionary<Vector2Int, Vector2> uvs, bool clockwise)
        {
            if (pixels.Count == 0) return;

            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;
            foreach (var p in pixels)
            {
                if (p.x < minX) minX = p.x;
                if (p.x > maxX) maxX = p.x;
                if (p.y < minY) minY = p.y;
                if (p.y > maxY) maxY = p.y;
            }

            int w = maxX - minX;
            int h = maxY - minY;

            var newUVs = new Dictionary<Vector2Int, Vector2>();
            foreach (var p in pixels)
            {
                int dx = p.x - minX;
                int dy = p.y - minY;

                int srcLocalX;
                int srcLocalY;

                if (clockwise)
                {
                    // 顺时针：dest(dx,dy) ← src(dy, h - dx)
                    srcLocalX = dy;
                    srcLocalY = h - dx;
                }
                else
                {
                    // 逆时针：dest(dx,dy) ← src(w - dy, dx)
                    srcLocalX = w - dy;
                    srcLocalY = dx;
                }

                var srcPos = new Vector2Int(minX + srcLocalX, minY + srcLocalY);

                if (uvs.TryGetValue(srcPos, out var uv))
                    newUVs[p] = uv;
                else if (uvs.TryGetValue(p, out var selfUv))
                    newUVs[p] = selfUv;
            }

            uvs.Clear();
            foreach (var kv in newUVs)
                uvs[kv.Key] = kv.Value;
        }
        
        #endregion
    }
}
