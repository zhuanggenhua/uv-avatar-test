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
        SerializedProperty _layer;
        // 4方向贴图
        SerializedProperty _spriteSE;
        SerializedProperty _spriteSW;
        SerializedProperty _spriteNE;
        SerializedProperty _spriteNW;
        SerializedProperty _anchorType;
        SerializedProperty _selfAnchor;
        SerializedProperty _leftColor;
        SerializedProperty _rightColor;
        SerializedProperty _sortingOffset;
        
        void OnEnable()
        {
            _equipmentId = serializedObject.FindProperty("equipmentId");
            _type = serializedObject.FindProperty("type");
            _layer = serializedObject.FindProperty("layer");
            _spriteSE = serializedObject.FindProperty("spriteSE");
            _spriteSW = serializedObject.FindProperty("spriteSW");
            _spriteNE = serializedObject.FindProperty("spriteNE");
            _spriteNW = serializedObject.FindProperty("spriteNW");
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
            // 4方向贴图
            EditorGUILayout.LabelField("贴图 (4方向)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_spriteSE, new GUIContent("SE 东南 (必填)"));
            EditorGUILayout.PropertyField(_spriteSW, new GUIContent("SW 西南"));
            EditorGUILayout.PropertyField(_spriteNE, new GUIContent("NE 东北"));
            EditorGUILayout.PropertyField(_spriteNW, new GUIContent("NW 西北"));
            EditorGUILayout.HelpBox("只填 SE 时，其他方向会自动回退到 SE", MessageType.Info);
            
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
        /// 服装配置: 4方向贴图
        /// </summary>
        void DrawClothingFields()
        {
            EditorGUILayout.LabelField("层级", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_layer, new GUIContent("装备层", "Body=身体层(衣服), Head=头部层(头盔/胡子/头发)"));
            
            EditorGUILayout.Space(5);
            
            EditorGUILayout.LabelField("贴图 (4方向)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_spriteSE, new GUIContent("SE 东南 (必填)"));
            EditorGUILayout.PropertyField(_spriteSW, new GUIContent("SW 西南"));
            EditorGUILayout.PropertyField(_spriteNE, new GUIContent("NE 东北"));
            EditorGUILayout.PropertyField(_spriteNW, new GUIContent("NW 西北"));
            
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox("服装贴图会通过 UV Map 映射到角色\n只填 SE 时，其他方向会自动回退到 SE", MessageType.Info);
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
