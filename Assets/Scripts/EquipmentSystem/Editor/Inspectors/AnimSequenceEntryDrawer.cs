using UnityEngine;
using UnityEditor;
using EquipmentSystem;

namespace EquipmentSystem.Editor
{
    /// <summary>
    /// AnimSequenceEntry 的自定义 PropertyDrawer
    /// 使用四向序列帧网格显示
    /// </summary>
    [CustomPropertyDrawer(typeof(AnimSequenceEntry))]
    public class AnimSequenceEntryDrawer : PropertyDrawer
    {
        const float HEADER_HEIGHT = 20f;
        const float GRID_PADDING = 4f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            
            var animTypeProp = property.FindPropertyRelative("animationType");
            var stripsProp = property.FindPropertyRelative("strips");
            
            float y = position.y;
            
            // 第一行：动画类型 + 信息
            var headerRect = new Rect(position.x, y, position.width, HEADER_HEIGHT);
            DrawHeader(headerRect, animTypeProp, stripsProp);
            y += HEADER_HEIGHT + 2f;
            
            // 四向序列帧网格
            var gridRect = new Rect(position.x, y, position.width, AnimSequenceDrawerUtils.GetGridHeight());
            
            // 获取实际的 AnimSequenceEntry 对象
            AnimSequenceEntry entry = GetEntryFromProperty(property);
            
            AnimSequenceDrawerUtils.DrawDirectionalGridRect(gridRect, entry, property, AnimSequenceDrawerUtils.FRAMES_PER_ROW);
            
            EditorGUI.EndProperty();
        }

        void DrawHeader(Rect rect, SerializedProperty animTypeProp, SerializedProperty stripsProp)
        {
            float typeWidth = rect.width * 0.55f;
            float infoWidth = rect.width * 0.45f;
            
            // 动画类型字段
            var typeRect = new Rect(rect.x, rect.y, typeWidth - 4f, rect.height);
            EditorGUI.PropertyField(typeRect, animTypeProp, GUIContent.none);
            
            // 信息显示：动画名 + 方向数 + 帧数
            var infoRect = new Rect(rect.x + typeWidth, rect.y, infoWidth, rect.height);
            
            string displayName = "(未设置)";
            if (animTypeProp.objectReferenceValue != null)
            {
                var animType = animTypeProp.objectReferenceValue as AnimationTypeItem;
                displayName = animType != null ? animType.name : "(未设置)";
            }
            
            // 统计方向数和总帧数
            int dirCount = 0;
            int totalFrames = 0;
            if (stripsProp != null)
            {
                for (int i = 0; i < stripsProp.arraySize; i++)
                {
                    var framesProp = stripsProp.GetArrayElementAtIndex(i).FindPropertyRelative("frames");
                    if (framesProp != null && framesProp.arraySize > 0)
                    {
                        dirCount++;
                        totalFrames += framesProp.arraySize;
                    }
                }
            }
            
            string info = dirCount > 0 
                ? $"{displayName} [{dirCount}向/{totalFrames}帧]" 
                : displayName;
            EditorGUI.LabelField(infoRect, info, EditorStyles.miniLabel);
        }

        /// <summary>
        /// 从 SerializedProperty 获取实际的 AnimSequenceEntry 对象
        /// </summary>
        AnimSequenceEntry GetEntryFromProperty(SerializedProperty property)
        {
            // 尝试从父对象获取
            var targetObject = property.serializedObject.targetObject;
            if (targetObject is EquipmentRenderData equipment)
            {
                // 解析 property path 获取索引
                // 路径格式如: "animSequences.Array.data[0]"
                string path = property.propertyPath;
                if (path.StartsWith("animSequences.Array.data[") && path.EndsWith("]"))
                {
                    string indexStr = path.Substring(25, path.Length - 26);
                    if (int.TryParse(indexStr, out int index))
                    {
                        if (equipment.animSequences != null && index < equipment.animSequences.Count)
                        {
                            return equipment.animSequences[index];
                        }
                    }
                }
            }
            return null;
        }
        
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return HEADER_HEIGHT + 2f + AnimSequenceDrawerUtils.GetGridHeight() + GRID_PADDING;
        }
    }
}
