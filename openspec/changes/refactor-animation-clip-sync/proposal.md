# Change: 重构动画同步机制 - 使用 AnimationClip 替代 AnimationTypeItem

## Why
当前框架通过 `AnimationTypeItem` (ScriptableObject) 作为动画类型的 Key，存在以下问题：
1. **状态与动画混用**：通过 Animator Bool 参数获取的是"状态名"而非"实际播放的动画"
2. **无法支持一状态多动画**：同一个 Attack 状态下的多种攻击动画（Slash/Thrust/Spin）无法区分
3. **维护成本高**：每新增一个动画都需要手动创建 AnimationTypeItem Asset 并注册到数据库
4. **字符串匹配易错**：AnimationTypeItem.name 与 Animator 参数名需手动保持一致

## What Changes
- **移除 AnimationTypeItem 依赖**：改用 `AnimationClip` 直接引用作为动画索引 Key
- **改进运行时同步**：从 Animator 获取当前实际播放的 AnimationClip，而非 Bool 参数状态
- **简化数据结构**：`AnimSequenceEntry` 和 `AnimationData` 改用 AnimationClip 字段
- **移除冗余资产**：删除 AnimationTypeItem、AnimationTypeDatabase 相关代码和资产

## Impact
- Affected specs: animation-sync
- Affected code:
  - `AnimSequenceEntry` (EquipmentAnimSequenceData.cs)
  - `AnimationData` (CharacterFrameData.cs)
  - `EquipmentRenderer.SyncAnimationName()` (EquipmentRenderer.cs)
  - `AnimationController` (AnimationController.cs)
  - 移除 `AnimationTypeItem.cs`
  - 移除 `AnimationTypeDatabase.cs`
  - 移除 `AnimationTypeAutoRegister.cs`
  - 移除 `AnimationTypeDatabaseEditor.cs`
  - 相关编辑器脚本更新
