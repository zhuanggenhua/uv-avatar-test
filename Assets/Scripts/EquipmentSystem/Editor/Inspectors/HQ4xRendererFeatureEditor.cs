using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace EquipmentSystem.Editor
{
    [CustomEditor(typeof(global::EquipmentSystem.HQ4xRendererFeature))]
    public class HQ4xRendererFeatureEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();

            EditorGUILayout.Space();

            if (GUILayout.Button("重置为默认值"))
            {
                Undo.RecordObject(target, "Reset HQ4x Settings");

                SerializedProperty settingsProp = serializedObject.FindProperty("hq4xSettings");
                if (settingsProp != null)
                {
                    SerializedProperty shaderProp = settingsProp.FindPropertyRelative("shader");
                    if (shaderProp != null) shaderProp.objectReferenceValue = null;

                    SerializedProperty lutProp = settingsProp.FindPropertyRelative("lut");
                    if (lutProp != null) lutProp.objectReferenceValue = null;

                    SerializedProperty rpeProp = settingsProp.FindPropertyRelative("renderPassEvent");
                    if (rpeProp != null) rpeProp.intValue = (int)RenderPassEvent.AfterRenderingPostProcessing;

                    SerializedProperty scaleProp = settingsProp.FindPropertyRelative("pixelScale");
                    if (scaleProp != null) scaleProp.floatValue = 4f;

                    SerializedProperty enabledProp = settingsProp.FindPropertyRelative("enabled");
                    if (enabledProp != null) enabledProp.boolValue = true;
                }

                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
