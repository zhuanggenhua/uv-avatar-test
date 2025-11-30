// Dual UV Map Equipment System
// Body Layer: Clothing/Gloves/Shoes (BodyUVMap)
// Head Layer: Hair->Beard->Helmet (HeadUVMap) - Three layers rendered on top of body
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
        
        [Header(Head Layer Textures)]
        _HairTex ("Hair Texture", 2D) = "white" {}
        _BeardTex ("Beard Texture", 2D) = "white" {}
        _HelmetTex ("Helmet Texture", 2D) = "white" {}
        
        [Header(Glove Colors)]
        [HDR] _LeftHandColor ("Left Hand Color", Color) = (0.6, 0.4, 0.2, 1)
        [HDR] _RightHandColor ("Right Hand Color", Color) = (0.6, 0.4, 0.2, 1)
        
        [Header(Shoe Colors)]
        [HDR] _LeftFootColor ("Left Foot Color", Color) = (0.3, 0.2, 0.1, 1)
        [HDR] _RightFootColor ("Right Foot Color", Color) = (0.3, 0.2, 0.1, 1)
        
        [Header(Enable Layers)]
        _EnableHair ("Enable Hair", Float) = 0
        _EnableBeard ("Enable Beard", Float) = 0
        _EnableHelmet ("Enable Helmet", Float) = 0
        _EnableCloth ("Enable Clothing", Float) = 0
        _EnableGloves ("Enable Gloves", Float) = 0
        _EnableShoes ("Enable Shoes", Float) = 0
        
        [Header(Debug)]
        // Debug Mode: 0=Off, 1=Body regions, 2=Head regions, 3=UV sampling
        _DebugMode ("Debug Mode", Float) = 0
        
        _Color ("Tint", Color) = (1,1,1,1)
        
        // Legacy properties (deprecated)
        [HideInInspector] _UVMapTex ("UV Map", 2D) = "black" {}
        [HideInInspector] _HeadTex ("Head Texture", 2D) = "white" {}
        [HideInInspector] _EnableHead ("Enable Head", Float) = 0
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
                float2 uvRaw : TEXCOORD1;  // 原始顶点 UV
                float4 color : COLOR;
            };
            
            sampler2D _MainTex;
            sampler2D _BodyUVMap;
            sampler2D _HeadUVMap;
            sampler2D _ClothTex;
            sampler2D _HairTex;
            sampler2D _BeardTex;
            sampler2D _HelmetTex;
            float4 _MainTex_ST;
            
            // Unity provides texel size uniforms; declare to use them
            float4 _HelmetTex_TexelSize; // (1/width, 1/height, width, height)
            
            // ============================================================
            // Sprite Rect for each equipment texture (minU, minV, maxU, maxV)
            // ============================================================
            // IMPORTANT: Unity Sprite.texture returns the ENTIRE source texture!
            // - If sprite is cut from spritesheet, texture is the whole sheet
            // - If sprite uses Sprite Atlas, texture is the packed atlas
            // - If sprite is standalone, texture is just that image (rect = 0,0,1,1)
            //
            // Therefore, we MUST use sprite.rect to calculate correct UV coordinates.
            // The C# code passes these rects, and we use TransformUV() to convert
            // 0-1 UV coordinates to the actual sprite region in the texture.
            // ============================================================
            float4 _ClothRect;   // Clothing sprite rect in its texture
            float4 _HairRect;    // Hair sprite rect in its texture
            float4 _BeardRect;   // Beard sprite rect in its texture
            float4 _HelmetRect;  // Helmet sprite rect in its texture
            
            
            // Legacy properties
            sampler2D _UVMapTex;
            sampler2D _HeadTex;
            
            fixed4 _LeftHandColor;
            fixed4 _RightHandColor;
            fixed4 _LeftFootColor;
            fixed4 _RightFootColor;
            
            float _EnableHair;
            float _EnableBeard;
            float _EnableHelmet;
            float _EnableHead;
            float _EnableCloth;
            float _EnableGloves;
            float _EnableShoes;
            float _DebugMode;
            
            fixed4 _Color;
            
            // Transform 0-1 UV to actual sprite rect UV in the texture
            // This is ESSENTIAL for sprites cut from spritesheet or packed in atlas!
            // 
            // rect: (minU, minV, maxU, maxV) - the sprite's region in texture UV space
            // uv: 0-1 coordinates within the sprite (from UV Map)
            // returns: actual UV coordinates to sample the texture
            //
            // Example: sprite at rect (0.25, 0.5, 0.5, 0.75) in a 512x512 texture
            //   Input uv (0,0) -> output (0.25, 0.5)  = bottom-left of sprite
            //   Input uv (1,1) -> output (0.5, 0.75)  = top-right of sprite
            float2 TransformUV(float2 uv, float4 rect)
            {
                return float2(
                    lerp(rect.x, rect.z, uv.x),
                    lerp(rect.y, rect.w, uv.y)
                );
            }
            
            // Pixel-art: treat alpha > cutoff as solid
            static const float CUTOFF = 0.5;
            
            // Body Part ID 定义 (对应 B 通道值)
            // 0.0        = 非换装区域
            // 0.1 (25)   = Head (面部装饰)
            // 0.2 (51)   = Torso (服装)
            // 0.4 (102)  = LeftHand (左手套)
            // 0.5 (127)  = RightHand (右手套)
            // 0.6 (153)  = LeftFoot (左鞋)
            // 0.7 (178)  = RightFoot (右鞋)
            
            #define ID_NONE      0.0
            #define ID_HEAD      0.1
            #define ID_TORSO     0.2
            #define ID_LEFTHAND  0.4
            #define ID_RIGHTHAND 0.5
            #define ID_LEFTFOOT  0.6
            #define ID_RIGHTFOOT 0.7
            
            // 判断 ID 是否在范围内
            bool IsPartID(float id, float target)
            {
                return abs(id - target) < 0.05;
            }
            
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                // 对于 Sprite，直接使用原始 UV
                o.uv = v.uv;
                o.uvRaw = v.uv;  // 保存原始 UV 用于计算
                o.color = v.color * _Color;
                return o;
            }
            
            // ------------------------------------------------------------
            // Split body/head composition into two helpers so they can be toggled independently
            // ------------------------------------------------------------
            fixed4 ApplyBodyLayers(fixed4 baseColor, fixed4 bodyUV)
            {
                fixed4 color = baseColor;
                float bodyPartID = bodyUV.b;

                if (IsPartID(bodyPartID, ID_TORSO) && _EnableCloth > 0.5)
                {
                    float2 clothUVCoord = TransformUV(bodyUV.rg, _ClothRect);
                    fixed4 clothColor = tex2D(_ClothTex, clothUVCoord);
                    color.rgb = clothColor.rgb;
                }
                else if (IsPartID(bodyPartID, ID_LEFTHAND) && _EnableGloves > 0.5)
                {
                    color.rgb = _LeftHandColor.rgb;
                }
                else if (IsPartID(bodyPartID, ID_RIGHTHAND) && _EnableGloves > 0.5)
                {
                    color.rgb = _RightHandColor.rgb;
                }
                else if (IsPartID(bodyPartID, ID_LEFTFOOT) && _EnableShoes > 0.5)
                {
                    color.rgb = _LeftFootColor.rgb;
                }
                else if (IsPartID(bodyPartID, ID_RIGHTFOOT) && _EnableShoes > 0.5)
                {
                    color.rgb = _RightFootColor.rgb;
                }

                return color;
            }

            // Head layers: Hair (bottom) -> Beard (middle) -> Helmet (top)
            // Hard overlay: no color blending. Any hit writes RGB and lifts final alpha.
            void ApplyHeadLayers(float2 baseHeadUV, float headPartID, inout fixed4 ioColor, out float headLayerAlpha)
            {
                headLayerAlpha = 0;
                if (!IsPartID(headPartID, ID_HEAD)) return;

                // Helmet (top). If hit, override and early-out.
                if (_EnableHelmet > 0.5)
                {
                    // UVMap already uses bottom-up V, so no flip needed.
                    float2 uv = TransformUV(baseHeadUV, _HelmetRect);
                    fixed4 c = tex2D(_HelmetTex, uv);
                    if (c.a > CUTOFF)
                    {
                        ioColor.rgb = c.rgb;
                        headLayerAlpha = 1.0;
                        return;
                    }
                }

                // Beard (middle)
                bool wrote = false;
                if (_EnableBeard > 0.5)
                {
                    float2 uv = TransformUV(baseHeadUV, _BeardRect);
                    fixed4 c = tex2D(_BeardTex, uv);
                    if (c.a > CUTOFF)
                    {
                        ioColor.rgb = c.rgb;
                        headLayerAlpha = 1.0;
                        wrote = true;
                    }
                }

                // Hair (bottom) - only if beard didn't write
                if (!wrote && _EnableHair > 0.5)
                {
                    float2 uv = TransformUV(baseHeadUV, _HairRect);
                    fixed4 c = tex2D(_HairTex, uv);
                    if (c.a > CUTOFF)
                    {
                        ioColor.rgb = c.rgb;
                        headLayerAlpha = 1.0;
                    }
                }
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 baseColor = tex2D(_MainTex, i.uv);

                // Sample UV maps directly with sprite UVs (UVMap shares spritesheet layout at runtime)
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

                // Debug mode 3: Show sampling result (both layers)
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
                
                // Debug mode 7: Show helmetUV as color
                if (_DebugMode > 6.5 && _DebugMode < 7.5)
                {
                    if (IsPartID(headPartID, ID_HEAD))
                    {
                        float2 helmetUV = TransformUV(headUV.rg, _HelmetRect);
                        return fixed4(helmetUV.x, helmetUV.y, 0, 1);
                    }
                    return fixed4(0, 0, 0, baseColor.a);
                }
                
                // Debug mode 8: Show _HelmetTex with _HelmetRect applied
                if (_DebugMode > 7.5 && _DebugMode < 8.5)
                {
                    // Use TransformUV to map to the correct sprite in the atlas
                    float2 helmetUV = TransformUV(i.uv, _HelmetRect);
                    fixed4 helmetColor = tex2D(_HelmetTex, helmetUV);
                    return fixed4(helmetColor.rgb, 1);
                }
                
                // Debug mode 9: Show _HelmetRect values as color
                if (_DebugMode > 8.5 && _DebugMode < 9.5)
                {
                    // Display rect as: R=minU, G=minV, B=width, A=height
                    float width = _HelmetRect.z - _HelmetRect.x;
                    float height = _HelmetRect.w - _HelmetRect.y;
                    return fixed4(_HelmetRect.x, _HelmetRect.y, width, 1);
                }

                // Debug mode 4: Show raw UV values from head UV map (R=U, G=V as colors)
                if (_DebugMode > 3.5 && _DebugMode < 4.5)
                {
                    if (IsPartID(headPartID, ID_HEAD))
                    {
                        return fixed4(headUV.r, headUV.g, 0, 1);
                    }
                    return fixed4(0, 0, 0, baseColor.a);
                }

                // Debug mode 5: Show raw vertex UV (i.uv) as colors
                if (_DebugMode > 4.5 && _DebugMode < 5.5)
                {
                    return fixed4(i.uv.x, i.uv.y, 0, baseColor.a);
                }

                // Debug mode 6: Show helmet sampling directly
                if (_DebugMode > 5.5 && _DebugMode < 6.5)
                {
                    if (IsPartID(headPartID, ID_HEAD))
                    {
                        float2 helmetUV = TransformUV(headUV.rg, _HelmetRect);
                        fixed4 helmetColor = tex2D(_HelmetTex, helmetUV);
                        return fixed4(helmetColor.rgb, 1);
                    }
                    return fixed4(1, 0, 0, 1);
                }

                fixed4 finalColor = baseColor;

                // Body: isolated method
                finalColor = ApplyBodyLayers(finalColor, bodyUV);

                // Head: isolated method
                float headLayerAlpha;
                ApplyHeadLayers(headUV.rg, headPartID, finalColor, headLayerAlpha);

                // In expanded areas (baseColor.a = 0), use head layer alpha
                if (IsPartID(headPartID, ID_HEAD))
                {
                    finalColor.a = max(finalColor.a, headLayerAlpha);
                }

                return finalColor * i.color;
            }
            ENDCG
        }
    }
    
    Fallback "Sprites/Default"
}
