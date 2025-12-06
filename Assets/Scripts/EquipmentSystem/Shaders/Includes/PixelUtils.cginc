// ============================================================================
// PixelUtils.cginc - 像素画通用工具
// ============================================================================

#ifndef PIXEL_UTILS_INCLUDED
#define PIXEL_UTILS_INCLUDED

// 像素风格 alpha 阈值
static const float PIXEL_CUTOFF = 0.5;

// 将 0~1 的局部 UV 转换为纹理上的实际 UV（根据 Sprite Rect 映射）
// rect: (minU, minV, maxU, maxV)
float2 TransformUV(float2 uv, float4 rect)
{
    return float2(
        lerp(rect.x, rect.z, uv.x),
        lerp(rect.y, rect.w, uv.y)
    );
}

// 采样贴图，alpha 大于阈值才算有效
bool TrySampleTexture(float2 uv, float4 rect, sampler2D tex, out fixed3 outColor)
{
    float2 coord = TransformUV(uv, rect);
    fixed4 c = tex2D(tex, coord);
    outColor = c.rgb;
    return c.a > PIXEL_CUTOFF;
}

// 采样贴图（返回完整 RGBA）
bool TrySampleTextureRGBA(float2 uv, float4 rect, sampler2D tex, out fixed4 outColor)
{
    float2 coord = TransformUV(uv, rect);
    outColor = tex2D(tex, coord);
    return outColor.a > PIXEL_CUTOFF;
}

#endif // PIXEL_UTILS_INCLUDED
