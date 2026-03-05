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
        _MaskTex ("Mask Texture", 2D) = "white" {}
        
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
        _Weapon0IsSequence ("Main Hand Uses Sequence", Float) = 0
        _Weapon0HideOutlineOnBody ("Main Hand Hide Outline On Body", Float) = 0
        
        [Header(Weapon Off Hand)]
        _Weapon1Tex ("Off Hand Weapon Texture", 2D) = "white" {}
        _Weapon1Rect ("Off Hand Weapon Rect", Vector) = (0,0,1,1)
        _Weapon1AnchorFrameUV ("Weapon1 Anchor Frame UV", Vector) = (0.5, 0.5, 0, 0)
        _Weapon1RotCosSin ("Weapon1 Rot Cos/Sin", Vector) = (1, 0, 0, 0)
        _Weapon1FlipX ("Off Hand Flip X", Float) = 0
        _Weapon1DepthMode ("Off Hand Depth Mode", Float) = 0
        _Weapon1Enabled ("Enable Off Hand Weapon", Float) = 0
        _Weapon1HandInFront ("Off Hand: Hand In Front", Float) = 1
        _Weapon1IsSequence ("Off Hand Uses Sequence", Float) = 0
        _Weapon1HideOutlineOnBody ("Off Hand Hide Outline On Body", Float) = 0
        
        [Header(Enable Layers)]
        _EnableHair ("Enable Hair", Float) = 0
        _EnableFaceAccessory ("Enable Face Accessory", Float) = 0
        _EnableBeard ("Enable Beard", Float) = 0
        _EnableHelmet ("Enable Helmet", Float) = 0
        _EnableMask ("Enable Mask", Float) = 0
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
        _EyeDecoTex ("Eye Deco Texture", 2D) = "white" {}
        _EyeDecoRect ("Eye Deco Rect", Vector) = (0, 0, 1, 1)
        _EnableEyeDeco ("Enable Eye Deco", Float) = 0
        
        [Header(Debug)]
        // 调试模式：0=关闭，1=身体层区域，2=头部层区域，3=装备采样结果，4=UVMap原始UV，5=顶点UV，6=核心区域
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

        [Header(Default Outline)]
        [Toggle] _DefaultOutlineEnabled ("Default Outline Enabled", Float) = 1

        [Header(Extra Outline)]
        _ExtraOutlineEnabled ("Extra Outline Enabled", Float) = 0
        [HDR] _ExtraOutlineColor ("Extra Outline Color", Color) = (1,1,1,1)
        
        [Header(Skin Palette)]
        _SkinPaletteEnabled ("Skin Palette Enabled", Float) = 0
        
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
            sampler2D _MaskTex;
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
            float4 _MaskRect;
            float4 _CharFrameRect;      // 当前角色帧在 _MainTex 中的 Rect
            
            // 主手武器参数
            float4 _Weapon0Rect;
            float4 _Weapon0AnchorFrameUV;
            float4 _Weapon0RotCosSin;
            float _Weapon0FlipX;
            float _Weapon0DepthMode;
            float _Weapon0Enabled;
            float _Weapon0HandInFront;
            float _Weapon0IsSequence;
            float _Weapon0HideOutlineOnBody;
            
            // 副手武器参数
            float4 _Weapon1Rect;
            float4 _Weapon1AnchorFrameUV;
            float4 _Weapon1RotCosSin;
            float _Weapon1FlipX;
            float _Weapon1DepthMode;
            float _Weapon1Enabled;
            float _Weapon1HandInFront;
            float _Weapon1IsSequence;
            float _Weapon1HideOutlineOnBody;
            
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
            float _EnableMask;
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
            
            // 眼部装饰参数（贴图方式）
            sampler2D _EyeDecoTex;
            float4 _EyeDecoRect;
            float _EnableEyeDeco;
            
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

            // 默认黑色程序描边开关（仅影响“默认黑”，Hit/Extra 不受影响）
            float _DefaultOutlineEnabled;

            float _ExtraOutlineEnabled;
            fixed4 _ExtraOutlineColor;

            // 肤色映射（颜色数组查表）
            static const int MAX_SKIN_COLORS = 16;
            fixed4 _SkinSrcColors[MAX_SKIN_COLORS];
            fixed4 _SkinDstColors[MAX_SKIN_COLORS];
            float _SkinColorCount;
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
            #define SRC_MASK    11
            
            // 层级优先级（越大越在前面），集中了所有层级规则
            // 描边逻辑只用这个函数判断优先级，不再重复写规则
            float GetLayerPriority(float sid)
            {
                // 背包：朝北时最前面，朝南时最后面
                if (sid == SRC_BAG)
                    return (_BodyInFront > 0.5) ? 100.0 : 0.0;
                
                // 武器0：朝南（DepthMode > 0.5）时在前面，手在前时优先级降低
                if (sid == SRC_WEAPON0)
                    return (_Weapon0DepthMode > 0.5) ? 80.0 : 10.0;
                
                // 武器1：朝南（DepthMode > 0.5）时在前面
                if (sid == SRC_WEAPON1)
                    return (_Weapon1DepthMode > 0.5) ? 70.0 : 5.0;
                
                // 人本体（各种装备）：中间优先级
                if (sid == SRC_MAIN || sid == SRC_OTHER || sid == SRC_CLOAK ||
                    sid == SRC_HELMET || sid == SRC_BEARD || sid == SRC_FACE ||
                    sid == SRC_HAIR || sid == SRC_MASK)
                    return 50.0;
                
                // 空
                return -1.0;
            }
            
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

            // 判断颜色是否接近黑色（当前仅用于少量逻辑，如地面黑线过滤）
            bool IsNearBlack(fixed3 rgb)
            {
                float sumRGB = rgb.r + rgb.g + rgb.b;
                return sumRGB < (80.0 / 255.0);
            }

            // 颜色近似相等（用于肤色映射查表）
            bool ColorApproxEqual(fixed3 a, fixed3 b, float eps)
            {
                return abs(a.r - b.r) < eps
                    && abs(a.g - b.g) < eps
                    && abs(a.b - b.b) < eps;
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

            // 通用贴图采样（保留 alpha）
            bool TrySampleEquipFull(float2 uv, float4 rect, sampler2D tex, out fixed4 outColor)
            {
                float2 coord = TransformUV(uv, rect);
                fixed4 c = tex2D(tex, coord);
                outColor = c;
                return c.a > CUTOFF;
            }

            // 前向声明：主手 / 副手武器采样函数（在下方实现）
            bool TrySampleWeapon0(float2 mainUV, out fixed4 outColor);
            bool TrySampleWeapon1(float2 mainUV, out fixed4 outColor);

            // 前向声明：本体描边 / 阴影用轮廓 alpha 采样函数（在后面实现）
            // 描边：使用 GetOutlineAlphaAtFrameUV（本体 + 身体装备 + 头部装备，不含武器/背包）
            // Mode0 阴影：使用 GetShadowCasterAlphaAtFrameUV（本体 + 身体装备 + 头部装备 + 武器，不含背包）
            float GetOutlineAlphaAtFrameUV(float2 frameUVSample, float2 frameMin, float2 frameMax);
            float GetShadowCasterAlphaAtFrameUV(float2 frameUVSample, float2 frameMin, float2 frameMax);

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
            
            // 在帧内 UV 空间下，采样基线 y = _ShadowBaseY 上、x=xOnFrame 处的
            // “加上黑边后”的近似轮廓 alpha：在基线附近对 X 轴做一次 3 像素（x, x±1）膨胀，效果接近原先依赖黑描边时的阴影宽度。
            float SampleCasterAlphaAtGroundX(float xOnFrame)
            {
                float2 frameMin = _CharFrameRect.xy;
                float2 frameMax = _CharFrameRect.zw;
                float2 step = 1.0 / _FrameSize;

                float alpha = 0.0;

                // 中心：x
                float2 frameUV = float2(xOnFrame, _ShadowBaseY);
                if (frameUV.x >= 0.0 && frameUV.x <= 1.0)
                {
                    alpha = max(alpha, GetShadowCasterAlphaAtFrameUV(frameUV, frameMin, frameMax));
                }

                // 右侧一格：x + 1 像素
                frameUV = float2(xOnFrame + step.x, _ShadowBaseY);
                if (frameUV.x >= 0.0 && frameUV.x <= 1.0)
                {
                    alpha = max(alpha, GetShadowCasterAlphaAtFrameUV(frameUV, frameMin, frameMax));
                }

                // 左侧一格：x - 1 像素
                frameUV = float2(xOnFrame - step.x, _ShadowBaseY);
                if (frameUV.x >= 0.0 && frameUV.x <= 1.0)
                {
                    alpha = max(alpha, GetShadowCasterAlphaAtFrameUV(frameUV, frameMin, frameMax));
                }

                return alpha;
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

                // 面罩图层：优先级高于胡子/饰品/头发，仅次于头盔
                if (_EnableMask > 0.5 && TrySampleEquip(baseHeadUV, _MaskRect, _MaskTex, sampled))
                {
                    ioColor.rgb = sampled;
                    headSrcId = SRC_MASK;
                }
                
                if (wrote) headLayerAlpha = 1.0;
            }

            // 计算指定帧内 UV 位置在当前帧上的“本体轮廓 alpha”
            // 描边：只关心 角色本体 + 身体装备(裤子/衣服/披风) + 头部装备(头盔/头发/胡子/面饰/面罩)
            // - 不包含武器/背包，它们有各自的描边
            float GetOutlineAlphaAtFrameUV(float2 frameUVSample, float2 frameMin, float2 frameMax)
            {
                // 将帧内 UV 映射回整张 _MainTex 的 UV
                float2 uvSample = lerp(frameMin, frameMax, frameUVSample);

                // 1）底图 alpha 直接来自 _MainTex（角色本体轮廓）
                fixed4 baseColorSample = tex2D(_MainTex, uvSample);
                float alphaSample = baseColorSample.a;

                // 2）身体装备轮廓（裤子 / 上衣 / 披风）
                fixed4 bodyUVSample = tex2D(_BodyUVMap, uvSample);
                fixed3 equipSample;

                if (_EnablePants > 0.5 && TrySampleEquip(bodyUVSample.rg, _PantsRect, _PantsTex, equipSample))
                {
                    alphaSample = max(alphaSample, 1.0);
                }
                if (_EnableCloth > 0.5 && TrySampleEquip(bodyUVSample.rg, _ClothRect, _ClothTex, equipSample))
                {
                    alphaSample = max(alphaSample, 1.0);
                }
                if (_EnableCloak > 0.5 && TrySampleEquip(bodyUVSample.rg, _CloakRect, _CloakTex, equipSample))
                {
                    alphaSample = max(alphaSample, 1.0);
                }

                // 3）头部装备轮廓（头盔 / 头发 / 面饰 / 胡子 / 面罩）
                fixed4 headUVSample = tex2D(_HeadUVMap, uvSample);
                fixed3 headEquipSample;

                if (_EnableHelmet > 0.5 && TrySampleEquip(headUVSample.rg, _HelmetRect, _HelmetTex, headEquipSample))
                {
                    alphaSample = max(alphaSample, 1.0);
                }
                if (_EnableHair > 0.5 && TrySampleEquip(headUVSample.rg, _HairRect, _HairTex, headEquipSample))
                {
                    alphaSample = max(alphaSample, 1.0);
                }
                if (_EnableBeard > 0.5 && TrySampleEquip(headUVSample.rg, _BeardRect, _BeardTex, headEquipSample))
                {
                    alphaSample = max(alphaSample, 1.0);
                }
                if (_EnableFaceAccessory > 0.5 && TrySampleEquip(headUVSample.rg, _FaceAccessoryRect, _FaceAccessoryTex, headEquipSample))
                {
                    alphaSample = max(alphaSample, 1.0);
                }
                if (_EnableMask > 0.5 && TrySampleEquip(headUVSample.rg, _MaskRect, _MaskTex, headEquipSample))
                {
                    alphaSample = max(alphaSample, 1.0);
                }

                return alphaSample;
            }

            // Mode0 阴影用的 caster alpha：本体 + 身体装备 + 头部装备 + 武器（不含背包）
            // 注意：当某个武器槽位使用序列帧(_WeaponXIsSequence > 0.5)时，
            // 视为悬空效果，不计入阴影宽度，仅静态武器参与阴影计算。
            float GetShadowCasterAlphaAtFrameUV(float2 frameUVSample, float2 frameMin, float2 frameMax)
            {
                // 将帧内 UV 映射回整张 _MainTex 的 UV
                float2 uvSample = lerp(frameMin, frameMax, frameUVSample);

                // 1）底图 alpha 直接来自 _MainTex（角色本体轮廓）
                fixed4 baseColorSample = tex2D(_MainTex, uvSample);
                float alphaSample = baseColorSample.a;

                // 2）身体装备轮廓（裤子 / 上衣 / 披风）
                fixed4 bodyUVSample = tex2D(_BodyUVMap, uvSample);
                fixed3 equipSample;

                if (_EnablePants > 0.5 && TrySampleEquip(bodyUVSample.rg, _PantsRect, _PantsTex, equipSample))
                {
                    alphaSample = max(alphaSample, 1.0);
                }
                if (_EnableCloth > 0.5 && TrySampleEquip(bodyUVSample.rg, _ClothRect, _ClothTex, equipSample))
                {
                    alphaSample = max(alphaSample, 1.0);
                }
                if (_EnableCloak > 0.5 && TrySampleEquip(bodyUVSample.rg, _CloakRect, _CloakTex, equipSample))
                {
                    alphaSample = max(alphaSample, 1.0);
                }

                // 3）头部装备轮廓
                fixed4 headUVSample = tex2D(_HeadUVMap, uvSample);
                fixed3 headEquipSample;

                if (_EnableHelmet > 0.5 && TrySampleEquip(headUVSample.rg, _HelmetRect, _HelmetTex, headEquipSample))
                {
                    alphaSample = max(alphaSample, 1.0);
                }
                if (_EnableHair > 0.5 && TrySampleEquip(headUVSample.rg, _HairRect, _HairTex, headEquipSample))
                {
                    alphaSample = max(alphaSample, 1.0);
                }
                if (_EnableBeard > 0.5 && TrySampleEquip(headUVSample.rg, _BeardRect, _BeardTex, headEquipSample))
                {
                    alphaSample = max(alphaSample, 1.0);
                }
                if (_EnableFaceAccessory > 0.5 && TrySampleEquip(headUVSample.rg, _FaceAccessoryRect, _FaceAccessoryTex, headEquipSample))
                {
                    alphaSample = max(alphaSample, 1.0);
                }
                if (_EnableMask > 0.5 && TrySampleEquip(headUVSample.rg, _MaskRect, _MaskTex, headEquipSample))
                {
                    alphaSample = max(alphaSample, 1.0);
                }

                // 4）叠加武器 alpha（不区分前后，只要有像素就记为不透明，以获得全局阴影宽度）
                //     但仅静态武器参与阴影；序列帧武器一般为悬空特效，不参与阴影宽度计算。
                fixed4 weapon0ColorSample = fixed4(0, 0, 0, 0);
                fixed4 weapon1ColorSample = fixed4(0, 0, 0, 0);
                bool hasWeapon0Sample = (_Weapon0IsSequence < 0.5) && TrySampleWeapon0(uvSample, weapon0ColorSample);
                bool hasWeapon1Sample = (_Weapon1IsSequence < 0.5) && TrySampleWeapon1(uvSample, weapon1ColorSample);

                if (hasWeapon0Sample)
                    alphaSample = max(alphaSample, weapon0ColorSample.a);
                if (hasWeapon1Sample)
                    alphaSample = max(alphaSample, weapon1ColorSample.a);

                return alphaSample;
            }

            // 武器专用轮廓 alpha（主手/副手任何有像素即视为占据轮廓）
            // 注意：当某个槽位使用序列帧(_WeaponXIsSequence > 0.5)时，不参与程序描边，
            // 只作为成品图像显示，避免与美术自带描边叠加。
            float GetWeaponOutlineAlphaAtFrameUV_Slot(float2 frameUVSample, float2 frameMin, float2 frameMax, int slot)
            {
                float2 uvSample = lerp(frameMin, frameMax, frameUVSample);
                fixed4 weaponColorSample = fixed4(0, 0, 0, 0);
                float resultAlpha = 0.0;

                // 只要任意一把武器开启了“在身体部分隐藏描边”，
                // 且当前采样点属于角色本体/头部区域，则不对该位置生成武器程序描边。
                bool blockWeaponOutline = false;
                float hideOnBody = max(_Weapon0HideOutlineOnBody, _Weapon1HideOutlineOnBody);
                if (hideOnBody > 0.5)
                {
                    float bodyAlpha = GetOutlineAlphaAtFrameUV(frameUVSample, frameMin, frameMax);
                    blockWeaponOutline = (bodyAlpha > CUTOFF);
                }

                if (!blockWeaponOutline)
                {
                    if (slot == 0)
                    {
                        // 主手：仅当不是序列帧时参与轮廓
                        if (_Weapon0IsSequence < 0.5)
                        {
                            if (TrySampleWeapon0(uvSample, weaponColorSample))
                                resultAlpha = weaponColorSample.a;
                        }
                    }
                    else
                    {
                        // 副手：仅当不是序列帧时参与轮廓
                        if (_Weapon1IsSequence < 0.5)
                        {
                            if (TrySampleWeapon1(uvSample, weaponColorSample))
                                resultAlpha = weaponColorSample.a;
                        }
                    }
                }

                return resultAlpha;
            }

            float GetWeaponOutlineAlphaAtFrameUV(float2 frameUVSample, float2 frameMin, float2 frameMax)
            {
                float a0 = GetWeaponOutlineAlphaAtFrameUV_Slot(frameUVSample, frameMin, frameMax, 0);
                float a1 = GetWeaponOutlineAlphaAtFrameUV_Slot(frameUVSample, frameMin, frameMax, 1);
                return max(a0, a1);
            }

            // 背包专用轮廓 alpha（通过 BodyUVMap 采样 BagRect）
            float GetBagOutlineAlphaAtFrameUV(float2 frameUVSample, float2 frameMin, float2 frameMax)
            {
                if (_EnableBag < 0.5) return 0.0;
                float2 uvSample = lerp(frameMin, frameMax, frameUVSample);
                fixed4 bodyUVSample = tex2D(_BodyUVMap, uvSample);
                fixed3 bagSample;
                if (TrySampleEquip(bodyUVSample.rg, _BagRect, _BagTex, bagSample))
                    return 1.0;
                return 0.0;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 baseColor = tex2D(_MainTex, i.uv);

                // 使用 Sprite 的 UV 直接采样 UV Map（运行时 UVMap 与角色 spritesheet 共享布局）
                float2 uvFrame = i.uv;
                fixed4 bodyUV = tex2D(_BodyUVMap, uvFrame);
                fixed4 headUV = tex2D(_HeadUVMap, uvFrame);

                // 将顶点 UV（整张角色贴图坐标）映射到"当前帧"的局部 UV（0~1，左下为原点）
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

                // 调试模式 6：显示核心区域（BodyUVMap.a 和 HeadUVMap.a）
                // 绿色=核心躯干, 青色=核心头部, 黄色=两者重叠
                if (_DebugMode > 5.5 && _DebugMode < 6.5)
                {
                    fixed4 debugColor = baseColor;
                    bool isCoreTorso = bodyUV.a > 0.5;
                    bool isCoreHead = parts.isHead && headUV.a > 0.5;
                    
                    if (isCoreTorso && isCoreHead)
                        debugColor.rgb = fixed3(1.0, 1.0, 0.0); // 黄色：重叠
                    else if (isCoreTorso)
                        debugColor.rgb = fixed3(0.0, 1.0, 0.0); // 绿色：核心躯干
                    else if (isCoreHead)
                        debugColor.rgb = fixed3(0.0, 1.0, 1.0); // 青色：核心头部
                    
                    return debugColor;
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

                // ========== 肤色映射替换：基于颜色数组查表 ==========
                // 只限定在主体贴图 SRC_MAIN 上：避免装备/武器被误改色
                if (_SkinPaletteEnabled > 0.5 && srcId == SRC_MAIN && charColor.a > CUTOFF)
                {
                    // 先过滤近黑像素（描边/线稿），与 Editor 侧一致：gray < 0.15 视为非肤色
                    float gray = dot(charColor.rgb, float3(0.299, 0.587, 0.114));
                    if (gray > 0.15)
                    {
                        int colorCount = (int)_SkinColorCount;
                        colorCount = clamp(colorCount, 0, MAX_SKIN_COLORS);

                        if (colorCount > 0)
                        {
                            fixed3 src = charColor.rgb;

                            // 只在当前像素颜色与某条源颜色“几乎完全相等”时才进行替换，
                            // 避免将其他非肤色像素拉到最近的肤色上。
                            // 这里使用一个非常小的阈值（约 1/255），等价于“严格相等”但允许浮点误差。
                            const float SKIN_COLOR_EPS = 1.0 / 255.0;

                            for (int i = 0; i < MAX_SKIN_COLORS; i++)
                            {
                                if (i >= colorCount)
                                    break;

                                fixed3 srcColor = _SkinSrcColors[i].rgb;

                                if (ColorApproxEqual(src, srcColor, SKIN_COLOR_EPS))
                                {
                                    charColor.rgb = _SkinDstColors[i].rgb;
                                    break;
                                }
                            }
                        }
                    }
                }

                // 手脚区域判断（直接使用预计算结果）
                bool isAnyFoot = parts.isLeftFoot || parts.isRightFoot;

                // ========== 第二步：采样武器 ==========
                fixed4 weapon0Color = fixed4(0, 0, 0, 0);
                fixed4 weapon1Color = fixed4(0, 0, 0, 0);
                bool hasWeapon0 = TrySampleWeapon0(i.uv, weapon0Color);
                bool hasWeapon1 = TrySampleWeapon1(i.uv, weapon1Color);

                // ========== 第三步：根据 DepthMode 和是否手/脚/配置合成最终颜色 ==========
                fixed4 finalColor = charColor;
                float finalAlpha = charAlpha;

                // 朝北武器（在角色后面）：有角色像素时由角色遮挡；无角色像素时只显示武器
                if (hasWeapon0 && _Weapon0DepthMode < 0.5)
                {
                    if (charAlpha <= CUTOFF)
                    {
                        finalColor.rgb = weapon0Color.rgb;
                        srcId = SRC_WEAPON0;
                    }
                    finalAlpha = max(finalAlpha, weapon0Color.a);
                }
                if (hasWeapon1 && _Weapon1DepthMode < 0.5)
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
                    if (!handBlocksW1)
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
                    if (!handBlocksW0)
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

                // ========== 程序描边 ==========
                // 使用 GetLayerPriority 函数判断层级，不在此处重复写规则
                // 层级规则全部集中在 GetLayerPriority 里
                // 颜色优先级：Hit > Extra > 默认黑
                bool hitOutlineRequested   = _HitOutline > 0.5;
                bool extraOutlineRequested = _ExtraOutlineEnabled > 0.5;

                float2 step = 1.0 / _FrameSize;
                bool isOutline = false;

                // 各层的 center alpha（只用于判断当前像素是否属于某层的“内部”）
                float centerWeaponAlpha = GetWeaponOutlineAlphaAtFrameUV(frameUV, frameMin, frameMax);
                float centerBodyAlpha   = GetOutlineAlphaAtFrameUV(frameUV, frameMin, frameMax);
                float centerBagAlpha    = GetBagOutlineAlphaAtFrameUV(frameUV, frameMin, frameMax);

                // 基线之上才考虑描边；是否可画由各层 center alpha + 层级优先级控制
                bool aboveBaseline = frameUV.y + 0.5 * step.y >= _ShadowBaseY;
                
                // 当前像素的优先级
                float currentPriority = GetLayerPriority(srcId);

                if (aboveBaseline)
                {
                    float2 neighbors[4];
                    neighbors[0] = frameUV + float2(step.x, 0);
                    neighbors[1] = frameUV + float2(-step.x, 0);
                    neighbors[2] = frameUV + float2(0, step.y);
                    neighbors[3] = frameUV + float2(0, -step.y);

                    float bestPriority = -1.0;
                    
                    // 检查每个邻居，找出优先级最高的可画描边
                    [unroll]
                    for (int i = 0; i < 4; i++)
                    {
                        float2 n = neighbors[i];
                        
                        // 检查武器：若当前像素本身属于身体/头部，且配置了“在身体部分隐藏描边”，
                        // 则完全跳过武器程序描边，只保留主体/背包描边。
                        bool hideWeaponOutlineOnBody = (_Weapon0HideOutlineOnBody > 0.5 || _Weapon1HideOutlineOnBody > 0.5);
                        bool blockWeaponOutlineAtCenter = hideWeaponOutlineOnBody && (centerBodyAlpha > CUTOFF);

                        if (!blockWeaponOutlineAtCenter && centerWeaponAlpha <= CUTOFF)
                        {
                            float aW0 = GetWeaponOutlineAlphaAtFrameUV_Slot(n, frameMin, frameMax, 0);
                            float aW1 = GetWeaponOutlineAlphaAtFrameUV_Slot(n, frameMin, frameMax, 1);

                            if (aW0 > CUTOFF || aW1 > CUTOFF)
                            {
                                // 如果邻居也有“更前”的层，武器描边应该被挡住：
                                // - 朝北时：身体/背包的优先级都高于武器 → 阻挡武器描边
                                // - 朝南时：身体优先级低于武器，背包优先级为 0 → 不阻挡
                                float neighborBodyAlpha = GetOutlineAlphaAtFrameUV(n, frameMin, frameMax);
                                float bodyPriority = GetLayerPriority(SRC_MAIN);

                                float neighborBagAlpha = GetBagOutlineAlphaAtFrameUV(n, frameMin, frameMax);
                                float bagPriority = GetLayerPriority(SRC_BAG);

                                if (aW0 > CUTOFF)
                                {
                                    float weaponPriority = GetLayerPriority(SRC_WEAPON0);
                                    bool weaponBlockedByBody = (neighborBodyAlpha > CUTOFF) && (bodyPriority > weaponPriority);
                                    bool weaponBlockedByBag  = (neighborBagAlpha  > CUTOFF) && (bagPriority  > weaponPriority);

                                    if (!weaponBlockedByBody && !weaponBlockedByBag &&
                                        weaponPriority >= currentPriority && weaponPriority > bestPriority)
                                    {
                                        bestPriority = weaponPriority;
                                    }
                                }

                                if (aW1 > CUTOFF)
                                {
                                    float weaponPriority = GetLayerPriority(SRC_WEAPON1);
                                    bool weaponBlockedByBody = (neighborBodyAlpha > CUTOFF) && (bodyPriority > weaponPriority);
                                    bool weaponBlockedByBag  = (neighborBagAlpha  > CUTOFF) && (bagPriority  > weaponPriority);

                                    if (!weaponBlockedByBody && !weaponBlockedByBag &&
                                        weaponPriority >= currentPriority && weaponPriority > bestPriority)
                                    {
                                        bestPriority = weaponPriority;
                                    }
                                }
                            }
                        }
                        
                        // 检查人本体
                        if (centerBodyAlpha <= CUTOFF)
                        {
                            float aB = GetOutlineAlphaAtFrameUV(n, frameMin, frameMax);
                            if (aB > CUTOFF)
                            {
                                // 检查邻居是否是手部区域
                                float2 nUV = lerp(frameMin, frameMax, n);
                                fixed4 nBodyUV = tex2D(_BodyUVMap, nUV);
                                float neighborBodyPartID = nBodyUV.b;
                                bool neighborIsHand = IsPartID(neighborBodyPartID, ID_LEFTHAND) ||
                                                      IsPartID(neighborBodyPartID, ID_RIGHTHAND);

                                // 手部像素如果落在“实际头部(core head)”或“实际身体(core torso)”区域内，
                                // 则不让它参与程序描边（避免在身体/头部内部产生多余描边）。
                                // - core head 由 HeadUVMap.a 标记（alpha>0.5）
                                // - core torso 由 BodyUVMap.a 标记（alpha>0.5）
                                fixed4 nHeadUV = tex2D(_HeadUVMap, nUV);
                                bool neighborHandInCoreHead = neighborIsHand && IsPartID(nHeadUV.b, ID_HEAD) && (nHeadUV.a > 0.5);
                                bool neighborHandInCoreTorso = neighborIsHand && (nBodyUV.a > 0.5);
                                bool blockHandOutlineByCoreRegion = neighborHandInCoreHead || neighborHandInCoreTorso;
                                
                                // 如果邻居是手部，且当前像素有武器颜色，不画手部描边（避免遮挡武器）
                                bool handOutlineBlockedByWeapon = neighborIsHand &&
                                                                  (srcId == SRC_WEAPON0 || srcId == SRC_WEAPON1);
                                
                                if (!handOutlineBlockedByWeapon && !blockHandOutlineByCoreRegion)
                                {
                                    float bodyPriority = GetLayerPriority(SRC_MAIN);
                                    if (bodyPriority >= currentPriority && bodyPriority > bestPriority)
                                    {
                                        bestPriority = bodyPriority;
                                    }
                                }
                            }
                        }
                        
                        // 检查背包
                        if (centerBagAlpha <= CUTOFF)
                        {
                            float aBag = GetBagOutlineAlphaAtFrameUV(n, frameMin, frameMax);
                            if (aBag > CUTOFF)
                            {
                                float bagPriority = GetLayerPriority(SRC_BAG);
                                if (bagPriority >= currentPriority && bagPriority > bestPriority)
                                {
                                    bestPriority = bagPriority;
                                }
                            }
                        }
                    }
                    
                    isOutline = (bestPriority >= 0);
                }

                if (isOutline)
                {
                    // 默认黑色描边可开关；但 Hit/Extra 请求时仍然强制绘制
                    bool defaultOutlineRequested = (_DefaultOutlineEnabled > 0.5);
                    bool shouldDrawOutline = hitOutlineRequested || extraOutlineRequested || defaultOutlineRequested;

                    if (shouldDrawOutline)
                    {
                        fixed3 outlineColor = fixed3(0.0, 0.0, 0.0);
                        if (hitOutlineRequested)
                        {
                            outlineColor = _HitOutlineColor.rgb;
                        }
                        else if (extraOutlineRequested)
                        {
                            outlineColor = _ExtraOutlineColor.rgb;
                        }

                        finalColor.rgb = outlineColor;
                        finalAlpha = 1.0;
                        finalColor.a = finalAlpha;
                    }
                }

                // ========== 眼部装饰（贴图方式，在角色和武器之上、阴影之前）==========
                if (_EnableEyeDeco > 0.5 && parts.isHead)
                {
                    fixed3 eyeDecoSample;
                    if (TrySampleEquip(headUV.rg, _EyeDecoRect, _EyeDecoTex, eyeDecoSample))
                    {
                        finalColor.rgb = eyeDecoSample;
                    }
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
