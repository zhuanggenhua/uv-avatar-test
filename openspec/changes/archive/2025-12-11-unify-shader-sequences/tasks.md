# Tasks: unify-shader-sequences

1. **梳理现有序列帧代码并标记待移除逻辑**  
   - 复查 `EquipmentRenderer` 中：
     - `_equipSequenceRenderers` 字典及其相关方法（`EnsureEquipSequenceRenderer` 等）；
     - `ApplySpriteEquipment` 中使用子对象 `SpriteRenderer` 播放序列帧的分支；
     - `RenderWeapons` / `RenderWeaponSlot` 中使用子对象序列帧的分支。
   - 标记这些逻辑为"待移除"。

2. **定义 Shader 序列帧参数接口（Spec 层）**  
   - 在 `equipment-system` 的 Spec delta 中新增 Requirement：
     - 所有装备序列帧 SHALL 通过 Shader 渲染；
     - 不再使用 `SpriteRenderer` 子对象作为序列帧播放通道。
   - 明确对武器与 Sprite 类装备的统一行为。

3. **扩展 Shader 能力设计（无代码实现）**  
   - 在 `design.md` 进一步细化：
     - 对 Sprite 类装备（衣服/裤子/斗篷/头部/背包/坐骑）的序列帧采样参数形式；
     - 对武器层如何复用 `_Weapon0/1*` 通道来承载序列帧；
     - 序列帧装备在现有 srcId + `GetLayerPriority` 模型中的位置与优先级策略。

4. **起草 Spec Delta 文档**  
   - 在 `openspec/changes/unify-shader-sequences/specs/equipment-system/spec.md` 中：
     - 使用 `## ADDED Requirements` 段落，新增"Shader 驱动装备序列帧渲染"的 Requirement；
     - 包含：
       - "Equipment sequences rendered via Shader" 场景；
       - "Remove SpriteRenderer sequence path" 场景。

5. **运行 OpenSpec 校验**  
   - 运行：`openspec validate unify-shader-sequences --strict`；
   - 修正任何结构或语义错误，直至校验通过。

6. **与实现阶段解耦的交付检查**  
   - 确认本 change 仅包含：
     - proposal.md / design.md / tasks.md；
     - 对 `equipment-system` 的 Spec delta；
   - 不包含任何 C# / Shader / 资源改动；
   - 将此 change 作为后续 `/openspec-apply` 的输入，驱动具体实现与测试工作。
