using UnityEngine;

namespace EquipmentSystem.Data
{
    /// <summary>
    /// 装备类型
    /// </summary>
    public enum EquipmentType
    {
        Accessory,  // 挂件（头盔、武器）- 用锚点定位
        Clothing,   // 服装（衣服）- 2x3像素映射到身体
        Gloves,     // 手套 - 颜色替换左右手像素
        Shoes       // 鞋子 - 颜色替换左右脚像素
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
        
        [Header("贴图 (挂件/服装用)")]
        public Sprite frontSprite;
        public Sprite backSprite;
        
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
        
        public Sprite GetSprite(FacingDirection dir)
        {
            if (dir == FacingDirection.Back && backSprite != null)
                return backSprite;
            return frontSprite;
        }
    }
}
