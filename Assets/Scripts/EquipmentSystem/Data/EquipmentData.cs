using UnityEngine;

namespace EquipmentSystem.Data
{
    /// <summary>
    /// 装备类型
    /// </summary>
    public enum EquipmentType
    {
        Accessory,    // 挂件（头盔、武器）- 用锚点定位
        Clothing,     // 服装（衣服）- UV 映射到躯干
        FacialDecor,  // 面部装饰（刀疑、文身等）- UV 映射到头部
        Gloves,       // 手套 - 颜色替换左右手像素
        Shoes         // 鞋子 - 颜色替换左右脚像素
    }

    /// <summary>
    /// 装备层类型 - 决定使用哪个 UV Map
    /// </summary>
    public enum EquipmentLayer
    {
        Body,   // 身体层: 衣服、手套、鞋子
        Head    // 头部层: 头盔、胡子、头发
    }
    
    /// <summary>
    /// 装备数据
    /// </summary>
    [CreateAssetMenu(fileName = "EquipmentData", menuName = "Equipment System/Equipment Data")]
    public class EquipmentData : ScriptableObject
    {
        [Header("基础")]
        public string equipmentId;
        public EquipmentType type;
        
        [Header("层级")]
        [Tooltip("装备所属层级，决定使用 Body UV Map 还是 Head UV Map")]
        public EquipmentLayer layer = EquipmentLayer.Body;
        
        [Header("贴图 - 4方向 (SE/SW/NE/NW)")]
        [Tooltip("东南方向 (必填，其他方向为空时回退到此)")]
        public Sprite spriteSE;
        [Tooltip("西南方向 (可选)")]
        public Sprite spriteSW;
        [Tooltip("东北方向 (可选)")]
        public Sprite spriteNE;
        [Tooltip("西北方向 (可选)")]
        public Sprite spriteNW;
        
        [Header("挂件设置 (Accessory)")]
        [Tooltip("锚点类型")]
        public AnchorType anchorType = AnchorType.Head;
        [Tooltip("装备自身锚点")]
        public Vector2Int selfAnchor;
        
        [Header("颜色替换 (手套/鞋子)")]
        [Tooltip("左手/左脚颜色")]
        public Color32 leftColor = new Color32(150, 100, 50, 255);
        [Tooltip("右手/右脚颜色")]
        public Color32 rightColor = new Color32(150, 100, 50, 255);
        
        [Header("渲染")]
        public int sortingOffset = 1;
        
        /// <summary>
        /// 根据方向获取对应的 Sprite
        /// </summary>
        public Sprite GetSprite(CharacterFacing facing)
        {
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
            // 回退到 SE
            return result != null ? result : spriteSE;
        }
        
        /// <summary>
        /// 根据行索引获取对应的 Sprite (0=SE, 1=SW, 2=NE, 3=NW)
        /// </summary>
        public Sprite GetSpriteByRow(int rowIndex)
        {
            return GetSprite((CharacterFacing)rowIndex);
        }
        
        // 保留旧接口以兼容
        [System.Obsolete("使用 GetSprite(CharacterFacing) 代替")]
        public Sprite GetSprite(FacingDirection dir)
        {
            if (dir == FacingDirection.Back)
                return spriteNE != null ? spriteNE : spriteSE;
            return spriteSE;
        }
    }
}
