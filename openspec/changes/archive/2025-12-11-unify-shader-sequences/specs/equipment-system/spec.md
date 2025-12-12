# Spec Delta: Equipment System — Shader-Driven Equipment Sequences

## ADDED Requirements

### Requirement: Shader-driven equipment animation sequences

系统 SHALL 通过 Shader 管线统一渲染所有装备序列帧，不再使用 `SpriteRenderer` 子对象作为序列帧播放通道。

#### Scenario: Equipment sequences rendered via Shader
- **GIVEN** 至少一件已装备的装备（包括 Sprite 模式与 Weapon 模式）在其 `EquipmentRenderData.animSequences` 中为当前动画 Key 配置了 `AnimSequenceEntry`，
- **THEN** 系统 SHALL：
  - 对所有 `RenderMode == Sprite` 且具备有效序列帧的装备：
    - 使用当前动画 Key、行索引、帧索引，通过 `EquipmentRenderData.TryGetSequenceSpriteByKeyWithDepth(...)` 获取对应方向与帧的序列帧 Sprite 及其深度模式；
    - 将该序列帧 Sprite 的底层纹理与 UV Rect 映射为 Shader 装备通道的输入参数（例如 `_ClothTex/_ClothRect`、`_CloakTex/_CloakRect`、`_HelmetTex/_HelmetRect` 或未来扩展的挂载类通道），并根据深度模式与身体部位信息在 Shader 内部决定前后顺序；
  - 对所有 `RenderMode == Weapon` 且具备有效序列帧的装备：
    - 使用同一动画 Key、行索引、帧索引，通过 `TryGetSequenceSpriteByKeyWithDepth(...)` 获取当前武器方向与帧的序列帧 Sprite 及其深度模式；
    - 将该序列帧 Sprite 的底层纹理与 UV Rect 折叠为 `_Weapon0Tex/_Weapon0Rect` 或 `_Weapon1Tex/_Weapon1Rect` 等 Shader 参数（具体槽位取决于装备在主手/副手槽位中的归属）；
    - 保持现有基于锚点、旋转与 FlipX 的 `_Weapon0/1AnchorFrameUV/_Weapon0/1RotCosSin/_Weapon0/1FlipX/_Weapon0/1DepthMode` 流程不变，仅更换武器采样所使用的贴图与 Rect；
- **AND** 系统 SHALL 不为任何装备创建或启用序列帧专用的 `SpriteRenderer` 子对象。

#### Scenario: Remove SpriteRenderer sequence path
- **GIVEN** 系统实现本 Requirement，
- **THEN** 系统 SHALL：
  - 移除 `EquipmentRenderer` 中的 `_equipSequenceRenderers` 字典及其相关逻辑（如 `EnsureEquipSequenceRenderer` 方法）；
  - 移除 `ApplySpriteEquipment` 中为装备序列帧创建/更新 `SpriteRenderer` 子对象的分支；
  - 移除 `RenderWeapons` / `RenderWeaponSlot` 中使用子对象 `SpriteRenderer` 播放武器序列帧的分支；
- **AND** 所有装备序列帧的渲染 SHALL 完全通过 Shader 参数传递与 `EquipmentUV.shader` 内部采样完成。
