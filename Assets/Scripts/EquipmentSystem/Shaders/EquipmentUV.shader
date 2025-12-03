// 装备系统 - 双层 UV Map
// 身体层：服装 / 裤子 / 手套 / 鞋子（BodyUVMap）
// 头部层：头发 -> 胡子 -> 头盔（HeadUVMap，叠加在身体层之上）
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
        _Weapon0PivotUV ("Weapon0 Pivot UV (Right Hand)", Vector) = (0.5, 0.5, 0, 0)
        _Weapon0FlipX ("Main Hand Flip X", Float) = 0
        _Weapon0DepthMode ("Main Hand Depth Mode", Float) = 0
        _Weapon0Enabled ("Enable Main Hand Weapon", Float) = 0
        
        [Header(Weapon Off Hand)]
        _Weapon1Tex ("Off Hand Weapon Texture", 2D) = "white" {}
        _Weapon1Rect ("Off Hand Weapon Rect", Vector) = (0,0,1,1)
        _Weapon1AnchorFrameUV ("Weapon1 Anchor Frame UV", Vector) = (0.5, 0.5, 0, 0)
        _Weapon1RotCosSin ("Weapon1 Rot Cos/Sin", Vector) = (1, 0, 0, 0)
        _Weapon1PivotUV ("Weapon1 Pivot UV (Left Hand)", Vector) = (0.5, 0.5, 0, 0)
        _Weapon1FlipX ("Off Hand Flip X", Float) = 0
        _Weapon1DepthMode ("Off Hand Depth Mode", Float) = 0
        _Weapon1Enabled ("Enable Off Hand Weapon", Float) = 0
        
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
        
        [Header(Debug)]
        // 调试模式：0=关闭，1=身体层区域，2=头部层区域，3=装备采样结果，4=UVMap原始UV，5=顶点UV
        _DebugMode ("Debug Mode", Float) = 0
        
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
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            
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
            float4 _Weapon0PivotUV;
            float _Weapon0FlipX;
            float _Weapon0DepthMode;
            float _Weapon0Enabled;
            
            // 副手武器参数
            float4 _Weapon1Rect;
            float4 _Weapon1AnchorFrameUV;
            float4 _Weapon1RotCosSin;
            float4 _Weapon1PivotUV;
            float _Weapon1FlipX;
            float _Weapon1DepthMode;
            float _Weapon1Enabled;
            
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
            float _DebugMode;
            
            fixed4 _Color;
            
            // 将 0~1 的局部 UV 转换为纹理上的实际 UV（根据 Sprite Rect 映射）
            // 对于从 spritesheet 或图集中切出来的 Sprite，这一步是必不可少的。
            // 
            // rect: (minU, minV, maxU, maxV) 表示该 Sprite 在整张纹理 UV 空间中的区域
            // uv:   0~1 的局部 UV（来自 UV Map，表示在装备贴图内的位置）
            // 返回值：可以直接用于采样纹理的实际 UV 坐标
            //
            // 示例：Sprite 在 512x512 纹理中的 UV Rect 为 (0.25, 0.5, 0.5, 0.75)
            //   输入 uv (0,0) -> 输出 (0.25, 0.5)   = Sprite 左下角
            //   输入 uv (1,1) -> 输出 (0.5, 0.75)  = Sprite 右上角
            float2 TransformUV(float2 uv, float4 rect)
            {
                return float2(
                    lerp(rect.x, rect.z, uv.x),
                    lerp(rect.y, rect.w, uv.y)
                );
            }
            
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
            // - anchorFrameUV：角色帧内的锚点 UV（0~1），由 C# 根据 AnchorPoint.position/frameSize 计算
            // - rotCosSin：武器局部坐标的旋转（目前可以保持为 (1,0) 即不旋转，或用于特殊武器）
            // - pivotUV：武器贴图内的握点 UV（0~1），基于 UV 底图手部位置计算
            // - flipX：是否对武器贴图做“中线镜像”（u -> 1-u）。不再围绕锚点翻转 offset。
            bool TrySampleWeaponGeneric(float2 mainUV, sampler2D weaponTex, float4 weaponRect,
                float4 anchorFrameUV, float4 rotCosSin, float4 pivotUV, float flipX, float enabled, out fixed4 outColor)
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

                // 相对于锚点的偏移（帧内局部 UV 空间）
                float2 anchorUV = anchorFrameUV.xy;
                float2 offset = frameUV - anchorUV;

                // 旋转（围绕锚点，当前主要用于特殊武器；普通武器可保持 rotCosSin=(1,0)）
                float rotCos = rotCosSin.x;
                float rotSin = rotCosSin.y;
                float2 rotated;
                rotated.x = offset.x * rotCos - offset.y * rotSin;
                rotated.y = offset.x * rotSin + offset.y * rotCos;

                // 转换到武器贴图的局部 UV，使用传入的握点 pivot 作为原点
                float2 pivot = pivotUV.xy;
                float2 weaponLocalUV = rotated + pivot;

                // 水平中线镜像：用于从 SE 贴图生成 SW/NW 方向
                if (flipX > 0.5)
                    weaponLocalUV.x = 1.0 - weaponLocalUV.x;

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
                    _Weapon0AnchorFrameUV, _Weapon0RotCosSin, _Weapon0PivotUV, _Weapon0FlipX, _Weapon0Enabled, outColor);
            }
            
            // 副手武器采样
            bool TrySampleWeapon1(float2 mainUV, out fixed4 outColor)
            {
                return TrySampleWeaponGeneric(mainUV, _Weapon1Tex, _Weapon1Rect,
                    _Weapon1AnchorFrameUV, _Weapon1RotCosSin, _Weapon1PivotUV, _Weapon1FlipX, _Weapon1Enabled, outColor);
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
            // isHeadCore: 真实头部区域（headUV.b=ID_HEAD 且 baseColor.a>0）
            // 在真实头部区域，衣服不会覆盖角色本体的头
            void ApplyBodyLayers(fixed4 bodyUV, bool isHeadCore, inout fixed4 ioColor, out float bodyLayerAlpha)
            {
                float bodyPartID = bodyUV.b;
                bodyLayerAlpha = 0;

                // 真实头部区域：禁止 Torso 覆盖（衣服不能画在角色头上）
                if (isHeadCore && IsPartID(bodyPartID, ID_TORSO))
                    return;

                if (IsPartID(bodyPartID, ID_TORSO))
                {
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
                else if (IsPartID(bodyPartID, ID_LEFTHAND) && _EnableGloves > 0.5)
                {
                    ioColor.rgb = _LeftHandColor.rgb;
                    bodyLayerAlpha = 1.0;
                }
                else if (IsPartID(bodyPartID, ID_RIGHTHAND) && _EnableGloves > 0.5)
                {
                    ioColor.rgb = _RightHandColor.rgb;
                    bodyLayerAlpha = 1.0;
                }
                else if (IsPartID(bodyPartID, ID_LEFTFOOT) && _EnableShoes > 0.5)
                {
                    ioColor.rgb = _LeftFootColor.rgb;
                    bodyLayerAlpha = 1.0;
                }
                else if (IsPartID(bodyPartID, ID_RIGHTFOOT) && _EnableShoes > 0.5)
                {
                    ioColor.rgb = _RightFootColor.rgb;
                    bodyLayerAlpha = 1.0;
                }
                else if (IsPartID(bodyPartID, ID_LEFTEYE) && _EnableLeftEye > 0.5)
                {
                    ioColor.rgb = _LeftEyeColor.rgb;
                    bodyLayerAlpha = 1.0;
                }
                else if (IsPartID(bodyPartID, ID_RIGHTEYE) && _EnableRightEye > 0.5)
                {
                    ioColor.rgb = _RightEyeColor.rgb;
                    bodyLayerAlpha = 1.0;
                }
            }

            // 头部层顺序：头发（底层）-> 面部装饰 -> 胡子 -> 头盔（顶层）
            void ApplyHeadLayers(float2 baseHeadUV, float headPartID, inout fixed4 ioColor, out float headLayerAlpha)
            {
                headLayerAlpha = 0;
                if (!IsPartID(headPartID, ID_HEAD)) return;

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

                // 调试模式 1: 显示身体层区域
                if (_DebugMode > 0.5 && _DebugMode < 1.5)
                {
                    fixed4 debugColor = baseColor;
                    debugColor.a = baseColor.a;
                    if (IsPartID(bodyPartID, ID_TORSO))      debugColor.rgb = fixed3(0.3, 0.5, 0.9); // 蓝色
                    else if (IsPartID(bodyPartID, ID_LEFTHAND))  debugColor.rgb = fixed3(0.9, 0.9, 0.2); // 黄色
                    else if (IsPartID(bodyPartID, ID_RIGHTHAND)) debugColor.rgb = fixed3(0.9, 0.6, 0.2); // 橙色
                    else if (IsPartID(bodyPartID, ID_LEFTFOOT))  debugColor.rgb = fixed3(0.6, 0.3, 0.9); // 紫色
                    else if (IsPartID(bodyPartID, ID_RIGHTFOOT)) debugColor.rgb = fixed3(0.9, 0.3, 0.6); // 粉色
                    return debugColor;
                }

                // 调试模式 2: 显示头部层区域
                if (_DebugMode > 1.5 && _DebugMode < 2.5)
                {
                    fixed4 debugColor = baseColor;
                    debugColor.a = baseColor.a;
                    if (IsPartID(headPartID, ID_HEAD)) debugColor.rgb = fixed3(0.2, 0.8, 0.8); // 青色
                    return debugColor;
                }

                // 调试模式 3：显示装备采样结果（身体层=衣服，头部层=头盔）
                if (_DebugMode > 2.5 && _DebugMode < 3.5)
                {
                    if (IsPartID(bodyPartID, ID_TORSO))
                    {
                        float2 clothUVCoord = TransformUV(bodyUV.rg, _ClothRect);
                        fixed4 clothColor = tex2D(_ClothTex, clothUVCoord);
                        return fixed4(clothColor.rgb, 1);
                    }
                    if (IsPartID(headPartID, ID_HEAD))
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
                    if (IsPartID(bodyPartID, ID_TORSO))
                        return fixed4(bodyUV.r, bodyUV.g, 0, 1);
                    if (IsPartID(headPartID, ID_HEAD))
                        return fixed4(headUV.r, headUV.g, 0, 1);
                    return fixed4(0, 0, 0, baseColor.a);
                }

                // 调试模式 5：显示顶点原始 UV（i.uv）
                if (_DebugMode > 4.5 && _DebugMode < 5.5)
                {
                    return fixed4(i.uv.x, i.uv.y, 0, baseColor.a);
                }

                fixed4 finalColor = baseColor;

                // 预先采样双武器
                fixed4 weapon0Color, weapon1Color;
                bool hasWeapon0 = TrySampleWeapon0(i.uv, weapon0Color);
                bool hasWeapon1 = TrySampleWeapon1(i.uv, weapon1Color);

                // 计算真实头部区域
                bool isHeadCore = IsPartID(headPartID, ID_HEAD) && baseColor.a > CUTOFF && !IsPartID(bodyPartID, ID_TORSO);
                
                // 手脚区域判断（武器不会覆盖手脚）
                bool isHandOrFoot = IsPartID(bodyPartID, ID_LEFTHAND)
                                 || IsPartID(bodyPartID, ID_RIGHTHAND)
                                 || IsPartID(bodyPartID, ID_LEFTFOOT)
                                 || IsPartID(bodyPartID, ID_RIGHTFOOT);

                float bodyLayerAlpha = 0;

                // ========== 朝北武器（在身体后面）先画 ==========
                if (hasWeapon0 && _Weapon0DepthMode < 0.5)
                {
                    finalColor.rgb = weapon0Color.rgb;
                    bodyLayerAlpha = max(bodyLayerAlpha, weapon0Color.a);
                }
                if (hasWeapon1 && _Weapon1DepthMode < 0.5)
                {
                    finalColor.rgb = weapon1Color.rgb;
                    bodyLayerAlpha = max(bodyLayerAlpha, weapon1Color.a);
                }

                // 应用身体层装备
                ApplyBodyLayers(bodyUV, isHeadCore, finalColor, bodyLayerAlpha);

                // ========== 朝南武器（在身体前面，但手脚始终在武器前面）==========
                if (!isHandOrFoot)
                {
                    // 副手先画（在主手后面）
                    if (hasWeapon1 && _Weapon1DepthMode > 0.5)
                    {
                        finalColor.rgb = weapon1Color.rgb;
                        bodyLayerAlpha = max(bodyLayerAlpha, weapon1Color.a);
                    }
                    // 主手后画（在副手前面）
                    if (hasWeapon0 && _Weapon0DepthMode > 0.5)
                    {
                        finalColor.rgb = weapon0Color.rgb;
                        bodyLayerAlpha = max(bodyLayerAlpha, weapon0Color.a);
                    }
                }

                // 应用头部层装备
                float headLayerAlpha;
                ApplyHeadLayers(headUV.rg, headPartID, finalColor, headLayerAlpha);

                // 在扩展区域使用装备层的 alpha
                finalColor.a = max(finalColor.a, max(bodyLayerAlpha, headLayerAlpha));

                return finalColor * i.color;
            }
            ENDCG
        }
    }
    
    Fallback "Sprites/Default"
}
