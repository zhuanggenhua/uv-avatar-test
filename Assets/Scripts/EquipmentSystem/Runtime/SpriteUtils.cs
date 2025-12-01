using UnityEngine;

namespace EquipmentSystem
{
    /// <summary>
    /// Sprite 相关的工具方法
    /// </summary>
    public static class SpriteUtils
    {
        /// <summary>
        /// 计算 Sprite 在其 Texture 中的 UV Rect (minU, minV, maxU, maxV)
        /// 
        /// 重要: Unity 中 Sprite.texture 返回的是整张原始贴图，而不是切片后的小图！
        /// - 如果 sprite 是从 spritesheet 切出的，texture 指向整张大图
        /// - 如果 sprite 是单独的图片，texture 就是这张图片本身
        /// - 如果使用了 Sprite Atlas 打包，texture 指向打包后的图集
        /// 
        /// 因此，在 Shader 中采样时必须使用 sprite.rect 计算正确的 UV 范围，
        /// 而不能直接用 0-1 的 UV 采样整张 texture。
        /// </summary>
        /// <param name="sprite">要计算的 Sprite</param>
        /// <returns>Vector4(minU, minV, maxU, maxV)</returns>
        public static Vector4 GetUVRect(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null)
                return new Vector4(0, 0, 1, 1);  // 默认全图
            
            var tex = sprite.texture;
            var rect = sprite.rect;
            
            float minU = rect.x / tex.width;
            float minV = rect.y / tex.height;
            float maxU = (rect.x + rect.width) / tex.width;
            float maxV = (rect.y + rect.height) / tex.height;
            
            return new Vector4(minU, minV, maxU, maxV);
        }
        
        /// <summary>
        /// 获取 Sprite 的像素尺寸
        /// </summary>
        public static Vector2 GetPixelSize(Sprite sprite)
        {
            if (sprite == null) return Vector2.zero;
            return new Vector2(sprite.rect.width, sprite.rect.height);
        }
        
        /// <summary>
        /// 获取 Sprite 在 Texture 中的归一化尺寸
        /// </summary>
        public static Vector2 GetNormalizedSize(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null)
                return Vector2.one;
            
            var tex = sprite.texture;
            return new Vector2(sprite.rect.width / tex.width, sprite.rect.height / tex.height);
        }
    }
}
