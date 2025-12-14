using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EquipmentSystem.Editor
{
    /// <summary>
    /// 装备动画序列编辑器窗口
    /// 用于集中管理装备的序列帧动画
    /// </summary>
    public class EquipmentAnimSequenceEditor : EditorWindow
    {
        #region 字段

        // ==================== 数据源 ====================
        /// <summary>当前选中的装备</summary>
        [SerializeField] EquipmentRenderData _selectedEquipment;
        /// <summary>当前装备的 SerializedObject</summary>
        SerializedObject _serializedEquipment;
        /// <summary>animSequences 属性</summary>
        SerializedProperty _animSequencesProp;

        // ==================== 装备列表 ====================
        /// <summary>所有装备资产（按类型分组）</summary>
        Dictionary<EquipmentType, List<EquipmentRenderData>> _equipmentsByType;
        /// <summary>各类型的折叠状态</summary>
        Dictionary<EquipmentType, bool> _typeFoldouts = new Dictionary<EquipmentType, bool>();
        /// <summary>装备列表滚动位置</summary>
        Vector2 _listScroll;

        // ==================== 自动生成工具 ====================
        /// <summary>动画数据库</summary>
        [SerializeField] AnimationTypeDatabase _animDatabase;
        /// <summary>当前选中的动画类型</summary>
        [SerializeField] AnimationTypeItem _selectedAnimType;
        /// <summary>Spritesheet 贴图</summary>
        [SerializeField] Texture2D _spritesheet;
        /// <summary>生成模式：4=四向，1=单向SE</summary>
        int _directionMode = 4;
        /// <summary>手动行数（0 表示自动检测）</summary>
        [SerializeField] int _manualRowCount = 0;
        /// <summary>手动每行帧数（0 表示自动检测）</summary>
        [SerializeField] int _manualFramesPerRow = 0;

        // ==================== 视图状态 ====================
        /// <summary>右侧面板滚动位置</summary>
        Vector2 _rightScroll;
        /// <summary>当前选中的动画索引</summary>
        int _selectedAnimIndex = 0;

        #endregion

        #region 常量

        const string PREF_LAST_EQUIPMENT_PATH = "EquipAnimSeqEditor_LastEquipment";
        const string PREF_ANIM_DATABASE_PATH = "EquipAnimSeqEditor_AnimDatabase";
        const float SIDEBAR_WIDTH = 220f;

        #endregion

        #region 初始化

        [MenuItem("Tools/Equipment System/Equipment Anim Sequences")]
        public static void ShowWindow()
        {
            var window = GetWindow<EquipmentAnimSequenceEditor>("装备动画序列");
            window.minSize = new Vector2(800, 500);
        }

        /// <summary>
        /// 从指定装备打开窗口，并自动选中该装备
        /// </summary>
        public static void ShowWindowFor(EquipmentRenderData equipment)
        {
            var window = GetWindow<EquipmentAnimSequenceEditor>("装备动画序列");
            window.minSize = new Vector2(800, 500);
            if (equipment != null)
            {
                window.SelectEquipment(equipment);
            }
        }

        void OnEnable()
        {
            RefreshEquipmentList();
            RestoreLastState();

            if (_selectedEquipment == null && _equipmentsByType != null)
                SelectFirstEquipment();
        }

        void OnDisable()
        {
            SaveLastState();
        }

        void SaveLastState()
        {
            if (_selectedEquipment != null)
                EditorPrefs.SetString(PREF_LAST_EQUIPMENT_PATH, AssetDatabase.GetAssetPath(_selectedEquipment));

            if (_animDatabase != null)
                EditorPrefs.SetString(PREF_ANIM_DATABASE_PATH, AssetDatabase.GetAssetPath(_animDatabase));
        }

        void RestoreLastState()
        {
            // 恢复上次选中的装备
            string equipPath = EditorPrefs.GetString(PREF_LAST_EQUIPMENT_PATH, "");
            if (!string.IsNullOrEmpty(equipPath))
            {
                _selectedEquipment = AssetDatabase.LoadAssetAtPath<EquipmentRenderData>(equipPath);
                if (_selectedEquipment != null)
                    SetupSerializedObject();
            }

            // 恢复动画数据库
            string dbPath = EditorPrefs.GetString(PREF_ANIM_DATABASE_PATH, "");
            if (!string.IsNullOrEmpty(dbPath))
                _animDatabase = AssetDatabase.LoadAssetAtPath<AnimationTypeDatabase>(dbPath);
        }

        void SelectFirstEquipment()
        {
            if (_equipmentsByType == null) return;

            foreach (var kv in _equipmentsByType)
            {
                if (kv.Value.Count > 0)
                {
                    SelectEquipment(kv.Value[0]);
                    break;
                }
            }
        }

        #endregion

        #region 装备列表管理

        void RefreshEquipmentList()
        {
            _equipmentsByType = new Dictionary<EquipmentType, List<EquipmentRenderData>>();

            // 初始化所有类型
            foreach (EquipmentType type in Enum.GetValues(typeof(EquipmentType)))
            {
                _equipmentsByType[type] = new List<EquipmentRenderData>();
                if (!_typeFoldouts.ContainsKey(type))
                    _typeFoldouts[type] = true;
            }

            // 查找所有装备资产
            string[] guids = AssetDatabase.FindAssets("t:EquipmentRenderData");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var equipment = AssetDatabase.LoadAssetAtPath<EquipmentRenderData>(path);
                if (equipment != null)
                    _equipmentsByType[equipment.type].Add(equipment);
            }

            // 按名称排序
            foreach (var list in _equipmentsByType.Values)
                list.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
        }

        public void SelectEquipment(EquipmentRenderData equipment)
        {
            _selectedEquipment = equipment;
            SetupSerializedObject();
        }

        void SetupSerializedObject()
        {
            if (_selectedEquipment != null)
            {
                _serializedEquipment = new SerializedObject(_selectedEquipment);
                _animSequencesProp = _serializedEquipment.FindProperty("animSequences");
            }
            else
            {
                _serializedEquipment = null;
                _animSequencesProp = null;
            }
        }

        #endregion

        #region 主绘制

        void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();

            // 左侧：装备列表
            DrawSidebar();

            // 右侧：动画编辑区
            DrawMainPanel();

            EditorGUILayout.EndHorizontal();
        }

        void DrawSidebar()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(SIDEBAR_WIDTH));

            // 标题和刷新按钮
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField("装备列表", EditorStyles.boldLabel);
            if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(40)))
                RefreshEquipmentList();
            EditorGUILayout.EndHorizontal();

            // 列表
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);

            if (_equipmentsByType != null)
            {
                foreach (var kv in _equipmentsByType)
                {
                    if (kv.Value.Count == 0)
                        continue;

                    // 类型折叠框
                    _typeFoldouts[kv.Key] = EditorGUILayout.Foldout(_typeFoldouts[kv.Key], $"{kv.Key} ({kv.Value.Count})", true);

                    if (_typeFoldouts[kv.Key])
                    {
                        EditorGUI.indentLevel++;
                        foreach (var equipment in kv.Value)
                        {
                            bool isSelected = equipment == _selectedEquipment;
                            var style = isSelected ? EditorStyles.boldLabel : EditorStyles.label;

                            EditorGUILayout.BeginHorizontal();
                            GUILayout.Space(16);

                            if (GUILayout.Button(equipment.name, style))
                                SelectEquipment(equipment);

                            // 显示动画数量
                            int animCount = equipment.animSequences?.Count ?? 0;
                            if (animCount > 0)
                                GUILayout.Label($"[{animCount}]", GUILayout.Width(30));

                            EditorGUILayout.EndHorizontal();
                        }
                        EditorGUI.indentLevel--;
                    }
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        void DrawMainPanel()
        {
            EditorGUILayout.BeginVertical();
            _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);

            if (_selectedEquipment == null)
            {
                EditorGUILayout.HelpBox("请在左侧选择一个装备", MessageType.Info);
            }
            else
            {
                // 装备信息
                DrawEquipmentInfo();

                EditorGUILayout.Space(10);

                // 动画序列编辑
                DrawAnimSequences();

                EditorGUILayout.Space(10);

                // 自动生成工具
                DrawGeneratorTools();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        #endregion

        #region 装备信息

        void DrawEquipmentInfo()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("当前装备", EditorStyles.boldLabel);

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField("资产", _selectedEquipment, typeof(EquipmentRenderData), false);
            EditorGUILayout.TextField("ID", _selectedEquipment.equipmentId);
            EditorGUILayout.EnumPopup("类型", _selectedEquipment.type);
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndVertical();
        }

        #endregion

        #region 动画序列编辑

        void DrawAnimSequences()
        {
            if (_serializedEquipment == null || _animSequencesProp == null)
                return;

            _serializedEquipment.Update();

            EditorGUILayout.BeginVertical("box");
            
            // 标题行：动画序列 + 动画数量
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("动画序列", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"共 {_animSequencesProp.arraySize} 个动画", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            if (_animSequencesProp.arraySize == 0)
            {
                EditorGUILayout.HelpBox("暂无动画序列，请使用下方工具添加", MessageType.Info);
            }
            else
            {
                // 动画选择下拉框
                DrawAnimationSelector();
                
                EditorGUILayout.Space(5);
                
                // 四向序列帧网格
                DrawSelectedAnimationGrid();
            }

            _serializedEquipment.ApplyModifiedProperties();
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 绘制动画选择下拉框
        /// </summary>
        void DrawAnimationSelector()
        {
            if (_animSequencesProp == null || _animSequencesProp.arraySize == 0)
                return;

            // 构建动画名称列表
            var names = new string[_animSequencesProp.arraySize];
            for (int i = 0; i < _animSequencesProp.arraySize; i++)
            {
                var entryProp = _animSequencesProp.GetArrayElementAtIndex(i);
                var animTypeProp = entryProp.FindPropertyRelative("animationType");
                var animType = animTypeProp?.objectReferenceValue as AnimationTypeItem;
                
                string name = animType != null ? animType.name : $"(未设置 {i})";
                
                // 统计方向数
                var stripsProp = entryProp.FindPropertyRelative("strips");
                int dirCount = 0;
                if (stripsProp != null)
                {
                    for (int j = 0; j < stripsProp.arraySize; j++)
                    {
                        var framesProp = stripsProp.GetArrayElementAtIndex(j).FindPropertyRelative("frames");
                        if (framesProp != null && framesProp.arraySize > 0)
                            dirCount++;
                    }
                }
                
                names[i] = dirCount > 0 ? $"{name} [{dirCount}向]" : name;
            }

            // 确保索引有效
            if (_selectedAnimIndex >= _animSequencesProp.arraySize)
                _selectedAnimIndex = 0;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("当前动画");
            _selectedAnimIndex = EditorGUILayout.Popup(_selectedAnimIndex, names);
            
            // 删除按钮
            if (GUILayout.Button("删除", GUILayout.Width(50)))
            {
                if (EditorUtility.DisplayDialog("确认删除", $"确定要删除动画 [{names[_selectedAnimIndex]}] 吗？", "删除", "取消"))
                {
                    _animSequencesProp.DeleteArrayElementAtIndex(_selectedAnimIndex);
                    _serializedEquipment.ApplyModifiedProperties();
                    if (_selectedAnimIndex >= _animSequencesProp.arraySize && _selectedAnimIndex > 0)
                        _selectedAnimIndex--;
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 绘制选中动画的序列帧网格
        /// </summary>
        void DrawSelectedAnimationGrid()
        {
            if (_animSequencesProp == null || _selectedAnimIndex >= _animSequencesProp.arraySize)
                return;

            var entryProp = _animSequencesProp.GetArrayElementAtIndex(_selectedAnimIndex);
            
            // 获取实际的 AnimSequenceEntry 对象（用于读取数据）
            AnimSequenceEntry entry = null;
            if (_selectedEquipment != null && 
                _selectedEquipment.animSequences != null && 
                _selectedAnimIndex < _selectedEquipment.animSequences.Count)
            {
                entry = _selectedEquipment.animSequences[_selectedAnimIndex];
            }

            EditorGUILayout.BeginVertical("helpbox");
            EditorGUILayout.LabelField("序列帧网格", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField("左键点击切换深度 | 右键菜单 | 拖拽添加/替换帧", EditorStyles.miniLabel);
            EditorGUILayout.Space(5);
            
            bool changed = AnimSequenceDrawerUtils.DrawDirectionalGridLayout(entry, entryProp);
            
            if (changed)
            {
                _serializedEquipment.ApplyModifiedProperties();
                Repaint();
            }
            
            EditorGUILayout.EndVertical();
        }

        #endregion

        #region 自动生成工具

        void DrawGeneratorTools()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("自动生成工具", EditorStyles.boldLabel);

            // 动画数据库
            EditorGUI.BeginChangeCheck();
            _animDatabase = (AnimationTypeDatabase)EditorGUILayout.ObjectField(
                "动画数据库", _animDatabase, typeof(AnimationTypeDatabase), false);
            if (EditorGUI.EndChangeCheck())
                SaveLastState();

            // 动画类型选择
            if (_animDatabase != null && _animDatabase.Count > 0)
            {
                var types = _animDatabase.ItemsReadOnly.ToArray();
                string[] names = types.Select(t => t != null ? t.name : "(null)").ToArray();

                int currentIndex = Array.IndexOf(types, _selectedAnimType);
                if (currentIndex < 0) currentIndex = 0;

                int newIndex = EditorGUILayout.Popup("动画类型", currentIndex, names);
                if (newIndex >= 0 && newIndex < types.Length)
                    _selectedAnimType = types[newIndex];
            }
            else
            {
                _selectedAnimType = (AnimationTypeItem)EditorGUILayout.ObjectField(
                    "动画类型", _selectedAnimType, typeof(AnimationTypeItem), false);
            }

            // Spritesheet（拖入时自动尝试检测布局）
            EditorGUI.BeginChangeCheck();
            var newSheet = (Texture2D)EditorGUILayout.ObjectField(
                "Spritesheet", _spritesheet, typeof(Texture2D), false);
            if (EditorGUI.EndChangeCheck())
            {
                _spritesheet = newSheet;

                if (_spritesheet != null)
                {
                    var spritesForAuto = EquipmentAnimSequenceTools.GetSpritesFromTexture(_spritesheet);
                    EquipmentAnimSequenceTools.AnalyzeSpriteLayout(spritesForAuto, _spritesheet, out int autoRows, out int autoCols);

                    // 仅在四向模式下自动写入布局，单向模式保持 0 = 平铺
                    if (_directionMode == 4 && autoRows > 0 && autoCols > 0)
                    {
                        _manualRowCount = autoRows;
                        _manualFramesPerRow = autoCols;
                    }
                    else
                    {
                        _manualRowCount = 0;
                        _manualFramesPerRow = 0;
                    }
                }
                else
                {
                    _manualRowCount = 0;
                    _manualFramesPerRow = 0;
                }
            }

            // 显示 Spritesheet 布局信息 + 行/帧数设置
            if (_spritesheet != null)
            {
                var sprites = EquipmentAnimSequenceTools.GetSpritesFromTexture(_spritesheet);
                EquipmentAnimSequenceTools.AnalyzeSpriteLayout(sprites, _spritesheet, out int rows, out int cols);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("行/每行帧");

                _manualRowCount = EditorGUILayout.IntField(_manualRowCount, GUILayout.Width(40));
                GUILayout.Label("×", GUILayout.Width(10));
                _manualFramesPerRow = EditorGUILayout.IntField(_manualFramesPerRow, GUILayout.Width(40));

                GUILayout.Space(4);
                GUILayout.Label($"(自动: {rows}×{cols}，共 {sprites.Count})", EditorStyles.miniLabel);

                if (GUILayout.Button("自动", GUILayout.Width(40)))
                {
                    if (rows > 0 && cols > 0)
                    {
                        _manualRowCount = rows;
                        _manualFramesPerRow = cols;
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            // 方向模式
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("生成模式");
            if (GUILayout.Toggle(_directionMode == 4, "四向", EditorStyles.miniButtonLeft))
                _directionMode = 4;
            if (GUILayout.Toggle(_directionMode == 1, "单向 (SE)", EditorStyles.miniButtonRight))
                _directionMode = 1;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // 操作按钮
            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginDisabledGroup(_selectedAnimType == null);

            if (GUILayout.Button("生成/覆盖动画"))
                DoGenerateAnimation();

            if (GUILayout.Button("添加空动画"))
                DoAddEmptyAnimation();

            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        void DoGenerateAnimation()
        {
            if (_selectedEquipment == null || _selectedAnimType == null)
                return;

            // 检查是否已存在
            bool exists = _selectedEquipment.animSequences != null &&
                          _selectedEquipment.animSequences.Exists(a => a != null && a.animationType == _selectedAnimType);

            if (exists)
            {
                if (!EditorUtility.DisplayDialog(
                    "确认覆盖",
                    $"动画 [{_selectedAnimType.name}] 已存在，是否覆盖？",
                    "覆盖", "取消"))
                {
                    return;
                }
            }

            bool success = EquipmentAnimSequenceTools.AddAnimationFromSpritesheet(
                _selectedEquipment,
                _selectedAnimType,
                _spritesheet,
                _directionMode,
                overwrite: true,
                manualRowCount: _manualRowCount,
                manualFramesPerRow: _manualFramesPerRow);

            if (success)
            {
                SetupSerializedObject(); // 刷新 SerializedObject
                Debug.Log($"已为 [{_selectedEquipment.name}] 生成动画 [{_selectedAnimType.name}]");
            }
        }

        void DoAddEmptyAnimation()
        {
            if (_selectedEquipment == null || _selectedAnimType == null)
                return;

            bool exists = _selectedEquipment.animSequences != null &&
                          _selectedEquipment.animSequences.Exists(a => a != null && a.animationType == _selectedAnimType);

            if (exists)
            {
                EditorUtility.DisplayDialog("错误", $"动画 [{_selectedAnimType.name}] 已存在", "确定");
                return;
            }

            bool success = EquipmentAnimSequenceTools.AddEmptyAnimation(_selectedEquipment, _selectedAnimType);

            if (success)
            {
                SetupSerializedObject();
                Debug.Log($"已为 [{_selectedEquipment.name}] 添加空动画 [{_selectedAnimType.name}]");
            }
        }

        #endregion
    }
}
