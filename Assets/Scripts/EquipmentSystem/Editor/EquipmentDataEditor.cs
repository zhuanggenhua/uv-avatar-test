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
        SerializedProperty _weaponSprite;
        SerializedProperty _spriteSE;
        SerializedProperty _spriteSW;
        SerializedProperty _spriteNE;
        SerializedProperty _spriteNW;
        SerializedProperty _anchorType;
        SerializedProperty _selfAnchor;
        SerializedProperty _sortingOffset;
        SerializedProperty _leftColor;
        SerializedProperty _rightColor;
        
        void OnEnable()
        {
            _equipmentId = serializedObject.FindProperty("equipmentId");
            _type = serializedObject.FindProperty("type");
            _weaponSprite = serializedObject.FindProperty("weaponSprite");
            _spriteSE = serializedObject.FindProperty("spriteSE");
            _spriteSW = serializedObject.FindProperty("spriteSW");
            _spriteNE = serializedObject.FindProperty("spriteNE");
            _spriteNW = serializedObject.FindProperty("spriteNW");
            _anchorType = serializedObject.FindProperty("anchorType");
            _selfAnchor = serializedObject.FindProperty("selfAnchor");
            _sortingOffset = serializedObject.FindProperty("sortingOffset");
            _leftColor = serializedObject.FindProperty("leftColor");
            _rightColor = serializedObject.FindProperty("rightColor");
        }
        
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            // 基础
            EditorGUILayout.LabelField("基础", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_equipmentId, new GUIContent("装备ID"));
            EditorGUILayout.PropertyField(_type, new GUIContent("装备类型"));
            
            EditorGUILayout.Space(10);
            
            var equipType = (EquipmentType)_type.enumValueIndex;
            
            switch (equipType)
            {
                case EquipmentType.Weapon:
                    DrawWeaponFields();
                    break;
                case EquipmentType.Clothing:
                    DrawClothingFields();
                    break;
                case EquipmentType.HeadGear:
                    DrawHeadGearFields();
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
        /// 武器: 单张贴图 + 锚点 + 渲染
        /// </summary>
        void DrawWeaponFields()
        {
            EditorGUILayout.PropertyField(_weaponSprite, new GUIContent("武器贴图"));
            
            EditorGUILayout.Space(10);
            
            EditorGUILayout.LabelField("挂点设置", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_anchorType, new GUIContent("挂点类型", "左手或右手"));
            EditorGUILayout.PropertyField(_selfAnchor, new GUIContent("装备锚点", "装备自身的锚点位置 (像素坐标)"));
            EditorGUILayout.PropertyField(_sortingOffset, new GUIContent("排序偏移", "相对角色的渲染层级偏移"));
        }
        
        /// <summary>
        /// 服装: 4方向贴图 (Body 层)
        /// </summary>
        void DrawClothingFields()
        {
            EditorGUILayout.LabelField("贴图 (4方向)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_spriteSE, new GUIContent("SE 东南 (必填)"));
            EditorGUILayout.PropertyField(_spriteSW, new GUIContent("SW 西南"));
            EditorGUILayout.PropertyField(_spriteNE, new GUIContent("NE 东北"));
            EditorGUILayout.PropertyField(_spriteNW, new GUIContent("NW 西北"));
            
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox("服装通过 Body UV Map 映射到角色躯干", MessageType.Info);
        }
        
        /// <summary>
        /// 头部装饰: 4方向贴图 (Head 层)
        /// </summary>
        void DrawHeadGearFields()
        {
            EditorGUILayout.LabelField("贴图 (4方向)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_spriteSE, new GUIContent("SE 东南 (必填)"));
            EditorGUILayout.PropertyField(_spriteSW, new GUIContent("SW 西南"));
            EditorGUILayout.PropertyField(_spriteNE, new GUIContent("NE 东北"));
            EditorGUILayout.PropertyField(_spriteNW, new GUIContent("NW 西北"));
            
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox("头部装饰通过 Head UV Map 映射到角色头部\n包括头盔、胡子、头发等", MessageType.Info);
        }
        
        /// <summary>
        /// 手套: 左右手颜色
        /// </summary>
        void DrawGlovesFields()
        {
            EditorGUILayout.LabelField("颜色", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_leftColor, new GUIContent("左手颜色"));
            EditorGUILayout.PropertyField(_rightColor, new GUIContent("右手颜色"));
            
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox("手套替换角色手部像素的颜色", MessageType.Info);
        }
        
        /// <summary>
        /// 鞋子: 左右脚颜色
        /// </summary>
        void DrawShoesFields()
        {
            EditorGUILayout.LabelField("颜色", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_leftColor, new GUIContent("左脚颜色"));
            EditorGUILayout.PropertyField(_rightColor, new GUIContent("右脚颜色"));
            
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox("鞋子替换角色脚部像素的颜色", MessageType.Info);
        }
    }
}
