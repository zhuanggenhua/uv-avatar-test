## 1. 数据结构重构
- [ ] 1.1 修改 `AnimSequenceEntry`：将 `AnimationTypeItem animationType` 改为 `AnimationClip animationClip`
- [ ] 1.2 修改 `AnimationData`：将 `AnimationTypeItem animationType` 改为 `AnimationClip animationClip`
- [ ] 1.3 更新 `GetKey()` 方法：返回 `animationClip.name`

## 2. 运行时同步逻辑
- [ ] 2.1 重构 `EquipmentRenderer.SyncAnimationName()`：使用 `Animator.GetCurrentAnimatorClipInfo()` 获取当前 Clip
- [ ] 2.2 移除 `CacheValidAnimParams()` 和 `_validAnimParams` 相关逻辑
- [ ] 2.3 更新 `FindAnimationByKey()` 适配新的 Clip 名称匹配

## 3. AnimationController 简化
- [ ] 3.1 移除 `AnimationTypeDatabase` 依赖
- [ ] 3.2 简化 `SetAnimation()` 方法，改为直接设置 Animator 参数或触发器
- [ ] 3.3 保留方向控制逻辑不变

## 4. 移除废弃代码
- [ ] 4.1 删除 `AnimationTypeItem.cs`
- [ ] 4.2 删除 `AnimationTypeDatabase.cs`
- [ ] 4.3 删除 `AnimationTypeAutoRegister.cs`
- [ ] 4.4 删除 `AnimationTypeDatabaseEditor.cs`
- [ ] 4.5 删除 `Data/AnimationType/` 目录下所有 .asset 文件

## 5. 编辑器脚本更新
- [ ] 5.1 更新 `AnimSequenceEntryDrawer` 适配 AnimationClip 选择器
- [ ] 5.2 更新 `EquipmentAnimSequenceEditor` 适配新数据结构
- [ ] 5.3 更新 `FrameDataEditor` 移除 AnimationTypeItem 相关 UI
- [ ] 5.4 更新 `EquipmentDataEditor` 适配新的序列帧配置

## 6. 验证与测试
- [ ] 6.1 确保编译通过，无错误
- [ ] 6.2 在编辑器中测试装备序列帧配置
- [ ] 6.3 运行时验证动画同步正确性
- [ ] 6.4 测试多动画状态切换场景
