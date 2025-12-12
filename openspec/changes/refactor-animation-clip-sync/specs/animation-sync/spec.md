## ADDED Requirements

### Requirement: AnimationClip 直接引用
装备序列帧和角色帧数据 SHALL 使用 `AnimationClip` 直接引用作为动画索引 Key，而非 ScriptableObject 中间层。

#### Scenario: 配置装备序列帧
- **WHEN** 配置装备的序列帧动画时
- **THEN** 用户可直接拖拽 AnimationClip 到序列帧条目
- **AND** 系统使用 Clip.name 作为运行时匹配的 Key

#### Scenario: 配置角色帧数据
- **WHEN** 配置角色的 AnimationData 时
- **THEN** 用户可直接拖拽 AnimationClip 到动画条目
- **AND** 系统使用 Clip.name 作为 FrameData 的索引 Key

### Requirement: 实时 Clip 同步
运行时装备渲染器 SHALL 从 Animator 获取当前实际播放的 AnimationClip 名称进行同步，而非依赖 Bool 参数状态。

#### Scenario: 获取当前播放动画
- **WHEN** Animator 播放动画时
- **THEN** 系统通过 `Animator.GetCurrentAnimatorClipInfo(0)` 获取当前 Clip
- **AND** 使用 Clip.name 匹配装备序列帧和帧数据

#### Scenario: 同一状态多动画切换
- **WHEN** Animator 在同一状态（如 Attack）下切换不同 Clip（如 Attack_Slash → Attack_Thrust）
- **THEN** 装备序列帧 SHALL 正确切换到对应 Clip 的序列帧

#### Scenario: 无匹配序列帧回退
- **WHEN** 当前播放的 Clip 没有对应的装备序列帧配置
- **THEN** 系统 SHALL 回退到静态四向贴图模式

### Requirement: 简化动画配置工作流
系统 SHALL 不再需要手动创建和维护 AnimationTypeItem 资产。

#### Scenario: 新增动画支持
- **WHEN** 项目新增一个动画 Clip
- **THEN** 用户只需在装备/帧数据配置中直接引用该 Clip
- **AND** 无需创建额外的 ScriptableObject 资产

## REMOVED Requirements

### Requirement: AnimationTypeItem 资产系统
**Reason**: 使用 AnimationClip 直接引用替代，减少中间层和维护成本
**Migration**: 删除所有 AnimationTypeItem 和 AnimationTypeDatabase 相关代码和资产

### Requirement: Bool 参数状态同步
**Reason**: Bool 参数只能获取状态名，无法区分同一状态下的多个动画
**Migration**: 改用 `Animator.GetCurrentAnimatorClipInfo()` 获取实际 Clip
