using System.Collections.Generic;
using System.Linq;

using EquipmentSystem.Data;

using UnityEditor;
using UnityEditor.Animations;

using UnityEngine;

using BodyPart = EquipmentSystem.Data.BodyPart;

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
        BodyPart _currentPart = BodyPart.Torso;
        AnchorType _anchorType = AnchorType.Head;
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
        Dictionary<BodyPart, HashSet<Vector2Int>> _partPixels = new Dictionary<BodyPart, HashSet<Vector2Int>>();
        Dictionary<BodyPart, PartDirection> _partDirections = new Dictionary<BodyPart, PartDirection>();
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
            DrawPartButton(BodyPart.Head, "头部", new Color(0.2f, 0.9f, 0.2f));
            DrawPartButton(BodyPart.Torso, "身体", new Color(0.2f, 0.7f, 0.2f));
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            DrawPartButton(BodyPart.LeftHand, "左手", new Color(1.0f, 0.8f, 0.0f));
            DrawPartButton(BodyPart.RightHand, "右手", new Color(1.0f, 0.5f, 0.0f));
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            DrawPartButton(BodyPart.LeftFoot, "左脚", new Color(0.3f, 0.5f, 1.0f));
            DrawPartButton(BodyPart.RightFoot, "右脚", new Color(0.8f, 0.2f, 0.8f));
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
            
            GUILayout.Space(5);
            
            EditorGUILayout.BeginVertical("helpbox");
            GUILayout.Label($"当前: {_currentPart}", EditorStyles.boldLabel);
            if (!_partDirections.ContainsKey(_currentPart))
                _partDirections[_currentPart] = PartDirection.Down;
            _partDirections[_currentPart] = (PartDirection)EditorGUILayout.EnumPopup("方向", _partDirections[_currentPart]);
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
        }
        
        void DrawPartButton(BodyPart part, string label, Color color)
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
        
        Color GetPartColor(BodyPart part)
        {
            switch (part)
            {
                case BodyPart.Head:      return new Color(0.2f, 0.9f, 0.2f, 0.6f);
                case BodyPart.Torso:     return new Color(0.2f, 0.7f, 0.2f, 0.6f);
                case BodyPart.LeftHand:  return new Color(1.0f, 0.8f, 0.0f, 0.7f);
                case BodyPart.RightHand: return new Color(1.0f, 0.5f, 0.0f, 0.7f);
                case BodyPart.LeftFoot:  return new Color(0.3f, 0.5f, 1.0f, 0.7f);
                case BodyPart.RightFoot: return new Color(0.8f, 0.2f, 0.8f, 0.7f);
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
                    
                    var region = new BodyPartRegion
                    {
                        part = kv.Key,
                        direction = _partDirections.ContainsKey(kv.Key) ? _partDirections[kv.Key] : PartDirection.Down
                    };
                    
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
                                color = pixels[gy * _sprite.width + gx]
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
            _partDirections.Clear();
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
                _partDirections[region.part] = region.direction;
                _partPixels[region.part] = new HashSet<Vector2Int>();
                foreach (var px in region.pixels)
                    _partPixels[region.part].Add(px.position);
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
            _partPixels[BodyPart.Head] = new HashSet<Vector2Int>();
            for (int dy = 0; dy < 3; dy++)
                for (int dx = 0; dx < 4; dx++)
                {
                    int px = p.firstPixel.x + dx, py = p.firstPixel.y + dy;
                    if (px < _frameSize.x && py < _frameSize.y)
                        _partPixels[BodyPart.Head].Add(new Vector2Int(px, py));
                }
            
            // 身体
            if (p.torsoStart.HasValue)
            {
                _partPixels[BodyPart.Torso] = new HashSet<Vector2Int>();
                for (int dy = 0; dy < 2; dy++)
                    for (int dx = 0; dx < 3; dx++)
                    {
                        int px = p.torsoStart.Value.x + dx, py = p.torsoStart.Value.y + dy;
                        if (px < _frameSize.x && py < _frameSize.y)
                            _partPixels[BodyPart.Torso].Add(new Vector2Int(px, py));
                    }
            }
            
            // 手脚
            DetectLimb(p, BodyPart.LeftHand, p.GetLeftHandColor());
            DetectLimb(p, BodyPart.RightHand, p.GetRightHandColor());
            DetectLimb(p, BodyPart.LeftFoot, p.GetLeftFootColor());
            DetectLimb(p, BodyPart.RightFoot, p.GetRightFootColor());
            
            // 锚点
            SetOrUpdateAnchor(AnchorType.Head, p.firstPixel, PartDirection.Down);
            if (_partPixels.ContainsKey(BodyPart.LeftHand) && _partPixels[BodyPart.LeftHand].Count > 0)
                SetOrUpdateAnchor(AnchorType.LeftWeapon, _partPixels[BodyPart.LeftHand].First(), PartDirection.Down);
            if (_partPixels.ContainsKey(BodyPart.RightHand) && _partPixels[BodyPart.RightHand].Count > 0)
                SetOrUpdateAnchor(AnchorType.RightWeapon, _partPixels[BodyPart.RightHand].First(), PartDirection.Down);
            
            MarkDirty();
            Repaint();
        }
        
        void AutoDetectPart(BodyPart targetPart)
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
                case BodyPart.LeftHand: color = p.GetLeftHandColor(); break;
                case BodyPart.RightHand: color = p.GetRightHandColor(); break;
                case BodyPart.LeftFoot: color = p.GetLeftFootColor(); break;
                case BodyPart.RightFoot: color = p.GetRightFootColor(); break;
                default: return;
            }
            
            DetectLimb(p, targetPart, color);
            
            if (_partPixels.ContainsKey(targetPart) && _partPixels[targetPart].Count > 0)
            {
                var pos = _partPixels[targetPart].First();
                if (targetPart == BodyPart.LeftHand)
                    SetOrUpdateAnchor(AnchorType.LeftWeapon, pos, PartDirection.Down);
                else if (targetPart == BodyPart.RightHand)
                    SetOrUpdateAnchor(AnchorType.RightWeapon, pos, PartDirection.Down);
            }
            
            MarkDirty();
            Repaint();
        }
        
        void DetectLimb(DetectParams p, BodyPart part, Color32 color)
        {
            bool isHand = part == BodyPart.LeftHand || part == BodyPart.RightHand;
            bool isLeft = part == BodyPart.LeftHand || part == BodyPart.LeftFoot;
            
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
                    _partDirections.Clear();
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
    }
}
