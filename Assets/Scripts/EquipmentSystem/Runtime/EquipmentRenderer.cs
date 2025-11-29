using UnityEngine;
using EquipmentSystem.Data;
using System.Collections.Generic;

namespace EquipmentSystem.Runtime
{
    /// <summary>
    /// 装备渲染器 (GPU 版本)
    /// - 挂件(Accessory): 用锚点定位
    /// - 服装(Clothing): GPU UV 重映射到躯干
    /// - 面部装饰(FacialDecor): GPU UV 重映射到头部
    /// - 手套(Gloves): GPU 颜色参数
    /// - 鞋子(Shoes): GPU 颜色参数
    /// 
    /// 需要配合 UV Map Generator 生成的 UV/ID 贴图使用
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class EquipmentRenderer : MonoBehaviour
    {
        [Header("数据")]
        public CharacterFrameData frameData;
        public string currentAnimation = "Idle";
        
        [Header("装备")]
        public List<EquipmentData> equipments = new List<EquipmentData>();
        
        [Header("调试")]
        [Tooltip("如果 Shader.Find 失败，可以手动指定 Shader")]
        public Shader overrideShader;
        
        SpriteRenderer _charRenderer;
        Dictionary<EquipmentData, SpriteRenderer> _equipRenderers = new Dictionary<EquipmentData, SpriteRenderer>();
        
        // 帧同步
        Sprite _lastSprite;
        int _frameIndex;
        int _rowIndex;
        FrameData _cachedFrame;
        AnimationData _currentAnimData;
        
        // GPU 换装材质
        Material _gpuMaterial;
        
        // Shader 属性 ID
        static readonly int UVMapTexProp = Shader.PropertyToID("_UVMapTex");
        static readonly int ClothTexProp = Shader.PropertyToID("_ClothTex");
        static readonly int HeadTexProp = Shader.PropertyToID("_HeadTex");
        static readonly int SpriteRectProp = Shader.PropertyToID("_SpriteRect");
        static readonly int LeftHandColorProp = Shader.PropertyToID("_LeftHandColor");
        static readonly int RightHandColorProp = Shader.PropertyToID("_RightHandColor");
        static readonly int LeftFootColorProp = Shader.PropertyToID("_LeftFootColor");
        static readonly int RightFootColorProp = Shader.PropertyToID("_RightFootColor");
        static readonly int EnableHeadProp = Shader.PropertyToID("_EnableHead");
        static readonly int EnableClothProp = Shader.PropertyToID("_EnableCloth");
        static readonly int EnableGlovesProp = Shader.PropertyToID("_EnableGloves");
        static readonly int EnableShoesProp = Shader.PropertyToID("_EnableShoes");
        
        void Awake()
        {
            _charRenderer = GetComponent<SpriteRenderer>();
            InitMaterial();
        }
        
        void Start()
        {
            foreach (var e in equipments)
                if (e != null && e.type == EquipmentType.Accessory)
                    CreateAccessoryRenderer(e);
            Refresh();
        }
        
        void LateUpdate()
        {
            // 自动同步 Sprite 变化
            if (_charRenderer.sprite != _lastSprite)
            {
                _lastSprite = _charRenderer.sprite;
                SyncFromSprite();
            }
        }
        
        void OnDestroy()
        {
            if (_gpuMaterial != null)
                Destroy(_gpuMaterial);
        }
        
        void InitMaterial()
        {
            // 加载 GPU 换装 Shader
            var shader = overrideShader != null ? overrideShader : Shader.Find("EquipmentSystem/EquipmentUV");
            
            if (shader == null)
            {
                Debug.LogError("[EquipmentRenderer] 找不到 EquipmentSystem/EquipmentUV Shader！" +
                    "请确保 Shader 在 Project Settings > Graphics > Always Included Shaders 中，" +
                    "或手动拖拽 Shader 到 overrideShader 字段");
                return;
            }
            
            _gpuMaterial = new Material(shader);
            _charRenderer.material = _gpuMaterial;
            
            Debug.Log($"[EquipmentRenderer] Shader 加载成功: {shader.name}");
        }
        
        /// <summary>
        /// 从 Sprite 的 rect 位置同步帧索引和行索引
        /// </summary>
        void SyncFromSprite()
        {
            if (_lastSprite == null || frameData == null) return;
            
            // 获取当前动画配置
            _currentAnimData = frameData.GetAnimation(currentAnimation);
            if (_currentAnimData == null) return;
            
            // 从 Sprite 的 rect 位置计算帧索引和行索引
            var rect = _lastSprite.rect;
            int frameW = _currentAnimData.frameSize.x;
            int frameH = _currentAnimData.frameSize.y;
            
            if (frameW > 0 && frameH > 0)
            {
                _frameIndex = Mathf.FloorToInt(rect.x / frameW);
                // Unity Sprite 的 Y 是从底部计算的，需要转换
                _rowIndex = Mathf.FloorToInt((_lastSprite.texture.height - rect.y - rect.height) / frameH);
                Refresh();
            }
        }
        
        public void Equip(EquipmentData equip)
        {
            if (equip == null) return;
            
            if (!equipments.Contains(equip))
            {
                equipments.Add(equip);
                if (equip.type == EquipmentType.Accessory)
                    CreateAccessoryRenderer(equip);
            }
            Refresh();
        }
        
        public void Unequip(EquipmentData equip)
        {
            if (equip == null) return;
            
            if (_equipRenderers.TryGetValue(equip, out var sr))
            {
                Destroy(sr.gameObject);
                _equipRenderers.Remove(equip);
            }
            equipments.Remove(equip);
            Refresh();
        }
        
        void CreateAccessoryRenderer(EquipmentData equip)
        {
            if (_equipRenderers.ContainsKey(equip)) return;
            
            var go = new GameObject($"Equip_{equip.name}");
            go.transform.SetParent(transform);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = Vector3.one;
            
            var sr = go.AddComponent<SpriteRenderer>();
            _equipRenderers[equip] = sr;
        }
        
        public void Refresh()
        {
            if (frameData == null)
            {
                Debug.LogWarning("[EquipmentRenderer] frameData 未设置");
                return;
            }
            
            _cachedFrame = frameData.GetFrameData(currentAnimation, _rowIndex, _frameIndex);
            
            // 获取当前动画配置
            if (_currentAnimData == null)
                _currentAnimData = frameData.GetAnimation(currentAnimation);
            
            if (_currentAnimData == null)
            {
                Debug.LogWarning($"[EquipmentRenderer] 找不到动画: {currentAnimation}");
                return;
            }
            
            bool hideLeftWeapon = _currentAnimData?.hideLeftWeapon ?? false;
            bool hideRightWeapon = _currentAnimData?.hideRightWeapon ?? false;
            
            // 设置 UV Map 贴图
            UpdateUVMapTexture();
            
            // 重置装备状态
            ResetEquipmentState();
            
            // 处理所有装备
            foreach (var equip in equipments)
            {
                if (equip == null) continue;
                
                Debug.Log($"[EquipmentRenderer] 处理装备: {equip.name}, 类型: {equip.type}");
                
                switch (equip.type)
                {
                    case EquipmentType.Accessory:
                        if (_equipRenderers.TryGetValue(equip, out var sr))
                            RenderAccessory(equip, sr, hideLeftWeapon, hideRightWeapon);
                        break;
                    case EquipmentType.Clothing:
                        SetClothingGPU(equip);
                        break;
                    case EquipmentType.FacialDecor:
                        SetFacialDecorGPU(equip);
                        break;
                    case EquipmentType.Gloves:
                        SetGlovesGPU(equip);
                        break;
                    case EquipmentType.Shoes:
                        SetShoesGPU(equip);
                        break;
                }
            }
        }
        
        void UpdateUVMapTexture()
        {
            if (_gpuMaterial == null || _currentAnimData == null) return;
            
            if (_currentAnimData.uvMapTexture != null)
            {
                _gpuMaterial.SetTexture(UVMapTexProp, _currentAnimData.uvMapTexture);
                Debug.Log($"[EquipmentRenderer] UV Map 已设置: {_currentAnimData.uvMapTexture.name}");
            }
            else
            {
                Debug.LogWarning($"[EquipmentRenderer] 动画 '{currentAnimation}' 没有 UV Map 贴图！" +
                    "请在 Frame Editor 中生成 UV Map");
            }
            
            // 设置当前帧在 spritesheet 中的 UV 范围
            UpdateSpriteRect();
        }
        
        void UpdateSpriteRect()
        {
            if (_gpuMaterial == null || _lastSprite == null) return;
            
            var tex = _lastSprite.texture;
            if (tex == null) return;
            
            var rect = _lastSprite.rect;
            
            // 计算当前帧在 spritesheet 中的 UV 范围
            float minU = rect.x / tex.width;
            float minV = rect.y / tex.height;
            float maxU = (rect.x + rect.width) / tex.width;
            float maxV = (rect.y + rect.height) / tex.height;
            
            _gpuMaterial.SetVector(SpriteRectProp, new Vector4(minU, minV, maxU, maxV));
        }
        
        void ResetEquipmentState()
        {
            if (_gpuMaterial == null) return;
            
            // 重置所有装备层为禁用
            _gpuMaterial.SetFloat(EnableHeadProp, 0);
            _gpuMaterial.SetFloat(EnableClothProp, 0);
            _gpuMaterial.SetFloat(EnableGlovesProp, 0);
            _gpuMaterial.SetFloat(EnableShoesProp, 0);
        }
        
        /// <summary>
        /// GPU 方式设置服装 - 只需设置贴图和启用标志
        /// </summary>
        void SetClothingGPU(EquipmentData equip)
        {
            if (_gpuMaterial == null) return;
            
            var clothingSprite = equip.frontSprite;
            if (clothingSprite == null || clothingSprite.texture == null)
            {
                Debug.LogWarning($"[EquipmentRenderer] 服装 {equip.name} 没有 frontSprite");
                return;
            }
            
            _gpuMaterial.SetTexture(ClothTexProp, clothingSprite.texture);
            _gpuMaterial.SetFloat(EnableClothProp, 1);
            Debug.Log($"[EquipmentRenderer] 服装已启用: {equip.name}, 贴图: {clothingSprite.texture.name}");
        }
        
        /// <summary>
        /// GPU 方式设置面部装饰 - 类似服装，但映射到头部
        /// </summary>
        void SetFacialDecorGPU(EquipmentData equip)
        {
            if (_gpuMaterial == null) return;
            
            var headSprite = equip.frontSprite;
            if (headSprite == null || headSprite.texture == null) return;
            
            _gpuMaterial.SetTexture(HeadTexProp, headSprite.texture);
            _gpuMaterial.SetFloat(EnableHeadProp, 1);
        }
        
        /// <summary>
        /// GPU 方式设置手套 - 只需设置颜色参数
        /// </summary>
        void SetGlovesGPU(EquipmentData equip)
        {
            if (_gpuMaterial == null) return;
            
            _gpuMaterial.SetColor(LeftHandColorProp, equip.leftColor);
            _gpuMaterial.SetColor(RightHandColorProp, equip.rightColor);
            _gpuMaterial.SetFloat(EnableGlovesProp, 1);
            Debug.Log($"[EquipmentRenderer] 手套已启用: {equip.name}, 左={equip.leftColor}, 右={equip.rightColor}");
        }
        
        /// <summary>
        /// GPU 方式设置鞋子 - 只需设置颜色参数
        /// </summary>
        void SetShoesGPU(EquipmentData equip)
        {
            if (_gpuMaterial == null) return;
            
            _gpuMaterial.SetColor(LeftFootColorProp, equip.leftColor);
            _gpuMaterial.SetColor(RightFootColorProp, equip.rightColor);
            _gpuMaterial.SetFloat(EnableShoesProp, 1);
        }
        
        /// <summary>
        /// 渲染挂件 - 用锚点定位
        /// </summary>
        void RenderAccessory(EquipmentData equip, SpriteRenderer sr, bool hideLeftWeapon, bool hideRightWeapon)
        {
            var facingDir = CharacterFrameData.GetFacingDirection((CharacterFacing)_rowIndex);
            sr.sprite = equip.GetSprite(facingDir);
            
            if (_cachedFrame == null)
            {
                sr.enabled = false;
                return;
            }
            
            // 检查武器隐藏配置
            if (equip.anchorType == AnchorType.LeftWeapon && hideLeftWeapon)
            {
                sr.enabled = false;
                return;
            }
            if (equip.anchorType == AnchorType.RightWeapon && hideRightWeapon)
            {
                sr.enabled = false;
                return;
            }
            
            var anchor = _cachedFrame.GetAnchor(equip.anchorType);
            if (anchor == null)
            {
                sr.enabled = false;
                return;
            }
            
            // 死区检查 - 如果锚点在死区内则隐藏
            if (_cachedFrame.IsInDeadZone(anchor.position))
            {
                sr.enabled = false;
                return;
            }
            
            sr.enabled = true;
            
            // 计算位置 - 直接用像素坐标
            float ppu = _charRenderer.sprite != null ? _charRenderer.sprite.pixelsPerUnit : 16f;
            float equipW = sr.sprite != null ? sr.sprite.rect.width : 0;
            
            // 装备自身锚点
            float equipAnchorX = equip.selfAnchor.x;
            float equipAnchorY = equip.selfAnchor.y;
            
            // 翻转时镜像装备锚点 X
            if (anchor.flipX && equipW > 0)
            {
                equipAnchorX = equipW - 1 - equip.selfAnchor.x;
            }
            
            // 像素偏移
            float deltaX = anchor.position.x - equipAnchorX;
            float deltaY = anchor.position.y - equipAnchorY;
            
            // 转换到 Unity 坐标（Y 取反）
            sr.transform.localPosition = new Vector3(deltaX / ppu, -deltaY / ppu, 0);
            
            // 翻转
            sr.flipX = anchor.flipX;
            
            // 旋转
            sr.transform.localRotation = Quaternion.Euler(0, 0, anchor.GetRotationAngle());
            
            // 排序
            sr.sortingLayerID = _charRenderer.sortingLayerID;
            sr.sortingOrder = _charRenderer.sortingOrder + equip.sortingOffset;
        }
        
#if UNITY_EDITOR
        void OnValidate()
        {
            if (Application.isPlaying) Refresh();
        }
#endif
    }
}
