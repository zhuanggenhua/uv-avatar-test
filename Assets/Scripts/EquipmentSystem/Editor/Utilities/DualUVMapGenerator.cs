using System.Collections.Generic;
using System.IO;
using System.Linq;
using EquipmentSystem;
using UnityEditor;
using UnityEngine;

namespace EquipmentSystem.Editor
{
    /// <summary>
    /// 双层 UV Map 生成工具
    /// 
    /// 功能：
    /// 1. 从帧数据生成UV Map纹理，用于GPU换装
    /// 2. 分离头部和身体UV映射，支持独立换装
    /// 3. 编码部位、UV和朝向信息到纹理
    /// 
    /// 工作原理：
    /// - R通道：部位ID（区分不同身体部位）
    /// - G通道：U坐标（水平UV）
    /// - B通道：V坐标（垂直UV）
    /// - A通道：额外信息（朝向、变体等）
    /// 
    /// 使用场景：生成纹理后Shader可直接采样获取UV信息
    /// </summary>
    public static class DualUVMapGenerator
    {
        // ==================== 部位ID常量定义 ====================
        // 这些值必须与Shader中的定义保持一致
        const float ID_NONE = 0f;
        const float ID_HEAD = 0.1f;       // 头部 - 头盔/胡子/头发
        const float ID_TORSO = 0.2f;      // 躯干 - 服装
        const float ID_LEFTEYE = 0.3f;    // 左眼 - 颜色替换
        const float ID_RIGHTEYE = 0.35f;  // 右眼 - 颜色替换
        const float ID_LEFTHAND = 0.4f;   // 左手 - 手套
        const float ID_RIGHTHAND = 0.5f;  // 右手 - 手套
        const float ID_LEFTFOOT = 0.6f;   // 左脚 - 鞋子
        const float ID_RIGHTFOOT = 0.7f;  // 右脚 - 鞋子

        public static bool GenerateDualUVMapsForAnimation(CharacterFrameData data, AnimationData anim)
        {
            if (data == null || anim == null || anim.spritesheet == null)
            {
                Debug.LogWarning($"[UV Map] 动画 {anim?.GetKey() ?? "null"} 没有 spritesheet");
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
        /// 直接使用每个像素存储的 UV 坐标，不做任何计算
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
                WriteRegionToUVMap(frame, CharacterBodyPart.Torso, ID_TORSO, pixels, width, height, frameOffsetX, frameOffsetY, frameH);

                // 手脚眼睛 - 从 limbMask 读取（只用颜色替换，不需要UV映射）
                WriteLimbToUVMap(frame, CharacterBodyPart.LeftHand,  ID_LEFTHAND, pixels, width, height, frameOffsetX, frameOffsetY, frameH);
                WriteLimbToUVMap(frame, CharacterBodyPart.RightHand, ID_RIGHTHAND, pixels, width, height, frameOffsetX, frameOffsetY, frameH);
                WriteLimbToUVMap(frame, CharacterBodyPart.LeftFoot,  ID_LEFTFOOT, pixels, width, height, frameOffsetX, frameOffsetY, frameH);
                WriteLimbToUVMap(frame, CharacterBodyPart.RightFoot, ID_RIGHTFOOT, pixels, width, height, frameOffsetX, frameOffsetY, frameH);
                WriteLimbToUVMap(frame, CharacterBodyPart.LeftEye,   ID_LEFTEYE, pixels, width, height, frameOffsetX, frameOffsetY, frameH);
                WriteLimbToUVMap(frame, CharacterBodyPart.RightEye,  ID_RIGHTEYE, pixels, width, height, frameOffsetX, frameOffsetY, frameH);
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// 生成头部层 UV Map
        /// 直接使用每个像素存储的 UV 坐标，不做任何计算
        /// </summary>
        /// <param name="data">角色帧数据</param>
        /// <param name="anim">动画数据</param>
        /// <returns>头部层 UV Map 纹理</returns>
        static Texture2D GenerateHeadUVMapTexture(CharacterFrameData data, AnimationData anim)
        {
            if (anim.spritesheet == null) return null;

            int width = anim.spritesheet.width;
            int height = anim.spritesheet.height;

            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;

            // 非头部区域：ID_NONE，alpha=0（不视为核心头部）
            var defaultColor = new Color(0, 0, ID_NONE, 0);
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = defaultColor;

            int frameW = anim.frameSize.x;
            int frameH = anim.frameSize.y;

            foreach (var frame in anim.frames)
            {
                int frameOffsetX = frame.frameIndex * frameW;
                int frameOffsetY = (anim.rowCount - 1 - frame.rowIndex) * frameH;
                
                // 计算“核心头部区域”像素集合：直接使用 BodyPartPixel.isCore 标记
                HashSet<Vector2Int> coreHeadPixels = null;
                var headRegion = frame.GetRegion(CharacterBodyPart.Head);
                if (headRegion != null && headRegion.pixels.Count > 0)
                {
                    var corePositions = new HashSet<Vector2Int>();
                    foreach (var px in headRegion.pixels)
                    {
                        if (px.isCore)
                            corePositions.Add(px.position);
                    }

                    // 如果至少有一个像素被标记为核心，则使用该集合；
                    // 否则保持 null，表示所有头像素按默认 alpha=1 处理。
                    if (corePositions.Count > 0)
                        coreHeadPixels = corePositions;
                }

                WriteRegionToUVMap(
                    frame,
                    CharacterBodyPart.Head,
                    ID_HEAD,
                    pixels,
                    width,
                    height,
                    frameOffsetX,
                    frameOffsetY,
                    frameH,
                    null,
                    coreHeadPixels
                );
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// 将 UV 部位（头/身体）像素写入 UV Map
        /// </summary>
        /// <param name="excludePositions">需要排除的像素（例如手部像素，防止头覆盖手）</param>
        /// <param name="coreHeadPositions">
        ///  仅用于 Head：表示“核心头部区域”的像素集合。
        ///  核心区域会写入 alpha=1，扩展区域 alpha=0；其它情况 alpha=1。
        /// </param>
        static void WriteRegionToUVMap(
            EquipmentSystem.FrameData frame,
            CharacterBodyPart part,
            float partID,
            Color[] pixels,
            int texWidth,
            int texHeight,
            int frameOffsetX,
            int frameOffsetY,
            int frameH,
            HashSet<Vector2Int> excludePositions = null,
            HashSet<Vector2Int> coreHeadPositions = null)
        {
            var region = frame.GetRegion(part);
            if (region == null || region.pixels.Count == 0) return;

            int missingUVCount = 0;
            
            foreach (var px in region.pixels)
            {
                // 跳过排除的像素位置
                if (excludePositions != null && excludePositions.Contains(px.position))
                    continue;
                
                int globalX = frameOffsetX + px.position.x;
                int globalY = frameOffsetY + (frameH - 1 - px.position.y);
                
                if (globalX < 0 || globalX >= texWidth || globalY < 0 || globalY >= texHeight)
                    continue;

                if (px.HasUV)
                {
                    float alpha = 1f;
                    // 对 Head：若提供了核心区域集合，则用 alpha 区分核心/扩展
                    if (part == CharacterBodyPart.Head && coreHeadPositions != null)
                    {
                        alpha = coreHeadPositions.Contains(px.position) ? 1f : 0f;
                    }

                    pixels[globalY * texWidth + globalX] = new Color(px.uv.x, px.uv.y, partID, alpha);
                }
                else
                {
                    missingUVCount++;
                }
            }
            
            if (missingUVCount > 0)
            {
                Debug.LogWarning($"[UV Map] 帧({frame.frameIndex},{frame.rowIndex}) 部位 {part} 有 {missingUVCount} 个像素没有设置 UV");
            }
        }

        /// <summary>
        /// 将手脚蒙版像素写入 UV Map（只用颜色替换，不需要UV映射）
        /// </summary>
        static void WriteLimbToUVMap(EquipmentSystem.FrameData frame, CharacterBodyPart part, float partID,
                                     Color[] pixels, int texWidth, int texHeight,
                                     int frameOffsetX, int frameOffsetY, int frameH)
        {
            var limbPixels = frame.GetLimbPixels(part);
            if (limbPixels == null || limbPixels.Count == 0) return;

            foreach (var pos in limbPixels)
            {
                int globalX = frameOffsetX + pos.x;
                int globalY = frameOffsetY + (frameH - 1 - pos.y);
                
                if (globalX < 0 || globalX >= texWidth || globalY < 0 || globalY >= texHeight)
                    continue;
                int index = globalY * texWidth + globalX;
                var prev = pixels[index];
                // 保留原有的 UV（通常来自 Torso 区域），只更新部位 ID
                // 这样同一像素既可以作为手脚进行颜色替换，又在需要时沿用躯干 UV 采样衣服
                pixels[index] = new Color(prev.r, prev.g, partID, prev.a);
            }
        }
        
    }
}
