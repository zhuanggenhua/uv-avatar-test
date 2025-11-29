using System;
using System.Collections.Generic;
using UnityEngine;

namespace EquipmentSystem.Data
{
    /// <summary>
    /// 单方向模板数据
    /// </summary>
    [Serializable]
    public class DirectionTemplate
    {
        [Tooltip("该方向的像素列表（从图片有色区域生成）")]
        public List<Vector2Int> pixels = new List<Vector2Int>();
        
        /// <summary>
        /// 获取包围盒
        /// </summary>
        public RectInt GetBounds()
        {
            if (pixels == null || pixels.Count == 0)
                return new RectInt(0, 0, 0, 0);
            
            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;
            
            foreach (var p in pixels)
            {
                minX = Mathf.Min(minX, p.x);
                maxX = Mathf.Max(maxX, p.x);
                minY = Mathf.Min(minY, p.y);
                maxY = Mathf.Max(maxY, p.y);
            }
            
            return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }
        
        /// <summary>
        /// 像素数
        /// </summary>
        public int PixelCount => pixels?.Count ?? 0;
        
        /// <summary>
        /// 是否有效
        /// </summary>
        public bool IsValid => pixels != null && pixels.Count > 0;
    }
    
    /// <summary>
    /// 装备模板 - 定义装备贴图的标准布局
    /// 一套装备包含4个方向的模板（SE/SW/NE/NW）
    /// </summary>
    [CreateAssetMenu(fileName = "EquipmentTemplate", menuName = "Equipment System/Equipment Template")]
    public class EquipmentTemplate : ScriptableObject
    {
        [Header("贴图尺寸")]
        [Tooltip("装备贴图的标准尺寸")]
        public Vector2Int textureSize = new Vector2Int(32, 32);
        
        [Header("四方向模板")]
        [Tooltip("东南方向 (SE) - Row 0")]
        public DirectionTemplate SE = new DirectionTemplate();
        
        [Tooltip("西南方向 (SW) - Row 1")]
        public DirectionTemplate SW = new DirectionTemplate();
        
        [Tooltip("东北方向 (NE) - Row 2")]
        public DirectionTemplate NE = new DirectionTemplate();
        
        [Tooltip("西北方向 (NW) - Row 3")]
        public DirectionTemplate NW = new DirectionTemplate();
        
        /// <summary>
        /// 根据行索引获取对应方向的模板
        /// </summary>
        public DirectionTemplate GetTemplate(int rowIndex)
        {
            switch (rowIndex)
            {
                case 0: return SE;
                case 1: return SW;
                case 2: return NE;
                case 3: return NW;
                default: return SE;
            }
        }
        
        /// <summary>
        /// 根据 CharacterFacing 获取模板
        /// </summary>
        public DirectionTemplate GetTemplate(CharacterFacing facing)
        {
            return GetTemplate((int)facing);
        }
        
        /// <summary>
        /// 获取指定方向第 index 个像素的 UV 坐标
        /// </summary>
        public Vector2 GetUV(int rowIndex, int pixelIndex)
        {
            var template = GetTemplate(rowIndex);
            if (template == null || pixelIndex < 0 || pixelIndex >= template.pixels.Count)
                return Vector2.zero;
            
            var pixel = template.pixels[pixelIndex];
            return new Vector2(
                (pixel.x + 0.5f) / textureSize.x,
                (pixel.y + 0.5f) / textureSize.y
            );
        }
        
#if UNITY_EDITOR
        /// <summary>
        /// 从 Sprite 自动生成单方向模板
        /// </summary>
        public static void GenerateFromSprite(Sprite sprite, DirectionTemplate template, int alphaThreshold = 1)
        {
            template.pixels.Clear();
            
            if (sprite == null || sprite.texture == null) return;
            
            var tex = sprite.texture;
            if (!tex.isReadable)
            {
                Debug.LogWarning($"[EquipmentTemplate] 贴图 {tex.name} 不可读，请在导入设置中启用 Read/Write");
                return;
            }
            
            var rect = sprite.rect;
            var pixels = tex.GetPixels32();
            
            int startX = Mathf.FloorToInt(rect.x);
            int startY = Mathf.FloorToInt(rect.y);
            int width = Mathf.FloorToInt(rect.width);
            int height = Mathf.FloorToInt(rect.height);
            
            // 从左到右、从上到下扫描有色像素
            for (int y = height - 1; y >= 0; y--)  // 从上到下
            {
                for (int x = 0; x < width; x++)  // 从左到右
                {
                    int texX = startX + x;
                    int texY = startY + y;
                    
                    if (texX >= 0 && texX < tex.width && texY >= 0 && texY < tex.height)
                    {
                        var c = pixels[texY * tex.width + texX];
                        // 有色像素（包括黑色轮廓）
                        if (c.a >= alphaThreshold)
                        {
                            // 存储相对于 sprite 左下角的坐标
                            template.pixels.Add(new Vector2Int(x, y));
                        }
                    }
                }
            }
            
            Debug.Log($"[EquipmentTemplate] 从 {sprite.name} 生成了 {template.pixels.Count} 个像素");
        }
        
        /// <summary>
        /// 从4张 Sprite 生成完整模板
        /// </summary>
        public void GenerateFromSprites(Sprite spriteSE, Sprite spriteSW, Sprite spriteNE, Sprite spriteNW)
        {
            GenerateFromSprite(spriteSE, SE);
            GenerateFromSprite(spriteSW ?? spriteSE, SW);  // 为空时使用 SE
            GenerateFromSprite(spriteNE ?? spriteSE, NE);
            GenerateFromSprite(spriteNW ?? spriteSE, NW);
            
            // 自动设置贴图尺寸
            if (spriteSE != null)
            {
                textureSize = new Vector2Int(
                    Mathf.FloorToInt(spriteSE.rect.width),
                    Mathf.FloorToInt(spriteSE.rect.height)
                );
            }
        }
#endif
    }
}
