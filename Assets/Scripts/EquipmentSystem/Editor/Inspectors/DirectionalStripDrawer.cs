using UnityEngine;
using UnityEditor;
using EquipmentSystem;

namespace EquipmentSystem.Editor
{
    /// <summary>
    /// DirectionalStrip 的简化 PropertyDrawer
    /// 注：AnimSequenceEntryDrawer 现在直接使用四向网格显示，该 Drawer 仅在直接展开 strips 时显示
    /// </summary>
    [CustomPropertyDrawer(typeof(DirectionalStrip))]
    public class DirectionalStripDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var facingProp = property.FindPropertyRelative("facing");
            var framesProp = property.FindPropertyRelative("frames");

            float lineHeight = EditorGUIUtility.singleLineHeight;
            float y = position.y;

            // 单行显示：方向 + 帧数
            float facingWidth = 80f;
            float infoWidth = position.width - facingWidth - 4f;

            var facingRect = new Rect(position.x, y, facingWidth, lineHeight);
            var infoRect = new Rect(position.x + facingWidth + 4f, y, infoWidth, lineHeight);

            EditorGUI.PropertyField(facingRect, facingProp, GUIContent.none);

            int frameCount = framesProp != null ? framesProp.arraySize : 0;
            EditorGUI.LabelField(infoRect, $"帧数: {frameCount}", EditorStyles.miniLabel);

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }
    }
}
