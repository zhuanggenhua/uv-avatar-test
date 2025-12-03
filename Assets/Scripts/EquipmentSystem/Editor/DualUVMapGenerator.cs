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

            foreach (var frame in anim.frames)
            {
                int frameOffsetX = frame.frameIndex * frameW;
                int frameOffsetY = (anim.rowCount - 1 - frame.rowIndex) * frameH;

                // 收集手部像素位置（头部不应覆盖手）
                var handPixels = new HashSet<Vector2Int>();
                var leftHand = frame.GetLimbPixels(CharacterBodyPart.LeftHand);
                var rightHand = frame.GetLimbPixels(CharacterBodyPart.RightHand);
                if (leftHand != null)
                    foreach (var pos in leftHand) handPixels.Add(pos);
                if (rightHand != null)
                    foreach (var pos in rightHand) handPixels.Add(pos);

                WriteRegionToUVMap(frame, CharacterBodyPart.Head, ID_HEAD, pixels, width, height, frameOffsetX, frameOffsetY, frameH, handPixels);
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// 将 UV 部位（头/身体）像素写入 UV Map
        /// </summary>
        static void WriteRegionToUVMap(FrameData frame, CharacterBodyPart part, float partID,
                                       Color[] pixels, int texWidth, int texHeight,
                                       int frameOffsetX, int frameOffsetY, int frameH,
                                       HashSet<Vector2Int> excludePositions = null)
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
                    pixels[globalY * texWidth + globalX] = new Color(px.uv.x, px.uv.y, partID, 1f);
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
        static void WriteLimbToUVMap(FrameData frame, CharacterBodyPart part, float partID,
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

                pixels[globalY * texWidth + globalX] = new Color(0, 0, partID, 1f);
            }
        }
        
    }
}
