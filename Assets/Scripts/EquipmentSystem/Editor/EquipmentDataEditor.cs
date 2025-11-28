using UnityEngine;
using UnityEditor;
using EquipmentSystem.Data;

namespace EquipmentSystem.Editor
{
    /// <summary>
    /// EquipmentData 自定义编辑器
    /// 根据装备类型显示对应的配置项
    /// </summary>
    [CustomEditor(typeof(EquipmentData))]
    public class EquipmentDataEditor : UnityEditor.Editor
    {
        SerializedProperty _equipmentId;
        SerializedProperty _type;
        SerializedProperty _frontSprite;
        SerializedProperty _backSprite;
        SerializedProperty _anchorType;
        SerializedProperty _selfAnchor;
        SerializedProperty _leftColor;
        SerializedProperty _rightColor;
        SerializedProperty _sortingOffset;
        
        void OnEnable()
        {
            _equipmentId = serializedObject.FindProperty("equipmentId");
            _type = serializedObject.FindProperty("type");
            _frontSprite = serializedObject.FindProperty("frontSprite");
            _backSprite = serializedObject.FindProperty("backSprite");
            _anchorType = serializedObject.FindProperty("anchorType");
            _selfAnchor = serializedObject.FindProperty("selfAnchor");
            _leftColor = serializedObject.FindProperty("leftColor");
            _rightColor = serializedObject.FindProperty("rightColor");
            _sortingOffset = serializedObject.FindProperty("sortingOffset");
        }
        
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            // ========== 基础设置 ==========
            EditorGUILayout.LabelField("基础", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_equipmentId, new GUIContent("装备ID"));
            EditorGUILayout.PropertyField(_type, new GUIContent("装备类型"));
            
            EditorGUILayout.Space(10);
            
            var equipType = (EquipmentType)_type.enumValueIndex;
            
            switch (equipType)
            {
                case EquipmentType.Accessory:
                    DrawAccessoryFields();
                    break;
                case EquipmentType.Clothing:
                    DrawClothingFields();
                    break;
                case EquipmentType.Gloves:
                    DrawGlovesFields();
                    break;
                case EquipmentType.Shoes:
                    DrawShoesFields();
                    break;
            }
            
            serializedObject.ApplyModifiedProperties();
        }
        
        /// <summary>
        /// 挂件配置: 贴图 + 锚点 + 渲染排序
        /// </summary>
        void DrawAccessoryFields()
        {
            // 贴图
            EditorGUILayout.LabelField("贴图", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_frontSprite, new GUIContent("正面贴图"));
            EditorGUILayout.PropertyField(_backSprite, new GUIContent("背面贴图"));
            
            EditorGUILayout.Space(10);
            
            // 挂点设置
            EditorGUILayout.LabelField("挂点设置", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_anchorType, new GUIContent("挂点类型"));
            EditorGUILayout.PropertyField(_selfAnchor, new GUIContent("装备锚点", "装备自身的锚点位置（像素坐标）"));
            
            EditorGUILayout.Space(10);
            
            // 渲染
            EditorGUILayout.LabelField("渲染", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_sortingOffset, new GUIContent("排序偏移", "相对角色的渲染层级偏移\n正数=前面, 负数=后面"));
        }
        
        /// <summary>
        /// 服装配置: 只需要贴图 (2x3像素映射)
        /// </summary>
        void DrawClothingFields()
        {
            EditorGUILayout.LabelField("贴图 (2×3像素)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_frontSprite, new GUIContent("正面贴图"));
            EditorGUILayout.PropertyField(_backSprite, new GUIContent("背面贴图"));
            
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox("服装贴图会映射到躯干区域的标记像素上", MessageType.Info);
        }
        
        /// <summary>
        /// 手套配置: 左右手颜色
        /// </summary>
        void DrawGlovesFields()
        {
            EditorGUILayout.LabelField("颜色", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_leftColor, new GUIContent("左手颜色"));
            EditorGUILayout.PropertyField(_rightColor, new GUIContent("右手颜色"));
            
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox("手套会替换角色手部像素的颜色", MessageType.Info);
        }
        
        /// <summary>
        /// 鞋子配置: 左右脚颜色
        /// </summary>
        void DrawShoesFields()
        {
            EditorGUILayout.LabelField("颜色", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_leftColor, new GUIContent("左脚颜色"));
            EditorGUILayout.PropertyField(_rightColor, new GUIContent("右脚颜色"));
            
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox("鞋子会替换角色脚部像素的颜色", MessageType.Info);
        }
    }
}
