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
        _EnableGloves ("Enable Gloves", Float) = 0
        _EnableShoes ("Enable Shoes", Float) = 0
        _EnableLeftEye ("Enable Left Eye", Float) = 0
        _EnableRightEye ("Enable Right Eye", Float) = 0
        _BodyInFront ("Body In Front", Float) = 0
        
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
            #include "Includes/PixelUtils.cginc"
            #include "Includes/PixelShadow.cginc"
            
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
            float _EnableGloves;
            float _EnableShoes;
            float _EnableLeftEye;
            float _EnableRightEye;
            float _BodyInFront;
            float _DebugMode;
            
            // 阴影参数
            float _ShadowEnabled;
            fixed4 _ShadowColor;
            float _ShadowMode;
            float _ShadowLeftX;
            float _ShadowRightX;
            float _ShadowCenterX;
            float _ShadowBaseY;
            
            fixed4 _Color;
            
            // TransformUV 已定义在 PixelUtils.cginc 中
            // 像素风格：将 alpha 大于阈值的像素视为实心像素
            static const float CUTOFF = 0.5;
            
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
                return abs(id - target) < 0.05;
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
            
            // 通用贴图采样：采样成功返回 true 并写入颜色
            // 注意：HLSL 不支持 swizzle 作为 inout 参数，改用 out 参数
            bool TrySampleEquip(float2 uv, float4 rect, sampler2D tex, out fixed3 outColor)
            {
                float2 coord = TransformUV(uv, rect);
                fixed4 c = tex2D(tex, coord);
                outColor = c.rgb;
                if (c.a > CUTOFF)
                    return true;
                return false;
            }

            // 通用武器采样函数（支持主手/副手）
            // 说明：
            // - anchorFrameUV.xy：角色帧内的手点 UV（0~1），由 C# 根据 AnchorPoint.position/frameSize 计算
            // - anchorFrameUV.zw：武器贴图中的“虚拟左手”局部 UV（0~1），作为旋转/镜像的 pivot
            // - rotCosSin：武器局部坐标的旋转（cos,sin），围绕虚拟左手进行旋转
            // - flipX：是否对武器贴图做“绕虚拟左手的水平镜像”。
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

                // 相对于角色帧中“手点”的偏移（帧内局部 UV 空间）
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
            
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }
            
            // ------------------------------------------------------------
            // 身体层 / 头部层的合成拆分为两个函数，方便独立开关与调试
            // ------------------------------------------------------------
            // isHeadCore: 真实头部区域（headUV.b=ID_HEAD 且 headUV.a>0.5）
            // _BodyInFront 影响两件事：
            // - Torso(衣服/裤子/斗篷) 与“核心头部”的前后关系：
            //   * 朝南 (_BodyInFront < 0.5): 核心头部前置，Torso 在核心头部后面
            //   * 朝北 (_BodyInFront > 0.5): Torso 可以覆盖头部
            // - 手脚与衣服的前后关系：
            //   * 朝南: 手脚在衣服前（显示手套/鞋子）
            //   * 朝北: 手脚也视为 Torso 区域，由衣服/斗篷覆盖
            void ApplyBodyLayers(fixed4 bodyUV, PartIDs parts, bool isHeadCore, inout fixed4 ioColor, out float bodyLayerAlpha)
            {
                bodyLayerAlpha = 0;

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
                    // 裤子（最底层）-> 服装 -> 斗篷（最上层）
                    if (_EnablePants > 0.5 && TrySampleEquip(bodyUV.rg, _PantsRect, _PantsTex, sampled))
                    {
                        ioColor.rgb = sampled;
                        bodyLayerAlpha = 1.0;
                    }
                    if (_EnableCloth > 0.5 && TrySampleEquip(bodyUV.rg, _ClothRect, _ClothTex, sampled))
                    {
                        ioColor.rgb = sampled;
                        bodyLayerAlpha = 1.0;
                    }
                    if (_EnableCloak > 0.5 && TrySampleEquip(bodyUV.rg, _CloakRect, _CloakTex, sampled))
                    {
                        ioColor.rgb = sampled;
                        bodyLayerAlpha = 1.0;
                    }
                }
                else if ((parts.isLeftHand || parts.isRightHand) && _EnableGloves > 0.5)
                {
                    // 只有朝南时才会走到这里；朝北时手脚已被视为 Torso 覆盖
                    ioColor.rgb = parts.isLeftHand ? _LeftHandColor.rgb : _RightHandColor.rgb;
                    bodyLayerAlpha = 1.0;
                }
                else if ((parts.isLeftFoot || parts.isRightFoot) && _EnableShoes > 0.5)
                {
                    ioColor.rgb = parts.isLeftFoot ? _LeftFootColor.rgb : _RightFootColor.rgb;
                    bodyLayerAlpha = 1.0;
                }
                else if (parts.isLeftEye && _EnableLeftEye > 0.5)
                {
                    ioColor.rgb = _LeftEyeColor.rgb;
                    bodyLayerAlpha = 1.0;
                }
                else if (parts.isRightEye && _EnableRightEye > 0.5)
                {
                    ioColor.rgb = _RightEyeColor.rgb;
                    bodyLayerAlpha = 1.0;
                }
            }

            // 头部层顺序：头发（底层）-> 面部装饰 -> 胡子 -> 头盔（顶层）
            void ApplyHeadLayers(float2 baseHeadUV, bool isHead, inout fixed4 ioColor, out float headLayerAlpha)
            {
                headLayerAlpha = 0;
                if (!isHead) return;

                fixed3 sampled;
                // 头盔（顶层）：命中则提前返回
                if (_EnableHelmet > 0.5 && TrySampleEquip(baseHeadUV, _HelmetRect, _HelmetTex, sampled))
                {
                    ioColor.rgb = sampled;
                    headLayerAlpha = 1.0;
                    return;
                }

                // 胡子 -> 面部装饰 -> 头发（从上到下层级）
                bool wrote = false;
                if (_EnableBeard > 0.5 && TrySampleEquip(baseHeadUV, _BeardRect, _BeardTex, sampled))
                {
                    ioColor.rgb = sampled;
                    wrote = true;
                }
                if (!wrote && _EnableFaceAccessory > 0.5 && TrySampleEquip(baseHeadUV, _FaceAccessoryRect, _FaceAccessoryTex, sampled))
                {
                    ioColor.rgb = sampled;
                    wrote = true;
                }
                if (!wrote && _EnableHair > 0.5 && TrySampleEquip(baseHeadUV, _HairRect, _HairTex, sampled))
                {
                    ioColor.rgb = sampled;
                    wrote = true;
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

                // 头部层（头发/饰品/胡子/头盔）
                float headLayerAlpha = 0;

                if (_BodyInFront > 0.5)
                {
                    // 身体在前：先绘制头部，再绘制身体（衣服/斗篷可以盖住头）
                    ApplyHeadLayers(headUV.rg, parts.isHead, charColor, headLayerAlpha);
                    ApplyBodyLayers(bodyUV, parts, isHeadCore, charColor, bodyLayerAlpha);
                }
                else
                {
                    // 身体在后：先绘制身体，再绘制头部（头部始终在衣服前）
                    ApplyBodyLayers(bodyUV, parts, isHeadCore, charColor, bodyLayerAlpha);
                    ApplyHeadLayers(headUV.rg, parts.isHead, charColor, headLayerAlpha);
                }

                // 角色最终 alpha：包含底图 + 身体层 + 头部层
                float charAlpha = max(charColor.a, max(bodyLayerAlpha, headLayerAlpha));

                // 手脚区域判断（直接使用预计算结果）
                bool isAnyFoot = parts.isLeftFoot || parts.isRightFoot;

                // ========== 第二步：采样武器 ==========
                fixed4 weapon0Color, weapon1Color;
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
                    }
                    finalAlpha = max(finalAlpha, weapon0Color.a);
                }
                if (hasWeapon1 && _Weapon1DepthMode < 0.5)
                {
                    if (charAlpha <= CUTOFF)
                    {
                        finalColor.rgb = weapon1Color.rgb;
                    }
                    finalAlpha = max(finalAlpha, weapon1Color.a);
                }

                // 朝南武器（在角色前面）：脚始终在所有武器前面；手是否在前由每个武器的 HandInFront 配置决定。
                bool isAnyHand = parts.isLeftHand || parts.isRightHand;

                // 副手先画（在主手后面）
                if (hasWeapon1 && _Weapon1DepthMode > 0.5)
                {
                    bool handBlocksW1 = isAnyHand && (_Weapon1HandInFront > 0.5);
                    if (!handBlocksW1)
                    {
                        finalColor.rgb = weapon1Color.rgb;
                        finalAlpha = max(finalAlpha, weapon1Color.a);
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
                    }
                }

                finalColor.a = finalAlpha;

                // ========== 像素级阴影系统 ==========
                if (finalAlpha <= CUTOFF)
                {
                    fixed4 shadowColor = SamplePixelShadow(
                        i.uv, _MainTex,
                        _ShadowMode,
                        _ShadowLeftX, _ShadowRightX,
                        _ShadowCenterX, _ShadowBaseY,
                        _ShadowColor, _ShadowEnabled);
                    
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
