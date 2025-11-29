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
    public enum TabMode { BodyPaint, DeadZone, Anchor }

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
        PartDirection _anchorDir = PartDirection.Down;
        bool _anchorFlipX;
        bool _showSkinColors;
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
        
        // 编辑缓存
        Dictionary<CharacterBodyPart, HashSet<Vector2Int>> _partPixels = new Dictionary<CharacterBodyPart, HashSet<Vector2Int>>();
        Dictionary<CharacterBodyPart, Dictionary<Vector2Int, int>> _partUVIndices = new Dictionary<CharacterBodyPart, Dictionary<Vector2Int, int>>();
        HashSet<Vector2Int> _deadPixels = new HashSet<Vector2Int>();
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
            float toolbarWidth = Mathf.Clamp(position.width * 0.25f, 280f, 350f);
            
            GUILayout.BeginArea(new Rect(0, 0, toolbarWidth, position.height));
            DrawToolbar();
            GUILayout.EndArea();
            
            GUILayout.BeginArea(new Rect(toolbarWidth, 0, position.width - toolbarWidth, position.height));
            DrawCanvas();
            GUILayout.EndArea();
            
            _canvasOffset = new Vector2(toolbarWidth, 0);
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
            
            // 行选择
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            int maxRow = Mathf.Max(1, _rowCount) - 1;
            int newRow = EditorGUILayout.IntSlider("行", _row, 0, maxRow);
            if (EditorGUI.EndChangeCheck() && newRow != _row)
                SwitchRow(newRow);
            if (_row < 4)
            {
                string[] hints = { "SE", "SW", "NE", "NW" };
                GUILayout.Label(hints[_row], GUILayout.Width(30));
            }
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
            _tab = (TabMode)GUILayout.Toolbar((int)_tab, new string[] { "部位上色", "死区", "锚点" });
            if (EditorGUI.EndChangeCheck())
                SaveIfDirty();
            
            EditorGUILayout.BeginVertical("box");
            switch (_tab)
            {
                case TabMode.BodyPaint: DrawBodyPaintTab(); break;
                case TabMode.DeadZone: DrawDeadZoneTab(); break;
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
                "左键涂色/设置, 右键擦除\n" +
                "中键拖动: 平移 | 滚轮: 缩放\n" +
                "快捷键: 1/2/3 切换标签页",
                MessageType.Info);
        }
        
        #endregion
        
        #region 标签页内容
        
        void DrawBodyPaintTab()
        {
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
                MarkDirty();
            }
            GUILayout.Space(3);
            if (GUILayout.Button("🔍 自动检测全部部位"))
                AutoDetectAllParts();
            
            GUILayout.Space(3);
            GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
            if (GUILayout.Button("清除全部部位"))
            {
                _partPixels.Clear();
                MarkDirty();
            }
            GUI.backgroundColor = Color.white;
        }
        
        void DrawDeadZoneTab()
        {
            GUILayout.Label("死区涂色", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("死区内的装饰不会显示\n（如躺下时被遮挡的部分）", MessageType.Info);
            EditorGUILayout.LabelField($"已涂: {_deadPixels.Count} 像素");
            
            if (GUILayout.Button("清除死区"))
            {
                _deadPixels.Clear();
                MarkDirty();
            }
        }
        
        void DrawAnchorTab()
        {
            GUILayout.Label("锚点设置", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("用于挂件定位（头盔、武器等）", MessageType.Info);
            
            _anchorType = (AnchorType)EditorGUILayout.EnumPopup("锚点类型", _anchorType);
            _anchorDir = (PartDirection)EditorGUILayout.EnumPopup("方向", _anchorDir);
            _anchorFlipX = EditorGUILayout.Toggle("水平翻转", _anchorFlipX);
            
            GUILayout.Space(5);
            GUILayout.Label("已有锚点:", EditorStyles.miniLabel);
            
            for (int i = _anchors.Count - 1; i >= 0; i--)
            {
                var a = _anchors[i];
                EditorGUILayout.BeginHorizontal();
                GUI.color = a.type == _anchorType ? Color.yellow : Color.white;
                if (GUILayout.Button(a.type.ToString(), EditorStyles.miniButtonLeft, GUILayout.Width(70)))
                {
                    _anchorType = a.type;
                    _anchorDir = a.direction;
                    _anchorFlipX = a.flipX;
                }
                GUI.color = Color.white;
                GUILayout.Label($"({a.position.x},{a.position.y})", GUILayout.Width(55));
                GUILayout.Label(a.direction.ToString(), GUILayout.Width(40));
                
                // 翻转切换
                EditorGUI.BeginChangeCheck();
                bool flip = GUILayout.Toggle(a.flipX, "翻转", EditorStyles.miniButton, GUILayout.Width(36));
                if (EditorGUI.EndChangeCheck())
                {
                    a.flipX = flip;
                    MarkDirty();
                }
                
                if (GUILayout.Button("×", GUILayout.Width(20)))
                {
                    _anchors.RemoveAt(i);
                    MarkDirty();
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
            DrawDeadZone(_display);
            DrawAnchors(_display);
            if (_zoom >= 4) DrawGrid(_display);
            
            string rowHint = _row < 4 ? new[]{ "SE", "SW", "NE", "NW" }[_row] : $"R{_row}";
            string modeStr = _tab == TabMode.BodyPaint ? $"涂色:{_currentPart}" : _tab.ToString();
            GUI.Label(new Rect(_canvas.x + 10, _canvas.y + 10, 300, 20),
                $"{_animName} | 行{_row}({rowHint}) | 帧{_frame} | {modeStr}", EditorStyles.whiteLabel);
        }
        
        void DrawBodyPixels(Rect r)
        {
            if (_paintDisplayMode == 0) return;
            
            foreach (var kv in _partPixels)
            {
                if (_paintDisplayMode == 1 && kv.Key != _currentPart) continue;
                
                Color c = GetPartColor(kv.Key);
                foreach (var p in kv.Value)
                    EditorGUI.DrawRect(new Rect(r.x + p.x * _zoom, r.y + p.y * _zoom, _zoom, _zoom), c);
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
        
        void DrawDeadZone(Rect r)
        {
            foreach (var p in _deadPixels)
                EditorGUI.DrawRect(new Rect(r.x + p.x * _zoom, r.y + p.y * _zoom, _zoom, _zoom), new Color(1, 0, 0, 0.4f));
        }
        
        void DrawAnchors(Rect r)
        {
            foreach (var a in _anchors)
            {
                float x = r.x + a.position.x * _zoom + _zoom/2;
                float y = r.y + a.position.y * _zoom + _zoom/2;
                
                Handles.color = a.type == _anchorType ? Color.yellow : Color.cyan;
                Handles.DrawSolidDisc(new Vector3(x, y, 0), Vector3.forward, 6);
                
                Vector2 dir = GetDirVec(a.direction) * _zoom;
                Handles.DrawLine(new Vector3(x, y), new Vector3(x + dir.x, y + dir.y));
                
                GUI.Label(new Rect(x + 8, y - 8, 100, 20), a.type.ToString(), EditorStyles.whiteMiniLabel);
            }
        }
        
        Vector2 GetDirVec(PartDirection d)
        {
            switch (d)
            {
                case PartDirection.Up: return new Vector2(0, -1);
                case PartDirection.Down: return new Vector2(0, 1);
                case PartDirection.Left: return new Vector2(-1, 0);
                case PartDirection.Right: return new Vector2(1, 0);
                default: return Vector2.zero;
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
                    case KeyCode.Alpha2: SaveIfDirty(); _tab = TabMode.DeadZone; Repaint(); e.Use(); break;
                    case KeyCode.Alpha3: SaveIfDirty(); _tab = TabMode.Anchor; Repaint(); e.Use(); break;
                }
            }
            
            Vector2 localMouse = e.mousePosition - _canvasOffset;
            if (!new Rect(0, 0, _canvas.width, _canvas.height).Contains(localMouse)) return;
            
            switch (e.type)
            {
                case EventType.MouseDown:
                    if (e.button == 0) { OnLeftClick(localMouse); e.Use(); }
                    else if (e.button == 1 && _tab != TabMode.Anchor) { OnRightClick(localMouse); e.Use(); }
                    else if (e.button == 2) { _panning = true; _lastMouse = localMouse; e.Use(); }
                    break;
                    
                case EventType.MouseDrag:
                    if (_panning) { _pan += localMouse - _lastMouse; _lastMouse = localMouse; Repaint(); e.Use(); }
                    else if (e.button == 0 && _tab != TabMode.Anchor) { OnLeftClick(localMouse); e.Use(); }
                    else if (e.button == 1 && _tab != TabMode.Anchor) { OnRightClick(localMouse); e.Use(); }
                    break;
                    
                case EventType.MouseUp:
                    _panning = false;
                    break;
                    
                case EventType.ScrollWheel:
                    _zoom = Mathf.Clamp(_zoom - e.delta.y * 0.5f, 2f, 50f);
                    Repaint();
                    e.Use();
                    break;
            }
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
                    SetOrUpdateAnchor(_anchorType, p, _anchorDir, _anchorFlipX);
                    break;
                case TabMode.DeadZone:
                    _deadPixels.Add(p);
                    break;
                case TabMode.BodyPaint:
                    if (!_partPixels.ContainsKey(_currentPart))
                        _partPixels[_currentPart] = new HashSet<Vector2Int>();
                    _partPixels[_currentPart].Add(p);
                    break;
            }
            
            MarkDirty();
            Repaint();
        }
        
        void OnRightClick(Vector2 mouse)
        {
            var p = GetPixelPos(mouse);
            if (!IsValidPixel(p)) return;
            
            switch (_tab)
            {
                case TabMode.DeadZone:
                    _deadPixels.Remove(p);
                    break;
                case TabMode.BodyPaint:
                    if (_partPixels.ContainsKey(_currentPart))
                        _partPixels[_currentPart].Remove(p);
                    break;
            }
            
            MarkDirty();
            Repaint();
        }
        
        #endregion
        
        #region 数据保存/加载
        
        void MarkDirty() => _isDirty = true;
        
        void SaveIfDirty()
        {
            if (_isDirty)
            {
                SaveFrameToData();
                _isDirty = false;
            }
        }
        
        void SaveFrameToData(bool recordUndo = true)
        {
            if (_data == null || string.IsNullOrEmpty(_animName)) return;
            
            if (recordUndo)
                Undo.RecordObject(_data, "Edit Frame");
            
            var anim = _data.GetOrCreateAnimation(_animName);
            var frame = anim.GetOrCreateFrame(_frame, _row);
            
            // 保存锚点
            frame.anchors.Clear();
            frame.anchors.AddRange(_anchors);
            
            // 保存部位区域
            frame.bodyRegions.Clear();
            if (_sprite != null && _sprite.isReadable)
            {
                var pixels = _sprite.GetPixels32();
                foreach (var kv in _partPixels)
                {
                    if (kv.Value.Count == 0) continue;
                    
                    var region = new BodyPartRegion { part = kv.Key };
                    
                    // 获取 UV 索引字典
                    Dictionary<Vector2Int, int> uvIndices = null;
                    if (_partUVIndices.ContainsKey(kv.Key))
                        uvIndices = _partUVIndices[kv.Key];
                    
                    foreach (var pos in kv.Value)
                    {
                        int gx = _frame * _frameSize.x + pos.x;
                        int gy = _sprite.height - 1 - (_row * _frameSize.y + pos.y);
                        
                        if (gx >= 0 && gx < _sprite.width && gy >= 0 && gy < _sprite.height)
                        {
                            region.pixels.Add(new BodyPartPixel
                            {
                                part = kv.Key,
                                position = pos,
                                color = pixels[gy * _sprite.width + gx],
                                uvIndex = uvIndices != null && uvIndices.ContainsKey(pos) ? uvIndices[pos] : -1
                            });
                        }
                    }
                    
                    frame.bodyRegions.Add(region);
                }
            }
            
            // 保存死区
            frame.deadZone.pixels.Clear();
            frame.deadZone.pixels.AddRange(_deadPixels);
            
            EditorUtility.SetDirty(_data);
        }
        
        void LoadFrameData()
        {
            _anchors.Clear();
            _partPixels.Clear();
            _partUVIndices.Clear();
            _deadPixels.Clear();
            _isDirty = false;
            
            if (_data == null) return;
            
            var anim = _data.animations.Find(a => 
                string.Equals(a.animationName, _animName, System.StringComparison.OrdinalIgnoreCase));
            if (anim == null) return;
            
            var frame = anim.GetFrame(_frame, _row);
            if (frame == null) return;
            
            _anchors.AddRange(frame.anchors);
            
            foreach (var region in frame.bodyRegions)
            {
                _partPixels[region.part] = new HashSet<Vector2Int>();
                _partUVIndices[region.part] = new Dictionary<Vector2Int, int>();
                foreach (var px in region.pixels)
                {
                    _partPixels[region.part].Add(px.position);
                    if (px.uvIndex >= 0)
                        _partUVIndices[region.part][px.position] = px.uvIndex;
                }
            }
            
            foreach (var p in frame.deadZone.pixels)
                _deadPixels.Add(p);
            
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
            
            // 头部
            _partPixels[CharacterBodyPart.Head] = new HashSet<Vector2Int>();
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
                SetOrUpdateAnchor(AnchorType.LeftWeapon, _partPixels[CharacterBodyPart.LeftHand].First(), PartDirection.Down);
            if (_partPixels.ContainsKey(CharacterBodyPart.RightHand) && _partPixels[CharacterBodyPart.RightHand].Count > 0)
                SetOrUpdateAnchor(AnchorType.RightWeapon, _partPixels[CharacterBodyPart.RightHand].First(), PartDirection.Down);
            
            MarkDirty();
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
            
            Color32 color;
            switch (targetPart)
            {
                case CharacterBodyPart.LeftHand: color = p.GetLeftHandColor(); break;
                case CharacterBodyPart.RightHand: color = p.GetRightHandColor(); break;
                case CharacterBodyPart.LeftFoot: color = p.GetLeftFootColor(); break;
                case CharacterBodyPart.RightFoot: color = p.GetRightFootColor(); break;
                default: return;
            }
            
            DetectLimb(p, targetPart, color);
            
            if (_partPixels.ContainsKey(targetPart) && _partPixels[targetPart].Count > 0)
            {
                var pos = _partPixels[targetPart].First();
                if (targetPart == CharacterBodyPart.LeftHand)
                    SetOrUpdateAnchor(AnchorType.LeftWeapon, pos, PartDirection.Down);
                else if (targetPart == CharacterBodyPart.RightHand)
                    SetOrUpdateAnchor(AnchorType.RightWeapon, pos, PartDirection.Down);
            }
            
            MarkDirty();
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
                    _partUVIndices.Clear();
                    _anchors.Clear();
                    _deadPixels.Clear();
                    
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
        
        void SetOrUpdateAnchor(AnchorType type, Vector2Int pos, PartDirection dir, bool flipX = false)
        {
            var existing = _anchors.Find(a => a.type == type);
            if (existing != null)
            {
                existing.position = pos;
                existing.direction = dir;
                existing.flipX = flipX;
            }
            else
            {
                _anchors.Add(new AnchorPoint { type = type, position = pos, direction = dir, flipX = flipX });
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
        
        #endregion
        
        #region UV Map 生成 (双层)
        
        // Body Part ID 常量 (对应 Shader 中的定义)
        const float ID_NONE = 0f;
        const float ID_HEAD = 0.1f;       // 头部 - 头盔/胡子/头发
        const float ID_TORSO = 0.2f;      // 躯干 - 服装
        const float ID_LEFTHAND = 0.4f;   // 左手 - 手套
        const float ID_RIGHTHAND = 0.5f;  // 右手 - 手套
        const float ID_LEFTFOOT = 0.6f;   // 左脚 - 鞋子
        const float ID_RIGHTFOOT = 0.7f;  // 右脚 - 鞋子
        
        void GenerateDualUVMapsForCurrentAnimation()
        {
            if (_data == null || string.IsNullOrEmpty(_animName))
            {
                Debug.LogWarning("[UV Map] 请先选择 CharacterFrameData 和动画");
                return;
            }
            
            var anim = _data.GetAnimation(_animName);
            GenerateDualUVMapsForAnimation(anim);
            AssetDatabase.Refresh();
        }
        
        void GenerateAllDualUVMaps()
        {
            if (_data == null)
            {
                Debug.LogWarning("[UV Map] 请先选择 CharacterFrameData");
                return;
            }
            
            int count = 0;
            foreach (var anim in _data.animations)
            {
                if (GenerateDualUVMapsForAnimation(anim))
                    count++;
            }
            
            AssetDatabase.Refresh();
            Debug.Log($"[UV Map] 已生成 {count} 个动画的双层 UV Map");
        }
        
        bool GenerateDualUVMapsForAnimation(AnimationData anim)
        {
            if (anim == null || anim.spritesheet == null)
            {
                Debug.LogWarning($"[UV Map] 动画 {anim?.animationName ?? "null"} 没有 spritesheet");
                return false;
            }
            
            string spritesheetPath = AssetDatabase.GetAssetPath(anim.spritesheet);
            string directory = Path.GetDirectoryName(spritesheetPath);
            string baseName = Path.GetFileNameWithoutExtension(spritesheetPath);
            
            // 生成身体层 UV Map
            var bodyTex = GenerateBodyUVMapTexture(anim);
            if (bodyTex != null)
            {
                string bodyPath = Path.Combine(directory, baseName + "_BodyUV.png");
                SaveUVMapTexture(bodyTex, bodyPath);
                DestroyImmediate(bodyTex);
                
                var loadedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(bodyPath);
                if (loadedTex != null)
                {
                    anim.bodyUVMap = loadedTex;
                    Debug.Log($"[UV Map] 身体层: {bodyPath}");
                }
            }
            
            // 生成头部层 UV Map
            var headTex = GenerateHeadUVMapTexture(anim);
            if (headTex != null)
            {
                string headPath = Path.Combine(directory, baseName + "_HeadUV.png");
                SaveUVMapTexture(headTex, headPath);
                DestroyImmediate(headTex);
                
                var loadedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(headPath);
                if (loadedTex != null)
                {
                    anim.headUVMap = loadedTex;
                    Debug.Log($"[UV Map] 头部层: {headPath}");
                }
            }
            
            EditorUtility.SetDirty(_data);
            AssetDatabase.SaveAssets();
            return true;
        }
        
        void SaveUVMapTexture(Texture2D tex, string path)
        {
            byte[] pngData = tex.EncodeToPNG();
            File.WriteAllBytes(path, pngData);
            AssetDatabase.ImportAsset(path);
            
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
        }
        
        /// <summary>
        /// 生成身体层 UV Map: 躯干 + 手 + 脚
        /// </summary>
        Texture2D GenerateBodyUVMapTexture(AnimationData anim)
        {
            if (anim.spritesheet == null) return null;
            
            int width = anim.spritesheet.width;
            int height = anim.spritesheet.height;
            
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            
            var defaultColor = new Color(0, 0, ID_NONE, 1);
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = defaultColor;
            
            int frameW = anim.frameSize.x;
            int frameH = anim.frameSize.y;
            
            foreach (var frame in anim.frames)
            {
                int frameOffsetX = frame.frameIndex * frameW;
                int frameOffsetY = (anim.rowCount - 1 - frame.rowIndex) * frameH;
                
                // 躯干
                ProcessRegionForUVMap(frame, CharacterBodyPart.Torso, ID_TORSO, pixels, width, height, frameOffsetX, frameOffsetY, frameH);
                
                // 手脚
                ProcessBodyPartForUVMap(frame, CharacterBodyPart.LeftHand, ID_LEFTHAND, pixels, width, height, frameOffsetX, frameOffsetY, frameH);
                ProcessBodyPartForUVMap(frame, CharacterBodyPart.RightHand, ID_RIGHTHAND, pixels, width, height, frameOffsetX, frameOffsetY, frameH);
                ProcessBodyPartForUVMap(frame, CharacterBodyPart.LeftFoot, ID_LEFTFOOT, pixels, width, height, frameOffsetX, frameOffsetY, frameH);
                ProcessBodyPartForUVMap(frame, CharacterBodyPart.RightFoot, ID_RIGHTFOOT, pixels, width, height, frameOffsetX, frameOffsetY, frameH);
            }
            
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
        
        /// <summary>
        /// 生成头部层 UV Map: 头部 + 扩展区域
        /// </summary>
        Texture2D GenerateHeadUVMapTexture(AnimationData anim)
        {
            if (anim.spritesheet == null) return null;
            
            int width = anim.spritesheet.width;
            int height = anim.spritesheet.height;
            
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            
            var defaultColor = new Color(0, 0, ID_NONE, 1);
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = defaultColor;
            
            int frameW = anim.frameSize.x;
            int frameH = anim.frameSize.y;
            
            foreach (var frame in anim.frames)
            {
                int frameOffsetX = frame.frameIndex * frameW;
                int frameOffsetY = (anim.rowCount - 1 - frame.rowIndex) * frameH;
                
                // 头部区域 + 扩展
                ProcessHeadRegionWithExpansion(frame, anim, ID_HEAD, pixels, width, height, frameOffsetX, frameOffsetY, frameH);
            }
            
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
        
        /// <summary>
        /// 处理头部区域，并根据配置扩展
        /// </summary>
        void ProcessHeadRegionWithExpansion(FrameData frame, AnimationData anim, float partID,
                                            Color[] pixels, int texWidth, int texHeight,
                                            int frameOffsetX, int frameOffsetY, int frameH)
        {
            var headRegion = frame.GetRegion(CharacterBodyPart.Head);
            if (headRegion == null || headRegion.pixels.Count == 0) return;
            
            // 获取头部包围盒
            var bounds = headRegion.GetBounds();
            
            // 扩展配置
            int expandUp = _data.headExpandUp;
            int expandSide = _data.headExpandSide;
            int expandDown = _data.headExpandDown;
            
            // 计算扩展后的范围
            int minX = Mathf.Max(0, bounds.x - expandSide);
            int maxX = Mathf.Min(anim.frameSize.x - 1, bounds.xMax - 1 + expandSide);
            int minY = Mathf.Max(0, bounds.y - expandUp);
            int maxY = Mathf.Min(anim.frameSize.y - 1, bounds.yMax - 1 + expandDown);
            
            // 收集身体层占用的像素
            HashSet<Vector2Int> bodyOccupied = new HashSet<Vector2Int>();
            CollectBodyOccupiedPixels(frame, bodyOccupied);
            
            // 生成扩展区域的像素列表
            List<Vector2Int> expandedPixels = new List<Vector2Int>();
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    var pos = new Vector2Int(x, y);
                    if (!bodyOccupied.Contains(pos))
                        expandedPixels.Add(pos);
                }
            }
            
            // 处理扩展后的头部区域
            ProcessExpandedHeadRegion(expandedPixels, frame.rowIndex, partID, pixels, texWidth, texHeight, frameOffsetX, frameOffsetY, frameH, frame.deadZone);
        }
        
        void CollectBodyOccupiedPixels(FrameData frame, HashSet<Vector2Int> occupied)
        {
            var parts = new[] { CharacterBodyPart.Torso, CharacterBodyPart.LeftHand, CharacterBodyPart.RightHand,
                               CharacterBodyPart.LeftFoot, CharacterBodyPart.RightFoot };
            foreach (var part in parts)
            {
                var region = frame.GetRegion(part);
                if (region != null)
                {
                    foreach (var px in region.pixels)
                        occupied.Add(px.position);
                }
            }
        }
        
        void ProcessExpandedHeadRegion(List<Vector2Int> expandedPixels, int rowIndex, float partID,
                                       Color[] pixels, int texWidth, int texHeight,
                                       int frameOffsetX, int frameOffsetY, int frameH,
                                       DeadZoneMark deadZone)
        {
            if (expandedPixels.Count == 0) return;
            
            // 计算扩展区域的包围盒
            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;
            foreach (var p in expandedPixels)
            {
                minX = Mathf.Min(minX, p.x);
                maxX = Mathf.Max(maxX, p.x);
                minY = Mathf.Min(minY, p.y);
                maxY = Mathf.Max(maxY, p.y);
            }
            int charW = maxX - minX + 1;
            int charH = maxY - minY + 1;
            
            foreach (var pos in expandedPixels)
            {
                // 计算相对位置
                float relX = charW > 1 ? (float)(pos.x - minX) / (charW - 1) : 0.5f;
                float relY = charH > 1 ? (float)(pos.y - minY) / (charH - 1) : 0.5f;
                
                float texU = relX;
                float texV = relY;
                
                bool isDead = deadZone != null && deadZone.Contains(pos);
                
                int globalX = frameOffsetX + pos.x;
                int globalY = frameOffsetY + (frameH - 1 - pos.y);
                
                if (globalX >= 0 && globalX < texWidth && globalY >= 0 && globalY < texHeight)
                {
                    pixels[globalY * texWidth + globalX] = new Color(texU, texV, partID, isDead ? 0f : 1f);
                }
            }
        }
        
        void ProcessRegionForUVMap(FrameData frame, CharacterBodyPart part, float partID,
                                   Color[] pixels, int texWidth, int texHeight,
                                   int frameOffsetX, int frameOffsetY, int frameH)
        {
            var region = frame.GetRegion(part);
            if (region == null || region.pixels.Count == 0) return;
            
            var bounds = region.GetBounds();
            int regionW = bounds.width;
            int regionH = bounds.height;
            
            foreach (var px in region.pixels)
            {
                float relX = regionW > 1 ? (float)(px.position.x - bounds.x) / (regionW - 1) : 0.5f;
                float relY = regionH > 1 ? (float)(px.position.y - bounds.y) / (regionH - 1) : 0.5f;
                
                bool isDead = frame.IsInDeadZone(px.position);
                
                int globalX = frameOffsetX + px.position.x;
                int globalY = frameOffsetY + (frameH - 1 - px.position.y);
                
                if (globalX >= 0 && globalX < texWidth && globalY >= 0 && globalY < texHeight)
                {
                    pixels[globalY * texWidth + globalX] = new Color(relX, relY, partID, isDead ? 0f : 1f);
                }
            }
        }
        
        void ProcessBodyPartForUVMap(FrameData frame, CharacterBodyPart part, float partID,
                                     Color[] pixels, int texWidth, int texHeight,
                                     int frameOffsetX, int frameOffsetY, int frameH)
        {
            var region = frame.GetRegion(part);
            if (region == null || region.pixels.Count == 0) return;
            
            foreach (var px in region.pixels)
            {
                bool isDead = frame.IsInDeadZone(px.position);
                
                int globalX = frameOffsetX + px.position.x;
                int globalY = frameOffsetY + (frameH - 1 - px.position.y);
                
                if (globalX >= 0 && globalX < texWidth && globalY >= 0 && globalY < texHeight)
                {
                    pixels[globalY * texWidth + globalX] = new Color(0, 0, partID, isDead ? 0f : 1f);
                }
            }
        }
        
        #endregion
    }
}
