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
                case EquipmentType.Helmet:
                    DrawHelmetFields();
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
        }
        
        /// <summary>
        /// 服装: 4方向贴图 (Body 层)
        /// </summary>
        void DrawClothingFields()
        {
            DrawDirectionalSprites("服装通过 Body UV Map 映射到角色躯干");
        }
        
        /// <summary>
        /// 头盔: 4方向贴图 (Head 层，覆盖在头发/胡子之上)
        /// </summary>
        void DrawHelmetFields()
        {
            DrawDirectionalSprites("头盔通过 Head UV Map 映射到角色头部\n渲染在头发/胡子之上");
        }
        
        /// <summary>
        /// 手套: 左右手颜色
        /// </summary>
        void DrawGlovesFields()
        {
            DrawLeftRightColors("左手颜色", "右手颜色", "手套替换角色手部像素的颜色");
        }
        
        /// <summary>
        /// 鞋子: 左右脚颜色
        /// </summary>
        void DrawShoesFields()
        {
            DrawLeftRightColors("左脚颜色", "右脚颜色", "鞋子替换角色脚部像素的颜色");
        }
        
        /// <summary>
        /// 绘制 4 方向贴图字段 (通用)
        /// </summary>
        void DrawDirectionalSprites(string helpText)
        {
            EditorGUILayout.LabelField("贴图 (4方向)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_spriteSE, new GUIContent("SE 东南 (必填)"));
            EditorGUILayout.PropertyField(_spriteSW, new GUIContent("SW 西南"));
            EditorGUILayout.PropertyField(_spriteNE, new GUIContent("NE 东北"));
            EditorGUILayout.PropertyField(_spriteNW, new GUIContent("NW 西北"));
            
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(helpText, MessageType.Info);
        }
        
        /// <summary>
        /// 绘制左右颜色字段 (通用)
        /// </summary>
        void DrawLeftRightColors(string leftLabel, string rightLabel, string helpText)
        {
            EditorGUILayout.LabelField("颜色", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_leftColor, new GUIContent(leftLabel));
            EditorGUILayout.PropertyField(_rightColor, new GUIContent(rightLabel));
            
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(helpText, MessageType.Info);
        }
    }
}
