// ============================================================================
// PixelShadow.cginc - 像素级阴影系统
// ============================================================================
// 基于脚部位置的四种阴影模式：
// Mode 0: 地面状态 - 脚在基线上，基线有色像素形成阴影基础
// Mode 1: 离地渲染 - 脚离地1-2像素，左脚到右脚范围形成阴影
// Mode 2: 空中模式 - 脚离地3-9像素，缺角4x3矩形阴影
// Mode 3: 完全离地 - 脚离地10+像素，中心十字形阴影
// ============================================================================

#ifndef PIXEL_SHADOW_INCLUDED
#define PIXEL_SHADOW_INCLUDED

// 阴影参数（需要在主 Shader 中声明）
// float _ShadowMode;       // 阴影模式 0-3
// float _ShadowLeftX;      // Mode 1 左边界
// float _ShadowRightX;     // Mode 1 右边界  
// float _ShadowCenterX;    // Mode 2/3 中心位置
// float _ShadowBaseY;      // 基线Y坐标（归一化）
// fixed4 _ShadowColor;
// float _ShadowEnabled;

// 帧尺寸（像素），由 C# 通过 _FrameSize 属性传入（x=宽，y=高）
float2 _FrameSize;

// 检查UV是否在像素格子内
bool IsInPixel(float2 uv, int pixelX, int pixelY)
{
    float2 texelSize = 1.0 / _FrameSize;          // (1/width, 1/height)
    float2 pixelMin = float2(pixelX, pixelY) * texelSize;
    float2 pixelMax = pixelMin + texelSize;
    return uv.x >= pixelMin.x && uv.x < pixelMax.x && 
           uv.y >= pixelMin.y && uv.y < pixelMax.y;
}

// Mode 1: 离地渲染阴影
// 从左脚到右脚的范围，上下左右各扩1格
bool IsOffGroundShadow(float2 uv, float leftX, float rightX, float baseY)
{
    int basePixelY = (int)(baseY * _FrameSize.y);
    int leftPixelX = (int)(leftX * _FrameSize.x);
    int rightPixelX = (int)(rightX * _FrameSize.x);
    
    // 检查是否在扩展后的矩形范围内
    for (int x = leftPixelX - 1; x <= rightPixelX + 1; x++)
    {
        for (int y = basePixelY - 1; y <= basePixelY + 1; y++)
        {
            if (IsInPixel(uv, x, y)) return true;
        }
    }
    
    return false;
}

// Mode 2: 空中模式阴影
// 以下方脚的特定边缘为基准，形成缺四角的4x3矩形
bool IsAirShadow(float2 uv, float centerX, float baseY)
{
    int basePixelY = (int)(baseY * _FrameSize.y);
    int centerPixelX = (int)(centerX * _FrameSize.x);
    
    // 4x3矩形，缺四角
    for (int dx = -1; dx <= 2; dx++)
    {
        for (int dy = -1; dy <= 1; dy++)
        {
            // 跳过四个角
            if ((dx == -1 || dx == 2) && (dy == -1 || dy == 1))
                continue;
                
            int px = centerPixelX + dx;
            int py = basePixelY + dy;
            
            if (IsInPixel(uv, px, py)) return true;
        }
    }
    
    return false;
}

// Mode 3: 完全离地阴影
// 十字形：中心点上下左右各扩1格
bool IsHighAirShadow(float2 uv, float centerX, float baseY)
{
    int basePixelY = (int)(baseY * _FrameSize.y);
    int centerPixelX = (int)(centerX * _FrameSize.x);
    
    // 中心点
    if (IsInPixel(uv, centerPixelX, basePixelY)) return true;
    // 上下左右
    if (IsInPixel(uv, centerPixelX, basePixelY + 1)) return true;
    if (IsInPixel(uv, centerPixelX, basePixelY - 1)) return true;
    if (IsInPixel(uv, centerPixelX - 1, basePixelY)) return true;
    if (IsInPixel(uv, centerPixelX + 1, basePixelY)) return true;
    
    return false;
}

// 主阴影采样函数（Mode1~3）
// Mode0 的地面阴影在主 Shader 中单独实现，这里不再处理
fixed4 SamplePixelShadow(
    float2 uv,
    float shadowMode,
    float shadowLeftX,
    float shadowRightX,
    float shadowCenterX,
    float shadowBaseY,
    fixed4 shadowColor,
    float shadowEnabled)
{
    if (shadowEnabled < 0.5 || shadowMode < 0.5)
        return fixed4(0, 0, 0, 0);

    bool inShadow = false;

    if (shadowMode < 1.5)
    {
        // Mode 1: 离地渲染
        inShadow = IsOffGroundShadow(uv, shadowLeftX, shadowRightX, shadowBaseY);
    }
    else if (shadowMode < 2.5)
    {
        // Mode 2: 空中模式
        inShadow = IsAirShadow(uv, shadowCenterX, shadowBaseY);
    }
    else
    {
        // Mode 3: 完全离地
        inShadow = IsHighAirShadow(uv, shadowCenterX, shadowBaseY);
    }

    return inShadow ? shadowColor : fixed4(0, 0, 0, 0);
}

#endif // PIXEL_SHADOW_INCLUDED
