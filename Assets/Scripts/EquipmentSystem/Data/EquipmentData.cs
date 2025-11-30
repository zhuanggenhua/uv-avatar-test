using UnityEngine;

namespace EquipmentSystem.Data
{
    /// <summary>
    /// 装备类型
    /// </summary>
    public enum EquipmentType
    {
        Weapon,       // 武器 - 用锚点定位
        Clothing,     // 服装 - UV 映射到躯干 (Body 层)
        Helmet,       // 头盔 - UV 映射到头部 (Head 层)，覆盖在头发/胡子之上
        Gloves,       // 手套 - 颜色替换手部像素
        Shoes         // 鞋子 - 颜色替换脚部像素
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
        public static Sprite GetByFacing(CharacterFacing facing, Sprite se, Sprite sw, Sprite ne, Sprite nw)
        {
            Sprite result;
            switch (facing)
            {
                case CharacterFacing.SouthEast: result = se; break;
                case CharacterFacing.SouthWest: result = sw; break;
                case CharacterFacing.NorthEast: result = ne; break;
                case CharacterFacing.NorthWest: result = nw; break;
                default: result = se; break;
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
        
        // 武器设置
        public AnchorType anchorType = AnchorType.RightWeapon;
        public Vector2Int selfAnchor;
        
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
            
            return DirectionalSpriteHelper.GetByFacing(facing, spriteSE, spriteSW, spriteNE, spriteNW);
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
