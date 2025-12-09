// ============================================================================
// 装备系统 Shader - 像素画优化版
// ============================================================================
// 
// 核心特性：
// 1. 双层 UV Map 系统（身体层 + 头部层）
// 2. 支持主手 + 副手双武器渲染
// 3. 像素画优化：使用 step 函数代替 if 分支，保持颜色锐利
// 4. 支持装备显示/隐藏、颜色替换、深度控制
// 5. 使用 [branch] 进行分支预测优化
//
// UV Map 颜色通道说明：
// - R/G 通道：局部 UV 坐标（用于采样装备纹理）
// - B 通道：部位 ID（用于识别身体部位）
// - A 通道：遮罩/优先级标记 
// ============================================================================
Shader "EquipmentSystem/EquipmentUV"
{
    Properties
    {
        _MainTex ("Base Sprite", 2D) = "white" {}
        
        [Header(Dual UV Maps)]
        _BodyUVMap ("Body UV Map", 2D) = "black" {}
        _HeadUVMap ("Head UV Map", 2D) = "black" {}
        
        [Header(Body Layer Textures)]
        _ClothTex ("Clothing Texture", 2D) = "white" {}
        _PantsTex ("Pants Texture", 2D) = "white" {}
        _CloakTex ("Cloak Texture", 2D) = "white" {}
        _BagTex ("Bag Texture", 2D) = "white" {}
        
        [Header(Head Layer Textures)]
        _HairTex ("Hair Texture", 2D) = "white" {}
        _FaceAccessoryTex ("Face Accessory Texture", 2D) = "white" {}
        _BeardTex ("Beard Texture", 2D) = "white" {}
        _HelmetTex ("Helmet Texture", 2D) = "white" {}
        
        [Header(Glove Colors)]
        [HDR] _LeftHandColor ("Left Hand Color", Color) = (0.6, 0.4, 0.2, 1)
        [HDR] _RightHandColor ("Right Hand Color", Color) = (0.6, 0.4, 0.2, 1)
        
        [Header(Shoe Colors)]
        [HDR] _LeftFootColor ("Left Foot Color", Color) = (0.3, 0.2, 0.1, 1)
        [HDR] _RightFootColor ("Right Foot Color", Color) = (0.3, 0.2, 0.1, 1)
        
        [Header(Eye Colors)]
        [HDR] _LeftEyeColor ("Left Eye Color", Color) = (0.6, 0.2, 0.8, 1)
        [HDR] _RightEyeColor ("Right Eye Color", Color) = (0.6, 0.2, 0.8, 1)
        
        [Header(Weapon Main Hand)]
        _CharFrameRect ("Character Frame Rect", Vector) = (0,0,1,1)
        _Weapon0Tex ("Main Hand Weapon Texture", 2D) = "white" {}
        _Weapon0Rect ("Main Hand Weapon Rect", Vector) = (0,0,1,1)
        _Weapon0AnchorFrameUV ("Weapon0 Anchor Frame UV", Vector) = (0.5, 0.5, 0, 0)
        _Weapon0RotCosSin ("Weapon0 Rot Cos/Sin", Vector) = (1, 0, 0, 0)
        _Weapon0FlipX ("Main Hand Flip X", Float) = 0
        _Weapon0DepthMode ("Main Hand Depth Mode", Float) = 0
        _Weapon0Enabled ("Enable Main Hand Weapon", Float) = 0
        _Weapon0HandInFront ("Main Hand: Hand In Front", Float) = 1
        
        [Header(Weapon Off Hand)]
        _Weapon1Tex ("Off Hand Weapon Texture", 2D) = "white" {}
        _Weapon1Rect ("Off Hand Weapon Rect", Vector) = (0,0,1,1)
        _Weapon1AnchorFrameUV ("Weapon1 Anchor Frame UV", Vector) = (0.5, 0.5, 0, 0)
        _Weapon1RotCosSin ("Weapon1 Rot Cos/Sin", Vector) = (1, 0, 0, 0)
        _Weapon1FlipX ("Off Hand Flip X", Float) = 0
        _Weapon1DepthMode ("Off Hand Depth Mode", Float) = 0
        _Weapon1Enabled ("Enable Off Hand Weapon", Float) = 0
        _Weapon1HandInFront ("Off Hand: Hand In Front", Float) = 1
        
        [Header(Enable Layers)]
        _EnableHair ("Enable Hair", Float) = 0
        _EnableFaceAccessory ("Enable Face Accessory", Float) = 0
        _EnableBeard ("Enable Beard", Float) = 0
        _EnableHelmet ("Enable Helmet", Float) = 0
        _EnableCloth ("Enable Clothing", Float) = 0
        _EnableCloak ("Enable Cloak", Float) = 0
        _EnableBag ("Enable Bag", Float) = 0
        _EnableGloves ("Enable Gloves", Float) = 0
        _EnableShoes ("Enable Shoes", Float) = 0
        _EnableLeftEye ("Enable Left Eye", Float) = 0
        _EnableRightEye ("Enable Right Eye", Float) = 0
        _BodyInFront ("Body In Front", Float) = 0
        _BodyInEast ("Body Facing East", Float) = 1
        
        [Header(Eye Decoration)]
        _EyeDecoMode ("Eye Deco Mode", Float) = 0
        [HDR] _EyeDecoColor ("Eye Deco Color", Color) = (0.3, 0.2, 0.2, 1)
        _LeftEyePos ("Left Eye Pos", Vector) = (0, 0, 0, 0)
        _RightEyePos ("Right Eye Pos", Vector) = (0, 0, 0, 0)
        
        [Header(Debug)]
        // 调试模式：0=关闭，1=身体层区域，2=头部层区域，3=装备采样结果，4=UVMap原始UV，5=顶点UV
        _DebugMode ("Debug Mode", Float) = 0
        
        [Header(Shadow)]
        _ShadowEnabled ("Enable Shadow", Float) = 1
        _ShadowColor ("Shadow Color", Color) = (0,0,0,0.6)
        _ShadowMode ("Shadow Mode", Float) = 0
        _ShadowLeftX ("Shadow Left X", Float) = 0
        _ShadowRightX ("Shadow Right X", Float) = 0
        _ShadowCenterX ("Shadow Center X", Float) = 0.5
        _ShadowBaseY ("Shadow Base Y", Float) = 0.25

        [Header(Hit Outline)]
        _HitOutline ("Hit Outline", Float) = 0
        [HDR] _HitOutlineColor ("Hit Outline Color", Color) = (0.7059, 0.0353, 0.0353, 1)
        
        [Header(Skin Palette)]
        _SkinPaletteEnabled ("Skin Palette Enabled", Float) = 0
        _SkinKeyTex ("Skin Key Map", 2D) = "white" {}
        _SkinPaletteTex ("Skin Palette Map", 2D) = "white" {}
        
        _Color ("Tint", Color) = (1,1,1,1)
    }
    
    SubShader
    {
        Tags 
        { 
            "Queue" = "Transparent" 
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
        }
        
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        
        // 单 Pass：角色本体 + 阴影（SpriteRenderer 只支持单 Pass）
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            
            // ============================================================================
            // 内联 PixelUtils（原 PixelUtils.cginc）
            // - TransformUV: 将 0~1 的局部 UV 映射到大贴图中的 Sprite Rect
            // ============================================================================
            float2 TransformUV(float2 uv, float4 rect)
            {
                return float2(
                    lerp(rect.x, rect.z, uv.x),
                    lerp(rect.y, rect.w, uv.y)
                );
            }
            
            // ============================================================================
            // 结构体定义
            // ============================================================================
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };
            
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };
            
            sampler2D _MainTex;
            sampler2D _BodyUVMap;
            sampler2D _HeadUVMap;
            sampler2D _ClothTex;
            sampler2D _PantsTex;
            sampler2D _CloakTex;
            sampler2D _BagTex;
            sampler2D _HairTex;
            sampler2D _FaceAccessoryTex;
            sampler2D _BeardTex;
            sampler2D _HelmetTex;
            sampler2D _Weapon0Tex;
            sampler2D _Weapon1Tex;
            
            // ============================================================
            // 每个装备贴图在纹理中的 Sprite Rect（minU, minV, maxU, maxV）
            // ============================================================
            float4 _ClothRect;
            float4 _PantsRect;
            float4 _CloakRect;
            float4 _BagRect;
            float4 _HairRect;
            float4 _FaceAccessoryRect;
            float4 _BeardRect;
            float4 _HelmetRect;
            float4 _CharFrameRect;      // 当前角色帧在 _MainTex 中的 Rect
            
            // 主手武器参数
            float4 _Weapon0Rect;
            float4 _Weapon0AnchorFrameUV;
            float4 _Weapon0RotCosSin;
            float _Weapon0FlipX;
            float _Weapon0DepthMode;
            float _Weapon0Enabled;
            float _Weapon0HandInFront;
            
            // 副手武器参数
            float4 _Weapon1Rect;
            float4 _Weapon1AnchorFrameUV;
            float4 _Weapon1RotCosSin;
            float _Weapon1FlipX;
            float _Weapon1DepthMode;
            float _Weapon1Enabled;
            float _Weapon1HandInFront;
            
            fixed4 _LeftHandColor;
            fixed4 _RightHandColor;
            fixed4 _LeftFootColor;
            fixed4 _RightFootColor;
            fixed4 _LeftEyeColor;
            fixed4 _RightEyeColor;
            
            float _EnableHair;
            float _EnableFaceAccessory;
            float _EnableBeard;
            float _EnableHelmet;
            float _EnableCloth;
            float _EnablePants;
            float _EnableCloak;
            float _EnableBag;
            float _EnableGloves;
            float _EnableShoes;
            float _EnableLeftEye;
            float _EnableRightEye;
            float _BodyInFront;
            float _BodyInEast;
            float _DebugMode;
            
            // 眼部装饰参数
            float _EyeDecoMode;
            fixed4 _EyeDecoColor;
            float2 _LeftEyePos;
            float2 _RightEyePos;
            
            // 阴影参数
            float _ShadowEnabled;
            fixed4 _ShadowColor;
            float _ShadowMode;
            float _ShadowLeftX;
            float _ShadowRightX;
            float _ShadowCenterX;
            float _ShadowBaseY;
            float2 _FrameSize;          // 帧尺寸（像素），用于阴影判断

            // 受击描边
            float _HitOutline;
            fixed4 _HitOutlineColor;

            // 肤色调色板（Key/Palette 查表式）
            sampler2D _SkinKeyTex;
            sampler2D _SkinPaletteTex;
            float _SkinPaletteEnabled;

            fixed4 _Color;
            
            // TransformUV 已定义在 PixelUtils.cginc 中
            // 像素风格：将 alpha 大于阈值的像素视为实心像素
            static const float CUTOFF = 0.5;

            // 最终像素来源 ID，用于受击描边只对真实来源执行一次描边检测
            #define SRC_NONE     0
            #define SRC_MAIN     1
            #define SRC_CLOAK    2
            #define SRC_HELMET   3
            #define SRC_FACE     4
            #define SRC_BEARD    5
            #define SRC_HAIR     6
            #define SRC_WEAPON0  7
            #define SRC_WEAPON1  8
            #define SRC_OTHER    9
            #define SRC_BAG     10
            
            // Body Part ID 定义 (对应 B 通道值)
            // 0.0        = 非换装区域
            // 0.1 (25)   = Head (面部装饰)
            // 0.2 (51)   = Torso (服装)
            // 0.3 (76)   = LeftEye (左眼)
            // 0.35(89)   = RightEye (右眼)
            // 0.4 (102)  = LeftHand (左手套)
            // 0.5 (127)  = RightHand (右手套)
            // 0.6 (153)  = LeftFoot (左鞋)
            // 0.7 (178)  = RightFoot (右鞋)
            
            #define ID_NONE      0.0
            #define ID_HEAD      0.1
            #define ID_TORSO     0.2
            #define ID_LEFTEYE   0.3
            #define ID_RIGHTEYE  0.35
            #define ID_LEFTHAND  0.4
            #define ID_RIGHTHAND 0.5
            #define ID_LEFTFOOT  0.6
            #define ID_RIGHTFOOT 0.7
            
            // 判断 ID 是否在范围内
            bool IsPartID(float id, float target)
            {
                // 使用更小的容差，避免相邻 ID 区间重叠（例如 0.30 和 0.35）
                return abs(id - target) < 0.025;
            }

            // 判断颜色是否接近黑色，用于识别描边/黑块
            // 与编辑器侧 DetectConfig.IsOutline 保持思路一致：使用 RGB 之和阈值（默认约 80）
            bool IsNearBlack(fixed3 rgb)
            {
                // rgb 为 0~1，sumRGB 相当于 (r+g+b)/255
                float sumRGB = rgb.r + rgb.g + rgb.b;
                // 80 / 255 ≈ 0.314：只有非常接近黑色的像素才视为描边
                return sumRGB < (80.0 / 255.0);
            }

            // 颜色近似相等（用于判断当前像素是否来自某一装备纹理）
            bool ColorApproxEqual(fixed3 a, fixed3 b, float eps)
            {
                return abs(a.r - b.r) < eps
                    && abs(a.g - b.g) < eps
                    && abs(a.b - b.b) < eps;
            }

            // 判断某个纹理采样是否代表当前像素的可见黑色描边
            bool IsVisibleOutlineFromColor(fixed4 sampleColor, fixed3 finalRGB)
            {
                if (sampleColor.a <= CUTOFF)
                    return false;
                if (!IsNearBlack(sampleColor.rgb))
                    return false;
                return ColorApproxEqual(sampleColor.rgb, finalRGB, 0.01);
            }

            fixed4 SampleMainTexAtFrameUV(float2 frameUV, float2 frameMin, float2 frameSizeUV)
            {
                float2 uv = frameMin + frameSizeUV * frameUV;
                return tex2D(_MainTex, uv);
            }

            bool IsMainTexOutlineAtFrameUV(float2 frameUV, float2 frameMin, float2 frameSizeUV)
            {
                fixed4 c = SampleMainTexAtFrameUV(frameUV, frameMin, frameSizeUV);
                if (c.a <= CUTOFF)
                    return false;
                if (!IsNearBlack(c.rgb))
                    return false;

                float2 step = 1.0 / _FrameSize;

                float2 n;
                fixed4 nc;

                n = frameUV + float2(step.x, 0);
                if (n.x < 0 || n.x > 1 || n.y < 0 || n.y > 1)
                    return true;
                nc = SampleMainTexAtFrameUV(n, frameMin, frameSizeUV);
                if (nc.a <= CUTOFF)
                    return true;

                n = frameUV + float2(-step.x, 0);
                if (n.x < 0 || n.x > 1 || n.y < 0 || n.y > 1)
                    return true;
                nc = SampleMainTexAtFrameUV(n, frameMin, frameSizeUV);
                if (nc.a <= CUTOFF)
                    return true;

                n = frameUV + float2(0, step.y);
                if (n.x < 0 || n.x > 1 || n.y < 0 || n.y > 1)
                    return true;
                nc = SampleMainTexAtFrameUV(n, frameMin, frameSizeUV);
                if (nc.a <= CUTOFF)
                    return true;

                n = frameUV + float2(0, -step.y);
                if (n.x < 0 || n.x > 1 || n.y < 0 || n.y > 1)
                    return true;
                nc = SampleMainTexAtFrameUV(n, frameMin, frameSizeUV);
                if (nc.a <= CUTOFF)
                    return true;

                return false;
            }
            
            // 预计算的部位ID结构体
            struct PartIDs
            {
                bool isTorso;
                bool isLeftHand;
                bool isRightHand;
                bool isLeftFoot;
                bool isRightFoot;
                bool isLeftEye;
                bool isRightEye;
                bool isHead;
            };
            
            // 初始化部位ID判断
            PartIDs ComputePartIDs(float bodyPartID, float headPartID)
            {
                PartIDs p;
                p.isTorso     = IsPartID(bodyPartID, ID_TORSO);
                p.isLeftHand  = IsPartID(bodyPartID, ID_LEFTHAND);
                p.isRightHand = IsPartID(bodyPartID, ID_RIGHTHAND);
                p.isLeftFoot  = IsPartID(bodyPartID, ID_LEFTFOOT);
                p.isRightFoot = IsPartID(bodyPartID, ID_RIGHTFOOT);
                p.isLeftEye   = IsPartID(bodyPartID, ID_LEFTEYE);
                p.isRightEye  = IsPartID(bodyPartID, ID_RIGHTEYE);
                p.isHead      = IsPartID(headPartID, ID_HEAD);
                return p;
            }
            
            // ============================================================================
            // 装备采样
            // ============================================================================
            // 通用贴图采样：采样成功返回 true 并写入颜色
            bool TrySampleEquip(float2 uv, float4 rect, sampler2D tex, out fixed3 outColor)
            {
                float2 coord = TransformUV(uv, rect);
                fixed4 c = tex2D(tex, coord);
                outColor = c.rgb;
                if (c.a > CUTOFF)
                    return true;
                return false;
            }

            // 通用贴图采样（保留 alpha），用于受击描边检测
            bool TrySampleEquipFull(float2 uv, float4 rect, sampler2D tex, out fixed4 outColor)
            {
                float2 coord = TransformUV(uv, rect);
                fixed4 c = tex2D(tex, coord);
                outColor = c;
                return c.a > CUTOFF;
            }

            // 判断给定局部 UV 下，某装备贴图是否在当前像素提供了“可见黑色轮廓边缘”
            // 要求：
            // 1）当前像素来自该装备，且是可见的近黑色像素
            // 2）其四邻域中至少有一个是“非装备像素”（透明或越界）
            bool IsEquipOutlineAtUVLocal(float2 uvLocal, float4 rect, sampler2D tex, fixed3 finalRGB)
            {
                fixed4 c;
                if (!TrySampleEquipFull(uvLocal, rect, tex, c))
                    return false;

                // 先确认当前像素本身是可见黑描边且颜色来自该装备
                if (!IsVisibleOutlineFromColor(c, finalRGB))
                    return false;

                // 再检查四邻域是否有“非装备像素”，只有这种才算真正的轮廓边缘
                float2 step = 1.0 / _FrameSize;
                float2 n;
                fixed4 nc;

                // 右邻
                n = uvLocal + float2(step.x, 0);
                if (n.x < 0 || n.x > 1 || n.y < 0 || n.y > 1)
                    return true;
                if (!TrySampleEquipFull(n, rect, tex, nc) || nc.a <= CUTOFF)
                    return true;

                // 左邻
                n = uvLocal + float2(-step.x, 0);
                if (n.x < 0 || n.x > 1 || n.y < 0 || n.y > 1)
                    return true;
                if (!TrySampleEquipFull(n, rect, tex, nc) || nc.a <= CUTOFF)
                    return true;

                // 上邻
                n = uvLocal + float2(0, step.y);
                if (n.x < 0 || n.x > 1 || n.y < 0 || n.y > 1)
                    return true;
                if (!TrySampleEquipFull(n, rect, tex, nc) || nc.a <= CUTOFF)
                    return true;

                // 下邻
                n = uvLocal + float2(0, -step.y);
                if (n.x < 0 || n.x > 1 || n.y < 0 || n.y > 1)
                    return true;
                if (!TrySampleEquipFull(n, rect, tex, nc) || nc.a <= CUTOFF)
                    return true;

                // 四个方向都还是该装备的实心像素，则视为内部像素，不算描边
                return false;
            }

            // 前向声明：主手 / 副手武器采样函数（在下方实现）
            bool TrySampleWeapon0(float2 mainUV, out fixed4 outColor);
            bool TrySampleWeapon1(float2 mainUV, out fixed4 outColor);

            // 在帧内 UV 空间下，判断武器像素是否处于自身轮廓边缘
            // weaponIndex: 0=主手, 1=副手
            bool IsWeaponOutlineAtFrameUV(float2 frameUV, float2 frameMin, float2 frameSizeUV, int weaponIndex)
            {
                float2 step = 1.0 / _FrameSize;
                float2 nFrameUV;
                float2 nUV;
                fixed4 c;

                // 右邻
                nFrameUV = frameUV + float2(step.x, 0);
                if (nFrameUV.x < 0 || nFrameUV.x > 1 || nFrameUV.y < 0 || nFrameUV.y > 1)
                    return true;
                nUV = frameMin + frameSizeUV * nFrameUV;
                if (weaponIndex == 0)
                {
                    if (!TrySampleWeapon0(nUV, c) || c.a <= CUTOFF)
                        return true;
                }
                else
                {
                    if (!TrySampleWeapon1(nUV, c) || c.a <= CUTOFF)
                        return true;
                }

                // 左邻
                nFrameUV = frameUV + float2(-step.x, 0);
                if (nFrameUV.x < 0 || nFrameUV.x > 1 || nFrameUV.y < 0 || nFrameUV.y > 1)
                    return true;
                nUV = frameMin + frameSizeUV * nFrameUV;
                if (weaponIndex == 0)
                {
                    if (!TrySampleWeapon0(nUV, c) || c.a <= CUTOFF)
                        return true;
                }
                else
                {
                    if (!TrySampleWeapon1(nUV, c) || c.a <= CUTOFF)
                        return true;
                }

                // 上邻
                nFrameUV = frameUV + float2(0, step.y);
                if (nFrameUV.x < 0 || nFrameUV.x > 1 || nFrameUV.y < 0 || nFrameUV.y > 1)
                    return true;
                nUV = frameMin + frameSizeUV * nFrameUV;
                if (weaponIndex == 0)
                {
                    if (!TrySampleWeapon0(nUV, c) || c.a <= CUTOFF)
                        return true;
                }
                else
                {
                    if (!TrySampleWeapon1(nUV, c) || c.a <= CUTOFF)
                        return true;
                }

                // 下邻
                nFrameUV = frameUV + float2(0, -step.y);
                if (nFrameUV.x < 0 || nFrameUV.x > 1 || nFrameUV.y < 0 || nFrameUV.y > 1)
                    return true;
                nUV = frameMin + frameSizeUV * nFrameUV;
                if (weaponIndex == 0)
                {
                    if (!TrySampleWeapon0(nUV, c) || c.a <= CUTOFF)
                        return true;
                }
                else
                {
                    if (!TrySampleWeapon1(nUV, c) || c.a <= CUTOFF)
                        return true;
                }

                // 四个方向都有武器实心像素，则为内部像素，不是描边
                return false;
            }

            // 组合武器受击描边条件：颜色来自该武器的近黑色像素，且位于该武器自身的轮廓边缘
            bool IsWeaponHitOutline(
                int weaponIndex,
                bool hasWeapon,
                fixed4 weaponColor,
                float2 frameUV,
                float2 frameMin,
                float2 frameSizeUV,
                fixed3 finalRGB)
            {
                if (!hasWeapon)
                    return false;

                if (!IsVisibleOutlineFromColor(weaponColor, finalRGB))
                    return false;

                return IsWeaponOutlineAtFrameUV(frameUV, frameMin, frameSizeUV, weaponIndex);
            }

            // 通用武器采样函数（支持主手/副手）
            // 说明：
            // - anchorFrameUV.xy：角色帧内的手点 UV（0~1），由 C# 根据 AnchorPoint.position/frameSize 计算
            // - anchorFrameUV.zw：武器贴图中的"虚拟左手"局部 UV（0~1），作为旋转/镜像的 pivot
            // - rotCosSin：武器局部坐标的旋转（cos,sin），围绕虚拟左手进行旋转
            // - flipX：是否对武器贴图做"绕虚拟左手的水平镜像"。
            bool TrySampleWeaponGeneric(float2 mainUV, sampler2D weaponTex, float4 weaponRect,
                float4 anchorFrameUV, float4 rotCosSin, float flipX, float enabled, out fixed4 outColor)
            {
                outColor = 0;
                if (enabled < 0.5)
                    return false;

                // 将 mainUV 映射到当前帧的局部 UV（0~1，左下角为原点）
                float2 frameMin = _CharFrameRect.xy;
                float2 frameMax = _CharFrameRect.zw;
                float2 frameSize = frameMax - frameMin;
                if (frameSize.x <= 0.0001 || frameSize.y <= 0.0001)
                    return false;

                float2 frameUV = (mainUV - frameMin) / frameSize;
                if (frameUV.x < 0 || frameUV.x > 1 || frameUV.y < 0 || frameUV.y > 1)
                    return false;

                // 相对于角色帧中"手点"的偏移（帧内局部 UV 空间）
                float2 handFrameUV = anchorFrameUV.xy;
                float2 handLocalUV = anchorFrameUV.zw;
                float2 offset = frameUV - handFrameUV;

                // 绕虚拟左手的旋转
                float rotCos = rotCosSin.x;
                float rotSin = rotCosSin.y;
                float2 rotated;
                rotated.x = offset.x * rotCos - offset.y * rotSin;
                rotated.y = offset.x * rotSin + offset.y * rotCos;

                // 绕虚拟左手做水平镜像（用于从 SE 贴图生成 SW/NW 方向）
                if (flipX > 0.5)
                    rotated.x = -rotated.x;

                // 转换到武器贴图的局部 UV：以 handLocalUV 作为 pivot
                float2 weaponLocalUV = handLocalUV + rotated;

                if (weaponLocalUV.x < 0 || weaponLocalUV.x > 1 || weaponLocalUV.y < 0 || weaponLocalUV.y > 1)
                    return false;

                float2 weaponUV = TransformUV(weaponLocalUV, weaponRect);
                fixed4 c4 = tex2D(weaponTex, weaponUV);
                if (c4.a <= CUTOFF)
                    return false;

                outColor = c4;
                return true;
            }
            
            // 主手武器采样
            bool TrySampleWeapon0(float2 mainUV, out fixed4 outColor)
            {
                return TrySampleWeaponGeneric(mainUV, _Weapon0Tex, _Weapon0Rect,
                    _Weapon0AnchorFrameUV, _Weapon0RotCosSin, _Weapon0FlipX, _Weapon0Enabled, outColor);
            }
            
            // 副手武器采样
            bool TrySampleWeapon1(float2 mainUV, out fixed4 outColor)
            {
                return TrySampleWeaponGeneric(mainUV, _Weapon1Tex, _Weapon1Rect,
                    _Weapon1AnchorFrameUV, _Weapon1RotCosSin, _Weapon1FlipX, _Weapon1Enabled, outColor);
            }
            
            // ============================================================================
            // 阴影系统
            // - 所有阴影判断都工作在"帧内 UV 空间"：
            //   * X/Y 取值 0~1，对应一帧的左下到右上
            //   * _FrameSize 传入帧的像素宽高，用于从 UV 反推「像素步长」
            //   * _ShadowBaseY 传入 groundPixelY 对应的帧内 UV（像素中心）
            // ============================================================================
            
            // Mode 1: 离地渲染阴影（矩形，左右/上下各扩一格）
            bool IsOffGroundShadow(float2 uv, float leftX, float rightX, float baseY)
            {
                float2 step = 1.0 / _FrameSize;
                float minX = leftX - step.x;
                float maxX = rightX + step.x;
                float minY = baseY - step.y;
                float maxY = baseY + step.y;

                if (uv.x < minX || uv.x > maxX)
                    return false;
                if (uv.y < minY || uv.y > maxY)
                    return false;

                return true;
            }
            
            // Mode 2: 空中模式阴影（缺四角的 4×3 矩形）
            bool IsAirShadow(float2 uv, float centerX, float baseY)
            {
                float2 step = 1.0 / _FrameSize;
                float dx = (uv.x - centerX) / step.x;
                float dy = (uv.y - baseY) / step.y;

                bool inRect = (dx >= -1.0 && dx <= 2.0 && dy >= -1.0 && dy <= 1.0);
                if (!inRect)
                    return false;

                bool inCorner = ((dx < -0.5 || dx > 1.5) && (dy < -0.5 || dy > 0.5));
                if (inCorner)
                    return false;

                return true;
            }
            
            // Mode 3: 完全离地阴影（十字形）
            bool IsHighAirShadow(float2 uv, float centerX, float baseY)
            {
                float2 step = 1.0 / _FrameSize;
                float dx = (uv.x - centerX) / step.x;
                float dy = (uv.y - baseY) / step.y;

                bool inCenter = abs(dx) <= 0.5 && abs(dy) <= 0.5;
                bool inVertical = abs(dx) <= 0.5 && (abs(dy - 1.0) <= 0.5 || abs(dy + 1.0) <= 0.5);
                bool inHorizontal = abs(dy) <= 0.5 && (abs(dx - 1.0) <= 0.5 || abs(dx + 1.0) <= 0.5);

                return inCenter || inVertical || inHorizontal;
            }
            
            // ============================================================================
            // 眼部装饰系统
            // Mode 1: 黑眼圈 - 两只眼睛下方一格
            // Mode 2: 刀疤 - SE时右眼上下一格，SW时左眼上下一格
            // ============================================================================
            
            // 判断当前像素是否在眼睛的指定偏移位置（上/下一格）
            bool IsPixelAtEyeOffset(float2 frameUV, float2 eyePos, float2 offset)
            {
                float2 step = 1.0 / _FrameSize;
                float2 targetPos = eyePos + offset * step;
                
                float dx = abs(frameUV.x - targetPos.x) / step.x;
                float dy = abs(frameUV.y - targetPos.y) / step.y;
                
                return dx <= 0.5 && dy <= 0.5;
            }
            
            // 应用眼部装饰，返回是否命中装饰区域
            bool ApplyEyeDecoration(float2 frameUV, float2 headUVLocal, PartIDs parts, inout fixed4 color)
            {
                if (_EyeDecoMode < 0.5)
                    return false;

                // 仅在头部区域内生效
                if (!parts.isHead)
                    return false;

                // 若当前像素被头盔覆盖，则不绘制眼部装饰（装饰在头盔下面）
                if (_EnableHelmet > 0.5)
                {
                    fixed3 helmetSample;
                    if (TrySampleEquip(headUVLocal, _HelmetRect, _HelmetTex, helmetSample))
                        return false;
                }

                bool hit = false;
                
                if (_EyeDecoMode < 1.5)
                {
                    // Mode 1: 黑眼圈 - 两只眼睛下方一格（frameUV 的 Y 向下为负）
                    // 左眼下方
                    if (_LeftEyePos.x > 0.01 || _LeftEyePos.y > 0.01)
                    {
                        if (IsPixelAtEyeOffset(frameUV, _LeftEyePos, float2(0, -1)))
                            hit = true;
                    }
                    // 右眼下方
                    if (_RightEyePos.x > 0.01 || _RightEyePos.y > 0.01)
                    {
                        if (IsPixelAtEyeOffset(frameUV, _RightEyePos, float2(0, -1)))
                            hit = true;
                    }
                }
                else if (_EyeDecoMode < 2.5)
                {
                    // Mode 2: 刀疤 - SE时右眼上下一格，SW时左眼上下一格
                    // _BodyInEast > 0.5 表示朝东（SE/NE）
                    float2 targetEyePos = _BodyInEast > 0.5 ? _RightEyePos : _LeftEyePos;
                    
                    if (targetEyePos.x > 0.01 || targetEyePos.y > 0.01)
                    {
                        // 上一格
                        if (IsPixelAtEyeOffset(frameUV, targetEyePos, float2(0, 1)))
                            hit = true;
                        // 下一格
                        if (IsPixelAtEyeOffset(frameUV, targetEyePos, float2(0, -1)))
                            hit = true;
                    }
                }
                
                if (hit)
                {
                    color.rgb = _EyeDecoColor.rgb;
                }

                return hit;
            }

            // 判断武器黑色描边是否位于眼睛附近（中心+上下左右一格），用于屏蔽武器描边
            bool IsWeaponBlackOutlineNearEyes(float2 frameUV, fixed4 weaponColor)
            {
                if (!IsNearBlack(weaponColor.rgb))
                    return false;

                bool hit = false;

                if (_LeftEyePos.x > 0.01 || _LeftEyePos.y > 0.01)
                {
                    if (IsPixelAtEyeOffset(frameUV, _LeftEyePos, float2(0, 0))
                        || IsPixelAtEyeOffset(frameUV, _LeftEyePos, float2(0, 1))
                        || IsPixelAtEyeOffset(frameUV, _LeftEyePos, float2(0, -1))
                        || IsPixelAtEyeOffset(frameUV, _LeftEyePos, float2(1, 0))
                        || IsPixelAtEyeOffset(frameUV, _LeftEyePos, float2(-1, 0)))
                    {
                        hit = true;
                    }
                }

                if (!hit && (_RightEyePos.x > 0.01 || _RightEyePos.y > 0.01))
                {
                    if (IsPixelAtEyeOffset(frameUV, _RightEyePos, float2(0, 0))
                        || IsPixelAtEyeOffset(frameUV, _RightEyePos, float2(0, 1))
                        || IsPixelAtEyeOffset(frameUV, _RightEyePos, float2(0, -1))
                        || IsPixelAtEyeOffset(frameUV, _RightEyePos, float2(1, 0))
                        || IsPixelAtEyeOffset(frameUV, _RightEyePos, float2(-1, 0)))
                    {
                        hit = true;
                    }
                }

                return hit;
            }

            // 在帧内 UV 空间下，采样基线 y = _ShadowBaseY 上、x=xOnFrame 处的
            // （本体/披风/武器）alpha，用于决定该 X 处是否有可投射阴影的像素。
            float SampleCasterAlphaAtGroundX(float xOnFrame)
            {
                float2 frameUV = float2(xOnFrame, _ShadowBaseY);
                float2 frameMin = _CharFrameRect.xy;
                float2 frameMax = _CharFrameRect.zw;
                float2 uvGround = lerp(frameMin, frameMax, frameUV);
                fixed4 col = tex2D(_MainTex, uvGround);
                if (col.a > 0.001)
                    return col.a;

                if (_EnableCloak > 0.5)
                {
                    fixed4 bodyUV = tex2D(_BodyUVMap, uvGround);
                    float2 cloakUV = TransformUV(bodyUV.rg, _CloakRect);
                    col = tex2D(_CloakTex, cloakUV);
                    if (col.a > 0.001)
                        return col.a;
                }

                fixed4 weaponCol;
                if (TrySampleWeapon0(uvGround, weaponCol))
                    return weaponCol.a;
                if (TrySampleWeapon1(uvGround, weaponCol))
                    return weaponCol.a;

                return 0;
            }

            // Mode0 地面阴影判断
            // groundPixelY 表示“整一排像素是地面”：
            // - 基线这一排：左右各扩一格（只在周围画阴影，不盖住本体像素）
            // - 基线正下方一排：只用原始宽度（不包含左右扩展）
            // 注意：这里的 uv 参数为帧内 UV（frameUV），而非全贴图 UV。
            bool IsGroundShadowMode0(float2 uv)
            {
                float stepY = 1.0 / _FrameSize.y;
                float stepX = 1.0 / _FrameSize.x;

                // 将当前像素的 y 与基线中心做像素级偏移比较
                float dy = uv.y - _ShadowBaseY;

                // 只在基线这一排以及其正下方一排内绘制阴影
                if (dy > 0.5 * stepY || dy < -1.5 * stepY)
                    return false;

                float alpha = 0.0;

                // 基线正下方一排：只看正上方一列的 caster（对应“向下扩一格”）
                if (dy < -0.5 * stepY)
                {
                    alpha = max(alpha, SampleCasterAlphaAtGroundX(uv.x));
                }
                // 基线这一排：只看左右两侧的 caster，用于形成左右各扩一格的阴影带
                else
                {
                    alpha = max(alpha, SampleCasterAlphaAtGroundX(uv.x - stepX));
                    alpha = max(alpha, SampleCasterAlphaAtGroundX(uv.x + stepX));
                }

                return alpha > 0.001;
            }
            
            // ============================================================================
            // 顶点着色器
            // ============================================================================
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }
            
            // ============================================================================
            // 身体层 / 头部层合成
            // ============================================================================
            // isHeadCore: 真实头部区域（headUV.b=ID_HEAD 且 headUV.a>0.5）
            // _BodyInFront 影响两件事：
            // - Torso(衣服/裤子/斗篷) 与“核心头部”的前后关系：
            //   * 朝南 (_BodyInFront < 0.5): 核心头部前置，Torso 在核心头部后面
            //   * 朝北 (_BodyInFront > 0.5): Torso 可以覆盖头部
            // - 手脚与衣服的前后关系：
            //   * 朝南: 手脚在衣服前（显示手套/鞋子）
            //   * 朝北: 手脚也视为 Torso 区域，由衣服/斗篷覆盖
            // baseAlpha：主贴图(_MainTex)在该像素处的 alpha，用于判断是否有身体像素遮挡
            void ApplyBodyLayers(fixed4 bodyUV, PartIDs parts, bool isHeadCore, float baseAlpha, inout fixed4 ioColor, out float bodyLayerAlpha, out float bodySrcId)
            {
                bodyLayerAlpha = 0;
                bodySrcId = SRC_NONE;

                bool isAnyHand = parts.isLeftHand || parts.isRightHand;
                bool isAnyFoot = parts.isLeftFoot || parts.isRightFoot;

                // 朝北时：手脚也视为 Torso 区域，用衣服/裤子/斗篷覆盖
                bool useTorsoEquip = parts.isTorso || (_BodyInFront > 0.5 && (isAnyHand || isAnyFoot));

                if (useTorsoEquip)
                {
                    // 朝南且真实头部区域：禁止 Torso 覆盖核心头部
                    if (_BodyInFront < 0.5 && isHeadCore)
                        return;

                    fixed3 sampled;
                    // 背包 + 裤子（最底层）-> 服装 -> 斗篷（最上层）

                    // 朝南：仅在主贴图透明处绘制背包，让身体像素始终挡在背包前面
                    // 同时参与受击描边
                    if (_BodyInFront < 0.5 && baseAlpha <= CUTOFF && _EnableBag > 0.5 && TrySampleEquip(bodyUV.rg, _BagRect, _BagTex, sampled))
                    {
                        ioColor.rgb = sampled;
                        bodyLayerAlpha = 1.0;
                        bodySrcId = SRC_BAG;
                    }

                    if (_EnablePants > 0.5 && TrySampleEquip(bodyUV.rg, _PantsRect, _PantsTex, sampled))
                    {
                        ioColor.rgb = sampled;
                        bodyLayerAlpha = 1.0;
                        bodySrcId = SRC_OTHER;
                    }
                    if (_EnableCloth > 0.5 && TrySampleEquip(bodyUV.rg, _ClothRect, _ClothTex, sampled))
                    {
                        ioColor.rgb = sampled;
                        bodyLayerAlpha = 1.0;
                        bodySrcId = SRC_OTHER;
                    }
                    if (_EnableCloak > 0.5 && TrySampleEquip(bodyUV.rg, _CloakRect, _CloakTex, sampled))
                    {
                        ioColor.rgb = sampled;
                        bodyLayerAlpha = 1.0;
                        bodySrcId = SRC_CLOAK;
                    }
                }
                else if ((parts.isLeftHand || parts.isRightHand) && _EnableGloves > 0.5)
                {
                    // 只有朝南时才会走到这里；朝北时手脚已被视为 Torso 覆盖
                    ioColor.rgb = parts.isLeftHand ? _LeftHandColor.rgb : _RightHandColor.rgb;
                    bodyLayerAlpha = 1.0;
                    bodySrcId = SRC_OTHER;
                }
                else if ((parts.isLeftFoot || parts.isRightFoot) && _EnableShoes > 0.5)
                {
                    ioColor.rgb = parts.isLeftFoot ? _LeftFootColor.rgb : _RightFootColor.rgb;
                    bodyLayerAlpha = 1.0;
                    bodySrcId = SRC_OTHER;
                }
                else if (parts.isLeftEye && _EnableLeftEye > 0.5)
                {
                    ioColor.rgb = _LeftEyeColor.rgb;
                    bodyLayerAlpha = 1.0;
                    bodySrcId = SRC_OTHER;
                }
                else if (parts.isRightEye && _EnableRightEye > 0.5)
                {
                    ioColor.rgb = _RightEyeColor.rgb;
                    bodyLayerAlpha = 1.0;
                    bodySrcId = SRC_OTHER;
                }
            }

            // 头部层顺序：头发（底层）-> 面部装饰 -> 胡子 -> 头盔（顶层）
            // 若当前像素属于任意一只手且同时处于头部区域，则跳过头部层覆盖，保留身体层（手）颜色
            void ApplyHeadLayers(float2 baseHeadUV, PartIDs parts, inout fixed4 ioColor, out float headLayerAlpha, out float headSrcId)
            {
                headLayerAlpha = 0;
                headSrcId = SRC_NONE;
                if (!parts.isHead) return;

                // 无论身体朝向如何，只要当前像素属于手，就不让头部装备覆盖
                bool isAnyHand = parts.isLeftHand || parts.isRightHand;
                if (isAnyHand)
                    return;

                fixed3 sampled;
                // 头盔（顶层）：命中则提前返回
                if (_EnableHelmet > 0.5 && TrySampleEquip(baseHeadUV, _HelmetRect, _HelmetTex, sampled))
                {
                    ioColor.rgb = sampled;
                    headLayerAlpha = 1.0;
                    headSrcId = SRC_HELMET;
                    return;
                }

                // 胡子 -> 面部饰品 -> 头发（从上到下层级）
                bool wrote = false;
                if (_EnableBeard > 0.5 && TrySampleEquip(baseHeadUV, _BeardRect, _BeardTex, sampled))
                {
                    ioColor.rgb = sampled;
                    wrote = true;
                    headSrcId = SRC_BEARD;
                }
                if (!wrote && _EnableFaceAccessory > 0.5 && TrySampleEquip(baseHeadUV, _FaceAccessoryRect, _FaceAccessoryTex, sampled))
                {
                    ioColor.rgb = sampled;
                    wrote = true;
                    headSrcId = SRC_FACE;
                }
                if (!wrote && _EnableHair > 0.5 && TrySampleEquip(baseHeadUV, _HairRect, _HairTex, sampled))
                {
                    ioColor.rgb = sampled;
                    wrote = true;
                    headSrcId = SRC_HAIR;
                }
                
                if (wrote) headLayerAlpha = 1.0;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 baseColor = tex2D(_MainTex, i.uv);

                // 使用 Sprite 的 UV 直接采样 UV Map（运行时 UVMap 与角色 spritesheet 共享布局）
                float2 uvFrame = i.uv;
                fixed4 bodyUV = tex2D(_BodyUVMap, uvFrame);
                fixed4 headUV = tex2D(_HeadUVMap, uvFrame);

                // 将顶点 UV（整张角色贴图坐标）映射到“当前帧”的局部 UV（0~1，左下为原点）
                float2 frameMin = _CharFrameRect.xy;
                float2 frameMax = _CharFrameRect.zw;
                float2 frameSizeUV = frameMax - frameMin;
                float2 frameUV = (uvFrame - frameMin) / frameSizeUV;

                float bodyPartID = bodyUV.b;
                float headPartID = headUV.b;

                // 预计算所有部位ID判断
                PartIDs parts = ComputePartIDs(bodyPartID, headPartID);
                
                // 调试模式 1: 显示身体层区域
                if (_DebugMode > 0.5 && _DebugMode < 1.5)
                {
                    fixed4 debugColor = baseColor;
                    debugColor.a = baseColor.a;
                    if (parts.isTorso)          debugColor.rgb = fixed3(0.3, 0.5, 0.9); // 蓝色
                    else if (parts.isLeftHand)  debugColor.rgb = fixed3(0.9, 0.9, 0.2); // 黄色
                    else if (parts.isRightHand) debugColor.rgb = fixed3(0.9, 0.6, 0.2); // 橙色
                    else if (parts.isLeftFoot)  debugColor.rgb = fixed3(0.6, 0.3, 0.9); // 紫色
                    else if (parts.isRightFoot) debugColor.rgb = fixed3(0.9, 0.3, 0.6); // 粉色
                    return debugColor;
                }

                // 调试模式 2: 显示头部层区域
                if (_DebugMode > 1.5 && _DebugMode < 2.5)
                {
                    fixed4 debugColor = baseColor;
                    debugColor.a = baseColor.a;
                    if (parts.isHead) debugColor.rgb = fixed3(0.2, 0.8, 0.8); // 青色
                    return debugColor;
                }

                // 调试模式 3：显示装备采样结果（身体层=衣服，头部层=头盔）
                if (_DebugMode > 2.5 && _DebugMode < 3.5)
                {
                    if (parts.isTorso)
                    {
                        float2 clothUVCoord = TransformUV(bodyUV.rg, _ClothRect);
                        fixed4 clothColor = tex2D(_ClothTex, clothUVCoord);
                        return fixed4(clothColor.rgb, 1);
                    }
                    if (parts.isHead)
                    {
                        float2 helmetUV = TransformUV(headUV.rg, _HelmetRect);
                        fixed4 helmetColor = tex2D(_HelmetTex, helmetUV);
                        return fixed4(helmetColor.rgb, 1);
                    }
                    return fixed4(0, 0, 0, baseColor.a);
                }

                // 调试模式 4：显示 UVMap 中的原始 UV（R=U, G=V）
                if (_DebugMode > 3.5 && _DebugMode < 4.5)
                {
                    if (parts.isTorso)
                        return fixed4(bodyUV.r, bodyUV.g, 0, 1);
                    if (parts.isHead)
                        return fixed4(headUV.r, headUV.g, 0, 1);
                    return fixed4(0, 0, 0, baseColor.a);
                }

                // 调试模式 5：显示顶点原始 UV（i.uv）
                if (_DebugMode > 4.5 && _DebugMode < 5.5)
                {
                    return fixed4(i.uv.x, i.uv.y, 0, baseColor.a);
                }

                // ========== 第一步：先合成角色图层（底图 + 身体层 + 头部层）==========
                fixed4 charColor = baseColor;

                // 计算真实头部区域：由 HeadUVMap 的 alpha 通道标记（方案A）
                // headUV.b 标记整块头部/扩展区域；headUV.a>0.5 仅表示核心头部区域
                bool isHeadCore = parts.isHead && headUV.a > 0.5;

                // 身体层（衣服/鞋子/手套/眼睛等）
                float bodyLayerAlpha = 0;
                float bodySrcId = SRC_NONE;

                // 头部层（头发/饰品/胡子/头盔）
                float headLayerAlpha = 0;
                float headSrcId = SRC_NONE;

                if (_BodyInFront > 0.5)
                {
                    // 身体在前：先绘制头部，再绘制身体（衣服/斗篷可以盖住头）
                    ApplyHeadLayers(headUV.rg, parts, charColor, headLayerAlpha, headSrcId);
                    ApplyBodyLayers(bodyUV, parts, isHeadCore, baseColor.a, charColor, bodyLayerAlpha, bodySrcId);
                }
                else
                {
                    // 身体在后：先绘制身体，再绘制头部（头部始终在衣服前）
                    ApplyBodyLayers(bodyUV, parts, isHeadCore, baseColor.a, charColor, bodyLayerAlpha, bodySrcId);
                    ApplyHeadLayers(headUV.rg, parts, charColor, headLayerAlpha, headSrcId);
                }

                // 角色最终 alpha：包含底图 + 身体层 + 头部层
                float charAlpha = max(charColor.a, max(bodyLayerAlpha, headLayerAlpha));

                // 推导当前像素的主要来源（主体 / 斗篷 / 头盔 / 面部饰品 / 胡子 / 头发）
                float srcId = SRC_NONE;
                if (_BodyInFront > 0.5)
                {
                    // 身体在前：身体覆盖头部
                    if (bodyLayerAlpha > CUTOFF)
                        srcId = bodySrcId;
                    else if (headLayerAlpha > CUTOFF)
                        srcId = headSrcId;
                }
                else
                {
                    // 身体在后：头部覆盖身体
                    if (headLayerAlpha > CUTOFF)
                        srcId = headSrcId;
                    else if (bodyLayerAlpha > CUTOFF)
                        srcId = bodySrcId;
                }

                // 若没有任何装备覆盖，则来源退回到主体贴图
                if (srcId == SRC_NONE && baseColor.a > CUTOFF)
                    srcId = SRC_MAIN;

                // ========== 肤色调色板替换：Key/Palette 查表式 ==========
                if (_SkinPaletteEnabled > 0.5 && srcId == SRC_MAIN)
                {
                    // 判断是否是皮肤部位（头/手/脚/躯干）
                    bool isSkinPart =
                        parts.isHead ||
                        parts.isLeftHand || parts.isRightHand ||
                        parts.isLeftFoot || parts.isRightFoot ||
                        parts.isTorso;

                    // 只处理皮肤区域的非黑色像素（避免动描边）
                    if (isSkinPart && charColor.a > CUTOFF && !IsNearBlack(charColor.rgb))
                    {
                        // 从 KeyTex 读取颜色索引（存在 alpha 通道）
                        fixed4 keySample = tex2D(_SkinKeyTex, i.uv);
                        float key = keySample.a;
                        
                        // key > 0 表示该像素有调色板映射
                        if (key > 0.001)
                        {
                            // 用 key 作为 U 坐标采样 PaletteTex，获取目标颜色
                            fixed4 paletteColor = tex2D(_SkinPaletteTex, float2(key, 0.5));
                            charColor.rgb = paletteColor.rgb;
                        }
                    }
                }

                // 手脚区域判断（直接使用预计算结果）
                bool isAnyFoot = parts.isLeftFoot || parts.isRightFoot;

                // ========== 第二步：采样武器 ==========
                fixed4 weapon0Color, weapon1Color;
                bool hasWeapon0 = TrySampleWeapon0(i.uv, weapon0Color);
                bool hasWeapon1 = TrySampleWeapon1(i.uv, weapon1Color);

                // 判断武器黑描边是否在眼睛附近，需要跳过
                bool skipWeapon0NearEye = false;
                bool skipWeapon1NearEye = false;
                if (hasWeapon0)
                    skipWeapon0NearEye = IsWeaponBlackOutlineNearEyes(frameUV, weapon0Color);
                if (hasWeapon1)
                    skipWeapon1NearEye = IsWeaponBlackOutlineNearEyes(frameUV, weapon1Color);

                // ========== 第三步：根据 DepthMode 和是否手/脚/配置合成最终颜色 ==========
                fixed4 finalColor = charColor;
                float finalAlpha = charAlpha;

                // 朝北武器（在角色后面）：有角色像素时由角色遮挡；无角色像素时只显示武器
                if (hasWeapon0 && _Weapon0DepthMode < 0.5 && !skipWeapon0NearEye)
                {
                    if (charAlpha <= CUTOFF)
                    {
                        finalColor.rgb = weapon0Color.rgb;
                        srcId = SRC_WEAPON0;
                    }
                    finalAlpha = max(finalAlpha, weapon0Color.a);
                }
                if (hasWeapon1 && _Weapon1DepthMode < 0.5 && !skipWeapon1NearEye)
                {
                    if (charAlpha <= CUTOFF)
                    {
                        finalColor.rgb = weapon1Color.rgb;
                        srcId = SRC_WEAPON1;
                    }
                    finalAlpha = max(finalAlpha, weapon1Color.a);
                }

                // 朝南武器（在角色前面）：脚始终在所有武器前面；手是否在前由每个武器的 HandInFront 配置决定。
                bool isAnyHand = parts.isLeftHand || parts.isRightHand;

                // 副手先画（在主手后面）
                if (hasWeapon1 && _Weapon1DepthMode > 0.5)
                {
                    bool handBlocksW1 = isAnyHand && (_Weapon1HandInFront > 0.5);
                    // 手在前时阻挡武器像素，仅当手不在前面时才由武器覆盖角色
                    if (!handBlocksW1 && !skipWeapon1NearEye)
                    {
                        finalColor.rgb = weapon1Color.rgb;
                        finalAlpha = max(finalAlpha, weapon1Color.a);
                        srcId = SRC_WEAPON1;
                    }
                }
                // 主手后画（在副手前面）
                if (hasWeapon0 && _Weapon0DepthMode > 0.5)
                {
                    bool handBlocksW0 = isAnyHand && (_Weapon0HandInFront > 0.5);
                    if (!handBlocksW0 && !skipWeapon0NearEye)
                    {
                        finalColor.rgb = weapon0Color.rgb;
                        finalAlpha = max(finalAlpha, weapon0Color.a);
                        srcId = SRC_WEAPON0;
                    }
                }

                // 朝北：背包在包括武器在内的最前面
                if (_BodyInFront > 0.5 && _EnableBag > 0.5)
                {
                    fixed3 bagSample;
                    if (TrySampleEquip(bodyUV.rg, _BagRect, _BagTex, bagSample))
                    {
                        finalColor.rgb = bagSample;
                        finalAlpha = max(finalAlpha, 1.0);
                        srcId = SRC_BAG;
                    }
                }

                finalColor.a = finalAlpha;

                float stepYGround = 1.0 / _FrameSize.y;
                float dyGround = frameUV.y - _ShadowBaseY;
                if (dyGround < -0.5 * stepYGround && dyGround > -1.5 * stepYGround)
                {
                    if (IsNearBlack(finalColor.rgb))
                    {
                        finalAlpha = 0;
                        finalColor.a = 0;
                    }
                }

                // 只有最终颜色本身接近黑色时，才有可能是描边像素，才进入受击描边逻辑
                // 仅对特定来源执行描边检测，避免无关像素进入该分支
                bool canHitOutline =
                    srcId == SRC_MAIN ||
                    srcId == SRC_CLOAK ||
                    srcId == SRC_HELMET ||
                    srcId == SRC_FACE ||
                    srcId == SRC_BEARD ||
                    srcId == SRC_HAIR ||
                    srcId == SRC_WEAPON0 ||
                    srcId == SRC_WEAPON1 ||
                    srcId == SRC_BAG;

                if (_HitOutline > 0.5 && finalAlpha > CUTOFF && canHitOutline && IsNearBlack(finalColor.rgb))
                {
                    bool isHitOutline = false;

                    // 根据最终像素来源，仅对对应图层执行一次描边检测，避免多余采样
                    if (srcId == SRC_MAIN)
                    {
                        isHitOutline = IsMainTexOutlineAtFrameUV(frameUV, frameMin, frameSizeUV);
                    }
                    else if (srcId == SRC_CLOAK)
                    {
                        if (_EnableCloak > 0.5)
                            isHitOutline = IsEquipOutlineAtUVLocal(bodyUV.rg, _CloakRect, _CloakTex, finalColor.rgb);
                    }
                    else if (srcId == SRC_HELMET)
                    {
                        if (_EnableHelmet > 0.5)
                            isHitOutline = IsEquipOutlineAtUVLocal(headUV.rg, _HelmetRect, _HelmetTex, finalColor.rgb);
                    }
                    else if (srcId == SRC_FACE)
                    {
                        if (_EnableFaceAccessory > 0.5)
                            isHitOutline = IsEquipOutlineAtUVLocal(headUV.rg, _FaceAccessoryRect, _FaceAccessoryTex, finalColor.rgb);
                    }
                    else if (srcId == SRC_BEARD)
                    {
                        if (_EnableBeard > 0.5)
                            isHitOutline = IsEquipOutlineAtUVLocal(headUV.rg, _BeardRect, _BeardTex, finalColor.rgb);
                    }
                    else if (srcId == SRC_HAIR)
                    {
                        if (_EnableHair > 0.5)
                            isHitOutline = IsEquipOutlineAtUVLocal(headUV.rg, _HairRect, _HairTex, finalColor.rgb);
                    }
                    else if (srcId == SRC_WEAPON0)
                    {
                        isHitOutline = IsWeaponHitOutline(0, hasWeapon0, weapon0Color, frameUV, frameMin, frameSizeUV, finalColor.rgb);
                    }
                    else if (srcId == SRC_WEAPON1)
                    {
                        isHitOutline = IsWeaponHitOutline(1, hasWeapon1, weapon1Color, frameUV, frameMin, frameSizeUV, finalColor.rgb);
                    }
                    else if (srcId == SRC_BAG)
                    {
                        if (_EnableBag > 0.5)
                            isHitOutline = IsEquipOutlineAtUVLocal(bodyUV.rg, _BagRect, _BagTex, finalColor.rgb);
                    }

                    if (isHitOutline)
                    {
                        // 受击描边颜色：由 _HitOutlineColor 控制，默认 #b40909
                        finalColor.rgb = _HitOutlineColor.rgb;
                    }
                }

                // ========== 眼部装饰（在角色和武器之上、阴影之前）==========
                if (finalAlpha > CUTOFF)
                {
                    ApplyEyeDecoration(frameUV, headUV.rg, parts, finalColor);
                }

                // ========== 像素级阴影系统（帧内 UV 上的 4 种模式）==========
                // ShadowMode:
                // 0 = Mode0：脚在地面基线，基线一排 + 下一排阴影
                // 1 = Mode1：离地 1~2 像素，矩形阴影（左右/上下各扩一格）
                // 2 = Mode2：离地 3~9 像素，缺四角 4x3 阴影
                // 3 = Mode3：完全离地，中心十字形阴影
                if (finalAlpha <= CUTOFF && _ShadowEnabled > 0.5)
                {
                    fixed4 shadowColor = fixed4(0, 0, 0, 0);

                    if (_ShadowMode < 0.5)
                    {
                        // Mode0：调用 IsGroundShadowMode0
                        if (IsGroundShadowMode0(frameUV))
                        {
                            shadowColor = _ShadowColor;
                        }
                    }
                    else if (_ShadowMode < 1.5)
                    {
                        // Mode1：内联 IsOffGroundShadow
                        if (IsOffGroundShadow(frameUV, _ShadowLeftX, _ShadowRightX, _ShadowBaseY))
                        {
                            shadowColor = _ShadowColor;
                        }
                    }
                    else if (_ShadowMode < 2.5)
                    {
                        // Mode2：内联 IsAirShadow
                        if (IsAirShadow(frameUV, _ShadowCenterX, _ShadowBaseY))
                        {
                            shadowColor = _ShadowColor;
                        }
                    }
                    else
                    {
                        // Mode3：内联 IsHighAirShadow
                        if (IsHighAirShadow(frameUV, _ShadowCenterX, _ShadowBaseY))
                        {
                            shadowColor = _ShadowColor;
                        }
                    }

                    if (shadowColor.a > 0)
                    {
                        finalColor = shadowColor;
                    }
                }
             

                return finalColor * i.color;
            }
            ENDCG
        }
    }
    
    Fallback "Sprites/Default"
}
