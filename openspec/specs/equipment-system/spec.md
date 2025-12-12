# Capability: Equipment System

## Purpose
MiniCharacterCreator 的装备系统负责：
- 管理角色可装备的各类装备（服装、裤子、斗篷、头盔、帽子、面罩、手套、鞋子、武器、盾牌、背包等），并通过集中配置表 `EquipTypeRegistry` 描述渲染方式。
- 基于 `CharacterFrameData` 和 `CharacterAppearance`，将装备与角色基础外观组合成最终 Sprite / Shader 效果。
- 在运行时根据动画帧数据和脚部像素位置，计算像素级阴影形状和模式，并通过 Shader 精确绘制。
## Requirements
### Requirement: Equip and unequip items by equipment type
系统 SHALL 通过 `EquipmentRenderer` 提供按装备类型的穿戴 / 卸下行为，并保证各类型的互斥与武器槽位规则符合预期。

#### Scenario: Equip non-weapon item replaces same-type slot
- **WHEN** 调用 `EquipmentRenderer.Equip(equip)`，且 `equip` 的类型为非武器类型（例如 `Clothing`, `Pants`, `Helmet`, `Shoes` 等）
- **AND** `EquipTypeRegistry` 中已为该 `EquipmentType` 注册了配置（`RenderMode != Weapon`）
- **THEN** 系统 SHALL 将该类型槽位中原有的装备（如果存在）替换为新的 `equip`
- **AND** 后续调用 `GetEquipped(type)` SHALL 返回新装备实例
- **AND** 在随后的 `Refresh()` 调用中，该装备 SHALL 被渲染到对应身体部位（Body/Head 层）

#### Scenario: Equipping main-hand weapon enforces off-hand rules
- **WHEN** `equip.weaponSlotType` 为 `MainHand`, `TwoHand` 或 `DualWield`
- **AND** 调用 `EquipmentRenderer.Equip(equip)`
- **THEN** 任何已装备在主手槽位上的旧武器 SHALL 被卸下
- **AND** 如果 `weaponSlotType` 为 `TwoHand` 或 `DualWield`，当前已装备的副手武器（如果有） SHALL 被自动卸下
- **AND** `_mainHandWeapon` SHALL 指向新武器，`GetMainHandWeapon()` SHALL 返回该实例
- **AND** 随后的 `CanEquipOffHand()` SHALL 返回 `false`

#### Scenario: Equipping off-hand weapon respects main-hand restrictions
- **WHEN** 当前主手为空，或主手武器的 `weaponSlotType` 为 `MainHand`
- **AND** 调用 `EquipmentRenderer.Equip(equip)`，其中 `equip.weaponSlotType == OffHand`
- **THEN** 旧的副手武器（如果存在） SHALL 被卸下
- **AND** `_offHandWeapon` SHALL 指向新武器，`GetOffHandWeapon()` SHALL 返回该实例

- **WHEN** 当前主手武器的 `weaponSlotType` 为 `TwoHand` 或 `DualWield`
- **AND** 调用 `EquipmentRenderer.Equip(equip)`，其中 `equip.weaponSlotType == OffHand`
- **THEN** 系统 SHALL 拒绝装备该副手武器（例如写入一条 `Debug.LogWarning`）
- **AND** `_offHandWeapon` SHALL 保持不变

#### Scenario: Unequip all clears equipment slots
- **WHEN** 调用 `EquipmentRenderer.UnequipAll()`
- **THEN** 所有通过 `Equip` 装备上的装备 SHALL 被卸下，包括：
  - 所有非武器类型槽位中的装备
  - 主手武器 `_mainHandWeapon`
  - 副手武器 `_offHandWeapon`
- **AND** 紧接着的 `Refresh()` SHALL 被调用以更新渲染状态
- **AND** 后续调用 `GetEquipped(type)` / `GetMainHandWeapon()` / `GetOffHandWeapon()` SHALL 返回 `null`

### Requirement: Apply appearance and equipment rendering to shader
系统 SHALL 在每一帧通过 `EquipmentRenderer.Refresh()` 组合角色基础外观、装备配置与动画帧数据，将结果应用到 `SpriteRenderer` 和绑定的换装 Shader（例如 `EquipmentSystem/EquipmentUV`）。

#### Scenario: Frame has valid animation data and UV maps
- **WHEN** 当前 `frameData` 非空，且能通过动画 Key、行索引、帧索引获取到 `_currentAnimData` 与 `_cachedFrame`
- **AND** `_currentAnimData.bodyUVMap` 与 `_currentAnimData.headUVMap` 均非空
- **THEN** 系统 SHALL：
  - 将 `bodyUVMap` 绑定到 `_BodyUVMap`，`headUVMap` 绑定到 `_HeadUVMap`
  - 调用 `ResetEquipmentState()` 将所有装备相关 Shader 开关清零
  - 根据 `_cachedFrame` 设置身体/头部前后关系（`_BodyInFront`, `_BodyInEast`）
  - 为主手/副手武器调用 `RenderWeapons()`，计算锚点、深度模式和排序
  - 遍历 `EquipTypeRegistry.All`，根据 `RenderMode` 为每个已装备类型应用：
    - Sprite 模式：设置对应纹理与 UV Rect，并打开启用开关
    - Color 模式：设置左右手/脚颜色，并打开启用开关
  - 根据 `CharacterAppearance` 设置头发、胡子、面部装饰、肤色映射与眼睛相关参数

#### Scenario: Frame is missing animation data or UV maps
- **WHEN** 当前动画 Key 在 `CharacterFrameData` 中不存在，或 `_cachedFrame` 为 `null`
- **OR** `_currentAnimData.bodyUVMap` 或 `_currentAnimData.headUVMap` 为空
- **THEN** 系统 SHALL：
  - 调用 `ResetEquipmentState()` 清理所有装备与外观相关 Shader 状态
  - 将 `_BodyUVMap` 与 `_HeadUVMap` 纹理设为 `null`
  - 仍然允许通过 `RenderWeapons()` 使用独立 `SpriteRenderer` 渲染武器序列帧（如果装备数据存在并配置了序列帧）
  - 对于配置为 Sprite 且具有序列帧的装备，使用 `ApplySpriteEquipment` 以 `SpriteRenderer` 方式渲染（不依赖 UVMap）

#### Scenario: Head slot priority for helmet, hat, and mask
- **WHEN** 多个头部装备类型（`Helmet`, `Hat`, `Mask`）同时被装备
- **THEN** `EquipmentRenderer.GetHeadSlotEquipment()` SHALL 按以下优先级返回头部装备：
  - 如当前佩戴 `Helmet`，返回 `Helmet`
  - 否则如当前佩戴 `Hat`，返回 `Hat`
  - 否则如当前佩戴 `Mask`，返回 `Mask`
- **AND** 相应的 Shader 通道（例如 `_HelmetTex`, `_MaskTex`） SHALL 仅根据此有效头部装备进行渲染

### Requirement: Pixel-accurate shadow modes based on foot height
系统 SHALL 基于当前帧脚部像素相对于 `CharacterFrameData.groundPixelY` 的高度，计算像素级阴影模式和关键坐标，并通过 Shader 精确绘制不同形态的阴影。

#### Scenario: Mode 0 - grounded shadow on baseline
- **WHEN** 当前帧中任意脚部最底部像素的 y 坐标 `minY` 大于等于 `groundPixelY`（即脚在地面基线或更低）
- **THEN** 计算得到 `heightDiff = groundPixelY - minY <= 0`
- **AND** 系统 SHALL 将 `_ShadowMode` 设为 `0`
- **AND** Shader SHALL 在地面基线处扫描脚部下方的有色像素，并沿基线扩展形成一条连续阴影带（左右各扩展 1 像素，向下扩展 1 像素）

#### Scenario: Mode 1 - low hover shadow spanning both feet
- **WHEN** `heightDiff` 在 `1` 到 `2` 像素之间（脚离开地面 1–2 像素）
- **THEN** 系统 SHALL 将 `_ShadowMode` 设为 `1`
- **AND** 按如下方式计算阴影横向范围：
  - 取左右脚像素集合的最小 x 与最大 x（忽略缺失的脚）
  - 计算 `leftX = overallMinX / frameSizeX`，`rightX = overallMaxX / frameSizeX`
- **AND** Shader SHALL 在基线上绘制一条覆盖 `leftX` 到 `rightX` 的阴影，并在上下左右各扩展 1 像素，以表现轻微离地的柔和阴影

#### Scenario: Mode 2 - mid-air shadow anchored to lower foot side
- **WHEN** `heightDiff` 在 `3` 到 `9` 像素之间（脚高于地面 3–9 像素）
- **THEN** 系统 SHALL 将 `_ShadowMode` 设为 `2`
- **AND** 比较左右脚最低像素高度，确定下方的那只脚：
  - 如左脚更低且存在左脚像素，则以左脚的 `xMax` 作为阴影中心
  - 否则如右脚存在，则以右脚的 `xMin` 作为阴影中心
- **AND** 计算 `centerX = selectedX / frameSizeX` 并写入 `_ShadowCenterX`
- **AND** Shader SHALL 在地面基线附近绘制一个宽 4×高 3 像素、缺四角的矩形阴影，中心对齐 `centerX`，表现中等高度的空中阴影

#### Scenario: Mode 3 - fully airborne cross-shaped shadow at frame center
- **WHEN** `heightDiff` 大于等于 `10` 像素（脚部远离地面，高度 ≥ 10 像素）
- **THEN** 系统 SHALL 将 `_ShadowMode` 设为 `3`
- **AND** 计算并写入 `_ShadowCenterX = 0.5`（帧中心）
- **AND** Shader SHALL 以帧中心为基准绘制一个十字形阴影：
  - 横向、纵向各从中心向两侧扩展 1 像素
  - 阴影整体紧贴地面基线所在的像素行

#### Scenario: No shadow when feet data is missing
- **WHEN** 当前 `_cachedFrame` 中既无左脚像素集合，也无右脚像素集合，或 `frameData` 为空
- **THEN** 系统 SHALL 将 `_ShadowMode` 设为 `-1`
- **AND** Shader SHALL 不绘制任意阴影像素

#### Scenario: Shadow base Y aligns with groundPixelY
- **WHEN** 正常计算任意模式阴影时
- **THEN** 系统 SHALL 计算 `_ShadowBaseY` 的归一化值：
  - 使用当前帧像素高度 `frameSizeY`
  - 令 `shadowBaseY01 = 1 - (groundPixelY + 0.5) / frameSizeY`
- **AND** 写入 `_ShadowBaseY` Shader 属性，使阴影基线与 `groundPixelY` 像素行的几何中心对齐
- **AND** 系统 SHALL 同步写入 `_FrameSize`（宽高像素数），供 Shader 在像素网格内进行精确采样

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

