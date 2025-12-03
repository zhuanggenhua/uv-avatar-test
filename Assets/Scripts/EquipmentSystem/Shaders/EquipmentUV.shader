// 装备系统 - 双层 UV Map
// 身体层：服装 / 手套 / 鞋子（BodyUVMap）
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
            sampler2D _CloakTex;
            sampler2D _HairTex;
            sampler2D _FaceAccessoryTex;
            sampler2D _BeardTex;
            sampler2D _HelmetTex;
            
            // ============================================================
            // 每个装备贴图在纹理中的 Sprite Rect（minU, minV, maxU, maxV）
            // ============================================================
            // 重要：Sprite.texture 始终指向整张源纹理，而不是切片后的小图！
            // - 如果 sprite 来自 spritesheet，texture 指向整张 spritesheet
            // - 如果 sprite 使用 Sprite Atlas 打包，texture 指向打包后的图集
            // - 如果 sprite 是单独图片，texture 就是该图片本身（Rect 为 0,0,1,1）
            //
            // 因此在 Shader 中采样时，必须使用 sprite.rect 计算出正确的 UV 范围。
            // C# 侧会把这些 Rect 传进来，这里通过 TransformUV() 把 0~1 的局部 UV
            // 映射到纹理上的实际区域。
            // ============================================================
            float4 _ClothRect;   // 服装贴图在纹理中的 Rect
            float4 _CloakRect;   // 斗篷贴图在纹理中的 Rect
            float4 _HairRect;          // 头发贴图在纹理中的 Rect
            float4 _FaceAccessoryRect; // 面部装饰贴图在纹理中的 Rect
            float4 _BeardRect;          // 胡子贴图在纹理中的 Rect
            float4 _HelmetRect;         // 头盔贴图在纹理中的 Rect
            
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
                    // 服装（底层）-> 斗篷（上层）
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

                // 计算真实头部区域：headUV 标记为 HEAD 且角色本体有像素，且不在身体区域
                // 如果同时是 HEAD 和 TORSO（重叠区域），优先渲染衣服
                bool isHeadCore = IsPartID(headPartID, ID_HEAD) && baseColor.a > CUTOFF && !IsPartID(bodyPartID, ID_TORSO);

                // 应用身体层装备（真实头部区域不渲染衣服）
                float bodyLayerAlpha;
                ApplyBodyLayers(bodyUV, isHeadCore, finalColor, bodyLayerAlpha);

                // 应用头部层装备
                float headLayerAlpha;
                ApplyHeadLayers(headUV.rg, headPartID, finalColor, headLayerAlpha);

                // 在扩展区域（原 baseColor.a = 0）使用装备层的 alpha
                finalColor.a = max(finalColor.a, max(bodyLayerAlpha, headLayerAlpha));

                return finalColor * i.color;
            }
            ENDCG
        }
    }
    
    Fallback "Sprites/Default"
}
