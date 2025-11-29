Shader "EquipmentSystem/EquipmentUV"
{
    Properties
    {
        _MainTex ("Base Sprite", 2D) = "white" {}
        _UVMapTex ("UV/ID Map", 2D) = "black" {}
        _ClothTex ("Clothing Texture", 2D) = "white" {}
        _HeadTex ("Head/Facial Decor Texture", 2D) = "white" {}
        
        // Sprite 在 spritesheet 中的 UV 范围 (x=minU, y=minV, z=maxU, w=maxV)
        _SpriteRect ("Sprite UV Rect", Vector) = (0, 0, 1, 1)
        
        // 手套颜色
        [HDR] _LeftHandColor ("Left Hand Color", Color) = (0.6, 0.4, 0.2, 1)
        [HDR] _RightHandColor ("Right Hand Color", Color) = (0.6, 0.4, 0.2, 1)
        
        // 鞋子颜色
        [HDR] _LeftFootColor ("Left Foot Color", Color) = (0.3, 0.2, 0.1, 1)
        [HDR] _RightFootColor ("Right Foot Color", Color) = (0.3, 0.2, 0.1, 1)
        
        // 是否启用各装备层
        _EnableHead ("Enable Head/Facial Decor", Float) = 0
        _EnableCloth ("Enable Clothing", Float) = 0
        _EnableGloves ("Enable Gloves", Float) = 0
        _EnableShoes ("Enable Shoes", Float) = 0
        
        // 调试模式: 1=显示 UV Map 原始颜色, 2=显示部位 ID
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
            sampler2D _UVMapTex;
            sampler2D _ClothTex;
            sampler2D _HeadTex;
            float4 _MainTex_ST;
            float4 _ClothTex_TexelSize;
            float4 _SpriteRect; // x=minU, y=minV, z=maxU, w=maxV
            
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
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                return o;
            }
            
            fixed4 frag(v2f i) : SV_Target
            {
                // 采样原图
                fixed4 baseColor = tex2D(_MainTex, i.uv);
                
                // Unity SpriteRenderer 的 UV 已经是 spritesheet 上的实际坐标
                // UV Map 和 spritesheet 尺寸一致，所以直接用 i.uv 采样
                fixed4 uvMap = tex2D(_UVMapTex, i.uv);
                
                // 调试模式
                if (_DebugMode > 0.5 && _DebugMode < 1.5)
                {
                    // 模式 1: 显示 UV 坐标 (红=U, 绿=V)
                    return fixed4(i.uv.x, i.uv.y, 0, baseColor.a);
                }
                if (_DebugMode > 1.5 && _DebugMode < 2.5)
                {
                    // 模式 2: 显示 UV Map 原始颜色
                    return fixed4(uvMap.rgb, baseColor.a);
                }
                if (_DebugMode > 2.5)
                {
                    // 模式 3: 显示部位 ID (不同部位不同颜色)
                    float id = uvMap.b;
                    fixed4 debugColor = fixed4(0, 0, 0, baseColor.a);
                    if (id > 0.05 && id < 0.15) debugColor.rgb = fixed3(0, 1, 1);      // Head - 青色
                    else if (id > 0.15 && id < 0.3) debugColor.rgb = fixed3(0, 0, 1);  // Torso - 蓝色
                    else if (id > 0.35 && id < 0.45) debugColor.rgb = fixed3(1, 1, 0); // LeftHand - 黄色
                    else if (id > 0.45 && id < 0.55) debugColor.rgb = fixed3(1, 0.5, 0); // RightHand - 橙色
                    else if (id > 0.55 && id < 0.65) debugColor.rgb = fixed3(0.5, 0, 1); // LeftFoot - 紫色
                    else if (id > 0.65 && id < 0.75) debugColor.rgb = fixed3(1, 0, 0.5); // RightFoot - 粉色
                    return debugColor;
                }
                
                float bodyPartID = uvMap.b;
                float mask = uvMap.a;
                
                // 如果是死区(mask=0)或非换装区域(ID=0)，直接返回原图
                if (mask < 0.5 || bodyPartID < 0.05)
                {
                    return baseColor * i.color;
                }
                
                fixed4 finalColor = baseColor;
                
                // Head - 面部装饰 (刀疑、文身等)
                if (IsPartID(bodyPartID, ID_HEAD) && _EnableHead > 0.5)
                {
                    fixed4 headColor = tex2D(_HeadTex, float2(uvMap.r, uvMap.g));
                    if (headColor.a > 0.01)
                    {
                        finalColor.rgb = headColor.rgb;
                    }
                }
                // Torso - 服装
                else if (IsPartID(bodyPartID, ID_TORSO) && _EnableCloth > 0.5)
                {
                    fixed4 clothColor = tex2D(_ClothTex, float2(uvMap.r, uvMap.g));
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
                
                return finalColor * i.color;
            }
            ENDCG
        }
    }
    
    // 回退到旧 Shader
    Fallback "EquipmentSystem/EquipmentOverlay"
}
