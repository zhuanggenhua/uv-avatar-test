# 装备换装系统文档

## 概述

这是一个 **配置驱动** 的像素风格角色换装系统，基于 UV Map + Shader 实现，支持：

- **武器**: 主手 + 副手双武器系统，锚点定位 + Shader 深度处理
- **服装/裤子/斗篷**: Shader UV 重映射到躯干，按层级叠加
- **头部装备**: 头发 → 面部装饰 → 胡子 → 头盔（四层叠加）
- **手套/鞋子**: 颜色替换

### 核心特性

- **配置驱动渲染**: 通过 `EquipTypeConfig` + `EquipTypeRegistry` 管理装备类型，新增类型无需修改 Renderer 代码
- **武器槽位系统**: `WeaponSlotType` 定义武器类型（主手/双手/双持/副手），自动处理装备规则
- **双武器 Shader**: 支持主手 + 副手同时渲染，双持武器在两个锚点显示

#### 当前版本要点总览
- 使用 `CharacterFrameData + DualUVMap` 描述每一帧的锚点、部位区域、UV 与检测配置。
- 使用 `EquipmentData + EquipTypeConfig/EquipTypeRegistry` 配置每一种装备的渲染模式（Sprite/Color/Weapon）、Shader 属性和头部遮挡规则（如隐藏头发/胡子、手在前还是武器在前等）。
- 运行时由 `EquipmentRenderer`：
  - 根据 Animator Bool 参数 + `AnimationTypeDatabase` 推导当前动画 Key（如 Idle/Walk/Attack）。
  - 通过角色 `Sprite.rect` 计算 `_rowIndex`（朝向行：SE/SW/NE/NW）和 `_frameIndex`（列内帧号）。
  - 从 `CharacterFrameData` 取出当前帧的 `FrameData`（锚点、部位区域、手脚蒙版等）。
  - 先渲染武器，再按配置遍历所有装备类型，将贴图 / 颜色写入 Shader 参数。
- 序列帧优先：若 `EquipmentData.animSet` 存在，优先通过 `EquipAnimSetAsset` 使用该装备自己的序列帧；若某方向/动画缺失，则自动回退到该装备的 4 向基础贴图。
- 武器方向与镜像（详见下文专节）：
  - 武器方向始终跟随躯干 `BodyPartRegion.spriteFacing`，而不是简单使用 `_rowIndex`；
  - NE/NW 缺失时统一回退：NE → SE，NW → SW → SE（静态贴图与序列帧行为一致）；
  - 只有在「西向行(SW/NW) 且没有 SW 基础贴图」时，才会从 SE 通过 `flipX + 角度取反` 镜像生成西向武器。

## 系统架构

```
EquipmentSystem/
├── Data/                        # 数据定义
│   ├── CharacterFrameData.cs        # 帧数据（锚点、部位区域、UV方向等）
│   ├── CharacterAppearance.cs       # 角色外观（头发、胡子、眼睛颜色）
│   ├── EquipmentData.cs             # 装备数据（4方向贴图、武器槽位类型）
│   ├── EquipTypeConfig.cs           # 装备类型配置（渲染模式、Shader属性）
│   └── EquipAnimSequenceAsset.cs    # 装备序列帧动画资产
├── Editor/                      # 编辑器工具
│   ├── FrameDataEditor.cs           # 帧数据编辑器窗口
│   ├── DualUVMapGenerator.cs        # UV Map 生成器
│   ├── EquipmentDataEditor.cs       # 装备数据编辑器
│   └── EquipAnimSequenceEditor.cs   # 序列帧动画编辑器
├── Runtime/                     # 运行时组件
│   ├── EquipmentRenderer.cs         # 装备渲染器（配置驱动）
│   ├── AnimationController.cs       # 动画控制器
│   └── EquipmentDemoExtension.cs    # 测试工具（主手/副手UI）
└── Shaders/                     # Shader
    └── EquipmentUV.shader           # 装备渲染 Shader（双武器支持）
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
EquipmentData
├── equipmentId: string
├── type: EquipmentType           // Weapon/Clothing/Cloak/Helmet/Gloves/Shoes/Pants
├── spriteSE/SW/NE/NW: Sprite     // 4方向贴图
├── weaponSlotType: WeaponSlotType // 武器槽位类型（仅武器）
├── leftColor/rightColor: Color32  // 手套/鞋子颜色
└── animSet: EquipAnimSetAsset    // 序列帧动画集（可选）
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
- `Sprite`: UV 贴图映射（服装、裤子、斗篷、头盔）
- `Color`: 颜色替换（手套、鞋子）
- `Weapon`: 武器专用渲染（锚点 + Shader 深度处理）
- `None`: 不渲染

### BodyPartRegion

部位区域数据：

```csharp
BodyPartRegion
├── part: CharacterBodyPart  // Head/Torso/LeftHand/RightHand/LeftFoot/RightFoot
├── orientation: UVOrientation   // UV 旋转方向
├── spriteFacing: CharacterFacing // 贴图方向（选择哪张贴图）
├── variant: FrameVariant          // 帧变体（Base/Up/Down 等），用于同一方向下的姿态区分
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

### 阴影（当前实现状态）

- 当前 Demo 中阴影是一个单独的 `Shadow` 子对象，由 `AnimationController.SetShadowEnabled(bool)` 控制启/停。
- `EquipmentDemoExtension` 的「显示阴影」开关也是调用上述 API，仅切换 Shadow 对象的激活状态。
- 主体 Shader `EquipmentUV.shader` 目前只负责角色与装备的合成渲染，尚未集成多 Pass 阴影；后续阴影 Pass 设计见《装备系统多 Pass 阴影与武器渲染设计文档》。

---

## Shader 说明

### EquipmentUV.shader

主要功能：
1. 采样 UV Map 获取部位信息和 UV 坐标
2. 根据部位 ID 选择对应的装备贴图采样
3. 支持多层叠加（身体层 + 头部层 + 武器层）
4. **双武器支持**: 主手 (`_Weapon0*`) + 副手 (`_Weapon1*`) 独立渲染

#### 渲染层级

**身体层** (`ApplyBodyLayers`):
- 裤子 (`_PantsTex`) → 服装 (`_ClothTex`) → 斗篷 (`_CloakTex`)
- 手套/鞋子: 颜色替换

**头部层** (`ApplyHeadLayers`):
- 头发 → 面部装饰 → 胡子 → 头盔（顶层）

**武器层** (无武器区域方案):
- 朝北: 武器在身体后面
- 朝南: 武器在身体前面，但手脚始终在武器前面
- 主手在副手前面

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

- 在 `EquipAnimSequenceAsset` 中为对应的动画 Key 新建 `AnimSequenceEntry`。
- 为需要支持的方向（SE/SW/NE/NW）配置 `DirectionalStrip`，NE/NW 可按需留空，系统会自动回退到 SE / SW。
- 在 `EquipmentData` 上挂载该 `animSet` 资产，即可在运行时优先使用序列帧渲染此装备。

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

### EquipmentData

```csharp
// 是否包含序列帧动画集
public bool HasAnimSet { get; }

// 根据方向获取基础 4 向贴图（带 NE/NW 回退）
public Sprite GetSprite(CharacterFacing facing);

// 根据方向和帧变体获取贴图（非武器支持 Up/Down 变体）
public Sprite GetSprite(CharacterFacing facing, FrameVariant variant);

// 根据行索引获取基础贴图 (0=SE,1=SW,2=NE,3=NW)
public Sprite GetSpriteByRow(int rowIndex);

// 按动画 Key 尝试获取序列帧（与 Animator Bool 同名）
public Sprite TryGetSequenceSpriteByKey(string key, int rowIndex, int frameIndex);
```

 ## 版本历史

- **v1.0**: 初始版本，基础换装功能
- **v1.1**: 添加双层 UV Map，分离头部和身体渲染
- **v1.2**: 添加参考帧系统，解决头部抖动问题
- **v1.3**: 添加 spriteFacing 支持，用于转头场景
- **v1.4**: 添加从 SE 方向自动生成其他方向数据的功能
- **v1.5**: 添加斗篷(Cloak)装备类型
- **v1.6**: 添加面部装饰(FaceAccessory)外观层
- **v1.7**: 添加裤子(Pants)装备类型，渲染层级在服装下面
- **v2.0**: 配置驱动渲染系统重构
  - 引入 `EquipTypeConfig` + `EquipTypeRegistry`，新增装备类型无需修改 Renderer 代码
  - 移除 `hideLeftWeapon`/`hideRightWeapon` 配置，武器渲染由序列帧优先原则控制
- **v2.1**: 武器槽位系统
  - 引入 `WeaponSlotType` 枚举（MainHand/TwoHand/DualWield/OffHand）
  - 主手 + 副手双武器支持，自动处理装备规则
  - Shader 支持双武器独立渲染（`_Weapon0*` + `_Weapon1*`）
  - 双持武器在两个锚点同时显示
  - 测试 UI 拆分为主手/副手两个下拉框

