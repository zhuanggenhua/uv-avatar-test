using System.Collections.Generic;
using System.Linq;

using EquipmentSystem.Data;

using UnityEngine;

namespace EquipmentSystem.Runtime
{
    /// <summary>
    /// 装备渲染器 (配置驱动版本)
    /// 新增装备类型时只需在 EquipTypeRegistry 添加配置
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class EquipmentRenderer : MonoBehaviour
    {
        [Header("数据")]
        public CharacterFrameData frameData;
        
        [Header("角色外观")]
        public CharacterAppearance appearance;
        
        [Header("初始装备（可选）")]
        public List<EquipmentData> initialEquipments = new List<EquipmentData>();
        
        [Header("调试")]
        public Shader overrideShader;
        
        [Header("运行时状态 (只读)")]
        [SerializeField] string _debugCurrentAnim = "";
        [SerializeField] string _debugAnimatorState = "";
        [SerializeField] bool _debugHasBodyUVMap = false;
        [SerializeField] bool _debugHasHeadUVMap = false;
        
        // ========== 槽位：统一用字典管理 ==========
        readonly Dictionary<EquipmentType, EquipmentData> _slots = new Dictionary<EquipmentType, EquipmentData>();
        
        // ========== 武器槽位：主手 + 副手 ==========
        EquipmentData _mainHandWeapon;
        EquipmentData _offHandWeapon;
        
        // 渲染器缓存
        SpriteRenderer _charRenderer;
        readonly Dictionary<EquipmentData, SpriteRenderer> _weaponRenderers = new Dictionary<EquipmentData, SpriteRenderer>();
        
        // 动画同步
        Animator _animator;
        string _currentAnimName;
        List<string> _validAnimParams;
        bool _animParamsCached;
        
        // 帧同步
        Sprite _lastSprite;
        int _frameIndex;
        int _rowIndex;
        FrameData _cachedFrame;
        AnimationData _currentAnimData;
        
        // Shader
        Material _shaderMaterial;
        
        // 外观相关 Shader 属性（不走配置表的特殊处理）
        static readonly int BodyUVMapProp = Shader.PropertyToID("_BodyUVMap");
        static readonly int HeadUVMapProp = Shader.PropertyToID("_HeadUVMap");
        static readonly int HairTexProp = Shader.PropertyToID("_HairTex");
        static readonly int HairRectProp = Shader.PropertyToID("_HairRect");
        static readonly int EnableHairProp = Shader.PropertyToID("_EnableHair");
        static readonly int FaceAccessoryTexProp = Shader.PropertyToID("_FaceAccessoryTex");
        static readonly int FaceAccessoryRectProp = Shader.PropertyToID("_FaceAccessoryRect");
        static readonly int EnableFaceAccessoryProp = Shader.PropertyToID("_EnableFaceAccessory");
        static readonly int BeardTexProp = Shader.PropertyToID("_BeardTex");
        static readonly int BeardRectProp = Shader.PropertyToID("_BeardRect");
        static readonly int EnableBeardProp = Shader.PropertyToID("_EnableBeard");
        static readonly int LeftEyeColorProp = Shader.PropertyToID("_LeftEyeColor");
        static readonly int RightEyeColorProp = Shader.PropertyToID("_RightEyeColor");
        static readonly int EnableLeftEyeProp = Shader.PropertyToID("_EnableLeftEye");
        static readonly int EnableRightEyeProp = Shader.PropertyToID("_EnableRightEye");
        // 武器通用参数
        static readonly int CharFrameRectProp = Shader.PropertyToID("_CharFrameRect");
        // 主手武器参数（Weapon0）
        static readonly int Weapon0TexProp = Shader.PropertyToID("_Weapon0Tex");
        static readonly int Weapon0RectProp = Shader.PropertyToID("_Weapon0Rect");
        static readonly int Weapon0AnchorFrameUVProp = Shader.PropertyToID("_Weapon0AnchorFrameUV");
        static readonly int Weapon0RotCosSinProp = Shader.PropertyToID("_Weapon0RotCosSin");
        static readonly int Weapon0PivotUVProp = Shader.PropertyToID("_Weapon0PivotUV");
        static readonly int Weapon0FlipXProp = Shader.PropertyToID("_Weapon0FlipX");
        static readonly int Weapon0DepthModeProp = Shader.PropertyToID("_Weapon0DepthMode");
        static readonly int Weapon0EnabledProp = Shader.PropertyToID("_Weapon0Enabled");
        // 副手武器参数（Weapon1）
        static readonly int Weapon1TexProp = Shader.PropertyToID("_Weapon1Tex");
        static readonly int Weapon1RectProp = Shader.PropertyToID("_Weapon1Rect");
        static readonly int Weapon1AnchorFrameUVProp = Shader.PropertyToID("_Weapon1AnchorFrameUV");
        static readonly int Weapon1RotCosSinProp = Shader.PropertyToID("_Weapon1RotCosSin");
        static readonly int Weapon1PivotUVProp = Shader.PropertyToID("_Weapon1PivotUV");
        static readonly int Weapon1FlipXProp = Shader.PropertyToID("_Weapon1FlipX");
        static readonly int Weapon1DepthModeProp = Shader.PropertyToID("_Weapon1DepthMode");
        static readonly int Weapon1EnabledProp = Shader.PropertyToID("_Weapon1Enabled");
        
        void Awake()
        {
            _charRenderer = GetComponent<SpriteRenderer>();
            _animator = GetComponentInChildren<Animator>();
            InitMaterial();
        }
        
        void Start()
        {
            // 初始装备
            foreach (var e in initialEquipments)
            {
                if (e != null)
                    Equip(e, false);
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
        
        /// <summary>
        /// 装备（配置驱动，无 switch）
        /// </summary>
        public void Equip(EquipmentData equip, bool autoRefresh = true)
        {
            if (equip == null) return;
            
            var cfg = EquipTypeRegistry.Get(equip.type);
            if (cfg == null) return;
            
            if (cfg.RenderMode == EquipRenderMode.Weapon)
            {
                EquipWeapon(equip);
            }
            else
            {
                _slots[equip.type] = equip;
            }
            
            if (autoRefresh) Refresh();
        }
        
        /// <summary>
        /// 装备武器（根据 WeaponSlotType 自动分配到主手/副手）
        /// </summary>
        void EquipWeapon(EquipmentData equip)
        {
            switch (equip.weaponSlotType)
            {
                case WeaponSlotType.MainHand:
                case WeaponSlotType.TwoHand:
                case WeaponSlotType.DualWield:
                    // 卸下旧主手
                    if (_mainHandWeapon != null && _mainHandWeapon != equip)
                        UnequipWeaponInternal(_mainHandWeapon);
                    // 双手/双持禁止副手
                    if (equip.weaponSlotType != WeaponSlotType.MainHand && _offHandWeapon != null)
                        UnequipWeaponInternal(_offHandWeapon);
                    _mainHandWeapon = equip;
                    CreateWeaponRenderer(equip);
                    break;
                    
                case WeaponSlotType.OffHand:
                    // 检查主手是否允许副手
                    if (_mainHandWeapon != null && 
                        (_mainHandWeapon.weaponSlotType == WeaponSlotType.TwoHand ||
                         _mainHandWeapon.weaponSlotType == WeaponSlotType.DualWield))
                    {
                        Debug.LogWarning("[EquipmentRenderer] 双手/双持武器不允许装备副手");
                        return;
                    }
                    // 卸下旧副手
                    if (_offHandWeapon != null && _offHandWeapon != equip)
                        UnequipWeaponInternal(_offHandWeapon);
                    _offHandWeapon = equip;
                    CreateWeaponRenderer(equip);
                    break;
            }
        }
        
        /// <summary>
        /// 卸下装备
        /// </summary>
        public void Unequip(EquipmentData equip, bool autoRefresh = true)
        {
            if (equip == null) return;
            
            var cfg = EquipTypeRegistry.Get(equip.type);
            if (cfg == null) return;
            
            if (cfg.RenderMode == EquipRenderMode.Weapon)
            {
                UnequipWeaponInternal(equip);
            }
            else
            {
                if (_slots.TryGetValue(equip.type, out var current) && current == equip)
                    _slots.Remove(equip.type);
            }
            
            if (autoRefresh) Refresh();
        }
        
        /// <summary>
        /// 内部卸下武器（不触发 Refresh）
        /// </summary>
        void UnequipWeaponInternal(EquipmentData equip)
        {
            if (equip == null) return;
            
            if (_mainHandWeapon == equip) _mainHandWeapon = null;
            if (_offHandWeapon == equip) _offHandWeapon = null;
            
            // 销毁武器渲染器
            if (_weaponRenderers.TryGetValue(equip, out var sr))
            {
                Destroy(sr.gameObject);
                _weaponRenderers.Remove(equip);
            }
        }
        
        /// <summary>
        /// 设置角色外观
        /// </summary>
        public void SetAppearance(CharacterAppearance newAppearance)
        {
            if (appearance == newAppearance) return;
            appearance = newAppearance;
            Refresh();
        }
        
        /// <summary>
        /// 卸下所有装备
        /// </summary>
        public void UnequipAll()
        {
            var allEquipped = _slots.Values.ToList();
            if (_mainHandWeapon != null) allEquipped.Add(_mainHandWeapon);
            if (_offHandWeapon != null) allEquipped.Add(_offHandWeapon);
            
            foreach (var e in allEquipped)
                Unequip(e, false);
            
            Refresh();
        }
        
        /// <summary>
        /// 获取指定类型当前装备（武器返回主手）
        /// </summary>
        public EquipmentData GetEquipped(EquipmentType type)
        {
            var cfg = EquipTypeRegistry.Get(type);
            if (cfg == null) return null;
            
            if (cfg.RenderMode == EquipRenderMode.Weapon)
                return _mainHandWeapon;
            
            return _slots.TryGetValue(type, out var equip) ? equip : null;
        }
        
        /// <summary>
        /// 获取主手武器
        /// </summary>
        public EquipmentData GetMainHandWeapon() => _mainHandWeapon;
        
        /// <summary>
        /// 获取副手武器
        /// </summary>
        public EquipmentData GetOffHandWeapon() => _offHandWeapon;
        
        /// <summary>
        /// 检查当前是否允许装备副手
        /// </summary>
        public bool CanEquipOffHand()
        {
            if (_mainHandWeapon == null) return true;
            return _mainHandWeapon.weaponSlotType == WeaponSlotType.MainHand;
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
            
            if (_currentAnimData == null)
                _currentAnimData = frameData.GetAnimationByKey(_currentAnimName);
            
            if (_currentAnimData == null) return;
            
            UpdateUVMapTexture();
            
            // 重置所有装备层（包括武器）
            ResetEquipmentState();
            
            // ========== 武器渲染（支持主手+副手，双持双锚点）==========
            RenderWeapons();
            
            // 角色外观
            ApplyAppearanceToShader();
            
            // ========== 配置驱动：遍历所有装备配置并应用 ==========
            foreach (var cfg in EquipTypeRegistry.All)
            {
                if (cfg.RenderMode == EquipRenderMode.None || cfg.RenderMode == EquipRenderMode.Weapon)
                    continue;
                
                if (!_slots.TryGetValue(cfg.Type, out var equip) || equip == null)
                    continue;
                
                switch (cfg.RenderMode)
                {
                    case EquipRenderMode.Sprite:
                        ApplySpriteEquipment(equip, cfg);
                        break;
                    case EquipRenderMode.Color:
                        ApplyColorEquipment(equip, cfg);
                        break;
                }
            }
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
        /// 重置所有装备层为禁用状态（配置驱动）
        /// </summary>
        void ResetEquipmentState()
        {
            if (_shaderMaterial == null) return;
            
            // 外观层（不走配置表）
            _shaderMaterial.SetFloat(EnableHairProp, 0);
            _shaderMaterial.SetFloat(EnableFaceAccessoryProp, 0);
            _shaderMaterial.SetFloat(EnableBeardProp, 0);
            _shaderMaterial.SetFloat(EnableLeftEyeProp, 0);
            _shaderMaterial.SetFloat(EnableRightEyeProp, 0);
            // 双武器
            _shaderMaterial.SetFloat(Weapon0EnabledProp, 0);
            _shaderMaterial.SetFloat(Weapon1EnabledProp, 0);
            
            // 装备层（遍历配置表）
            foreach (var cfg in EquipTypeRegistry.All)
            {
                if (cfg.EnablePropId != 0)
                    _shaderMaterial.SetFloat(cfg.EnablePropId, 0);
            }
        }
        
        /// <summary>
        /// Sprite 装备应用（配置驱动）
        /// </summary>
        void ApplySpriteEquipment(EquipmentData equip, EquipTypeConfig cfg)
        {
            if (_shaderMaterial == null) return;
            
            var facing = GetSpriteFacingForPart(cfg.BodyPart);
            
            // 1. 序列帧
            var seqSprite = equip.TryGetSequenceSpriteByKey(_currentAnimName, (int)facing, _frameIndex);
            Sprite finalSprite = seqSprite;

            // 2. 帧变体 → 基础贴图
            if (finalSprite == null)
            {
                var variant = GetVariantForPart(cfg.BodyPart);
                finalSprite = equip.GetSprite(facing, variant);
            }
            
            if (finalSprite == null || finalSprite.texture == null) return;
            
            _shaderMaterial.SetTexture(cfg.TexPropId, finalSprite.texture);
            _shaderMaterial.SetVector(cfg.RectPropId, SpriteUtils.GetUVRect(finalSprite));
            _shaderMaterial.SetFloat(cfg.EnablePropId, 1);
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
        /// 获取指定部位当前帧使用的变体（默认 Base）
        /// </summary>
        FrameVariant GetVariantForPart(CharacterBodyPart part)
        {
            if (_cachedFrame == null)
                return FrameVariant.Base;

            var region = _cachedFrame.GetRegion(part);
            if (region == null)
                return FrameVariant.Base;

            return region.variant;
        }
        
        /// <summary>
        /// 设置角色外观 (头发/胡子) - 来自 CharacterAppearance
        /// </summary>
        void ApplyAppearanceToShader()
        {
            if (_shaderMaterial == null || appearance == null) return;
            
            // 头盔隐藏配置
            bool hideHair = false;
            bool hideBeard = false;
            var helmet = GetEquipped(EquipmentType.Helmet);
            if (helmet != null)
            {
                hideHair = helmet.hideHair;
                hideBeard = helmet.hideBeard;
            }
            
            // 头部外观统一跟随 Head 的 spriteFacing
            var headFacing = GetSpriteFacingForPart(CharacterBodyPart.Head);
            
            // 设置头发
            if (appearance.HasHair && !hideHair)
            {
                var hairSprite = appearance.GetHairSprite(headFacing);
                if (hairSprite != null && hairSprite.texture != null)
                {
                    _shaderMaterial.SetTexture(HairTexProp, hairSprite.texture);
                    _shaderMaterial.SetVector(HairRectProp, SpriteUtils.GetUVRect(hairSprite));
                    _shaderMaterial.SetFloat(EnableHairProp, 1);
                }
            }
            
            // 设置面部装饰（只在朝南时显示）
            if (appearance.HasFaceAccessory)
            {
                var faceAccessorySprite = appearance.GetFaceAccessorySprite(headFacing);
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
                var beardSprite = appearance.GetBeardSprite(headFacing);
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
        /// 颜色装备应用（配置驱动）
        /// </summary>
        void ApplyColorEquipment(EquipmentData equip, EquipTypeConfig cfg)
        {
            if (_shaderMaterial == null) return;
            
            _shaderMaterial.SetColor(cfg.LeftColorPropId, equip.leftColor);
            _shaderMaterial.SetColor(cfg.RightColorPropId, equip.rightColor);
            _shaderMaterial.SetFloat(cfg.EnablePropId, 1);
        }
        
        /// <summary>
        /// 渲染所有武器（主手 + 副手，支持双持双锚点）
        /// </summary>
        void RenderWeapons()
        {
            // 先隐藏所有武器子对象
            foreach (var kv in _weaponRenderers)
                kv.Value.enabled = false;
            
            // 更新角色帧 Rect（双武器共用）
            UpdateCharFrameRect();
            
            if (_mainHandWeapon != null)
            {
                // 双持：同一装备在两个锚点显示
                if (_mainHandWeapon.weaponSlotType == WeaponSlotType.DualWield)
                {
                    RenderWeaponSlot(_mainHandWeapon, AnchorType.LeftWeapon, 0);
                    RenderWeaponSlot(_mainHandWeapon, AnchorType.RightWeapon, 1);
                }
                else
                {
                    // 单手/双手：仅使用左手锚点
                    RenderWeaponSlot(_mainHandWeapon, AnchorType.LeftWeapon, 0);
                }
            }
            
            // 副手武器
            if (_offHandWeapon != null)
            {
                RenderWeaponSlot(_offHandWeapon, AnchorType.RightWeapon, 1);
            }
        }
        
        /// <summary>
        /// 更新角色帧 Rect（供 Shader 使用）
        /// </summary>
        void UpdateCharFrameRect()
        {
            if (_shaderMaterial == null) return;
            var charSprite = _charRenderer.sprite;
            if (charSprite == null || charSprite.texture == null) return;
            
            var charRect = charSprite.rect;
            float texW = charSprite.texture.width;
            float texH = charSprite.texture.height;
            var charFrameRect = new Vector4(
                charRect.xMin / texW,
                charRect.yMin / texH,
                charRect.xMax / texW,
                charRect.yMax / texH);
            _shaderMaterial.SetVector(CharFrameRectProp, charFrameRect);
        }
        
        /// <summary>
        /// 渲染单个武器槽位
        /// </summary>
        /// <param name="equip">武器装备</param>
        /// <param name="anchorType">使用的锚点类型</param>
        /// <param name="shaderSlot">Shader 武器槽位（0=主手, 1=副手）</param>
        void RenderWeaponSlot(EquipmentData equip, AnchorType anchorType, int shaderSlot)
        {
            if (equip == null) return;
            
            // 获取对应的 SpriteRenderer
            _weaponRenderers.TryGetValue(equip, out var sr);
            
            // 1. 优先使用序列帧
            var seqSprite = equip.TryGetSequenceSpriteByKey(_currentAnimName, _rowIndex, _frameIndex);
            if (seqSprite != null && sr != null)
            {
                sr.sprite = seqSprite;
                sr.enabled = true;
                sr.transform.localPosition = Vector3.zero;
                sr.transform.localRotation = Quaternion.identity;
                sr.flipX = false;
                sr.sortingLayerID = _charRenderer.sortingLayerID;
                sr.sortingOrder = _charRenderer.sortingOrder + GetWeaponSortOffset(anchorType, _rowIndex);
                return;
            }
            
            // 2. 无序列帧 → Shader 武器层
            if (_shaderMaterial == null || _cachedFrame == null) return;
            
            var weaponSprite = equip.GetSpriteByRow(_rowIndex);
            var charSprite = _charRenderer.sprite;
            if (weaponSprite == null || weaponSprite.texture == null || charSprite == null) return;
            
            var anchor = _cachedFrame.GetAnchor(anchorType);
            if (anchor == null) return;
            
            // 更新子对象 Transform（用于挂特效）
            if (sr != null)
            {
                float ppu = charSprite.pixelsPerUnit;
                float equipCenterX = weaponSprite.rect.width * 0.5f;
                float equipCenterY = weaponSprite.rect.height * 0.5f;
                
                float deltaX = anchor.position.x - equipCenterX;
                float deltaY = anchor.position.y - equipCenterY;
                sr.transform.localPosition = new Vector3(deltaX / ppu, -deltaY / ppu, 0);
                sr.transform.localRotation = Quaternion.Euler(0, 0, anchor.GetRotationAngle());
            }
            
            // Shader 参数
            int texProp = shaderSlot == 0 ? Weapon0TexProp : Weapon1TexProp;
            int rectProp = shaderSlot == 0 ? Weapon0RectProp : Weapon1RectProp;
            int anchorProp = shaderSlot == 0 ? Weapon0AnchorFrameUVProp : Weapon1AnchorFrameUVProp;
            int rotProp = shaderSlot == 0 ? Weapon0RotCosSinProp : Weapon1RotCosSinProp;
            int pivotProp = shaderSlot == 0 ? Weapon0PivotUVProp : Weapon1PivotUVProp;
            int flipProp = shaderSlot == 0 ? Weapon0FlipXProp : Weapon1FlipXProp;
            int depthProp = shaderSlot == 0 ? Weapon0DepthModeProp : Weapon1DepthModeProp;
            int enableProp = shaderSlot == 0 ? Weapon0EnabledProp : Weapon1EnabledProp;
            
            _shaderMaterial.SetTexture(texProp, weaponSprite.texture);
            _shaderMaterial.SetVector(rectProp, SpriteUtils.GetUVRect(weaponSprite));
            
            // 锚点 UV
            var charRect = charSprite.rect;
            int frameW = _currentAnimData != null ? _currentAnimData.frameSize.x : (int)charRect.width;
            int frameH = _currentAnimData != null ? _currentAnimData.frameSize.y : (int)charRect.height;
            frameW = Mathf.Max(frameW, 1);
            frameH = Mathf.Max(frameH, 1);
            float anchorU = (anchor.position.x + 0.5f) / frameW;
            float anchorV = 1f - (anchor.position.y + 0.5f) / frameH;
            _shaderMaterial.SetVector(anchorProp, new Vector4(anchorU, anchorV, 0f, 0f));
            
            // 武器握点 UV：基于 UV 画板中的左右手基准像素
            Vector4 pivotUV = new Vector4(0.5f, 0.5f, 0f, 0f);
            if (frameData != null)
            {
                var pivotPixel = anchorType == AnchorType.LeftWeapon
                    ? frameData.leftHandWeaponPivot
                    : frameData.rightHandWeaponPivot;
                var palSize = frameData.paletteSize;
                int palW = Mathf.Max(palSize.x, 1);
                int palH = Mathf.Max(palSize.y, 1);
                float pivotU = (pivotPixel.x + 0.5f) / palW;
                float pivotV = 1f - (pivotPixel.y + 0.5f) / palH;
                pivotUV = new Vector4(pivotU, pivotV, 0f, 0f);
            }
            _shaderMaterial.SetVector(pivotProp, pivotUV);
            
            // 旋转
            float angleDeg = anchor.GetRotationAngle();
            float angleRad = angleDeg * Mathf.Deg2Rad;
            _shaderMaterial.SetVector(rotProp, new Vector4(Mathf.Cos(angleRad), Mathf.Sin(angleRad), 0f, 0f));
            
            // 水平中线镜像：根据行号决定（从 SE 贴图生成 SW/NW）
            var facing = (CharacterFacing)_rowIndex;
            bool mirror = facing == CharacterFacing.SouthWest || facing == CharacterFacing.NorthWest;
            _shaderMaterial.SetFloat(flipProp, mirror ? 1f : 0f);
            
            // 深度模式：前景(S/E) 在身体前，北向在身体后
            var dir = CharacterFrameData.GetFacingDirection(facing);
            _shaderMaterial.SetFloat(depthProp, dir == FacingDirection.Front ? 1f : 0f);
            
            // 启用
            _shaderMaterial.SetFloat(enableProp, 1f);
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
