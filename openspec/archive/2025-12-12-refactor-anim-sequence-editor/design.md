# Design: 装备动画序列编辑器重构

## 1. 目标 UI 布局

```
┌──────────────────────────────────────────────────────────────────────────┐
│ 装备列表     │ 动画列表      │  序列帧网格                               │
│ (220px)     │ (150px)       │  (剩余宽度)                               │
├─────────────┼───────────────┼───────────────────────────────────────────┤
│ ▾ Weapon    │ ▸ Idle   [4]  │  当前: Walk                    [▶][⏹]   │
│   ├ Sword   │ ▾ Walk   [4]  │ ┌─────┬─────┬─────┬─────┬─────┬─────┐    │
│   └ Axe ←   │ ▸ Attack [4]  │ │ SE  │ 🖼️1 │ 🖼️2 │ 🖼️3 │ 🖼️4 │     │    │
│ ▾ Shield    │ ▸ Die    [2]  │ ├─────┼─────┼─────┼─────┼─────┼─────┤    │
│   └ Round   │               │ │ SW  │ 🖼️1 │ 🖼️2 │ 🖼️3 │ 🖼️4 │     │    │
│             │ [+ 添加动画]  │ ├─────┼─────┼─────┼─────┼─────┼─────┤    │
│             │               │ │ NE  │ 🖼️1 │ 🖼️2 │ 🖼️3 │ 🖼️4 │     │    │
│             │               │ ├─────┼─────┼─────┼─────┼─────┼─────┤    │
│ [刷新]      │               │ │ NW  │ 🖼️1 │ 🖼️2 │ 🖼️3 │ 🖼️4 │     │    │
│             │               │ └─────┴─────┴─────┴─────┴─────┴─────┘    │
│             │               │                                          │
│             │               │ [自动生成工具...]                         │
└─────────────┴───────────────┴──────────────────────────────────────────┘
```

## 2. 核心组件

### 2.1 EquipmentAnimSequenceEditor (重构)

**主要变更**：
- `DrawMainPanel()` 拆分为 `DrawAnimationList()` 和 `DrawSequenceGrid()`
- 移除对 `PropertyField(_animSequencesProp)` 的依赖
- 新增 `_selectedAnimIndex` 追踪当前选中的动画

**关键字段**：
```csharp
// 动画选择状态
int _selectedAnimIndex = -1;
AnimSequenceEntry _selectedAnim => GetSelectedAnimation();

// 预览播放状态
bool _isPlaying;
int _previewFrameIndex;
double _lastFrameTime;

// 缩略图配置
const float THUMB_SIZE = 48f;
const float ROW_HEIGHT = 56f;
```

### 2.2 Sprite 缩略图绘制

```csharp
void DrawSpriteThumbnail(Rect rect, Sprite sprite, FrameDepthMode depth, bool isSelected)
{
    // 1. 背景
    EditorGUI.DrawRect(rect, isSelected ? new Color(0.3f, 0.5f, 0.8f) : new Color(0.2f, 0.2f, 0.2f));
    
    // 2. Sprite 预览
    if (sprite != null && sprite.texture != null)
    {
        var uvRect = GetSpriteUVRect(sprite);
        var innerRect = new Rect(rect.x + 2, rect.y + 2, rect.width - 4, rect.height - 4);
        GUI.DrawTextureWithTexCoords(innerRect, sprite.texture, uvRect);
    }
    
    // 3. 深度模式标识
    if (depth == FrameDepthMode.Back)
    {
        var iconRect = new Rect(rect.xMax - 14, rect.yMax - 14, 12, 12);
        GUI.Label(iconRect, "◀", EditorStyles.miniLabel);
    }
}

Rect GetSpriteUVRect(Sprite sprite)
{
    var tex = sprite.texture;
    var rect = sprite.textureRect;
    return new Rect(
        rect.x / tex.width,
        rect.y / tex.height,
        rect.width / tex.width,
        rect.height / tex.height
    );
}
```

### 2.3 四向网格绘制

```csharp
void DrawDirectionalGrid(AnimSequenceEntry entry)
{
    var facings = new[] {
        CharacterFacing.SouthEast,
        CharacterFacing.SouthWest,
        CharacterFacing.NorthEast,
        CharacterFacing.NorthWest
    };
    
    var facingNames = new[] { "SE", "SW", "NE", "NW" };
    
    for (int row = 0; row < 4; row++)
    {
        EditorGUILayout.BeginHorizontal();
        
        // 方向标签
        EditorGUILayout.LabelField(facingNames[row], GUILayout.Width(30));
        
        // 获取该方向的 strip
        var strip = entry?.GetStrip(facings[row]);
        
        if (strip != null && strip.frames != null)
        {
            for (int i = 0; i < strip.frames.Count; i++)
            {
                var thumbRect = GUILayoutUtility.GetRect(THUMB_SIZE, THUMB_SIZE);
                var depth = i < strip.depthModes.Count ? strip.depthModes[i] : FrameDepthMode.Front;
                bool isSelected = (row == _selectedRow && i == _selectedFrame);
                
                DrawSpriteThumbnail(thumbRect, strip.frames[i], depth, isSelected);
                
                // 点击选中
                if (Event.current.type == EventType.MouseDown && thumbRect.Contains(Event.current.mousePosition))
                {
                    _selectedRow = row;
                    _selectedFrame = i;
                    Event.current.Use();
                }
            }
        }
        
        // 添加帧按钮
        if (GUILayout.Button("+", GUILayout.Width(24), GUILayout.Height(THUMB_SIZE)))
        {
            AddFrameToStrip(entry, facings[row]);
        }
        
        EditorGUILayout.EndHorizontal();
    }
}
```

### 2.4 拖拽处理

```csharp
void HandleDragAndDrop(Rect dropArea, AnimSequenceEntry entry, CharacterFacing facing, int insertIndex)
{
    var evt = Event.current;
    
    switch (evt.type)
    {
        case EventType.DragUpdated:
        case EventType.DragPerform:
            if (!dropArea.Contains(evt.mousePosition))
                return;
            
            // 检查是否有 Sprite
            var sprites = DragAndDrop.objectReferences
                .OfType<Sprite>()
                .ToList();
            
            if (sprites.Count > 0)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                
                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    InsertSpritesToStrip(entry, facing, insertIndex, sprites);
                }
            }
            
            evt.Use();
            break;
            
        case EventType.DragExited:
            break;
    }
}
```

### 2.5 预览播放

```csharp
void UpdatePreview()
{
    if (!_isPlaying || _selectedAnim == null)
        return;
    
    double currentTime = EditorApplication.timeSinceStartup;
    double frameDuration = 1.0 / 8.0; // 8 FPS
    
    if (currentTime - _lastFrameTime >= frameDuration)
    {
        _lastFrameTime = currentTime;
        _previewFrameIndex++;
        
        // 循环播放
        var strip = _selectedAnim.GetStrip(CharacterFacing.SouthEast);
        if (strip != null && strip.frames.Count > 0)
        {
            _previewFrameIndex %= strip.frames.Count;
        }
        
        Repaint();
    }
}

void OnEnable()
{
    EditorApplication.update += UpdatePreview;
}

void OnDisable()
{
    EditorApplication.update -= UpdatePreview;
}
```

## 3. 数据修改策略

使用 `SerializedObject` + `SerializedProperty` 保证 Undo 支持：

```csharp
void AddFrameToStrip(AnimSequenceEntry entry, CharacterFacing facing)
{
    _serializedEquipment.Update();
    
    // 找到对应的 strip property
    int animIndex = GetAnimationIndex(entry);
    var stripsProp = _animSequencesProp
        .GetArrayElementAtIndex(animIndex)
        .FindPropertyRelative("strips");
    
    // 找到或创建对应方向的 strip
    int stripIndex = FindStripIndex(stripsProp, facing);
    if (stripIndex < 0)
    {
        stripIndex = CreateStrip(stripsProp, facing);
    }
    
    // 添加空帧
    var framesProp = stripsProp
        .GetArrayElementAtIndex(stripIndex)
        .FindPropertyRelative("frames");
    framesProp.arraySize++;
    
    _serializedEquipment.ApplyModifiedProperties();
}
```

## 4. 不变的部分

- `AnimSequenceEntry` 数据结构不变
- `DirectionalStrip` 数据结构不变
- `EquipmentAnimSequenceTools` 工具方法不变
- 自动生成工具的逻辑不变（只移动到新位置）

## 5. 可选移除

如果重构完成后不再需要 Inspector 中的编辑能力，可以简化：
- `AnimSequenceEntryDrawer` - 改为只读显示或移除
- `DirectionalStripDrawer` - 改为只读显示或移除
