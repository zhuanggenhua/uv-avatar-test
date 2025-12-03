using System.Collections.Generic;
using System.IO;
using System.Linq;

using EquipmentSystem.Data;

using UnityEditor;
using UnityEngine;

// 使用别名避免与 UnityEditor.BodyPart 冲突
using CharacterBodyPart = EquipmentSystem.Data.CharacterBodyPart;

namespace EquipmentSystem.Editor
{
    public enum TabMode { BodyPaint, Anchor }

    public class FrameDataEditor : EditorWindow
    {
        #region 字段
        
        [SerializeField] CharacterFrameData _data;
        [SerializeField] Texture2D _sprite;
        
        // 编辑状态
        [SerializeField] int _animIndex;
        string _animName = "Idle";
        int _row, _frame;
        TabMode _tab = TabMode.BodyPaint;
        CharacterBodyPart _currentPart = CharacterBodyPart.Torso;
        AnchorType _anchorType = AnchorType.LeftWeapon;
        AnchorDirection _anchorDirection = AnchorDirection.East;
        bool _showSkinColors;
        bool _showHeadExpandConfig;
        bool _showBodyExpandConfig;
        int _paintDisplayMode = 2;  // 0=隐藏, 1=当前, 2=全部
        
        // 视图
        Vector2 _scroll, _pan;
        float _zoom = 10f;
        Vector2Int _frameSize = new Vector2Int(32, 32);
        int _framesPerRow = 8, _rowCount = 4;
        bool _panning;
        Vector2 _lastMouse;
        Rect _canvas, _display;
        Vector2 _canvasOffset;
        
        // UV 画板
        float _paletteZoom = 8f;        // 画板缩放
        Vector2 _palettePan;            // 画板平移
        Rect _paletteDisplayRect;       // 画板显示区域
        Rect _paletteCanvasRect;        // 画板画布区域（用于输入检测）
        bool _showPalette = true;       // 是否显示画板
        
        // 选区
        bool _isSelecting;              // 是否正在框选
        bool _selectOnPalette;          // 选区在画板上还是画布上
        bool _isErasing;                // 是否正在擦除模式
        bool _isRightDragging;          // 是否正在右键拖动删除
        Vector2Int _selectionStart;     // 选区起始点
        Vector2Int _selectionEnd;       // 选区结束点
        RectInt _paletteSelection;      // 画板选区（已确定）
        RectInt _canvasSelection;       // 画布选区（已确定）
        
        // 编辑模式
        bool _editMode;                 // 编辑模式开关
        bool _showPaletteConfig;        // UV画板配置折叠
        bool _hideCanvasSprite;         // 隐藏画布角色原图
        bool _hidePaletteSprite;        // 隐藏画板底图
        Vector2Int? _hoverPalettePixel; // 当前鼠标悬停的画板像素
        Vector2Int? _hoverCanvasPixel;  // 当前鼠标悬停的画布像素
        
        // 编辑缓存
        Dictionary<CharacterBodyPart, HashSet<Vector2Int>> _partPixels = new Dictionary<CharacterBodyPart, HashSet<Vector2Int>>();
        Dictionary<CharacterBodyPart, Dictionary<Vector2Int, Vector2>> _partUVs = new Dictionary<CharacterBodyPart, Dictionary<Vector2Int, Vector2>>();
        Dictionary<CharacterBodyPart, CharacterFacing> _partSpriteFacings = new Dictionary<CharacterBodyPart, CharacterFacing>();
        Dictionary<CharacterBodyPart, FrameVariant> _partVariants = new Dictionary<CharacterBodyPart, FrameVariant>();
        List<AnchorPoint> _anchors = new List<AnchorPoint>();
        
        // 脏标记 - 只在有修改时保存
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
            float toolbarWidth = Mathf.Clamp(position.width * 0.28f, 320f, 400f);
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
                EditorUtility.SetDirty(_data);
            
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
                "• Shift+左键拖拽: 框选区域\n" +
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
            
            // 当前部位信息（大字体 + 部位颜色，提亮显示）
            int count = _partPixels.ContainsKey(_currentPart) ? _partPixels[_currentPart].Count : 0;
            var partColor = GetPartColor(_currentPart);
            // 提亮颜色：将颜色向白色混合，使其更明亮
            var brightColor = Color.Lerp(partColor, Color.white, 0.4f);
            var prevColor = GUI.contentColor;
            GUI.contentColor = brightColor;
            GUILayout.Label($"当前: {GetPartName(_currentPart)} ({count}像素)", new GUIStyle(EditorStyles.boldLabel) { fontSize = 16 });
            GUI.contentColor = prevColor;
            
            // 贴图方向与变体设置（只对 UV 部位：头/身体）
            if (!IsLimbPart(_currentPart))
            {
                // 确保有默认值
                if (!_partSpriteFacings.ContainsKey(_currentPart))
                    _partSpriteFacings[_currentPart] = GetDefaultSpriteFacing();
                
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("贴图方向:", GUILayout.Width(60));
                var newFacing = (CharacterFacing)EditorGUILayout.EnumPopup(_partSpriteFacings[_currentPart]);
                if (newFacing != _partSpriteFacings[_currentPart])
                {
                    _partSpriteFacings[_currentPart] = newFacing;
                    SaveWithUndo("设置贴图方向");
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
            
            // === 涂色显示 ===
            GUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("涂色:", GUILayout.Width(35));
            _paintDisplayMode = GUILayout.Toolbar(_paintDisplayMode, new[] { "隐藏", "当前", "全部" });
            EditorGUILayout.EndHorizontal();
            
            // === 显示选项 ===
            GUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            _hideCanvasSprite = GUILayout.Toggle(_hideCanvasSprite, "隐藏角色", GUILayout.Width(70));
            _hidePaletteSprite = GUILayout.Toggle(_hidePaletteSprite, "隐藏底图", GUILayout.Width(70));
            EditorGUILayout.EndHorizontal();
            
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
                    if (_partVariants.ContainsKey(_currentPart))
                        _partVariants[_currentPart] = FrameVariant.Base;
                    SaveWithUndo("清除部位");
                }
                
                // UV镜像（仅头部/身体有UV）
                bool isLimb = _currentPart == CharacterBodyPart.LeftHand || _currentPart == CharacterBodyPart.RightHand ||
                              _currentPart == CharacterBodyPart.LeftFoot || _currentPart == CharacterBodyPart.RightFoot ||
                              _currentPart == CharacterBodyPart.LeftEye || _currentPart == CharacterBodyPart.RightEye;
                if (!isLimb && _partUVs.ContainsKey(_currentPart) && _partUVs[_currentPart].Count > 0)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("UV镜像:", GUILayout.Width(50));
                    if (GUILayout.Button("水平")) MirrorCurrentPartUV(true);
                    if (GUILayout.Button("垂直")) MirrorCurrentPartUV(false);
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
            
            // === 自动涂色 ===
            GUILayout.Space(10);
            GUILayout.Label("自动涂色", EditorStyles.boldLabel);
            
            if (GUILayout.Button("🎨 自动涂色（当前帧）", GUILayout.Height(25)))
                AutoPaintCurrentPart();
            if (GUILayout.Button("🎨 自动涂色 + 挂点（当前帧）", GUILayout.Height(25)))
                AutoPaintAllWithAnchors();
            if (GUILayout.Button("🎨 自动涂色 + 挂点（全部帧）", GUILayout.Height(25)))
                AutoPaintAllFrames();
            
            GUILayout.Space(3);
            GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
            if (GUILayout.Button("清除当前帧涂色"))
            {
                _partPixels.Clear();
                _partUVs.Clear();
                _partVariants.Clear();
                SaveWithUndo("清除当前帧涂色");
            }
            if (GUILayout.Button("清除全部帧涂色"))
            {
                ClearAllFramesPaint();
            }
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
                
                // 武器握点配置（UV画板像素坐标）
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("武器握点（UV画板像素）", EditorStyles.miniBoldLabel);
                _data.rightHandWeaponPivot = EditorGUILayout.Vector2IntField("右手握点", _data.rightHandWeaponPivot);
                _data.leftHandWeaponPivot = EditorGUILayout.Vector2IntField("左手握点", _data.leftHandWeaponPivot);
                EditorGUILayout.HelpBox("武器贴图基于UV底图绘制，握点对应手部位置", MessageType.None);
                
                // 检测目标区域大小（角色实际区域）
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("检测目标区域（角色）", EditorStyles.miniBoldLabel);
                _data.headDetectSize = EditorGUILayout.Vector2IntField("头部大小", _data.headDetectSize);
                _data.torsoDetectSize = EditorGUILayout.Vector2IntField("身体大小", _data.torsoDetectSize);
                
                if (EditorGUI.EndChangeCheck())
                    EditorUtility.SetDirty(_data);
                
                EditorGUILayout.HelpBox("检测区域 > UV区域时，边缘UV会自动复制填充", MessageType.Info);
                EditorGUI.indentLevel--;
            }
        }
        
        void DrawAnchorTab()
        {
            GUILayout.Label("锚点设置", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("用于挂件定位（头盔、武器等）", MessageType.Info);
            
            _anchorType = (AnchorType)EditorGUILayout.EnumPopup("锚点类型", _anchorType);
            _anchorDirection = (AnchorDirection)EditorGUILayout.EnumPopup("武器方向", _anchorDirection);
            
            GUILayout.Space(5);
            GUILayout.Label("已有锚点:", EditorStyles.miniLabel);
            
            for (int i = _anchors.Count - 1; i >= 0; i--)
            {
                var a = _anchors[i];
                EditorGUILayout.BeginHorizontal();
                GUI.color = a.type == _anchorType ? Color.yellow : Color.white;
                if (GUILayout.Button(a.type.ToString(), EditorStyles.miniButtonLeft, GUILayout.Width(80)))
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
                
                GUILayout.Label("手脚颜色 (用于自动检测):", EditorStyles.miniLabel);
                c.leftHandColor = EditorGUILayout.ColorField("左手", c.leftHandColor);
                c.rightHandColor = EditorGUILayout.ColorField("右手", c.rightHandColor);
                c.leftFootColor = EditorGUILayout.ColorField("左脚", c.leftFootColor);
                c.rightFootColor = EditorGUILayout.ColorField("右脚", c.rightFootColor);
                
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
            if (GUILayout.Button("🔧 修复所有帧贴图方向"))
                FixAllFramesSpriteFacing();
            
            GUILayout.Space(5);
            GUILayout.Label("区域扩展", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("基于边界像素的 UV 向外扩展\n先上下、再左右，扩展像素继承边界 UV", MessageType.Info);
            
            // 头部扩展配置
            _showHeadExpandConfig = EditorGUILayout.Foldout(_showHeadExpandConfig, "头部扩展配置", true);
            if (_showHeadExpandConfig && _data != null)
            {
                EditorGUI.indentLevel++;
                _data.headExpandUp = EditorGUILayout.IntSlider("向上扩展", _data.headExpandUp, 0, 10);
                _data.headExpandSide = EditorGUILayout.IntSlider("左右扩展", _data.headExpandSide, 0, 10);
                _data.headExpandDown = EditorGUILayout.IntSlider("向下扩展", _data.headExpandDown, 0, 10);
                EditorGUI.indentLevel--;
            }
            
            // 身体扩展配置
            _showBodyExpandConfig = EditorGUILayout.Foldout(_showBodyExpandConfig, "身体扩展配置", true);
            if (_showBodyExpandConfig && _data != null)
            {
                EditorGUI.indentLevel++;
                _data.bodyExpandUp = EditorGUILayout.IntSlider("向上扩展", _data.bodyExpandUp, 0, 10);
                _data.bodyExpandSide = EditorGUILayout.IntSlider("左右扩展", _data.bodyExpandSide, 0, 10);
                _data.bodyExpandDown = EditorGUILayout.IntSlider("向下扩展", _data.bodyExpandDown, 0, 10);
                EditorGUI.indentLevel--;
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
            
            GUILayout.Space(5);
            GUILayout.Label("方向数据生成", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("从SE行数据生成其他行：\n• SW/NW = SE水平镜像（左右互换、贴图方向镜像）\n• NE = SE复制", MessageType.Info);
            
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
            
            // 4. 绘制自动涂色UV区域标注
            DrawUVRegionMarkers();
            
            // 5. 绘制选区和悬停高亮
            DrawPaletteSelection();
            
            // 6. 绘制网格线
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
            
            // 7. 标签 - 显示悬停坐标
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
            
            if (!sizeMatch && !singlePalette)
            {
                Debug.LogWarning($"选区大小不匹配: 画板 {_paletteSelection.width}×{_paletteSelection.height}, 画布 {_canvasSelection.width}×{_canvasSelection.height}");
                return;
            }
            
            if (!_partPixels.ContainsKey(_currentPart))
                _partPixels[_currentPart] = new HashSet<Vector2Int>();
            if (!_partUVs.ContainsKey(_currentPart))
                _partUVs[_currentPart] = new Dictionary<Vector2Int, Vector2>();
            
            var pixels = _partPixels[_currentPart];
            var uvs = _partUVs[_currentPart];
            
            int palW = _data != null ? _data.paletteSize.x : 32;
            int palH = _data != null ? _data.paletteSize.y : 32;
            
            for (int dy = 0; dy < _canvasSelection.height; dy++)
            {
                for (int dx = 0; dx < _canvasSelection.width; dx++)
                {
                    // 画布目标像素
                    int dstX = _canvasSelection.x + dx;
                    int dstY = _canvasSelection.y + dy;
                    var dstPos = new Vector2Int(dstX, dstY);
                    
                    if (!IsValidPixel(dstPos)) continue;
                    
                    // 画板源像素的UV（画板绝对坐标）
                    int srcX = _paletteSelection.x + (singlePalette ? 0 : dx);
                    int srcY = _paletteSelection.y + (singlePalette ? 0 : dy);
                    float u = (srcX + 0.5f) / palW;
                    float v = 1f - (srcY + 0.5f) / palH;
                    Vector2 uv = new Vector2(u, v);
                    
                    pixels.Add(dstPos);
                    uvs[dstPos] = uv;
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
            
            int removed = 0;
            for (int dy = 0; dy < _canvasSelection.height; dy++)
            {
                for (int dx = 0; dx < _canvasSelection.width; dx++)
                {
                    var pos = new Vector2Int(_canvasSelection.x + dx, _canvasSelection.y + dy);
                    if (pixels.Remove(pos))
                    {
                        uvs?.Remove(pos);
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
        /// 先身体后头部，身体向上扩展时避开本体正上方（形成凹字形）
        /// </summary>
        void ExpandAllPartsRegion()
        {
            if (_data == null) return;
            
            // 记录身体本体的边界（扩展前）
            int torsoMinX = int.MaxValue, torsoMaxX = int.MinValue, torsoMinY = int.MaxValue;
            if (_partPixels.ContainsKey(CharacterBodyPart.Torso))
            {
                foreach (var pos in _partPixels[CharacterBodyPart.Torso])
                {
                    torsoMinX = Mathf.Min(torsoMinX, pos.x);
                    torsoMaxX = Mathf.Max(torsoMaxX, pos.x);
                    torsoMinY = Mathf.Min(torsoMinY, pos.y);
                }
            }
            
            // 1. 先扩展身体
            ExpandPartRegion(CharacterBodyPart.Torso, _data.bodyExpandUp, _data.bodyExpandDown, _data.bodyExpandSide);
            
            // 2. 从身体中移除本体正上方的像素（形成凹字形）
            if (_partPixels.ContainsKey(CharacterBodyPart.Torso) && torsoMinX != int.MaxValue)
            {
                var toRemove = new List<Vector2Int>();
                foreach (var pos in _partPixels[CharacterBodyPart.Torso])
                {
                    // 在身体本体X范围内，且在身体本体上方
                    if (pos.x >= torsoMinX && pos.x <= torsoMaxX && pos.y < torsoMinY)
                        toRemove.Add(pos);
                }
                foreach (var pos in toRemove)
                {
                    _partPixels[CharacterBodyPart.Torso].Remove(pos);
                    if (_partUVs.ContainsKey(CharacterBodyPart.Torso))
                        _partUVs[CharacterBodyPart.Torso].Remove(pos);
                }
            }
            
            // 3. 再扩展头部
            ExpandPartRegion(CharacterBodyPart.Head, _data.headExpandUp, _data.headExpandDown, _data.headExpandSide);
            
            SaveWithUndo("扩展区域");
            Repaint();
        }
        
        void ExpandPartRegion(CharacterBodyPart part, int expandUp, int expandDown, int expandSide)
        {
            if (!_partPixels.ContainsKey(part) || _partPixels[part].Count == 0) return;
            if (expandUp == 0 && expandDown == 0 && expandSide == 0) return;
            
            var pixels = _partPixels[part];
            if (!_partUVs.ContainsKey(part))
                _partUVs[part] = new Dictionary<Vector2Int, Vector2>();
            var uvs = _partUVs[part];
            
            var paletteSize = _data != null ? _data.paletteSize : new Vector2Int(32, 32);
            FrameDataAlgorithms.ExpandRegionWithBoundaryUV(pixels, uvs, expandUp, expandDown, expandSide, expandSide, _frameSize, paletteSize);
        }
        
        /// <summary>
        /// 扩展所有部位的区域（全部帧）- 并行优化版
        /// </summary>
        void ExpandAllPartsForAllFrames()
        {
            if (_data == null) return;
            
            SaveIfDirty();
            Undo.RecordObject(_data, "扩展全部帧区域");
            
            var anim = GetCurrentAnimation();
            if (anim == null) return;
            
            // 收集所有帧
            var frames = new List<FrameData>();
            for (int r = 0; r < _rowCount; r++)
                for (int f = 0; f < _framesPerRow; f++)
                    if (anim.GetFrame(f, r) is FrameData frame && frame.bodyRegions.Count > 0)
                        frames.Add(frame);
            
            // 并行扩展
            var paletteSize = _data.paletteSize;
            var expandParams = (_data.headExpandUp, _data.headExpandDown, _data.headExpandSide,
                               _data.bodyExpandUp, _data.bodyExpandDown, _data.bodyExpandSide);
            int expandedCount = 0;
            
            System.Threading.Tasks.Parallel.ForEach(frames, frame =>
            {
                if (ExpandFrameDataDirectly(frame, paletteSize, expandParams))
                    System.Threading.Interlocked.Increment(ref expandedCount);
            });
            
            _isDirty = false;
            LoadFrameData();
            EditorUtility.SetDirty(_data);
            Debug.Log($"区域扩展完成: 扩展了 {expandedCount} 帧");
        }
        
        bool ExpandFrameDataDirectly(FrameData frame, Vector2Int paletteSize,
            (int headUp, int headDown, int headSide, int bodyUp, int bodyDown, int bodySide) p)
        {
            bool anyExpanded = false;
            
            var headRegion = frame.GetRegion(CharacterBodyPart.Head);
            var torsoRegion = frame.GetRegion(CharacterBodyPart.Torso);
            
            // 记录身体本体的边界（扩展前）
            int torsoMinX = int.MaxValue, torsoMaxX = int.MinValue, torsoMinY = int.MaxValue;
            if (torsoRegion != null)
            {
                foreach (var px in torsoRegion.pixels)
                {
                    torsoMinX = Mathf.Min(torsoMinX, px.position.x);
                    torsoMaxX = Mathf.Max(torsoMaxX, px.position.x);
                    torsoMinY = Mathf.Min(torsoMinY, px.position.y);
                }
            }
            
            // 1. 先扩展身体
            if (torsoRegion != null && (p.bodyUp > 0 || p.bodyDown > 0 || p.bodySide > 0))
            {
                var pixels = new HashSet<Vector2Int>(torsoRegion.pixels.Select(px => px.position));
                var uvs = torsoRegion.pixels.Where(px => px.HasUV).ToDictionary(px => px.position, px => px.uv);
                int before = pixels.Count;
                
                FrameDataAlgorithms.ExpandRegionWithBoundaryUV(pixels, uvs, p.bodyUp, p.bodyDown, p.bodySide, p.bodySide, _frameSize, paletteSize);
                
                // 2. 从身体中移除本体正上方的像素（形成凹字形）
                if (torsoMinX != int.MaxValue)
                {
                    var toRemove = pixels.Where(pos => pos.x >= torsoMinX && pos.x <= torsoMaxX && pos.y < torsoMinY).ToList();
                    foreach (var pos in toRemove)
                    {
                        pixels.Remove(pos);
                        uvs.Remove(pos);
                    }
                }
                
                if (pixels.Count != before)
                {
                    lock (torsoRegion.pixels)
                    {
                        torsoRegion.pixels.Clear();
                        foreach (var pos in pixels)
                            torsoRegion.pixels.Add(new BodyPartPixel { part = CharacterBodyPart.Torso, position = pos, 
                                uv = uvs.TryGetValue(pos, out var uv) ? uv : default });
                    }
                    anyExpanded = true;
                }
            }
            
            // 3. 再扩展头部
            if (headRegion != null && (p.headUp > 0 || p.headDown > 0 || p.headSide > 0))
            {
                var pixels = new HashSet<Vector2Int>(headRegion.pixels.Select(px => px.position));
                var uvs = headRegion.pixels.Where(px => px.HasUV).ToDictionary(px => px.position, px => px.uv);
                int before = pixels.Count;
                
                FrameDataAlgorithms.ExpandRegionWithBoundaryUV(pixels, uvs, p.headUp, p.headDown, p.headSide, p.headSide, _frameSize, paletteSize);
                
                if (pixels.Count > before)
                {
                    lock (headRegion.pixels)
                    {
                        headRegion.pixels.Clear();
                        foreach (var pos in pixels)
                            headRegion.pixels.Add(new BodyPartPixel { part = CharacterBodyPart.Head, position = pos, 
                                uv = uvs.TryGetValue(pos, out var uv) ? uv : default });
                    }
                    anyExpanded = true;
                }
            }
            
            return anyExpanded;
        }
        
        /// <summary>
        /// 收缩所有部位的区域（当前帧）
        /// </summary>
        void ShrinkAllPartsRegion()
        {
            if (_data == null) return;
            
            ShrinkPartRegion(CharacterBodyPart.Head, _data.headExpandUp, _data.headExpandDown, _data.headExpandSide);
            ShrinkPartRegion(CharacterBodyPart.Torso, _data.bodyExpandUp, _data.bodyExpandDown, _data.bodyExpandSide);
            
            SaveWithUndo("收缩区域");
            Repaint();
        }
        
        void ShrinkPartRegion(CharacterBodyPart part, int shrinkUp, int shrinkDown, int shrinkSide)
        {
            if (!_partPixels.ContainsKey(part) || _partPixels[part].Count == 0) return;
            
            var pixels = _partPixels[part];
            var uvs = _partUVs.ContainsKey(part) ? _partUVs[part] : null;
            
            FrameDataAlgorithms.ShrinkRegion(pixels, uvs, shrinkUp, shrinkDown, shrinkSide, shrinkSide);
        }
        
        /// <summary>
        /// 收缩所有部位的区域（全部帧）- 并行优化版
        /// </summary>
        void ShrinkAllPartsForAllFrames()
        {
            if (_data == null) return;
            
            SaveIfDirty();
            Undo.RecordObject(_data, "收缩全部帧区域");
            
            var anim = GetCurrentAnimation();
            if (anim == null) return;
            
            // 收集所有帧
            var frames = new List<FrameData>();
            for (int r = 0; r < _rowCount; r++)
                for (int f = 0; f < _framesPerRow; f++)
                    if (anim.GetFrame(f, r) is FrameData frame && frame.bodyRegions.Count > 0)
                        frames.Add(frame);
            
            // 并行收缩
            var shrinkParams = (_data.headExpandUp, _data.headExpandDown, _data.headExpandSide,
                               _data.bodyExpandUp, _data.bodyExpandDown, _data.bodyExpandSide);
            int shrunkCount = 0;
            
            System.Threading.Tasks.Parallel.ForEach(frames, frame =>
            {
                if (ShrinkFrameDataDirectly(frame, shrinkParams))
                    System.Threading.Interlocked.Increment(ref shrunkCount);
            });
            
            _isDirty = false;
            LoadFrameData();
            EditorUtility.SetDirty(_data);
            Debug.Log($"区域收缩完成: 收缩了 {shrunkCount} 帧");
        }
        
        bool ShrinkFrameDataDirectly(FrameData frame,
            (int headUp, int headDown, int headSide, int bodyUp, int bodyDown, int bodySide) p)
        {
            bool anyShrunk = false;
            foreach (var region in frame.bodyRegions)
            {
                if (region.part != CharacterBodyPart.Head && region.part != CharacterBodyPart.Torso) continue;
                
                var (up, down, side) = region.part == CharacterBodyPart.Head
                    ? (p.headUp, p.headDown, p.headSide)
                    : (p.bodyUp, p.bodyDown, p.bodySide);
                if (up == 0 && down == 0 && side == 0) continue;
                
                var pixels = new HashSet<Vector2Int>(region.pixels.Select(px => px.position));
                var uvs = region.pixels.Where(px => px.HasUV).ToDictionary(px => px.position, px => px.uv);
                int before = pixels.Count;
                
                FrameDataAlgorithms.ShrinkRegion(pixels, uvs, up, down, side, side);
                
                if (pixels.Count < before)
                {
                    lock (region.pixels)
                    {
                        region.pixels.Clear();
                        foreach (var pos in pixels)
                            region.pixels.Add(new BodyPartPixel { part = region.part, position = pos, 
                                uv = uvs.TryGetValue(pos, out var uv) ? uv : default });
                    }
                    anyShrunk = true;
                }
            }
            return anyShrunk;
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
                                else if (_paletteSelection.width > 0 && _paletteSelection.size == _canvasSelection.size)
                                {
                                    // 编辑模式下，自动复制UV（画布选区大小与画板选区匹配时）
                                    CopyUVFromPaletteToCanvas();
                                    _canvasSelection = default;  // 复制后清除画布选区
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
            // 获取像素集合
            HashSet<Vector2Int> partPixels = null;
            if (_partPixels.ContainsKey(part))
                partPixels = _partPixels[part];
            
            // 如果没有像素、没有设置过贴图方向、也没有设置变体，跳过
            bool hasPixels = partPixels != null && partPixels.Count > 0;
            bool hasFacing = _partSpriteFacings.ContainsKey(part);
            bool hasVariant = _partVariants.ContainsKey(part) && _partVariants[part] != FrameVariant.Base;
            
            if (!hasPixels && !hasFacing && !hasVariant)
                return;
            
            // 收集手脚像素（用于排除）
            // 注意：眼睛不排除！眼睛是头部的一部分，仍需参与头部 UV 映射（头发/胡子/头盔等）
            HashSet<Vector2Int> limbPixels = new HashSet<Vector2Int>();
            foreach (var limbPart in new[] { CharacterBodyPart.LeftHand, CharacterBodyPart.RightHand, 
                                              CharacterBodyPart.LeftFoot, CharacterBodyPart.RightFoot })
            {
                if (_partPixels.ContainsKey(limbPart))
                    limbPixels.UnionWith(_partPixels[limbPart]);
            }
            
            var variant = FrameVariant.Base;
            if (_partVariants.ContainsKey(part))
                variant = _partVariants[part];

            var region = new BodyPartRegion
            {
                part = part,
                orientation = UVOrientation.UpRight,
                spriteFacing = hasFacing ? _partSpriteFacings[part] : GetDefaultSpriteFacing(),
                variant = variant
            };
            
            // 保存像素（排除手脚像素，手脚有更高优先级）
            if (hasPixels)
            {
                Dictionary<Vector2Int, Vector2> uvDict = null;
                if (_partUVs.ContainsKey(part))
                    uvDict = _partUVs[part];
                
                foreach (var pos in partPixels)
                {
                    // 跳过手脚像素（手脚优先级更高，保存在 limbMask 中）
                    if (limbPixels.Contains(pos))
                        continue;
                    
                    int gx = _frame * _frameSize.x + pos.x;
                    int gy = _sprite.height - 1 - (_row * _frameSize.y + pos.y);
                    
                    if (gx >= 0 && gx < _sprite.width && gy >= 0 && gy < _sprite.height)
                    {
                        var pixel = new BodyPartPixel
                        {
                            part = part,
                            position = pos,
                            color = pixels[gy * _sprite.width + gx]
                        };
                        
                        if (uvDict != null && uvDict.ContainsKey(pos))
                            pixel.uv = uvDict[pos];
                        
                        region.pixels.Add(pixel);
                    }
                }
            }
            
            frame.bodyRegions.Add(region);
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
            _isDirty = false;
            
            if (_data == null) return;
            
            var anim = GetCurrentAnimation();
            if (anim == null) return;
            
            var frame = anim.GetFrame(_frame, _row);
            if (frame == null) return;
            
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
            
            // 加载 UV 部位（头/身体）
            foreach (var region in frame.bodyRegions)
            {
                _partSpriteFacings[region.part] = region.spriteFacing;
                _partPixels[region.part] = new HashSet<Vector2Int>();
                _partUVs[region.part] = new Dictionary<Vector2Int, Vector2>();
                _partVariants[region.part] = region.variant;
                foreach (var px in region.pixels)
                {
                    _partPixels[region.part].Add(px.position);
                    if (px.HasUV)
                        _partUVs[region.part][px.position] = px.uv;
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
        /// 获取检测参数（头部位置、颜色映射等）
        /// </summary>
        DetectParams GetDetectParams()
        {
            if (_sprite == null || !_sprite.isReadable || _data == null)
                return null;
            
            var cfg = _data.detectConfig;
            var pixels = _sprite.GetPixels32();
            
            // 判断朝向
            bool facingRight = (_row == 0 || _row == 2);  // SE/NE
            
            // 查找第一个皮肤色像素（排除武器等非皮肤颜色）
            Vector2Int? firstPixel = null;
            for (int y = 0; y < _frameSize.y && !firstPixel.HasValue; y++)
            {
                for (int x = 0; x < _frameSize.x; x++)
                {
                    var c = GetPixelAt(pixels, x, y);
                    if (cfg.IsSkinLike(c))
                    {
                        firstPixel = new Vector2Int(x, y);
                        break;
                    }
                }
            }
            
            if (!firstPixel.HasValue) return null;
            
            // 查找躯干起始点（在头部下面，使用配置的头部高度）
            var headDetectSize = _data != null ? _data.headDetectSize : new Vector2Int(4, 3);
            int torsoRowY = firstPixel.Value.y + headDetectSize.y;
            int headLeft = firstPixel.Value.x;  // 头部最左列
            Vector2Int? torsoStart = null;
            
            if (torsoRowY < _frameSize.y)
            {
                // 从头部最左列开始查找（躯干起点 >= 头部最左列）
                for (int x = headLeft; x < _frameSize.x; x++)
                {
                    if (cfg.IsColoredPixel(GetPixelAt(pixels, x, torsoRowY)))
                    {
                        torsoStart = new Vector2Int(x, torsoRowY);
                        break;
                    }
                }
            }
            
            return new DetectParams
            {
                pixels = pixels,
                cfg = cfg,
                facingRight = facingRight,
                firstPixel = firstPixel.Value,
                torsoStart = torsoStart,
                headLeft = headLeft,
                headRight = headLeft + headDetectSize.x,
                footMinY = torsoStart.HasValue ? torsoStart.Value.y + (_data != null ? _data.torsoDetectSize.y : 2) : _frameSize.y
            };
        }
        
        class DetectParams
        {
            public Color32[] pixels;
            public DetectConfig cfg;
            public bool facingRight;
            public Vector2Int firstPixel;
            public Vector2Int? torsoStart;
            public int headLeft, headRight, footMinY;
            
            // 颜色映射（SE方向固定，其他方向由SE生成）
            public Color32 GetLeftHandColor() => cfg.leftHandColor;
            public Color32 GetRightHandColor() => cfg.rightHandColor;
            public Color32 GetLeftFootColor() => cfg.leftFootColor;
            public Color32 GetRightFootColor() => cfg.rightFootColor;
        }
        
        void AutoDetectAllParts()
        {
            if (_sprite == null || _data == null)
            {
                Debug.LogWarning("需要 Spritesheet 和 CharacterFrameData");
                return;
            }
            
            if (!WithReadableTexture(AutoDetectAllPartsInternal))
                Debug.LogWarning("Spritesheet 不可读，请在 Import Settings 中启用 Read/Write");
        }
        
        void AutoDetectAllPartsInternal()
        {
            var p = GetDetectParams();
            if (p == null)
            {
                Debug.LogWarning($"帧 [{_row},{_frame}] 找不到皮肤色像素，请检查 DetectConfig 中的颜色设置");
                return;
            }
            
            int palW = _data != null ? _data.paletteSize.x : 32;
            int palH = _data != null ? _data.paletteSize.y : 32;
            var defaultFacing = GetDefaultSpriteFacing();
            
            // 头部 + UV
            var headFacing = _partSpriteFacings.ContainsKey(CharacterBodyPart.Head) ? _partSpriteFacings[CharacterBodyPart.Head] : defaultFacing;
            var headDetectSize = _data != null ? _data.headDetectSize : new Vector2Int(4, 3);
            var torsoDetectSize = _data != null ? _data.torsoDetectSize : new Vector2Int(3, 2);
            var headUVRegion = _data != null ? _data.headUVRegion : new RectInt(0, 0, 4, 3);
            var torsoUVRegion = _data != null ? _data.torsoUVRegion : new RectInt(0, 3, 3, 2);
            
            _partPixels[CharacterBodyPart.Head] = new HashSet<Vector2Int>();
            _partUVs[CharacterBodyPart.Head] = new Dictionary<Vector2Int, Vector2>();
            _partSpriteFacings[CharacterBodyPart.Head] = headFacing;
            FillPartWithUV(p.firstPixel, headDetectSize, headUVRegion, CharacterBodyPart.Head, palW, palH);
            // 自动检测眼睛：头部区域内的黑色像素
            DetectEyesInHead(p, headDetectSize);
            
            // 身体 + UV
            if (p.torsoStart.HasValue)
            {
                var torsoFacing = _partSpriteFacings.ContainsKey(CharacterBodyPart.Torso) ? _partSpriteFacings[CharacterBodyPart.Torso] : defaultFacing;
                _partPixels[CharacterBodyPart.Torso] = new HashSet<Vector2Int>();
                _partUVs[CharacterBodyPart.Torso] = new Dictionary<Vector2Int, Vector2>();
                _partSpriteFacings[CharacterBodyPart.Torso] = torsoFacing;
                FillPartWithUV(p.torsoStart.Value, torsoDetectSize, torsoUVRegion, CharacterBodyPart.Torso, palW, palH);
            }
            
            // 手脚
            DetectLimb(p, CharacterBodyPart.LeftHand, p.GetLeftHandColor());
            DetectLimb(p, CharacterBodyPart.RightHand, p.GetRightHandColor());
            DetectLimb(p, CharacterBodyPart.LeftFoot, p.GetLeftFootColor());
            DetectLimb(p, CharacterBodyPart.RightFoot, p.GetRightFootColor());
            
            // 锚点：默认使用 East 方向
            if (_partPixels.ContainsKey(CharacterBodyPart.LeftHand) && _partPixels[CharacterBodyPart.LeftHand].Count > 0)
                SetOrUpdateAnchor(AnchorType.LeftWeapon, _partPixels[CharacterBodyPart.LeftHand].First(), AnchorDirection.East);
            if (_partPixels.ContainsKey(CharacterBodyPart.RightHand) && _partPixels[CharacterBodyPart.RightHand].Count > 0)
                SetOrUpdateAnchor(AnchorType.RightWeapon, _partPixels[CharacterBodyPart.RightHand].First(), AnchorDirection.East);
            
        }
        
        /// <summary>
        /// 用UV区域填充检测区域，支持不同大小的映射
        /// 检测区域 > UV区域时：
        /// - 头部：靠右对齐，左边多出的列用UV最左列填充
        /// - 身体：居中对齐，边缘复制边界UV
        /// </summary>
        void FillPartWithUV(Vector2Int startPos, Vector2Int detectSize, RectInt uvRegion, CharacterBodyPart part, int palW, int palH)
        {
            int detectW = detectSize.x, detectH = detectSize.y;
            int uvW = uvRegion.width, uvH = uvRegion.height;
            
            bool isHead = (part == CharacterBodyPart.Head);
            
            // 头部：靠右对齐，多出的全放左边
            // 身体：居中对齐，多出的左右分摊
            int extraLeftX = isHead 
                ? Mathf.Max(0, detectW - uvW)                    // 头部：全放左边
                : Mathf.Max(0, (detectW - uvW + 1) / 2);         // 身体：居中
            int extraTopY = Mathf.Max(0, (detectH - uvH + 1) / 2);
            
            // 身体：当UV高度 > 检测高度时，从UV的下部开始取
            int uvOffsetY = isHead ? 0 : Mathf.Max(0, (uvH - detectH + 1) / 2);
            
            for (int dy = 0; dy < detectH; dy++)
            {
                for (int dx = 0; dx < detectW; dx++)
                {
                    int px = startPos.x + dx, py = startPos.y + dy;
                    if (px >= _frameSize.x || py >= _frameSize.y) continue;
                    
                    var pos = new Vector2Int(px, py);
                    _partPixels[part].Add(pos);
                    
                    // 计算对应的UV坐标
                    int uvDx, uvDy;
                    
                    if (isHead)
                    {
                        // 头部特殊处理：
                        // 第一列放最左边，然后复制第二列填充多出的空间
                        // 例如 UV 4列 + 检测 5列 → 0,1,1,2,3
                        if (dx == 0)
                            uvDx = 0;  // 第一列
                        else if (dx <= 1 + extraLeftX)
                            uvDx = 1;  // 第二列（复制填充多出的空间）
                        else
                            uvDx = dx - extraLeftX;  // 剩余正常映射
                        
                        // Y方向：居中，多出的用边界行填充
                        if (dy < extraTopY)
                            uvDy = 0;
                        else if (dy >= extraTopY + uvH)
                            uvDy = uvH - 1;
                        else
                            uvDy = dy - extraTopY;
                    }
                    else
                    {
                        // 身体：居中对齐，边缘复制边界UV
                        if (dx < extraLeftX)
                            uvDx = 0;
                        else if (dx >= extraLeftX + uvW)
                            uvDx = uvW - 1;
                        else
                            uvDx = dx - extraLeftX;
                        
                        if (dy < extraTopY)
                            uvDy = uvOffsetY;
                        else if (dy >= extraTopY + uvH)
                            uvDy = uvH - 1;
                        else
                            uvDy = Mathf.Min(dy - extraTopY + uvOffsetY, uvH - 1);
                    }
                    
                    // UV是画板上的绝对坐标，和装备贴图布局一致
                    int uvX = uvRegion.x + uvDx;
                    int uvY = uvRegion.y + uvDy;
                    float u = (uvX + 0.5f) / palW;
                    float v = 1f - (uvY + 0.5f) / palH;
                    _partUVs[part][pos] = new Vector2(u, v);
                }
            }
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
        /// 自动涂色头部和身体（带UV设置）
        /// </summary>
        void AutoPaintCurrentPart()
        {
            if (_sprite == null || _data == null) return;
            
            if (!WithReadableTexture(() =>
            {
                AutoPaintPart(CharacterBodyPart.Head);
                AutoPaintPart(CharacterBodyPart.Torso);
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
            
            // 检测和保存必须在同一个 WithReadableTexture 中执行
            if (!WithReadableTexture(() =>
            {
                AutoDetectAllPartsInternal();
                SaveFrameToData(true, "自动涂色+挂点");
                _isDirty = false;
            }))
            {
                Debug.LogWarning("Spritesheet 不可读，请在 Import Settings 中启用 Read/Write");
            }
            Repaint();
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
        /// 清除全部帧的涂色数据
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
                        frameData.anchors.Clear();
                        if (frameData.limbMask != null)
                            frameData.limbMask.Clear();
                        cleared++;
                    }
                }
            }
            
            // 清除当前编辑状态
            _partPixels.Clear();
            _partUVs.Clear();
            _partVariants.Clear();
            _anchors.Clear();
            
            EditorUtility.SetDirty(_data);
            Debug.Log($"[清除] 已清除 {cleared} 帧的涂色数据");
        }
        
        /// <summary>
        /// 自动涂色指定部位（带UV设置）
        /// </summary>
        void AutoPaintPart(CharacterBodyPart targetPart)
        {
            var p = GetDetectParams();
            if (p == null)
            {
                Debug.LogWarning("需要可读的 Spritesheet");
                return;
            }
            
            int palW = _data != null ? _data.paletteSize.x : 32;
            int palH = _data != null ? _data.paletteSize.y : 32;
            var headDetectSize = _data != null ? _data.headDetectSize : new Vector2Int(4, 3);
            var torsoDetectSize = _data != null ? _data.torsoDetectSize : new Vector2Int(3, 2);
            var headUVRegion = _data != null ? _data.headUVRegion : new RectInt(0, 0, 4, 3);
            var torsoUVRegion = _data != null ? _data.torsoUVRegion : new RectInt(0, 3, 3, 2);
            
            switch (targetPart)
            {
                case CharacterBodyPart.Head:
                    _partPixels[CharacterBodyPart.Head] = new HashSet<Vector2Int>();
                    _partUVs[CharacterBodyPart.Head] = new Dictionary<Vector2Int, Vector2>();
                    if (!_partSpriteFacings.ContainsKey(CharacterBodyPart.Head))
                        _partSpriteFacings[CharacterBodyPart.Head] = GetDefaultSpriteFacing();
                    FillPartWithUV(p.firstPixel, headDetectSize, headUVRegion, CharacterBodyPart.Head, palW, palH);
                    
                    // 自动检测眼睛：头部区域内的黑色像素
                    DetectEyesInHead(p, headDetectSize);
                    break;
                    
                case CharacterBodyPart.LeftEye:
                case CharacterBodyPart.RightEye:
                    // 眼睛单独检测时，基于已有的头部区域
                    DetectEyesInHead(p, headDetectSize);
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
                    // 手脚只需要位置，不需要UV（shader直接用颜色替换）
                    Color32 color = targetPart switch
                    {
                        CharacterBodyPart.LeftHand => p.GetLeftHandColor(),
                        CharacterBodyPart.RightHand => p.GetRightHandColor(),
                        CharacterBodyPart.LeftFoot => p.GetLeftFootColor(),
                        _ => p.GetRightFootColor()
                    };
                    DetectLimb(p, targetPart, color);
                    
                    // 设置武器挂点（默认 East）
                    if (_partPixels.ContainsKey(targetPart) && _partPixels[targetPart].Count > 0)
                    {
                        var pos = _partPixels[targetPart].First();
                        if (targetPart == CharacterBodyPart.LeftHand)
                            SetOrUpdateAnchor(AnchorType.LeftWeapon, pos, AnchorDirection.East);
                        else if (targetPart == CharacterBodyPart.RightHand)
                            SetOrUpdateAnchor(AnchorType.RightWeapon, pos, AnchorDirection.East);
                    }
                    break;
                    
                default:
                    return;
            }
        }
        
        void DetectLimb(DetectParams p, CharacterBodyPart part, Color32 color)
        {
            bool isHand = part == CharacterBodyPart.LeftHand || part == CharacterBodyPart.RightHand;
            bool isLeft = part == CharacterBodyPart.LeftHand || part == CharacterBodyPart.LeftFoot;
            
            // 先找到有色像素块的边界
            int minX = _frameSize.x, maxX = -1, maxY = -1;
            for (int y = 0; y < _frameSize.y; y++)
            {
                for (int x = 0; x < _frameSize.x; x++)
                {
                    if (p.cfg.IsColoredPixel(GetPixelAt(p.pixels, x, y)))
                    {
                        minX = Mathf.Min(minX, x);
                        maxX = Mathf.Max(maxX, x);
                        maxY = Mathf.Max(maxY, y);
                    }
                }
            }
            
            if (maxX < 0) return;  // 没有有色像素
            
            int colCount = 2;  // 搜索两列/行
            
            if (isHand)
            {
                // 手部：从有色像素块的左/右边缘开始，限定两列范围
                int xStart, xEnd, xStep;
                
                if (p.facingRight == isLeft)  // 左手在右边(SE/NE)或右手在左边(SW/NW)
                {
                    // 从有色块右边缘往左搜索
                    xStart = maxX;
                    xEnd = Mathf.Max(0, maxX - colCount);
                    xStep = -1;
                }
                else
                {
                    // 从有色块左边缘往右搜索
                    xStart = minX;
                    xEnd = Mathf.Min(_frameSize.x, minX + colCount + 1);
                    xStep = 1;
                }
                
                // 头部底部Y（手部像素一般要低于这个位置）
                var headDetectSize = _data != null ? _data.headDetectSize : new Vector2Int(4, 3);
                int headBottomY = p.firstPixel.y + headDetectSize.y;
                
                // 按列扫描，每列从下往上
                Vector2Int? result = null;
                for (int x = xStart; x != xEnd && !result.HasValue; x += xStep)
                {
                    // 统计这一列匹配的像素
                    var matchedYs = new List<int>();
                    for (int y = _frameSize.y - 1; y >= 0; y--)
                    {
                        if (p.cfg.IsLimbColorMatch(GetPixelAt(p.pixels, x, y), color))
                            matchedYs.Add(y);
                    }
                    
                    if (matchedYs.Count == 1)
                    {
                        // 落单情况，直接返回（不限制位置）
                        result = new Vector2Int(x, matchedYs[0]);
                    }
                    else if (matchedYs.Count > 1)
                    {
                        // 多个像素，只考虑低于头部区域的（Y >= headBottomY）
                        foreach (int y in matchedYs)
                        {
                            if (y >= headBottomY)
                            {
                                result = new Vector2Int(x, y);
                                break;
                            }
                        }
                    }
                }
                
                if (result.HasValue)
                    _partPixels[part] = new HashSet<Vector2Int> { result.Value };
            }
            else
            {
                // 脚部：只看最底下一行，允许连续多个像素
                int footY = maxY;
                
                // X方向：左脚从右到左，右脚从左到右
                int xStart = isLeft ? maxX : minX;
                int xEnd = isLeft ? minX - 1 : maxX + 1;
                int xStep = isLeft ? -1 : 1;
                
                // 收集这一行所有匹配脚颜色的像素
                var pixels = new HashSet<Vector2Int>();
                for (int x = xStart; x != xEnd; x += xStep)
                {
                    if (p.cfg.IsLimbColorMatch(GetPixelAt(p.pixels, x, footY), color))
                        pixels.Add(new Vector2Int(x, footY));
                }
                
                if (pixels.Count > 0)
                    _partPixels[part] = pixels;
            }
        }
        
        /// <summary>
        /// 检测头部区域内的眼睛（黑色/描边像素）
        /// 注意：SE朝向（朝下）时，画面左边是角色右眼，画面右边是角色左眼
        /// </summary>
        void DetectEyesInHead(DetectParams p, Vector2Int headDetectSize)
        {
            var leftEyePixels = new HashSet<Vector2Int>();
            var rightEyePixels = new HashSet<Vector2Int>();
            
            // 头部中心 X 坐标（用于区分左右眼）
            float headCenterX = p.firstPixel.x + headDetectSize.x / 2.0f;
            
            // 在头部检测区域内扫描黑色像素
            for (int dy = 0; dy < headDetectSize.y; dy++)
            {
                for (int dx = 0; dx < headDetectSize.x; dx++)
                {
                    int px = p.firstPixel.x + dx;
                    int py = p.firstPixel.y + dy;
                    
                    if (px < 0 || px >= _frameSize.x || py < 0 || py >= _frameSize.y)
                        continue;
                    
                    var c = GetPixelAt(p.pixels, px, py);
                    
                    // 使用 IsOutline 判断是否为黑色/描边像素
                    if (p.cfg.IsOutline(c))
                    {
                        // SE朝向：画面左边（x < 中心）是角色右眼，画面右边（x >= 中心）是角色左眼
                        if (px < headCenterX)
                            rightEyePixels.Add(new Vector2Int(px, py));
                        else
                            leftEyePixels.Add(new Vector2Int(px, py));
                    }
                }
            }
            
            if (leftEyePixels.Count > 0)
                _partPixels[CharacterBodyPart.LeftEye] = leftEyePixels;
            if (rightEyePixels.Count > 0)
                _partPixels[CharacterBodyPart.RightEye] = rightEyePixels;
        }
        
        /// <summary>
        /// 在限定范围内查找第一个匹配的手脚像素
        /// </summary>
        /// <param name="isHand">true=手部按列优先，false=脚部按行优先</param>
        Vector2Int? FindFirstLimbPixel(Color32[] pixels, Color32 targetColor, DetectConfig cfg,
            int xStart, int xEnd, int xStep, int yMin, int yMax, bool isHand)
        {
            if (isHand)
            {
                // 手部：按列扫描（X优先），每列从下往上
                for (int x = xStart; x != xEnd; x += xStep)
                {
                    for (int y = yMax - 1; y >= yMin; y--)
                    {
                        if (cfg.IsLimbColorMatch(GetPixelAt(pixels, x, y), targetColor))
                            return new Vector2Int(x, y);
                    }
                }
            }
            else
            {
                // 脚部：按行扫描（Y优先，从下往上），每行按X方向
                for (int y = yMax - 1; y >= yMin; y--)
                {
                    for (int x = xStart; x != xEnd; x += xStep)
                    {
                        if (cfg.IsLimbColorMatch(GetPixelAt(pixels, x, y), targetColor))
                            return new Vector2Int(x, y);
                    }
                }
            }
            return null;
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
            
            int fixedCount = FixAllFramesSpriteFacingInternal();
            
            EditorUtility.SetDirty(_data);
            LoadFrameData();
            
            Debug.Log($"贴图方向修复完成: 修复了 {fixedCount} 个区域");
        }
        
        /// <summary>
        /// 修复所有帧的贴图方向（内部版本，不带 Undo）
        /// </summary>
        int FixAllFramesSpriteFacingInternal()
        {
            if (_data == null) return 0;
            
            int fixedCount = 0;
            
            foreach (var anim in _data.animations)
            {
                foreach (var frame in anim.frames)
                {
                    // 根据行索引确定正确的 spriteFacing
                    CharacterFacing correctFacing = (CharacterFacing)frame.rowIndex;
                    if (frame.rowIndex < 0 || frame.rowIndex > 3)
                        correctFacing = CharacterFacing.SouthEast;
                    
                    foreach (var region in frame.bodyRegions)
                    {
                        if (region.spriteFacing != correctFacing)
                        {
                            region.spriteFacing = correctFacing;
                            fixedCount++;
                        }
                    }
                }
            }
            
            return fixedCount;
        }
        
        void SetOrUpdateAnchor(AnchorType type, Vector2Int pos, AnchorDirection direction)
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
            
            Undo.RecordObject(_data, "从SE生成所有行");
            
            int savedFrame = _frame;
            int savedRow = _row;
            int totalGenerated = 0;
            
            // 遍历所有帧，从SE生成SW/NE/NW
            for (int f = 0; f < _framesPerRow; f++)
            {
                var seFrame = anim.GetFrame(f, 0);
                if (seFrame == null || seFrame.bodyRegions.Count == 0) continue;
                
                GenerateSWFrame(anim, f, seFrame);
                GenerateNEFrame(anim, f, seFrame);
                GenerateNWFrame(anim, f, seFrame);
                
                totalGenerated++;
            }
            
            _frame = savedFrame;
            _row = savedRow;
            _isDirty = false;
            LoadFrameData();
            
            EditorUtility.SetDirty(_data);
            Debug.Log($"从SE生成所有行完成: 共处理 {totalGenerated} 帧 × 3行 = {totalGenerated * 3} 帧数据");
        }
        
        /// <summary>
        /// 生成帧数据的统一方法
        /// </summary>
        /// <param name="mirrorFacing">是否镜像贴图方向（SE↔SW）</param>
        /// <param name="toNorth">是否转换为North方向（SE→NE）</param>
        /// <param name="translatePos">是否平移位置到镜像位置（形状不变，只平移）</param>
        void GenerateFrame(AnimationData anim, int frameIndex, FrameData sourceFrame, int targetRow,
            bool mirrorFacing, bool toNorth, bool translatePos)
        {
            var targetFrame = anim.GetOrCreateFrame(frameIndex, targetRow);
            targetFrame.bodyRegions.Clear();
            targetFrame.anchors.Clear();
            
            // 生成 UV 部位区域（头/身体）
            foreach (var sourceRegion in sourceFrame.bodyRegions)
            {
                var targetFacing = sourceRegion.spriteFacing;
                if (mirrorFacing) targetFacing = MirrorSpriteFacing(targetFacing);
                if (toNorth) targetFacing = SouthToNorth(targetFacing);
                
                var newRegion = new BodyPartRegion
                {
                    part = sourceRegion.part,
                    orientation = sourceRegion.orientation,
                    spriteFacing = targetFacing,
                    variant = sourceRegion.variant
                };
                
                // 头部/身体：每个部位单独计算偏移量
                int offsetX = 0;
                if (translatePos && sourceRegion.pixels.Count > 0)
                {
                    var positions = sourceRegion.pixels.Select(p => p.position);
                    offsetX = FrameDataAlgorithms.CalculateMirrorTranslateOffset(positions, _frameSize.x);
                }
                
                foreach (var px in sourceRegion.pixels)
                {
                    var newPos = translatePos 
                        ? FrameDataAlgorithms.TranslatePosition(px.position, offsetX)
                        : px.position;
                    newRegion.pixels.Add(new BodyPartPixel
                    {
                        part = sourceRegion.part,
                        position = newPos,
                        color = px.color,
                        uv = px.uv
                    });
                }
                
                targetFrame.bodyRegions.Add(newRegion);
            }
            
            // 生成手脚蒙版
            if (sourceFrame.limbMask != null)
            {
                if (targetFrame.limbMask == null)
                    targetFrame.limbMask = new LimbMask();
                else
                    targetFrame.limbMask.Clear();
                
                CopyLimbMask(sourceFrame.limbMask.leftHand, targetFrame.limbMask.leftHand, translatePos);
                CopyLimbMask(sourceFrame.limbMask.rightHand, targetFrame.limbMask.rightHand, translatePos);
                CopyLimbMask(sourceFrame.limbMask.leftFoot, targetFrame.limbMask.leftFoot, translatePos);
                CopyLimbMask(sourceFrame.limbMask.rightFoot, targetFrame.limbMask.rightFoot, translatePos);
                CopyLimbMask(sourceFrame.limbMask.leftEye, targetFrame.limbMask.leftEye, translatePos);
                CopyLimbMask(sourceFrame.limbMask.rightEye, targetFrame.limbMask.rightEye, translatePos);
            }
            
            // 生成锚点
            foreach (var anchor in sourceFrame.anchors)
            {
                targetFrame.anchors.Add(new AnchorPoint
                {
                    type = anchor.type,
                    position = translatePos ? MirrorPosition(anchor.position) : anchor.position,
                    direction = anchor.direction
                });
            }
        }
        
        void CopyLimbMask(List<Vector2Int> source, List<Vector2Int> target, bool mirror)
        {
            target.Clear();
            foreach (var pos in source)
                target.Add(mirror ? MirrorPosition(pos) : pos);
        }
        
        // SE：源数据（直接使用，不生成）
        // SW：平移到镜像位置 + spriteFacing 镜像（SE→SW）
        void GenerateSWFrame(AnimationData anim, int f, FrameData seFrame)
            => GenerateFrame(anim, f, seFrame, 1, mirrorFacing: true, toNorth: false, translatePos: true);
        
        // NE：位置不变 + spriteFacing 转 North（SE→NE）
        void GenerateNEFrame(AnimationData anim, int f, FrameData seFrame)
            => GenerateFrame(anim, f, seFrame, 2, mirrorFacing: false, toNorth: true, translatePos: false);
        
        // NW：平移到镜像位置 + spriteFacing 镜像并转 North（SE→SW→NW）
        void GenerateNWFrame(AnimationData anim, int f, FrameData seFrame)
            => GenerateFrame(anim, f, seFrame, 3, mirrorFacing: true, toNorth: true, translatePos: true);
        
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
