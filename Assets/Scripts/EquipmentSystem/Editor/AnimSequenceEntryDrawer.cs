using UnityEngine;
using UnityEditor;
using EquipmentSystem.Data;

namespace EquipmentSystem.Editor
{
    /// <summary>
    /// AnimSequenceEntry 的自定义 PropertyDrawer
    /// 支持 AnimationTypeItem 选择
    /// </summary>
    [CustomPropertyDrawer(typeof(AnimSequenceEntry))]
    public class AnimSequenceEntryDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            
            var animTypeProp = property.FindPropertyRelative("animationType");
            var stripsProp = property.FindPropertyRelative("strips");
            
            var lineHeight = EditorGUIUtility.singleLineHeight;
            var spacing = 2f;
            float y = position.y;
            
            // 第一行：动画类型（ScriptableObject）
            var typeRect = new Rect(position.x, y, position.width * 0.6f - 5, lineHeight);
            var infoRect = new Rect(position.x + position.width * 0.6f + 5, y, position.width * 0.4f - 5, lineHeight);
            
            EditorGUI.PropertyField(typeRect, animTypeProp, GUIContent.none);
            
            // 信息显示
            int stripCount = stripsProp.arraySize;
            string displayName = "(未设置)";
            if (animTypeProp.objectReferenceValue != null)
            {
                var animType = animTypeProp.objectReferenceValue as AnimationTypeItem;
                displayName = animType != null ? animType.name : "(未设置)";
            }
            EditorGUI.LabelField(infoRect, $"{displayName} ({stripCount}向)", EditorStyles.miniLabel);
            
            y += lineHeight + spacing;
            
            // strips 列表
            var stripsRect = new Rect(position.x, y, position.width, 
                EditorGUI.GetPropertyHeight(stripsProp, true));
            EditorGUI.PropertyField(stripsRect, stripsProp, new GUIContent("方向序列帧"), true);
            
            EditorGUI.EndProperty();
        }
        
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var stripsProp = property.FindPropertyRelative("strips");
            return EditorGUIUtility.singleLineHeight + 4 + EditorGUI.GetPropertyHeight(stripsProp, true);
        }
    }
}
