## ADDED Requirements

### Requirement: 装备动画序列编辑器 - 四向序列帧网格
编辑器 SHALL 以四向序列帧网格（SE/SW/NE/NW 四行）展示和编辑装备动画序列。

#### Scenario: 四向序列帧网格显示
- **WHEN** 用户选择一个动画类型
- **THEN** 显示四行网格（SE/SW/NE/NW）
- **AND** 每行显示该方向的所有序列帧缩略图

#### Scenario: Sprite 缩略图显示
- **WHEN** 序列帧网格中存在 Sprite 帧
- **THEN** 每帧显示为缩略图
- **AND** 显示该帧的深度模式（前/后）

### Requirement: 装备动画序列编辑器 - 拖拽支持
编辑器 SHALL 支持通过拖拽操作添加和管理序列帧。

#### Scenario: 拖拽添加单个 Sprite
- **WHEN** 用户将一个 Sprite 拖拽到序列帧网格的某一行
- **THEN** 该 Sprite 被添加到对应方向的帧列表末尾

#### Scenario: 拖拽添加多个 Sprite
- **WHEN** 用户将多个 Sprite（来自同一 Spritesheet）拖拽到序列帧网格
- **THEN** 所有 Sprite 按顺序添加到对应方向的帧列表

#### Scenario: 拖拽替换帧
- **WHEN** 用户将一个 Sprite 拖拽到已存在的帧缩略图上
- **THEN** 该帧被替换为新的 Sprite

## MODIFIED Requirements

### Requirement: 装备动画序列数据管理
编辑器 SHALL 提供管理装备动画序列数据的界面，采用统一的序列帧网格渲染方法，代替嵌套的 PropertyField 显示。

#### Scenario: 添加新动画序列
- **WHEN** 用户点击添加动画按钮并选择动画类型
- **THEN** 为当前装备创建一个新的空动画序列条目

#### Scenario: 删除动画序列
- **WHEN** 用户点击删除动画按钮
- **THEN** 弹出确认对话框
- **AND** 确认后删除选中的动画序列

#### Scenario: 编辑帧深度模式
- **WHEN** 用户点击某个帧的深度模式控件
- **THEN** 在前/后之间切换该帧的深度模式
