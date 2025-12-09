using UnityEditor;
using UnityEngine;
using EquipmentSystem;

namespace EquipmentSystem.Editor
{
    /// <summary>
    /// AnimationTypeDatabase 自定义 Inspector
    /// 在默认面板下方增加“扫描并注册所有动画类型”的按钮
    /// </summary>
    [CustomEditor(typeof(AnimationTypeDatabase))]
    public class AnimationTypeDatabaseEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // 先画默认 Inspector（_items 列表等）
            base.OnInspectorGUI();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("动画类型自动注册", EditorStyles.boldLabel);

            if (GUILayout.Button("扫描并注册所有 AnimationTypeItem"))
            {
                AnimationTypeAutoRegister.ScanAndRegisterAll();
            }
        }
    }
}
