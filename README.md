# uv-avatar-test

这是项目本体。  
一个 Unity 6 的 2D 像素角色换装系统，核心目标是：在运行时给同一套角色动画切换外观、服装和武器，而不是为每套装备单独重画整套动画。

## 先看哪里

如果你第一次接手这个项目，按这个顺序看：

1. [Assets/Scenes/SampleScene.unity](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Scenes\SampleScene.unity)
2. [Assets/Scripts/EquipmentSystem/Runtime/EquipmentDemoExtension.cs](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Scripts\EquipmentSystem\Runtime\EquipmentDemoExtension.cs)
3. [Assets/Scripts/EquipmentSystem/Runtime/AnimationController.cs](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Scripts\EquipmentSystem\Runtime\AnimationController.cs)
4. [Assets/Scripts/EquipmentSystem/Runtime/EquipmentRenderer.cs](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Scripts\EquipmentSystem\Runtime\EquipmentRenderer.cs)
5. [Assets/Scripts/EquipmentSystem/Data/CharacterFrameData.cs](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Scripts\EquipmentSystem\Data\CharacterFrameData.cs)
6. [Assets/Scripts/EquipmentSystem/Data/Appearance/CharacterAppearance.cs](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Scripts\EquipmentSystem\Data\Appearance\CharacterAppearance.cs)
7. [Assets/Scripts/EquipmentSystem/Data/Appearance/EquipmentRenderData.cs](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Scripts\EquipmentSystem\Data\Appearance\EquipmentRenderData.cs)

## 环境

- Unity `6000.3.10f1`
- 打开目录：[test](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test)

## 这个系统能做什么

- 切换服装、裤子、披风、帽子、头盔、面具、手套、鞋子
- 切换头发、胡子、面部装饰、眼部装饰
- 装备主手、副手、双手、双持武器
- 根据朝向和帧数据自动计算武器前后层级
- 根据角色当前动画帧同步装备显示
- 用颜色映射做换肤

## 快速运行

1. 用 Unity Hub 打开 [test](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test)
2. 等包导入完成
3. 打开 [Assets/Scenes/SampleScene.unity](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Scenes\SampleScene.unity)
4. 进入 Play
5. 用场景里的 Demo UI 测试切装备、切动作、切方向

Demo UI 脚本是：

- [Assets/Scripts/EquipmentSystem/Runtime/EquipmentDemoExtension.cs](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Scripts\EquipmentSystem\Runtime\EquipmentDemoExtension.cs)

它会自动找场景里激活的 `EquipmentRenderer`。

## 整个流程怎么走

这个项目的完整流程不是“只写运行时代码”，而是下面这条链：

```text
准备角色动画图 -> 建 AnimationType -> 建 CharacterFrameData -> 用编辑器工具标注帧数据/锚点/UV ->
建 CharacterAppearance -> 建 EquipmentRenderData -> 在角色上挂 EquipmentRenderer + AnimationController ->
运行时切换装备和动画
```

下面按这个顺序写。

## 第 1 步：准备角色动画图

你首先要有角色基础动画图集，也就是角色本体 spritesheet。

`CharacterFrameData` 会围绕这套图来建立所有换装数据，所以这一步是源头。

已有示例数据在：

- [Assets/Data/FrameData](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Data\FrameData)

## 第 2 步：准备动画类型

动画类型不是直接写死在代码里的，而是数据化的。

相关文件：

- [Assets/Scripts/EquipmentSystem/Data/Animation/AnimationTypeItem.cs](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Scripts\EquipmentSystem\Data\Animation\AnimationTypeItem.cs)
- [Assets/Scripts/EquipmentSystem/Data/Animation/AnimationTypeDatabase.cs](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Scripts\EquipmentSystem\Data\Animation\AnimationTypeDatabase.cs)
- [Assets/Scripts/EquipmentSystem/Editor/Utilities/AnimationTypeAutoRegister.cs](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Scripts\EquipmentSystem\Editor\Utilities\AnimationTypeAutoRegister.cs)

做法：

1. 创建 `AnimationTypeItem`
2. 放进 `AnimationTypeDatabase`
3. 或直接用自动注册工具扫描

这一层的作用是统一动作名，比如：

- `Idle`
- `Walk`
- `Attack`
- `Die`

运行时 `AnimationController` 和装备序列帧都会用这个动作类型来对齐。

## 第 3 步：创建 CharacterFrameData

这是最核心的数据资产。

相关文件：

- [Assets/Scripts/EquipmentSystem/Data/CharacterFrameData.cs](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Scripts\EquipmentSystem\Data\CharacterFrameData.cs)

它负责描述：

- 角色有哪些动画
- 每个动画的 spritesheet
- 每帧尺寸
- 每一帧的头部区域和身体区域
- 每一帧的手脚像素
- 每一帧的武器锚点
- 每一帧的 UV map
- 每一帧的闭眼状态、描边状态、序列偏移

可以先直接参考现有资产：

- [Assets/Data/FrameData/HalflingFramData.asset](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Data\FrameData\HalflingFramData.asset)

## 第 4 步：用 FrameDataEditor 标注帧数据

这是整个制作流程里最重要的一步。

工具入口：

- `Tools/Equipment System/Frame Editor`

对应代码：

- [Assets/Scripts/EquipmentSystem/Editor/FrameDataEditor.cs](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Scripts\EquipmentSystem\Editor\FrameDataEditor.cs)

这个工具主要干四件事：

1. 选择当前 `CharacterFrameData`
2. 选择动画、方向、帧
3. 标注头部/身体/手脚/眼睛区域
4. 标注主手、副手锚点

你可以把它理解成“把角色每一帧的可换装信息标出来”。

### 在这个工具里通常怎么做

常见顺序：

1. 选中 `CharacterFrameData`
2. 选一个动画类型
3. 填 `Spritesheet / frameSize / framesPerRow / rowCount`
4. 对当前帧做自动检测
5. 手工修正身体区域、头部区域、手脚像素
6. 标主手和副手锚点
7. 存盘
8. 生成整行动画或整套动画的数据

### 这个工具里你会用到的关键功能

- 自动检测当前帧身体部位
- 自动检测全帧
- 只自动涂色，不动锚点
- 从 `SE` 生成其他方向
- 修复所有帧的 `spriteFacing`
- 生成当前动画或全部动画的双层 UV Map

这几个功能基本就是项目制作流程的骨架。

## 第 5 步：生成双层 UV Map

这个项目不是把衣服直接叠一张图，而是靠 UV 映射把装备贴到角色对应区域。

所以帧数据做完后，要生成 UV Map。

相关位置：

- `FrameDataEditor` 里的 UV Map 生成入口
- [Assets/Scripts/EquipmentSystem/Editor/Utilities/DualUVMapGenerator.cs](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Scripts\EquipmentSystem\Editor\Utilities\DualUVMapGenerator.cs)

这里生成的是两层：

- `bodyUVMap`
- `headUVMap`

它们会在运行时被 `EquipmentRenderer` 和 Shader 使用。

## 第 6 步：创建 CharacterAppearance

这一步是做“角色本体外观”，不是装备。

相关文件：

- [Assets/Scripts/EquipmentSystem/Data/Appearance/CharacterAppearance.cs](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Scripts\EquipmentSystem\Data\Appearance\CharacterAppearance.cs)

这个资产里可以配置：

- 四方向头发
- 四方向胡子
- 四方向面部装饰
- 东西向眼部装饰
- 左右眼颜色
- 肤色映射

现有示例：

- [Assets/Data/Appearance/CharacterAppearance.asset](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Data\Appearance\CharacterAppearance.asset)

### 换肤怎么做

换肤不是手工写颜色数组，而是用工具生成。

工具入口：

- `Tools/Equipment System/Pixel Skin Map`

对应代码：

- [Assets/Scripts/EquipmentSystem/Editor/PixelSkinMapWindow.cs](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Scripts\EquipmentSystem\Editor\PixelSkinMapWindow.cs)

操作顺序：

1. 选 `Base Sprite`
2. 选 `Target Sprite`
3. 选 `CharacterAppearance`
4. 点 `Analyze & Apply`

工具会把结果写入：

- `skinSrcColors`
- `skinDstColors`

## 第 7 步：创建装备数据 EquipmentRenderData

每件装备对应一个 `EquipmentRenderData`。

相关文件：

- [Assets/Scripts/EquipmentSystem/Data/Appearance/EquipmentRenderData.cs](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Scripts\EquipmentSystem\Data\Appearance\EquipmentRenderData.cs)

现有示例：

- [Assets/Data/Equip/cloth.asset](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Data\Equip\cloth.asset)

创建一件装备时，最少要填：

1. `type`
2. 四方向基础贴图

按类型再补下面的数据。

### 如果是普通穿戴装备

例如：

- Clothing
- Pants
- Cloak
- Helmet
- Hat
- Mask

你要关注：

- 四方向贴图
- 是否隐藏头发 `hideHair`
- 是否隐藏胡子 `hideBeard`

### 如果是武器或盾牌

你要关注：

- `weaponSlotType`
- 是否使用副手锚点 `useOffHandAnchor`
- 是否隐藏身体描边 `hideOutlineOnBody`

武器槽位包括：

- `MainHand`
- `OffHand`
- `TwoHand`
- `DualWield`

## 第 8 步：如果武器有独立动作，再做 Anim Sequence

不是所有武器都只用四方向静态图。

如果某个武器在 `Idle/Walk/Attack` 等动作下有专门序列帧，就要填 `animSequences`。

相关工具：

- [Assets/Scripts/EquipmentSystem/Editor/Utilities/EquipmentAnimSequenceTools.cs](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Scripts\EquipmentSystem\Editor\Utilities\EquipmentAnimSequenceTools.cs)
- [Assets/Scripts/EquipmentSystem/Editor/EquipmentAnimSequenceEditor.cs](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Scripts\EquipmentSystem\Editor\EquipmentAnimSequenceEditor.cs)

这一步的作用是：

- 让武器在不同动作里播放自己的序列帧
- 而不是只挂一张静态武器图

## 第 9 步：把系统挂到角色上

运行时最关键的两个组件是：

- [Assets/Scripts/EquipmentSystem/Runtime/EquipmentRenderer.cs](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Scripts\EquipmentSystem\Runtime\EquipmentRenderer.cs)
- [Assets/Scripts/EquipmentSystem/Runtime/AnimationController.cs](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Scripts\EquipmentSystem\Runtime\AnimationController.cs)

角色对象至少需要：

1. `SpriteRenderer`
2. `Animator`
3. `EquipmentRenderer`
4. `AnimationController`

`EquipmentRenderer` 至少要配置：

- `frameData`
- `appearance`
- `initialEquipments` 可选

## 第 10 步：运行时切换

运行时主要就三类调用。

### 切装备

`EquipmentRenderer`：

- `Equip(...)`
- `Unequip(...)`
- `UnequipAll()`

### 切外观

`EquipmentRenderer`：

- `SetAppearance(...)`

### 切动画和方向

`AnimationController`：

- `SetAnimation(...)`
- `SetDirection(...)`
- `SetShadowEnabled(...)`

## 运行时原理，只说流程相关的

### 1. 角色动画先动

`AnimationController` 改的是 Animator 参数。  
也就是说，角色本体动画先切。

### 2. EquipmentRenderer 读当前 Sprite

`EquipmentRenderer` 在 `LateUpdate` 里看角色当前 Sprite 是否变化。

变了以后，它会从 Sprite 的 `rect` 算出：

- 当前第几帧
- 当前第几行

### 3. 再去 FrameData 里查这一帧该怎么渲染

查到这一帧后，系统会知道：

- 头部区域
- 身体区域
- 手脚像素
- 武器锚点
- UV Map
- 眼睛闭合等附加信息

### 4. 非武器装备走 UV / 颜色映射

例如：

- 服装
- 裤子
- 帽子
- 头盔
- 手套
- 鞋子

这些要么走 UV 贴图映射，要么走颜色替换。

### 5. 武器走锚点

武器和盾牌会根据当前帧锚点决定：

- 放在哪里
- 旋转多少
- 在角色前还是后
- 主手还是副手

如果武器配置了独立序列帧，就优先走序列帧；没有的话才退回四方向静态图。

## 你真正需要改的几个地方

如果你后面要继续开发，通常就是改这几类内容：

### 做新角色

- 新建 `CharacterFrameData`
- 用 `FrameDataEditor` 把一整套帧数据做出来

### 做新外观

- 新建 `CharacterAppearance`

### 做新装备

- 新建 `EquipmentRenderData`

### 做新武器动作

- 给装备补 `animSequences`

### 调运行时逻辑

- 改 [EquipmentRenderer.cs](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Scripts\EquipmentSystem\Runtime\EquipmentRenderer.cs)
- 改 [AnimationController.cs](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Scripts\EquipmentSystem\Runtime\AnimationController.cs)

## 现成数据入口

你要找示例资产，优先看这里：

- [Assets/Data/Equip](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Data\Equip)
- [Assets/Data/Appearance](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Data\Appearance)
- [Assets/Data/FrameData](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Data\FrameData)
- [Assets/Data/AnimationType](C:\Users\zhuagenbao\Desktop\MiniCharacterCreator-main\test\Assets\Data\AnimationType)

## 一句话总结

这个项目的真正核心不是“装备切换按钮”，而是：

`CharacterFrameData + 编辑器标注工具 + EquipmentRenderer`

前者负责把每一帧该怎么换装描述清楚，后者负责在运行时按当前动画帧把这些描述渲染出来。
