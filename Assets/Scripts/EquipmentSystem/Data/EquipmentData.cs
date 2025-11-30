using UnityEngine;

namespace EquipmentSystem.Data
{
    /// <summary>
    /// 装备类型
    /// </summary>
    public enum EquipmentType
    {
        Weapon,       // 武器 - 用锚点定位，需要设置装备锚点
        Clothing,     // 服装 - UV 映射到躯干 (Body 层)
        HeadGear,     // 头部装饰 - UV 映射到头部 (Head 层)，包括头盔/胡子/头发
        Gloves,       // 手套 - 颜色替换手部像素
        Shoes         // 鞋子 - 颜色替换脚部像素
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
        
        // 武器设置
        public AnchorType anchorType = AnchorType.RightWeapon;
        public Vector2Int selfAnchor;
        public int sortingOffset = 1;
        
        // 颜色替换 (手套/鞋子)
        public Color32 leftColor = new Color32(150, 100, 50, 255);
        public Color32 rightColor = new Color32(150, 100, 50, 255);
        
        /// <summary>
        /// 根据方向获取对应的 Sprite (服装/头部装饰用)
        /// </summary>
        public Sprite GetSprite(CharacterFacing facing)
        {
            // 武器只有一张贴图
            if (type == EquipmentType.Weapon)
                return weaponSprite;
            
            Sprite result = null;
            switch (facing)
            {
                case CharacterFacing.SouthEast:
                    result = spriteSE;
                    break;
                case CharacterFacing.SouthWest:
                    result = spriteSW;
                    break;
                case CharacterFacing.NorthEast:
                    result = spriteNE;
                    break;
                case CharacterFacing.NorthWest:
                    result = spriteNW;
                    break;
            }
            return result != null ? result : spriteSE;
        }
        
        /// <summary>
        /// 根据行索引获取对应的 Sprite (0=SE, 1=SW, 2=NE, 3=NW)
        /// </summary>
        public Sprite GetSpriteByRow(int rowIndex)
        {
            return GetSprite((CharacterFacing)rowIndex);
        }
    }
}
