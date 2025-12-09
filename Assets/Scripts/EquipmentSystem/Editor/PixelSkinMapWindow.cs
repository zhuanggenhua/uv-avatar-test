using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using EquipmentSystem;

//参考：https://www.bilibili.com/video/BV17P4y1o7D5
//参考：https://www.youtube.com/watch?v=u4Iz5AJa31Q  三种换色方式
namespace EquipmentSystem.Editor
{
    /// <summary>
    /// 肤色映射生成工具窗口
    /// 从一张基准贴图（Base）和一张目标贴图（Target）中分析皮肤颜色，
    /// 自动生成源/目标肤色颜色表（skinSrcColors/skinDstColors），用于 EquipmentUV 运行时换肤。
    /// </summary>
    public class PixelSkinMapWindow : EditorWindow
    {
        // 单个肤色映射累积数据
        private class SkinColorAccum
        {
            public Color srcColorSum;
            public int srcCount;
            public Color dstColorSum;
            public int dstCount;

            public Color MeanSrcColor => srcCount > 0 ? srcColorSum / srcCount : Color.clear;
            public Color MeanDstColor => dstCount > 0 ? dstColorSum / dstCount : MeanSrcColor;
        }

        // 输入
        private Sprite baseSprite;               // 源皮肤贴图
        private Sprite targetSprite;             // 目标皮肤贴图
        private CharacterAppearance targetAppearance; // 要写入颜色表的外观数据

        // 生成的中间数据（基于 Base Sprite）
        private List<Color> baseColors = new List<Color>(); // 仅用于在面板中预览源肤色

        // UI 滚动位置
        private Vector2 scrollPosition;
        private bool showBaseColors = false;

        [MenuItem("Tools/Equipment System/Pixel Skin Map")]
        public static void OpenWindow()
        {
            var window = GetWindow<PixelSkinMapWindow>("Pixel Skin Map");
            window.minSize = new Vector2(400, 500);
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            EditorGUILayout.LabelField("肤色映射生成工具（颜色表）", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "1. 拖入 Base Sprite（源皮肤贴图）\n" +
                "2. 拖入 Target Sprite（目标皮肤贴图，布局需与 Base 相同）\n" +
                "3. 选择要写入的 CharacterAppearance 资源\n" +
                "4. 点击 \"Analyze & Apply\" 自动生成肤色颜色映射表（src/dst 颜色表）",
                MessageType.Info);

            EditorGUILayout.Space(10);

            // === 输入区 ===
            EditorGUILayout.LabelField("输入", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            baseSprite = (Sprite)EditorGUILayout.ObjectField("Base Sprite", baseSprite, typeof(Sprite), false);
            if (EditorGUI.EndChangeCheck())
            {
                // 切换 Base 贴图时清空旧数据
                ClearGeneratedData();
            }

            targetSprite = (Sprite)EditorGUILayout.ObjectField(
                new GUIContent("Target Sprite", "目标皮肤贴图（布局需与 Base 相同）"),
                targetSprite,
                typeof(Sprite),
                false);

            targetAppearance = (CharacterAppearance)EditorGUILayout.ObjectField(
                new GUIContent("Character Appearance", "生成的肤色映射将写入此外观资源的 skinSrcColors/skinDstColors 数组"),
                targetAppearance,
                typeof(CharacterAppearance),
                false);

            EditorGUILayout.Space(10);

            // === 按钮：分析并写入外观数据 ===
            using (new EditorGUI.DisabledScope(baseSprite == null))
            {
                if (GUILayout.Button("Analyze & Apply", GUILayout.Height(28)))
                {
                    AnalyzeBaseSprite();
                }
            }

            EditorGUILayout.Space(10);

            // === Base Colors（只读展示）===
            if (baseColors.Count > 0)
            {
                showBaseColors = EditorGUILayout.Foldout(showBaseColors, $"Base Colors ({baseColors.Count})", true);
                if (showBaseColors)
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUI.indentLevel++;
                        for (int i = 0; i < baseColors.Count; i++)
                        {
                            EditorGUILayout.ColorField($"[{i}]", baseColors[i]);
                        }
                        EditorGUI.indentLevel--;
                    }
                }
            }

            EditorGUILayout.Space(10);

            // === 清空按钮 ===
            if (baseColors.Count > 0)
            {
                if (GUILayout.Button("Clear All", GUILayout.Height(22)))
                {
                    ClearGeneratedData();
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void ClearGeneratedData()
        {
            baseColors.Clear();
            Repaint();
        }

        /// <summary>
        /// 分析 Base/Target 贴图中的肤色，并生成源/目标颜色表
        /// </summary>
        private void AnalyzeBaseSprite()
        {
            if (baseSprite == null)
                return;

            ClearGeneratedData();

            Texture2D baseTex = baseSprite.texture;
            Rect baseRect = baseSprite.rect;

            Texture2D targetTex = null;
            Rect targetRect = new Rect();

            // 记录并临时修改 Read/Write 设置
            string basePath = AssetDatabase.GetAssetPath(baseTex);
            TextureImporter baseImporter = null;
            bool baseWasReadable = true;

            string targetPath = null;
            TextureImporter targetImporter = null;
            bool targetWasReadable = true;

            if (!string.IsNullOrEmpty(basePath))
            {
                baseImporter = AssetImporter.GetAtPath(basePath) as TextureImporter;
                if (baseImporter != null)
                {
                    baseWasReadable = baseImporter.isReadable;
                    if (!baseWasReadable)
                    {
                        baseImporter.isReadable = true;
                        baseImporter.SaveAndReimport();
                    }
                }
            }

            if (targetSprite != null)
            {
                targetTex = targetSprite.texture;
                targetRect = targetSprite.rect;

                targetPath = AssetDatabase.GetAssetPath(targetTex);
                if (!string.IsNullOrEmpty(targetPath))
                {
                    targetImporter = AssetImporter.GetAtPath(targetPath) as TextureImporter;
                    if (targetImporter != null)
                    {
                        targetWasReadable = targetImporter.isReadable;
                        if (!targetWasReadable)
                        {
                            targetImporter.isReadable = true;
                            targetImporter.SaveAndReimport();
                        }
                    }
                }
            }

            try
            {
                // 重新获取贴图（防止 Reimport 后对象改变）
                baseTex = baseSprite.texture;
                if (!baseTex.isReadable)
                {
                    EditorUtility.DisplayDialog("Error",
                        $"贴图 '{baseTex.name}' 不可读，且无法自动开启。\n请手动在 Import Settings 中勾选 'Read/Write Enabled'。",
                        "OK");
                    return;
                }

                if (targetSprite != null)
                {
                    targetTex = targetSprite.texture;
                    if (!targetTex.isReadable)
                    {
                        EditorUtility.DisplayDialog("Warning",
                            $"目标贴图 '{targetTex.name}' 不可读，将忽略 Target Sprite，仅使用 Base 颜色生成源颜色表。",
                            "OK");
                        targetTex = null;
                    }
                }

                int startX = Mathf.FloorToInt(baseRect.x);
                int startY = Mathf.FloorToInt(baseRect.y);
                int width = Mathf.FloorToInt(baseRect.width);
                int height = Mathf.FloorToInt(baseRect.height);

                int targetStartX = 0;
                int targetStartY = 0;
                if (targetTex != null)
                {
                    targetRect = targetSprite.rect;
                    targetStartX = Mathf.FloorToInt(targetRect.x);
                    targetStartY = Mathf.FloorToInt(targetRect.y);

                    int tWidth = Mathf.FloorToInt(targetRect.width);
                    int tHeight = Mathf.FloorToInt(targetRect.height);
                    if (tWidth != width || tHeight != height)
                    {
                        bool cont = EditorUtility.DisplayDialog(
                            "尺寸不一致",
                            $"Base Sprite 和 Target Sprite 的可见区域尺寸不同（{width}x{height} vs {tWidth}x{tHeight}）。\n" +
                            "如果继续，将忽略 Target，仅生成源肤色列表。",
                            "继续（忽略 Target）",
                            "取消");

                        if (!cont)
                            return;

                        targetTex = null;
                    }
                }

                // === 第一阶段：按 Base 颜色聚类，累积对应 Target 颜色 ===
                var map = new Dictionary<Color32, SkinColorAccum>();

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        Color baseColor = baseTex.GetPixel(startX + x, startY + y);
                        if (baseColor.a <= 0f)
                            continue;

                        // 过滤接近黑色的描边等非肤色
                        if (IsNearBlack(baseColor))
                            continue;

                        Color32 key = (Color32)baseColor;

                        SkinColorAccum accum;
                        if (!map.TryGetValue(key, out accum))
                        {
                            accum = new SkinColorAccum();
                            map[key] = accum;
                        }

                        accum.srcColorSum += baseColor;
                        accum.srcCount++;

                        // 对于 Target：只记录第一个遇到的、非透明且非近黑的颜色，
                        // 避免在不同部位之间做平均，保证是贴图中真实存在的代表色。
                        if (targetTex != null && accum.dstCount == 0)
                        {
                            Color targetColor = targetTex.GetPixel(targetStartX + x, targetStartY + y);
                            if (targetColor.a > 0f && !IsNearBlack(targetColor))
                            {
                                accum.dstColorSum = targetColor;
                                accum.dstCount = 1;
                            }
                        }
                    }
                }

                if (map.Count == 0)
                {
                    EditorUtility.DisplayDialog("提示", "未找到可用的非黑色肤色像素，无法生成映射。", "OK");
                    return;
                }

                // === 第二阶段：将聚类结果转为有序列表（按亮度排序，便于查看）===
                var srcList = new List<Color>(map.Count);
                var dstList = new List<Color>(map.Count);

                // 为了排序方便，先构建一个临时列表
                var tempList = new List<(Color32 key, SkinColorAccum accum)>(map.Count);
                foreach (var kv in map)
                {
                    tempList.Add((kv.Key, kv.Value));
                }

                tempList.Sort((a, b) =>
                {
                    float ga = CalcGray(a.accum.MeanSrcColor);
                    float gb = CalcGray(b.accum.MeanSrcColor);
                    return ga.CompareTo(gb);
                });

                foreach (var item in tempList)
                {
                    // 源颜色直接使用聚类 key（Color32 -> Color），确保与贴图中实际像素颜色完全一致
                    Color src = (Color)item.key;
                    Color dst;

                    if (targetTex != null && item.accum.dstCount > 0)
                        dst = item.accum.MeanDstColor;  // 这里只有一次赋值，相当于记录首个目标色
                    else
                        dst = src;

                    srcList.Add(src);
                    dstList.Add(dst);
                }

                // 更新用于调试查看的 Base 颜色列表
                baseColors.Clear();
                baseColors.AddRange(srcList);

                // 如果指定了外观数据，则写入其 skinSrcColors/skinDstColors
                if (targetAppearance != null)
                {
                    targetAppearance.skinSrcColors = srcList.ToArray();
                    targetAppearance.skinDstColors = dstList.ToArray();

                    EditorUtility.SetDirty(targetAppearance);
                    AssetDatabase.SaveAssets();
                }
            }
            finally
            {
                // 恢复 Read/Write 设置
                if (baseImporter != null && !baseWasReadable)
                {
                    baseImporter.isReadable = false;
                    baseImporter.SaveAndReimport();
                }

                if (targetImporter != null && !targetWasReadable)
                {
                    targetImporter.isReadable = false;
                    targetImporter.SaveAndReimport();
                }
            }

            Repaint();
        }

        private static float CalcGray(Color c)
        {
            return 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
        }

        private static bool IsNearBlack(Color c)
        {
            // 使用亮度判断接近黑色的描边/线稿，避免被当作肤色
            float gray = CalcGray(c);
            return gray < 0.15f;
        }

        private void OnDestroy()
        {
            // 窗口关闭时清理临时数据
        }
    }
}
