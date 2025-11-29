using UnityEngine;
using UnityEditor;
using EquipmentSystem.Data;

namespace EquipmentSystem.Editor
{
    /// <summary>
    /// 装备模板编辑器 - 支持 4 方向模板 (SE/SW/NE/NW)
    /// 可以从 Sprite 自动生成模板，或手动绘制
    /// </summary>
    public class EquipmentTemplateEditor : EditorWindow
    {
        [MenuItem("Tools/Equipment System/Equipment Template Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<EquipmentTemplateEditor>("装备模板编辑器");
            window.minSize = new Vector2(700, 500);
        }
        
        EquipmentTemplate _template;
        
        // 当前编辑的方向
        int _currentDirection = 0;  // 0=SE, 1=SW, 2=NE, 3=NW
        static readonly string[] DirectionNames = { "SE (东南)", "SW (西南)", "NE (东北)", "NW (西北)" };
        
        // 画布状态
        float _zoom = 8f;
        Vector2 _pan = Vector2.zero;
        Rect _canvasArea;
        Rect _display;
        
        // 预览和生成用的 Sprite
        Sprite _previewSprite;
        Sprite[] _generateSprites = new Sprite[4];  // 用于批量生成
        
        // 左侧面板宽度
        const float LEFT_PANEL_WIDTH = 320f;
        
        // 检测配置
        int _outlineThreshold = 30;
        
        void OnEnable()
        {
            Undo.undoRedoPerformed += Repaint;
        }
        
        void OnDisable()
        {
            Undo.undoRedoPerformed -= Repaint;
        }
        
        void OnGUI()
        {
            Rect leftPanelRect = new Rect(0, 0, LEFT_PANEL_WIDTH, position.height);
            _canvasArea = new Rect(LEFT_PANEL_WIDTH, 0, position.width - LEFT_PANEL_WIDTH, position.height);
            
            // 先绘制画布
            DrawCanvas();
            
            // 再绘制左侧面板
            EditorGUI.DrawRect(leftPanelRect, new Color(0.22f, 0.22f, 0.22f));
            GUILayout.BeginArea(leftPanelRect);
            DrawLeftPanel();
            GUILayout.EndArea();
            
            HandleInput();
        }
        
        Vector2 _leftPanelScroll;
        
        void DrawLeftPanel()
        {
            _leftPanelScroll = GUILayout.BeginScrollView(_leftPanelScroll);
            
            GUILayout.Label("装备模板编辑器", EditorStyles.boldLabel);
            GUILayout.Space(10);
            
            // 模板选择
            EditorGUI.BeginChangeCheck();
            _template = (EquipmentTemplate)EditorGUILayout.ObjectField("模板", _template, typeof(EquipmentTemplate), false);
            if (EditorGUI.EndChangeCheck())
            {
                Repaint();
            }
            
            if (_template == null)
            {
                EditorGUILayout.HelpBox("请选择或创建一个 Equipment Template", MessageType.Info);
                if (GUILayout.Button("创建新模板"))
                {
                    CreateNewTemplate();
                }
                GUILayout.EndScrollView();
                return;
            }
            
            GUILayout.Space(10);
            
            // 贴图尺寸
            EditorGUI.BeginChangeCheck();
            _template.textureSize = EditorGUILayout.Vector2IntField("贴图尺寸", _template.textureSize);
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(_template);
            }
            
            GUILayout.Space(10);
            
            // 方向选择
            GUILayout.Label("方向", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            _currentDirection = GUILayout.Toolbar(_currentDirection, DirectionNames);
            if (EditorGUI.EndChangeCheck())
            {
                Repaint();
            }
            
            // 当前方向的模板信息
            var currentTemplate = GetCurrentDirectionTemplate();
            EditorGUILayout.BeginVertical("helpbox");
            GUILayout.Label($"当前: {DirectionNames[_currentDirection]}", EditorStyles.boldLabel);
            int pixelCount = currentTemplate?.pixels?.Count ?? 0;
            EditorGUILayout.LabelField($"像素数: {pixelCount}");
            if (currentTemplate != null && pixelCount > 0)
            {
                var bounds = currentTemplate.GetBounds();
                EditorGUILayout.LabelField($"范围: ({bounds.x},{bounds.y}) - ({bounds.xMax-1},{bounds.yMax-1})");
            }
            EditorGUILayout.EndVertical();
            
            GUILayout.Space(10);
            
            // 从 Sprite 生成
            GUILayout.Label("自动生成", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("拖入 Sprite 自动生成模板\n未填的方向会自动用 SE 填充", MessageType.Info);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("SE (必填):", GUILayout.Width(60));
            _generateSprites[0] = (Sprite)EditorGUILayout.ObjectField(_generateSprites[0], typeof(Sprite), false, GUILayout.Height(18));
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("SW:", GUILayout.Width(60));
            _generateSprites[1] = (Sprite)EditorGUILayout.ObjectField(_generateSprites[1], typeof(Sprite), false, GUILayout.Height(18));
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("NE:", GUILayout.Width(60));
            _generateSprites[2] = (Sprite)EditorGUILayout.ObjectField(_generateSprites[2], typeof(Sprite), false, GUILayout.Height(18));
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("NW:", GUILayout.Width(60));
            _generateSprites[3] = (Sprite)EditorGUILayout.ObjectField(_generateSprites[3], typeof(Sprite), false, GUILayout.Height(18));
            EditorGUILayout.EndHorizontal();
            
            if (GUILayout.Button("生成全部方向"))
            {
                GenerateFromAllSprites();
            }
            
            GUILayout.Space(10);
            
            // 复制按钮
            GUILayout.Label("快捷操作", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("SE→全部"))
                CopyTemplateToAll(0);
            if (GUILayout.Button("镜像SE→SW"))
                MirrorTemplate(0, 1);
            EditorGUILayout.EndHorizontal();
            
            if (GUILayout.Button($"清除 {DirectionNames[_currentDirection]}"))
            {
                ClearCurrentDirection();
            }
            
            GUILayout.Space(10);
            
            // 帮助信息
            EditorGUILayout.HelpBox(
                "左键: 添加像素 | 右键: 删除像素\n" +
                "中键拖动: 平移 | 滚轮: 缩放",
                MessageType.Info);
            
            GUILayout.EndScrollView();
        }
        
        void DrawCanvas()
        {
            EditorGUI.DrawRect(_canvasArea, new Color(0.15f, 0.15f, 0.15f));
            
            if (_template == null) return;
            
            float w = _template.textureSize.x * _zoom;
            float h = _template.textureSize.y * _zoom;
            var center = _canvasArea.center + _pan;
            _display = new Rect(center.x - w / 2, center.y - h / 2, w, h);
            
            // 绘制棋盘格背景
            DrawCheckerboard(_display);
            
            // 绘制预览贴图
            if (_previewSprite != null)
            {
                DrawSprite(_previewSprite, _display);
            }
            
            // 绘制当前方向的像素
            var currentTemplate = GetCurrentDirectionTemplate();
            if (currentTemplate?.pixels != null)
            {
                DrawPixels(currentTemplate.pixels);
            }
            
            // 绘制网格
            if (_zoom >= 4)
                DrawGrid(_display);
            
            // 绘制信息
            GUI.Label(new Rect(_canvasArea.x + 10, _canvasArea.y + 10, 300, 20),
                $"{_template.name} | {DirectionNames[_currentDirection]} | {_template.textureSize.x}x{_template.textureSize.y}",
                EditorStyles.whiteLabel);
        }
        
        DirectionTemplate GetCurrentDirectionTemplate()
        {
            if (_template == null) return null;
            return _template.GetTemplate(_currentDirection);
        }
        
        void DrawSprite(Sprite sprite, Rect displayRect)
        {
            if (sprite == null || sprite.texture == null) return;
            
            Texture2D tex = sprite.texture;
            Rect spriteRect = sprite.rect;
            
            Rect uvRect = new Rect(
                spriteRect.x / tex.width,
                spriteRect.y / tex.height,
                spriteRect.width / tex.width,
                spriteRect.height / tex.height);
            
            GUI.DrawTextureWithTexCoords(displayRect, tex, uvRect);
        }
        
        void DrawPixels(System.Collections.Generic.List<Vector2Int> pixels)
        {
            if (pixels == null || pixels.Count == 0) return;
            
            int maxIndex = pixels.Count - 1;
            
            for (int i = 0; i < pixels.Count; i++)
            {
                var p = pixels[i];
                var rect = new Rect(
                    _display.x + p.x * _zoom,
                    _display.y + (_template.textureSize.y - 1 - p.y) * _zoom,
                    _zoom, _zoom);
                
                // 用渐变色显示索引
                float t = maxIndex > 0 ? (float)i / maxIndex : 0;
                float hue = 0.55f;  // 青蓝色
                float sat = 0.3f + t * 0.6f;
                float val = 0.95f - t * 0.4f;
                Color c = Color.HSVToRGB(hue, sat, val);
                c.a = 0.75f;
                
                EditorGUI.DrawRect(rect, c);
            }
        }
        
        void DrawCheckerboard(Rect rect)
        {
            int size = 8;
            for (int x = 0; x < rect.width; x += size)
            {
                for (int y = 0; y < rect.height; y += size)
                {
                    bool dark = ((x / size) + (y / size)) % 2 == 0;
                    EditorGUI.DrawRect(new Rect(rect.x + x, rect.y + y, size, size),
                        dark ? new Color(0.3f, 0.3f, 0.3f) : new Color(0.4f, 0.4f, 0.4f));
                }
            }
        }
        
        void DrawGrid(Rect rect)
        {
            Handles.color = new Color(1, 1, 1, 0.1f);
            
            for (int x = 0; x <= _template.textureSize.x; x++)
            {
                float px = rect.x + x * _zoom;
                Handles.DrawLine(new Vector3(px, rect.y), new Vector3(px, rect.yMax));
            }
            
            for (int y = 0; y <= _template.textureSize.y; y++)
            {
                float py = rect.y + y * _zoom;
                Handles.DrawLine(new Vector3(rect.x, py), new Vector3(rect.xMax, py));
            }
        }
        
        void HandleInput()
        {
            var e = Event.current;
            
            if (!_canvasArea.Contains(e.mousePosition)) return;
            
            // 缩放
            if (e.type == EventType.ScrollWheel)
            {
                float delta = -e.delta.y * 0.1f;
                _zoom = Mathf.Clamp(_zoom * (1 + delta), 2f, 64f);
                e.Use();
                Repaint();
            }
            
            // 平移
            if (e.type == EventType.MouseDrag && e.button == 2)
            {
                _pan += e.delta;
                e.Use();
                Repaint();
            }
            
            // 绘制/擦除
            if (_template != null && _display.Contains(e.mousePosition))
            {
                if (e.type == EventType.MouseDown || e.type == EventType.MouseDrag)
                {
                    if (e.button == 0 || e.button == 1)
                    {
                        Vector2Int pixel = GetPixelAt(e.mousePosition);
                        
                        if (pixel.x >= 0 && pixel.x < _template.textureSize.x &&
                            pixel.y >= 0 && pixel.y < _template.textureSize.y)
                        {
                            var template = GetCurrentDirectionTemplate();
                            if (template == null)
                            {
                                // 创建新的方向模板
                                template = new DirectionTemplate();
                                SetCurrentDirectionTemplate(template);
                            }
                            
                            var pixels = template.pixels;
                            
                            if (e.button == 0) // 添加
                            {
                                if (!pixels.Contains(pixel))
                                {
                                    Undo.RecordObject(_template, "Add Pixel");
                                    pixels.Add(pixel);
                                    EditorUtility.SetDirty(_template);
                                }
                            }
                            else if (e.button == 1) // 删除
                            {
                                if (pixels.Contains(pixel))
                                {
                                    Undo.RecordObject(_template, "Remove Pixel");
                                    pixels.Remove(pixel);
                                    EditorUtility.SetDirty(_template);
                                }
                            }
                        }
                        
                        e.Use();
                        Repaint();
                    }
                }
            }
        }
        
        void SetCurrentDirectionTemplate(DirectionTemplate template)
        {
            switch (_currentDirection)
            {
                case 0: _template.SE = template; break;
                case 1: _template.SW = template; break;
                case 2: _template.NE = template; break;
                case 3: _template.NW = template; break;
            }
        }
        
        Vector2Int GetPixelAt(Vector2 mousePos)
        {
            int x = Mathf.FloorToInt((mousePos.x - _display.x) / _zoom);
            int y = _template.textureSize.y - 1 - Mathf.FloorToInt((mousePos.y - _display.y) / _zoom);
            return new Vector2Int(x, y);
        }
        
        void CreateNewTemplate()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "创建装备模板", "NewEquipmentTemplate", "asset",
                "选择保存位置");
            
            if (!string.IsNullOrEmpty(path))
            {
                var template = ScriptableObject.CreateInstance<EquipmentTemplate>();
                AssetDatabase.CreateAsset(template, path);
                AssetDatabase.SaveAssets();
                _template = template;
            }
        }
        
        void GenerateFromAllSprites()
        {
            if (_template == null) return;
            
            // SE 必须有
            if (_generateSprites[0] == null)
            {
                Debug.LogWarning("[模板] 请先填写 SE 方向的 Sprite");
                return;
            }
            
            Undo.RecordObject(_template, "Generate All Directions");
            
            // 先生成 SE
            var seTemplate = GenerateFromSprite(_generateSprites[0]);
            _template.SE = seTemplate;
            Debug.Log($"[模板] SE: 从 {_generateSprites[0].name} 生成了 {seTemplate.pixels.Count} 个像素");
            
            // 其他方向：有 Sprite 就用对应的，没有就用 SE 的
            for (int i = 1; i < 4; i++)
            {
                var sprite = _generateSprites[i] ?? _generateSprites[0];  // 回退到 SE
                var template = GenerateFromSprite(sprite);
                switch (i)
                {
                    case 1: _template.SW = template; break;
                    case 2: _template.NE = template; break;
                    case 3: _template.NW = template; break;
                }
                string srcName = _generateSprites[i] != null ? _generateSprites[i].name : $"SE({_generateSprites[0].name})";
                Debug.Log($"[模板] {DirectionNames[i]}: 从 {srcName} 生成了 {template.pixels.Count} 个像素");
            }
            
            // 自动设置贴图尺寸
            if (_generateSprites[0] != null)
            {
                _template.textureSize = new Vector2Int(
                    Mathf.FloorToInt(_generateSprites[0].rect.width),
                    Mathf.FloorToInt(_generateSprites[0].rect.height)
                );
            }
            
            EditorUtility.SetDirty(_template);
            Repaint();
        }
        
        DirectionTemplate GenerateFromSprite(Sprite sprite)
        {
            var template = new DirectionTemplate();
            
            if (sprite == null || sprite.texture == null) return template;
            
            // 确保贴图可读
            var tex = sprite.texture;
            if (!tex.isReadable)
            {
                Debug.LogWarning($"贴图 {tex.name} 不可读，请在 Import Settings 中启用 Read/Write");
                return template;
            }
            
            var rect = sprite.rect;
            var pixels = tex.GetPixels32();
            int texWidth = tex.width;
            
            for (int y = 0; y < (int)rect.height; y++)
            {
                for (int x = 0; x < (int)rect.width; x++)
                {
                    int globalX = (int)rect.x + x;
                    int globalY = (int)rect.y + y;
                    
                    if (globalX < 0 || globalX >= texWidth || globalY < 0 || globalY >= tex.height)
                        continue;
                    
                    var c = pixels[globalY * texWidth + globalX];
                    
                    // 检测有色像素（包括黑色轮廓）
                    if (c.a > 0)
                    {
                        template.pixels.Add(new Vector2Int(x, y));
                    }
                }
            }
            
            return template;
        }
        
        void ClearCurrentDirection()
        {
            if (_template == null) return;
            
            Undo.RecordObject(_template, "Clear Direction");
            
            var template = GetCurrentDirectionTemplate();
            if (template != null)
            {
                template.pixels.Clear();
            }
            
            EditorUtility.SetDirty(_template);
            Repaint();
        }
        
        void CopyTemplateToAll(int sourceDir)
        {
            if (_template == null) return;
            
            var source = _template.GetTemplate(sourceDir);
            if (source == null || source.pixels.Count == 0)
            {
                Debug.LogWarning("源方向没有像素");
                return;
            }
            
            Undo.RecordObject(_template, "Copy to All");
            
            for (int i = 0; i < 4; i++)
            {
                if (i != sourceDir)
                {
                    var copy = new DirectionTemplate();
                    copy.pixels.AddRange(source.pixels);
                    
                    switch (i)
                    {
                        case 0: _template.SE = copy; break;
                        case 1: _template.SW = copy; break;
                        case 2: _template.NE = copy; break;
                        case 3: _template.NW = copy; break;
                    }
                }
            }
            
            EditorUtility.SetDirty(_template);
            Debug.Log($"[模板] 已将 {DirectionNames[sourceDir]} 复制到所有方向");
            Repaint();
        }
        
        void MirrorTemplate(int sourceDir, int targetDir)
        {
            if (_template == null) return;
            
            var source = _template.GetTemplate(sourceDir);
            if (source == null || source.pixels.Count == 0)
            {
                Debug.LogWarning("源方向没有像素");
                return;
            }
            
            Undo.RecordObject(_template, "Mirror Template");
            
            var target = new DirectionTemplate();
            int width = _template.textureSize.x;
            
            foreach (var p in source.pixels)
            {
                // X 轴镜像
                target.pixels.Add(new Vector2Int(width - 1 - p.x, p.y));
            }
            
            switch (targetDir)
            {
                case 0: _template.SE = target; break;
                case 1: _template.SW = target; break;
                case 2: _template.NE = target; break;
                case 3: _template.NW = target; break;
            }
            
            EditorUtility.SetDirty(_template);
            Debug.Log($"[模板] 已将 {DirectionNames[sourceDir]} 镜像到 {DirectionNames[targetDir]}");
            Repaint();
        }
    }
}
