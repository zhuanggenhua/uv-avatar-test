## 1. 共享绘制工具
- [ ] 1.1 新建 `AnimSequenceDrawerUtils.cs` 静态工具类
- [ ] 1.2 实现 `DrawSpriteThumbnail()` - Sprite 缩略图绘制
- [ ] 1.3 实现 `DrawDirectionalGrid()` - 四向序列帧网格绘制
- [ ] 1.4 实现 `HandleGridDragAndDrop()` - 拖拽添加/替换帧

## 2. 重构 EquipmentAnimSequenceEditor
- [ ] 2.1 简化 UI 布局（装备列表 | 动画下拉框 + 网格）
- [ ] 2.2 用动画选择下拉框替代动画列表面板
- [ ] 2.3 调用 `AnimSequenceDrawerUtils` 绘制序列帧网格
- [ ] 2.4 保留自动生成工具部分

## 3. 重构 AnimSequenceEntryDrawer
- [ ] 3.1 移除嵌套 PropertyField 显示
- [ ] 3.2 调用 `AnimSequenceDrawerUtils.DrawDirectionalGrid()` 绘制网格
- [ ] 3.3 调整高度计算方法

## 4. 简化/移除 DirectionalStripDrawer
- [ ] 4.1 评估是否仍需要单独的 Strip Drawer
- [ ] 4.2 如不需要，移除该文件

## 5. 测试验证
- [ ] 5.1 测试独立窗口编辑流程
- [ ] 5.2 测试 Inspector 中的 PropertyDrawer 显示
- [ ] 5.3 测试拖拽添加帧功能
