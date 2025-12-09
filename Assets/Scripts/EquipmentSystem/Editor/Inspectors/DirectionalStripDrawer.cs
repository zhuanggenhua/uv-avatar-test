using UnityEngine;
using UnityEditor;
using EquipmentSystem;

namespace EquipmentSystem.Editor
{
    /// <summary>
    /// DirectionalStrip 的自定义绘制：
    /// - 显示方向、帧数
    /// - 同时编辑 frames 与 depthModes
    /// - 在编辑帧列表时自动同步 depthModes 长度（缺省填 Front，多余截断）
    /// </summary>
    [CustomPropertyDrawer(typeof(DirectionalStrip))]
    public class DirectionalStripDrawer : PropertyDrawer
    {
        static readonly System.Collections.Generic.Dictionary<string, System.Collections.Generic.HashSet<int>> _selectedDepthIndices =
            new System.Collections.Generic.Dictionary<string, System.Collections.Generic.HashSet<int>>();
        static readonly System.Collections.Generic.Dictionary<string, int> _lastClickedDepthIndex =
            new System.Collections.Generic.Dictionary<string, int>();
        static readonly System.Collections.Generic.Dictionary<string, bool> _depthFoldouts =
            new System.Collections.Generic.Dictionary<string, bool>();

        static System.Collections.Generic.HashSet<int> GetSelectedIndices(SerializedProperty depthProp)
        {
            if (depthProp == null)
                return null;

            string key = depthProp.propertyPath;
            if (!_selectedDepthIndices.TryGetValue(key, out var set))
            {
                set = new System.Collections.Generic.HashSet<int>();
                _selectedDepthIndices[key] = set;
            }
            return set;
        }

        static bool GetDepthFoldout(SerializedProperty depthProp)
        {
            if (depthProp == null)
                return true;

            string key = depthProp.propertyPath;
            if (!_depthFoldouts.TryGetValue(key, out var val))
            {
                val = true;
                _depthFoldouts[key] = val;
            }
            return val;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var facingProp = property.FindPropertyRelative("facing");
            var framesProp = property.FindPropertyRelative("frames");
            var depthProp  = property.FindPropertyRelative("depthModes");

            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = 2f;
            float y = position.y;

            // 头行：方向 + 信息（帧/深度数量）
            var facingRect = new Rect(position.x, y, position.width * 0.4f, lineHeight);
            var infoRect   = new Rect(position.x + position.width * 0.4f + 4, y, position.width * 0.6f - 4, lineHeight);

            EditorGUI.PropertyField(facingRect, facingProp, GUIContent.none);

            int frameCount = framesProp != null ? framesProp.arraySize : 0;
            EditorGUI.LabelField(infoRect, $"帧数: {frameCount}（深度列表自动跟随帧数）", EditorStyles.miniLabel);

            y += lineHeight + spacing;

            // 帧列表
            if (framesProp != null)
            {
                float framesHeight = EditorGUI.GetPropertyHeight(framesProp, true);
                var framesRect = new Rect(position.x, y, position.width, framesHeight);
                EditorGUI.PropertyField(framesRect, framesProp, new GUIContent("帧序列"), true);
                y += framesHeight + spacing;
            }

            // 同步 depthModes 长度到 frames
            if (framesProp != null && depthProp != null)
            {
                SyncDepthList(framesProp, depthProp);
            }

            // 深度列表
            if (depthProp != null && depthProp.isArray)
            {
                string key = depthProp.propertyPath;
                bool foldout = GetDepthFoldout(depthProp);

                var headerRect = new Rect(position.x, y, position.width, lineHeight);
                foldout = EditorGUI.Foldout(headerRect, foldout, "深度配置", true);
                _depthFoldouts[key] = foldout;
                y += lineHeight + spacing;

                if (foldout)
                {
                    int count = depthProp.arraySize;
                    var selected = GetSelectedIndices(depthProp);

                    for (int i = 0; i < count; i++)
                    {
                        var rowRect = new Rect(position.x, y, position.width, lineHeight);

                        float x = rowRect.x;
                        float toggleWidth = 16f;
                        float indexWidth = 30f;

                        var toggleRect = new Rect(x, rowRect.y, toggleWidth, lineHeight);
                        x += toggleWidth + 4f;

                        var indexRect = new Rect(x, rowRect.y, indexWidth, lineHeight);
                        x += indexWidth + 4f;

                        var enumRect = new Rect(x, rowRect.y, rowRect.width - (x - rowRect.x), lineHeight);

                        bool isSelected = selected != null && selected.Contains(i);
                        bool newSelected = EditorGUI.Toggle(toggleRect, GUIContent.none, isSelected);
                        if (newSelected != isSelected && selected != null)
                        {
                            bool shift = Event.current != null && (Event.current.modifiers & EventModifiers.Shift) != 0;

                            if (shift && _lastClickedDepthIndex.TryGetValue(key, out var lastIndex) && lastIndex >= 0 && lastIndex < count)
                            {
                                int from = Mathf.Min(lastIndex, i);
                                int to = Mathf.Max(lastIndex, i);
                                for (int j = from; j <= to; j++)
                                {
                                    if (newSelected)
                                        selected.Add(j);
                                    else
                                        selected.Remove(j);
                                }
                            }
                            else
                            {
                                if (newSelected)
                                    selected.Add(i);
                                else
                                    selected.Remove(i);
                            }

                            _lastClickedDepthIndex[key] = i;
                        }

                        EditorGUI.LabelField(indexRect, i.ToString());

                        var elem = depthProp.GetArrayElementAtIndex(i);

                        EditorGUI.BeginChangeCheck();
                        EditorGUI.PropertyField(enumRect, elem, GUIContent.none);
                        if (EditorGUI.EndChangeCheck())
                        {
                            int newEnumIndex = elem.enumValueIndex;

                            if (selected != null && selected.Count > 1 && selected.Contains(i))
                            {
                                foreach (var idx in selected)
                                {
                                    if (idx == i)
                                        continue;
                                    if (idx < 0 || idx >= count)
                                        continue;

                                    var other = depthProp.GetArrayElementAtIndex(idx);
                                    other.enumValueIndex = newEnumIndex;
                                }
                            }
                        }

                        y += lineHeight + spacing;
                    }
                }
            }

            EditorGUI.EndProperty();
        }

        static void SyncDepthList(SerializedProperty framesProp, SerializedProperty depthProp)
        {
            int frameCount = Mathf.Max(framesProp.arraySize, 0);
            if (frameCount == depthProp.arraySize)
                return;

            // 扩展：新增元素默认设为 Front（身前）
            while (depthProp.arraySize < frameCount)
            {
                depthProp.arraySize++;
                var elem = depthProp.GetArrayElementAtIndex(depthProp.arraySize - 1);
                elem.enumValueIndex = (int)FrameDepthMode.Front;
            }

            // 截断多余的元素
            while (depthProp.arraySize > frameCount)
            {
                depthProp.arraySize--;
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var framesProp = property.FindPropertyRelative("frames");
            var depthProp  = property.FindPropertyRelative("depthModes");

            float h = 0f;
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = 2f;

            h += lineHeight + spacing; // 头行

            if (framesProp != null)
                h += EditorGUI.GetPropertyHeight(framesProp, true) + spacing;

            if (depthProp != null && depthProp.isArray)
            {
                h += lineHeight + spacing; // 折叠标题行

                string key = depthProp.propertyPath;
                bool foldout = GetDepthFoldout(depthProp);

                if (foldout)
                {
                    int count = depthProp.arraySize;
                    if (count > 0)
                        h += count * (lineHeight + spacing);
                }
            }

            return h;
        }
    }
}
