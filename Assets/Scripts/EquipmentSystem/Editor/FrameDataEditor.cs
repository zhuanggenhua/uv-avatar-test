using System.Collections.Generic;
using System.IO;
using System.Linq;

using EquipmentSystem.Data;

using UnityEditor;
using UnityEditor.Animations;

using UnityEngine;

// 使用别名避免与 UnityEditor.BodyPart 冲突
using CharacterBodyPart = EquipmentSystem.Data.CharacterBodyPart;

namespace EquipmentSystem.Editor
{
    public enum TabMode { BodyPaint, Anchor }

    public class FrameDataEditor : EditorWindow
    {
        #region 字段
        
        CharacterFrameData _data;
        Texture2D _sprite;
        AnimatorController _animatorController;
        
        // 编辑状态
        string _animName = "Idle";
        int _animIndex, _row, _frame;
        TabMode _tab = TabMode.BodyPaint;
        CharacterBodyPart _currentPart = CharacterBodyPart.Torso;
        AnchorType _anchorType = AnchorType.LeftWeapon;
        UVOrientation _anchorOrientation = UVOrientation.UpRight;
        bool _anchorFlipX;
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
        
        // UV 画板（纯坐标网格）
        Vector2Int _paletteSize = new Vector2Int(32, 32);  // 画板尺寸
        Sprite _paletteRefSprite;       // 参考底图（可选，显示装备贴图）
        float _paletteZoom = 8f;        // 画板缩放
        Vector2 _palettePan;            // 画板平移
        Rect _paletteDisplayRect;       // 画板显示区域
        Rect _paletteCanvasRect;        // 画板画布区域（用于输入检测）
        bool _showPalette = true;       // 是否显示画板
        
        // 选区
        bool _isSelecting;              // 是否正在框选
        bool _selectOnPalette;          // 选区在画板上还是画布上
        bool _isErasing;                // 是否正在擦除模式
        Vector2Int _selectionStart;     // 选区起始点
        Vector2Int _selectionEnd;       // 选区结束点
        RectInt _paletteSelection;      // 画板选区（已确定）
        RectInt _canvasSelection;       // 画布选区（已确定）
        
        // 编辑缓存
        Dictionary<CharacterBodyPart, HashSet<Vector2Int>> _partPixels = new Dictionary<CharacterBodyPart, HashSet<Vector2Int>>();
        Dictionary<CharacterBodyPart, Dictionary<Vector2Int, Vector2>> _partUVs = new Dictionary<CharacterBodyPart, Dictionary<Vector2Int, Vector2>>();
        Dictionary<CharacterBodyPart, UVOrientation> _partOrientations = new Dictionary<CharacterBodyPart, UVOrientation>();
        Dictionary<CharacterBodyPart, CharacterFacing> _partSpriteFacings = new Dictionary<CharacterBodyPart, CharacterFacing>();
        List<AnchorPoint> _anchors = new List<AnchorPoint>();
        
        // 脏标记 - 只在有修改时保存
        bool _isDirty;
        
        #endregion
        
        #region 初始化
        
        [MenuItem("Tools/Equipment System/Frame Editor")]
        public static void ShowWindow() => GetWindow<FrameDataEditor>("帧数据编辑器").minSize = new Vector2(900, 700);
        
        void OnEnable()
        {
            wantsMouseMove = true;
            Undo.undoRedoPerformed += OnUndoRedo;
            
            // 加载保存的参考底图设置
            LoadPaletteSettings();
            
            // 如果没有选中数据，自动查找并选中第一个 CharacterFrameData 资源
            if (_data == null)
                AutoSelectFirstFrameData();
            else
            {
                // 脚本编译后重新加载当前帧数据（保持当前动画选择）
                var anim = _data.GetAnimation(_animName);
                if (anim != null)
                    SyncFromAnimation(anim);
                LoadFrameData();
            }
        }
        
        void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            SavePaletteSettings();
        }
        
        void LoadPaletteSettings()
        {
            _paletteSize.x = EditorPrefs.GetInt("FrameDataEditor_PaletteSizeX", 32);
            _paletteSize.y = EditorPrefs.GetInt("FrameDataEditor_PaletteSizeY", 32);
            
            string spritePath = EditorPrefs.GetString("FrameDataEditor_PaletteRefSprite", "");
            if (!string.IsNullOrEmpty(spritePath))
                _paletteRefSprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        }
        
        void SavePaletteSettings()
        {
            EditorPrefs.SetInt("FrameDataEditor_PaletteSizeX", _paletteSize.x);
            EditorPrefs.SetInt("FrameDataEditor_PaletteSizeY", _paletteSize.y);
            
            string spritePath = _paletteRefSprite != null ? AssetDatabase.GetAssetPath(_paletteRefSprite) : "";
            EditorPrefs.SetString("FrameDataEditor_PaletteRefSprite", spritePath);
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
        
        void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            SaveIfDirty();
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
            _data = (CharacterFrameData)EditorGUILayout.ObjectField("帧数据", _data, typeof(CharacterFrameData), false);
            if (EditorGUI.EndChangeCheck() && _data != null)
                SyncFromData();
        }
        
        void DrawConfigSection()
        {
            if (_data == null || string.IsNullOrEmpty(_animName)) return;
            
            var anim = _data.GetOrCreateAnimation(_animName);
            
            GUILayout.Space(10);
            GUILayout.Label("当前动画配置", EditorStyles.boldLabel);
            
            EditorGUI.BeginChangeCheck();
            
            // Spritesheet
            anim.spritesheet = (Texture2D)EditorGUILayout.ObjectField("Spritesheet", anim.spritesheet, typeof(Texture2D), false);
            
            // 帧配置
            EditorGUILayout.BeginHorizontal();
            anim.frameSize = EditorGUILayout.Vector2IntField("帧尺寸", anim.frameSize);
            if (GUILayout.Button("自动", GUILayout.Width(40)))
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
            
            // 从 Animator 导入动画名称
            EditorGUI.BeginChangeCheck();
            _animatorController = (AnimatorController)EditorGUILayout.ObjectField("从Animator导入", _animatorController, typeof(AnimatorController), false);
            if (EditorGUI.EndChangeCheck() && _animatorController != null)
                ImportAnimationsFromAnimator();
            
            if (_data != null)
            {
                var names = _data.GetAnimationNames();
                if (names.Count > 0)
                {
                    _animIndex = Mathf.Clamp(_animIndex, 0, names.Count - 1);
                    EditorGUI.BeginChangeCheck();
                    _animIndex = EditorGUILayout.Popup("动画", _animIndex, names.ToArray());
                    if (EditorGUI.EndChangeCheck())
                        SwitchAnimation(names[_animIndex]);
                    
                    // 动画名称编辑
                    var anim = _data.GetOrCreateAnimation(_animName);
                    EditorGUI.BeginChangeCheck();
                    string newName = EditorGUILayout.TextField("名称", anim.animationName);
                    if (EditorGUI.EndChangeCheck() && newName != anim.animationName)
                    {
                        // 检查名称是否已存在
                        if (!string.IsNullOrEmpty(newName) && !names.Contains(newName))
                        {
                            Undo.RecordObject(_data, "Rename Animation");
                            anim.animationName = newName;
                            _animName = newName;
                            EditorUtility.SetDirty(_data);
                        }
                    }
                    
                    // 武器隐藏配置
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.BeginHorizontal();
                    anim.hideLeftWeapon = EditorGUILayout.ToggleLeft("隐藏左手武器", anim.hideLeftWeapon, GUILayout.Width(100));
                    anim.hideRightWeapon = EditorGUILayout.ToggleLeft("隐藏右手武器", anim.hideRightWeapon, GUILayout.Width(100));
                    EditorGUILayout.EndHorizontal();
                    if (EditorGUI.EndChangeCheck())
                        EditorUtility.SetDirty(_data);
                }
                
                // 添加/删除动画按钮
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("+", GUILayout.Width(25)))
                    AddNewAnimation();
                if (names.Count > 0 && GUILayout.Button("-", GUILayout.Width(25)))
                    RemoveCurrentAnimation();
                EditorGUILayout.EndHorizontal();
            }
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
                DrawDetectConfig();
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
            // 显示设置
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("显示:", GUILayout.Width(35));
            _paintDisplayMode = GUILayout.Toolbar(_paintDisplayMode, new[] { "隐藏", "当前", "全部" });
            EditorGUILayout.EndHorizontal();
            
            GUILayout.Space(5);
            GUILayout.Label("选择部位", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            DrawPartButton(CharacterBodyPart.Head, "头部", new Color(0.2f, 0.9f, 0.2f));
            DrawPartButton(CharacterBodyPart.Torso, "身体", new Color(0.2f, 0.7f, 0.2f));
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            DrawPartButton(CharacterBodyPart.LeftHand, "左手", new Color(1.0f, 0.8f, 0.0f));
            DrawPartButton(CharacterBodyPart.RightHand, "右手", new Color(1.0f, 0.5f, 0.0f));
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            DrawPartButton(CharacterBodyPart.LeftFoot, "左脚", new Color(0.3f, 0.5f, 1.0f));
            DrawPartButton(CharacterBodyPart.RightFoot, "右脚", new Color(0.8f, 0.2f, 0.8f));
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
            
            GUILayout.Space(5);
            
            EditorGUILayout.BeginVertical("helpbox");
            GUILayout.Label($"当前: {_currentPart}", EditorStyles.boldLabel);
            
            // 部位 UV 方向设置
            if (!_partOrientations.ContainsKey(_currentPart))
                _partOrientations[_currentPart] = UVOrientation.UpRight;
            EditorGUI.BeginChangeCheck();
            _partOrientations[_currentPart] = (UVOrientation)EditorGUILayout.EnumPopup("UV 方向", _partOrientations[_currentPart]);
            if (EditorGUI.EndChangeCheck())
                SaveWithUndo("修改UV方向");
            
            // 贴图方向（仅对头部和躯干有意义，用于转头等场景）
            if (_currentPart == CharacterBodyPart.Head || _currentPart == CharacterBodyPart.Torso)
            {
                GUILayout.Space(3);
                if (!_partSpriteFacings.ContainsKey(_currentPart))
                    _partSpriteFacings[_currentPart] = GetDefaultSpriteFacing();
                
                EditorGUI.BeginChangeCheck();
                _partSpriteFacings[_currentPart] = (CharacterFacing)EditorGUILayout.EnumPopup("贴图方向", _partSpriteFacings[_currentPart]);
                if (EditorGUI.EndChangeCheck())
                    SaveWithUndo("修改贴图方向");
            }
            
            int count = _partPixels.ContainsKey(_currentPart) ? _partPixels[_currentPart].Count : 0;
            EditorGUILayout.LabelField($"已涂: {count} 像素");
            EditorGUILayout.EndVertical();
            
            GUILayout.Space(5);
            
            if (GUILayout.Button("🔍 自动检测该部位"))
                AutoDetectPart(_currentPart);
            if (GUILayout.Button("清除该部位"))
            {
                if (_partPixels.ContainsKey(_currentPart))
                    _partPixels[_currentPart].Clear();
                SaveWithUndo("清除部位");
            }
            GUILayout.Space(3);
            if (GUILayout.Button("🔍 自动检测全部部位"))
                AutoDetectAllPartsWithUndo();
            
            GUILayout.Space(3);
            GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
            if (GUILayout.Button("清除全部部位"))
            {
                _partPixels.Clear();
                SaveWithUndo("清除全部部位");
            }
            GUI.backgroundColor = Color.white;
            
            // UV 画板配置
            GUILayout.Space(10);
            GUILayout.Label("UV 画板", EditorStyles.boldLabel);
            
            _showPalette = EditorGUILayout.Toggle("显示画板", _showPalette);
            
            if (_showPalette)
            {
                _paletteSize = EditorGUILayout.Vector2IntField("画板尺寸", _paletteSize);
                _paletteRefSprite = (Sprite)EditorGUILayout.ObjectField("参考底图(可选)", _paletteRefSprite, typeof(Sprite), false);
                
                if (_paletteRefSprite != null)
                {
                    var spriteSize = new Vector2Int((int)_paletteRefSprite.rect.width, (int)_paletteRefSprite.rect.height);
                    if (spriteSize != _paletteSize)
                    {
                        if (GUILayout.Button($"同步尺寸为 {spriteSize.x}×{spriteSize.y}"))
                            _paletteSize = spriteSize;
                    }
                }
                
                EditorGUILayout.Space(5);
                
                // 选区状态和操作
                EditorGUILayout.BeginVertical("helpbox");
                GUILayout.Label("选区操作", EditorStyles.boldLabel);
                
                if (_paletteSelection.width > 0)
                    EditorGUILayout.LabelField($"画板选区: {_paletteSelection.width}×{_paletteSelection.height}");
                else
                    EditorGUILayout.LabelField("画板选区: 无 (左键拖拽)");
                    
                if (_canvasSelection.width > 0)
                    EditorGUILayout.LabelField($"画布选区: {_canvasSelection.width}×{_canvasSelection.height}");
                else
                    EditorGUILayout.LabelField("画布选区: 无 (左键拖拽)");
                
                // 核心操作按钮
                bool canCopy = _paletteSelection.width > 0 && _canvasSelection.width > 0;
                bool sizeMatch = canCopy && _paletteSelection.size == _canvasSelection.size;
                bool singlePalette = _paletteSelection.width == 1 && _paletteSelection.height == 1;
                
                // 情况1：选区大小一致，正常复制
                // 情况2：画板选区是单像素，可以填充到画布选区
                GUI.enabled = canCopy && (sizeMatch || singlePalette);
                string copyLabel = singlePalette && !sizeMatch ? 
                    $"🎨 用单点UV填充选区 ({_canvasSelection.width}×{_canvasSelection.height})" : 
                    "📋 复制UV到画布选区";
                if (GUILayout.Button(copyLabel, GUILayout.Height(30)))
                    CopyUVFromPaletteToCanvas();
                GUI.enabled = true;
                
                // 删除选区涂色
                GUI.enabled = _canvasSelection.width > 0;
                if (GUILayout.Button("🗑️ 删除画布选区涂色", GUILayout.Height(25)))
                    DeleteCanvasSelection();
                GUI.enabled = true;
                
                if (canCopy && !sizeMatch && !singlePalette)
                {
                    EditorGUILayout.HelpBox($"选区大小不一致！\n画板: {_paletteSelection.width}×{_paletteSelection.height}\n画布: {_canvasSelection.width}×{_canvasSelection.height}\n(画板选单像素可填充)", MessageType.Warning);
                }
                
                if (GUILayout.Button("清除选区 (ESC)"))
                {
                    _paletteSelection = default;
                    _canvasSelection = default;
                }
                EditorGUILayout.EndVertical();
            }
        }
        
        void DrawAnchorTab()
        {
            GUILayout.Label("锚点设置", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("用于挂件定位（头盔、武器等）", MessageType.Info);
            
            _anchorType = (AnchorType)EditorGUILayout.EnumPopup("锚点类型", _anchorType);
            _anchorOrientation = (UVOrientation)EditorGUILayout.EnumPopup("武器方向", _anchorOrientation);
            _anchorFlipX = EditorGUILayout.Toggle("水平翻转", _anchorFlipX);
            
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
                    _anchorOrientation = a.orientation;
                    _anchorFlipX = a.flipX;
                }
                GUI.color = Color.white;
                GUILayout.Label($"({a.position.x},{a.position.y})", GUILayout.Width(55));
                
                // 翻转切换
                EditorGUI.BeginChangeCheck();
                bool flip = GUILayout.Toggle(a.flipX, "翻转", EditorStyles.miniButton, GUILayout.Width(36));
                if (EditorGUI.EndChangeCheck())
                {
                    a.flipX = flip;
                    SaveWithUndo("修改锚点翻转");
                }
                
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
                c.outlineThreshold = EditorGUILayout.IntSlider("描边阈值", c.outlineThreshold, 0, 100);
                
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
            if (GUILayout.Button("🔍 自动检测全部帧部位"))
                AutoDetectAllFrames();
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
            if (GUILayout.Button("🔄 扩展当前部位"))
                ExpandCurrentPartRegion();
            if (GUILayout.Button("🔄 扩展全部帧当前部位"))
                ExpandPartRegionForAllFrames();
            EditorGUILayout.EndHorizontal();
            
            GUILayout.Space(5);
            GUILayout.Label("方向数据生成", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("从SE方向自动生成其他方向的部位数据：\n• SW = SE水平镜像\n• NE = SE复制\n• NW = NE水平镜像", MessageType.Info);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("📋 从SE生成当前帧"))
                GenerateOtherDirectionsFromSE(false);
            if (GUILayout.Button("📋 从SE生成全部帧"))
                GenerateOtherDirectionsFromSE(true);
            EditorGUILayout.EndHorizontal();
            
            GUILayout.Space(5);
            GUILayout.Label("GPU 换装", EditorStyles.boldLabel);
            
            // 显示当前 UV Map 状态（双层）
            if (_data != null && !string.IsNullOrEmpty(_animName))
            {
                var anim = _data.GetAnimation(_animName);
                if (anim != null)
                {
                    bool hasBodyUV = anim.bodyUVMap != null;
                    bool hasHeadUV = anim.headUVMap != null;
                    GUI.color = (hasBodyUV && hasHeadUV) ? Color.green : Color.yellow;
                    string status = hasBodyUV && hasHeadUV ? "✓ 双层 UV Map 已设置" :
                                   hasBodyUV ? "○ 仅身体层" :
                                   hasHeadUV ? "○ 仅头部层" : "✗ 未设置 UV Map";
                    EditorGUILayout.LabelField(status);
                    GUI.color = Color.white;
                }
            }
            
            if (GUILayout.Button("💾 生成当前动画 UV Map (双层)"))
                GenerateDualUVMapsForCurrentAnimation();
            
            if (GUILayout.Button("💾 生成所有动画 UV Map (双层)"))
                GenerateAllDualUVMaps();
        }
        
        void DrawPartButton(CharacterBodyPart part, string label, Color color)
        {
            GUI.backgroundColor = _currentPart == part ? color : Color.white;
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
            
            int palW = _paletteSize.x;
            int palH = _paletteSize.y;
            
            float w = palW * _paletteZoom;
            float h = palH * _paletteZoom;
            var center = paletteCanvas.center + _palettePan;
            _paletteDisplayRect = new Rect(center.x - w/2, center.y - h/2, w, h);
            
            // 1. 先绘制参考底图（在最底层）
            if (_paletteRefSprite != null && _paletteRefSprite.texture != null)
            {
                var spriteRect = _paletteRefSprite.rect;
                var tex = _paletteRefSprite.texture;
                Rect uv = new Rect(
                    spriteRect.x / tex.width,
                    spriteRect.y / tex.height,
                    spriteRect.width / tex.width,
                    spriteRect.height / tex.height
                );
                GUI.DrawTextureWithTexCoords(_paletteDisplayRect, tex, uv);
            }
            
            // 2. 再绘制 UV 颜色（半透明叠加在上方）
            for (int y = 0; y < palH; y++)
            {
                for (int x = 0; x < palW; x++)
                {
                    // UV 颜色：R=U, G=V
                    float u = (float)x / palW;
                    float v = 1f - (float)y / palH;  // Y 翻转
                    Color uvColor = new Color(u, v, 0.3f, 0.4f);  // 40% 透明度
                    
                    EditorGUI.DrawRect(new Rect(_paletteDisplayRect.x + x * _paletteZoom, 
                                                _paletteDisplayRect.y + y * _paletteZoom, 
                                                _paletteZoom, _paletteZoom), uvColor);
                }
            }
            
            // 3. 绘制选区
            DrawPaletteSelection();
            
            // 4. 绘制网格线
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
            
            // 5. 标签
            string label = $"UV 画板 ({palW}×{palH}) | 左键框选";
            if (_paletteSelection.width > 0)
                label += $" | 选区: {_paletteSelection.width}×{_paletteSelection.height}";
            GUI.Label(new Rect(paletteCanvas.x + 10, paletteCanvas.y + 10, 400, 20), label, EditorStyles.whiteLabel);
        }
        
        /// <summary>
        /// 绘制画板选区
        /// </summary>
        void DrawPaletteSelection()
        {
            // 已确定的选区
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
            
            DrawCheckerboard(_display);
            GUI.DrawTextureWithTexCoords(_display, _sprite, uv);
            DrawBodyPixels(_display);
            DrawAnchors(_display);
            DrawCanvasSelection(_display);  // 画布选区
            if (_zoom >= 4) DrawGrid(_display);
            
            string rowHint = _row < 4 ? new[]{ "SE", "SW", "NE", "NW" }[_row] : $"R{_row}";
            string selInfo = _canvasSelection.width > 0 ? $" | 选区: {_canvasSelection.width}×{_canvasSelection.height}" : "";
            GUI.Label(new Rect(_canvas.x + 10, _canvas.y + 10, 450, 20),
                $"画布 | {_animName} | 行{_row}({rowHint}) | 帧{_frame} | 左键框选 | Shift+拖拽擦除{selInfo}", EditorStyles.whiteLabel);
        }
        
        void DrawBodyPixels(Rect r)
        {
            if (_paintDisplayMode == 0) return;
            
            foreach (var kv in _partPixels)
            {
                if (_paintDisplayMode == 1 && kv.Key != _currentPart) continue;
                
                var uvs = _partUVs.ContainsKey(kv.Key) ? _partUVs[kv.Key] : null;
                int palW = _paletteSize.x;
                int palH = _paletteSize.y;
                
                foreach (var p in kv.Value)
                {
                    Color c;
                    
                    // 使用 UV 颜色显示
                    if (uvs != null && uvs.TryGetValue(p, out var uv))
                    {
                        // UV 颜色：R=U, G=V, B=0.3
                        c = new Color(uv.x, uv.y, 0.3f, 0.8f);
                    }
                    else
                    {
                        // 没有 UV 的像素用灰色
                        c = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                    }
                    
                    EditorGUI.DrawRect(new Rect(r.x + p.x * _zoom, r.y + p.y * _zoom, _zoom, _zoom), c);
                }
            }
        }
        
        /// <summary>
        /// 绘制画布选区
        /// </summary>
        void DrawCanvasSelection(Rect r)
        {
            // 已确定的选区
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
            
            int palW = _paletteSize.x;
            int palH = _paletteSize.y;
            
            // 单像素填充：计算单个 UV
            Vector2 singleUV = Vector2.zero;
            if (singlePalette)
            {
                float u = (float)_paletteSelection.x / palW;
                float v = 1f - (float)_paletteSelection.y / palH;
                singleUV = new Vector2(u, v);
            }
            
            for (int dy = 0; dy < _canvasSelection.height; dy++)
            {
                for (int dx = 0; dx < _canvasSelection.width; dx++)
                {
                    // 画布目标像素
                    int dstX = _canvasSelection.x + dx;
                    int dstY = _canvasSelection.y + dy;
                    var dstPos = new Vector2Int(dstX, dstY);
                    
                    if (!IsValidPixel(dstPos)) continue;
                    
                    Vector2 uv;
                    if (singlePalette)
                    {
                        // 单像素填充模式
                        uv = singleUV;
                    }
                    else
                    {
                        // 1:1 复制模式
                        int srcX = _paletteSelection.x + dx;
                        int srcY = _paletteSelection.y + dy;
                        float u = (float)srcX / palW;
                        float v = 1f - (float)srcY / palH;
                        uv = new Vector2(u, v);
                    }
                    
                    pixels.Add(dstPos);
                    uvs[dstPos] = uv;
                }
            }
            
            SaveWithUndo("拷贝 UV");
            Repaint();
            
            if (singlePalette && !sizeMatch)
                Debug.Log($"已用单点UV填充 {_canvasSelection.width * _canvasSelection.height} 个像素");
            else
                Debug.Log($"已拷贝 {_canvasSelection.width * _canvasSelection.height} 个像素的 UV");
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
        /// 扩展当前选中部位的区域
        /// 基于边界像素的 UV 向外扩展：先上下、再左右
        /// 扩展像素继承边界像素的 UV
        /// </summary>
        void ExpandCurrentPartRegion()
        {
            if (_data == null) return;
            
            if (!_partPixels.ContainsKey(_currentPart) || _partPixels[_currentPart].Count == 0)
            {
                Debug.LogWarning($"当前部位 {_currentPart} 没有涂色区域");
                return;
            }
            
            var pixels = _partPixels[_currentPart];
            if (!_partUVs.ContainsKey(_currentPart))
                _partUVs[_currentPart] = new Dictionary<Vector2Int, Vector2>();
            var uvs = _partUVs[_currentPart];
            
            // 根据部位选择扩展配置
            int expandUp, expandDown, expandSide;
            if (_currentPart == CharacterBodyPart.Head)
            {
                expandUp = _data.headExpandUp;
                expandDown = _data.headExpandDown;
                expandSide = _data.headExpandSide;
            }
            else
            {
                expandUp = _data.bodyExpandUp;
                expandDown = _data.bodyExpandDown;
                expandSide = _data.bodyExpandSide;
            }
            
            ExpandRegionWithBoundaryUV(pixels, uvs, expandUp, expandDown, expandSide, expandSide);
            
            SaveWithUndo($"扩展{_currentPart}区域");
            Repaint();
        }
        
        /// <summary>
        /// 扩展全部帧的当前部位区域
        /// </summary>
        void ExpandPartRegionForAllFrames()
        {
            if (_data == null) return;
            
            SaveIfDirty();
            Undo.RecordObject(_data, "扩展全部帧区域");
            
            // 根据部位选择扩展配置
            int expandUp, expandDown, expandSide;
            if (_currentPart == CharacterBodyPart.Head)
            {
                expandUp = _data.headExpandUp;
                expandDown = _data.headExpandDown;
                expandSide = _data.headExpandSide;
            }
            else
            {
                expandUp = _data.bodyExpandUp;
                expandDown = _data.bodyExpandDown;
                expandSide = _data.bodyExpandSide;
            }
            
            int savedFrame = _frame;
            int savedRow = _row;
            int expandedCount = 0;
            
            for (int r = 0; r < _rowCount; r++)
            {
                _row = r;
                for (int f = 0; f < _framesPerRow; f++)
                {
                    _frame = f;
                    LoadFrameData();
                    
                    if (!_partPixels.ContainsKey(_currentPart) || _partPixels[_currentPart].Count == 0)
                        continue;
                    
                    var pixels = _partPixels[_currentPart];
                    if (!_partUVs.ContainsKey(_currentPart))
                        _partUVs[_currentPart] = new Dictionary<Vector2Int, Vector2>();
                    var uvs = _partUVs[_currentPart];
                    
                    int beforeCount = pixels.Count;
                    ExpandRegionWithBoundaryUV(pixels, uvs, expandUp, expandDown, expandSide, expandSide);
                    
                    if (pixels.Count > beforeCount)
                    {
                        SaveFrameToData(false);
                        expandedCount++;
                    }
                }
            }
            
            _frame = savedFrame;
            _row = savedRow;
            _isDirty = false;
            LoadFrameData();
            
            EditorUtility.SetDirty(_data);
            Debug.Log($"区域扩展完成: 扩展了 {expandedCount} 帧");
        }
        
        /// <summary>
        /// 基于边界像素 UV 进行四边扩展
        /// 扩展顺序：先上下、再左右（这样角落会被正确覆盖）
        /// </summary>
        void ExpandRegionWithBoundaryUV(HashSet<Vector2Int> regionPixels, Dictionary<Vector2Int, Vector2> pixelUVs,
                                        int expandUp, int expandDown, int expandLeft, int expandRight)
        {
            if (regionPixels.Count == 0) return;
            
            // 1. 计算当前边界
            int minX = regionPixels.Min(p => p.x);
            int maxX = regionPixels.Max(p => p.x);
            int minY = regionPixels.Min(p => p.y);
            int maxY = regionPixels.Max(p => p.y);
            
            // 2. 向上扩展（Y 减小）
            for (int i = 1; i <= expandUp; i++)
            {
                int newY = minY - i;
                if (newY < 0) break;
                
                for (int x = minX; x <= maxX; x++)
                {
                    var boundaryPos = new Vector2Int(x, minY);
                    var newPos = new Vector2Int(x, newY);
                    
                    if (!regionPixels.Contains(newPos))
                    {
                        regionPixels.Add(newPos);
                        if (pixelUVs.TryGetValue(boundaryPos, out var uv))
                            pixelUVs[newPos] = uv;
                    }
                }
            }
            
            // 3. 向下扩展（Y 增大）
            for (int i = 1; i <= expandDown; i++)
            {
                int newY = maxY + i;
                if (newY >= _frameSize.y) break;
                
                for (int x = minX; x <= maxX; x++)
                {
                    var boundaryPos = new Vector2Int(x, maxY);
                    var newPos = new Vector2Int(x, newY);
                    
                    if (!regionPixels.Contains(newPos))
                    {
                        regionPixels.Add(newPos);
                        if (pixelUVs.TryGetValue(boundaryPos, out var uv))
                            pixelUVs[newPos] = uv;
                    }
                }
            }
            
            // 4. 重新计算边界（上下扩展后）
            int expandedMinY = Mathf.Max(0, minY - expandUp);
            int expandedMaxY = Mathf.Min(_frameSize.y - 1, maxY + expandDown);
            
            // 5. 向左扩展（X 减小）
            for (int i = 1; i <= expandLeft; i++)
            {
                int newX = minX - i;
                if (newX < 0) break;
                
                for (int y = expandedMinY; y <= expandedMaxY; y++)
                {
                    var boundaryPos = new Vector2Int(minX, y);
                    var newPos = new Vector2Int(newX, y);
                    
                    if (!regionPixels.Contains(newPos))
                    {
                        regionPixels.Add(newPos);
                        if (pixelUVs.TryGetValue(boundaryPos, out var uv))
                            pixelUVs[newPos] = uv;
                    }
                }
            }
            
            // 6. 向右扩展（X 增大）
            for (int i = 1; i <= expandRight; i++)
            {
                int newX = maxX + i;
                if (newX >= _frameSize.x) break;
                
                for (int y = expandedMinY; y <= expandedMaxY; y++)
                {
                    var boundaryPos = new Vector2Int(maxX, y);
                    var newPos = new Vector2Int(newX, y);
                    
                    if (!regionPixels.Contains(newPos))
                    {
                        regionPixels.Add(newPos);
                        if (pixelUVs.TryGetValue(boundaryPos, out var uv))
                            pixelUVs[newPos] = uv;
                    }
                }
            }
        }
        
        /// <summary>
        /// 获取部位绘制颜色 - 仅用于编辑器显示，与 UV Map 无关
        /// </summary>
        Color GetPartColor(CharacterBodyPart part)
        {
            switch (part)
            {
                case CharacterBodyPart.Head:      return new Color(0.2f, 0.8f, 0.8f, 0.6f);  // 青色
                case CharacterBodyPart.Torso:     return new Color(0.3f, 0.5f, 0.9f, 0.6f);  // 蓝色
                case CharacterBodyPart.LeftHand:  return new Color(0.9f, 0.9f, 0.2f, 0.6f);  // 黄色
                case CharacterBodyPart.RightHand: return new Color(0.9f, 0.6f, 0.2f, 0.6f);  // 橙色
                case CharacterBodyPart.LeftFoot:  return new Color(0.6f, 0.3f, 0.9f, 0.6f);  // 紫色
                case CharacterBodyPart.RightFoot: return new Color(0.9f, 0.3f, 0.6f, 0.6f);  // 粉色
                default: return new Color(0.5f, 0.5f, 0.5f, 0.5f);
            }
        }
        
        void DrawAnchors(Rect r)
        {
            foreach (var a in _anchors)
            {
                float x = r.x + a.position.x * _zoom + _zoom/2;
                float y = r.y + a.position.y * _zoom + _zoom/2;
                
                Handles.color = a.type == _anchorType ? Color.yellow : Color.cyan;
                Handles.DrawSolidDisc(new Vector3(x, y, 0), Vector3.forward, 6);
                
                Vector2 dir = GetOrientationDirVec(a.orientation) * _zoom;
                Handles.DrawLine(new Vector3(x, y), new Vector3(x + dir.x, y + dir.y));
                
                GUI.Label(new Rect(x + 8, y - 8, 100, 20), a.type.ToString(), EditorStyles.whiteMiniLabel);
            }
        }
        
        /// <summary>
        /// 获取 UV 方向对应的屏幕空间方向向量（用于可视化）
        /// </summary>
        Vector2 GetOrientationDirVec(UVOrientation orientation)
        {
            switch (orientation)
            {
                case UVOrientation.DownLeft: return new Vector2(0, -1);   // 指向上方（屏幕空间）
                case UVOrientation.UpLeft: return new Vector2(-1, 0);     // 指向左方
                case UVOrientation.DownRight: return new Vector2(1, 0);   // 指向右方
                case UVOrientation.UpRight:
                default: return new Vector2(0, 1);  // 指向下方（屏幕空间）
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
                    if (e.button == 0 && _tab == TabMode.BodyPaint)
                    {
                        bool isShift = e.shift;
                        
                        // Shift+左键在画布 = 擦除模式
                        // 左键 = 框选模式
                        if (inPalette && !isShift)
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
                        }
                        else if (inCanvas)
                        {
                            var p = GetPixelPos(e.mousePosition - _canvasOffset);
                            if (IsValidPixel(p))
                            {
                                _isSelecting = true;
                                _isErasing = isShift;  // Shift = 擦除模式
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
                    else if (e.button == 1 && inCanvas)
                    {
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
                            if (_isErasing)
                            {
                                // 擦除模式：直接删除选区内的涂色
                                _canvasSelection = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
                                DeleteCanvasSelection();
                                _canvasSelection = default;  // 擦除后清除选区
                            }
                            else
                            {
                                _canvasSelection = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
                            }
                        }
                        
                        _isErasing = false;
                        Repaint();
                        e.Use();
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
            return _paletteSize;
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
                    SetOrUpdateAnchor(_anchorType, p, _anchorOrientation, _anchorFlipX);
                    SaveWithUndo("设置锚点");
                    break;
            }
            
            Repaint();
        }
        
        void OnRightClick(Vector2 mouse)
        {
            var p = GetPixelPos(mouse);
            if (!IsValidPixel(p)) return;
            
            if (_tab == TabMode.BodyPaint && _partPixels.ContainsKey(_currentPart))
            {
                _partPixels[_currentPart].Remove(p);
                SaveWithUndo("擦除");
            }
            
            Repaint();
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
            if (_data == null || string.IsNullOrEmpty(_animName)) return;
            
            if (recordUndo)
                Undo.RecordObject(_data, undoName);
            
            var anim = _data.GetOrCreateAnimation(_animName);
            var frame = anim.GetOrCreateFrame(_frame, _row);
            
            // 保存锚点（深拷贝）
            frame.anchors.Clear();
            foreach (var anchor in _anchors)
            {
                frame.anchors.Add(new AnchorPoint
                {
                    type = anchor.type,
                    position = anchor.position,
                    orientation = anchor.orientation,
                    flipX = anchor.flipX
                });
            }
            
            // 保存部位区域
            frame.bodyRegions.Clear();
            if (_sprite != null && _sprite.isReadable)
            {
                var pixels = _sprite.GetPixels32();
                foreach (var kv in _partPixels)
                {
                    if (kv.Value.Count == 0) continue;
                    
                    var region = new BodyPartRegion
                    {
                        part = kv.Key,
                        orientation = _partOrientations.ContainsKey(kv.Key) ? _partOrientations[kv.Key] : UVOrientation.UpRight,
                        spriteFacing = _partSpriteFacings.ContainsKey(kv.Key) ? _partSpriteFacings[kv.Key] : GetDefaultSpriteFacing()
                    };
                    
                    // 获取 UV 字典
                    Dictionary<Vector2Int, Vector2> uvDict = null;
                    if (_partUVs.ContainsKey(kv.Key))
                        uvDict = _partUVs[kv.Key];
                    
                    foreach (var pos in kv.Value)
                    {
                        int gx = _frame * _frameSize.x + pos.x;
                        int gy = _sprite.height - 1 - (_row * _frameSize.y + pos.y);
                        
                        if (gx >= 0 && gx < _sprite.width && gy >= 0 && gy < _sprite.height)
                        {
                            var pixel = new BodyPartPixel
                            {
                                part = kv.Key,
                                position = pos,
                                color = pixels[gy * _sprite.width + gx]
                            };
                            
                            // 保存 UV 坐标
                            if (uvDict != null && uvDict.ContainsKey(pos))
                                pixel.uv = uvDict[pos];
                            
                            region.pixels.Add(pixel);
                        }
                    }
                    
                    frame.bodyRegions.Add(region);
                }
            }
            
            EditorUtility.SetDirty(_data);
        }
        
        void LoadFrameData()
        {
            _anchors.Clear();
            _partPixels.Clear();
            _partUVs.Clear();
            _partOrientations.Clear();
            _partSpriteFacings.Clear();
            _isDirty = false;
            
            if (_data == null) return;
            
            var anim = _data.animations.Find(a => 
                string.Equals(a.animationName, _animName, System.StringComparison.OrdinalIgnoreCase));
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
                    orientation = anchor.orientation,
                    flipX = anchor.flipX
                });
            }
            
            foreach (var region in frame.bodyRegions)
            {
                _partOrientations[region.part] = region.orientation;
                _partSpriteFacings[region.part] = region.spriteFacing;
                _partPixels[region.part] = new HashSet<Vector2Int>();
                _partUVs[region.part] = new Dictionary<Vector2Int, Vector2>();
                foreach (var px in region.pixels)
                {
                    _partPixels[region.part].Add(px.position);
                    if (px.HasUV)
                        _partUVs[region.part][px.position] = px.uv;
                }
            }
            
            Repaint();
        }
        
        void SyncFromData()
        {
            if (_data == null) return;
            
            var names = _data.GetAnimationNames();
            if (names.Count > 0)
            {
                _animIndex = 0;
                _animName = names[0];
                var anim = _data.GetAnimation(_animName);
                if (anim != null)
                    SyncFromAnimation(anim);
            }
            
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
        
        void SwitchAnimation(string newAnim)
        {
            SaveIfDirty();
            _animName = newAnim;
            
            // 切换动画时同步配置
            if (_data != null)
            {
                var anim = _data.GetAnimation(_animName);
                if (anim != null)
                    SyncFromAnimation(anim);
            }
            
            LoadFrameData();
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
            if (_data == null || string.IsNullOrEmpty(_animName)) return;
            
            var anim = _data.GetOrCreateAnimation(_animName);
            if (anim.spritesheet == null || anim.frameSize.x <= 0 || anim.frameSize.y <= 0) return;
            
            // 根据帧尺寸计算行数和每行帧数
            anim.framesPerRow = Mathf.Max(1, anim.spritesheet.width / anim.frameSize.x);
            anim.rowCount = Mathf.Max(1, anim.spritesheet.height / anim.frameSize.y);
            
            SyncFromAnimation(anim);
            EditorUtility.SetDirty(_data);
            Repaint();
        }
        
        void ImportAnimationsFromAnimator()
        {
            if (_data == null || _animatorController == null) return;
            
            Undo.RecordObject(_data, "Import Animations From Animator");
            
            var existingNames = _data.GetAnimationNames();
            int added = 0;
            
            foreach (var layer in _animatorController.layers)
            {
                foreach (var state in layer.stateMachine.states)
                {
                    string name = state.state.name;
                    if (!existingNames.Contains(name))
                    {
                        _data.GetOrCreateAnimation(name);
                        added++;
                    }
                }
            }
            
            if (added > 0)
            {
                Debug.Log($"导入了 {added} 个新动画");
                EditorUtility.SetDirty(_data);
            }
            
            // 选中第一个动画
            var names = _data.GetAnimationNames();
            if (names.Count > 0)
            {
                _animIndex = 0;
                _animName = names[0];
                var anim = _data.GetAnimation(_animName);
                if (anim != null)
                    SyncFromAnimation(anim);
            }
            
            Repaint();
        }
        
        void AddNewAnimation()
        {
            if (_data == null) return;
            
            Undo.RecordObject(_data, "Add Animation");
            
            string baseName = "NewAnimation";
            var existingNames = _data.GetAnimationNames();
            string newName = baseName;
            int i = 1;
            while (existingNames.Contains(newName))
                newName = baseName + i++;
            
            var newAnim = _data.GetOrCreateAnimation(newName);
            
            // 复制当前动画的配置作为默认值
            if (!string.IsNullOrEmpty(_animName))
            {
                var current = _data.GetAnimation(_animName);
                if (current != null)
                {
                    newAnim.spritesheet = current.spritesheet;
                    newAnim.frameSize = current.frameSize;
                    newAnim.framesPerRow = current.framesPerRow;
                    newAnim.rowCount = current.rowCount;
                }
            }
            
            _animName = newName;
            _animIndex = _data.GetAnimationNames().IndexOf(newName);
            SyncFromAnimation(newAnim);
            
            EditorUtility.SetDirty(_data);
            Repaint();
        }
        
        void RemoveCurrentAnimation()
        {
            if (_data == null || string.IsNullOrEmpty(_animName)) return;
            
            var anim = _data.GetAnimation(_animName);
            if (anim == null) return;
            
            if (!EditorUtility.DisplayDialog("删除动画", $"确定要删除动画 '{_animName}' 吗？", "删除", "取消"))
                return;
            
            Undo.RecordObject(_data, "Remove Animation");
            _data.animations.Remove(anim);
            
            var names = _data.GetAnimationNames();
            if (names.Count > 0)
            {
                _animIndex = 0;
                _animName = names[0];
                var newAnim = _data.GetAnimation(_animName);
                if (newAnim != null)
                    SyncFromAnimation(newAnim);
            }
            else
            {
                _animName = "";
                _animIndex = 0;
            }
            
            EditorUtility.SetDirty(_data);
            LoadFrameData();
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
            bool facingDown = (_row == 0 || _row == 1);   // SE/SW
            
            // 查找第一个有色像素
            Vector2Int? firstPixel = null;
            for (int y = 0; y < _frameSize.y && !firstPixel.HasValue; y++)
            {
                for (int x = 0; x < _frameSize.x; x++)
                {
                    if (cfg.IsColoredPixel(GetPixelAt(pixels, x, y)))
                    {
                        firstPixel = new Vector2Int(x, y);
                        break;
                    }
                }
            }
            
            if (!firstPixel.HasValue) return null;
            
            // 查找躯干起始点
            int torsoRowY = firstPixel.Value.y + 3;
            Vector2Int? torsoStart = null;
            if (torsoRowY < _frameSize.y)
            {
                for (int x = 0; x < _frameSize.x; x++)
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
                facingDown = facingDown,
                firstPixel = firstPixel.Value,
                torsoStart = torsoStart,
                headLeft = firstPixel.Value.x,
                headRight = firstPixel.Value.x + 4,
                footMinY = torsoStart.HasValue ? torsoStart.Value.y + 1 : _frameSize.y
            };
        }
        
        class DetectParams
        {
            public Color32[] pixels;
            public DetectConfig cfg;
            public bool facingRight, facingDown;
            public Vector2Int firstPixel;
            public Vector2Int? torsoStart;
            public int headLeft, headRight, footMinY;
            
            // 颜色映射
            public Color32 GetLeftHandColor() => facingDown ? cfg.rightHandColor : cfg.leftHandColor;
            public Color32 GetRightHandColor() => facingDown ? cfg.leftHandColor : cfg.rightHandColor;
            public Color32 GetLeftFootColor() => facingDown ? cfg.rightFootColor : cfg.leftFootColor;
            public Color32 GetRightFootColor() => facingDown ? cfg.leftFootColor : cfg.rightFootColor;
        }
        
        void AutoDetectAllParts()
        {
            var p = GetDetectParams();
            if (p == null)
            {
                Debug.LogWarning("需要可读的 Spritesheet");
                return;
            }
            
            // 根据当前行设置默认的贴图方向（SE/SW/NE/NW）
            var defaultFacing = GetDefaultSpriteFacing();
            
            // 头部
            _partPixels[CharacterBodyPart.Head] = new HashSet<Vector2Int>();
            _partSpriteFacings[CharacterBodyPart.Head] = defaultFacing;
            for (int dy = 0; dy < 3; dy++)
                for (int dx = 0; dx < 4; dx++)
                {
                    int px = p.firstPixel.x + dx, py = p.firstPixel.y + dy;
                    if (px < _frameSize.x && py < _frameSize.y)
                        _partPixels[CharacterBodyPart.Head].Add(new Vector2Int(px, py));
                }
            
            // 身体
            if (p.torsoStart.HasValue)
            {
                _partPixels[CharacterBodyPart.Torso] = new HashSet<Vector2Int>();
                _partSpriteFacings[CharacterBodyPart.Torso] = defaultFacing;
                for (int dy = 0; dy < 2; dy++)
                    for (int dx = 0; dx < 3; dx++)
                    {
                        int px = p.torsoStart.Value.x + dx, py = p.torsoStart.Value.y + dy;
                        if (px < _frameSize.x && py < _frameSize.y)
                            _partPixels[CharacterBodyPart.Torso].Add(new Vector2Int(px, py));
                    }
            }
            
            // 手脚
            DetectLimb(p, CharacterBodyPart.LeftHand, p.GetLeftHandColor());
            DetectLimb(p, CharacterBodyPart.RightHand, p.GetRightHandColor());
            DetectLimb(p, CharacterBodyPart.LeftFoot, p.GetLeftFootColor());
            DetectLimb(p, CharacterBodyPart.RightFoot, p.GetRightFootColor());
            
            // 锚点 (只设置武器锚点，头部现在用UV Map制)
            if (_partPixels.ContainsKey(CharacterBodyPart.LeftHand) && _partPixels[CharacterBodyPart.LeftHand].Count > 0)
                SetOrUpdateAnchor(AnchorType.LeftWeapon, _partPixels[CharacterBodyPart.LeftHand].First(), UVOrientation.UpRight);
            if (_partPixels.ContainsKey(CharacterBodyPart.RightHand) && _partPixels[CharacterBodyPart.RightHand].Count > 0)
                SetOrUpdateAnchor(AnchorType.RightWeapon, _partPixels[CharacterBodyPart.RightHand].First(), UVOrientation.UpRight);
        }
        
        /// <summary>
        /// 自动检测当前帧全部部位（由UI按钮调用，带撤销）
        /// </summary>
        void AutoDetectAllPartsWithUndo()
        {
            AutoDetectAllParts();
            SaveWithUndo("自动检测全部部位");
            Repaint();
        }
        
        void AutoDetectPart(CharacterBodyPart targetPart)
        {
            var p = GetDetectParams();
            if (p == null)
            {
                Debug.LogWarning("需要可读的 Spritesheet");
                return;
            }
            
            switch (targetPart)
            {
                case CharacterBodyPart.Head:
                    // 重新检测头部 (3x4 区域)，会覆盖扩展区域
                    _partPixels[CharacterBodyPart.Head] = new HashSet<Vector2Int>();
                    // 如果没有设置过贴图方向，则设置为当前行对应的默认值
                    if (!_partSpriteFacings.ContainsKey(CharacterBodyPart.Head))
                        _partSpriteFacings[CharacterBodyPart.Head] = GetDefaultSpriteFacing();
                    for (int dy = 0; dy < 3; dy++)
                        for (int dx = 0; dx < 4; dx++)
                        {
                            int px = p.firstPixel.x + dx, py = p.firstPixel.y + dy;
                            if (px < _frameSize.x && py < _frameSize.y)
                                _partPixels[CharacterBodyPart.Head].Add(new Vector2Int(px, py));
                        }
                    break;
                    
                case CharacterBodyPart.Torso:
                    // 重新检测躯干 (3x2 区域)
                    if (p.torsoStart.HasValue)
                    {
                        _partPixels[CharacterBodyPart.Torso] = new HashSet<Vector2Int>();
                        // 如果没有设置过贴图方向，则设置为当前行对应的默认值
                        if (!_partSpriteFacings.ContainsKey(CharacterBodyPart.Torso))
                            _partSpriteFacings[CharacterBodyPart.Torso] = GetDefaultSpriteFacing();
                        for (int dy = 0; dy < 2; dy++)
                            for (int dx = 0; dx < 3; dx++)
                            {
                                int px = p.torsoStart.Value.x + dx, py = p.torsoStart.Value.y + dy;
                                if (px < _frameSize.x && py < _frameSize.y)
                                    _partPixels[CharacterBodyPart.Torso].Add(new Vector2Int(px, py));
                            }
                    }
                    break;
                    
                case CharacterBodyPart.LeftHand:
                case CharacterBodyPart.RightHand:
                case CharacterBodyPart.LeftFoot:
                case CharacterBodyPart.RightFoot:
                    Color32 color = targetPart switch
                    {
                        CharacterBodyPart.LeftHand => p.GetLeftHandColor(),
                        CharacterBodyPart.RightHand => p.GetRightHandColor(),
                        CharacterBodyPart.LeftFoot => p.GetLeftFootColor(),
                        _ => p.GetRightFootColor()
                    };
                    DetectLimb(p, targetPart, color);
                    
                    if (_partPixels.ContainsKey(targetPart) && _partPixels[targetPart].Count > 0)
                    {
                        var pos = _partPixels[targetPart].First();
                        if (targetPart == CharacterBodyPart.LeftHand)
                            SetOrUpdateAnchor(AnchorType.LeftWeapon, pos, UVOrientation.UpRight);
                        else if (targetPart == CharacterBodyPart.RightHand)
                            SetOrUpdateAnchor(AnchorType.RightWeapon, pos, UVOrientation.UpRight);
                    }
                    break;
                    
                default:
                    return;
            }
            
            SaveWithUndo("自动检测部位");
            Repaint();
        }
        
        void DetectLimb(DetectParams p, CharacterBodyPart part, Color32 color)
        {
            bool isHand = part == CharacterBodyPart.LeftHand || part == CharacterBodyPart.RightHand;
            bool isLeft = part == CharacterBodyPart.LeftHand || part == CharacterBodyPart.LeftFoot;
            
            int xMin, xMax, yMin, yMax;
            bool leftToRight;
            
            if (isHand)
            {
                yMin = 0; yMax = _frameSize.y;
                if (p.facingRight == isLeft)  // 左手在右边(SE/NE)或右手在右边(SW/NW)
                {
                    xMin = p.headRight - 1; xMax = _frameSize.x;
                    leftToRight = false;
                }
                else
                {
                    xMin = 0; xMax = p.headLeft + 1;
                    leftToRight = true;
                }
            }
            else
            {
                xMin = 0; xMax = _frameSize.x;
                yMin = p.footMinY; yMax = _frameSize.y;
                leftToRight = p.facingRight != isLeft;
            }
            
            var result = FindIsolatedPixelInRange(p.pixels, color, p.cfg, xMin, xMax, yMin, yMax, leftToRight);
            if (result.HasValue)
                _partPixels[part] = new HashSet<Vector2Int> { result.Value };
        }
        
        void AutoDetectAllFrames()
        {
            if (_sprite == null || !_sprite.isReadable || _data == null)
            {
                Debug.LogWarning("需要可读的 Spritesheet");
                return;
            }
            
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
                    
                    // 重置缓存，确保每帧独立检测
                    _partPixels.Clear();
                    _partUVs.Clear();
                    _partOrientations.Clear();
                    _partSpriteFacings.Clear();
                    _anchors.Clear();
                    
                    AutoDetectAllParts();
                    SaveFrameToData(false);  // 不单独记录撤销，已经在外层记录
                    if (_partPixels.Count > 0) detectedCount++;
                }
            }
            
            _frame = savedFrame;
            _row = savedRow;
            _isDirty = false;
            LoadFrameData();
            
            EditorUtility.SetDirty(_data);
            Debug.Log($"自动检测完成: 共{totalFrames}帧({_rowCount}行×{_framesPerRow}帧), 成功检测{detectedCount}帧");
        }
        
        /// <summary>
        /// 修复所有帧的贴图方向
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
            
            int fixedCount = 0;
            int totalRegions = 0;
            
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
                        totalRegions++;
                        if (region.spriteFacing != correctFacing)
                        {
                            region.spriteFacing = correctFacing;
                            fixedCount++;
                        }
                    }
                }
            }
            
            EditorUtility.SetDirty(_data);
            LoadFrameData();
            
            Debug.Log($"贴图方向修复完成: 共 {totalRegions} 个区域，修复了 {fixedCount} 个");
        }
        
        void SetOrUpdateAnchor(AnchorType type, Vector2Int pos, UVOrientation orientation, bool flipX = false)
        {
            var existing = _anchors.Find(a => a.type == type);
            if (existing != null)
            {
                existing.position = pos;
                existing.orientation = orientation;
                existing.flipX = flipX;
            }
            else
            {
                _anchors.Add(new AnchorPoint { type = type, position = pos, orientation = orientation, flipX = flipX });
            }
        }
        
        Vector2Int? FindIsolatedPixelInRange(Color32[] pixels, Color32 targetColor, DetectConfig cfg,
            int xMin, int xMax, int yMin, int yMax, bool leftToRight)
        {
            xMin = Mathf.Max(0, xMin);
            xMax = Mathf.Min(_frameSize.x, xMax);
            yMin = Mathf.Max(0, yMin);
            yMax = Mathf.Min(_frameSize.y, yMax);
            
            if (xMin >= xMax || yMin >= yMax) return null;
            
            int xStart = leftToRight ? xMin : xMax - 1;
            int xEnd = leftToRight ? xMax : xMin - 1;
            int xStep = leftToRight ? 1 : -1;
            
            for (int x = xStart; x != xEnd; x += xStep)
            {
                for (int y = yMax - 1; y >= yMin; y--)
                {
                    var c = GetPixelAt(pixels, x, y);
                    if (cfg.ColorMatch(c, targetColor) && IsIsolatedPixel(pixels, x, y, targetColor, cfg))
                        return new Vector2Int(x, y);
                }
            }
            return null;
        }
        
        bool IsIsolatedPixel(Color32[] pixels, int x, int y, Color32 targetColor, DetectConfig cfg)
        {
            int[] dx = { 0, 0, -1, 1 };
            int[] dy = { -1, 1, 0, 0 };
            
            for (int i = 0; i < 4; i++)
            {
                int nx = x + dx[i], ny = y + dy[i];
                if (nx < 0 || nx >= _frameSize.x || ny < 0 || ny >= _frameSize.y) continue;
                if (cfg.ColorMatch(GetPixelAt(pixels, nx, ny), targetColor)) return false;
            }
            return true;
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
        /// 从 SE 方向生成其他三个方向的部位数据
        /// SW = SE 水平镜像, NE = SE 复制, NW = NE 水平镜像
        /// </summary>
        void GenerateOtherDirectionsFromSE(bool allFrames)
        {
            if (_data == null || string.IsNullOrEmpty(_animName))
            {
                Debug.LogWarning("请先选择 CharacterFrameData 和动画");
                return;
            }
            
            SaveIfDirty();
            Undo.RecordObject(_data, "从SE生成其他方向");
            
            var anim = _data.GetAnimation(_animName);
            if (anim == null) return;
            
            int savedFrame = _frame;
            int savedRow = _row;
            int generatedCount = 0;
            
            int startFrame = allFrames ? 0 : _frame;
            int endFrame = allFrames ? _framesPerRow : _frame + 1;
            
            for (int f = startFrame; f < endFrame; f++)
            {
                // 获取 SE (row 0) 的帧数据
                var seFrame = anim.GetFrame(f, 0);
                if (seFrame == null || seFrame.bodyRegions.Count == 0) continue;
                
                // 生成 SW (row 1) = SE 水平镜像
                GenerateMirroredFrame(anim, f, seFrame, 1, CharacterFacing.SouthWest);
                
                // 生成 NE (row 2) = SE 复制
                GenerateCopiedFrame(anim, f, seFrame, 2, CharacterFacing.NorthEast);
                
                // 生成 NW (row 3) = NE 水平镜像 (实际上就是SE镜像，与SW相同逻辑)
                GenerateMirroredFrame(anim, f, seFrame, 3, CharacterFacing.NorthWest);
                
                generatedCount++;
            }
            
            _frame = savedFrame;
            _row = savedRow;
            _isDirty = false;
            LoadFrameData();
            
            EditorUtility.SetDirty(_data);
            Debug.Log($"从SE生成其他方向完成: 共处理 {generatedCount} 帧");
        }
        
        /// <summary>
        /// 生成镜像帧数据（用于 SW 和 NW）
        /// </summary>
        void GenerateMirroredFrame(AnimationData anim, int frameIndex, FrameData sourceFrame, int targetRow, CharacterFacing targetFacing)
        {
            var targetFrame = anim.GetOrCreateFrame(frameIndex, targetRow);
            targetFrame.bodyRegions.Clear();
            targetFrame.anchors.Clear();
            
            // 镜像部位区域
            foreach (var sourceRegion in sourceFrame.bodyRegions)
            {
                var newRegion = new BodyPartRegion
                {
                    part = MirrorBodyPart(sourceRegion.part),  // 左右手/脚互换
                    orientation = sourceRegion.orientation,
                    spriteFacing = targetFacing
                };
                
                foreach (var px in sourceRegion.pixels)
                {
                    newRegion.pixels.Add(new BodyPartPixel
                    {
                        part = newRegion.part,
                        position = MirrorPosition(px.position),
                        color = px.color,
                        uv = px.uv
                    });
                }
                
                targetFrame.bodyRegions.Add(newRegion);
            }
            
            // 镜像锚点
            foreach (var anchor in sourceFrame.anchors)
            {
                var newAnchor = new AnchorPoint
                {
                    type = MirrorAnchorType(anchor.type),  // 左右武器互换
                    position = MirrorPosition(anchor.position),
                    orientation = anchor.orientation,
                    flipX = !anchor.flipX  // 翻转状态取反
                };
                targetFrame.anchors.Add(newAnchor);
            }
        }
        
        /// <summary>
        /// 生成复制帧数据（用于 NE）
        /// </summary>
        void GenerateCopiedFrame(AnimationData anim, int frameIndex, FrameData sourceFrame, int targetRow, CharacterFacing targetFacing)
        {
            var targetFrame = anim.GetOrCreateFrame(frameIndex, targetRow);
            targetFrame.bodyRegions.Clear();
            targetFrame.anchors.Clear();
            
            // 复制部位区域
            foreach (var sourceRegion in sourceFrame.bodyRegions)
            {
                var newRegion = new BodyPartRegion
                {
                    part = sourceRegion.part,
                    orientation = sourceRegion.orientation,
                    spriteFacing = targetFacing
                };
                
                foreach (var px in sourceRegion.pixels)
                {
                    newRegion.pixels.Add(new BodyPartPixel
                    {
                        part = px.part,
                        position = px.position,
                        color = px.color,
                        uv = px.uv
                    });
                }
                
                targetFrame.bodyRegions.Add(newRegion);
            }
            
            // 复制锚点
            foreach (var anchor in sourceFrame.anchors)
            {
                targetFrame.anchors.Add(new AnchorPoint
                {
                    type = anchor.type,
                    position = anchor.position,
                    orientation = anchor.orientation,
                    flipX = anchor.flipX
                });
            }
        }
        
        /// <summary>
        /// 水平镜像像素位置
        /// </summary>
        Vector2Int MirrorPosition(Vector2Int pos)
        {
            return new Vector2Int(_frameSize.x - 1 - pos.x, pos.y);
        }
        
        /// <summary>
        /// 镜像时左右部位互换
        /// </summary>
        CharacterBodyPart MirrorBodyPart(CharacterBodyPart part)
        {
            switch (part)
            {
                case CharacterBodyPart.LeftHand: return CharacterBodyPart.RightHand;
                case CharacterBodyPart.RightHand: return CharacterBodyPart.LeftHand;
                case CharacterBodyPart.LeftFoot: return CharacterBodyPart.RightFoot;
                case CharacterBodyPart.RightFoot: return CharacterBodyPart.LeftFoot;
                default: return part;  // Head, Torso 不变
            }
        }
        
        /// <summary>
        /// 镜像时左右武器锚点互换
        /// </summary>
        AnchorType MirrorAnchorType(AnchorType type)
        {
            switch (type)
            {
                case AnchorType.LeftWeapon: return AnchorType.RightWeapon;
                case AnchorType.RightWeapon: return AnchorType.LeftWeapon;
                default: return type;
            }
        }
        
        #endregion
        
        #region UV Map 生成 (双层)

        void GenerateDualUVMapsForCurrentAnimation()
        {
            if (_data == null || string.IsNullOrEmpty(_animName))
            {
                Debug.LogWarning("[UV Map] 请先选择 CharacterFrameData 和动画");
                return;
            }

            var anim = _data.GetAnimation(_animName);
            if (anim == null)
            {
                Debug.LogWarning($"[UV Map] 找不到动画 {_animName}");
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
    }
}
