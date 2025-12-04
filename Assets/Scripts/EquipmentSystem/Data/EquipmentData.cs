using System;
using System.Collections.Generic;
using UnityEngine;

namespace EquipmentSystem.Data
{
    /// <summary>
    /// 装备类型
    /// </summary>
    public enum EquipmentType
    {
        Gloves, //   手套 - 颜色替换手部像素
        Shoes, //   鞋子 - 颜色替换脚部像素 
        Clothing, //   服装 - UV 映射到躯干 (Body 层)
        Cloak, //   斗篷 - UV 映射到躯干 (Body 层)，渲染在服装前面
        Pants, //   裤子 - UV 映射到躯干 (Body 层)，与服装共用 Body 区域
        Helmet, //   头盔 - UV 映射到头部 (Head 层)，覆盖在头发/胡子之上
        Hat,   //   帽子 - 使用头部插槽 (Head 层)
        Mask,  //   面罩 - 使用头部插槽 (Head 层)
        Weapon, //   武器 - 用锚点定位
        Shield, //   盾牌 - Weapon 渲染模式，通常占用副手
        Bag, //  背包 - 用锚点定位

    }

    /// <summary>
    /// 武器槽位类型
    /// </summary>
    public enum WeaponSlotType
    {
        MainHand,    // 单手主手武器
        TwoHand,     // 双手武器（禁止副手）
        DualWield,   // 双持武器（禁止副手）
        OffHand,     // 副手武器（盾牌等）
    }

    /// <summary>
    /// 4方向贴图集（用于变体）
    /// </summary>
    [Serializable]
    public class DirectionalSpriteSet
    {
        public Sprite se;
        public Sprite sw;
        public Sprite ne;
        public Sprite nw;

        public Sprite GetByFacing(CharacterFacing facing)
        {
            return DirectionalSpriteHelper.GetByFacing(facing, se, sw, ne, nw);
        }

        public Sprite GetByRow(int rowIndex) => GetByFacing((CharacterFacing)rowIndex);
    }

    /// <summary>
    /// 4方向贴图工具类
    /// </summary>
    public static class DirectionalSpriteHelper
    {
        /// <summary>
        /// 根据方向获取对应的 Sprite
        /// </summary>
        /// <param name="facing">角色朝向</param>
        /// <param name="se">SE 方向贴图 (默认回退)</param>
        /// <param name="sw">SW 方向贴图</param>
        /// <param name="ne">NE 方向贴图</param>
        /// <param name="nw">NW 方向贴图</param>
        public static Sprite GetByFacing(
            CharacterFacing facing,
            Sprite se,
            Sprite sw,
            Sprite ne,
            Sprite nw
        )
        {
            Sprite result;
            switch (facing)
            {
                case CharacterFacing.SouthEast:
                    result = se;
                    break;
                case CharacterFacing.SouthWest:
                    result = sw;
                    break;
                case CharacterFacing.NorthEast:
                    // NE 优先使用自身；未配置时回退到 SE
                    result = ne != null ? ne : se;
                    break;
                case CharacterFacing.NorthWest:
                    // NW 优先使用自身；未配置时先回退到 SW，SW 也为空时再回退到 SE
                    if (nw != null) return nw;
                    if (sw != null) return sw;
                    return se;
                default:
                    result = se;
                    break;
            }
            return result != null ? result : se;
        }

        /// <summary>
        /// 根据行索引获取对应的 Sprite (0=SE, 1=SW, 2=NE, 3=NW)
        /// </summary>
        public static Sprite GetByRow(int rowIndex, Sprite se, Sprite sw, Sprite ne, Sprite nw)
        {
            return GetByFacing((CharacterFacing)rowIndex, se, sw, ne, nw);
        }
    }

    /// <summary>
    /// 装备数据
    /// </summary>
    [CreateAssetMenu(fileName = "EquipmentData", menuName = "Equipment System/Equipment Data")]
    public class EquipmentData : ScriptableObject
    {
        // 基础
        public string equipmentId;
        public EquipmentType type;

        // 四向基础贴图（所有 Sprite/Weapon 类型统一使用）
        public Sprite spriteSE;
        public Sprite spriteSW;
        public Sprite spriteNE;
        public Sprite spriteNW;

        [Header("贴图变体（可选）")]
        [Tooltip("向上动作时使用的 4 向变体贴图（FrameVariant.Up）；SE 不为空时视为启用")] 
        public DirectionalSpriteSet upVariant;

        [Tooltip("向下动作时使用的 4 向变体贴图（FrameVariant.Down）；SE 不为空时视为启用")] 
        public DirectionalSpriteSet downVariant;

        // 武器设置
        [Tooltip("武器槽位类型：主手/双手/双持/副手")]
        public WeaponSlotType weaponSlotType = WeaponSlotType.MainHand;

        // 颜色替换 (手套/鞋子)
        public Color32 leftColor = new Color32(150, 100, 50, 255);
        public Color32 rightColor = new Color32(150, 100, 50, 255);
        
        [Header("头部装备设置")]
        [Tooltip("戴上此头部装备时隐藏头发")]
        public bool hideHair = false;
        [Tooltip("戴上此头部装备时隐藏胡子")]
        public bool hideBeard = false;

        [Header("序列帧动画集（可选）")]
        [Tooltip("一整套动画集资源（包含 Idle/Walk/Attack/Die 等）；可被多个装备共享")]
        public EquipAnimSetAsset animSet;

        /// <summary>
        /// 尝试获取序列帧 Sprite（按动画类型）
        /// </summary>
        public Sprite TryGetSequenceSprite(AnimationTypeItem animType, int rowIndex, int frameIndex)
        {
            return animSet?.TryGetSprite(animType, rowIndex, frameIndex);
        }

        /// <summary>
        /// 尝试获取序列帧 Sprite（按 Key，用于与 Animator 参数匹配）
        /// </summary>
        public Sprite TryGetSequenceSpriteByKey(string key, int rowIndex, int frameIndex)
        {
            return animSet?.TryGetSpriteByKey(key, rowIndex, frameIndex);
        }

        /// <summary>
        /// 检查是否有动画集
        /// </summary>
        public bool HasAnimSet => animSet != null;

        /// <summary>
        /// 根据方向获取对应的基础 Sprite（所有类型统一使用四向贴图）
        /// </summary>
        public Sprite GetSprite(CharacterFacing facing)
        {
            return DirectionalSpriteHelper.GetByFacing(
                facing,
                spriteSE,
                spriteSW,
                spriteNE,
                spriteNW
            );
        }

        /// <summary>
        /// 根据方向和帧变体获取 Sprite
        /// 武器：暂不区分变体，直接复用 GetSprite(facing)
        /// 非武器：支持 Up/Down 变体
        /// </summary>
        public Sprite GetSprite(CharacterFacing facing, FrameVariant variant)
        {
            var cfg = EquipTypeRegistry.Get(type);
            if (cfg != null && cfg.RenderMode == EquipRenderMode.Weapon)
                return GetSprite(facing);
            
            // 选择对应的变体贴图集
            DirectionalSpriteSet set = null;
            switch (variant)
            {
                case FrameVariant.Up:
                    set = upVariant;
                    break;
                case FrameVariant.Down:
                    set = downVariant;
                    break;
                case FrameVariant.Base:
                default:
                    break;
            }

            // 使用变体时，以 SE 是否配置作为「是否启用该变体」的标记
            if (set != null && set.se != null)
            {
                var sprite = set.GetByFacing(facing);
                if (sprite != null)
                    return sprite;
            }

            // 回退到基础贴图
            return GetSprite(facing);
        }

        /// <summary>
        /// 根据行索引获取对应的基础 Sprite (0=SE, 1=SW, 2=NE, 3=NW)
        /// </summary>
        public Sprite GetSpriteByRow(int rowIndex)
        {
            return GetSprite((CharacterFacing)rowIndex);
        }
    }
}
