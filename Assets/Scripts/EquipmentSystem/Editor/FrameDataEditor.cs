using System.Collections.Generic;
using System.IO;
using System.Linq; 
using UnityEditor;
using UnityEngine;

// 使用别名避免与 UnityEditor.BodyPart 冲突

namespace EquipmentSystem.Editor
{
    /// <summary>
    /// 编辑器标签页模式
    /// </summary>
    public enum TabMode 
    { 
        /// <summary>部位涂色模式</summary>
        BodyPaint, 
        /// <summary>锚点编辑模式</summary>
        Anchor 
    }

    /// <summary>
    /// 帧数据编辑器主窗口
    /// 用于编辑角色动画的帧数据，包括：
    /// - 部位涂色（头部、身体、手脚等）
    /// - UV映射（用于换装系统）
    /// - 锚点设置（武器、装备挂载点）
    /// - 批量操作（自动检测、扩展/收缩、方向生成等）
    /// </summary>
    public class FrameDataEditor : EditorWindow
    {
        #region 字段
        
        // ==================== 数据源 ====================
        /// <summary>当前编辑的帧数据资源</summary>
        [SerializeField] CharacterFrameData _data;
        /// <summary>当前动画的精灵表纹理</summary>
        [SerializeField] Texture2D _sprite;
        
        // ==================== 编辑状态 ====================
        /// <summary>当前选中的动画索引（对应动画数据库）</summary>
        [SerializeField] int _animIndex;
        /// <summary>当前动画名称</summary>
        string _animName = "Idle";
        /// <summary>当前编辑的行（方向）：0=SE, 1=SW, 2=NE, 3=NW</summary>
        int _row;
        /// <summary>当前编辑的帧索引</summary>
        int _frame;
        /// <summary>当前标签页模式</summary>
        TabMode _tab = TabMode.BodyPaint;
        /// <summary>当前选中的身体部位</summary>
        CharacterBodyPart _currentPart = CharacterBodyPart.Torso;
        /// <summary>当前编辑的锚点类型</summary>
        AnchorType _anchorType = AnchorType.MainHandWeapon;
        /// <summary>当前锚点的方向</summary>
        AnchorDirection _anchorDirection;  // 默认 South（枚举值 0）
        bool _showSkinColors;
        bool _showHeadExpandConfig;
        bool _showBodyExpandConfig;
        int _paintDisplayMode = 2;  // 0=隐藏, 1=当前, 2=全部
        
        // ==================== 视图控制 ====================
        /// <summary>工具栏滚动位置</summary>
        Vector2 _scroll;
        /// <summary>画布平移偏移</summary>
        Vector2 _pan;
        /// <summary>画布缩放级别</summary>
        float _zoom = 10f;
        /// <summary>单帧尺寸（像素）</summary>
        Vector2Int _frameSize = new Vector2Int(32, 32);
        /// <summary>每行的帧数</summary>
        int _framesPerRow = 8;
        /// <summary>总行数</summary>
        int _rowCount = 4;
        /// <summary>是否正在平移视图</summary>
        bool _panning;
        /// <summary>上一次的鼠标位置（用于拖动）</summary>
        Vector2 _lastMouse;
        /// <summary>画布区域矩形</summary>
        Rect _canvas;
        /// <summary>显示区域矩形</summary>
        Rect _display;
        /// <summary>画布偏移量</summary>
        Vector2 _canvasOffset;
        
        // ==================== UV 画板 ====================
        /// <summary>UV画板缩放级别</summary>
        float _paletteZoom = 8f;
        /// <summary>UV画板平移偏移</summary>
        Vector2 _palettePan;
        /// <summary>UV画板显示区域矩形</summary>
        Rect _paletteDisplayRect;
        /// <summary>UV画板画布区域矩形（用于输入检测）</summary>
        Rect _paletteCanvasRect;
        /// <summary>是否显示UV画板</summary>
        bool _showPalette = true;
        
        // ==================== 选区和编辑 ====================
        /// <summary>是否正在框选</summary>
        bool _isSelecting;
        /// <summary>选区位置：true=画板上，false=画布上</summary>
        bool _selectOnPalette;
        /// <summary>是否正在擦除模式</summary>
        bool _isErasing;
        /// <summary>是否正在右键拖动删除</summary>
        bool _isRightDragging;
        /// <summary>选区起始点（像素坐标）</summary>
        Vector2Int _selectionStart;
        /// <summary>选区结束点（像素坐标）</summary>
        Vector2Int _selectionEnd;
        /// <summary>UV画板上的确定选区</summary>
        RectInt _paletteSelection;
        /// <summary>画布上的确定选区</summary>
        RectInt _canvasSelection;
        
        // ==================== 编辑模式和显示选项 ====================
        /// <summary>是否启用编辑模式（允许修改涂色）</summary>
        bool _editMode;
        /// <summary>是否展开UV画板配置面板</summary>
        bool _showPaletteConfig;
        /// <summary>是否隐藏画布上的角色原图</summary>
        bool _hideCanvasSprite;
        /// <summary>是否隐藏UV画板的参考底图</summary>
        bool _hidePaletteSprite;
        /// <summary>当前鼠标悬停的画板像素坐标</summary>
        Vector2Int? _hoverPalettePixel;
        /// <summary>当前鼠标悬停的画布像素坐标</summary>
        Vector2Int? _hoverCanvasPixel;
        
        // ==================== 编辑缓存 ====================
        /// <summary>各部位的像素集合（记录涂色位置）</summary>
        Dictionary<CharacterBodyPart, HashSet<Vector2Int>> _partPixels = new Dictionary<CharacterBodyPart, HashSet<Vector2Int>>();
        /// <summary>各部位的核心像素集合（扩展前/手动涂色的真实区域）</summary>
        Dictionary<CharacterBodyPart, HashSet<Vector2Int>> _corePartPixels = new Dictionary<CharacterBodyPart, HashSet<Vector2Int>>();
        /// <summary>各部位的UV映射（像素位置 -> UV坐标）</summary>
        Dictionary<CharacterBodyPart, Dictionary<Vector2Int, Vector2>> _partUVs = new Dictionary<CharacterBodyPart, Dictionary<Vector2Int, Vector2>>();
        /// <summary>各部位的贴图朝向</summary>
        Dictionary<CharacterBodyPart, CharacterFacing> _partSpriteFacings = new Dictionary<CharacterBodyPart, CharacterFacing>();
        /// <summary>各部位的贴图变体（基础/向上/向下）</summary>
        Dictionary<CharacterBodyPart, FrameVariant> _partVariants = new Dictionary<CharacterBodyPart, FrameVariant>();
        /// <summary>当前帧的锚点列表</summary>
        List<AnchorPoint> _anchors = new List<AnchorPoint>();
        bool _leftEyeClosed;
        bool _rightEyeClosed;
        bool _hitOutlineFrame;
        Vector2Int _sequenceOffset;
        bool _showAnimConfig;
        
        /// <summary>数据脏标记 - 有修改时自动保存</summary>
        bool _isDirty;
        
        #endregion
        
        #region 初始化
        
        [MenuItem("Tools/Equipment System/Frame Editor")]
        public static void ShowWindow() => GetWindow<FrameDataEditor>("帧数据编辑器").minSize = new Vector2(900, 700);
        
        const string PREF_LAST_DATA_PATH = "FrameDataEditor_LastDataPath";
        const string PREF_LAST_ANIM_INDEX = "FrameDataEditor_LastAnimIndex";
        const string PREF_LAST_FRAME = "FrameDataEditor_LastFrame";
        const string PREF_LAST_ROW = "FrameDataEditor_LastRow";
        
        void OnEnable()
        {
            wantsMouseMove = true;
            Undo.undoRedoPerformed += OnUndoRedo;
            
            // 尝试恢复上次选中的数据
            if (_data == null)
                RestoreLastData();
            
            // 如果还是没有数据，自动查找第一个
            if (_data == null)
                AutoSelectFirstFrameData();
            else
            {
                // 脚本编译后重新加载当前帧数据（保持当前动画选择）
                var db = _data.animDatabase;
                if (db != null && _animIndex >= 0 && _animIndex < db.Count)
                {
                    var animType = db[_animIndex];
                    var anim = _data.GetAnimation(animType);
                    if (anim != null)
                        SyncFromAnimation(anim);
                }
                LoadFrameData();
            }
        }
        
        void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            SaveIfDirty();
            SaveLastData();
        }
        
        void SaveLastData()
        {
            if (_data != null)
            {
                var path = AssetDatabase.GetAssetPath(_data);
                EditorPrefs.SetString(PREF_LAST_DATA_PATH, path);
                EditorPrefs.SetInt(PREF_LAST_ANIM_INDEX, _animIndex);
                EditorPrefs.SetInt(PREF_LAST_FRAME, _frame);
                EditorPrefs.SetInt(PREF_LAST_ROW, _row);
            }
        }
        
        void RestoreLastData()
        {
            var path = EditorPrefs.GetString(PREF_LAST_DATA_PATH, "");
            if (!string.IsNullOrEmpty(path))
            {
                _data = AssetDatabase.LoadAssetAtPath<CharacterFrameData>(path);
                if (_data != null)
                {
                    _animIndex = EditorPrefs.GetInt(PREF_LAST_ANIM_INDEX, 0);
                    _frame = EditorPrefs.GetInt(PREF_LAST_FRAME, 0);
                    _row = EditorPrefs.GetInt(PREF_LAST_ROW, 0);
                    SyncFromDataKeepIndex();
                }
            }
        }
        
        /// <summary>
        /// 自动选中项目中第一个 CharacterFrameData 资源
        /// </summary>
        void AutoSelectFirstFrameData()
        {
            string[] guids = AssetDatabase.FindAssets("t:CharacterFrameData");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                _data = AssetDatabase.LoadAssetAtPath<CharacterFrameData>(path);
                if (_data != null)
                {
                    SyncFromData();
                    Debug.Log($"[FrameDataEditor] 自动加载: {path}");
                }
            }
        }
        
        void OnLostFocus() => SaveIfDirty();
        
        void OnUndoRedo()
        {
            // 撤销/重做后重新加载当前帧数据
            _isDirty = false;
            LoadFrameData();
        }
        
        #endregion
        
        #region 主绘制
        
        void OnGUI()
        {
            float toolbarWidth = Mathf.Clamp(position.width * 0.35f, 360f, 520f);
            float rightWidth = position.width - toolbarWidth;
            
            // 左侧工具栏
            GUILayout.BeginArea(new Rect(0, 0, toolbarWidth, position.height));
            DrawToolbar();
            GUILayout.EndArea();
            
            // 右侧区域：上面画板、下面画布
            float paletteHeight = _showPalette ? position.height * 0.4f : 0;
            float canvasHeight = position.height - paletteHeight;
            
            // 画板区域
            if (_showPalette)
            {
                _paletteCanvasRect = new Rect(toolbarWidth, 0, rightWidth, paletteHeight);
                GUILayout.BeginArea(_paletteCanvasRect);
                DrawPalette();
                GUILayout.EndArea();
            }
            
            // 画布区域
            _canvasOffset = new Vector2(toolbarWidth, paletteHeight);
            GUILayout.BeginArea(new Rect(toolbarWidth, paletteHeight, rightWidth, canvasHeight));
            DrawCanvas();
            GUILayout.EndArea();
            
            HandleInput();
        }
        
        void DrawToolbar()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            
            DrawDataSection();
            DrawConfigSection();
            DrawAnimationSection();
            DrawFrameSelection();
            DrawTabContent();
            DrawHelpInfo();
            
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }
        
        void DrawDataSection()
        {
            GUILayout.Space(10);
            GUILayout.Label("数据", EditorStyles.boldLabel);
            
            EditorGUI.BeginChangeCheck();
            var newData = (CharacterFrameData)EditorGUILayout.ObjectField("帧数据", _data, typeof(CharacterFrameData), false);
            if (EditorGUI.EndChangeCheck() && newData != _data)
            {
                SaveIfDirty();
                _data = newData;
                if (_data != null)
                    SyncFromData();
            }
        }
        
        void DrawConfigSection()
        {
            if (_data == null || _data.animDatabase == null || _animIndex < 0 || _animIndex >= _data.animDatabase.Count) return;
            
            var currentType = _data.animDatabase[_animIndex];
            var anim = _data.GetAnimation(currentType);
            if (anim == null) return;
            
            GUILayout.Space(10);
            GUILayout.Label("当前动画配置", EditorStyles.boldLabel);
            
            // Spritesheet - 拖入时自动检测帧尺寸
            var oldSpritesheet = anim.spritesheet;
            EditorGUI.BeginChangeCheck();
            anim.spritesheet = (Texture2D)EditorGUILayout.ObjectField("Spritesheet", anim.spritesheet, typeof(Texture2D), false);
            bool spritesheetChanged = EditorGUI.EndChangeCheck() && anim.spritesheet != oldSpritesheet && anim.spritesheet != null;
            
            EditorGUI.BeginChangeCheck();
            
            // 帧配置
            EditorGUILayout.BeginHorizontal();
            anim.frameSize = EditorGUILayout.Vector2IntField("帧尺寸", anim.frameSize);
            if (GUILayout.Button("自动", GUILayout.Width(40)) || spritesheetChanged)
                AutoDetectFrameConfig();
            EditorGUILayout.EndHorizontal();
            
            anim.framesPerRow = EditorGUILayout.IntField("每行帧数", anim.framesPerRow);
            anim.rowCount = EditorGUILayout.IntField("行数", anim.rowCount);
            
            if (EditorGUI.EndChangeCheck())
            {
                SyncFromAnimation(anim);
                EditorUtility.SetDirty(_data);
            }
        }
        
        void DrawAnimationSection()
        {
            GUILayout.Space(10);
            GUILayout.Label("动画", EditorStyles.boldLabel);
            
            if (_data == null) return;
            
            // 动画类型数据库（存在 _data 中）
            EditorGUI.BeginChangeCheck();
            _data.animDatabase = (AnimationTypeDatabase)EditorGUILayout.ObjectField(
                "动画数据库", _data.animDatabase, typeof(AnimationTypeDatabase), false);
            if (EditorGUI.EndChangeCheck())
            {
                // 数据库引用变更时，先保存当前修改并重置为新数据库的第一个动画
                SaveIfDirty();
                EditorUtility.SetDirty(_data);
                _animIndex = 0;
                SyncFromData();
            }
            
            if (_data.animDatabase == null || _data.animDatabase.Count == 0)
            {
                EditorGUILayout.HelpBox("请指定动画数据库", MessageType.Info);
                return;
            }
            
            // 动画类型下拉框 - 直接从数据库选择
            var dbNames = _data.animDatabase.GetAllDisplayNames();
            EditorGUI.BeginChangeCheck();
            _animIndex = Mathf.Clamp(_animIndex, 0, _data.animDatabase.Count - 1);
            _animIndex = EditorGUILayout.Popup("动画类型", _animIndex, dbNames);
            if (EditorGUI.EndChangeCheck())
            {
                // 先保存当前修改
                SaveIfDirty();
                
                var selectedType = _data.animDatabase[_animIndex];
                _animName = selectedType.name;
                
                // 查找或创建对应的动画数据
                var anim = _data.GetAnimation(selectedType);
                if (anim == null)
                {
                    anim = _data.GetOrCreateAnimation(selectedType);
                    EditorUtility.SetDirty(_data);
                }
                
                // 切换动画时重置帧位置
                _frame = 0;
                _row = 0;
                SyncFromAnimation(anim);
                LoadFrameData();
            }
            
            // 当前动画配置
            var currentType = _data.animDatabase[_animIndex];
            var currentAnim = _data.GetAnimation(currentType);
        }
        
        void DrawFrameSelection()
        {
            GUILayout.Space(5);
            _showAnimConfig = EditorGUILayout.Foldout(_showAnimConfig, "动画配置", true);
            if (_showAnimConfig)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(4);
                bool newHitOutline = EditorGUILayout.Toggle("受击描边帧", _hitOutlineFrame);
                if (newHitOutline != _hitOutlineFrame)
                {
                    _hitOutlineFrame = newHitOutline;
                    SaveWithUndo("设置受击描边帧");
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(4);
                EditorGUILayout.LabelField("武器序列帧偏移 (像素)", GUILayout.Width(130));
                EditorGUI.BeginChangeCheck();
                var newOffset = EditorGUILayout.Vector2IntField(GUIContent.none, _sequenceOffset);
                if (EditorGUI.EndChangeCheck())
                {
                    _sequenceOffset = newOffset;
                    SaveWithUndo("设置武器序列帧偏移");
                }
                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel--;
            }

            GUILayout.Space(5);
            GUILayout.Label("帧选择", EditorStyles.boldLabel);
            
            // 行选择 - 方向快捷按钮 + 数字输入
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("行", GUILayout.Width(20));
            string[] dirLabels = { "SE", "SW", "NE", "NW" };
            for (int i = 0; i < 4; i++)
            {
                GUI.backgroundColor = (_row == i) ? Color.yellow : Color.white;
                if (GUILayout.Button(dirLabels[i], GUILayout.Width(32)))
                    SwitchRow(i);
            }
            GUI.backgroundColor = Color.white;
            
            // 数字输入框
            GUILayout.Space(5);
            EditorGUI.BeginChangeCheck();
            int maxRow = Mathf.Max(1, _rowCount) - 1;
            int newRow = EditorGUILayout.IntField(_row, GUILayout.Width(30));
            if (EditorGUI.EndChangeCheck())
                SwitchRow(Mathf.Clamp(newRow, 0, maxRow));
            GUILayout.Label($"/{maxRow}", GUILayout.Width(25));
            EditorGUILayout.EndHorizontal();
            
            // 帧选择
            EditorGUILayout.BeginHorizontal();
            int maxFrame = Mathf.Max(1, _framesPerRow) - 1;
            if (GUILayout.Button("◀", GUILayout.Width(30)))
                SwitchFrame(Mathf.Max(0, _frame - 1));
            EditorGUI.BeginChangeCheck();
            int newFrame = EditorGUILayout.IntSlider(_frame, 0, maxFrame);
            if (EditorGUI.EndChangeCheck() && newFrame != _frame)
                SwitchFrame(newFrame);
            if (GUILayout.Button("▶", GUILayout.Width(30)))
                SwitchFrame(Mathf.Min(maxFrame, _frame + 1));
            EditorGUILayout.EndHorizontal();
            // 显示选项
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(4);
            _hideCanvasSprite = GUILayout.Toggle(_hideCanvasSprite, "隐藏角色", GUILayout.Width(70));
            _hidePaletteSprite = GUILayout.Toggle(_hidePaletteSprite, "隐藏底图", GUILayout.Width(70));
            EditorGUILayout.EndHorizontal();
        }
        
        void DrawTabContent()
        {
            GUILayout.Space(10);
            EditorGUI.BeginChangeCheck();
            _tab = (TabMode)GUILayout.Toolbar((int)_tab, new string[] { "部位上色", "锚点" });
            if (EditorGUI.EndChangeCheck())
                SaveIfDirty();
            
            EditorGUILayout.BeginVertical("box");
            switch (_tab)
            {
                case TabMode.BodyPaint: DrawBodyPaintTab(); break;
                case TabMode.Anchor: DrawAnchorTab(); break;
            }
            EditorGUILayout.EndVertical();
            
            if (_tab == TabMode.BodyPaint)
            {
                DrawBatchOperations();
            }
        }
        
        void DrawHelpInfo()
        {
            GUILayout.Space(10);
            EditorGUILayout.HelpBox(
                "涂色操作:\n" +
                "• 左键: 涂色（使用当前 UV）\n" +
                "• 右键: 擦除\n" +
                "• Shift+左键拖拽: 移除区域\n" +
                "• ESC: 取消选区\n\n" +
                "视图操作:\n" +
                "• 中键拖动: 平移\n" +
                "• 滚轮: 缩放\n" +
                "• 快捷键 1/2: 切换标签页",
                MessageType.Info);
        }
        
        #endregion
        
        #region 标签页内容
        
        void DrawBodyPaintTab()
        {
            // === 选择部位 ===
            GUILayout.Label("选择部位", EditorStyles.boldLabel);
            
            // 头部/身体 - 需要UV映射
            EditorGUILayout.BeginHorizontal();
            DrawPartButton(CharacterBodyPart.Head, "头部", new Color(0.2f, 0.9f, 0.2f));
            DrawPartButton(CharacterBodyPart.Torso, "身体", new Color(0.2f, 0.7f, 0.2f));
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
            
            // 手脚 - 单像素
            EditorGUILayout.BeginHorizontal();
            DrawPartButton(CharacterBodyPart.LeftHand, "左手", new Color(0.9f, 0.8f, 0.2f));
            DrawPartButton(CharacterBodyPart.RightHand, "右手", new Color(0.9f, 0.5f, 0.2f));
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            DrawPartButton(CharacterBodyPart.LeftFoot, "左脚", new Color(0.2f, 0.7f, 0.9f));
            DrawPartButton(CharacterBodyPart.RightFoot, "右脚", new Color(0.9f, 0.3f, 0.6f));
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
            
            // 眼睛
            EditorGUILayout.BeginHorizontal();
            DrawPartButton(CharacterBodyPart.LeftEye, "左眼", new Color(0.6f, 0.2f, 0.8f));
            DrawPartButton(CharacterBodyPart.RightEye, "右眼", new Color(0.8f, 0.3f, 0.6f));
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
            
            // === 涂色显示 ===
            GUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("涂色:", GUILayout.Width(35));
            _paintDisplayMode = GUILayout.Toolbar(_paintDisplayMode, new[] { "隐藏", "当前", "全部" });
            EditorGUILayout.EndHorizontal();
            
            // 当前部位信息（大字体 + 部位颜色，提亮显示）
            int count = _partPixels.ContainsKey(_currentPart) ? _partPixels[_currentPart].Count : 0;
            var partColor = GetPartColor(_currentPart);
            // 提亮颜色：将颜色向白色混合，使其更明亮
            var brightColor = Color.Lerp(partColor, Color.white, 0.4f);
            var prevColor = GUI.contentColor;
            GUI.contentColor = brightColor;
            GUILayout.Label($"当前: {GetPartName(_currentPart)} ({count}像素)", new GUIStyle(EditorStyles.boldLabel) { fontSize = 16 });
            GUI.contentColor = prevColor;
            
            // 实际方向与变体设置（只对 UV 部位：头/身体）
            if (!IsLimbPart(_currentPart))
            {
                // 确保有默认值
                if (!_partSpriteFacings.ContainsKey(_currentPart))
                    _partSpriteFacings[_currentPart] = GetDefaultSpriteFacing();
                
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("实际方向:", GUILayout.Width(60));
                var newFacing = (CharacterFacing)EditorGUILayout.EnumPopup(_partSpriteFacings[_currentPart]);
                if (newFacing != _partSpriteFacings[_currentPart])
                {
                    _partSpriteFacings[_currentPart] = newFacing;
                    SaveWithUndo("设置实际方向");
                }
                EditorGUILayout.EndHorizontal();

                // 贴图变体（基础/向上/向下）
                if (!_partVariants.ContainsKey(_currentPart))
                    _partVariants[_currentPart] = FrameVariant.Base;

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("贴图变体:", GUILayout.Width(60));
                var currentVariant = _partVariants[_currentPart];
                var newVariant = (FrameVariant)EditorGUILayout.EnumPopup(currentVariant);
                EditorGUILayout.EndHorizontal();

                if (newVariant != currentVariant)
                {
                    _partVariants[_currentPart] = newVariant;
                    SaveWithUndo("设置贴图变体");
                }
            }
            
            // === 编辑模式 ===
            GUILayout.Space(10);
            EditorGUILayout.BeginVertical("helpbox");
            
            EditorGUI.BeginChangeCheck();
            _editMode = EditorGUILayout.Toggle("✏️ 编辑模式", _editMode);
            if (EditorGUI.EndChangeCheck() && _editMode)
            {
                _paletteSelection = default;
                _canvasSelection = default;
            }
            
            if (_editMode)
            {
                // 清除当前部位
                GUILayout.Space(5);
                if (GUILayout.Button("清除当前部位"))
                {
                    if (_partPixels.ContainsKey(_currentPart))
                        _partPixels[_currentPart].Clear();
                    if (_partUVs.ContainsKey(_currentPart))
                        _partUVs[_currentPart].Clear();
                    if (_corePartPixels.ContainsKey(_currentPart))
                        _corePartPixels[_currentPart].Clear();
                    if (_partVariants.ContainsKey(_currentPart))
                        _partVariants[_currentPart] = FrameVariant.Base;
                    SaveWithUndo("清除部位");
                }
                
                // 眼睛闭眼状态
                if (_currentPart == CharacterBodyPart.LeftEye || _currentPart == CharacterBodyPart.RightEye)
                {
                    bool hasPixels = _partPixels.ContainsKey(_currentPart) && _partPixels[_currentPart].Count > 0;
                    EditorGUI.BeginDisabledGroup(!hasPixels);
                    bool closed = _currentPart == CharacterBodyPart.LeftEye ? _leftEyeClosed : _rightEyeClosed;
                    bool newClosed = EditorGUILayout.Toggle(
                        _currentPart == CharacterBodyPart.LeftEye ? "左眼闭眼" : "右眼闭眼",
                        closed);
                    if (newClosed != closed)
                    {
                        if (_currentPart == CharacterBodyPart.LeftEye)
                            _leftEyeClosed = newClosed;
                        else
                            _rightEyeClosed = newClosed;
                        SaveWithUndo("设置闭眼状态");
                    }
                    EditorGUI.EndDisabledGroup();
                }

                // UV镜像（仅头部/身体有UV）
                bool isLimb = IsLimbPart(_currentPart);
                if (!isLimb && _partUVs.ContainsKey(_currentPart) && _partUVs[_currentPart].Count > 0)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("UV镜像:", GUILayout.Width(50));
                    if (GUILayout.Button("水平")) MirrorCurrentPartUV(true);
                    if (GUILayout.Button("垂直")) MirrorCurrentPartUV(false);
                    if (GUILayout.Button("左旋90°")) RotateCurrentPartUV(false);
                    if (GUILayout.Button("右旋90°")) RotateCurrentPartUV(true);
                    EditorGUILayout.EndHorizontal();
                }
                
                // 操作提示
                GUILayout.Space(5);
                string tips = isLimb
                    ? "【手脚】点击画布设置，Shift+点击清除"
                    : "【头部/身体】画板框选UV→画布框选复制，Shift+拖拽擦除";
                EditorGUILayout.HelpBox(tips, MessageType.Info);
            }
            
            EditorGUILayout.EndVertical();
            
            // === 区域扩展 ===
            GUILayout.Space(5);
            GUILayout.Label("区域扩展", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("基于边界像素的 UV 向外扩展\n先上下、再左右，扩展像素继承边界 UV", MessageType.Info);
            
            // 头部扩展配置
            _showHeadExpandConfig = EditorGUILayout.Foldout(_showHeadExpandConfig, "头部扩展配置", true);
            if (_showHeadExpandConfig && _data != null)
            {
                EditorGUI.indentLevel++;
                _data.headExpandUp = EditorGUILayout.IntSlider("向上扩展", _data.headExpandUp, 0, 20);
                _data.headExpandSide = EditorGUILayout.IntSlider("左右扩展", _data.headExpandSide, 0, 20);
                _data.headExpandDown = EditorGUILayout.IntSlider("向下扩展", _data.headExpandDown, 0, 20);
                EditorGUI.indentLevel--;
            }
            
            // 身体扩展配置
            _showBodyExpandConfig = EditorGUILayout.Foldout(_showBodyExpandConfig, "身体扩展配置", true);
            if (_showBodyExpandConfig && _data != null)
            {
                EditorGUI.indentLevel++;
                _data.bodyExpandUp = EditorGUILayout.IntSlider("向上扩展", _data.bodyExpandUp, 0, 20);
                int upStart = _data.bodyExpandUpStartStep <= 0 ? 1 : _data.bodyExpandUpStartStep;
                _data.bodyExpandUpStartStep = EditorGUILayout.IntSlider("上扩起始步长", upStart, 1, 20);
                _data.bodyExpandSide = EditorGUILayout.IntSlider("左右扩展", _data.bodyExpandSide, 0, 20);
                _data.bodyExpandDown = EditorGUILayout.IntSlider("向下扩展", _data.bodyExpandDown, 0, 20);
                int downStart = _data.bodyExpandDownStartStep <= 0 ? 1 : _data.bodyExpandDownStartStep;
                _data.bodyExpandDownStartStep = EditorGUILayout.IntSlider("下扩起始步长", downStart, 1, 20);
                EditorGUI.indentLevel--;
            }

            // 扩展姿态（站立/向左躺/向右躺）
            if (_data != null)
            {
                _data.regionExpandPose = (RegionExpandPose)EditorGUILayout.EnumPopup("扩展姿态", _data.regionExpandPose);
            }
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🔄 扩展（当前帧）"))
                ExpandAllPartsRegion();
            if (GUILayout.Button("🔄 扩展（全部帧）"))
                ExpandAllPartsForAllFrames();
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("⬅️ 收缩（当前帧）"))
                ShrinkAllPartsRegion();
            if (GUILayout.Button("⬅️ 收缩（全部帧）"))
                ShrinkAllPartsForAllFrames();
            EditorGUILayout.EndHorizontal();
            
            // === 自动涂色 ===
            GUILayout.Space(10);
            GUILayout.Label("自动涂色", EditorStyles.boldLabel);
            
            if (GUILayout.Button("🎨 自动涂色（当前帧）", GUILayout.Height(25)))
                AutoPaintCurrentPart();
            if (GUILayout.Button("🎨 自动涂色 + 挂点（当前帧）", GUILayout.Height(25)))
                AutoPaintAllWithAnchors();
            if (GUILayout.Button("🎨 自动涂色（全部帧）", GUILayout.Height(25)))
                AutoPaintAllFramesWithoutAnchors();
            if (GUILayout.Button("🎨 自动涂色 + 挂点（全部帧）", GUILayout.Height(25)))
                AutoPaintAllFrames();
            
            GUILayout.Space(3);
            GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
            
            // 涂色清除
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("清除当前帧涂色"))
            {
                _partPixels.Clear();
                _partUVs.Clear();
                _corePartPixels.Clear();
                SaveWithUndo("清除当前帧涂色");
            }
            if (GUILayout.Button("清除全部帧涂色"))
            {
                ClearAllFramesPaint();
            }
            EditorGUILayout.EndHorizontal();
            
            // 挂点清除
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("清除当前帧挂点"))
            {
                _anchors.Clear();
                SaveWithUndo("清除当前帧挂点");
            }
            if (GUILayout.Button("清除全部帧挂点"))
            {
                ClearAllFramesAnchors();
            }
            EditorGUILayout.EndHorizontal();
            
            // 方向和贴图变体清除
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("清除当前帧方向和变体"))
            {
                _partSpriteFacings.Clear();
                _partVariants.Clear();
                SaveWithUndo("清除当前帧方向和变体");
            }
            if (GUILayout.Button("清除全部帧方向和变体"))
            {
                ClearAllFramesFacingAndVariant();
            }
            EditorGUILayout.EndHorizontal();
            
            GUI.backgroundColor = Color.white;
            
            // === 检测配置 ===
            DrawDetectConfig();
            
            // === UV 画板 ===
            GUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            _showPalette = EditorGUILayout.Toggle(_showPalette, GUILayout.Width(15));
            GUILayout.Label("显示 UV 画板", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();
            
            // === UV 画板配置（可折叠）===
            _showPaletteConfig = EditorGUILayout.Foldout(_showPaletteConfig, "UV 画板配置", true);
            if (_showPaletteConfig && _data != null)
            {
                EditorGUI.indentLevel++;
                EditorGUI.BeginChangeCheck();
                
                _data.paletteSize = EditorGUILayout.Vector2IntField("画板尺寸", _data.paletteSize);
                _data.groundPixelY = EditorGUILayout.IntField("阴影地面Y (像素)", _data.groundPixelY);
                 _data.paletteRefSprite = (Sprite)EditorGUILayout.ObjectField("参考底图", _data.paletteRefSprite, typeof(Sprite), false);
                
                if (_data.paletteRefSprite != null)
                {
                    var spriteSize = new Vector2Int((int)_data.paletteRefSprite.rect.width, (int)_data.paletteRefSprite.rect.height);
                    if (spriteSize != _data.paletteSize)
                    {
                        if (GUILayout.Button($"同步尺寸为 {spriteSize.x}×{spriteSize.y}"))
                            _data.paletteSize = spriteSize;
                    }
                }
                
                // UV源区域（画板上的UV区域）
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("UV源区域（画板）", EditorStyles.miniBoldLabel);
                _data.headUVRegion = EditorGUILayout.RectIntField("头部UV", _data.headUVRegion);
                _data.torsoUVRegion = EditorGUILayout.RectIntField("身体UV", _data.torsoUVRegion);
                
                // 检测目标区域大小（角色实际区域）
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("检测目标区域（角色）", EditorStyles.miniBoldLabel);
                _data.headDetectSize = EditorGUILayout.Vector2IntField("头部大小", _data.headDetectSize);
                _data.torsoDetectSize = EditorGUILayout.Vector2IntField("身体大小", _data.torsoDetectSize);
                
                if (EditorGUI.EndChangeCheck())
                    EditorUtility.SetDirty(_data);
                
                EditorGUILayout.HelpBox("检测区域 > UV区域时，边缘UV会自动复制填充\n左手在 UV 画板上的基准像素固定为 (15,16)，武器贴图绘制时请将握柄对齐到该像素（旋转仍以上下文默认的贴图中心为锚点）", MessageType.Info);
                EditorGUI.indentLevel--;
            }
        }
        
        void DrawAnchorTab()
        {
            GUILayout.Label("锚点设置", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("用于武器定位：主手/副手武器锚点", MessageType.Info);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("锚点类型", GUILayout.Width(70));
            var anchorTypes = (AnchorType[])System.Enum.GetValues(typeof(AnchorType));
            string[] anchorTypeNames = anchorTypes.Select(t => t.ToString()).ToArray();
            int currentIndex = System.Array.IndexOf(anchorTypes, _anchorType);
            int newIndex = GUILayout.Toolbar(currentIndex, anchorTypeNames);
            if (newIndex >= 0 && newIndex < anchorTypes.Length && newIndex != currentIndex)
            {
                _anchorType = anchorTypes[newIndex];
                var selected = _anchors.Find(a => a.type == _anchorType);
                if (selected != null)
                    _anchorDirection = selected.direction;
            }
            EditorGUILayout.EndHorizontal();

            // 修改武器方向时，立即同步到当前帧中对应类型的锚点
            EditorGUI.BeginChangeCheck();
            var newDirection = (AnchorDirection)EditorGUILayout.EnumPopup("武器方向", _anchorDirection);
            if (EditorGUI.EndChangeCheck())
            {
                _anchorDirection = newDirection;
                var existing = _anchors.Find(a => a.type == _anchorType);
                if (existing != null)
                {
                    existing.direction = newDirection;
                    SaveWithUndo("修改锚点方向");
                }
                Repaint();
            }
            
            GUILayout.Space(5);
            GUILayout.Label("已有锚点:", EditorStyles.miniLabel);
            
            for (int i = _anchors.Count - 1; i >= 0; i--)
            {
                var a = _anchors[i];
                EditorGUILayout.BeginHorizontal();
                GUI.color = a.type == _anchorType ? Color.yellow : Color.white;
                if (GUILayout.Button(a.type.ToString(), EditorStyles.miniButtonLeft, GUILayout.Width(120)))
                {
                    _anchorType = a.type;
                    _anchorDirection = a.direction;
                }
                GUI.color = Color.white;
                GUILayout.Label($"({a.position.x},{a.position.y})", GUILayout.Width(55));
                
                if (GUILayout.Button("×", GUILayout.Width(20)))
                {
                    _anchors.RemoveAt(i);
                    SaveWithUndo("删除锚点");
                }
                EditorGUILayout.EndHorizontal();
            }
        }
        
        void DrawDetectConfig()
        {
            GUILayout.Space(10);
            _showSkinColors = EditorGUILayout.Foldout(_showSkinColors, "检测配置", true);
            if (_showSkinColors && _data != null)
            {
                EditorGUI.indentLevel++;
                var c = _data.detectConfig;
                
                GUILayout.Label("阈值参数:", EditorStyles.miniLabel);
                c.outlineThreshold = EditorGUILayout.IntSlider("描边阈值", c.outlineThreshold, 0, 100);
                c.limbColorThreshold = EditorGUILayout.IntSlider("手脚色容差", c.limbColorThreshold, 0, 100);
                c.closedEyeColorThreshold = EditorGUILayout.IntSlider("闭眼色容差", c.closedEyeColorThreshold, 0, 100);
                
                GUILayout.Label("手脚颜色 (用于自动检测):", EditorStyles.miniLabel);
                c.leftHandColor = EditorGUILayout.ColorField("左手", c.leftHandColor);
                c.rightHandColor = EditorGUILayout.ColorField("右手", c.rightHandColor);
                c.leftFootColor = EditorGUILayout.ColorField("左脚", c.leftFootColor);
                c.rightFootColor = EditorGUILayout.ColorField("右脚", c.rightFootColor);
                c.closedEyeColor = EditorGUILayout.ColorField("闭眼颜色", c.closedEyeColor);
                
                if (GUILayout.Button("重置为默认值"))
                {
                    _data.detectConfig = new DetectConfig();
                    EditorUtility.SetDirty(_data);
                }
                EditorGUI.indentLevel--;
            }
        }
        
        void DrawBatchOperations()
        {
            GUILayout.Space(10);
            GUILayout.Label("批量操作", EditorStyles.boldLabel);
            if (GUILayout.Button("🔧 修复所有帧实际方向"))
                FixAllFramesSpriteFacing();
            
            GUILayout.Space(5);
            GUILayout.Label("方向数据生成", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("从SE行数据生成其他行：\n• SW/NW = SE水平镜像（左右互换、实际方向镜像）\n• NE = SE复制", MessageType.Info);
            
            if (GUILayout.Button("📋 从SE生成所有行数据"))
                GenerateAllRowsFromSE();
            
            GUILayout.Space(5);
            GUILayout.Label("GPU 换装", EditorStyles.boldLabel);
            
            // 显示当前 UV Map 状态（双层）
            var uvAnim = GetCurrentAnimation();
            if (uvAnim != null)
            {
                var anim = uvAnim;
                bool hasBodyUV = anim.bodyUVMap != null;
                bool hasHeadUV = anim.headUVMap != null;
                GUI.color = (hasBodyUV && hasHeadUV) ? Color.green : Color.yellow;
                string status = hasBodyUV && hasHeadUV ? "✓ 双层 UV Map 已设置" :
                               hasBodyUV ? "○ 仅身体层" :
                               hasHeadUV ? "○ 仅头部层" : "✗ 未设置 UV Map";
                EditorGUILayout.LabelField(status);
                GUI.color = Color.white;
            }
            
            if (GUILayout.Button("💾 生成当前动画 UV Map (双层)"))
                GenerateDualUVMapsForCurrentAnimation();
            
            if (GUILayout.Button("💾 生成所有动画 UV Map (双层)"))
                GenerateAllDualUVMaps();
            
            GUILayout.Space(10);
            EditorGUILayout.HelpBox(
                "⚠️ 重要提示：\n" +
                "角色 Spritesheet 必须设置 Mesh Type = Full Rect，\n" +
                "否则扩展区域（如头盔顶部）的装备无法显示。", 
                MessageType.Warning);
        }
        
        void DrawPartButton(CharacterBodyPart part, string label, Color color)
        {
            // 选中时灰色，非选中时显示部位颜色
            GUI.backgroundColor = _currentPart == part ? Color.gray : color;
            if (GUILayout.Button(label)) _currentPart = part;
        }
        
        #endregion
        
        #region 画布绘制
        
        /// <summary>
        /// 绘制 UV 画板区域
        /// </summary>
        void DrawPalette()
        {
            Rect paletteCanvas = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, 
                GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            
            EditorGUI.DrawRect(paletteCanvas, new Color(0.12f, 0.12f, 0.18f));
            
            int palW = _data != null ? _data.paletteSize.x : 32;
            int palH = _data != null ? _data.paletteSize.y : 32;
            
            float w = palW * _paletteZoom;
            float h = palH * _paletteZoom;
            var center = paletteCanvas.center + _palettePan;
            _paletteDisplayRect = new Rect(center.x - w/2, center.y - h/2, w, h);
            
            // 1. 先绘制参考底图（在最底层）
            var refSprite = _data?.paletteRefSprite;
            if (!_hidePaletteSprite && refSprite != null && refSprite.texture != null)
            {
                var spriteRect = refSprite.rect;
                var tex = refSprite.texture;
                Rect uv = new Rect(
                    spriteRect.x / tex.width,
                    spriteRect.y / tex.height,
                    spriteRect.width / tex.width,
                    spriteRect.height / tex.height
                );
                GUI.DrawTextureWithTexCoords(_paletteDisplayRect, tex, uv);
            }
            
            // 2. 再绘制 UV 颜色（隐藏底图时不透明，否则半透明叠加）
            float uvAlpha = _hidePaletteSprite ? 1f : 0.4f;
            for (int y = 0; y < palH; y++)
            {
                for (int x = 0; x < palW; x++)
                {
                    // UV 颜色：R=U, G=V（使用像素中心，与生成UV一致）
                    float u = (x + 0.5f) / palW;
                    float v = 1f - (y + 0.5f) / palH;
                    Color uvColor = new Color(u, v, 0.3f, uvAlpha);
                    
                    EditorGUI.DrawRect(new Rect(_paletteDisplayRect.x + x * _paletteZoom, 
                                                _paletteDisplayRect.y + y * _paletteZoom, 
                                                _paletteZoom, _paletteZoom), uvColor);
                }
            }
            
            // 3. 更新悬停像素（必须在绘制选区之前）
            Vector2 mousePos = Event.current.mousePosition;
            Vector2 local = mousePos - _paletteDisplayRect.position;
            int px = Mathf.FloorToInt(local.x / _paletteZoom);
            int py = Mathf.FloorToInt(local.y / _paletteZoom);
            if (px >= 0 && px < palW && py >= 0 && py < palH)
                _hoverPalettePixel = new Vector2Int(px, py);
            else
                _hoverPalettePixel = null;
            
            // 4. 绘制地面线
            DrawGroundLine(_paletteDisplayRect, palH);
            
            // 5. 绘制自动涂色UV区域标注
            DrawUVRegionMarkers();
            
            // 6. 绘制选区和悬停高亮
            DrawPaletteSelection();
            
            // 7. 绘制网格线
            if (_paletteZoom >= 4)
            {
                Handles.color = new Color(1, 1, 1, 0.2f);
                for (int x = 0; x <= palW; x++)
                    Handles.DrawLine(new Vector3(_paletteDisplayRect.x + x * _paletteZoom, _paletteDisplayRect.y), 
                                     new Vector3(_paletteDisplayRect.x + x * _paletteZoom, _paletteDisplayRect.yMax));
                for (int y = 0; y <= palH; y++)
                    Handles.DrawLine(new Vector3(_paletteDisplayRect.x, _paletteDisplayRect.y + y * _paletteZoom), 
                                     new Vector3(_paletteDisplayRect.xMax, _paletteDisplayRect.y + y * _paletteZoom));
            }
            
            // 8. 标签 - 显示悬停坐标
            string label = $"UV 画板 ({palW}×{palH})";
            if (_hoverPalettePixel.HasValue)
            {
                var hp = _hoverPalettePixel.Value;
                float u = (hp.x + 0.5f) / palW;
                float v = 1f - (hp.y + 0.5f) / palH;
                label += $" | ({hp.x}, {hp.y}) UV: ({u:F3}, {v:F3})";
            }
            GUI.Label(new Rect(paletteCanvas.x + 10, paletteCanvas.y + 10, 450, 20), label, EditorStyles.whiteLabel);
        }
        
        /// <summary>
        /// 绘制自动涂色UV区域标注
        /// </summary>
        void DrawUVRegionMarkers()
        {
            if (_data == null) return;
            
            // 头部区域（绿色边框）
            if (_data.headUVRegion.width > 0 && _data.headUVRegion.height > 0)
            {
                Rect headRect = new Rect(
                    _paletteDisplayRect.x + _data.headUVRegion.x * _paletteZoom,
                    _paletteDisplayRect.y + _data.headUVRegion.y * _paletteZoom,
                    _data.headUVRegion.width * _paletteZoom,
                    _data.headUVRegion.height * _paletteZoom);
                Handles.color = new Color(0.2f, 0.9f, 0.2f, 0.8f);
                Handles.DrawWireCube(headRect.center, new Vector3(headRect.width, headRect.height, 0));
                
                // 标签
                GUI.Label(new Rect(headRect.x + 2, headRect.y + 2, 50, 15), "头部", EditorStyles.miniLabel);
            }
            
            // 身体区域（蓝色边框）
            if (_data.torsoUVRegion.width > 0 && _data.torsoUVRegion.height > 0)
            {
                Rect torsoRect = new Rect(
                    _paletteDisplayRect.x + _data.torsoUVRegion.x * _paletteZoom,
                    _paletteDisplayRect.y + _data.torsoUVRegion.y * _paletteZoom,
                    _data.torsoUVRegion.width * _paletteZoom,
                    _data.torsoUVRegion.height * _paletteZoom);
                Handles.color = new Color(0.2f, 0.5f, 0.9f, 0.8f);
                Handles.DrawWireCube(torsoRect.center, new Vector3(torsoRect.width, torsoRect.height, 0));
                
                // 标签
                GUI.Label(new Rect(torsoRect.x + 2, torsoRect.y + 2, 50, 15), "身体", EditorStyles.miniLabel);
            }
        }
        
        /// <summary>
        /// 绘制画板选区
        /// </summary>
        void DrawPaletteSelection()
        {
            // 悬停像素高亮（白色边框）
            if (_hoverPalettePixel.HasValue)
            {
                var hp = _hoverPalettePixel.Value;
                Rect hoverRect = new Rect(
                    _paletteDisplayRect.x + hp.x * _paletteZoom,
                    _paletteDisplayRect.y + hp.y * _paletteZoom,
                    _paletteZoom, _paletteZoom);
                Handles.color = Color.white;
                Handles.DrawWireCube(hoverRect.center, new Vector3(hoverRect.width, hoverRect.height, 0));
            }
            
            // 已确定的选区（用于复制UV）
            if (_paletteSelection.width > 0)
            {
                Rect selRect = new Rect(
                    _paletteDisplayRect.x + _paletteSelection.x * _paletteZoom,
                    _paletteDisplayRect.y + _paletteSelection.y * _paletteZoom,
                    _paletteSelection.width * _paletteZoom,
                    _paletteSelection.height * _paletteZoom);
                EditorGUI.DrawRect(selRect, new Color(1, 1, 0, 0.3f));
                Handles.color = Color.yellow;
                Handles.DrawWireCube(selRect.center, new Vector3(selRect.width, selRect.height, 0));
            }
            
            // 正在拖拽的选区
            if (_isSelecting && _selectOnPalette)
            {
                int minX = Mathf.Min(_selectionStart.x, _selectionEnd.x);
                int maxX = Mathf.Max(_selectionStart.x, _selectionEnd.x);
                int minY = Mathf.Min(_selectionStart.y, _selectionEnd.y);
                int maxY = Mathf.Max(_selectionStart.y, _selectionEnd.y);
                
                Rect dragRect = new Rect(
                    _paletteDisplayRect.x + minX * _paletteZoom,
                    _paletteDisplayRect.y + minY * _paletteZoom,
                    (maxX - minX + 1) * _paletteZoom,
                    (maxY - minY + 1) * _paletteZoom);
                EditorGUI.DrawRect(dragRect, new Color(1, 1, 0, 0.2f));
                Handles.color = Color.yellow;
                Handles.DrawWireCube(dragRect.center, new Vector3(dragRect.width, dragRect.height, 0));
            }
        }
        
        void DrawCanvas()
        {
            _canvas = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, 
                GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            
            EditorGUI.DrawRect(_canvas, new Color(0.15f, 0.15f, 0.15f));
            
            if (_sprite == null)
            {
                GUI.Label(_canvas, "请选择 Spritesheet", EditorStyles.centeredGreyMiniLabel);
                return;
            }
            
            Rect uv = new Rect(
                (float)(_frame * _frameSize.x) / _sprite.width,
                1f - (float)((_row + 1) * _frameSize.y) / _sprite.height,
                (float)_frameSize.x / _sprite.width,
                (float)_frameSize.y / _sprite.height
            );
            
            float w = _frameSize.x * _zoom, h = _frameSize.y * _zoom;
            var center = _canvas.center + _pan;
            _display = new Rect(center.x - w/2, center.y - h/2, w, h);
            
            // 更新悬停像素（必须在绘制选区之前）
            Vector2 mousePos = Event.current.mousePosition;
            Vector2 local = mousePos - _display.position;
            int cpx = Mathf.FloorToInt(local.x / _zoom);
            int cpy = Mathf.FloorToInt(local.y / _zoom);
            if (cpx >= 0 && cpx < _frameSize.x && cpy >= 0 && cpy < _frameSize.y)
                _hoverCanvasPixel = new Vector2Int(cpx, cpy);
            else
                _hoverCanvasPixel = null;
            
            DrawCheckerboard(_display);
            if (!_hideCanvasSprite)
                GUI.DrawTextureWithTexCoords(_display, _sprite, uv);
            DrawBodyPixels(_display);
            DrawCurrentPartBorder(_display);  // 当前部位边框高亮
            DrawAnchors(_display);
            DrawCanvasSelection(_display);  // 画布选区和悬停高亮
            if (_zoom >= 4) DrawGrid(_display);
            
            string rowHint = _row < 4 ? new[]{ "SE", "SW", "NE", "NW" }[_row] : $"R{_row}";
            string modeInfo = _editMode ? (_tab == TabMode.BodyPaint ? " [编辑模式]" : "") : "";
            
            // 主标签 - 显示悬停坐标和UV
            string mainLabel = $"画布 | {_animName} | 行{_row}({rowHint}) | 帧{_frame}{modeInfo}";
            
            if (_hoverCanvasPixel.HasValue)
            {
                var hp = _hoverCanvasPixel.Value;
                mainLabel += $" | ({hp.x}, {hp.y})";
                
                // 查找该像素的UV值（优先显示当前选中部位）
                Vector2? foundUV = null;
                string foundPart = null;
                
                // 先查当前选中部位
                if (_partUVs.TryGetValue(_currentPart, out var currentUVs) && currentUVs.TryGetValue(hp, out var currentUV))
                {
                    foundUV = currentUV;
                    foundPart = _currentPart.ToString();
                }
                else
                {
                    // 再查其他部位
                    foreach (var kv in _partUVs)
                    {
                        if (kv.Value.TryGetValue(hp, out var pixelUV))
                        {
                            foundUV = pixelUV;
                            foundPart = kv.Key.ToString();
                            break;
                        }
                    }
                }
                
                if (foundUV.HasValue)
                    mainLabel += $" UV: ({foundUV.Value.x:F3}, {foundUV.Value.y:F3}) [{foundPart}]";
            }
            
            GUI.Label(new Rect(_canvas.x + 10, _canvas.y + 10, 550, 20), mainLabel, EditorStyles.whiteLabel);
        }
        
        void DrawBodyPixels(Rect r)
        {
            if (_paintDisplayMode == 0) return;
            
            foreach (var kv in _partPixels)
            {
                if (_paintDisplayMode == 1 && kv.Key != _currentPart) continue;
                
                var uvs = _partUVs.ContainsKey(kv.Key) ? _partUVs[kv.Key] : null;
                int palW = _data != null ? _data.paletteSize.x : 32;
                int palH = _data != null ? _data.paletteSize.y : 32;
                
                foreach (var p in kv.Value)
                {
                    Color c;
                    float alpha = _hideCanvasSprite ? 0.9f : 0.6f;  // 降低透明度，避免太亮
                    
                    // 手脚眼睛颜色与按钮一致，但使用较低透明度
                    if (kv.Key == CharacterBodyPart.LeftHand)
                        c = new Color(0.9f, 0.8f, 0.2f, alpha);   // 黄
                    else if (kv.Key == CharacterBodyPart.RightHand)
                        c = new Color(0.9f, 0.5f, 0.2f, alpha);   // 橙
                    else if (kv.Key == CharacterBodyPart.LeftFoot)
                        c = new Color(0.2f, 0.7f, 0.9f, alpha);   // 青
                    else if (kv.Key == CharacterBodyPart.RightFoot)
                        c = new Color(0.9f, 0.3f, 0.6f, alpha);   // 粉
                    else if (kv.Key == CharacterBodyPart.LeftEye)
                        c = new Color(0.6f, 0.2f, 0.8f, alpha);   // 紫
                    else if (kv.Key == CharacterBodyPart.RightEye)
                        c = new Color(0.8f, 0.3f, 0.6f, alpha);   // 深紫
                    else if (uvs != null && uvs.TryGetValue(p, out var uv))
                    {
                        // UV 颜色：R=U, G=V, B=0.3
                        // 隐藏角色时不透明，方便对比
                        float uvAlpha = _hideCanvasSprite ? 1f : 0.8f;
                        c = new Color(uv.x, uv.y, 0.3f, uvAlpha);
                    }
                    else
                    {
                        // 没有 UV 的像素用灰色
                        float grayAlpha = _hideCanvasSprite ? 1f : 0.5f;
                        c = new Color(0.5f, 0.5f, 0.5f, grayAlpha);
                    }
                    
                    EditorGUI.DrawRect(new Rect(r.x + p.x * _zoom, r.y + p.y * _zoom, _zoom, _zoom), c);
                }
            }
        }

        void DrawGroundLine(Rect r, int paletteHeight)
        {
            if (_data == null) return;
            int groundY = _data.groundPixelY;
            if (groundY < 0 || groundY >= paletteHeight) return;

            float y = r.y + (groundY + 0.5f) * _paletteZoom;
            Handles.color = new Color(0.3f, 1f, 0.3f, 0.8f);
            Handles.DrawLine(
                new Vector3(r.x, y, 0),
                new Vector3(r.x + r.width, y, 0));

            GUI.Label(new Rect(r.x + 5, y - 15, 120, 20),
                $"Ground Y: {groundY}", EditorStyles.whiteMiniLabel);
        }
        
        /// <summary>
        /// 绘制当前选中部位的边框高亮
        /// </summary>
        void DrawCurrentPartBorder(Rect r)
        {
            if (_paintDisplayMode == 0) return;
            if (!_partPixels.ContainsKey(_currentPart) || _partPixels[_currentPart].Count == 0) return;
            
            var pixels = _partPixels[_currentPart];
            
            // 计算当前部位的包围盒
            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;
            foreach (var p in pixels)
            {
                minX = Mathf.Min(minX, p.x);
                maxX = Mathf.Max(maxX, p.x);
                minY = Mathf.Min(minY, p.y);
                maxY = Mathf.Max(maxY, p.y);
            }
            
            if (minX > maxX) return;
            
            // 绘制边框
            Rect borderRect = new Rect(
                r.x + minX * _zoom - 1,
                r.y + minY * _zoom - 1,
                (maxX - minX + 1) * _zoom + 2,
                (maxY - minY + 1) * _zoom + 2);
            
            // 使用与按钮相同的颜色
            Color borderColor;
            switch (_currentPart)
            {
                case CharacterBodyPart.Head: borderColor = new Color(0.2f, 0.9f, 0.2f); break;
                case CharacterBodyPart.Torso: borderColor = new Color(0.2f, 0.7f, 0.2f); break;
                case CharacterBodyPart.LeftHand: borderColor = new Color(0.9f, 0.8f, 0.2f); break;
                case CharacterBodyPart.RightHand: borderColor = new Color(0.9f, 0.5f, 0.2f); break;
                case CharacterBodyPart.LeftFoot: borderColor = new Color(0.2f, 0.7f, 0.9f); break;
                case CharacterBodyPart.RightFoot: borderColor = new Color(0.9f, 0.3f, 0.6f); break;
                case CharacterBodyPart.LeftEye: borderColor = new Color(0.6f, 0.2f, 0.8f); break;
                case CharacterBodyPart.RightEye: borderColor = new Color(0.8f, 0.3f, 0.6f); break;
                default: borderColor = Color.white; break;
            }
            
            Handles.color = borderColor;
            Handles.DrawWireCube(borderRect.center, new Vector3(borderRect.width, borderRect.height, 0));
        }
        
        /// <summary>
        /// 绘制画布选区
        /// </summary>
        void DrawCanvasSelection(Rect r)
        {
            // 悬停像素高亮（白色边框）
            if (_hoverCanvasPixel.HasValue)
            {
                var hp = _hoverCanvasPixel.Value;
                Rect hoverRect = new Rect(
                    r.x + hp.x * _zoom,
                    r.y + hp.y * _zoom,
                    _zoom, _zoom);
                Handles.color = Color.white;
                Handles.DrawWireCube(hoverRect.center, new Vector3(hoverRect.width, hoverRect.height, 0));
            }
            
            // 已确定的选区（用于复制UV）
            if (_canvasSelection.width > 0)
            {
                Rect selRect = new Rect(
                    r.x + _canvasSelection.x * _zoom,
                    r.y + _canvasSelection.y * _zoom,
                    _canvasSelection.width * _zoom,
                    _canvasSelection.height * _zoom);
                EditorGUI.DrawRect(selRect, new Color(0, 1, 1, 0.3f));
                Handles.color = Color.cyan;
                Handles.DrawWireCube(selRect.center, new Vector3(selRect.width, selRect.height, 0));
            }
            
            // 正在拖拽的选区
            if (_isSelecting && !_selectOnPalette)
            {
                int minX = Mathf.Min(_selectionStart.x, _selectionEnd.x);
                int maxX = Mathf.Max(_selectionStart.x, _selectionEnd.x);
                int minY = Mathf.Min(_selectionStart.y, _selectionEnd.y);
                int maxY = Mathf.Max(_selectionStart.y, _selectionEnd.y);
                
                Rect dragRect = new Rect(
                    r.x + minX * _zoom,
                    r.y + minY * _zoom,
                    (maxX - minX + 1) * _zoom,
                    (maxY - minY + 1) * _zoom);
                
                // 擦除模式用红色，普通模式用青色
                Color fillColor = _isErasing ? new Color(1, 0, 0, 0.3f) : new Color(0, 1, 1, 0.2f);
                Color lineColor = _isErasing ? Color.red : Color.cyan;
                
                EditorGUI.DrawRect(dragRect, fillColor);
                Handles.color = lineColor;
                Handles.DrawWireCube(dragRect.center, new Vector3(dragRect.width, dragRect.height, 0));
            }
        }
        
        /// <summary>
        /// 从画板选区拷贝 UV 到画布选区
        /// 支持：1) 选区大小一致时 1:1 复制  2) 画板单像素时填充整个画布选区
        /// </summary>
        void CopyUVFromPaletteToCanvas()
        {
            if (_paletteSelection.width <= 0 || _canvasSelection.width <= 0)
            {
                Debug.LogWarning("请先在画板和画布上分别框选区域");
                return;
            }
            
            bool sizeMatch = _paletteSelection.size == _canvasSelection.size;
            bool singlePalette = _paletteSelection.width == 1 && _paletteSelection.height == 1;
            bool transposedSize = _paletteSelection.width == _canvasSelection.height
                                  && _paletteSelection.height == _canvasSelection.width;
            
            if (!sizeMatch && !singlePalette && !transposedSize)
            {
                Debug.LogWarning($"选区大小不匹配: 画板 {_paletteSelection.width}×{_paletteSelection.height}, 画布 {_canvasSelection.width}×{_canvasSelection.height}");
                return;
            }
            
            if (!_partPixels.ContainsKey(_currentPart))
                _partPixels[_currentPart] = new HashSet<Vector2Int>();
            if (!_partUVs.ContainsKey(_currentPart))
                _partUVs[_currentPart] = new Dictionary<Vector2Int, Vector2>();
            if (!_corePartPixels.ContainsKey(_currentPart))
                _corePartPixels[_currentPart] = new HashSet<Vector2Int>();

            var pixels = _partPixels[_currentPart];
            var uvs = _partUVs[_currentPart];
            var corePixels = _corePartPixels[_currentPart];
            
            int palW = _data != null ? _data.paletteSize.x : 32;
            int palH = _data != null ? _data.paletteSize.y : 32;
            int selW = _paletteSelection.width;
            int selH = _paletteSelection.height;

            string mode;
            if (singlePalette) mode = "singlePalette";
            else if (sizeMatch) mode = "sizeMatch";
            else mode = "transposedSize";
            Debug.Log($"[CopyUV] mode={mode}, palette={selW}x{selH}, canvas={_canvasSelection.width}x{_canvasSelection.height}");

            for (int dy = 0; dy < _canvasSelection.height; dy++)
            {
                for (int dx = 0; dx < _canvasSelection.width; dx++)
                {
                    // 画布目标像素
                    int dstX = _canvasSelection.x + dx;
                    int dstY = _canvasSelection.y + dy;
                    var dstPos = new Vector2Int(dstX, dstY);

                    if (!IsValidPixel(dstPos))
                        continue;

                    int localSrcX;
                    int localSrcY;

                    if (singlePalette)
                    {
                        localSrcX = 0;
                        localSrcY = 0;
                    }
                    else if (sizeMatch)
                    {
                        // 1:1 拷贝
                        localSrcX = dx;
                        localSrcY = dy;
                    }
                    else // transposedSize
                    {
                        // 画板选区 w×h，画布选区 h×w：左旋 90 度
                        // 画板从左到右的列 → 画布从下到上的行
                        localSrcX = selW - 1 - dy;
                        localSrcY = dx;
                    }

                    int srcX = _paletteSelection.x + localSrcX;
                    int srcY = _paletteSelection.y + localSrcY;
                    float u = (srcX + 0.5f) / palW;
                    float v = 1f - (srcY + 0.5f) / palH;
                    Vector2 uv = new Vector2(u, v);

                    pixels.Add(dstPos);
                    uvs[dstPos] = uv;
                    corePixels.Add(dstPos);
                }
            }

            SaveWithUndo("拷贝 UV");
            Repaint();
        }
        
        /// <summary>
        /// 删除画布选区内的涂色
        /// </summary>
        void DeleteCanvasSelection()
        {
            if (_canvasSelection.width <= 0)
            {
                Debug.LogWarning("请先在画布上框选区域");
                return;
            }
            
            if (!_partPixels.ContainsKey(_currentPart))
                return;
            
            var pixels = _partPixels[_currentPart];
            var uvs = _partUVs.ContainsKey(_currentPart) ? _partUVs[_currentPart] : null;
            var corePixels = _corePartPixels.ContainsKey(_currentPart) ? _corePartPixels[_currentPart] : null;
            
            int removed = 0;
            for (int dy = 0; dy < _canvasSelection.height; dy++)
            {
                for (int dx = 0; dx < _canvasSelection.width; dx++)
                {
                    var pos = new Vector2Int(_canvasSelection.x + dx, _canvasSelection.y + dy);
                    if (pixels.Remove(pos))
                    {
                        uvs?.Remove(pos);
                        corePixels?.Remove(pos);
                        removed++;
                    }
                }
            }
            
            SaveWithUndo("删除选区");
            Repaint();
            Debug.Log($"已删除 {removed} 个像素的涂色");
        }
        
        /// <summary>
        /// 扩展所有部位的区域（当前帧）
        /// 先身体后头部
        /// </summary>
        void ExpandAllPartsRegion()
        {
            if (_data == null) return;
            
            // 1. 先扩展身体（使用可配置的上扩起始步长，默认 1）
            int bodyUpStartStep = Mathf.Max(1, _data.bodyExpandUpStartStep);
            int bodyDownStartStep = Mathf.Max(1, _data.bodyExpandDownStartStep);
            ExpandPartRegion(CharacterBodyPart.Torso, _data.bodyExpandUp, _data.bodyExpandDown, _data.bodyExpandSide, bodyUpStartStep, bodyDownStartStep);
            
            // 2. 再扩展头部（使用默认起始步长 1）
            ExpandPartRegion(CharacterBodyPart.Head, _data.headExpandUp, _data.headExpandDown, _data.headExpandSide);
            
            SaveFrameToData(false, "扩展区域");
            _isDirty = false;
            Repaint();
        }
        
        void ExpandPartRegion(CharacterBodyPart part, int expandUp, int expandDown, int expandSide, int upStartStep = 1, int downStartStep = 1)
        {
            if (!_partPixels.ContainsKey(part) || _partPixels[part].Count == 0) return;
            if (expandUp == 0 && expandDown == 0 && expandSide == 0) return;
            
            var pixels = _partPixels[part];
            if (!_partUVs.ContainsKey(part))
                _partUVs[part] = new Dictionary<Vector2Int, Vector2>();
            var uvs = _partUVs[part];
            
            var paletteSize = _data != null ? _data.paletteSize : new Vector2Int(32, 32);
            var pose = _data != null ? _data.regionExpandPose : RegionExpandPose.HeadUp;

            // 先根据姿态把“身体坐标系”的扩展量（Up/Down/Side）映射到屏幕坐标的 up/down/left/right
            int up = expandUp, down = expandDown, left = expandSide, right = expandSide;
            FrameDataAlgorithms.MapExpandByPose(pose, expandUp, expandDown, expandSide, out up, out down, out left, out right);

            // 几何扩展和 UV 方向都按同一个姿态来旋转
            FrameDataAlgorithms.ExpandRegionWithBoundaryUV(
                pixels, uvs,
                up, down, left, right,
                _frameSize, paletteSize,
                upStartStep, downStartStep,
                pose);
        }
        
        /// <summary>
        /// 扩展所有部位的区域（全部帧）- 并行优化版
        /// </summary>
        void ExpandAllPartsForAllFrames()
        {
            if (_data == null) return;
            var anim = GetCurrentAnimation();
            if (anim == null) return;
            
            if (_isDirty)
            {
                SaveFrameToData(false, "Edit Frame");
                _isDirty = false;
            }

            FrameDataEditorTools.ExpandAllPartsForAllFrames(_data, anim, _data.regionExpandPose);
            
            _isDirty = false;
            LoadFrameData();
        }
        
        
        /// <summary>
        /// 收缩所有部位的区域（当前帧）
        /// </summary>
        void ShrinkAllPartsRegion()
        {
            if (_data == null) return;
            
            ShrinkPartRegion(CharacterBodyPart.Head, _data.headExpandUp, _data.headExpandDown, _data.headExpandSide);
            ShrinkPartRegion(CharacterBodyPart.Torso, _data.bodyExpandUp, _data.bodyExpandDown, _data.bodyExpandSide);
            
            SaveFrameToData(false, "收缩区域");
            _isDirty = false;
            Repaint();
        }
        
        void ShrinkPartRegion(CharacterBodyPart part, int shrinkUp, int shrinkDown, int shrinkSide)
        {
            if (!_partPixels.ContainsKey(part) || _partPixels[part].Count == 0) return;
            
            var pixels = _partPixels[part];
            var uvs = _partUVs.ContainsKey(part) ? _partUVs[part] : null;

            // 基于 isCore 的收缩：若该部位存在核心像素，则直接丢弃所有非核心像素
            if (_corePartPixels.ContainsKey(part) && _corePartPixels[part].Count > 0)
            {
                var core = _corePartPixels[part];
                var toRemove = new List<Vector2Int>();
                foreach (var pos in pixels)
                {
                    if (!core.Contains(pos))
                        toRemove.Add(pos);
                }

                foreach (var pos in toRemove)
                {
                    pixels.Remove(pos);
                    uvs?.Remove(pos);
                }

                return;
            }

            // 无核心数据时，保持原有几何收缩逻辑以兼容旧数据
            if (_data == null)
            {
                FrameDataAlgorithms.ShrinkRegion(pixels, uvs, shrinkUp, shrinkDown, shrinkSide, shrinkSide);
                return;
            }

            var pose = _data.regionExpandPose;
            Vector2Int detectSize;
            if (part == CharacterBodyPart.Head)
                detectSize = _data.headDetectSize;
            else if (part == CharacterBodyPart.Torso)
                detectSize = _data.torsoDetectSize;
            else
            {
                FrameDataAlgorithms.ShrinkRegion(pixels, uvs, shrinkUp, shrinkDown, shrinkSide, shrinkSide);
                return;
            }

            FrameDataAlgorithms.ShrinkRegionByPoseAndDetectSize(
                pixels, uvs,
                detectSize,
                pose,
                shrinkUp, shrinkDown, shrinkSide);
        }

        /// <summary>
        /// 收缩所有部位的区域（全部帧）- 并行优化版
        /// </summary>
        void ShrinkAllPartsForAllFrames()
        {
            if (_data == null) return;
            var anim = GetCurrentAnimation();
            if (anim == null) return;
            
            if (_isDirty)
            {
                SaveFrameToData(false, "Edit Frame");
                _isDirty = false;
            }

            FrameDataEditorTools.ShrinkAllPartsForAllFrames(_data, anim, _data.regionExpandPose);
            
            _isDirty = false;
            LoadFrameData();
        }
        
        
        // 部位颜色映射表 - 用于编辑器显示
        static readonly Dictionary<CharacterBodyPart, Color> PartColors = new Dictionary<CharacterBodyPart, Color>
        {
            { CharacterBodyPart.Head,      new Color(0.2f, 0.8f, 0.8f, 0.6f) },  // 青色
            { CharacterBodyPart.Torso,     new Color(0.3f, 0.5f, 0.9f, 0.6f) },  // 蓝色
            { CharacterBodyPart.LeftHand,  new Color(0.9f, 0.9f, 0.2f, 0.6f) },  // 黄色
            { CharacterBodyPart.RightHand, new Color(0.9f, 0.6f, 0.2f, 0.6f) },  // 橙色
            { CharacterBodyPart.LeftFoot,  new Color(0.6f, 0.3f, 0.9f, 0.6f) },  // 紫色
            { CharacterBodyPart.RightFoot, new Color(0.9f, 0.3f, 0.6f, 0.6f) },  // 粉色
            { CharacterBodyPart.LeftEye,   new Color(0.6f, 0.2f, 0.8f, 0.6f) },  // 紫色
            { CharacterBodyPart.RightEye,  new Color(0.8f, 0.3f, 0.6f, 0.6f) },  // 深紫色
        };
        static readonly Color DefaultPartColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        
        Color GetPartColor(CharacterBodyPart part) 
            => PartColors.TryGetValue(part, out var c) ? c : DefaultPartColor;
        
        void DrawAnchors(Rect r)
        {
            foreach (var a in _anchors)
            {
                float x = r.x + a.position.x * _zoom + _zoom/2;
                float y = r.y + a.position.y * _zoom + _zoom/2;
                
                Handles.color = a.type == _anchorType ? Color.yellow : Color.cyan;
                Handles.DrawSolidDisc(new Vector3(x, y, 0), Vector3.forward, 6);
                
                Vector2 dir = GetAnchorDirVec(a.direction) * _zoom;
                Handles.DrawLine(new Vector3(x, y), new Vector3(x + dir.x, y + dir.y));
                
                GUI.Label(new Rect(x + 8, y - 8, 100, 20), a.type.ToString(), EditorStyles.whiteMiniLabel);
            }
        }
        
        /// <summary>
        /// 获取锚点方向对应的屏幕空间方向向量（用于可视化）
        /// </summary>
        Vector2 GetAnchorDirVec(AnchorDirection direction)
        {
            switch (direction)
            {
                case AnchorDirection.East: return new Vector2(1, 0);
                case AnchorDirection.NorthEast: return new Vector2(1, -1).normalized;
                case AnchorDirection.North: return new Vector2(0, -1);
                case AnchorDirection.NorthWest: return new Vector2(-1, -1).normalized;
                case AnchorDirection.West: return new Vector2(-1, 0);
                case AnchorDirection.SouthWest: return new Vector2(-1, 1).normalized;
                case AnchorDirection.South: return new Vector2(0, 1);
                case AnchorDirection.SouthEast: return new Vector2(1, 1).normalized;
                default: return new Vector2(1, 0);
            }
        }
        
        void DrawCheckerboard(Rect r)
        {
            Color c1 = new Color(0.25f, 0.25f, 0.25f), c2 = new Color(0.35f, 0.35f, 0.35f);
            for (int y = 0; y < _frameSize.y; y++)
                for (int x = 0; x < _frameSize.x; x++)
                    EditorGUI.DrawRect(new Rect(r.x + x * _zoom, r.y + y * _zoom, _zoom, _zoom), (x+y)%2==0 ? c1 : c2);
        }
        
        void DrawGrid(Rect r)
        {
            Handles.color = new Color(1, 1, 1, 0.1f);
            for (int x = 0; x <= _frameSize.x; x++)
                Handles.DrawLine(new Vector3(r.x + x*_zoom, r.y), new Vector3(r.x + x*_zoom, r.yMax));
            for (int y = 0; y <= _frameSize.y; y++)
                Handles.DrawLine(new Vector3(r.x, r.y + y*_zoom), new Vector3(r.xMax, r.y + y*_zoom));
        }
        
        #endregion
        
        #region 输入处理
        
        void HandleInput()
        {
            var e = Event.current;
            
            if (e.type == EventType.KeyDown)
            {
                switch (e.keyCode)
                {
                    case KeyCode.Alpha1: SaveIfDirty(); _tab = TabMode.BodyPaint; Repaint(); e.Use(); break;
                    case KeyCode.Alpha2: SaveIfDirty(); _tab = TabMode.Anchor; Repaint(); e.Use(); break;
                    case KeyCode.Escape:
                        // 取消选区
                        _isSelecting = false;
                        _paletteSelection = default;
                        _canvasSelection = default;
                        Repaint();
                        e.Use();
                        break;
                }
            }
            
            // 检测鼠标在哪个区域
            bool inPalette = _showPalette && _paletteCanvasRect.Contains(e.mousePosition);
            bool inCanvas = !inPalette && new Rect(_canvasOffset.x, _canvasOffset.y, _canvas.width, _canvas.height).Contains(e.mousePosition);
            
            if (!inPalette && !inCanvas) return;
            
            switch (e.type)
            {
                case EventType.MouseDown:
                    // 画板框选（非编辑模式也可以选区，方便查看坐标）
                    if (e.button == 0 && _tab == TabMode.BodyPaint && inPalette && !e.shift)
                    {
                        var p = GetPalettePixelPos(e.mousePosition);
                        if (IsValidPalettePixel(p))
                        {
                            _isSelecting = true;
                            _isErasing = false;
                            _selectOnPalette = true;
                            _selectionStart = p;
                            _selectionEnd = p;
                        }
                        e.Use();
                    }
                    // 画布操作：编辑模式用于绘制/擦除，非编辑模式用于选区查看UV
                    else if (e.button == 0 && _tab == TabMode.BodyPaint && inCanvas)
                    {
                        var p = GetPixelPos(e.mousePosition - _canvasOffset);
                        if (IsValidPixel(p))
                        {
                            if (_editMode)
                            {
                                bool isShift = e.shift;
                                bool isLimb = _currentPart == CharacterBodyPart.LeftHand || _currentPart == CharacterBodyPart.RightHand ||
                                              _currentPart == CharacterBodyPart.LeftFoot || _currentPart == CharacterBodyPart.RightFoot ||
                                              _currentPart == CharacterBodyPart.LeftEye || _currentPart == CharacterBodyPart.RightEye;

                                if (isLimb)
                                {
                                    // 手脚部位：点击添加或清除（Shift清除全部）
                                    if (isShift)
                                    {
                                        if (_partPixels.ContainsKey(_currentPart))
                                            _partPixels[_currentPart].Clear();
                                        SaveWithUndo("清除手脚");
                                    }
                                    else
                                    {
                                        if (!_partPixels.ContainsKey(_currentPart))
                                            _partPixels[_currentPart] = new HashSet<Vector2Int>();
                                        _partPixels[_currentPart].Add(p);
                                        SaveWithUndo("添加手脚像素");
                                    }
                                    Repaint();
                                }
                                else
                                {
                                    // 头部/身体：框选模式（可擦除或拷贝UV）
                                    _isSelecting = true;
                                    _isErasing = isShift;
                                    _selectOnPalette = false;
                                    _selectionStart = p;
                                    _selectionEnd = p;
                                }
                            }
                            else
                            {
                                // 非编辑模式：只用于选区（查看UV），不修改涂色/UV
                                _isSelecting = true;
                                _isErasing = false;
                                _selectOnPalette = false;
                                _selectionStart = p;
                                _selectionEnd = p;
                            }
                        }
                        e.Use();
                    }
                    else if (e.button == 0 && _tab == TabMode.Anchor && inCanvas)
                    {
                        OnLeftClick(e.mousePosition - _canvasOffset);
                        e.Use();
                    }
                    else if (e.button == 1 && inCanvas && _tab == TabMode.BodyPaint && _editMode)
                    {
                        // 右键开始拖动删除
                        _isRightDragging = true;
                        OnRightClick(e.mousePosition - _canvasOffset);
                        e.Use();
                    }
                    else if (e.button == 2)
                    {
                        _panning = true;
                        _lastMouse = e.mousePosition;
                        e.Use();
                    }
                    break;
                    
                case EventType.MouseDrag:
                    if (_panning)
                    {
                        Vector2 delta = e.mousePosition - _lastMouse;
                        if (inPalette)
                            _palettePan += delta;
                        else
                            _pan += delta;
                        _lastMouse = e.mousePosition;
                        Repaint();
                        e.Use();
                    }
                    else if (_isRightDragging && inCanvas)
                    {
                        // 右键拖动删除
                        OnRightClick(e.mousePosition - _canvasOffset);
                        e.Use();
                    }
                    else if (_isSelecting)
                    {
                        // 更新选区
                        if (_selectOnPalette)
                        {
                            var p = GetPalettePixelPos(e.mousePosition);
                            var palSize = GetPaletteSize();
                            _selectionEnd = new Vector2Int(
                                Mathf.Clamp(p.x, 0, palSize.x - 1),
                                Mathf.Clamp(p.y, 0, palSize.y - 1)
                            );
                        }
                        else
                        {
                            var p = GetPixelPos(e.mousePosition - _canvasOffset);
                            _selectionEnd = new Vector2Int(
                                Mathf.Clamp(p.x, 0, _frameSize.x - 1),
                                Mathf.Clamp(p.y, 0, _frameSize.y - 1)
                            );
                        }
                        Repaint();
                        e.Use();
                    }
                    break;
                    
                case EventType.MouseUp:
                    if (_isSelecting && e.button == 0)
                    {
                        // 完成选区
                        _isSelecting = false;
                        int minX = Mathf.Min(_selectionStart.x, _selectionEnd.x);
                        int maxX = Mathf.Max(_selectionStart.x, _selectionEnd.x);
                        int minY = Mathf.Min(_selectionStart.y, _selectionEnd.y);
                        int maxY = Mathf.Max(_selectionStart.y, _selectionEnd.y);
                        
                        if (_selectOnPalette)
                        {
                            _paletteSelection = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
                        }
                        else
                        {
                            // 无论是否编辑模式，都更新画布选区，方便查看UV
                            _canvasSelection = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);

                            if (_editMode)
                            {
                                if (_isErasing)
                                {
                                    // 编辑模式下，擦除选区内的涂色
                                    DeleteCanvasSelection();
                                    _canvasSelection = default;
                                }
                                else if (_paletteSelection.width > 0)
                                {
                                    var palSize = _paletteSelection.size;
                                    var canvasSize = _canvasSelection.size;
                                    bool sizeMatch = palSize == canvasSize;
                                    bool singlePalette = _paletteSelection.width == 1 && _paletteSelection.height == 1;
                                    bool transposedSize = _paletteSelection.width == _canvasSelection.height && _paletteSelection.height == _canvasSelection.width;

                                    if (sizeMatch || singlePalette || transposedSize)
                                    {
                                        // 编辑模式下，自动复制UV（画布选区大小与画板选区匹配时）
                                        CopyUVFromPaletteToCanvas();
                                        _canvasSelection = default;  // 复制后清除画布选区
                                    }
                                }
                            }
                        }
                        
                        _isErasing = false;
                        Repaint();
                        e.Use();
                    }
                    if (e.button == 1)
                    {
                        _isRightDragging = false;
                        if (_isDirty) SaveWithUndo("删除涂色");
                    }
                    _panning = false;
                    break;
                    
                case EventType.ScrollWheel:
                    if (inPalette)
                        _paletteZoom = Mathf.Clamp(_paletteZoom - e.delta.y * 0.5f, 2f, 50f);
                    else
                        _zoom = Mathf.Clamp(_zoom - e.delta.y * 0.5f, 2f, 50f);
                    Repaint();
                    e.Use();
                    break;
            }
        }
        
        /// <summary>
        /// 获取画板像素坐标
        /// </summary>
        Vector2Int GetPalettePixelPos(Vector2 mousePos)
        {
            // _paletteDisplayRect 是相对于 _paletteCanvasRect 的，需要先转换
            Vector2 localInPaletteArea = mousePos - _paletteCanvasRect.position;
            Vector2 local = localInPaletteArea - _paletteDisplayRect.position;
            return new Vector2Int(Mathf.FloorToInt(local.x / _paletteZoom), Mathf.FloorToInt(local.y / _paletteZoom));
        }
        
        /// <summary>
        /// 获取画板尺寸
        /// </summary>
        Vector2Int GetPaletteSize()
        {
            return _data != null ? _data.paletteSize : new Vector2Int(32, 32);
        }
        
        /// <summary>
        /// 检查画板像素是否有效
        /// </summary>
        bool IsValidPalettePixel(Vector2Int p)
        {
            var size = GetPaletteSize();
            return p.x >= 0 && p.x < size.x && p.y >= 0 && p.y < size.y;
        }
        
        Vector2Int GetPixelPos(Vector2 mouse)
        {
            Vector2 local = mouse - _display.position;
            return new Vector2Int(Mathf.FloorToInt(local.x / _zoom), Mathf.FloorToInt(local.y / _zoom));
        }
        
        bool IsValidPixel(Vector2Int p) => p.x >= 0 && p.x < _frameSize.x && p.y >= 0 && p.y < _frameSize.y;
        
        void OnLeftClick(Vector2 mouse)
        {
            var p = GetPixelPos(mouse);
            if (!IsValidPixel(p)) return;
            
            switch (_tab)
            {
                case TabMode.Anchor:
                    SetOrUpdateAnchor(_anchorType, p, _anchorDirection);
                    SaveWithUndo("设置锚点");
                    break;
            }
            
            Repaint();
        }
        
        void OnRightClick(Vector2 mouse)
        {
            var p = GetPixelPos(mouse);
            if (!IsValidPixel(p)) return;
            
            if (_tab == TabMode.BodyPaint)
            {
                // 只删除当前选中部位的像素
                if (_partPixels.ContainsKey(_currentPart) && _partPixels[_currentPart].Remove(p))
                {
                    if (_partUVs.ContainsKey(_currentPart))
                        _partUVs[_currentPart].Remove(p);
                    if (_corePartPixels.ContainsKey(_currentPart))
                        _corePartPixels[_currentPart].Remove(p);
                    MarkDirty();
                    Repaint();
                }
            }
        }
        
        #endregion
        
        #region 数据保存/加载
        
        void MarkDirty() => _isDirty = true;
        
        /// <summary>
        /// 即时保存并记录撤销，用于需要独立撤销步骤的操作
        /// </summary>
        void SaveWithUndo(string undoName)
        {
            SaveFrameToData(true, undoName);
            _isDirty = false;
        }
        
        void SaveIfDirty()
        {
            if (_isDirty)
            {
                SaveFrameToData(true, "Edit Frame");
                _isDirty = false;
            }
        }
        
        void SaveFrameToData(bool recordUndo = true, string undoName = "Edit Frame")
        {
            var anim = GetCurrentAnimation();
            if (anim == null) return;
            
            if (recordUndo)
                Undo.RecordObject(_data, undoName);
            var frame = anim.GetOrCreateFrame(_frame, _row);
            
            // 保存锚点（深拷贝）
            frame.anchors.Clear();
            foreach (var anchor in _anchors)
            {
                frame.anchors.Add(new AnchorPoint
                {
                    type = anchor.type,
                    position = anchor.position,
                    direction = anchor.direction
                });
            }
            
            // 保存部位区域（需要读取像素颜色）
            frame.bodyRegions.Clear();
            if (_sprite != null)
            {
                // 如果已经可读，直接保存；否则临时开启可读
                if (_sprite.isReadable)
                    SaveBodyRegionsToFrame(frame);
                else
                    WithReadableTexture(() => SaveBodyRegionsToFrame(frame));
            }
            frame.leftEyeClosed = _leftEyeClosed;
            frame.rightEyeClosed = _rightEyeClosed;
            frame.hitOutlineFrame = _hitOutlineFrame;
            frame.sequenceOffset = _sequenceOffset;
            
            EditorUtility.SetDirty(_data);
        }
        
        /// <summary>
        /// 保存部位区域到帧数据（内部方法，需要 Texture 可读）
        /// </summary>
        void SaveBodyRegionsToFrame(FrameData frame)
        {
            if (_sprite == null || !_sprite.isReadable) return;
            
            var pixels = _sprite.GetPixels32();
            
            // 清空手脚蒙版
            if (frame.limbMask == null)
                frame.limbMask = new LimbMask();
            else
                frame.limbMask.Clear();
            
            // 保存手脚蒙版
            foreach (var kv in _partPixels)
            {
                if (kv.Value.Count == 0) continue;
                if (!IsLimbPart(kv.Key)) continue;
                frame.limbMask.SetPixels(kv.Key, kv.Value);
            }
            
            // 保存 UV 部位（头/身体）- 包括贴图方向和变体
            SaveUVPartToFrame(frame, CharacterBodyPart.Head, pixels);
            SaveUVPartToFrame(frame, CharacterBodyPart.Torso, pixels);
        }
        
        void SaveUVPartToFrame(FrameData frame, CharacterBodyPart part, Color32[] pixels)
        {
            var partPixels = _partPixels.ContainsKey(part) ? _partPixels[part] : null;
            var partUVs = _partUVs.ContainsKey(part) ? _partUVs[part] : null;
            var corePixels = _corePartPixels.ContainsKey(part) ? _corePartPixels[part] : null;
            
            FrameDataPersistence.SaveUVPartToFrame(
                frame, part, partPixels, partUVs, corePixels,
                _partSpriteFacings, _partVariants,
                pixels, _frame, _row, _frameSize, _sprite
            );
        }
        
        /// <summary>
        /// 获取当前选中的动画数据（使用 _animName 查找）
        /// </summary>
        AnimationData GetCurrentAnimation()
        {
            if (_data == null || string.IsNullOrEmpty(_animName)) return null;
            return _data.animations.Find(a => 
                a.animationType != null && 
                string.Equals(a.animationType.name, _animName, System.StringComparison.OrdinalIgnoreCase));
        }
        
        void LoadFrameData()
        {
            _anchors.Clear();
            _partPixels.Clear();
            _partUVs.Clear();
            _partSpriteFacings.Clear();
            _partVariants.Clear();
            _corePartPixels.Clear();
            _isDirty = false;
            
            if (_data == null) return;
            
            var anim = GetCurrentAnimation();
            if (anim == null) return;
            
            var frame = anim.GetFrame(_frame, _row);
            if (frame == null) return;

            _hitOutlineFrame = frame.hitOutlineFrame;
            _leftEyeClosed = frame.leftEyeClosed;
            _rightEyeClosed = frame.rightEyeClosed;
            _sequenceOffset = frame.sequenceOffset;
            
            // 深拷贝锚点，避免直接引用导致撤销失效
            foreach (var anchor in frame.anchors)
            {
                _anchors.Add(new AnchorPoint
                {
                    type = anchor.type,
                    position = anchor.position,
                    direction = anchor.direction
                });
            }

            // 同步当前锚点编辑状态：
            // 1. 如果当前选择的类型在本帧存在，则沿用该类型并更新方向；
            // 2. 否则如果本帧有任何锚点，则选中第一个锚点及其方向；
            // 3. 否则回退到默认 East。
            if (_anchors.Count > 0)
            {
                var selected = _anchors.Find(a => a.type == _anchorType);
                if (selected != null)
                {
                    _anchorDirection = selected.direction;
                }
                else
                {
                    _anchorType = _anchors[0].type;
                    _anchorDirection = _anchors[0].direction;
                }
            }
            else
            {
                _anchorDirection = default;  // South
            }
            
            // 加载 UV 部位（头/身体）
            foreach (var region in frame.bodyRegions)
            {
                _partSpriteFacings[region.part] = region.spriteFacing;
                _partPixels[region.part] = new HashSet<Vector2Int>();
                _partUVs[region.part] = new Dictionary<Vector2Int, Vector2>();
                _partVariants[region.part] = region.variant;
                _corePartPixels[region.part] = new HashSet<Vector2Int>();
                foreach (var px in region.pixels)
                {
                    _partPixels[region.part].Add(px.position);
                    if (px.HasUV)
                        _partUVs[region.part][px.position] = px.uv;
                    if (px.isCore)
                        _corePartPixels[region.part].Add(px.position);
                }
            }
            
            // 加载手脚眼睛蒙版
            if (frame.limbMask != null)
            {
                LoadLimbFromMask(CharacterBodyPart.LeftHand, frame.limbMask.leftHand);
                LoadLimbFromMask(CharacterBodyPart.RightHand, frame.limbMask.rightHand);
                LoadLimbFromMask(CharacterBodyPart.LeftFoot, frame.limbMask.leftFoot);
                LoadLimbFromMask(CharacterBodyPart.RightFoot, frame.limbMask.rightFoot);
                LoadLimbFromMask(CharacterBodyPart.LeftEye, frame.limbMask.leftEye);
                LoadLimbFromMask(CharacterBodyPart.RightEye, frame.limbMask.rightEye);
            }
            
            Repaint();
        }
        
        void LoadLimbFromMask(CharacterBodyPart part, List<Vector2Int> pixels)
        {
            if (pixels == null || pixels.Count == 0) return;
            _partPixels[part] = new HashSet<Vector2Int>(pixels);
            _partUVs[part] = new Dictionary<Vector2Int, Vector2>(); // 手脚不需要UV
        }
        
        void SyncFromData()
        {
            if (_data == null) return;
            
            // 如果有数据库，使用第一个动画类型
            if (_data.animDatabase != null && _data.animDatabase.Count > 0)
            {
                _animIndex = 0;
                var firstType = _data.animDatabase[0];
                _animName = firstType.name;
                var anim = _data.GetOrCreateAnimation(firstType);
                SyncFromAnimation(anim);
            }
            else if (_data.animations.Count > 0)
            {
                // 回退：使用已有数据
                _animIndex = 0;
                var anim = _data.animations[0];
                _animName = anim.animationType != null ? anim.animationType.name : "";
                SyncFromAnimation(anim);
            }
            
            LoadFrameData();
        }
        
        /// <summary>
        /// 同步数据但保持当前的 animIndex/frame/row
        /// </summary>
        void SyncFromDataKeepIndex()
        {
            if (_data == null) return;
            
            // 使用保存的 animIndex
            if (_data.animDatabase != null && _animIndex >= 0 && _animIndex < _data.animDatabase.Count)
            {
                var animType = _data.animDatabase[_animIndex];
                _animName = animType.name;
                var anim = _data.GetOrCreateAnimation(animType);
                SyncFromAnimation(anim);
            }
            else if (_data.animations.Count > 0)
            {
                _animIndex = Mathf.Clamp(_animIndex, 0, _data.animations.Count - 1);
                var anim = _data.animations[_animIndex];
                _animName = anim.animationType != null ? anim.animationType.name : "";
                SyncFromAnimation(anim);
            }
            
            // 确保 frame/row 在有效范围内
            _frame = Mathf.Clamp(_frame, 0, Mathf.Max(0, _framesPerRow - 1));
            _row = Mathf.Clamp(_row, 0, Mathf.Max(0, _rowCount - 1));
            
            LoadFrameData();
        }
        
        void SyncFromAnimation(AnimationData anim)
        {
            if (anim == null) return;
            
            _sprite = anim.spritesheet;
            _frameSize = anim.frameSize;
            _framesPerRow = anim.framesPerRow;
            _rowCount = anim.rowCount;
            
            // 确保帧和行索引在有效范围内
            _frame = Mathf.Clamp(_frame, 0, Mathf.Max(0, _framesPerRow - 1));
            _row = Mathf.Clamp(_row, 0, Mathf.Max(0, _rowCount - 1));
        }
        
        /// <summary>
        /// 根据当前行获取默认的 CharacterFacing
        /// </summary>
        CharacterFacing GetDefaultSpriteFacing()
        {
            // 行索引与 CharacterFacing 枚举值一一对应
            // 行 0 = SE, 行 1 = SW, 行 2 = NE, 行 3 = NW
            if (_row >= 0 && _row <= 3)
                return (CharacterFacing)_row;
            return CharacterFacing.SouthEast;
        }
        
        
        void SwitchRow(int newRow)
        {
            SaveIfDirty();
            _row = newRow;
            LoadFrameData();
        }
        
        void SwitchFrame(int newFrame)
        {
            if (newFrame == _frame) return;
            SaveIfDirty();
            _frame = newFrame;
            LoadFrameData();
        }
        
        void AutoDetectFrameConfig()
        {
            var anim = GetCurrentAnimation();
            if (anim == null) return;
            if (anim.spritesheet == null || anim.frameSize.x <= 0 || anim.frameSize.y <= 0) return;
            
            // 根据帧尺寸计算行数和每行帧数
            anim.framesPerRow = Mathf.Max(1, anim.spritesheet.width / anim.frameSize.x);
            anim.rowCount = Mathf.Max(1, anim.spritesheet.height / anim.frameSize.y);
            
            SyncFromAnimation(anim);
            EditorUtility.SetDirty(_data);
            Repaint();
        }
        
        #endregion
        
        #region 自动检测
        
        
        
        
        /// <summary>
        /// 用UV区域填充检测区域，支持不同大小的映射
        /// 检测区域 > UV区域时：
        /// - 头部：靠右对齐，左边多出的列用UV最左列填充
        /// - 身体：居中对齐，边缘复制边界UV
        /// </summary>
        void FillPartWithUV(Vector2Int startPos, Vector2Int detectSize, RectInt uvRegion, CharacterBodyPart part, int palW, int palH)
        {
            CharacterFacing facing;
            if (_partSpriteFacings != null && _partSpriteFacings.TryGetValue(part, out var partFacing))
                facing = partFacing;
            else
                facing = GetDefaultSpriteFacing();

            FrameDataPersistence.FillPartWithUV(
                startPos, detectSize, uvRegion, part, palW, palH,
                _frameSize, _partPixels[part], _partUVs[part], facing
            );

            // 通过自动检测填充的 UV 像素视为核心区域
            if (!_corePartPixels.ContainsKey(part))
                _corePartPixels[part] = new HashSet<Vector2Int>();
            _corePartPixels[part].UnionWith(_partPixels[part]);
        }
        
        /// <summary>
        /// 镜像当前部位的UV坐标
        /// </summary>
        void MirrorCurrentPartUV(bool horizontal)
        {
            if (!_partUVs.ContainsKey(_currentPart) || !_partPixels.ContainsKey(_currentPart)) return;
            
            var uvs = _partUVs[_currentPart];
            var pixels = _partPixels[_currentPart];
            if (pixels.Count == 0) return;
            
            FrameDataAlgorithms.MirrorUV(pixels, uvs, horizontal);
            SaveWithUndo(horizontal ? "水平镜像UV" : "垂直镜像UV");
            Repaint();
        }
        
        /// <summary>
        /// 旋转当前部位的UV坐标 90 度
        /// </summary>
        void RotateCurrentPartUV(bool clockwise)
        {
            if (!_partUVs.ContainsKey(_currentPart) || !_partPixels.ContainsKey(_currentPart)) return;

            var uvs = _partUVs[_currentPart];
            var pixels = _partPixels[_currentPart];
            if (pixels.Count == 0) return;

            FrameDataAlgorithms.RotateUV90(pixels, uvs, clockwise);
            SaveWithUndo(clockwise ? "右旋90°UV" : "左旋90°UV");
            Repaint();
        }
        
        /// <summary>
        /// 自动涂色头部和身体（带UV设置）
        /// </summary>
        void AutoPaintCurrentPart()
        {
            if (_sprite == null || _data == null) return;
            
            if (!WithReadableTexture(() =>
            {
                // 备份当前帧挂点，避免被自动检测覆盖
                var originalAnchors = new List<AnchorPoint>();
                foreach (var a in _anchors)
                {
                    originalAnchors.Add(new AnchorPoint
                    {
                        type = a.type,
                        position = a.position,
                        direction = a.direction
                    });
                }

                // 清除当前帧的部位像素/UV，保留方向和变体
                _partPixels.Clear();
                _partUVs.Clear();

                // 自动检测当前帧的所有部位（头/身体/眼睛/手脚）
                AutoDetectAllPartsInternal();

                // 恢复原有挂点，仅更新涂色
                _anchors.Clear();
                _anchors.AddRange(originalAnchors);

                SaveFrameToData(true, "自动涂色");
                _isDirty = false;
            }))
            {
                Debug.LogWarning("Spritesheet 不可读，请在 Import Settings 中启用 Read/Write");
            }
            Repaint();
        }
        
        /// <summary>
        /// 自动涂色全部 + 设置挂点
        /// </summary>
        void AutoPaintAllWithAnchors()
        {
            if (_sprite == null || _data == null) return;
            
            if (!WithReadableTexture(() =>
            {
                AutoDetectAllPartsInternal();
                SaveFrameToData(true, "自动涂色+挂点");
                _isDirty = false;
            }))
            {
                Debug.LogWarning("Spritesheet 不可读，请在 Import Settings 中启用 Read/Write");
            }
            LoadFrameData();
            Repaint();
        }
        
        void AutoDetectAllPartsInternal()
        {
            var p = FrameDataEditorTools.GetDetectParams(_sprite, _row, _frame, _frameSize, _data);
            if (p == null)
            {
                Debug.LogWarning($"帧 [{_row},{_frame}] 找不到皮肤色像素");
                return;
            }
            
            int palW = _data.paletteSize.x;
            int palH = _data.paletteSize.y;
            var defaultFacing = GetDefaultSpriteFacing();
            var headDetectSize = _data.headDetectSize;
            var torsoDetectSize = _data.torsoDetectSize;
            var headUVRegion = _data.headUVRegion;
            var torsoUVRegion = _data.torsoUVRegion;
            
            // 头部 + UV
            var headFacing = _partSpriteFacings.ContainsKey(CharacterBodyPart.Head) ? 
                _partSpriteFacings[CharacterBodyPart.Head] : defaultFacing;
            _partPixels[CharacterBodyPart.Head] = new HashSet<Vector2Int>();
            _partUVs[CharacterBodyPart.Head] = new Dictionary<Vector2Int, Vector2>();
            _corePartPixels[CharacterBodyPart.Head] = new HashSet<Vector2Int>();
            _partSpriteFacings[CharacterBodyPart.Head] = headFacing;
            FillPartWithUV(p.firstPixel, headDetectSize, headUVRegion, CharacterBodyPart.Head, palW, palH);
            
            // 眼睛
            _leftEyeClosed = false;
            _rightEyeClosed = false;
            FrameDataEditorTools.DetectEyes(p, headDetectSize, out var leftEye, out var rightEye,
                out _leftEyeClosed, out _rightEyeClosed);
            if (leftEye.Count > 0)
                _partPixels[CharacterBodyPart.LeftEye] = leftEye;
            if (rightEye.Count > 0)
                _partPixels[CharacterBodyPart.RightEye] = rightEye;
            
            // 身体 + UV
            if (p.torsoStart.HasValue)
            {
                var torsoFacing = _partSpriteFacings.ContainsKey(CharacterBodyPart.Torso) ? 
                    _partSpriteFacings[CharacterBodyPart.Torso] : defaultFacing;
                _partPixels[CharacterBodyPart.Torso] = new HashSet<Vector2Int>();
                _partUVs[CharacterBodyPart.Torso] = new Dictionary<Vector2Int, Vector2>();
                _corePartPixels[CharacterBodyPart.Torso] = new HashSet<Vector2Int>();
                _partSpriteFacings[CharacterBodyPart.Torso] = torsoFacing;
                FillPartWithUV(p.torsoStart.Value, torsoDetectSize, torsoUVRegion, CharacterBodyPart.Torso, palW, palH);
            }
            
            // 手脚
            var leftHand = FrameDataEditorTools.DetectLimb(p, CharacterBodyPart.LeftHand, p.GetLeftHandColor(), _data);
            if (leftHand.Count > 0)
            {
                _partPixels[CharacterBodyPart.LeftHand] = leftHand;
                SetOrUpdateAnchor(AnchorType.MainHandWeapon, leftHand.First());
            }
            
            var rightHand = FrameDataEditorTools.DetectLimb(p, CharacterBodyPart.RightHand, p.GetRightHandColor(), _data);
            if (rightHand.Count > 0)
            {
                _partPixels[CharacterBodyPart.RightHand] = rightHand;
                SetOrUpdateAnchor(AnchorType.OffHandWeapon, rightHand.First());
            }
            
            var leftFoot = FrameDataEditorTools.DetectLimb(p, CharacterBodyPart.LeftFoot, p.GetLeftFootColor(), _data);
            if (leftFoot.Count > 0)
                _partPixels[CharacterBodyPart.LeftFoot] = leftFoot;
            
            var rightFoot = FrameDataEditorTools.DetectLimb(p, CharacterBodyPart.RightFoot, p.GetRightFootColor(), _data);
            if (rightFoot.Count > 0)
                _partPixels[CharacterBodyPart.RightFoot] = rightFoot;
        }
        
        /// <summary>
        /// 全部帧自动涂色 + 挂点
        /// </summary>
        void AutoPaintAllFrames()
        {
            // 直接调用已有的完整实现
            AutoDetectAllFrames();
        }
        
        /// <summary>
        /// 全部帧自动涂色（不修改挂点）
        /// </summary>
        void AutoPaintAllFramesWithoutAnchors()
        {
            if (_sprite == null || _data == null)
            {
                Debug.LogWarning("需要 Spritesheet 和 CharacterFrameData");
                return;
            }

            if (!WithReadableTexture(() =>
            {
                Undo.RecordObject(_data, "Auto Paint All Frames");

                int savedFrame = _frame;
                int savedRow = _row;
                int paintedCount = 0;
                int totalFrames = _rowCount * _framesPerRow;

                for (int r = 0; r < _rowCount; r++)
                {
                    _row = r;
                    for (int f = 0; f < _framesPerRow; f++)
                    {
                        _frame = f;
                        LoadFrameData();

                        // 备份当前帧挂点
                        var originalAnchors = new List<AnchorPoint>();
                        foreach (var a in _anchors)
                        {
                            originalAnchors.Add(new AnchorPoint
                            {
                                type = a.type,
                                position = a.position,
                                direction = a.direction
                            });
                        }

                        // 清除当前帧的部位像素/UV/核心标记，保留方向和变体
                        _partPixels.Clear();
                        _partUVs.Clear();
                        _corePartPixels.Clear();

                        // 自动检测当前帧的所有部位
                        AutoDetectAllPartsInternal();

                        // 恢复挂点，仅更新涂色
                        _anchors.Clear();
                        _anchors.AddRange(originalAnchors);

                        SaveFrameToData(false);
                        if (_partPixels.Count > 0) paintedCount++;
                    }
                }

                _frame = savedFrame;
                _row = savedRow;
                _isDirty = false;
                LoadFrameData();

                EditorUtility.SetDirty(_data);
                Debug.Log($"自动涂色（不含挂点）完成: 共{totalFrames}帧({_rowCount}行×{_framesPerRow}帧), 成功处理{paintedCount}帧");
            }))
            {
                Debug.LogWarning("Spritesheet 不可读，请在 Import Settings 中启用 Read/Write");
            }
        }
        
        /// <summary>
        /// 清除全部帧的涂色数据（不包括挂点）
        /// </summary>
        void ClearAllFramesPaint()
        {
            if (_data == null) return;
            
            var anim = GetCurrentAnimation();
            if (anim == null) return;
            
            Undo.RecordObject(_data, "清除全部帧涂色");
            
            int cleared = 0;
            for (int row = 0; row < anim.rowCount; row++)
            {
                for (int frame = 0; frame < anim.framesPerRow; frame++)
                {
                    var frameData = anim.GetFrame(frame, row);
                    if (frameData != null)
                    {
                        frameData.bodyRegions.Clear();
                        if (frameData.limbMask != null)
                            frameData.limbMask.Clear();
                        frameData.leftEyeClosed = false;
                        frameData.rightEyeClosed = false;
                        cleared++;
                    }
                }
            }
            
            // 清除当前编辑状态
            _partPixels.Clear();
            _partUVs.Clear();
            _partVariants.Clear();
            
            EditorUtility.SetDirty(_data);
            Debug.Log($"[清除] 已清除 {cleared} 帧的涂色数据");
        }
        
        /// <summary>
        /// 清除全部帧的挂点数据
        /// </summary>
        void ClearAllFramesAnchors()
        {
            if (_data == null) return;
            
            var anim = GetCurrentAnimation();
            if (anim == null) return;
            
            Undo.RecordObject(_data, "清除全部帧挂点");
            
            int cleared = 0;
            for (int row = 0; row < anim.rowCount; row++)
            {
                for (int frame = 0; frame < anim.framesPerRow; frame++)
                {
                    var frameData = anim.GetFrame(frame, row);
                    if (frameData != null && frameData.anchors.Count > 0)
                    {
                        frameData.anchors.Clear();
                        cleared++;
                    }
                }
            }
            
            // 清除当前编辑状态
            _anchors.Clear();
            
            EditorUtility.SetDirty(_data);
            Debug.Log($"[清除] 已清除 {cleared} 帧的挂点数据");
        }
        
        /// <summary>
        /// 清除全部帧的方向和贴图变体数据
        /// </summary>
        void ClearAllFramesFacingAndVariant()
        {
            if (_data == null) return;
            
            var anim = GetCurrentAnimation();
            if (anim == null) return;
            
            Undo.RecordObject(_data, "清除全部帧方向和变体");
            
            int cleared = 0;
            for (int row = 0; row < anim.rowCount; row++)
            {
                for (int frame = 0; frame < anim.framesPerRow; frame++)
                {
                    var frameData = anim.GetFrame(frame, row);
                    if (frameData != null)
                    {
                        bool hasChange = false;
                        foreach (var region in frameData.bodyRegions)
                        {
                            if (region.spriteFacing != CharacterFacing.SouthEast || region.variant != FrameVariant.Base)
                            {
                                region.spriteFacing = CharacterFacing.SouthEast;
                                region.variant = FrameVariant.Base;
                                hasChange = true;
                            }
                        }
                        if (hasChange) cleared++;
                    }
                }
            }
            
            // 清除当前编辑状态
            _partSpriteFacings.Clear();
            _partVariants.Clear();
            
            EditorUtility.SetDirty(_data);
            Debug.Log($"[清除] 已清除 {cleared} 帧的方向和贴图变体数据");
        }
        
        /// <summary>
        /// 自动涂色指定部位（带UV设置）
        /// </summary>
        void AutoPaintPart(CharacterBodyPart targetPart)
        {
            if (!_sprite.isReadable || _data == null)
            {
                Debug.LogWarning("需要可读的 Spritesheet 和 CharacterFrameData");
                return;
            }
            
            var p = FrameDataEditorTools.GetDetectParams(_sprite, _row, _frame, _frameSize, _data);
            if (p == null)
            {
                Debug.LogWarning($"帧 [{_row},{_frame}] 找不到皮肤色像素，请检查 DetectConfig 中的颜色设置");
                return;
            }
            
            int palW = _data.paletteSize.x;
            int palH = _data.paletteSize.y;
            var headDetectSize = _data.headDetectSize;
            var torsoDetectSize = _data.torsoDetectSize;
            var headUVRegion = _data.headUVRegion;
            var torsoUVRegion = _data.torsoUVRegion;
            
            switch (targetPart)
            {
                case CharacterBodyPart.Head:
                    _partPixels[CharacterBodyPart.Head] = new HashSet<Vector2Int>();
                    _partUVs[CharacterBodyPart.Head] = new Dictionary<Vector2Int, Vector2>();
                    if (!_partSpriteFacings.ContainsKey(CharacterBodyPart.Head))
                        _partSpriteFacings[CharacterBodyPart.Head] = GetDefaultSpriteFacing();
                    FillPartWithUV(p.firstPixel, headDetectSize, headUVRegion, CharacterBodyPart.Head, palW, palH);
                    
                    // 自动检测眼睛
                    _leftEyeClosed = false;
                    _rightEyeClosed = false;
                    FrameDataEditorTools.DetectEyes(p, headDetectSize, out var leftEye, out var rightEye,
                        out _leftEyeClosed, out _rightEyeClosed);
                    if (leftEye.Count > 0)
                        _partPixels[CharacterBodyPart.LeftEye] = leftEye;
                    if (rightEye.Count > 0)
                        _partPixels[CharacterBodyPart.RightEye] = rightEye;
                    break;
                    
                case CharacterBodyPart.LeftEye:
                case CharacterBodyPart.RightEye:
                    // 眼睛单独检测时
                    {
                        bool leftClosed, rightClosed;
                        FrameDataEditorTools.DetectEyes(p, headDetectSize, out leftEye, out rightEye,
                            out leftClosed, out rightClosed);
                        if (targetPart == CharacterBodyPart.LeftEye)
                        {
                            if (leftEye.Count > 0)
                                _partPixels[CharacterBodyPart.LeftEye] = leftEye;
                            _leftEyeClosed = leftClosed;
                        }
                        if (targetPart == CharacterBodyPart.RightEye)
                        {
                            if (rightEye.Count > 0)
                                _partPixels[CharacterBodyPart.RightEye] = rightEye;
                            _rightEyeClosed = rightClosed;
                        }
                    }
                    break;
                    
                case CharacterBodyPart.Torso:
                    if (p.torsoStart.HasValue)
                    {
                        _partPixels[CharacterBodyPart.Torso] = new HashSet<Vector2Int>();
                        _partUVs[CharacterBodyPart.Torso] = new Dictionary<Vector2Int, Vector2>();
                        if (!_partSpriteFacings.ContainsKey(CharacterBodyPart.Torso))
                            _partSpriteFacings[CharacterBodyPart.Torso] = GetDefaultSpriteFacing();
                        FillPartWithUV(p.torsoStart.Value, torsoDetectSize, torsoUVRegion, CharacterBodyPart.Torso, palW, palH);
                    }
                    break;
                    
                case CharacterBodyPart.LeftHand:
                case CharacterBodyPart.RightHand:
                case CharacterBodyPart.LeftFoot:
                case CharacterBodyPart.RightFoot:
                    // 手脚只需要位置，不需要UV
                    Color32 color = targetPart switch
                    {
                        CharacterBodyPart.LeftHand => p.GetLeftHandColor(),
                        CharacterBodyPart.RightHand => p.GetRightHandColor(),
                        CharacterBodyPart.LeftFoot => p.GetLeftFootColor(),
                        _ => p.GetRightFootColor()
                    };
                    var limbPixels = FrameDataEditorTools.DetectLimb(p, targetPart, color, _data);
                    if (limbPixels.Count > 0)
                    {
                        _partPixels[targetPart] = limbPixels;
                        
                        // 设置武器挂点
                        var pos = limbPixels.First();
                        if (targetPart == CharacterBodyPart.LeftHand)
                            SetOrUpdateAnchor(AnchorType.MainHandWeapon, pos);
                        else if (targetPart == CharacterBodyPart.RightHand)
                            SetOrUpdateAnchor(AnchorType.OffHandWeapon, pos);
                    }
                    break;
                    
                default:
                    return;
            }
        }
        
        
        
        
        /// <summary>
        /// 确保 Texture 可读后执行操作，操作完成后自动恢复
        /// </summary>
        bool WithReadableTexture(System.Action action)
        {
            return TextureReadableScope.Execute(_sprite, tex =>
            {
                _sprite = tex;
                action?.Invoke();
            });
        }
        
        void AutoDetectAllFrames()
        {
            if (_sprite == null || _data == null)
            {
                Debug.LogWarning("需要 Spritesheet 和 CharacterFrameData");
                return;
            }
            
            if (!WithReadableTexture(() =>
            {
                Undo.RecordObject(_data, "Auto Detect All Frames");
                
                int savedFrame = _frame;
                int savedRow = _row;
                int detectedCount = 0;
                int totalFrames = _rowCount * _framesPerRow;
                
                for (int r = 0; r < _rowCount; r++)
                {
                    _row = r;
                    for (int f = 0; f < _framesPerRow; f++)
                    {
                        _frame = f;
                        LoadFrameData();
                        _partPixels.Clear();
                        _partUVs.Clear();
                        _anchors.Clear();
                        
                        AutoDetectAllPartsInternal();
                        SaveFrameToData(false);
                        if (_partPixels.Count > 0) detectedCount++;
                    }
                }
                
                _frame = savedFrame;
                _row = savedRow;
                _isDirty = false;
                LoadFrameData();
                
                EditorUtility.SetDirty(_data);
                Debug.Log($"自动检测完成: 共{totalFrames}帧({_rowCount}行×{_framesPerRow}帧), 成功检测{detectedCount}帧");
            }))
            {
                Debug.LogWarning("Spritesheet 不可读，请在 Import Settings 中启用 Read/Write");
            }
        }
        
        /// <summary>
        /// 修复所有帧的贴图方向（带 Undo 和日志）
        /// 将每个部位的 spriteFacing 修正为对应行的正确值（SE/SW/NE/NW）
        /// </summary>
        void FixAllFramesSpriteFacing()
        {
            if (_data == null)
            {
                Debug.LogWarning("请先选择 CharacterFrameData");
                return;
            }
            
            Undo.RecordObject(_data, "Fix All Frames SpriteFacing");
            
            int fixedCount = FrameDataEditorTools.FixAllFramesSpriteFacing(_data);
            
            EditorUtility.SetDirty(_data);
            LoadFrameData();
            
            Debug.Log($"贴图方向修复完成: 修复了 {fixedCount} 个区域");
        }
        
        
        void SetOrUpdateAnchor(AnchorType type, Vector2Int pos, AnchorDirection direction = default)
        {
            var existing = _anchors.Find(a => a.type == type);
            if (existing != null)
            {
                existing.position = pos;
                existing.direction = direction;
            }
            else
            {
                _anchors.Add(new AnchorPoint { type = type, position = pos, direction = direction });
            }
        }
        
        Color32 GetPixelAt(Color32[] pixels, int x, int y)
        {
            int gx = _frame * _frameSize.x + x;
            int gy = _sprite.height - 1 - (_row * _frameSize.y + y);
            
            if (gx < 0 || gx >= _sprite.width || gy < 0 || gy >= _sprite.height)
                return default;
            
            return pixels[gy * _sprite.width + gx];
        }
        
        /// <summary>
        /// 从 SE 方向一键生成所有行（SW/NE/NW）的部位数据
        /// </summary>
        void GenerateAllRowsFromSE()
        {
            if (_data == null || string.IsNullOrEmpty(_animName))
            {
                Debug.LogWarning("请先选择 CharacterFrameData 和动画");
                return;
            }
            
            SaveIfDirty();
            var anim = GetCurrentAnimation();
            if (anim == null) return;
            
            FrameDataEditorTools.GenerateAllRowsFromSE(_data, anim, _framesPerRow, _frameSize);
            
            _isDirty = false;
            LoadFrameData();
        }
        
        
        // 算法委托到 FrameDataAlgorithms
        Vector2Int MirrorPosition(Vector2Int pos) => FrameDataAlgorithms.MirrorPosition(pos, _frameSize.x);
        bool IsLimbPart(CharacterBodyPart part) => FrameDataAlgorithms.IsLimbPart(part);
        CharacterFacing MirrorSpriteFacing(CharacterFacing facing) => FrameDataAlgorithms.MirrorSpriteFacing(facing);
        CharacterFacing SouthToNorth(CharacterFacing facing) => FrameDataAlgorithms.SouthToNorth(facing);
        
        #endregion
        
        #region UV Map 生成 (双层)

        void GenerateDualUVMapsForCurrentAnimation()
        {
            if (_data == null || string.IsNullOrEmpty(_animName))
            {
                Debug.LogWarning("[UV Map] 请先选择 CharacterFrameData 和动画");
                return;
            }

            var anim = GetCurrentAnimation();
            if (anim == null)
            {
                Debug.LogWarning($"[UV Map] 未找到当前动画");
                return;
            }
            
            DualUVMapGenerator.GenerateDualUVMapsForAnimation(_data, anim);
            AssetDatabase.Refresh();
        }

        void GenerateAllDualUVMaps()
        {
            if (_data == null)
            {
                Debug.LogWarning("[UV Map] 请先选择 CharacterFrameData");
                return;
            }

            int count = DualUVMapGenerator.GenerateAllDualUVMaps(_data);
            AssetDatabase.Refresh();
            Debug.Log($"[UV Map] 已生成 {count} 个动画的双层 UV Map");
        }

        #endregion
        
        #region 辅助方法
        
        string GetPartName(CharacterBodyPart part)
        {
            switch (part)
            {
                case CharacterBodyPart.Head: return "头部";
                case CharacterBodyPart.Torso: return "身体";
                case CharacterBodyPart.LeftHand: return "左手";
                case CharacterBodyPart.RightHand: return "右手";
                case CharacterBodyPart.LeftFoot: return "左脚";
                case CharacterBodyPart.RightFoot: return "右脚";
                case CharacterBodyPart.LeftEye: return "左眼";
                case CharacterBodyPart.RightEye: return "右眼";
                default: return part.ToString();
            }
        }
        
        #endregion
    }
}
