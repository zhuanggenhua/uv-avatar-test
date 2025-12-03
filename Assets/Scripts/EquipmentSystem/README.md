# 装备换装系统文档

## 概述

这是一个基于 UV Map 的像素风格角色换装系统，支持：
- **武器**: 用锚点定位
- **服装**: Shader UV 重映射到躯干
- **斗篷**: Shader UV 重映射到躯干，渲染在服装前面
- **头部装备**: 头发 → 胡子 → 头盔（三层叠加）
- **手套/鞋子**: 颜色替换

## 系统架构

```
EquipmentSystem/
├── Data/                    # 数据定义
│   ├── CharacterFrameData.cs    # 帧数据（锚点、部位区域、UV方向等）
│   ├── CharacterAppearance.cs   # 角色外观（头发、胡子）
│   └── EquipmentData.cs         # 装备数据（4方向贴图）
├── Editor/                  # 编辑器工具
│   ├── FrameDataEditor.cs       # 帧数据编辑器窗口
│   ├── DualUVMapGenerator.cs    # UV Map 生成器
│   └── EquipmentDataEditor.cs   # 装备数据编辑器
├── Runtime/                 # 运行时组件
│   ├── EquipmentRenderer.cs     # 装备渲染器
│   └── AnimationController.cs   # 动画控制器
└── Shaders/                 # Shader
    └── EquipmentUV.shader       # 装备渲染 Shader
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
                └── bodyRegions: List<BodyPartRegion> // 部位区域
```

### EquipmentData (ScriptableObject)

存储装备数据：

```csharp
EquipmentData
├── equipmentId: string
├── type: EquipmentType      // Weapon/Clothing/Cloak/Helmet/Gloves/Shoes
├── spriteSE/SW/NE/NW: Sprite  // 4方向贴图
├── anchorType: AnchorType   // 武器锚点类型
├── selfAnchor: Vector2Int   // 装备自身锚点
└── leftColor/rightColor: Color32  // 手套/鞋子颜色
```

### BodyPartRegion

部位区域数据：

```csharp
BodyPartRegion
├── part: CharacterBodyPart  // Head/Torso/LeftHand/RightHand/LeftFoot/RightFoot
├── orientation: UVOrientation   // UV 旋转方向
├── spriteFacing: CharacterFacing // 贴图方向（选择哪张贴图）
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

---

## Shader 说明

### EquipmentUV.shader

主要功能：
1. 采样 UV Map 获取部位信息和 UV 坐标
2. 根据部位 ID 选择对应的装备贴图采样
3. 支持多层叠加（头发 → 胡子 → 头盔）

关键函数：

```hlsl
// 将局部 UV (0~1) 转换为贴图实际 UV
float2 TransformUV(float2 uv, float4 rect)

// 应用身体层装备
fixed4 ApplyBodyLayers(fixed4 baseColor, fixed4 bodyUV)

// 应用头部层装备
void ApplyHeadLayers(float2 baseHeadUV, float headPartID, 
                     inout fixed4 ioColor, out float headLayerAlpha)
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
// 装备
public void Equip(EquipmentData equip)

// 卸载
public void Unequip(EquipmentData equip)

// 刷新显示
public void Refresh()
```

### CharacterFrameData

```csharp
// 获取帧数据
public FrameData GetFrameData(string animName, int rowIndex, int frame)

// 获取动画
public AnimationData GetAnimation(string animName)
```

### EquipmentData

```csharp
// 根据方向获取贴图
public Sprite GetSprite(CharacterFacing facing)

// 根据行索引获取贴图
public Sprite GetSpriteByRow(int rowIndex)
```


## 版本历史

- **v1.0**: 初始版本，基础换装功能
- **v1.1**: 添加双层 UV Map，分离头部和身体渲染
- **v1.2**: 添加参考帧系统，解决头部抖动问题
- **v1.3**: 添加 spriteFacing 支持，用于转头场景
- **v1.4**: 添加从 SE 方向自动生成其他方向数据的功能
  - SW = SE 水平镜像（左右手/脚自动互换）
  - NE = SE 直接复制
  - NW = SE 水平镜像（同 SW 逻辑）
- **v1.5**: 添加斗篷(Cloak)装备类型，渲染层级在服装前面
- **v1.6**: 添加面部装饰(FaceAccessory)外观层
  - 渲染层级：头发（底层）→ 面部装饰 → 胡子 → 头盔（顶层）
  - 特殊处理：每个方向独立，未填写的方向不显示（不回退到其他方向）

## 帧数据编辑器（FrameDataEditor）架构概览

### 1. 角色与职责边界

- **CharacterFrameData**
  - 纯数据资产（ScriptableObject）
  - 持久化存储：动画列表、每帧的锚点、部位区域、UV、检测配置、UV 画板配置等
  - 不负责任何编辑逻辑或 GUI

- **FrameDataEditor (EditorWindow)**
  - 编辑器入口窗口，负责：
    - 显示和编辑 `CharacterFrameData` 的内容
    - 提供涂色、自动检测、区域扩展、UV Map 生成等工具
  - 持有一份“当前编辑会话状态”：当前动画、行、帧、当前部位、选区、视图缩放/平移等
  - 通过 `_isDirty + SaveFrameToData + LoadFrameData` 在内存缓存与 ScriptableObject 之间同步

- **DualUVMapGenerator**
  - 纯工具类，不依赖 EditorWindow 状态
  - 从 `CharacterFrameData.AnimationData` 生成 `bodyUVMap` / `headUVMap` 纹理
  - 只依赖于每个 `BodyPartPixel.uv` 和部位 ID 约定

- **EquipmentDataEditor**
  - `EquipmentData` 的自定义 Inspector
  - 与 `FrameDataEditor` 解耦，只通过约定的 UV Map / Shader 协议协同工作

> 设计要点：持久化数据（CharacterFrameData）与编辑器状态（FrameDataEditor）严格分离，
> 所有可以复用到其他工具或运行时调试器的逻辑，优先放在 Data/Utility 中，而不是 EditorWindow 里。

### 2. FrameDataEditor 内部模块

从代码结构上可以粗略分为四块：

1. **左侧工具栏（Toolbar）**
   - `DrawDataSection`：选择 `CharacterFrameData`
   - `DrawConfigSection`：当前动画的 Spritesheet / 帧尺寸 / 帧数
   - `DrawAnimationSection`：动画类型数据库、隐藏武器开关
   - `DrawFrameSelection`：行/帧切换，调用 `SwitchRow` / `SwitchFrame`（内部会 `SaveIfDirty + LoadFrameData`）
   - `DrawTabContent`：
     - `BodyPaint` 标签：部位选择、显示选项、编辑模式、自动涂色、批量操作、UV 画板配置
     - `Anchor` 标签：武器锚点编辑

2. **右上：UV 画板（Palette）**
   - `DrawPalette`：
     - 绘制背景与参考底图（`_data.paletteRefSprite`）
     - 按 `BodyPartPixel.uv` 计算颜色，显示 UV 分布
     - 绘制头/身体的 UV 区域框（`headUVRegion` / `torsoUVRegion`）
     - 根据 `_hoverPalettePixel` 在左上角显示当前悬停像素坐标与 UV 值
   - `DrawPaletteSelection`：
     - 显示悬停高亮（白框）
     - 显示已确定的画板选区（用于从调色板复制 UV）
   - `GetPalettePixelPos / IsValidPalettePixel`：处理屏幕坐标到调色板像素坐标的转换与校验

3. **右下：画布（角色 Spritesheet 预览）**
   - `DrawCanvas`：
     - 背景棋盘格 + 当前帧 Spritesheet 区域
     - 用 `_partPixels` / `_partUVs` 叠加部位着色（支持按当前部位或全部部位显示）
     - 显示当前部位的边框与锚点
     - 根据 `_hoverCanvasPixel` 在左上角显示悬停像素坐标与对应 UV
   - `DrawCanvasSelection`：
     - 显示悬停高亮（白框）
     - 显示画布选区（用于从画板复制 UV 到角色或擦除区域）

4. **输入与命令处理**
   - `HandleInput`：集中处理键鼠事件：
     - 左键：涂色 / 框选
     - 右键：擦除 / 拖动擦除
     - 中键：平移
     - Shift + 拖拽：框选区域
     - 键盘 1/2：标签切换
   - `OnLeftClick / OnRightClick`：根据当前标签和模式分发到具体逻辑（改锚点、改涂色、修改选区等）
   - 所有会修改当前帧数据的操作，最终都会更新 `_partPixels` / `_partUVs` / `_anchors`，并设置 `_isDirty = true`，由 `SaveIfDirty/SaveWithUndo` 统一落盘

### 3. 数据流与保存策略

1. **加载（Editor → 内存）**
   - 打开窗口或切换动画/行/帧时：
     - `LoadFrameData()` 根据 `_animName + _row + _frame` 从 `CharacterFrameData` 中取到对应 `FrameData`
     - 将其中的 `anchors`、`bodyRegions` 深拷贝到 `_anchors`、`_partPixels`、`_partUVs` 等运行时结构

2. **编辑（内存 → 内存）**
   - 所有操作只修改 EditorWindow 内部的字典/列表，不直接改 ScriptableObject
   - 例如：涂色、擦除、镜像、扩展/收缩、自动检测等

3. **保存（内存 → Editor 资产）**
   - 触发点：
     - 切换行/帧 / 切换标签
     - 窗口失焦 (`OnLostFocus`) / 关闭 (`OnDisable`)
     - 批量操作内部手动调用 `SaveWithUndo`
   - `SaveFrameToData()`：
     - 通过 `GetCurrentAnimation()` 找到当前动画
     - `GetOrCreateFrame(_frame, _row)` 拿到对应 `FrameData`
     - 清空并重建 `anchors` / `bodyRegions`，把 `_partPixels` / `_partUVs` 写回
     - `EditorUtility.SetDirty(_data)` 标记资产已修改

> 注意：Editor 使用 `_animName` + `AnimationTypeDatabase` 来定位当前动画，
> 而不是直接依赖 `_animIndex` 与 `animations` 列表的顺序，避免数据错位。

### 4. 可复用 & 强耦合部分（为后续重构做准备）

- **相对“通用/可抽象”的逻辑**：
  - 帧数据读写：`LoadFrameData / SaveFrameToData`
  - 区域扩展/收缩算法：基于边界像素向外填充 UV
  - 自动检测：根据检测配置和皮肤颜色，自动识别头部/身体/手脚区域
  - 从 SE 生成其他方向行的数据（镜像+手脚互换）
  - UV Map 生成（已在 `DualUVMapGenerator` 中，与具体编辑器解耦）

- **当前与项目强耦合的逻辑**：
  - 部位枚举与 Shader 中 B 通道编码的约定（0.1, 0.2, 0.4...）
  - 头/身体 UV 区域与检测区域尺寸的默认配置
  - 调色板尺寸、坐标系以及“以整张调色板为 UV 空间”的假设

在后续重构时，可以优先把“通用逻辑”下沉到 Data/Utility 层，
并通过接口或策略模式抽象出“项目特定的 UV 编码规则 / 调色板布局”，
从而让 FrameDataEditor 变成一个更通用的“像素角色 UV/区域编辑器”。

---
