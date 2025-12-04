using UnityEngine;
using UnityEditor;
using EquipmentSystem.Data;

namespace EquipmentSystem.Editor
{
    /// <summary>
    /// EquipmentData 自定义编辑器（配置驱动）
    /// 根据 EquipTypeRegistry 的 RenderMode 决定显示内容
    /// </summary>
    [CustomEditor(typeof(EquipmentData))]
    public class EquipmentDataEditor : UnityEditor.Editor
    {
        SerializedProperty _equipmentId;
        SerializedProperty _type;
        SerializedProperty _spriteSE, _spriteSW, _spriteNE, _spriteNW;
        SerializedProperty _weaponSlotType;
        SerializedProperty _leftColor, _rightColor;
        SerializedProperty _animSet;
        SerializedProperty _upVariant, _downVariant;
        SerializedProperty _hideHair, _hideBeard;
        
        void OnEnable()
        {
            _equipmentId = serializedObject.FindProperty("equipmentId");
            _type = serializedObject.FindProperty("type");
            _spriteSE = serializedObject.FindProperty("spriteSE");
            _spriteSW = serializedObject.FindProperty("spriteSW");
            _spriteNE = serializedObject.FindProperty("spriteNE");
            _spriteNW = serializedObject.FindProperty("spriteNW");
            _weaponSlotType = serializedObject.FindProperty("weaponSlotType");
            _leftColor = serializedObject.FindProperty("leftColor");
            _rightColor = serializedObject.FindProperty("rightColor");
            _animSet = serializedObject.FindProperty("animSet");
            _upVariant = serializedObject.FindProperty("upVariant");
            _downVariant = serializedObject.FindProperty("downVariant");
            _hideHair = serializedObject.FindProperty("hideHair");
            _hideBeard = serializedObject.FindProperty("hideBeard");
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
            var cfg = EquipTypeRegistry.Get(equipType);
            
            // 根据 RenderMode 绘制对应字段
            if (cfg != null)
            {
                switch (cfg.RenderMode)
                {
                    case EquipRenderMode.Sprite:
                        DrawSpriteFields(cfg);
                        break;
                    case EquipRenderMode.Color:
                        DrawColorFields(equipType);
                        break;
                    case EquipRenderMode.Weapon:
                        DrawWeaponFields();
                        break;
                    case EquipRenderMode.None:
                        EditorGUILayout.HelpBox("此类型暂无可配置字段", MessageType.Info);
                        break;
                }
            }
            
            serializedObject.ApplyModifiedProperties();
        }
        
        /// <summary>
        /// Sprite 类型：4向贴图 + 变体 + 动画集 + 特殊字段
        /// </summary>
        void DrawSpriteFields(EquipTypeConfig cfg)
        {
            string layerName = cfg.BodyPart == CharacterBodyPart.Head ? "Head 层" : "Body 层";
            DrawDirectionalSprites(layerName);
            DrawAnimSetField();
            
            // 头部装备共有字段：隐藏头发/胡子
            if (cfg.BodyPart == CharacterBodyPart.Head)
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("头部装备设置", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_hideHair, new GUIContent("隐藏头发"));
                EditorGUILayout.PropertyField(_hideBeard, new GUIContent("隐藏胡子"));
            }
        }
        
        /// <summary>
        /// 颜色类型：左右颜色
        /// </summary>
        void DrawColorFields(EquipmentType type)
        {
            string leftLabel = type == EquipmentType.Gloves ? "左手颜色" : "左脚颜色";
            string rightLabel = type == EquipmentType.Gloves ? "右手颜色" : "右脚颜色";
            string helpText = type == EquipmentType.Gloves 
                ? "手套替换角色手部像素的颜色" 
                : "鞋子替换角色脚部像素的颜色";
            
            EditorGUILayout.LabelField("颜色", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_leftColor, new GUIContent(leftLabel));
            EditorGUILayout.PropertyField(_rightColor, new GUIContent(rightLabel));
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(helpText, MessageType.Info);
        }
        
        /// <summary>
        /// 武器：四向基础贴图 + 槽位类型 + 动画集
        /// </summary>
        void DrawWeaponFields()
        {
            // 基础四向贴图（与 Sprite 类型一致）
            EditorGUILayout.LabelField("基础贴图 (4 向)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_spriteSE, new GUIContent("SE 东南 (必填)"));
            EditorGUILayout.PropertyField(_spriteSW, new GUIContent("SW 西南"));
            EditorGUILayout.PropertyField(_spriteNE, new GUIContent("NE 东北"));
            EditorGUILayout.PropertyField(_spriteNW, new GUIContent("NW 西北"));
            EditorGUILayout.HelpBox(
                "武器贴图基于 32x32 画布，\"虚拟左手\"基准点约为像素格 (15, 16)。\n" +
                "运行时按 AnchorDirection + 当前行进行旋转/镜像，始终围绕虚拟左手 pivot 渲染。",
                MessageType.Info);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("武器槽位", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_weaponSlotType, new GUIContent("槽位类型"));
            EditorGUILayout.HelpBox(
                "• 主手: 单手武器，可搭配副手\n" +
                "• 双手: 双手武器，禁止副手\n" +
                "• 双持: 一件装备两个锚点显示，禁止副手\n" +
                "• 副手: 盾牌等，只能装备在副手槽", 
                MessageType.Info);
            
            DrawAnimSetField();
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
        /// 绘制变体配置（向上/向下）
        /// </summary>
        void DrawSpriteVariants()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("贴图变体（可选）", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("按帧配置的语义化变体：基础 / 向上 / 向下。SE 不为空时视为启用该变体。", MessageType.Info);

            DrawDirectionalSet("向上变体 (Up)", _upVariant);
            DrawDirectionalSet("向下变体 (Down)", _downVariant);
        }
        
        /// <summary>
        /// 绘制单个 DirectionalSpriteSet（用于 Up/Down 变体）
        /// </summary>
        void DrawDirectionalSet(string title, SerializedProperty setProp)
        {
            if (setProp == null) return;

            EditorGUILayout.BeginVertical("helpbox");
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

            var seProp = setProp.FindPropertyRelative("se");
            var swProp = setProp.FindPropertyRelative("sw");
            var neProp = setProp.FindPropertyRelative("ne");
            var nwProp = setProp.FindPropertyRelative("nw");

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("SE", GUILayout.Width(24));
            EditorGUILayout.PropertyField(seProp, GUIContent.none, GUILayout.Width(60));
            GUILayout.Label("SW", GUILayout.Width(24));
            EditorGUILayout.PropertyField(swProp, GUIContent.none, GUILayout.Width(60));
            GUILayout.Label("NE", GUILayout.Width(24));
            EditorGUILayout.PropertyField(neProp, GUIContent.none, GUILayout.Width(60));
            GUILayout.Label("NW", GUILayout.Width(24));
            EditorGUILayout.PropertyField(nwProp, GUIContent.none, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }
        
        /// <summary>
        /// 绘制动画集字段
        /// </summary>
        void DrawAnimSetField()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.PropertyField(_animSet, new GUIContent("动画集", "一整套动画，可被多个装备共享"));
            
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
    }
}
