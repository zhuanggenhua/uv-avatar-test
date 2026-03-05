using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace EquipmentSystem.Editor
{
    [CustomEditor(typeof(global::EquipmentSystem.DepixelizeRendererFeature))]
    public class DepixelizeRendererFeatureEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();

            EditorGUILayout.Space();

            if (GUILayout.Button("重置为默认值"))
            {
                Undo.RecordObject(target, "Reset Depixelize Settings");

                SerializedProperty settingsProp = serializedObject.FindProperty("settings");
                if (settingsProp != null)
                {
                    SerializedProperty shaderProp = settingsProp.FindPropertyRelative("shader");
                    if (shaderProp != null) shaderProp.objectReferenceValue = null;

                    SerializedProperty rpeProp = settingsProp.FindPropertyRelative("renderPassEvent");
                    if (rpeProp != null) rpeProp.intValue = (int)RenderPassEvent.AfterRenderingPostProcessing;

                    SerializedProperty pixelScaleProp = settingsProp.FindPropertyRelative("pixelScale");
                    if (pixelScaleProp != null) pixelScaleProp.floatValue = 4f;

                    SerializedProperty colorThresholdProp = settingsProp.FindPropertyRelative("colorThreshold");
                    if (colorThresholdProp != null) colorThresholdProp.floatValue = 0.1176f;

                    SerializedProperty contourThresholdProp = settingsProp.FindPropertyRelative("contourThreshold");
                    if (contourThresholdProp != null) contourThresholdProp.floatValue = 0.392f;

                    SerializedProperty smoothnessProp = settingsProp.FindPropertyRelative("smoothness");
                    if (smoothnessProp != null) smoothnessProp.floatValue = 1.0f;

                    SerializedProperty antialiasingProp = settingsProp.FindPropertyRelative("antialiasing");
                    if (antialiasingProp != null) antialiasingProp.floatValue = 0.5f;

                    SerializedProperty curveWeightProp = settingsProp.FindPropertyRelative("curveWeight");
                    if (curveWeightProp != null) curveWeightProp.floatValue = 1.0f;

                    SerializedProperty sparseWeightProp = settingsProp.FindPropertyRelative("sparseWeight");
                    if (sparseWeightProp != null) sparseWeightProp.floatValue = 1.0f;

                    SerializedProperty islandWeightProp = settingsProp.FindPropertyRelative("islandWeight");
                    if (islandWeightProp != null) islandWeightProp.floatValue = 5.0f;

                    SerializedProperty enabledProp = settingsProp.FindPropertyRelative("enabled");
                    if (enabledProp != null) enabledProp.boolValue = true;

                    SerializedProperty debugModeProp = settingsProp.FindPropertyRelative("debugMode");
                    if (debugModeProp != null) debugModeProp.intValue = 0;
                }

                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
