# 装备换装系统文档

## 概述

这是一个基于 UV Map 的像素风格角色换装系统，支持：
- **武器**: 用锚点定位
- **服装**: Shader UV 重映射到躯干
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
├── type: EquipmentType      // Weapon/Clothing/Helmet/Gloves/Shoes
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

### UV Map 格式

每个像素存储 RGBA 四个通道：
- **R**: U 坐标 (0~1)
- **G**: V 坐标 (0~1)
- **B**: 部位 ID
- **A**: 1.0

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

---

## 版本历史

- **v1.0**: 初始版本，基础换装功能
- **v1.1**: 添加双层 UV Map，分离头部和身体渲染
- **v1.2**: 添加参考帧系统，解决头部抖动问题
- **v1.3**: 添加 spriteFacing 支持，用于转头场景
- **v1.4**: 添加从 SE 方向自动生成其他方向数据的功能
  - SW = SE 水平镜像（左右手/脚自动互换）
  - NE = SE 直接复制
  - NW = SE 水平镜像（同 SW 逻辑）
