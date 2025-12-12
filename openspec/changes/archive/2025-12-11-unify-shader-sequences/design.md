# Design: Shader 统一驱动装备序列帧

## 1. 能力边界

本设计限定在 **"如何用 Shader 统一渲染所有装备序列帧"**：
- 不改变现有 `CharacterFrameData` / UVMap 生成流程；
- 不引入新的 Animator 控制方式，仅复用 `AnimationTypeItem` + Animator Bool；
- **完全移除子对象 `SpriteRenderer` 序列帧路径**，所有装备序列帧一律走 Shader。

## 2. 现状建模

### 2.1 现有渲染路径

- **Shader 路径**（核心）
  - `EquipmentRenderer.Refresh()` 在有 FrameData + UVMap 时：
    - 设置 `_BodyUVMap/_HeadUVMap`；
    - 根据 `EquipTypeRegistry` 遍历装备，按 `RenderMode` 设置纹理/颜色/开关；
    - 使用 `_Weapon0/1*` 参数通过 `TrySampleWeapon0/1` 在 Shader 中渲染主手/副手；
    - 通过 `GetLayerPriority` + `srcId` 决定身体/头部/武器/背包的前后关系；
    - 阴影/受击描边/程序描边也都依赖同一帧内 UV 坐标系。

- **子对象序列帧路径**（待移除）
  - 现有代码中 `_equipSequenceRenderers` 可为某些装备创建 `SpriteRenderer` 子对象播放序列帧；
  - 这套逻辑将被完全移除。

### 2.2 问题点

- Shader 与子对象两套序列帧管线并存，深度规则分散：
  - Shader 内的人体/武器/背包有一套 srcId + priority 的精确规则；
  - 子对象序列帧完全依赖 SpriteRenderer.sortingOrder，难以"插层"到 Shader 内部。
- 引入坐骑后，如继续用子对象：
  - 很难满足"坐骑在（某侧）武器与身体之间"这类要求；
  - Draw Call 和 Overdraw 风险显著上升。

## 3. 目标架构

### 3.1 统一的 Shader 序列帧入口

- 对所有 `RenderMode == Sprite` 的装备（Clothing/Pants/Cloak/Helmet/Bag/Mount 等）：
  - 当该装备为当前动画配置了 `AnimSequenceEntry`：
    - 通过 `TryGetSequenceSpriteByKeyWithDepth` 得到当前帧的 `Sprite` 与 `FrameDepthMode`；
    - 将该 Sprite 的纹理与 Rect 填入对应的 Shader 装备通道；
    - 使用 `FrameDepthMode` 与部位信息（Torso/Head）在 Shader 内计算前后关系。

- 对 `RenderMode == Weapon` 的装备（Weapon/Shield 等）：
  - 当装备配置了序列帧：
    - 通过同样的 `TryGetSequenceSpriteByKeyWithDepth` 拿到当前武器序列帧；
    - 将序列帧 Sprite 的纹理与 Rect 折叠进 `_Weapon0/1Tex` + `_Weapon0/1Rect`；
    - 继续使用现有 `_Weapon0/1AnchorFrameUV/_RotCosSin/_FlipX/_DepthMode` 作为定位与深度控制参数；
    - Shader 无需感知"这个武器是静态贴图还是序列帧"，只看当前帧的纹理和 Rect。

- **移除 `_equipSequenceRenderers`**：
  - 不再为任何装备创建序列帧专用的 `SpriteRenderer` 子对象。

### 3.2 Shader 内的统一层级

- 延续现有的 srcId + `GetLayerPriority` 模型：
  - 身体/头部/斗篷/头盔/武器/背包/Mount 均通过 srcId 标记来源；
  - `GetLayerPriority` 决定在描边采样和部分覆盖逻辑中的前后关系。

- 对序列帧装备的处理：
  - 对于身体层装备：在 `ApplyBodyLayers` / `ApplyHeadLayers` 中，序列帧纹理与静态纹理走同一套采样逻辑；
  - 对于武器层：保持 `TrySampleWeapon0/1` 入口不变，底层使用的 `weaponTex/weaponRect` 可来自序列帧或静态贴图；
  - 对于坐骑：
    - 引入新的 SRC_MOUNT 及对应优先级；
    - 在身体与武器合成之间插入坐骑采样逻辑；
    - 参与受击描边与阴影投射的 alpha 采样。

## 4. 行为细节

### 4.1 优先级规则

- **统一优先顺序：**
  1. 角色基础贴图 + 身体/头部 UV 装备（含序列帧衣服/裤子/斗篷/头饰等）；
  2. 坐骑等身体挂载类序列帧装备（如启用）；
  3. 主手/副手武器（含序列帧）；
  4. 背包在南/北向的特殊前后覆盖逻辑（保留现有规则）。

### 4.2 粒度和控制点

- 对于每件装备，仍然使用 `EquipTypeConfig` 描述：
  - RenderMode / BodyPart / Shader 属性名；
  - RenderOrder 继续作为 Body/Head 内部次序参考；
- 序列帧深度：
  - 通过 `FrameDepthMode` 指示"身前/背后"（或更精细的模式），C# 将其映射为 Shader 中的深度控制参数。

### 4.3 对坐骑的前向兼容

- 本设计要求 Shader 序列帧通道：
  - 能够在身体与武器之间插入一层"挂载类"装备；
  - 提供足够的前后细分能力（例如通过 srcId + priority，或额外 depth 枚举），
    以支持后续变更中为坐骑定义更细的左右/前后规则。

## 5. 验证思路

- 编写针对 `equipment-system` 的 Spec delta：
  - 新增"Shader 驱动装备序列帧渲染"的 Requirement 与 Scenario；
  - 明确所有装备序列帧必须走 Shader。
- 在实现阶段（后续 change apply）：
  - 针对以下场景编写或更新测试/示例场景：
    - 衣服/裤子/斗篷序列帧；
    - 武器（主手/副手）使用序列帧 + 锚点旋转；
    - 同一动画下混合静态装备与序列帧装备。
