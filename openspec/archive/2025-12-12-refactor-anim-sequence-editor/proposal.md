# Change: 重构装备动画序列编辑器 - 采用 SpriteLibrary 风格显示

## Why
当前 `EquipmentAnimSequenceEditor` 使用嵌套的 PropertyField 显示动画序列数据，存在以下问题：
1. **层层嵌套**：需要展开 3-4 级才能看到具体的 Sprite 帧
2. **无缩略图预览**：frames 只显示对象引用名称，看不到实际图片
3. **信息密度低**：大量空间被折叠头和缩进占用
4. **难以对比四向**：无法同时看到 SE/SW/NE/NW 四个方向的序列帧
5. **渲染逻辑分散**：`AnimSequenceEntryDrawer`、`EquipmentDataEditor`、`EquipmentAnimSequenceEditor` 各自独立实现，无法复用

## What Changes
- **抽取共享渲染工具**：新建 `AnimSequenceDrawerUtils` 静态类，统一序列帧网格绘制逻辑
- **重构为四向网格视图**：用四行网格同时显示 SE/SW/NE/NW 四个方向的序列帧缩略图
- **简化 UI 布局**：用动画选择下拉框替代独立的动画列表面板
- **统一三处编辑器**：
  - `EquipmentAnimSequenceEditor` - 主窗口，调用共享绘制方法
  - `AnimSequenceEntryDrawer` - PropertyDrawer，调用共享绘制方法
  - `EquipmentDataEditor.DrawAnimSetField()` - Inspector，调用共享绘制方法
- **支持拖拽添加帧**：拖拽 Sprite 到网格中添加/替换帧

## Impact
- Affected specs: equipment-system
- Affected code:
  - 新建 `AnimSequenceDrawerUtils.cs` - 共享的序列帧网格绘制工具
  - `EquipmentAnimSequenceEditor.cs` - 主编辑器窗口重构
  - `AnimSequenceEntryDrawer.cs` - 重构为调用共享方法
  - `DirectionalStripDrawer.cs` - 可移除或简化
  - `EquipmentDataEditor.cs` - 可选：在 Inspector 中也显示缩略图网格
