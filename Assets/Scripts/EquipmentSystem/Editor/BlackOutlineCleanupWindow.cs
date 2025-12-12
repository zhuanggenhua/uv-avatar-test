using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace EquipmentSystem.EditorTools
{
    public class BlackOutlineCleanupWindow : EditorWindow
    {
        private readonly List<Texture2D> _textures = new List<Texture2D>();
        private Vector2 _scroll;

        // 灰度阈值：小于该值视为“接近黑色”（0~1）
        [SerializeField]
        private float _grayThreshold = 0.15f;

        [MenuItem("Tools/Equipment System/Black Outline Cleanup")] 
        private static void ShowWindow()
        {
            var window = GetWindow<BlackOutlineCleanupWindow>("Black Outline Cleanup");
            window.minSize = new Vector2(420, 320);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("批量去除图片外圈黑边", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "用法：\n" +
                "1. 在 Project 里选中若干 Texture2D 或文件夹。\n" +
                "2. 点击“从当前选中收集纹理”。\n" +
                "3. 根据需要调整‘黑色灰度阈值’。\n" +
                "4. 点击“处理所有纹理”，工具会将外圈黑边像素改为透明。\n" +
                "注意：本工具会直接覆盖原 PNG 资源，请在使用前自行备份。",
                MessageType.Info);

            EditorGUILayout.Space();

            _grayThreshold = EditorGUILayout.Slider("黑色灰度阈值", _grayThreshold, 0.0f, 0.35f);
            EditorGUILayout.LabelField("说明：灰度越小，只认越纯的黑；建议 0.10~0.20 之间。", EditorStyles.miniLabel);

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("从当前选中收集纹理", GUILayout.Height(24)))
                {
                    CollectTexturesFromSelection();
                }

                if (GUILayout.Button("清空列表", GUILayout.Width(80), GUILayout.Height(24)))
                {
                    _textures.Clear();
                }
            }

            EditorGUILayout.Space();

            EditorGUILayout.LabelField($"待处理纹理数量：{_textures.Count}");

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(150));
            foreach (var tex in _textures)
            {
                if (tex == null) continue;
                EditorGUILayout.ObjectField(tex, typeof(Texture2D), false);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();

            EditorGUI.BeginDisabledGroup(_textures.Count == 0);
            if (GUILayout.Button("处理列表中的所有纹理", GUILayout.Height(32)))
            {
                ProcessAllTextures();
            }
            EditorGUI.EndDisabledGroup();
        }

        private void CollectTexturesFromSelection()
        {
            _textures.Clear();

            var selection = Selection.objects;
            if (selection == null || selection.Length == 0)
            {
                Debug.LogWarning("[BlackOutlineCleanup] 当前未选择任何资源。");
                return;
            }

            var addedPaths = new HashSet<string>();

            foreach (var obj in selection)
            {
                if (obj == null) continue;
                string path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path))
                    continue;

                if (AssetDatabase.IsValidFolder(path))
                {
                    string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { path });
                    foreach (string guid in guids)
                    {
                        string texPath = AssetDatabase.GUIDToAssetPath(guid);
                        if (!addedPaths.Add(texPath))
                            continue;
                        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                        if (tex != null && !_textures.Contains(tex))
                            _textures.Add(tex);
                    }
                }
                else
                {
                    var tex = obj as Texture2D;
                    if (tex != null)
                    {
                        string texPath = AssetDatabase.GetAssetPath(tex);
                        if (addedPaths.Add(texPath) && !_textures.Contains(tex))
                            _textures.Add(tex);
                    }
                }
            }

            Debug.Log($"[BlackOutlineCleanup] 已收集纹理数量：{_textures.Count}");
        }

        private void ProcessAllTextures()
        {
            if (_textures.Count == 0)
                return;

            try
            {
                for (int i = 0; i < _textures.Count; i++)
                {
                    var tex = _textures[i];
                    if (tex == null)
                        continue;

                    string path = AssetDatabase.GetAssetPath(tex);
                    EditorUtility.DisplayProgressBar("Black Outline Cleanup", path, (float)i / _textures.Count);

                    ProcessSingleTexture(tex, path);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();
            }

            Debug.Log("[BlackOutlineCleanup] 处理完成。");
        }

        private void ProcessSingleTexture(Texture2D tex, string assetPath)
        {
            if (tex == null || string.IsNullOrEmpty(assetPath))
                return;

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[BlackOutlineCleanup] 无法获取 TextureImporter: {assetPath}");
                return;
            }

            if (!importer.isReadable)
            {
                importer.isReadable = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();

                tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (tex == null)
                {
                    Debug.LogWarning($"[BlackOutlineCleanup] 重新导入后无法加载纹理: {assetPath}");
                    return;
                }
            }

            int width = tex.width;
            int height = tex.height;
            var pixels = tex.GetPixels32();
            if (pixels == null || pixels.Length != width * height)
            {
                Debug.LogWarning($"[BlackOutlineCleanup] 读取像素失败: {assetPath}");
                return;
            }

            bool changed = RemoveBlackOutlinePixels(pixels, width, height, _grayThreshold);
            if (!changed)
                return;

            // 将修改后的像素写回 PNG 资源
            var temp = new Texture2D(width, height, TextureFormat.RGBA32, false);
            temp.SetPixels32(pixels);
            temp.Apply();

            byte[] pngData = temp.EncodeToPNG();
            DestroyImmediate(temp);

            if (pngData == null || pngData.Length == 0)
            {
                Debug.LogWarning($"[BlackOutlineCleanup] EncodeToPNG 失败: {assetPath}");
                return;
            }

            File.WriteAllBytes(assetPath, pngData);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        /// <summary>
        /// 只执行一轮外圈黑边清理：仅移除当前贴图最外层的黑色描边
        /// </summary>
        private static bool RemoveBlackOutlinePixels(Color32[] pixels, int width, int height, float grayThreshold)
        {
            return RemoveOneLayerOfBlackOutline(pixels, width, height, grayThreshold);
        }

        /// <summary>
        /// 单轮处理：清除当前最外层的黑边像素
        /// </summary>
        private static bool RemoveOneLayerOfBlackOutline(Color32[] pixels, int width, int height, float grayThreshold)
        {
            bool changed = false;

            // 使用本轮开始时的快照做邻域判断，避免同轮内链式影响
            var src = (Color32[])pixels.Clone();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int idx = y * width + x;
                    Color32 c = src[idx];
                    if (!IsNearBlack(c, grayThreshold))
                        continue;

                    // 只处理"外圈黑边"：要求四邻域中至少有一个是透明或越界
                    bool isEdge = false;

                    // 左
                    if (x == 0)
                    {
                        isEdge = true;
                    }
                    else
                    {
                        if (!IsOpaque(src[(y * width) + (x - 1)]))
                            isEdge = true;
                    }

                    // 右
                    if (!isEdge)
                    {
                        if (x == width - 1)
                        {
                            isEdge = true;
                        }
                        else if (!IsOpaque(src[(y * width) + (x + 1)]))
                        {
                            isEdge = true;
                        }
                    }

                    // 下
                    if (!isEdge)
                    {
                        if (y == 0)
                        {
                            isEdge = true;
                        }
                        else if (!IsOpaque(src[((y - 1) * width) + x]))
                        {
                            isEdge = true;
                        }
                    }

                    // 上
                    if (!isEdge)
                    {
                        if (y == height - 1)
                        {
                            isEdge = true;
                        }
                        else if (!IsOpaque(src[((y + 1) * width) + x]))
                        {
                            isEdge = true;
                        }
                    }

                    if (!isEdge)
                        continue;

                    // 将外圈黑边像素变为完全透明
                    pixels[idx] = new Color32(0, 0, 0, 0);
                    changed = true;
                }
            }

            return changed;
        }

        private static bool IsNearBlack(Color32 c, float grayThreshold)
        {
            if (c.a == 0)
                return false;

            float gray = (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f;
            return gray < grayThreshold;
        }

        private static bool IsOpaque(Color32 c)
        {
            return c.a > 0; // 可以根据需要加一点 alpha 阈值
        }
    }
}
