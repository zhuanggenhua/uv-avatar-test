# uv-avatar-test

这是项目本体，一个基于 Unity 6 的 2D 像素角色换装与装备渲染系统。

它不是简单的“换一张贴图”，而是把角色基础动画、部位 UV 映射、装备数据、武器锚点、外观数据和 Shader 渲染组合在一起，让一个角色在运行时完成：

- 换衣服
- 换帽子、头盔、面具、披风、裤子、手套、鞋子
- 切换头发、胡子、面部装饰、眼部装饰
- 装备主手、副手、双手或双持武器
- 根据动画方向和帧数据自动决定装备前后层级
- 在不同动作中保持装备位置与角色动作同步

## 项目定位

这个项目的目标，是把 `MiniFantasy` 一类像素角色素材，从“离线拼图”变成“运行时可切换的装备系统”。

适用场景：

- 2D RPG / ARPG 的角色换装系统
- 像素风角色外观编辑
- 多职业、多装备的角色展示
- 需要主副手、盾牌、双手武器、双持武器的角色系统
- 需要统一角色动作与装备动作表现的 Demo 或正式项目

## 技术环境

- Unity `6000.3.10f1`
- Universal Render Pipeline
- 2D Animation / 2D Sprite 相关包
- Addressables
- Odin Inspector 模块

主要依赖见 [Packages/manifest.json](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Packages\manifest.json)。

## 目录结构

核心目录：

- [Assets/Scripts/EquipmentSystem](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Scripts\EquipmentSystem)
- [Assets/Data](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Data)
- [Assets/Scenes/SampleScene.unity](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Scenes\SampleScene.unity)

其中：

- `Runtime/` 放运行时换装、动画和渲染逻辑
- `Data/` 放角色外观、装备、帧数据、动画类型等 ScriptableObject
- `Editor/` 放编辑器工具，用来生成或修正帧数据、UV、描边、像素映射等

## 核心功能

### 1. 运行时装备切换

角色在运行时可以装备多种类型的部件。根据 [EquipmentRenderData.cs](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Scripts\EquipmentSystem\Data\Appearance\EquipmentRenderData.cs) 当前定义，主要支持：

- `Gloves`
- `Shoes`
- `Clothing`
- `Cloak`
- `Pants`
- `Helmet`
- `Hat`
- `Mask`
- `Weapon`
- `Shield`
- `Bag`

这些装备并不是都走同一种渲染方式：

- 有些是颜色替换
- 有些是头/身体区域的 UV 贴图替换
- 有些是基于锚点定位的武器渲染

### 2. 主手 / 副手 / 双手 / 双持武器

武器槽位支持多种模式：

- `MainHand`
- `OffHand`
- `TwoHand`
- `DualWield`

这意味着系统能正确处理：

- 普通单手武器
- 副手盾牌
- 双手武器禁止副手
- 双持武器同时占用两个锚点

对应逻辑主要在 [EquipmentRenderer.cs](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Scripts\EquipmentSystem\Runtime\EquipmentRenderer.cs)。

### 3. 动画切换与方向切换

角色动画由 [AnimationController.cs](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Scripts\EquipmentSystem\Runtime\AnimationController.cs) 统一控制。

当前支持的方向是四方向：

- `SE`
- `SW`
- `NE`
- `NW`

动画切换的核心方式是：

- 通过 Animator 的 Bool 参数切换不同动作
- 通过 `X / Y` 参数切换朝向

这意味着角色动作、朝向和装备渲染是联动的，而不是各自独立。

### 4. 角色外观系统

除了装备，角色还有一层“基础外观”数据，由 [CharacterAppearance.cs](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Scripts\EquipmentSystem\Data\Appearance\CharacterAppearance.cs) 管理。

它支持：

- 四方向头发
- 四方向胡子
- 四方向面部装饰
- 东/西向眼部装饰
- 左右眼颜色
- 肤色映射表

这部分数据不属于装备槽，而是角色本体外观。

### 5. 帧级精确控制

这个系统不是粗粒度“整张图片替换”，而是依赖帧数据精确描述每一帧。

帧数据由 [CharacterFrameData.cs](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Scripts\EquipmentSystem\Data\CharacterFrameData.cs) 定义，核心包括：

- 动画类型列表
- 每个动画的 spritesheet
- 每帧对应的 body/head UV map
- 武器锚点
- 头部、身体区域像素定义
- 手脚、眼睛遮罩
- 眼睛闭合状态
- 命中描边帧
- 序列帧偏移

这让系统可以做到帧级别控制装备位置和层级。

### 6. 内置 Demo UI

场景里有一个测试用的装备切换面板，对应脚本是 [EquipmentDemoExtension.cs](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Scripts\EquipmentSystem\Runtime\EquipmentDemoExtension.cs)。

它支持：

- 自动找到场景中激活的 `EquipmentRenderer`
- 切换各装备槽位
- 切换外观数据
- 切换动作
- 切换方向
- 开关阴影
- 一键卸下全部装备

这个 Demo UI 主要用于验证系统，而不是最终产品 UI。

## 现有数据资产

当前项目里已经有一批示例数据：

- [Assets/Data/Equip](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Data\Equip)
- [Assets/Data/Appearance](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Data\Appearance)
- [Assets/Data/FrameData](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Data\FrameData)
- [Assets/Data/AnimationType](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Data\AnimationType)

例如：

- [cloth.asset](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Data\Equip\cloth.asset) 是一个 `EquipmentData`
- [CharacterAppearance.asset](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Data\Appearance\CharacterAppearance.asset) 是角色外观
- [HalflingFramData.asset](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Data\FrameData\HalflingFramData.asset) 是角色帧数据

## 如何运行

### 方式 1：直接在 Unity 中打开

1. 用 Unity Hub 打开目录 [test](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test)
2. Unity 版本选择 `6000.3.10f1`
3. 等待包和 Library 导入完成
4. 打开场景 [Assets/Scenes/SampleScene.unity](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Scenes\SampleScene.unity)
5. 点击 Play

这是当前最直接的体验方式。

### 方式 2：查看运行时换装效果

进入 Play 模式后，场景中的 Demo UI 会尝试自动找到当前激活的角色对象，然后你可以：

- 切换帽子、服装、裤子、鞋子、手套等
- 切换主手/副手武器
- 切换角色外观
- 切换动作
- 切换方向
- 测试是否允许副手装备

## 使用方法

### 一、查看现成示例

最简单的方式是直接跑 `SampleScene`。

你可以在 Play 模式中验证：

- 不同装备槽位是否正确渲染
- 双手武器是否禁用副手
- 盾牌在不同方向下是否切换前后层级
- 头发/胡子/面部装饰是否与头部朝向一致
- 动画切换后装备是否仍对齐

### 二、给角色挂上换装系统

如果你要在自己的角色上接入这个系统，最少需要这些组件和数据：

1. 一个带 `SpriteRenderer` 的角色对象
2. 一个 `Animator`
3. 一个 [EquipmentRenderer.cs](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Scripts\EquipmentSystem\Runtime\EquipmentRenderer.cs)
4. 一个 [AnimationController.cs](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Scripts\EquipmentSystem\Runtime\AnimationController.cs)
5. 一个 `CharacterFrameData`
6. 一个可选的 `CharacterAppearance`

`EquipmentRenderer` 里至少要配置：

- `frameData`
- `appearance`
- `initialEquipments`（可选）

### 三、创建新装备

要新增一件装备，一般做法是：

1. 新建一个 `Equipment Data` 资产
2. 选择它的 `type`
3. 填写四方向基础贴图
4. 如果是武器，设置 `weaponSlotType`
5. 如果是头部装备，按需要设置 `hideHair` 和 `hideBeard`
6. 如果这个装备需要动画序列帧，填写 `animSequences`

可参考现有资产：

- [cloth.asset](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Data\Equip\cloth.asset)

### 四、创建角色外观

如果要为角色定义“非装备型”外观，可以新建 `Character Appearance` 资产。

可配置：

- 四方向头发
- 四方向胡子
- 面部装饰
- 眼部装饰
- 左右眼颜色
- 肤色映射数组

这适合做：

- 不同发型
- 不同胡型
- 面部附件
- 换肤色
- 异色瞳

### 五、创建或编辑帧数据

帧数据是这个系统最关键的一层。

你需要在 `CharacterFrameData` 里配置：

- 动画类型
- 每个动画的图集
- 帧尺寸
- 每帧的武器锚点
- 头部和身体区域
- limb mask
- UV map

项目里已经有多种编辑器工具辅助这件事，主要在：

- [Assets/Scripts/EquipmentSystem/Editor](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Scripts\EquipmentSystem\Editor)

例如：

- `FrameDataEditor`
- `PixelSkinMapWindow`
- `BlackOutlineCleanupWindow`
- `EquipmentAnimSequenceEditor`

## 大致原理

这一部分是项目真正有价值的核心。

### 1. 角色贴图不是整张换，而是按区域映射

服装、裤子、披风、头盔这类装备，并不是通过替换整个角色精灵实现的。

系统会先通过 `CharacterFrameData` 知道当前帧里：

- 头部区域在哪
- 身体区域在哪
- 手脚像素在哪
- 眼睛像素在哪

然后 Shader 根据这些区域信息，把装备贴图映射到角色对应区域上。

这就是为什么这个系统可以在同一套角色动画上动态换装，而不需要每一套装备都重画整套角色动画。

### 2. 角色动作驱动帧同步

`AnimationController` 会通过 Animator 参数控制当前动作和方向。

`EquipmentRenderer` 在 `LateUpdate` 中观察角色当前使用的 `Sprite`，一旦发现 Sprite 变了，就从 Sprite 的 `rect` 反推出：

- 当前是第几帧
- 当前是第几行

然后根据这个索引去 `CharacterFrameData` 找到对应的 `FrameData`，再刷新当前装备渲染。

这意味着装备渲染始终跟着角色当前动画帧走。

### 3. 武器不是普通贴图，而是锚点驱动

武器和盾牌与衣服不同。

它们不是贴在 body/head 区域上，而是通过每帧定义的锚点来确定：

- 挂在哪里
- 旋转多少度
- 在角色前还是后
- 主手还是副手

锚点类型主要有：

- `MainHandWeapon`
- `OffHandWeapon`

每一帧都可以有不同锚点位置和朝向，所以武器能跟着角色动作自然摆动。

### 4. 前后层级不是固定的，而是按朝向和装备类型计算

像素角色最难的一点，不是“把武器画上去”，而是“什么时候武器在身体前，什么时候在身体后”。

这个系统会综合判断：

- 当前角色朝向
- 当前使用的是主手还是副手锚点
- 当前武器的类型
- 当前帧序列是否显式要求前景或背景

例如：

- 盾牌在朝南和朝北时，手与盾的遮挡关系会不同
- 主手和副手在 SE/SW/NE/NW 下，左右手的前后关系会变化

这部分逻辑主要就在 `EquipmentRenderer` 的武器渲染流程里。

### 5. 支持静态四方向和序列帧两套武器方案

`EquipmentRenderData` 不只支持四方向静态武器图，还支持按动画类型配置序列帧。

也就是说，一个武器可以有两种工作模式：

- 没有单独动作时，用四方向静态图
- 有动作序列时，按当前动画和帧索引播放专用武器帧

这让复杂武器动作也能被系统支持。

### 6. 外观和装备分层处理

角色外观和装备是分开的两层概念：

- `CharacterAppearance` 负责头发、胡子、眼睛、肤色等“角色本体”
- `EquipmentRenderData` 负责衣服、帽子、武器、盾牌等“可穿戴物”

这样设计的好处是：

- 可以同一套装备搭配不同外观
- 可以同一套外观切换不同装备
- 外观数据不会和装备槽位互相污染

### 7. 肤色替换基于颜色查表

肤色不是通过一张一张替换贴图完成的，而是通过 `skinSrcColors -> skinDstColors` 的颜色数组映射实现。

运行时 `EquipmentRenderer` 会把这组颜色传给 Shader，然后由 Shader 执行查表式换肤。

这样可以：

- 节省素材数量
- 同一套角色图支持多个肤色版本
- 不需要单独为每种肤色准备完整资源

### 8. 编辑器工具的意义

这个系统的数据量大，如果纯手填会非常痛苦，所以项目里有一批编辑器工具去做：

- 自动检测身体和头部区域
- 生成或修复 UV 数据
- 处理像素描边
- 编辑动画序列帧
- 自动注册动画类型

所以这个项目的真正工作流不是“纯运行时代码”，而是：

```text
素材准备 -> 编辑器生成帧数据 -> ScriptableObject 配置 -> 运行时渲染
```

## 推荐理解方式

如果你要继续开发这个项目，建议按这个顺序理解：

1. 先看 [Assets/Scenes/SampleScene.unity](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Scenes\SampleScene.unity)
2. 再看 [EquipmentDemoExtension.cs](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Scripts\EquipmentSystem\Runtime\EquipmentDemoExtension.cs)
3. 再看 [AnimationController.cs](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Scripts\EquipmentSystem\Runtime\AnimationController.cs)
4. 然后看 [EquipmentRenderer.cs](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Scripts\EquipmentSystem\Runtime\EquipmentRenderer.cs)
5. 最后看 `CharacterFrameData / CharacterAppearance / EquipmentRenderData`

这样最容易把“Demo 操作层”和“底层渲染层”串起来。

## 当前说明

我前一轮把仓库根目录误判成项目本体了；以当前仓库实际情况看，真正应当作为项目说明入口的是这个 [test/README.md](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\README.md)。
