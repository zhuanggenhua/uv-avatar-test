## 1. 共享绘制工具
- [x] 1.1 新建 `AnimSequenceDrawerUtils.cs` 静态工具类
- [x] 1.2 实现 `DrawSpriteThumbnail()` - Sprite 缩略图绘制
- [x] 1.3 实现 `DrawDirectionalGrid()` - 四向序列帧网格绘制
- [x] 1.4 实现 `HandleGridDragAndDrop()` - 拖拽添加/替换帧

## 2. 重构 EquipmentAnimSequenceEditor
- [x] 2.1 简化 UI 布局（装备列表 | 动画下拉框 + 网格）
- [x] 2.2 用动画选择下拉框替代动画列表面板
- [x] 2.3 调用 `AnimSequenceDrawerUtils` 绘制序列帧网格
- [x] 2.4 保留自动生成工具部分

## 3. 重构 AnimSequenceEntryDrawer
- [x] 3.1 移除嵌套 PropertyField 显示
- [x] 3.2 调用 `AnimSequenceDrawerUtils.DrawDirectionalGrid()` 绘制网格
- [x] 3.3 调整高度计算方法

## 4. 简化/移除 DirectionalStripDrawer
- [x] 4.1 评估是否仍需要单独的 Strip Drawer
- [x] 4.2 简化为单行显示（保留供调试用）

## 5. 测试验证
- [x] 5.1 测试独立窗口编辑流程
- [x] 5.2 测试 Inspector 中的 PropertyDrawer 显示
- [x] 5.3 测试拖拽添加帧功能
