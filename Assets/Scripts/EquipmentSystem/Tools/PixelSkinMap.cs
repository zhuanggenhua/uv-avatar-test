using NaughtyAttributes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace EquipmentSystem.Tools
{
    /// <summary>
    /// 肤色调色板生成工具
    /// 用于从裸体角色贴图生成 KeyMap 和 PaletteMap，支持 EquipmentUV 换肤系统
    /// 使用方法：
    /// 1. 将此组件挂到任意 GameObject
    /// 2. 拖入裸体角色 spritesheet 到 origionTex
    /// 3. 点击 "Get Origion Color And Map" 生成贴图
    /// 4. 点击 "Generate Map File" 导出 PNG 文件
    /// 5. 将生成的 KeyMap 和 PaletteMap 拖到 CharacterAppearance 的对应字段
    /// </summary>
    public class PixelSkinMap : MonoBehaviour
    {
        public Texture2D origionTex;
        [ReadOnly] public List<Color> origionColors = new List<Color>();
        public List<Color> paletteColors = new List<Color>();
        [ReadOnly] public Texture2D keyMap;
        [ReadOnly] public Texture2D paletteMap;
        public bool needKeyMap = true;

        private Renderer render;
        private Dictionary<int, List<Vector2Int>> pixelIndexs = new Dictionary<int, List<Vector2Int>>();
        private bool haveTransparent = false;

        private void OnValidate()
        {
            CreatePaletteMap();
            SetTexture();
        }

        [Button("Get Origion Color And Map")]
        private void GetOrigionColorAndMap()
        {
            if (origionTex != null)
            {
                // 存所有颜色和对应的位置
                origionColors.Clear();
                paletteColors.Clear();
                pixelIndexs.Clear();
                pixelIndexs.Add(0, new List<Vector2Int>());
                origionColors.Add(Color.clear);
                paletteColors.Add(Color.clear);
                haveTransparent = false;
                for (int i = 0; i < origionTex.width; i++)
                {
                    for (int j = 0; j < origionTex.height; j++)
                    {
                        Color color = origionTex.GetPixel(i, j);
                        Vector2Int pixelIndex = new Vector2Int(i, j);
                        // 透明的不管什么颜色都加入到一个里面
                        if (Mathf.Approximately(color.a, 0))
                        {
                            haveTransparent = true;
                            pixelIndexs[0].Add(pixelIndex);
                            continue;
                        }
                        // 不透明的
                        int index = origionColors.IndexOf(color);
                        if (index >= 0)
                        {
                            pixelIndexs[index].Add(pixelIndex);
                        }
                        else
                        {
                            pixelIndexs.Add(origionColors.Count, new List<Vector2Int>() { pixelIndex });
                            origionColors.Add(color);
                            paletteColors.Add(color);
                        }
                    }
                }

                if (!haveTransparent)
                {
                    pixelIndexs.Remove(0);
                    origionColors.RemoveAt(0);
                    paletteColors.RemoveAt(0);
                }

                CreateKeyMap();
                CreatePaletteMap();
                SetTexture();
            }
        }

        [Button("Generate Map File")]
        private void GenerateMapFile()
        {
            string path = string.Empty;
            string directory = "Assets";
            try
            {
                directory = Path.GetDirectoryName(path);
            }
            catch (ArgumentException) { }
            string chosenSavePath = EditorUtility.SaveFolderPanel("Save image file", directory, string.Empty);
            if (!string.IsNullOrEmpty(chosenSavePath))
            {
                path = chosenSavePath;
                if (needKeyMap) 
                {
                    byte[] keyPNG = keyMap.EncodeToPNG();
                    File.WriteAllBytes(path + "/" + origionTex.name + " KeyMap.png", keyPNG);
                }     
                byte[] palettePNG = paletteMap.EncodeToPNG();
                File.WriteAllBytes(path + "/" + origionTex.name + " PaletteMap.png", palettePNG);
                AssetDatabase.Refresh();
                Debug.Log($"肤色调色板已保存到: {path}");
            }
        }

        private void CreateKeyMap() 
        {
            if (origionTex == null || pixelIndexs.Count == 0)
                return;

            if (keyMap == null) 
            {
                keyMap = new Texture2D(origionTex.width, origionTex.height);
                keyMap.alphaIsTransparency = true;
                keyMap.filterMode = FilterMode.Point;
            }

            float interval = (float)1 / pixelIndexs.Count;
            foreach (var pixelIndex in pixelIndexs)
            {
                int colorIndex = pixelIndex.Key;
                if (!haveTransparent)
                {
                    colorIndex -= 1;
                }
                float key = interval * colorIndex + interval * 0.5f;

                List<Vector2Int> indexs = pixelIndex.Value;
                for (int i = 0; i < indexs.Count; i++)
                {
                    keyMap.SetPixel(indexs[i].x, indexs[i].y, new Color(0, 0, 0, key));
                }
            }

            keyMap.Apply();
        }

        private void CreatePaletteMap() 
        {
            if (origionTex == null || pixelIndexs.Count == 0)
                return;

            if (paletteMap == null) 
            {
                paletteMap = new Texture2D(paletteColors.Count, 1);
                paletteMap.filterMode = FilterMode.Point;
            }

            foreach (var pixelIndex in pixelIndexs)
            {
                int colorIndex = pixelIndex.Key;
                if (!haveTransparent)
                {
                    colorIndex -= 1;
                }
                Color color = paletteColors[colorIndex];
                paletteMap.SetPixel(colorIndex, 0, color);
            }

            paletteMap.Apply();
        }

        private void SetTexture()
        {
            if (render == null)
                render = GetComponent<Renderer>();
            if (render != null && keyMap != null && paletteMap != null) 
            {
                // 只兼容 EquipmentUV 换装 Shader 的肤色调色板属性
                render.sharedMaterial.SetTexture("_SkinKeyTex", keyMap);
                render.sharedMaterial.SetTexture("_SkinPaletteTex", paletteMap);
                render.sharedMaterial.SetFloat("_SkinPaletteEnabled", 1f);
            }
        }
    }
}
