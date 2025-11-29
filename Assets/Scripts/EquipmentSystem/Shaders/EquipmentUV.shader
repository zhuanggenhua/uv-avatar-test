// 双层 UV Map 换装系统
// 身体层: 衣服/手套/鞋子 (BodyUVMap)
// 头部层: 头盔/胡子/头发 (HeadUVMap) - 渲染在身体层之上
Shader "EquipmentSystem/EquipmentUV"
{
    Properties
    {
        _MainTex ("Base Sprite", 2D) = "white" {}
        
        [Header(Dual UV Maps)]
        _BodyUVMap ("Body UV Map (躯干+手+脚)", 2D) = "black" {}
        _HeadUVMap ("Head UV Map (扩展头部)", 2D) = "black" {}
        
        [Header(Body Layer Textures)]
        _ClothTex ("Clothing Texture (衣服)", 2D) = "white" {}
        
        [Header(Head Layer Textures)]
        _HeadTex ("Head Texture (头盔/胡子/头发)", 2D) = "white" {}
        
        [Header(Glove Colors)]
        [HDR] _LeftHandColor ("Left Hand Color", Color) = (0.6, 0.4, 0.2, 1)
        [HDR] _RightHandColor ("Right Hand Color", Color) = (0.6, 0.4, 0.2, 1)
        
        [Header(Shoe Colors)]
        [HDR] _LeftFootColor ("Left Foot Color", Color) = (0.3, 0.2, 0.1, 1)
        [HDR] _RightFootColor ("Right Foot Color", Color) = (0.3, 0.2, 0.1, 1)
        
        [Header(Enable Layers)]
        _EnableHead ("Enable Head Layer", Float) = 0
        _EnableCloth ("Enable Clothing", Float) = 0
        _EnableGloves ("Enable Gloves", Float) = 0
        _EnableShoes ("Enable Shoes", Float) = 0
        
        [Header(Debug)]
        // 调试模式: 0=关闭, 1=显示身体层区域, 2=显示头部层区域, 3=显示UV采样
        _DebugMode ("Debug Mode", Float) = 0
        
        _Color ("Tint", Color) = (1,1,1,1)
        
        // 兼容旧属性 (已废弃)
        [HideInInspector] _UVMapTex ("[Deprecated] UV Map", 2D) = "black" {}
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
            sampler2D _HeadTex;
            float4 _MainTex_ST;
            
            // 兼容旧属性
            sampler2D _UVMapTex;
            
            fixed4 _LeftHandColor;
            fixed4 _RightHandColor;
            fixed4 _LeftFootColor;
            fixed4 _RightFootColor;
            
            float _EnableHead;
            float _EnableCloth;
            float _EnableGloves;
            float _EnableShoes;
            float _DebugMode;
            
            fixed4 _Color;
            
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
            
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 baseColor = tex2D(_MainTex, i.uv);
                
                // 采样双层 UV Map
                fixed4 bodyUV = tex2D(_BodyUVMap, i.uv);
                fixed4 headUV = tex2D(_HeadUVMap, i.uv);
                
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
                
                // 调试模式 3: 显示 UV 采样结果
                if (_DebugMode > 2.5 && _DebugMode < 3.5)
                {
                    if (IsPartID(bodyPartID, ID_TORSO))
                    {
                        float2 clothUVCoord = float2(bodyUV.r, bodyUV.g);
                        fixed4 clothColor = tex2D(_ClothTex, clothUVCoord);
                        return fixed4(clothColor.rgb, baseColor.a);
                    }
                    if (IsPartID(headPartID, ID_HEAD))
                    {
                        float2 headUVCoord = float2(headUV.r, headUV.g);
                        fixed4 headColor = tex2D(_HeadTex, headUVCoord);
                        return fixed4(headColor.rgb, baseColor.a);
                    }
                    return fixed4(0, 0, 0, baseColor.a);
                }
                
                fixed4 finalColor = baseColor;
                
                // ============ 第一层: 身体层 ============
                float bodyMask = bodyUV.a;
                
                if (bodyMask > 0.5 && bodyPartID > 0.05)
                {
                    // Torso - 服装
                    if (IsPartID(bodyPartID, ID_TORSO) && _EnableCloth > 0.5)
                    {
                        float2 clothUVCoord = float2(bodyUV.r, bodyUV.g);
                        fixed4 clothColor = tex2D(_ClothTex, clothUVCoord);
                        if (clothColor.a > 0.01)
                        {
                            finalColor.rgb = clothColor.rgb;
                        }
                    }
                    // LeftHand - 左手套
                    else if (IsPartID(bodyPartID, ID_LEFTHAND) && _EnableGloves > 0.5)
                    {
                        finalColor.rgb = _LeftHandColor.rgb;
                    }
                    // RightHand - 右手套
                    else if (IsPartID(bodyPartID, ID_RIGHTHAND) && _EnableGloves > 0.5)
                    {
                        finalColor.rgb = _RightHandColor.rgb;
                    }
                    // LeftFoot - 左鞋
                    else if (IsPartID(bodyPartID, ID_LEFTFOOT) && _EnableShoes > 0.5)
                    {
                        finalColor.rgb = _LeftFootColor.rgb;
                    }
                    // RightFoot - 右鞋
                    else if (IsPartID(bodyPartID, ID_RIGHTFOOT) && _EnableShoes > 0.5)
                    {
                        finalColor.rgb = _RightFootColor.rgb;
                    }
                }
                
                // ============ 第二层: 头部层 (覆盖在身体层上) ============
                float headMask = headUV.a;
                
                if (headMask > 0.5 && IsPartID(headPartID, ID_HEAD) && _EnableHead > 0.5)
                {
                    float2 headUVCoord = float2(headUV.r, headUV.g);
                    fixed4 headColor = tex2D(_HeadTex, headUVCoord);
                    if (headColor.a > 0.01)
                    {
                        // 头部层覆盖身体层
                        finalColor.rgb = lerp(finalColor.rgb, headColor.rgb, headColor.a);
                    }
                }
                
                return finalColor * i.color;
            }
            ENDCG
        }
    }
    
    Fallback "Sprites/Default"
}
