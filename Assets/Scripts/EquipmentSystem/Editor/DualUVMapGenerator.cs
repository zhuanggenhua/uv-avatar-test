using System.Collections.Generic;
using System.IO;
using System.Linq;
using EquipmentSystem.Data;
using UnityEditor;
using UnityEngine;

namespace EquipmentSystem.Editor
{
    /// <summary>
    /// 双层 UV Map 生成工具：从 CharacterFrameData/AnimationData 生成 BodyUVMap 和 HeadUVMap
    /// </summary>
    public static class DualUVMapGenerator
    {
        // Body Part ID 常量 (对应 Shader 中的定义)
        const float ID_NONE = 0f;
        const float ID_HEAD = 0.1f;       // 头部 - 头盔/胡子/头发
        const float ID_TORSO = 0.2f;      // 躯干 - 服装
        const float ID_LEFTHAND = 0.4f;   // 左手 - 手套
        const float ID_RIGHTHAND = 0.5f;  // 右手 - 手套
        const float ID_LEFTFOOT = 0.6f;   // 左脚 - 鞋子
        const float ID_RIGHTFOOT = 0.7f;  // 右脚 - 鞋子

        public static bool GenerateDualUVMapsForAnimation(CharacterFrameData data, AnimationData anim)
        {
            if (data == null || anim == null || anim.spritesheet == null)
            {
                Debug.LogWarning($"[UV Map] 动画 {anim?.animationName ?? "null"} 没有 spritesheet");
                return false;
            }

            string spritesheetPath = AssetDatabase.GetAssetPath(anim.spritesheet);
            string directory = Path.GetDirectoryName(spritesheetPath);
            string baseName = Path.GetFileNameWithoutExtension(spritesheetPath);

            // 生成身体层 UV Map
            var bodyTex = GenerateBodyUVMapTexture(anim);
            if (bodyTex != null)
            {
                string bodyPath = Path.Combine(directory, baseName + "_BodyUV.png");
                SaveUVMapTexture(bodyTex, bodyPath);
                Object.DestroyImmediate(bodyTex);

                var loadedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(bodyPath);
                if (loadedTex != null)
                {
                    anim.bodyUVMap = loadedTex;
                    Debug.Log($"[UV Map] 身体层: {bodyPath}");
                }
            }

            // 生成头部层 UV Map
            var headTex = GenerateHeadUVMapTexture(data, anim);
            if (headTex != null)
            {
                string headPath = Path.Combine(directory, baseName + "_HeadUV.png");
                SaveUVMapTexture(headTex, headPath);
                Object.DestroyImmediate(headTex);

                var loadedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(headPath);
                if (loadedTex != null)
                {
                    anim.headUVMap = loadedTex;
                    Debug.Log($"[UV Map] 头部层: {headPath}");
                }
            }

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            return true;
        }

        public static int GenerateAllDualUVMaps(CharacterFrameData data)
        {
            if (data == null) return 0;

            int count = 0;
            foreach (var anim in data.animations)
            {
                if (GenerateDualUVMapsForAnimation(data, anim))
                    count++;
            }

            return count;
        }

        static void SaveUVMapTexture(Texture2D tex, string path)
        {
            byte[] pngData = tex.EncodeToPNG();
            File.WriteAllBytes(path, pngData);
            AssetDatabase.ImportAsset(path);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
        }

        /// <summary>
        /// 生成身体层 UV Map: 躯干 + 手 + 脚
        /// </summary>
        static Texture2D GenerateBodyUVMapTexture(AnimationData anim)
        {
            if (anim.spritesheet == null) return null;

            int width = anim.spritesheet.width;
            int height = anim.spritesheet.height;

            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;

            var defaultColor = new Color(0, 0, ID_NONE, 1);
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = defaultColor;

            int frameW = anim.frameSize.x;
            int frameH = anim.frameSize.y;

            foreach (var frame in anim.frames)
            {
                int frameOffsetX = frame.frameIndex * frameW;
                int frameOffsetY = (anim.rowCount - 1 - frame.rowIndex) * frameH;

                // 躯干
                ProcessRegionForUVMap(frame, CharacterBodyPart.Torso, ID_TORSO, pixels, width, height, frameOffsetX, frameOffsetY, frameH);

                // 手脚
                ProcessRegionForUVMap(frame, CharacterBodyPart.LeftHand,  ID_LEFTHAND, pixels, width, height, frameOffsetX, frameOffsetY, frameH);
                ProcessRegionForUVMap(frame, CharacterBodyPart.RightHand, ID_RIGHTHAND, pixels, width, height, frameOffsetX, frameOffsetY, frameH);
                ProcessRegionForUVMap(frame, CharacterBodyPart.LeftFoot,  ID_LEFTFOOT, pixels, width, height, frameOffsetX, frameOffsetY, frameH);
                ProcessRegionForUVMap(frame, CharacterBodyPart.RightFoot, ID_RIGHTFOOT, pixels, width, height, frameOffsetX, frameOffsetY, frameH);
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// 生成头部层 UV Map: 直接使用存储的头部像素（不再自动扩展）
        /// 扩展操作已移至编辑器中的"扩展头部区域"按钮
        /// 
        /// UV 计算基于参考帧：
        /// - 如果设置了参考帧，所有帧的 UV 都相对于参考帧头部中心计算
        /// - 这样无论头部在帧内如何移动，都能采样到头盔贴图的正确位置
        /// </summary>
        static Texture2D GenerateHeadUVMapTexture(CharacterFrameData data, AnimationData anim)
        {
            if (anim.spritesheet == null) return null;

            int width = anim.spritesheet.width;
            int height = anim.spritesheet.height;

            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;

            var defaultColor = new Color(0, 0, ID_NONE, 1);
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = defaultColor;

            int frameW = anim.frameSize.x;
            int frameH = anim.frameSize.y;
            
            // 获取参考帧中心（如果已设置）
            Vector2? refCenter = data.hasReferenceFrame ? (Vector2?)data.referenceHeadCenter : null;

            foreach (var frame in anim.frames)
            {
                int frameOffsetX = frame.frameIndex * frameW;
                int frameOffsetY = (anim.rowCount - 1 - frame.rowIndex) * frameH;

                // 使用参考帧中心生成头部 UV
                ProcessHeadRegionForUVMap(frame, ID_HEAD, pixels, width, height, frameOffsetX, frameOffsetY, frameH, refCenter);
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// 将部位像素写入 UV Map 纹理
        /// 
        /// 映射规则：假设把涂色区域移到帧中心，计算每个像素"移动后"的位置作为 UV
        /// - 涂色区域中心像素 → UV (0.5, 0.5) → 采样装备贴图中心
        /// - 涂色区域左边 n 像素 → UV (0.5 - n/frameH, 0.5) → 采样装备贴图偏左
        /// - 涂色区域上边 m 像素 → UV (0.5, 0.5 + m/frameH) → 采样装备贴图偏上
        /// 
        /// 这意味着：
        /// - 无论涂色区域在帧内的哪个位置，都能正确采样装备贴图
        /// - 装备贴图的设计可以假设部位在贴图中心
        /// - 1:1 像素对应，不会拉伸/压缩
        /// </summary>
        /// <param name="positions">部位像素在帧内的坐标列表</param>
        /// <param name="partID">部位 ID（写入 B 通道）</param>
        /// <param name="pixels">UV Map 像素数组</param>
        /// <param name="texWidth">UV Map 纹理宽度</param>
        /// <param name="texHeight">UV Map 纹理高度</param>
        /// <param name="frameOffsetX">当前帧在纹理中的 X 偏移</param>
        /// <param name="frameOffsetY">当前帧在纹理中的 Y 偏移</param>
        /// <param name="frameHeight">帧高度</param>
        /// <param name="orientation">UV 方向，用于旋转 UV 坐标（90°/180°/270°）</param>
        static void WritePixelsToUVMap(IEnumerable<Vector2Int> positions, float partID,
                                       Color[] pixels, int texWidth, int texHeight,
                                       int frameOffsetX, int frameOffsetY, int frameHeight,
                                       UVOrientation orientation = UVOrientation.UpRight)
        {
            var positionList = positions.ToList();
            if (positionList.Count == 0) return;
            
            // ========== 第一步：计算涂色区域中心 和 帧中心 ==========
            int boundMinX = positionList.Min(p => p.x);
            int boundMaxX = positionList.Max(p => p.x);
            int boundMinY = positionList.Min(p => p.y);
            int boundMaxY = positionList.Max(p => p.y);
            
            // 涂色区域的中心坐标
            float regionCenterX = (boundMinX + boundMaxX) / 2f;
            float regionCenterY = (boundMinY + boundMaxY) / 2f;
            
            // 帧中心（也是装备贴图的中心）
            float frameCenterX = frameHeight / 2f;  // 假设帧是正方形
            float frameCenterY = frameHeight / 2f;
            
            // 把涂色区域移到帧中心需要的偏移量
            float moveToFrameCenterX = frameCenterX - regionCenterX;
            float moveToFrameCenterY = frameCenterY - regionCenterY;

            // ========== 第二步：为每个像素计算 UV 坐标 ==========
            // 核心思想：假设把涂色区域移到帧中心，计算每个像素"移动后"的位置
            // 然后用这个位置相对于帧中心的坐标作为 UV
            foreach (var pixelPos in positionList)
            {
                // 像素"移动后"的位置（在帧中心附近）
                float movedX = pixelPos.x + moveToFrameCenterX;
                float movedY = pixelPos.y + moveToFrameCenterY;
                
                // 转换为 UV 坐标（相对于帧/装备贴图）
                // 帧内坐标: Y=0 在顶部，Y 增大向下
                // UV 坐标: V=0 在底部，V=1 在顶部
                // 所以需要翻转: v = 1 - y/h
                float u0 = movedX / frameHeight;
                float v0 = 1f - movedY / frameHeight;
                
                // 根据 UV 方向在 UV 空间做旋转（绕 0.5, 0.5 中心点）
                float u = u0, v = v0;
                switch (orientation)
                {
                    case UVOrientation.DownLeft:
                        // 旋转 180°
                        u = 1f - u0;
                        v = 1f - v0;
                        break;
                    case UVOrientation.UpLeft:
                        // 逆时针 90°
                        u = v0;
                        v = u0;
                        break;
                    case UVOrientation.DownRight:
                        // 顺时针 90°
                        u = 1f - v0;
                        v = 1f - u0;
                        break;
                    // UpRight: 默认，不旋转
                }

                // ========== 第三步：写入 UV Map 纹理 ==========
                int globalX = frameOffsetX + pixelPos.x;
                int globalY = frameOffsetY + (frameHeight - 1 - pixelPos.y);
                
                if (globalX >= 0 && globalX < texWidth && globalY >= 0 && globalY < texHeight)
                {
                    // pixels 是一维数组，存储整个纹理的像素
                    // 二维坐标 (x, y) 转一维索引: index = y * 宽度 + x
                    pixels[globalY * texWidth + globalX] = new Color(u, v, partID, 1f);
                }
            }
        }

        // 便捷重载：处理 FrameData 中的区域（用于身体部位）
        static void ProcessRegionForUVMap(FrameData frame, CharacterBodyPart part, float partID,
                                          Color[] pixels, int texWidth, int texHeight,
                                          int frameOffsetX, int frameOffsetY, int frameH)
        {
            var region = frame.GetRegion(part);
            if (region == null || region.pixels.Count == 0) return;

            var orientation = region.orientation;
            WritePixelsToUVMap(region.pixels.Select(px => px.position), partID, pixels, texWidth, texHeight,
                               frameOffsetX, frameOffsetY, frameH,
                               orientation);
        }
        
        /// <summary>
        /// 处理头部区域的 UV Map 生成（支持参考帧）
        /// 
        /// 如果设置了参考帧中心 (refCenter)：
        /// - 计算当前帧头部中心
        /// - 将像素坐标平移到参考帧坐标系：p_ref = p + (refCenter - curCenter)
        /// - 用平移后的坐标计算 UV（帧内绝对坐标方式）
        /// 
        /// 效果：所有帧的头部都映射到同一块头盔贴图区域
        /// </summary>
        static void ProcessHeadRegionForUVMap(FrameData frame, float partID,
                                              Color[] pixels, int texWidth, int texHeight,
                                              int frameOffsetX, int frameOffsetY, int frameH,
                                              Vector2? refCenter)
        {
            var region = frame.GetRegion(CharacterBodyPart.Head);
            if (region == null || region.pixels.Count == 0) return;
            
            var positions = region.pixels.Select(px => px.position).ToList();
            var orientation = region.orientation;
            
            if (refCenter.HasValue)
            {
                // ========== 使用参考帧坐标系 ==========
                // 计算当前帧头部中心（使用整数，避免小数导致 UV 采样重复）
                int minX = positions.Min(p => p.x);
                int maxX = positions.Max(p => p.x);
                int minY = positions.Min(p => p.y);
                int maxY = positions.Max(p => p.y);
                Vector2 curCenter = new Vector2((minX + maxX) / 2, (minY + maxY) / 2);
                
                // 平移偏移量：将当前帧头部中心移到参考帧头部中心
                Vector2 delta = refCenter.Value - curCenter;
                
                foreach (var pixelPos in positions)
                {
                    // 将像素平移到参考帧坐标系
                    float refX = pixelPos.x + delta.x;
                    float refY = pixelPos.y + delta.y;
                    
                    // 用参考帧坐标计算 UV（帧内绝对坐标方式）
                    float u0 = refX / frameH;
                    float v0 = 1f - refY / frameH;
                    
                    // 应用旋转
                    float u = u0, v = v0;
                    switch (orientation)
                    {
                        case UVOrientation.DownLeft:
                            u = 1f - u0;
                            v = 1f - v0;
                            break;
                        case UVOrientation.UpLeft:
                            u = v0;
                            v = u0;
                            break;
                        case UVOrientation.DownRight:
                            u = 1f - v0;
                            v = 1f - u0;
                            break;
                    }
                    
                    // 写入 UV Map
                    int globalX = frameOffsetX + pixelPos.x;
                    int globalY = frameOffsetY + (frameH - 1 - pixelPos.y);
                    if (globalX >= 0 && globalX < texWidth && globalY >= 0 && globalY < texHeight)
                    {
                        pixels[globalY * texWidth + globalX] = new Color(u, v, partID, 1f);
                    }
                }
            }
            else
            {
                // ========== 没有参考帧，使用原来的居中算法 ==========
                WritePixelsToUVMap(positions, partID, pixels, texWidth, texHeight,
                                   frameOffsetX, frameOffsetY, frameH, orientation);
            }
        }
    }
}
