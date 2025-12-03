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
        Helmet, //   头盔 - UV 映射到头部 (Head 层)，覆盖在头发/胡子之上
        Clothing, //   服装 - UV 映射到躯干 (Body 层)
        Cloak, //   斗篷 - UV 映射到躯干 (Body 层)，渲染在服装前面
        Weapon, //   武器 - 用锚点定位
        Bag, //  背包 - 用锚点定位
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
            switch (facing)
            {
                case CharacterFacing.SouthEast:
                    return se;
                case CharacterFacing.SouthWest:
                    return sw;
                case CharacterFacing.NorthEast:
                    return ne;
                case CharacterFacing.NorthWest:
                    return nw;
                default:
                    return se;
            }
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
                    result = ne;
                    break;
                case CharacterFacing.NorthWest:
                    result = nw;
                    break;
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

        // 武器贴图 (单张)
        public Sprite weaponSprite;

        // 服装/头部装饰贴图 (4方向)
        public Sprite spriteSE;
        public Sprite spriteSW;
        public Sprite spriteNE;
        public Sprite spriteNW;

        [Header("循环变体（可选）")]
        [Tooltip("静止类动画使用的循环贴图（如斗篷轻微摆动）")]
        public DirectionalSpriteSet[] idleLoop;
        
        [Tooltip("移动类动画使用的循环贴图（如斗篷飘动）")]
        public DirectionalSpriteSet[] moveLoop;

        // 武器设置
        public AnchorType anchorType = AnchorType.RightWeapon;
        public Vector2Int selfAnchor;

        // 颜色替换 (手套/鞋子)
        public Color32 leftColor = new Color32(150, 100, 50, 255);
        public Color32 rightColor = new Color32(150, 100, 50, 255);
        
        [Header("头盔设置")]
        [Tooltip("戴上头盔时隐藏头发")]
        public bool hideHair = false;
        [Tooltip("戴上头盔时隐藏胡子")]
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
        /// 根据方向获取对应的 Sprite (服装/头部装饰用)
        /// </summary>
        public Sprite GetSprite(CharacterFacing facing)
        {
            // 武器只有一张贴图
            if (type == EquipmentType.Weapon)
                return weaponSprite;

            return DirectionalSpriteHelper.GetByFacing(
                facing,
                spriteSE,
                spriteSW,
                spriteNE,
                spriteNW
            );
        }

        /// <summary>
        /// 根据循环类型、方向和动画帧索引获取循环变体贴图
        /// </summary>
        public Sprite GetLoopSprite(EquipVariantLoopType loopType, CharacterFacing facing, int frameIndex)
        {
            // 武器只有一张贴图
            if (type == EquipmentType.Weapon)
                return weaponSprite;
            
            var loopArray = loopType == EquipVariantLoopType.Move ? moveLoop : idleLoop;
            
            if (loopArray == null || loopArray.Length == 0)
                return GetSprite(facing);
            
            int idx = frameIndex % loopArray.Length;
            var set = loopArray[idx];
            if (set == null)
                return GetSprite(facing);
            
            return set.GetByFacing(facing) ?? GetSprite(facing);
        }

        /// <summary>
        /// 根据行索引获取对应的 Sprite (0=SE, 1=SW, 2=NE, 3=NW)
        /// </summary>
        public Sprite GetSpriteByRow(int rowIndex)
        {
            return GetSprite((CharacterFacing)rowIndex);
        }

        /// <summary>
        /// 根据行索引和循环类型获取对应的 Sprite
        /// </summary>
        public Sprite GetLoopSpriteByRow(EquipVariantLoopType loopType, int rowIndex, int frameIndex)
        {
            return GetLoopSprite(loopType, (CharacterFacing)rowIndex, frameIndex);
        }
    }
}
