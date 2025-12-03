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
        SerializedProperty _animSet;
        SerializedProperty _spriteVariants;
        
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
            _animSet = serializedObject.FindProperty("animSet");
            _spriteVariants = serializedObject.FindProperty("spriteVariants");
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
                case EquipmentType.Cloak:
                    DrawCloakFields();
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
        /// 武器: 单张贴图 + 锚点 + 序列帧覆盖
        /// </summary>
        void DrawWeaponFields()
        {
            EditorGUILayout.LabelField("基础贴图（挂点模式打底）", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_weaponSprite, new GUIContent("武器贴图"));
            
            EditorGUILayout.Space(10);
            
            EditorGUILayout.LabelField("挂点设置", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_anchorType, new GUIContent("挂点类型", "左手或右手"));
            EditorGUILayout.PropertyField(_selfAnchor, new GUIContent("装备锚点", "装备自身的锚点位置 (像素坐标)"));
            
            DrawAnimSetField("动画集可覆盖挂点模式");
        }
        
        /// <summary>
        /// 服装: 4方向贴图 (Body 层) + 序列帧覆盖
        /// </summary>
        void DrawClothingFields()
        {
            DrawDirectionalSprites("Body 层");
            DrawAnimSetField("动画集可覆盖 UV 模式");
        }
        
        /// <summary>
        /// 斗篷: 4方向贴图 (Body 层)，渲染在服装前面
        /// </summary>
        void DrawCloakFields()
        {
            DrawDirectionalSprites("Body 层，在服装上面");
            DrawAnimSetField("动画集可覆盖 UV 模式");
        }
        
        /// <summary>
        /// 头盔: 4方向贴图 (Head 层，覆盖在头发/胡子之上) + 序列帧覆盖
        /// </summary>
        void DrawHelmetFields()
        {
            DrawDirectionalSprites("Head 层，在头发/胡子上面");
            DrawAnimSetField("动画集可覆盖 UV 模式");
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
            EditorGUILayout.LabelField("基础贴图（UV 模式打底）", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_spriteSE, new GUIContent("SE 东南 (必填)"));
            EditorGUILayout.PropertyField(_spriteSW, new GUIContent("SW 西南"));
            EditorGUILayout.PropertyField(_spriteNE, new GUIContent("NE 东北"));
            EditorGUILayout.PropertyField(_spriteNW, new GUIContent("NW 西北"));
            
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(helpText, MessageType.Info);
            
            // 变体贴图
            DrawSpriteVariants();
        }
        
        /// <summary>
        /// 绘制变体贴图数组
        /// </summary>
        void DrawSpriteVariants()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("贴图变体（可选）", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("用于移动等动画的形变贴图，如斗篷飘动。\n在帧编辑器中选择当前帧使用哪个变体。", MessageType.Info);
            
            EditorGUILayout.Space(5);
            
            // 添加/删除按钮
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ 添加变体", GUILayout.Width(100)))
            {
                _spriteVariants.InsertArrayElementAtIndex(_spriteVariants.arraySize);
            }
            EditorGUILayout.EndHorizontal();
            
            // 绘制每个变体
            for (int i = 0; i < _spriteVariants.arraySize; i++)
            {
                var variant = _spriteVariants.GetArrayElementAtIndex(i);
                var seProp = variant.FindPropertyRelative("se");
                var swProp = variant.FindPropertyRelative("sw");
                var neProp = variant.FindPropertyRelative("ne");
                var nwProp = variant.FindPropertyRelative("nw");
                
                EditorGUILayout.Space(5);
                EditorGUILayout.BeginVertical("helpbox");
                
                // 标题行 + 删除按钮
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"变体 {i + 1}", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("×", GUILayout.Width(25)))
                {
                    _spriteVariants.DeleteArrayElementAtIndex(i);
                    break;
                }
                EditorGUILayout.EndHorizontal();
                
                // 4方向贴图
                EditorGUILayout.PropertyField(seProp, new GUIContent("SE"));
                EditorGUILayout.PropertyField(swProp, new GUIContent("SW"));
                EditorGUILayout.PropertyField(neProp, new GUIContent("NE"));
                EditorGUILayout.PropertyField(nwProp, new GUIContent("NW"));
                
                EditorGUILayout.EndVertical();
            }
        }
        
        /// <summary>
        /// 绘制动画集字段
        /// </summary>
        void DrawAnimSetField(string helpText)
        {
            EditorGUILayout.PropertyField(_animSet, new GUIContent("动画集", "一整套动画（Idle/Walk/Attack/Die 等），可被多个装备共享"));
            
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(helpText, MessageType.Info);
            
            // 如果已选择动画集，显示包含的动画列表
            var animSetObj = _animSet.objectReferenceValue as EquipAnimSetAsset;
            if (animSetObj != null && animSetObj.animations != null && animSetObj.animations.Count > 0)
            {
                EditorGUILayout.BeginVertical("helpbox");
                EditorGUILayout.LabelField("包含的动画:", EditorStyles.miniLabel);
                var animTypes = animSetObj.GetAnimationTypes();
                var displayNames = animTypes.ConvertAll(t => t != null ? t.name : "(空)");
                EditorGUILayout.LabelField(string.Join(", ", displayNames), EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.EndVertical();
            }
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
