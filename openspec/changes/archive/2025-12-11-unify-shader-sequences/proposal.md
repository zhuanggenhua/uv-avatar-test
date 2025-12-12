# Proposal: 统一装备序列帧到 Shader 渲染管线

## 背景
- 当前装备系统的渲染管线核心是 `EquipmentUV.shader`，在单 Pass 内合成角色本体、衣服/裤子/斗篷/头部、双武器、背包、阴影、描边、肤色映射等。
- 新增"坐骑装备"需求希望：
  - 坐骑动画与角色、武器共用统一的序列帧系统；
  - 坐骑需要与武器/身体深度紧密耦合（如：南向右手武器 > 坐骑 > 身体；北向左手武器 > 坐骑 > 身体）；
  - 所有装备序列帧统一在 Shader 内渲染，避免复杂的子对象排序和 Draw Call 膨胀。

## 问题
- 现有代码中部分装备序列帧仍然可能通过 `SpriteRenderer` 子对象播放，这导致：
  - 复杂前后关系在子对象层级上难以精确表达；
  - 多个子对象序列帧会增加 Draw Call、排序管理和 Overdraw，违背单 Shader 合成的设计初衷。

## 目标
- 定义一套 **完全基于 Shader 的序列帧渲染能力**，适用于：
  - 衣服 / 裤子 / 斗篷 / 头部装备 / 背包 / 坐骑等 `Sprite` 模式装备；
  - 武器 / 盾牌等 `Weapon` 模式装备；
- 在 `CharacterFrameData` + `EquipmentRenderData.animSequences` + `EquipmentUV.shader` 之间建立清晰的契约：
  - 所有配置为 `Sprite/Weapon` 且拥有序列帧的装备 **一律走 Shader 序列帧渲染**；
  - **不再使用 `SpriteRenderer` 子对象作为序列帧播放通道**。
- 移除现有代码中为装备序列帧创建/管理子对象 `SpriteRenderer` 的逻辑（如 `_equipSequenceRenderers`）。

## 非目标
- 本次变更 **不直接规定具体的坐骑前后遮挡规则**（如哪只脚/哪只武器挡住坐骑的哪些像素），
  仅为"坐骑及其他装备的序列帧能够在 Shader 中受精确深度控制"建立能力与规范。
- 不在本次变更中引入新的动画系统或 Animator 状态机规则，仅复用现有 `AnimationTypeItem/AnimationTypeDatabase` 与当前 `EquipmentRenderer` 的动画同步机制。

## 高层方案概述
- **C# 侧统一序列帧入口**：
  - 在 `EquipmentRenderer.Refresh()` 中，为所有 `Sprite/Weapon` 渲染模式的装备统一：
    - 读取当前动画 Key / 行索引 / 帧索引；
    - 通过 `EquipmentRenderData.TryGetSequenceSpriteByKeyWithDepth(...)` 拿到当前帧的序列帧 Sprite 和深度模式；
    - 将该序列帧的纹理与 Rect 映射为 Shader 通道参数。
  - 对 `Weapon`：
    - 将序列帧结果折叠为 `_Weapon0/1Tex + _Weapon0/1Rect` 的 per-frame 数据，保持现有锚点/旋转/FlipX 流程不变。
  - 对 `Sprite` 类装备（如 Clothing/Cloak/Helmet/Bag/Mount 等）：
    - 序列帧纹理与 Rect 直接写入对应的 Shader 装备通道（`_ClothTex/_ClothRect` 等）。
  - **移除 `_equipSequenceRenderers` 相关逻辑**，不再为装备序列帧创建子对象 `SpriteRenderer`.

- **Shader 侧统一采样与深度控制**：
  - 在 `EquipmentUV.shader` 中引入/复用一套"序列帧层"参数：
    - 对身体层装备：服装/裤子/斗篷/背包/坐骑等在 Body/Head UV 空间下采样；
    - 对武器层： `_Weapon0/1*` 在帧内 UV 空间下采样；
    - 对未来坐骑：扩展类似 `Bag` 的深度规则，使其可插入 Body 与 Weapon 之间，并参与受击描边与阴影遮挡.
  - 所有序列帧装备的层级关系统一由 Shader 的 `srcId + GetLayerPriority` 决定.

## 预期影响范围
- **Spec 层**：
  - 为 `equipment-system` 新增"Shader 驱动装备序列帧渲染"的需求，
    明确所有装备序列帧必须走 Shader，不再使用 SpriteRenderer 子对象.
- **Runtime C#**：
  - 重构 `EquipmentRenderer` 内对 `animSequences` 的处理逻辑：
    - 移除 `_equipSequenceRenderers` 及相关的子对象创建/管理代码；
    - 在 `ApplySpriteEquipment` / `RenderWeapons` 中，将序列帧结果直接转化为 Shader 参数.
- **Shader**：
  - 在 `EquipmentUV.shader` 中确保所有装备通道（包括武器）能够接受 per-frame 的序列帧纹理与 Rect.

## 风险与缓解
- **风险：** Shader 复杂度增加，调试难度上升.
  - **缓解：**
    - 保持改动集中在单一 Shader 文件，并尽量沿用现有 Bag/Weapon 的层级与采样模式；
    - 使用 `_DebugMode` 扩展调试视图，单独观察某一装备层的采样结果.
