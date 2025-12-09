# 装备换装系统文档（当前实现版）

## 概述

这是一个 **配置驱动** 的像素风角色换装系统，核心特点：

- **双层 UV Map 换装**：身体层 / 头部层分离，通过 UV Map 在 Shader 中重定向采样位置。
- **双武器管线**：主手 + 副手两把武器，支持双持、双手武器、手在前/武器在前等深度规则。
- **像素级脚底阴影**：基于 `groundPixelY + 脚部像素` 实时计算阴影模式和形状。
- **头部 4 层叠加 + 眼部系统**：头发 → 面部装饰 → 胡子 → 头盔，并带有眼睛颜色与眼部装饰（黑眼圈/刀疤）。

### 核心特性

- **配置驱动渲染**：`EquipTypeConfig + EquipTypeRegistry` 定义每种装备的渲染模式（Sprite/Color/Weapon）、绑定到哪个 `CharacterBodyPart`、使用哪些 Shader 属性以及渲染顺序。新增装备类型无需改 Renderer 逻辑。
- **武器槽位系统**：`WeaponSlotType` 控制主手/双手/双持/副手规则，自动阻止非法组合（例如双手武器禁止副手）。
- **四向朝向 + 转头支持**：Spritesheet 行 0~3 分别表示 SE/SW/NE/NW，`BodyPartRegion.spriteFacing` 允许头部独立于身体转向。
- **编辑器工作流完整闭环**：从帧涂色、自动检测手脚/眼睛、生成 UV Map，到运行时 `EquipmentRenderer` 一次性串起来。

### 当前版本要点总览

- 使用 `CharacterFrameData` 描述每一帧的：
  - `FrameData.bodyRegions`：UV 区域（Head/Torso 等）和 `spriteFacing / variant`；
  - `FrameData.limbMask`：手脚 + 眼睛像素蒙版（Left/Right Hand/Foot/Eye）；
  - `FrameData.anchors`：武器等挂点。
- `AnimationData.bodyUVMap / headUVMap`：通过 `DualUVMapGenerator` 离线生成双层 UV Map 纹理，运行时只在 Shader 中采样。
- `EquipmentRenderer` 在运行时负责：
  - 根据当前 `SpriteRenderer.sprite` 计算行列索引 (`_rowIndex / _frameIndex`) 和动画名；
  - 从 `CharacterFrameData` 中取出 `_cachedFrame` 和 `_currentAnimData`；
  - 更新阴影、UVMap、身体/头部深度模式；
  - 渲染武器（主手 + 副手）；
  - 应用角色外观（头发/胡子/眼睛/眼部装饰）；
  - 按 `EquipTypeRegistry` 遍历所有装备类型，写入 Shader 纹理/颜色/开关。

## 系统架构

```
EquipmentSystem/
├── Data/                        # 数据定义
│   ├── CharacterFrameData.cs        # 帧数据（锚点、部位区域、UV方向等）
│   ├── CharacterAppearance.cs       # 角色外观（头发、胡子、眼睛颜色）
│   ├── EquipmentRenderData.cs       # 装备渲染数据（4方向贴图、武器槽位、内嵌序列帧）
│   ├── EquipTypeConfig.cs           # 装备类型配置（渲染模式、Shader属性）
│   └── EquipmentAnimSequenceData.cs # 装备序列帧数据结构（DirectionalStrip/AnimSequenceEntry）
├── Editor/                      # 编辑器工具
│   ├── FrameDataEditor.cs           # 帧数据编辑器窗口
│   ├── DualUVMapGenerator.cs        # UV Map 生成器
│   ├── EquipmentDataEditor.cs       # 装备数据编辑器（基础字段 + 打开动画序列编辑器按钮）
│   └── EquipmentAnimSequenceEditor.cs # 装备动画序列编辑器窗口
├── Runtime/                     # 运行时组件
│   ├── EquipmentRenderer.cs         # 装备渲染器（配置驱动）
│   ├── AnimationController.cs       # 动画控制器
│   └── EquipmentDemoExtension.cs    # 测试工具（主手/副手UI）
└── Shaders/                     # Shader
    └── EquipmentUV.shader           # 装备渲染 Shader（双武器 + 像素阴影 + 眼部装饰）
```

---

## 核心概念

### 1. 角色方向 (CharacterFacing)

角色有 4 个朝向，对应 Spritesheet 的 4 行：

| 行索引 | 方向 | 枚举值 |
|--------|------|--------|
| 0 | 东南 (SE) | `CharacterFacing.SouthEast` |
| 1 | 西南 (SW) | `CharacterFacing.SouthWest` |
| 2 | 东北 (NE) | `CharacterFacing.NorthEast` |
| 3 | 西北 (NW) | `CharacterFacing.NorthWest` |

### 2. UV 方向 (UVOrientation)

控制 UV 坐标的旋转变换，用于处理不同朝向的贴图映射：

| 枚举值 | 说明 | 变换 |
|--------|------|------|
| `UpRight` | 默认 | 不变换 |
| `DownLeft` | 旋转180° | u=1-u, v=1-v |
| `UpLeft` | 逆时针90° | u=v, v=u |
| `DownRight` | 顺时针90° | u=1-v, v=1-u |

### 3. 贴图方向 (spriteFacing)

指定部位使用哪个方向的装备贴图，用于：
- **转头场景**: 身体朝 SE，但头转向 SW
- **特殊动作**: 某些帧需要显示不同方向的装备

### 4. 区域扩展姿态 (RegionExpandPose)

控制 **编辑器中区域扩展/收缩时，角色头相对于屏幕的方向**，影响几何扩展轴与 UV 采样方向。

```csharp
public enum RegionExpandPose
{
    HeadUp   = 0, // 头在上（默认站立）
    HeadLeft = 1, // 头在左（向左躺）
    HeadRight= 2, // 头在右（向右躺）
    HeadDown = 3  // 头在下（倒立）
}
```

- 在 `FrameDataEditor` 中通过「扩展姿态」下拉框设置。
- `headExpandUp/Down/Side` 与 `bodyExpandUp/Down/Side` 始终按**身体坐标系**理解：
  - Up = 朝头方向扩展
  - Down = 朝脚方向扩展
  - Side = 左右对称扩展
- `FrameDataAlgorithms.MapExpandByPose` 会根据 `RegionExpandPose` 把这些“身体方向”的扩展量旋转到 **屏幕坐标的 up/down/left/right**，无论角色是站立、躺左/躺右还是倒立，配置含义都保持一致。
- `ExpandRegionWithBoundaryUV` 在做几何扩展的同时，会按同一姿态旋转 UV 采样方向，保证扩出来的像素颜色梯度与角色实际朝向一致。
- `ShrinkRegionByPoseAndDetectSize` 使用同一套姿态映射 + `headDetectSize/torsoDetectSize`，使「扩展一次 → 立即收缩」能够在正常数据下精确还原到原始检测区域。

---

## 数据结构

### CharacterFrameData (ScriptableObject)

存储角色的帧数据配置：

```csharp
CharacterFrameData
├── hasReferenceFrame: bool          // 是否启用参考帧
├── referenceHeadCenter: Vector2     // 参考帧头部中心
└── animations: List<AnimationData>  // 动画列表
    └── AnimationData
        ├── animationName: string        // 动画名称
        ├── spritesheet: Texture2D       // 原始 Spritesheet
        ├── frameSize: Vector2Int        // 单帧尺寸
        ├── frameCount: int              // 帧数
        ├── bodyUVMap: Texture2D         // 身体层 UV Map
        ├── headUVMap: Texture2D         // 头部层 UV Map
        └── frames: List<FrameData>      // 帧数据列表
            └── FrameData
                ├── anchors: List<AnchorPoint>       // 锚点
                ├── bodyRegions: List<BodyPartRegion> // 部位区域（含 spriteFacing / variant 等）
                └── limbMask: LimbMask               // 手脚蒙版（Left/RightHand/Foot/Eye），用于颜色替换与脚部高度计算
```

### EquipmentData (ScriptableObject)

存储装备数据：

```csharp
EquipmentRenderData
├── equipmentId: string
├── type: EquipmentType              // Weapon/Clothing/Cloak/Helmet/Gloves/Shoes/Pants
├── spriteSE/SW/NE/NW: Sprite        // 4方向贴图
├── weaponSlotType: WeaponSlotType   // 武器槽位类型（仅武器）
├── leftColor/rightColor: Color32    // 手套/鞋子颜色
└── animSequences: List<AnimSequenceEntry> // 内嵌的序列帧动画列表（可选）
```

### WeaponSlotType（武器槽位类型）

```csharp
public enum WeaponSlotType
{
    MainHand,    // 主手单手武器，可搭配副手
    TwoHand,     // 双手武器，禁止副手
    DualWield,   // 双持武器，一件装备两个锚点显示，禁止副手
    OffHand,     // 副手武器（盾牌等），只能装在副手槽
}
```

**装备规则**:
- `MainHand/TwoHand/DualWield` → 装备到主手槽，使用 `LeftWeapon` 锚点
- `OffHand` → 装备到副手槽，使用 `RightWeapon` 锚点
- `TwoHand/DualWield` 装备后自动禁止副手装备
- `DualWield` 在静态模式下同一贴图在两个锚点各显示一次

### EquipTypeConfig（装备类型配置）

```csharp
public class EquipTypeConfig
{
    public EquipmentType Type;
    public string DisplayName;
    public EquipRenderMode RenderMode;  // None/Sprite/Color/Weapon
    public CharacterBodyPart BodyPart;  // Sprite 模式用
    public string TexProp, RectProp, EnableProp;  // Shader 属性名
    public string LeftColorProp, RightColorProp;  // Color 模式用
    public int RenderOrder;             // 渲染顺序
}
```

**渲染模式**:
- `Sprite`: UV 贴图映射（服装、裤子、斗篷、头盔、背包 Bag）
- `Color`: 颜色替换（手套、鞋子）
- `Weapon`: 武器专用渲染（锚点 + Shader 深度处理）
- `None`: 不渲染

当前内置的 `Sprite` 类装备类型中，`Bag` 的配置为：

- `Type = EquipmentType.Bag`
- `RenderMode = Sprite`
- `BodyPart = CharacterBodyPart.Torso`（使用身体 UV）
- Shader 属性：`_BagTex / _BagRect / _EnableBag`
- `RenderOrder = 3`，与衣服/裤子/斗篷同属躯干链路

Bag 具有额外的方向相关深度逻辑：

- **朝南 (SouthEast/SouthWest)**:
  - 在身体层中作为 **最底层** 采样（先于裤子/衣服/斗篷）；
  - 仅在主贴图 `_MainTex` 该像素 **透明** 时绘制包体，这样身体像素始终挡在前面；
  - 实际效果：包被身体/衣服/斗篷遮挡，只在角色轮廓后侧或下方露出一部分。
- **朝北 (NorthEast/NorthWest)**:
  - Shader 在角色+武器全部合成完成后，再次以躯干 UV 采样背包；
  - 将采样结果覆盖到最终颜色上，使背包处于 **包括武器在内的最前层**。

此外，Bag 也参与受击描边：

- Shader 内为背包定义了单独的来源 ID：`SRC_BAG`；
- 南向身体层和北向最终覆盖时，命中的背包像素会被标记为 `SRC_BAG`；
- 受击描边分支中，会在 `srcId == SRC_BAG` 时，
  通过 `_BagTex/_BagRect` + UVMap 检测该像素是否为背包自身的黑色轮廓边缘，
  若是，则将其替换为 `_HitOutlineColor`，与斗篷/头盔/武器等其他图层的描边行为保持一致。

### BodyPartRegion

部位区域数据：

```csharp
BodyPartRegion
├── part: CharacterBodyPart  // Head/Torso/LeftHand/RightHand/LeftFoot/RightFoot
├── orientation: UVOrientation   // UV 旋转方向
├── spriteFacing: CharacterFacing // 贴图方向（选择哪张贴图）
├── variant: FrameVariant          // 帧变体（Base/Up/Down/Left/Right 等），用于同一方向下的姿态区分
└── pixels: List<BodyPartPixel>   // 像素列表
```

---

## UV Map 系统

### 双层 UV Map

系统使用两张 UV Map 分离身体和头部的渲染：

| UV Map | 用途 | 包含部位 |
|--------|------|----------|
| `bodyUVMap` | 身体层 | Torso, LeftHand, RightHand, LeftFoot, RightFoot |
| `headUVMap` | 头部层 | Head |

### UV Map 格式与含义

每个像素存储 RGBA 四个通道：
- **R**: U 坐标 (0~1)
- **G**: V 坐标 (0~1)
- **B**: 部位 ID
- **A**: 1.0

> 当前实现中，编辑器 `FillPartWithUV` 会基于 **UV 调色板整张贴图** 来计算 U/V：
>
> - 假设调色板尺寸为 `palW x palH`，某个像素在调色板中的格子坐标为 `(uvX, uvY)`
> - 实际写入的 UV 为：
>   - `u = (uvX + 0.5f) / palW`
>   - `v = 1f - (uvY + 0.5f) / palH`
>
> 因此，R/G 表示的是 **在调色板整图上的绝对 UV 坐标**（已经归一化到 0~1），而不是某个小 Sprite 内部的局部坐标。

### 部位 ID 定义

| 部位 | ID 值 | 用途 |
|------|-------|------|
| None | 0.0 | 非换装区域 |
| Head | 0.1 | 头部装饰 |
| Torso | 0.2 | 服装 |
| LeftHand | 0.4 | 左手套 |
| RightHand | 0.5 | 右手套 |
| LeftFoot | 0.6 | 左鞋 |
| RightFoot | 0.7 | 右鞋 |

### UV Map 与装备贴图的关系（重要）

当前渲染管线的假设是：

- `bodyUVMap` / `headUVMap` 使用的 **UV 调色板纹理**，在 UV 空间中与实际的装备纹理（`_ClothTex`, `_HelmetTex`, `_HairTex`, `_BeardTex` 等）**共享同一张大图或严格对齐的网格布局**。
- 运行时：
  - `DualUVMapGenerator` 将编辑器中保存的 `BodyPartPixel.uv` 写入 UV Map 纹理的 R/G 通道；
  - `EquipmentRenderer` 为每件装备获取对应 `Sprite`，并将该 Sprite 在整张纹理中的 Rect 通过 `_ClothRect` / `_HelmetRect` 等属性传入 Shader；
  - Shader 中使用 `TransformUV(bodyUV.rg, _ClothRect)` 或 `TransformUV(headUV.rg, _HelmetRect)`，将 **UV 调色板中的坐标** 映射到对应装备 Sprite 的实际区域。

这意味着：

- 如果装备贴图与 UV 调色板来自同一张大图（Spritesheet / Atlas），并且各装备在该大图中的网格布局与调色板严格一致，那么当前实现是匹配的；
- 如果某个装备（例如头盔）使用了 **完全独立的一张纹理**，则需要保证这张纹理在 UV 上与调色板具有相同的网格布局，否则同一组 UV 会采样到错误的位置或透明区域。

> 简单理解：UV Map 记录的是“在一张统一的 **装备设计图/调色板** 上应该取哪里的颜色”，而 Shader 通过 `_XXXRect` 把这一套坐标映射到当前具体装备 Sprite 所在的区域。

### 头部参考帧系统

解决头部移动时贴图抖动的问题：

1. **设置参考帧**: 选择一帧作为参考，记录头部中心位置
2. **UV 计算**: 所有帧的头部 UV 都相对于参考帧中心计算
3. **效果**: 头部移动时，贴图保持稳定，不会抖动

---

## 工作流程

### 编辑器工作流

1. **创建 CharacterFrameData**
   ```
   Assets > Create > Equipment System > Character Frame Data
   ```

2. **打开帧数据编辑器**
   ```
   Window > Equipment System > Frame Data Editor
   ```

3. **配置动画**
   - 设置 Spritesheet
   - 设置帧尺寸和帧数

4. **涂色部位区域**
   - 选择部位（Head/Torso/LeftHand 等）
   - 在预览图上涂色标记该部位的像素
   - 或使用"自动检测"功能

5. **设置锚点**（武器用）
   - 切换到锚点模式
   - 在预览图上点击设置锚点位置

6. **设置 UV 方向和贴图方向**
   - UV 方向: 控制 UV 坐标的旋转
   - 贴图方向: 选择使用哪个方向的装备贴图

7. **生成 UV Map**
   - 点击"生成当前动画 UV Map"
   - UV Map 会保存到动画数据中

### 运行时工作流

1. **挂载 EquipmentRenderer**
   ```csharp
   [RequireComponent(typeof(SpriteRenderer))]
   public class EquipmentRenderer : MonoBehaviour
   ```

2. **设置数据引用**
   - `frameData`: CharacterFrameData 资产
   - `appearance`: CharacterAppearance（头发/胡子）
   - `equipments`: 装备列表

3. **装备/卸载装备**
   ```csharp
   equipmentRenderer.Equip(equipmentData);
   equipmentRenderer.Unequip(equipmentData);
   ```

4. **刷新显示**
   ```csharp
   equipmentRenderer.Refresh();
   ```

### 阴影（当前实现）

- 阴影逻辑已经 **集成到 `EquipmentUV.shader` 单 Pass 中**，不再依赖单独的 Shadow 子对象。
- 在 `CharacterFrameData` 中配置全局地面基准：
  - `groundPixelY`：脚站在地面时，帧内 y 像素坐标（与 `BodyPartPixel.position.y` 同坐标系）。
- 运行时由 `EquipmentRenderer.UpdateShadowHeight()` 完成：
  - 从 `_cachedFrame.limbMask` 获取 `LeftFoot/RightFoot` 像素列表；
  - 计算当前帧脚部最低像素相对 `groundPixelY` 的高度差 `heightDiff`；
  - 根据高度差自动选择阴影模式：
    - **Mode 0**：脚在地面或以下 → 贴合脚底的基础阴影；
    - **Mode 1**：离地 1~2 像素 → 宽度为左右脚范围的矩形阴影；
    - **Mode 2**：离地 3~9 像素 → 以下方那只脚为中心的 4×3 缺角矩形阴影；
    - **Mode 3**：离地 ≥10 像素 → 帧中心十字形阴影；
  - 将 `_ShadowMode/_ShadowLeftX/_ShadowRightX/_ShadowCenterX/_ShadowBaseY/_FrameSize` 写入 Shader。
- Shader 端在像素级根据 `_ShadowMode` 和 `_FrameSize`，在帧内 UV 空间绘制精确阴影形状，确保阴影紧贴角色轮廓且始终贴地。

---

## 眼睛与眼部装饰系统

### 1. 眼睛蒙版、闭眼与颜色

- **眼睛像素与闭眼状态由帧编辑器自动检测**：
  - `FrameDataEditorTools.DetectEyes` 仅在 **头部检测区域的中间一行** 扫描像素；
  - **第一步：查找黑色眼睛像素**（由 `DetectConfig` 的相关阈值决定，通常是画眼睛用的黑色）：
    - 若这一行上能找到黑色像素簇，则按水平方向分成左右两只眼；
    - 屏幕左边的簇视为角色 **右眼**，屏幕右边的簇视为角色 **左眼**；
    - 此时会将 `FrameData.leftEyeClosed / rightEyeClosed` 置为 `false`（睁眼）。
  - **第二步：查找闭眼颜色**：
    - 只有在这一行 **完全找不到任何黑色眼睛像素** 时，才会使用 `DetectConfig.closedEyeColor` 和 `closedEyeColorThreshold` 在同一行上查找闭眼颜色；
    - 同样按左右位置分成左右眼，并将对应的 `leftEyeClosed / rightEyeClosed` 置为 `true`（闭眼）；
    - 目前不做“一只睁眼、一只闭眼”的特殊自动识别，如果需要这种效果，建议手动编辑遮挡或贴图。
- **眼睛像素与闭眼状态的存储**：
  - `FrameData.limbMask.leftEye / rightEye` 保存每帧左右眼的像素位置；
  - `FrameData.leftEyeClosed / rightEyeClosed` 记录当前帧左右眼是否处于闭眼状态；
  - 从 SE 行生成 SW/NE/NW 行时，会同时镜像眼睛位置并同步闭眼标记，保证 SE/SW 都满足“**右眼在屏幕左边**”的约定。
- **UV 与 Shader 行为**：
  - `DualUVMapGenerator` 在生成 `bodyUVMap` 时，将眼睛像素写入 Body UVMap 的 B 通道：
    - `ID_LEFTEYE = 0.3`，`ID_RIGHTEYE = 0.35`；
  - Shader 中：
    - `ComputePartIDs` 根据 B 通道判断当前像素是否为左眼/右眼区域；
    - `ApplyBodyLayers` 在眼睛像素处，根据 `_EnableLeftEye/_EnableRightEye` 决定是否用 `_LeftEyeColor/_RightEyeColor` 替换颜色；
  - 运行时 `EquipmentRenderer.ApplyAppearanceToShader()`：
    - 始终将 `appearance.leftEyeColor/rightEyeColor` 写入 `_LeftEyeColor/_RightEyeColor`；
    - 仅在正面（`FacingDirection.Front`，即 SE/SW）并且对应眼睛 **未被标记为闭眼** 时，才启用 `_EnableLeftEye/_EnableRightEye`；
    - 背面 NE/NW 或被标记为闭眼的眼睛都不会渲染眼睛颜色，但其像素位置仍会被用于眼部装饰（黑眼圈、刀疤等）。

### 2. 眼部装饰（黑眼圈 & 刀疤）

- 新增枚举 `EyeDecorationType`：
  - `None`：无装饰；
  - `DarkCircle`：黑眼圈；
  - `Scar`：刀疤；
- 在 `CharacterAppearance` 中配置：
  - `eyeDecorationType`：装饰类型；
  - `eyeDecorationColor`：装饰颜色；
- 运行时 `EquipmentRenderer.ApplyEyeDecoration()`：
  - 仅在正面（SE/SW）且 `eyeDecorationType != None` 时生效；
  - 从 `_cachedFrame.limbMask` 读取 `LeftEye/RightEye` 像素，计算帧内中心 UV；
  - 若当前帧无眼睛数据，则使用上一帧缓存位置，按方向切换时清空缓存；
  - 将 `_EyeDecoMode/_EyeDecoColor/_LeftEyePos/_RightEyePos` 传给 Shader。

Shader `EquipmentUV.shader` 中：

- `ApplyEyeDecoration(frameUV, headUVLocal, parts, inout color)` 完成眼部装饰绘制：
  - 仅在头部区域 (`parts.isHead`) 且未被头盔覆盖时生效；
  - **Mode 1（黑眼圈）**：
    - 在两只眼睛下方一格（`offset = (0,-1)`）涂上 `_EyeDecoColor`；
  - **Mode 2（刀疤）**：
    - 根据 `_BodyInEast` 判断朝向：
      - 朝东（SE/NE）：使用 `_RightEyePos`；
      - 朝西（SW/NW）：使用 `_LeftEyePos`；
    - 在目标眼睛上方一格和下方一格（`offset = (0,1)` 与 `(0,-1)`）绘制刀疤色；
- 同时提供 `IsWeaponBlackOutlineNearEyes`，用于避免武器黑描边在眼睛附近时盖住眼部细节。

## Shader 说明

### EquipmentUV.shader

主要功能：
1. 采样双层 UV Map 获取部位信息和 UV 坐标；
2. 根据部位 ID 选择对应装备贴图或颜色（衣服/裤子/斗篷/手套/鞋子/眼睛）；
3. 支持身体层 + 头部层 + 双武器层的深度叠加；
4. 集成像素级脚底阴影绘制；
5. 支持眼睛颜色与眼部装饰（黑眼圈/刀疤）。

#### 渲染层级

**身体层** (`ApplyBodyLayers`):
- 裤子 (`_PantsTex`) → 服装 (`_ClothTex`) → 斗篷 (`_CloakTex`)
- 手套/鞋子：根据部位 ID 直接用 `_LeftHandColor/_RightHandColor/_LeftFootColor/_RightFootColor` 替换颜色；
- 眼睛：在眼睛区域用 `_LeftEyeColor/_RightEyeColor` 替换颜色。

**头部层** (`ApplyHeadLayers`):
- 头发 → 面部装饰 → 胡子 → 头盔（顶层）；
- 若当前像素同时属于前手且头部区域，则跳过头部层，保留手部颜色，避免头发/头盔遮挡手。

**武器层**:
- 朝北：武器在角色后面（有角色像素时由角色遮挡）；
- 朝南：武器在角色前面，但脚始终在所有武器前面，手是否挡住武器由每把武器的 `HandInFront` 决定；
- 主手与副手使用 `_Weapon0* / _Weapon1*` 两套独立参数，主手在视觉上优先生效。

#### 武器参数（每把武器独立）

```hlsl
// 主手武器 (Weapon0)
_Weapon0Tex, _Weapon0Rect, _Weapon0AnchorFrameUV
_Weapon0RotCosSin, _Weapon0FlipX, _Weapon0DepthMode, _Weapon0Enabled

// 副手武器 (Weapon1)
_Weapon1Tex, _Weapon1Rect, _Weapon1AnchorFrameUV
_Weapon1RotCosSin, _Weapon1FlipX, _Weapon1DepthMode, _Weapon1Enabled

// 共用
_CharFrameRect  // 当前角色帧在 _MainTex 中的 Rect
```

#### 关键函数

```hlsl
// 将局部 UV (0~1) 转换为贴图实际 UV
float2 TransformUV(float2 uv, float4 rect)

// 通用武器采样
bool TrySampleWeaponGeneric(float2 mainUV, sampler2D weaponTex, ...)

// 主手/副手武器采样
bool TrySampleWeapon0(float2 mainUV, out fixed4 outColor)
bool TrySampleWeapon1(float2 mainUV, out fixed4 outColor)

// 身体层装备
void ApplyBodyLayers(fixed4 bodyUV, bool isHeadCore, inout fixed4 ioColor, out float bodyLayerAlpha)

// 头部层装备
void ApplyHeadLayers(float2 baseHeadUV, float headPartID, inout fixed4 ioColor, out float headLayerAlpha)
```

### 调试模式

在 Material Inspector 中设置 `_DebugMode`:

| 模式 | 说明 |
|------|------|
| 0 | 正常渲染 |
| 1 | 显示身体层区域（彩色） |
| 2 | 显示头部层区域（青色） |
| 3 | 显示采样结果 |
| 4 | 显示头部 UV Map 原始值 |
| 5 | 显示顶点原始 UV |
| 6 | 只显示头盔采样 |
| 7 | 显示头盔 UV（经过 Rect 映射后） |
| 8 | 显示头盔贴图 |
| 9 | 显示 _HelmetRect 数值 |

---

## 扩展新装备类型与动画

### 扩展新装备类型

- 在 `EquipTypeConfig` / `EquipTypeRegistry` 中新增一条配置：指定 `EquipmentType`、`RenderMode`、`BodyPart`、Shader 属性名、渲染顺序等。
- 若是 Weapon 模式：根据是否需要特殊手部遮挡规则配置 `HandInFrontForWeapon`，并确保 Shader 中有对应 `_XXXTex/_XXXRect/_XXXEnable` 属性。
- 若是 Sprite/Color 模式：保证 UV Map 中该 BodyPart 已正确标注，且 Shader 中有对应采样或颜色属性。

### 为角色增加新动画

- 创建新的 `AnimationTypeItem` 资产，名称与 Animator Bool 参数一致。
- 运行 `AnimationTypeAutoRegister`，将新动画类型加入 `AnimationTypeDatabase`。
- 在 `CharacterFrameData` 中为该动画类型添加 `AnimationData`，配置 Spritesheet、帧数、UV Map 和锚点等。

### 为装备增加动画序列

- 打开 `EquipmentAnimSequenceEditor` 窗口，在左侧列表中选择目标 `EquipmentRenderData`。
- 在右侧“自动生成工具”中选择动画类型和 Spritesheet，点击“生成/覆盖动画”，会向 `animSequences` 中添加或更新 `AnimSequenceEntry`。
- 如需手动微调，可直接在右侧列表或 `EquipmentRenderData` 的 Inspector 中编辑 `animSequences` 里的 `DirectionalStrip` 与逐帧前后层。

---

## 常见问题

### Q: 装备贴图显示不正确？

1. 检查 UV Map 是否已生成
2. 检查部位是否正确涂色
3. 使用调试模式查看 UV 值
4. 确认装备贴图的 4 个方向都已设置

### Q: 头盔抖动/闪烁？

1. 启用参考帧功能
2. 在 Idle 第一帧设置参考帧
3. 重新生成 UV Map

### Q: 贴图方向不对？

1. 检查 `spriteFacing` 设置
2. 确认装备数据中 4 个方向的贴图都已设置
3. `spriteFacing` 决定选择哪张贴图，`orientation` 决定 UV 旋转

### Q: 武器位置不对？

1. 检查锚点是否正确设置
2. 检查装备的 `selfAnchor` 是否正确
3. 确认 `anchorType` 匹配（LeftWeapon/RightWeapon）

---

## API 参考

### EquipmentRenderer

```csharp
// 装备（自动根据 weaponSlotType 分配到主手/副手槽）
public void Equip(EquipmentData equip, bool autoRefresh = true)

// 卸载
public void Unequip(EquipmentData equip, bool autoRefresh = true)

// 卸载所有装备
public void UnequipAll()

// 刷新显示
public void Refresh()

// 获取主手武器
public EquipmentData GetMainHandWeapon()

// 获取副手武器
public EquipmentData GetOffHandWeapon()

// 检查当前是否允许装备副手
public bool CanEquipOffHand()

// 设置角色外观
public void SetAppearance(CharacterAppearance newAppearance)
```

### EquipTypeRegistry

```csharp
// 获取装备类型配置
public static EquipTypeConfig Get(EquipmentType type)

// 获取显示名称
public static string GetDisplayName(EquipmentType type)

// 所有配置（用于遍历）
public static IEnumerable<EquipTypeConfig> All
```

### CharacterFrameData

```csharp
// 获取帧数据（按 AnimationTypeItem）
public FrameData GetFrameData(AnimationTypeItem animType, int rowIndex, int frame);

// 获取帧数据（按 Key，用于与 Animator Bool 匹配）
public FrameData GetFrameDataByKey(string key, int rowIndex, int frame);

// 获取动画（按 AnimationTypeItem）
public AnimationData GetAnimation(AnimationTypeItem animType);

// 获取动画（按 Key）
public AnimationData GetAnimationByKey(string key);

// 获取所有动画类型
public List<AnimationTypeItem> GetAnimationTypes();
```

### EquipmentRenderData

```csharp
// 根据方向获取基础 4 向贴图（带 NE/NW 回退）
public Sprite GetSprite(CharacterFacing facing);

// 根据方向和帧变体获取贴图（非武器支持 Up/Down/Left/Right 变体）
public Sprite GetSprite(CharacterFacing facing, FrameVariant variant);

// 尝试获取序列帧 Sprite（按动画类型）
public Sprite TryGetSequenceSprite(AnimationTypeItem animType, int rowIndex, int frameIndex);

// 尝试获取序列帧 Sprite（按 Key，与 Animator 参数同名）
public Sprite TryGetSequenceSpriteByKey(string key, int rowIndex, int frameIndex);

// 尝试获取序列帧 Sprite，并返回该帧的深度模式（用于武器身前/背后层）
public Sprite TryGetSequenceSpriteByKeyWithDepth(
    string key,
    int rowIndex,
    int frameIndex,
    out FrameDepthMode depthMode);
```

 ## 版本历史

- **v1.0**: 初始版本，基础换装功能
- **v1.1**: 添加双层 UV Map，分离头部和身体渲染
- **v1.2**: 添加参考帧系统
- **v1.3**: 添加 spriteFacing 支持，用于转头场景
- **v1.4**: 添加从 SE 方向自动生成其他方向数据的功能
- **v1.5**: 添加斗篷(Cloak)装备类型
- **v1.6**: 添加面部装饰(FaceAccessory)外观层
- **v2.0**: 配置驱动渲染系统重构
  - 引入 `EquipTypeConfig` + `EquipTypeRegistry`，新增装备类型无需修改 Renderer 代码
  - 移除 `hideLeftWeapon`/`hideRightWeapon` 配置，武器渲染由序列帧优先原则控制
- **v2.1**: 武器槽位系统
  - 引入 `WeaponSlotType` 枚举（MainHand/TwoHand/DualWield/OffHand）
  - 主手 + 副手双武器支持，自动处理装备规则
  - Shader 支持双武器独立渲染（`_Weapon0*` + `_Weapon1*`）
  - 双持武器在两个锚点同时显示
  - 测试 UI 拆分为主手/副手两个下拉框

- **v2.2**: 区域扩展姿态与对称收缩
  - 新增 `RegionExpandPose`（HeadUp/HeadLeft/HeadRight/HeadDown）统一描述角色头相对屏幕的方向
  - 区域扩展逻辑通过 `MapExpandByPose` 将「向头/向脚/左右」的身体坐标扩展量旋转到屏幕坐标
  - `ExpandRegionWithBoundaryUV` 依据姿态旋转 UV 采样方向，保持扩展颜色梯度与姿态一致
  - `ShrinkRegionByPoseAndDetectSize` 利用 `headDetectSize/torsoDetectSize` 与姿态信息，将扩展区域精确收缩回检测区域，实现「扩一次 → 收一次」近似可逆

