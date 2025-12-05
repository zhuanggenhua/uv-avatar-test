using System.Collections.Generic;
using System.Linq;
using UnityEngine;
 
namespace EquipmentSystem.Editor
{
    /// <summary>
    /// 帧数据持久化工具类
    /// 
    /// 功能：
    /// 1. UV区域填充 - 将UV映射到检测区域
    /// 2. 像素颜色获取 - 从纹理读取像素颜色
    /// 3. 帧数据保存 - 将编辑器数据保存到ScriptableObject
    /// 4. 批量检测 - 对所有帧执行自动检测
    /// 
    /// 这个类的主要作用是将编辑器状态和持久化数据之间进行转换
    /// </summary>
    public static class FrameDataPersistence
    {
        /// <summary>
        /// 将UV区域映射到检测区域
        /// 
        /// 映射规则：
        /// - 当检测区域 > UV区域时：
        ///   * 头部：靠右对齐，左边多出的部分用UV最左列填充
        ///   * 身体：居中对齐，边缘复制边界UV
        /// - 当检测区域 < UV区域时：
        ///   * 裁剪中间部分，保留边缘
        /// 
        /// 这个算法确保不同尺寸的装备都能正确映射UV
        /// </summary>
        /// <param name="startPos">检测区域起始位置</param>
        /// <param name="detectSize">检测区域尺寸</param>
        /// <param name="uvRegion">UV区域矩形（在画板上的位置）</param>
        /// <param name="part">部位类型</param>
        /// <param name="palW">画板宽度</param>
        /// <param name="palH">画板高度</param>
        /// <param name="frameSize">帧尺寸</param>
        /// <param name="partPixels">输出像素集合</param>
        /// <param name="partUVs">输出UV映射</param>
        public static void FillPartWithUV(
            Vector2Int startPos, 
            Vector2Int detectSize, 
            RectInt uvRegion, 
            CharacterBodyPart part, 
            int palW, 
            int palH,
            Vector2Int frameSize,
            HashSet<Vector2Int> partPixels,
            Dictionary<Vector2Int, Vector2> partUVs)
        {
            int detectW = detectSize.x, detectH = detectSize.y;
            int uvW = uvRegion.width, uvH = uvRegion.height;
            
            bool isHead = (part == CharacterBodyPart.Head);
            
            // 头部：靠右对齐，多出的全放左边
            // 身体：居中对齐，多出的左右分摊
            int extraLeftX = isHead 
                ? Mathf.Max(0, detectW - uvW)                    // 头部：全放左边
                : Mathf.Max(0, (detectW - uvW + 1) / 2);         // 身体：居中
            int extraTopY = Mathf.Max(0, (detectH - uvH + 1) / 2);
            
            // 身体：当UV高度 > 检测高度时，从UV的下部开始取
            int uvOffsetY = isHead ? 0 : Mathf.Max(0, (uvH - detectH + 1) / 2);
            
            for (int dy = 0; dy < detectH; dy++)
            {
                for (int dx = 0; dx < detectW; dx++)
                {
                    int px = startPos.x + dx, py = startPos.y + dy;
                    if (px >= frameSize.x || py >= frameSize.y) continue;
                    
                    var pos = new Vector2Int(px, py);
                    partPixels.Add(pos);
                    
                    // 计算对应的UV坐标
                    int uvDx, uvDy;
                    
                    if (isHead)
                    {
                        // 头部特殊处理
                        if (dx == 0)
                            uvDx = 0;  // 第一列
                        else if (dx <= 1 + extraLeftX)
                            uvDx = 1;  // 第二列（复制填充多出的空间）
                        else
                            uvDx = dx - extraLeftX;  // 剩余正常映射
                        
                        // Y方向：居中，多出的用边界行填充
                        if (dy < extraTopY)
                            uvDy = 0;
                        else if (dy >= extraTopY + uvH)
                            uvDy = uvH - 1;
                        else
                            uvDy = dy - extraTopY;
                    }
                    else
                    {
                        // 身体：居中对齐，边缘复制边界UV
                        if (dx < extraLeftX)
                            uvDx = 0;
                        else if (dx >= extraLeftX + uvW)
                            uvDx = uvW - 1;
                        else
                            uvDx = dx - extraLeftX;
                        
                        if (dy < extraTopY)
                            uvDy = uvOffsetY;
                        else if (dy >= extraTopY + uvH)
                            uvDy = uvH - 1;
                        else
                            uvDy = Mathf.Min(dy - extraTopY + uvOffsetY, uvH - 1);
                    }
                    
                    // UV是画板上的绝对坐标，和装备贴图布局一致
                    int uvX = uvRegion.x + uvDx;
                    int uvY = uvRegion.y + uvDy;
                    float u = (uvX + 0.5f) / palW;
                    float v = 1f - (uvY + 0.5f) / palH;
                    partUVs[pos] = new Vector2(u, v);
                }
            }
        }

        /// <summary>
        /// 从精灵表获取指定位置的像素颜色
        /// 
        /// 坐标转换：
        /// 1. 局部坐标(x,y) -> 全局坐标(gx,gy)
        /// 2. 考虑Unity纹理坐标系（左下角为原点）
        /// 3. 根据帧和行计算偏移
        /// </summary>
        /// <param name="sprite">精灵表纹理</param>
        /// <param name="frame">帧索引</param>
        /// <param name="row">行索引</param>
        /// <param name="frameSize">单帧尺寸</param>
        /// <param name="x">帧内X坐标</param>
        /// <param name="y">帧内Y坐标</param>
        /// <returns>像素颜色</returns>
        public static Color32 GetPixelAt(Texture2D sprite, int frame, int row, Vector2Int frameSize, int x, int y)
        {
            int gx = frame * frameSize.x + x;
            int gy = sprite.height - 1 - (row * frameSize.y + y);
            
            if (gx < 0 || gx >= sprite.width || gy < 0 || gy >= sprite.height)
                return default;
            
            var pixels = sprite.GetPixels32();
            return pixels[gy * sprite.width + gx];
        }

        /// <summary>
        /// 批量自动检测所有帧
        /// 
        /// 这个方法仅提供遍历框架，具体检测逻辑由回调函数实现
        /// </summary>
        /// <param name="data">帧数据</param>
        /// <param name="sprite">精灵表纹理</param>
        /// <param name="rowCount">总行数</param>
        /// <param name="framesPerRow">每行帧数</param>
        /// <param name="processFrame">处理每一帧的回调函数</param>
        /// <returns>处理的帧数</returns>
        public static int AutoDetectAllFrames(
            CharacterFrameData data,
            Texture2D sprite,
            int rowCount,
            int framesPerRow,
            System.Action<int, int> processFrame)
        {
            if (sprite == null || data == null)
                return 0;

            int detectedCount = 0;
            
            for (int r = 0; r < rowCount; r++)
            {
                for (int f = 0; f < framesPerRow; f++)
                {
                    processFrame(r, f);
                    detectedCount++;
                }
            }
            
            return detectedCount;
        }

        /// <summary>
        /// 保存UV部位到帧数据
        /// 
        /// 保存内容：
        /// 1. 像素位置和颜色
        /// 2. UV坐标映射
        /// 3. 贴图朝向和变体
        /// 4. 部位类型和方向
        /// 
        /// 特殊处理：
        /// - 即使没有像素，但设置了朝向或变体也会保存区域
        /// - 保证所有设置都能被持久化
        /// </summary>
        public static void SaveUVPartToFrame(
            FrameData frame,
            CharacterBodyPart part,
            HashSet<Vector2Int> partPixels,
            Dictionary<Vector2Int, Vector2> partUVs,
            Dictionary<CharacterBodyPart, CharacterFacing> partSpriteFacings,
            Dictionary<CharacterBodyPart, FrameVariant> partVariants,
            Color32[] pixels,
            int frame_x,
            int row_y,
            Vector2Int frameSize,
            Texture2D sprite)
        {
            // 获取像素集合
            if (partPixels == null || partPixels.Count == 0)
            {
                // 如果没有像素但设置了贴图方向或变体，也要保存
                bool hasFacing = partSpriteFacings.ContainsKey(part);
                bool hasVariant = partVariants.ContainsKey(part) && partVariants[part] != FrameVariant.Base;
                
                if (!hasFacing && !hasVariant)
                    return;
            }
            
            // 创建新区域
            var region = new BodyPartRegion
            {
                part = part,
                orientation = UVOrientation.UpRight,  // 使用默认值
                spriteFacing = partSpriteFacings.ContainsKey(part) ? 
                    partSpriteFacings[part] : GetDefaultSpriteFacing(row_y),
                variant = partVariants.ContainsKey(part) ? partVariants[part] : FrameVariant.Base
            };
            
            // 保存像素（包括颜色和UV）
            if (partPixels != null)
            {
                foreach (var pos in partPixels.OrderBy(p => p.y).ThenBy(p => p.x))
                {
                    var color = GetPixelColor(pixels, pos.x, pos.y, frame_x, row_y, frameSize, sprite);
                    var pixel = new BodyPartPixel
                    {
                        part = part,
                        position = pos,
                        color = color
                    };
                    
                    // 如果有UV映射，添加UV
                    if (partUVs != null && partUVs.TryGetValue(pos, out var uv))
                    {
                        pixel.uv = uv;
                    }
                    
                    region.pixels.Add(pixel);
                }
            }
            
            frame.bodyRegions.Add(region);
        }

        static CharacterFacing GetDefaultSpriteFacing(int row)
        {
            // 行索引与 CharacterFacing 枚举值一一对应
            if (row >= 0 && row <= 3)
                return (CharacterFacing)row;
            return CharacterFacing.SouthEast;
        }

        static Color32 GetPixelColor(Color32[] pixels, int x, int y, int frame, int row, Vector2Int frameSize, Texture2D sprite)
        {
            int gx = frame * frameSize.x + x;
            int gy = sprite.height - 1 - (row * frameSize.y + y);
            
            if (gx < 0 || gx >= sprite.width || gy < 0 || gy >= sprite.height)
                return default;
            
            return pixels[gy * sprite.width + gx];
        }
    }
}
