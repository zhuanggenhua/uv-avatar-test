using System.Collections.Generic;

using EquipmentSystem.Data;

using UnityEngine;

namespace EquipmentSystem.Runtime
{
    /// <summary>
    /// 装备渲染器 (Shader 版本)
    /// - 武器(Weapon): 用锚点定位
    /// - 服装(Clothing): Shader UV 重映射到躯干 (Body 层)
    /// - 头部层: 头发 -> 胡子 -> 头盔 (三层叠加渲染)
    ///   - 头发/胡子: 来自 CharacterAppearance (捷人时选择)
    ///   - 头盔: 来自 EquipmentData (Helmet 类型)
    /// - 手套(Gloves): Shader 颜色参数
    /// - 鞋子(Shoes): Shader 颜色参数
    /// 
    /// 需要配合 UV Map Generator 生成的 UV/ID 贴图使用
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class EquipmentRenderer : MonoBehaviour
    {
        [Header("数据")]
        public CharacterFrameData frameData;
        
        [Header("角色外观 (捷人)")]
        [Tooltip("角色外观数据，包含头发、胡子等捷人时选择的外观")]
        public CharacterAppearance appearance;
        
        [Header("装备槽位")]
        public EquipmentData helmet;
        public EquipmentData clothing;
        public EquipmentData cloak;
        public EquipmentData gloves;
        public EquipmentData shoes;
        public List<EquipmentData> weapons = new List<EquipmentData>();
        
        [Header("调试")]
        [Tooltip("如果 Shader.Find 失败，可以手动指定 Shader")]
        public Shader overrideShader;
        
        [Header("运行时状态 (只读)")]
        [SerializeField] string _debugCurrentAnim = "";
        [SerializeField] string _debugAnimatorState = "";
        [SerializeField] bool _debugHasBodyUVMap = false;
        [SerializeField] bool _debugHasHeadUVMap = false;
        
        // 动画同步
        Animator _animator;
        string _currentAnimName;
        
        SpriteRenderer _charRenderer;
        Dictionary<EquipmentData, SpriteRenderer> _weaponRenderers = new Dictionary<EquipmentData, SpriteRenderer>();
        
        // 帧同步
        Sprite _lastSprite;
        int _frameIndex;
        int _rowIndex;
        FrameData _cachedFrame;
        AnimationData _currentAnimData;
        
        // Shader 换装材质
        Material _shaderMaterial;
        
        // 方案 D：Animator 参数缓存
        List<string> _validAnimParams;
        bool _animParamsCached;
        
        // Shader 属性 ID - 双层 UV Map
        static readonly int BodyUVMapProp = Shader.PropertyToID("_BodyUVMap");
        static readonly int HeadUVMapProp = Shader.PropertyToID("_HeadUVMap");
        static readonly int ClothTexProp = Shader.PropertyToID("_ClothTex");
        static readonly int CloakTexProp = Shader.PropertyToID("_CloakTex");
        // 头部四层贴图
        static readonly int HairTexProp = Shader.PropertyToID("_HairTex");
        static readonly int FaceAccessoryTexProp = Shader.PropertyToID("_FaceAccessoryTex");
        static readonly int BeardTexProp = Shader.PropertyToID("_BeardTex");
        static readonly int HelmetTexProp = Shader.PropertyToID("_HelmetTex");
        // 装备贴图的 Sprite Rect (UV 偏移和缩放)
        static readonly int ClothRectProp = Shader.PropertyToID("_ClothRect");
        static readonly int CloakRectProp = Shader.PropertyToID("_CloakRect");
        static readonly int HairRectProp = Shader.PropertyToID("_HairRect");
        static readonly int FaceAccessoryRectProp = Shader.PropertyToID("_FaceAccessoryRect");
        static readonly int BeardRectProp = Shader.PropertyToID("_BeardRect");
        static readonly int HelmetRectProp = Shader.PropertyToID("_HelmetRect");
        // 颜色属性
        static readonly int LeftHandColorProp = Shader.PropertyToID("_LeftHandColor");
        static readonly int RightHandColorProp = Shader.PropertyToID("_RightHandColor");
        static readonly int LeftFootColorProp = Shader.PropertyToID("_LeftFootColor");
        static readonly int RightFootColorProp = Shader.PropertyToID("_RightFootColor");
        static readonly int LeftEyeColorProp = Shader.PropertyToID("_LeftEyeColor");
        static readonly int RightEyeColorProp = Shader.PropertyToID("_RightEyeColor");
        // 启用开关
        static readonly int EnableHairProp = Shader.PropertyToID("_EnableHair");
        static readonly int EnableFaceAccessoryProp = Shader.PropertyToID("_EnableFaceAccessory");
        static readonly int EnableBeardProp = Shader.PropertyToID("_EnableBeard");
        static readonly int EnableHelmetProp = Shader.PropertyToID("_EnableHelmet");
        static readonly int EnableClothProp = Shader.PropertyToID("_EnableCloth");
        static readonly int EnableCloakProp = Shader.PropertyToID("_EnableCloak");
        static readonly int EnableGlovesProp = Shader.PropertyToID("_EnableGloves");
        static readonly int EnableShoesProp = Shader.PropertyToID("_EnableShoes");
        static readonly int EnableLeftEyeProp = Shader.PropertyToID("_EnableLeftEye");
        static readonly int EnableRightEyeProp = Shader.PropertyToID("_EnableRightEye");
        
        void Awake()
        {
            _charRenderer = GetComponent<SpriteRenderer>();
            _animator = GetComponentInChildren<Animator>();
            InitMaterial();
        }
        
        void Start()
        {
            foreach (var w in weapons)
            {
                if (w != null)
                    CreateWeaponRenderer(w);
            }
            Refresh();
        }
        
        void LateUpdate()
        {
            // 同步动画名称
            SyncAnimationName();
            
            // 自动同步 Sprite 变化
            if (_charRenderer.sprite != _lastSprite)
            {
                _lastSprite = _charRenderer.sprite;
                SyncFromSprite();
            }
        }
        
        /// <summary>
        /// 缓存 Animator 中有效的 Bool 参数
        /// 从 AnimationTypeDatabase 获取动画 Key 列表
        /// </summary>
        void CacheValidAnimParams()
        {
            if (_animParamsCached || _animator == null) return;
            
            _validAnimParams = new List<string>();
            
            // 从 frameData 中的数据库获取所有动画 Key
            var keywordSet = new HashSet<string>();
            var db = frameData?.animDatabase;
            if (db != null)
            {
                foreach (var type in db.ItemsReadOnly)
                {
                    if (type != null)
                        keywordSet.Add(type.name);
                }
            }
            
            foreach (var param in _animator.parameters)
            {
                if (param.type == AnimatorControllerParameterType.Bool 
                    && keywordSet.Contains(param.name))
                {
                    _validAnimParams.Add(param.name);
                }
            }
            _animParamsCached = true;
        }
        
        /// <summary>
        /// 从 Animator Bool 参数同步当前动画名称
        /// CTR_AnimateCreature 使用 SetBool("Idle", true) 等方式切换动画
        /// </summary>
        void SyncAnimationName()
        {
            if (_animator == null || frameData == null) return;
            
            // 方案 D：使用缓存的参数列表，避免 try/catch
            CacheValidAnimParams();
            
            // 从 Animator 的 Bool 参数找到当前激活的动画
            string activeParam = null;
            foreach (var keyword in _validAnimParams)
            {
                if (_animator.GetBool(keyword))
                {
                    activeParam = keyword;
                    break;
                }
            }
            
            _debugAnimatorState = activeParam ?? "(none)";
            
            // 如果没找到激活的参数，默认用 Idle 或第一个动画
            if (string.IsNullOrEmpty(activeParam))
            {
                activeParam = "Idle";
            }
            
            // 在 frameData 中找到包含该 Key 的动画
            var newAnimData = FindAnimationByKey(activeParam);
            
            if (newAnimData != null && newAnimData != _currentAnimData)
            {
                _currentAnimData = newAnimData;
                _currentAnimName = newAnimData.GetKey();
                _debugCurrentAnim = _currentAnimName ?? "(null)";
                
                UpdateUVMapTexture();
            }
        }
        
        /// <summary>
        /// 根据 Key 在 frameData 中找到对应的动画
        /// </summary>
        AnimationData FindAnimationByKey(string key)
        {
            if (frameData == null) return null;
            
            // 精确匹配
            var exact = frameData.GetAnimationByKey(key);
            if (exact != null) return exact;
            
            // 默认返回第一个
            if (frameData.animations.Count > 0)
                return frameData.animations[0];
            
            return null;
        }
        
        void OnDestroy()
        {
            if (_shaderMaterial != null)
                Destroy(_shaderMaterial);
        }
        
        void InitMaterial()
        {
            // 加载 Shader 换装 Shader
            var shader = overrideShader != null ? overrideShader : Shader.Find("EquipmentSystem/EquipmentUV");
            
            if (shader == null)
            {
                Debug.LogError("[EquipmentRenderer] 找不到 EquipmentSystem/EquipmentUV Shader！" +
                    "请确保 Shader 在 Project Settings > Graphics > Always Included Shaders 中，" +
                    "或手动拖拽 Shader 到 overrideShader 字段");
                return;
            }
            
            _shaderMaterial = new Material(shader);
            _charRenderer.material = _shaderMaterial;
        }
        
        /// <summary>
        /// 从 Sprite 的 rect 位置同步帧索引和行索引
        /// </summary>
        void SyncFromSprite()
        {
            if (_lastSprite == null || frameData == null || _currentAnimData == null) return;
            
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
            
            switch (equip.type)
            {
                case EquipmentType.Helmet:
                    helmet = equip;
                    break;
                case EquipmentType.Clothing:
                    clothing = equip;
                    break;
                case EquipmentType.Cloak:
                    cloak = equip;
                    break;
                case EquipmentType.Gloves:
                    gloves = equip;
                    break;
                case EquipmentType.Shoes:
                    shoes = equip;
                    break;
                case EquipmentType.Weapon:
                    if (!weapons.Contains(equip))
                    {
                        weapons.Add(equip);
                        CreateWeaponRenderer(equip);
                    }
                    break;
            }
            
            Refresh();
        }
        
        public void Unequip(EquipmentData equip)
        {
            if (equip == null) return;
            
            switch (equip.type)
            {
                case EquipmentType.Helmet:
                    if (helmet == equip) helmet = null;
                    break;
                case EquipmentType.Clothing:
                    if (clothing == equip) clothing = null;
                    break;
                case EquipmentType.Cloak:
                    if (cloak == equip) cloak = null;
                    break;
                case EquipmentType.Gloves:
                    if (gloves == equip) gloves = null;
                    break;
                case EquipmentType.Shoes:
                    if (shoes == equip) shoes = null;
                    break;
                case EquipmentType.Weapon:
                    if (weapons.Contains(equip))
                        weapons.Remove(equip);
                    break;
            }
            
            if (_weaponRenderers.TryGetValue(equip, out var sr))
            {
                Destroy(sr.gameObject);
                _weaponRenderers.Remove(equip);
            }
            
            Refresh();
        }
        
        /// <summary>
        /// 设置角色外观（头发/胡子等）
        /// 会自动刷新
        /// </summary>
        public void SetAppearance(CharacterAppearance newAppearance)
        {
            if (appearance == newAppearance) return;
            appearance = newAppearance;
            Refresh();
        }
        
        /// <summary>
        /// 一次性卸下所有装备（包括武器）
        /// </summary>
        public void UnequipAll()
        {
            var toUnequip = new List<EquipmentData>();
            if (helmet != null) toUnequip.Add(helmet);
            if (clothing != null) toUnequip.Add(clothing);
            if (cloak != null) toUnequip.Add(cloak);
            if (gloves != null) toUnequip.Add(gloves);
            if (shoes != null) toUnequip.Add(shoes);
            toUnequip.AddRange(weapons);
            
            foreach (var e in toUnequip)
            {
                Unequip(e);
            }
        }
        
        /// <summary>
        /// 获取指定类型当前装备（武器返回第一个，如有）
        /// </summary>
        public EquipmentData GetEquipped(EquipmentType type)
        {
            switch (type)
            {
                case EquipmentType.Helmet:   return helmet;
                case EquipmentType.Clothing: return clothing;
                case EquipmentType.Cloak:    return cloak;
                case EquipmentType.Gloves:   return gloves;
                case EquipmentType.Shoes:    return shoes;
                case EquipmentType.Weapon:   return weapons.Count > 0 ? weapons[0] : null;
                default:                     return null;
            }
        }
        
        void CreateWeaponRenderer(EquipmentData equip)
        {
            if (_weaponRenderers.ContainsKey(equip)) return;
            
            var go = new GameObject($"Weapon_{equip.name}");
            go.transform.SetParent(transform);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = Vector3.one;
            
            var sr = go.AddComponent<SpriteRenderer>();
            _weaponRenderers[equip] = sr;
        }
        
        public void Refresh()
        {
            if (frameData == null)
            {
                Debug.LogWarning("[EquipmentRenderer] frameData 未设置");
                return;
            }
            
            _cachedFrame = frameData.GetFrameDataByKey(_currentAnimName, _rowIndex, _frameIndex);
            
            // 获取当前动画配置
            if (_currentAnimData == null)
                _currentAnimData = frameData.GetAnimationByKey(_currentAnimName);
            
            if (_currentAnimData == null)
            {
                // 静默失败，等待动画同步
                return;
            }
            
            bool hideLeftWeapon = _currentAnimData?.hideLeftWeapon ?? false;
            bool hideRightWeapon = _currentAnimData?.hideRightWeapon ?? false;
            
            // 设置 UV Map 贴图
            UpdateUVMapTexture();
            
            // 武器需要每帧更新位置（基于锚点）——直接遍历缓存的武器渲染器
            foreach (var kv in _weaponRenderers)
            {
                RenderWeapon(kv.Key, kv.Value, hideLeftWeapon, hideRightWeapon);
            }
            
            // 每次刷新都从当前槽位完整重算 Shader 状态
            ResetEquipmentState();
            
            // 应用角色外观 (头发/胡子/面部装饰 - 来自捏人数据)
            ApplyAppearanceToShader();
            
            // 处理 Shader 装备（基于槽位）
            if (clothing != null)
                ApplySpriteEquipment(clothing, CharacterBodyPart.Torso, ClothTexProp, ClothRectProp, EnableClothProp);
            
            if (cloak != null)
                ApplySpriteEquipment(cloak, CharacterBodyPart.Torso, CloakTexProp, CloakRectProp, EnableCloakProp);
            
            if (helmet != null)
                ApplySpriteEquipment(helmet, CharacterBodyPart.Head, HelmetTexProp, HelmetRectProp, EnableHelmetProp);
            
            if (gloves != null)
                ApplyColorEquipment(gloves, LeftHandColorProp, RightHandColorProp, EnableGlovesProp);
            
            if (shoes != null)
                ApplyColorEquipment(shoes, LeftFootColorProp, RightFootColorProp, EnableShoesProp);
        }
        
        void UpdateUVMapTexture()
        {
            if (_shaderMaterial == null || _currentAnimData == null) return;
            
            // 双层 UV Map
            _debugHasBodyUVMap = _currentAnimData.bodyUVMap != null;
            _debugHasHeadUVMap = _currentAnimData.headUVMap != null;
            
            // 设置身体层 UV Map
            if (_currentAnimData.bodyUVMap != null)
                _shaderMaterial.SetTexture(BodyUVMapProp, _currentAnimData.bodyUVMap);
            
            // 设置头部层 UV Map
            if (_currentAnimData.headUVMap != null)
                _shaderMaterial.SetTexture(HeadUVMapProp, _currentAnimData.headUVMap);
        }
        
        /// <summary>
        /// 重置所有装备层为禁用状态
        /// </summary>
        void ResetEquipmentState()
        {
            if (_shaderMaterial == null) return;
            
            _shaderMaterial.SetFloat(EnableHairProp, 0);
            _shaderMaterial.SetFloat(EnableFaceAccessoryProp, 0);
            _shaderMaterial.SetFloat(EnableBeardProp, 0);
            _shaderMaterial.SetFloat(EnableHelmetProp, 0);
            _shaderMaterial.SetFloat(EnableClothProp, 0);
            _shaderMaterial.SetFloat(EnableCloakProp, 0);
            _shaderMaterial.SetFloat(EnableGlovesProp, 0);
            _shaderMaterial.SetFloat(EnableShoesProp, 0);
            _shaderMaterial.SetFloat(EnableLeftEyeProp, 0);
            _shaderMaterial.SetFloat(EnableRightEyeProp, 0);
        }
        
        /// <summary>
        /// 通用 Sprite 装备应用方法 - 支持序列帧覆盖 + 循环变体 + 基础贴图回退
        /// 用于服装、头盔等使用贴图的装备类型
        /// </summary>
        void ApplySpriteEquipment(EquipmentData equip, CharacterBodyPart part, int texProp, int rectProp, int enableProp)
        {
            if (_shaderMaterial == null) return;
            
            var facing = GetSpriteFacingForPart(part);
            
            // 优先级：1. 序列帧动画集 -> 2. 循环变体 -> 3. 基础 4 向贴图
            var seqSprite = equip.TryGetSequenceSpriteByKey(_currentAnimName, (int)facing, _frameIndex);
            Sprite finalSprite = seqSprite;
            
            if (finalSprite == null)
            {
                var loopType = GetCurrentVariantLoopType();
                finalSprite = equip.GetLoopSprite(loopType, facing, _frameIndex);
            }
            
            if (finalSprite == null || finalSprite.texture == null) return;
            
            _shaderMaterial.SetTexture(texProp, finalSprite.texture);
            _shaderMaterial.SetVector(rectProp, SpriteUtils.GetUVRect(finalSprite));
            _shaderMaterial.SetFloat(enableProp, 1);
        }
        
        /// <summary>
        /// 获取指定部位实际使用的贴图方向
        /// 如果部位配置了覆盖，返回覆盖的方向；否则返回当前动画行对应的方向
        /// </summary>
        CharacterFacing GetSpriteFacingForPart(CharacterBodyPart part)
        {
            if (_cachedFrame == null)
                return (CharacterFacing)_rowIndex;
            
            var region = _cachedFrame.GetRegion(part);
            if (region == null)
                return (CharacterFacing)_rowIndex;
            
            return region.spriteFacing;
        }
        
        /// <summary>
        /// 获取当前动画的变体循环类型
        /// </summary>
        EquipVariantLoopType GetCurrentVariantLoopType()
        {
            var animType = _currentAnimData?.animationType;
            return animType != null ? animType.variantLoopType : EquipVariantLoopType.Idle;
        }
        
        /// <summary>
        /// 设置角色外观 (头发/胡子) - 来自 CharacterAppearance
        /// 
        /// 注意: 必须同时设置 texture 和 rect，因为 sprite.texture 可能是整张 spritesheet
        /// </summary>
        void ApplyAppearanceToShader()
        {
            if (_shaderMaterial == null || appearance == null) return;
            
            bool hideHair = false;
            bool hideBeard = false;
            if (helmet != null)
            {
                hideHair = helmet.hideHair;
                hideBeard = helmet.hideBeard;
            }
            
            // 设置头发
            if (appearance.HasHair && !hideHair)
            {
                var hairSprite = appearance.GetHairByRow(_rowIndex);
                if (hairSprite != null && hairSprite.texture != null)
                {
                    _shaderMaterial.SetTexture(HairTexProp, hairSprite.texture);
                    _shaderMaterial.SetVector(HairRectProp, SpriteUtils.GetUVRect(hairSprite));
                    _shaderMaterial.SetFloat(EnableHairProp, 1);
                }
            }
            
            // 设置面部装饰（每个方向独立，未填写的方向不显示）
            if (appearance.HasFaceAccessory)
            {
                var faceAccessorySprite = appearance.GetFaceAccessoryByRow(_rowIndex);
                if (faceAccessorySprite != null && faceAccessorySprite.texture != null)
                {
                    _shaderMaterial.SetTexture(FaceAccessoryTexProp, faceAccessorySprite.texture);
                    _shaderMaterial.SetVector(FaceAccessoryRectProp, SpriteUtils.GetUVRect(faceAccessorySprite));
                    _shaderMaterial.SetFloat(EnableFaceAccessoryProp, 1);
                }
            }
            
            // 设置胡子
            if (appearance.HasBeard && !hideBeard)
            {
                var beardSprite = appearance.GetBeardByRow(_rowIndex);
                if (beardSprite != null && beardSprite.texture != null)
                {
                    _shaderMaterial.SetTexture(BeardTexProp, beardSprite.texture);
                    _shaderMaterial.SetVector(BeardRectProp, SpriteUtils.GetUVRect(beardSprite));
                    _shaderMaterial.SetFloat(EnableBeardProp, 1);
                }
            }
            
            // 设置眼睛颜色
            _shaderMaterial.SetColor(LeftEyeColorProp, appearance.leftEyeColor);
            _shaderMaterial.SetColor(RightEyeColorProp, appearance.rightEyeColor);
            _shaderMaterial.SetFloat(EnableLeftEyeProp, 1);
            _shaderMaterial.SetFloat(EnableRightEyeProp, 1);
        }
        
        /// <summary>
        /// 通用颜色装备应用方法 - 用于手套、鞋子等只需颜色参数的装备
        /// </summary>
        void ApplyColorEquipment(EquipmentData equip, int leftColorProp, int rightColorProp, int enableProp)
        {
            if (_shaderMaterial == null) return;
            
            _shaderMaterial.SetColor(leftColorProp, equip.leftColor);
            _shaderMaterial.SetColor(rightColorProp, equip.rightColor);
            _shaderMaterial.SetFloat(enableProp, 1);
        }
        
        /// <summary>
        /// 渲染武器 - 支持序列帧覆盖 + 挂点模式回退
        /// 
        /// 优先级：
        /// 1. 有序列帧 strip → 使用序列帧，位置由 Sprite pivot 决定
        /// 2. 无序列帧 → 回退挂点模式 + 4 向基础贴图
        /// </summary>
        void RenderWeapon(EquipmentData equip, SpriteRenderer sr, bool hideLeftWeapon, bool hideRightWeapon)
        {
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
            
            // 1. 先尝试序列帧覆盖
            var seqSprite = equip.TryGetSequenceSpriteByKey(_currentAnimName, _rowIndex, _frameIndex);
            
            if (seqSprite != null)
            {
                // 序列帧模式：位置由 Sprite pivot 决定，不依赖挂点精确计算
                sr.sprite = seqSprite;
                sr.enabled = true;
                sr.transform.localPosition = Vector3.zero;
                sr.transform.localRotation = Quaternion.identity;
                sr.flipX = false;
                
                // 排序仍然保持
                sr.sortingLayerID = _charRenderer.sortingLayerID;
                int sortOffset = GetWeaponSortOffset(equip.anchorType, _rowIndex);
                sr.sortingOrder = _charRenderer.sortingOrder + sortOffset;
                return;
            }
            
            // 2. 回退到挂点模式
            sr.sprite = equip.GetSpriteByRow(_rowIndex);
            
            if (_cachedFrame == null)
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
            
            // 排序 - 根据朝向和左右手决定前后
            sr.sortingLayerID = _charRenderer.sortingLayerID;
            sr.sortingOrder = _charRenderer.sortingOrder + GetWeaponSortOffset(equip.anchorType, _rowIndex);
        }
        
        // 武器排序偏移表：[rowIndex] = 左手偏移（右手取反）
        // SE(0): 左后(-1)/右前(+1), SW(1): 左前(+1)/右后(-1), NE(2): 左前(+1)/右后(-1), NW(3): 左后(-1)/右前(+1)
        static readonly int[] LeftWeaponSortOffsets = { -1, 1, 1, -1 };
        
        int GetWeaponSortOffset(AnchorType anchorType, int rowIndex)
        {
            int leftOffset = (rowIndex >= 0 && rowIndex < LeftWeaponSortOffsets.Length) 
                ? LeftWeaponSortOffsets[rowIndex] : 1;
            return anchorType == AnchorType.LeftWeapon ? leftOffset : -leftOffset;
        }
        
#if UNITY_EDITOR
        void OnValidate()
        {
            if (Application.isPlaying) Refresh();
        }
#endif
    }
}
