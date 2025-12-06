using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EquipmentSystem
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
        [SerializeField]
        string _debugCurrentAnim = "";

        [SerializeField]
        string _debugAnimatorState = "";

        [SerializeField]
        bool _debugHasBodyUVMap = false;

        [SerializeField]
        bool _debugHasHeadUVMap = false;

        // ========== 槽位：统一用字典管理 ==========
        readonly Dictionary<EquipmentType, EquipmentData> _slots =
            new Dictionary<EquipmentType, EquipmentData>();

        // ========== 武器槽位：主手 + 副手 ==========
        EquipmentData _mainHandWeapon;
        EquipmentData _offHandWeapon;

        // 渲染器缓存
        SpriteRenderer _charRenderer;
        readonly Dictionary<EquipmentData, SpriteRenderer> _weaponRenderers =
            new Dictionary<EquipmentData, SpriteRenderer>();

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
        static readonly int BodyInFrontProp = Shader.PropertyToID("_BodyInFront");
        
        // 像素级阴影参数
        static readonly int ShadowModeProp = Shader.PropertyToID("_ShadowMode");
        static readonly int ShadowLeftXProp = Shader.PropertyToID("_ShadowLeftX");
        static readonly int ShadowRightXProp = Shader.PropertyToID("_ShadowRightX");
        static readonly int ShadowCenterXProp = Shader.PropertyToID("_ShadowCenterX");
        static readonly int ShadowBaseYProp = Shader.PropertyToID("_ShadowBaseY");

        // 武器通用参数
        static readonly int CharFrameRectProp = Shader.PropertyToID("_CharFrameRect");

        // 主手武器参数（Weapon0）
        static readonly int Weapon0TexProp = Shader.PropertyToID("_Weapon0Tex");
        static readonly int Weapon0RectProp = Shader.PropertyToID("_Weapon0Rect");
        static readonly int Weapon0AnchorFrameUVProp = Shader.PropertyToID("_Weapon0AnchorFrameUV");
        static readonly int Weapon0RotCosSinProp = Shader.PropertyToID("_Weapon0RotCosSin");
        static readonly int Weapon0FlipXProp = Shader.PropertyToID("_Weapon0FlipX");
        static readonly int Weapon0DepthModeProp = Shader.PropertyToID("_Weapon0DepthMode");
        static readonly int Weapon0EnabledProp = Shader.PropertyToID("_Weapon0Enabled");
        static readonly int Weapon0HandInFrontProp = Shader.PropertyToID("_Weapon0HandInFront");

        // 副手武器参数（Weapon1）
        static readonly int Weapon1TexProp = Shader.PropertyToID("_Weapon1Tex");
        static readonly int Weapon1RectProp = Shader.PropertyToID("_Weapon1Rect");
        static readonly int Weapon1AnchorFrameUVProp = Shader.PropertyToID("_Weapon1AnchorFrameUV");
        static readonly int Weapon1RotCosSinProp = Shader.PropertyToID("_Weapon1RotCosSin");
        static readonly int Weapon1FlipXProp = Shader.PropertyToID("_Weapon1FlipX");
        static readonly int Weapon1DepthModeProp = Shader.PropertyToID("_Weapon1DepthMode");
        static readonly int Weapon1EnabledProp = Shader.PropertyToID("_Weapon1Enabled");
        static readonly int Weapon1HandInFrontProp = Shader.PropertyToID("_Weapon1HandInFront");

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
            if (_animParamsCached || _animator == null)
                return;

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
                if (
                    param.type == AnimatorControllerParameterType.Bool
                    && keywordSet.Contains(param.name)
                )
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
            if (_animator == null || frameData == null)
                return;

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
            if (frameData == null)
                return null;

            // 精确匹配
            var exact = frameData.GetAnimationByKey(key);
            if (exact != null)
                return exact;

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
            var shader =
                overrideShader != null
                    ? overrideShader
                    : Shader.Find("EquipmentSystem/EquipmentUV");

            if (shader == null)
            {
                Debug.LogError(
                    "[EquipmentRenderer] 找不到 EquipmentSystem/EquipmentUV Shader！"
                        + "请确保 Shader 在 Project Settings > Graphics > Always Included Shaders 中，"
                        + "或手动拖拽 Shader 到 overrideShader 字段"
                );
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
            if (_lastSprite == null || frameData == null || _currentAnimData == null)
                return;

            // 从 Sprite 的 rect 位置计算帧索引和行索引
            var rect = _lastSprite.rect;
            int frameW = _currentAnimData.frameSize.x;
            int frameH = _currentAnimData.frameSize.y;

            if (frameW > 0 && frameH > 0)
            {
                _frameIndex = Mathf.FloorToInt(rect.x / frameW);
                // Unity Sprite 的 Y 是从底部计算的，需要转换
                _rowIndex = Mathf.FloorToInt(
                    (_lastSprite.texture.height - rect.y - rect.height) / frameH
                );
                Refresh();
            }
        }

        /// <summary>
        /// 装备（配置驱动，无 switch）
        /// </summary>
        public void Equip(EquipmentData equip, bool autoRefresh = true)
        {
            if (equip == null)
                return;

            var cfg = EquipTypeRegistry.Get(equip.type);
            if (cfg == null)
                return;

            if (cfg.RenderMode == EquipRenderMode.Weapon)
            {
                EquipWeapon(equip);
            }
            else
            {
                _slots[equip.type] = equip;
            }

            if (autoRefresh)
                Refresh();
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
                    if (
                        _mainHandWeapon != null
                        && (
                            _mainHandWeapon.weaponSlotType == WeaponSlotType.TwoHand
                            || _mainHandWeapon.weaponSlotType == WeaponSlotType.DualWield
                        )
                    )
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
            if (equip == null)
                return;

            var cfg = EquipTypeRegistry.Get(equip.type);
            if (cfg == null)
                return;

            if (cfg.RenderMode == EquipRenderMode.Weapon)
            {
                UnequipWeaponInternal(equip);
            }
            else
            {
                if (_slots.TryGetValue(equip.type, out var current) && current == equip)
                    _slots.Remove(equip.type);
            }

            if (autoRefresh)
                Refresh();
        }

        /// <summary>
        /// 内部卸下武器（不触发 Refresh）
        /// </summary>
        void UnequipWeaponInternal(EquipmentData equip)
        {
            if (equip == null)
                return;

            if (_mainHandWeapon == equip)
                _mainHandWeapon = null;
            if (_offHandWeapon == equip)
                _offHandWeapon = null;

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
            if (appearance == newAppearance)
                return;
            appearance = newAppearance;
            Refresh();
        }

        /// <summary>
        /// 卸下所有装备
        /// </summary>
        public void UnequipAll()
        {
            var allEquipped = _slots.Values.ToList();
            if (_mainHandWeapon != null)
                allEquipped.Add(_mainHandWeapon);
            if (_offHandWeapon != null)
                allEquipped.Add(_offHandWeapon);

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
            if (cfg == null)
                return null;

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
        /// 获取当前头部插槽装备（Helmet / Hat / Mask，按优先级）
        /// </summary>
        public EquipmentData GetHeadSlotEquipment()
        {
            // 按优先级检查：Helmet > Hat > Mask
            if (_slots.TryGetValue(EquipmentType.Helmet, out var helmet) && helmet != null)
                return helmet;
            if (_slots.TryGetValue(EquipmentType.Hat, out var hat) && hat != null)
                return hat;
            if (_slots.TryGetValue(EquipmentType.Mask, out var mask) && mask != null)
                return mask;
            return null;
        }

        /// <summary>
        /// 检查当前是否允许装备副手
        /// </summary>
        public bool CanEquipOffHand()
        {
            if (_mainHandWeapon == null)
                return true;
            return _mainHandWeapon.weaponSlotType == WeaponSlotType.MainHand;
        }

        void CreateWeaponRenderer(EquipmentData equip)
        {
            if (_weaponRenderers.ContainsKey(equip))
                return;

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

            if (_currentAnimData == null)
                return;

            UpdateShadowHeight();

            UpdateUVMapTexture();

            // 重置所有装备层（包括武器）
            ResetEquipmentState();

            // 身体/头部前后关系（基于躯干部位的实际方向）
            UpdateBodyDepthMode();

            // ========== 武器渲染（支持主手+副手，双持双锚点）==========
            RenderWeapons();

            // 角色外观
            ApplyAppearanceToShader();

            // ========== 配置驱动：遍历所有装备配置并应用 ==========
            foreach (var cfg in EquipTypeRegistry.All)
            {
                if (
                    cfg.RenderMode == EquipRenderMode.None
                    || cfg.RenderMode == EquipRenderMode.Weapon
                )
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
            if (_shaderMaterial == null || _currentAnimData == null)
                return;

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
            if (_shaderMaterial == null)
                return;

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

        void UpdateBodyDepthMode()
        {
            if (_shaderMaterial == null)
                return;
            // 根据“身体实际方向”(Torso.spriteFacing) 判断深度模式：
            // - 当身体朝南 (SouthEast/SouthWest) 时，身体在头后(_BodyInFront=0)
            // - 当身体朝北 (NorthEast/NorthWest) 时，身体在头前(_BodyInFront=1)
            // 只看躯干的实际方向，不受 Head.spriteFacing 影响。

            CharacterFacing facing;
            if (_cachedFrame != null)
            {
                var torsoRegion = _cachedFrame.GetRegion(CharacterBodyPart.Torso);
                if (torsoRegion != null)
                    facing = torsoRegion.spriteFacing;
                else
                    facing = (CharacterFacing)_rowIndex; // 若缺少躯干区域，退回到当前行方向
            }
            else
            {
                facing = (CharacterFacing)_rowIndex;
            }

            int row = (int)facing;
            if (row < 0 || row > 3)
                row = 0;

            bool bodyInFront = row >= 2;
            _shaderMaterial.SetFloat(BodyInFrontProp, bodyInFront ? 1f : 0f);
        }

        void UpdateShadowHeight()
        {
            if (_shaderMaterial == null)
                return;

            if (frameData == null || _cachedFrame == null)
            {
                _shaderMaterial.SetFloat(ShadowModeProp, -1f); // 无阴影
                return;
            }

            var leftFoot = _cachedFrame.GetLimbPixels(CharacterBodyPart.LeftFoot);
            var rightFoot = _cachedFrame.GetLimbPixels(CharacterBodyPart.RightFoot);

            bool hasLeft = leftFoot != null && leftFoot.Count > 0;
            bool hasRight = rightFoot != null && rightFoot.Count > 0;

            if (!hasLeft && !hasRight)
            {
                _shaderMaterial.SetFloat(ShadowModeProp, -1f); // 无阴影
                return;
            }

            // 计算脚部边界
            int minY = int.MaxValue;
            int leftMinX = int.MaxValue, leftMaxX = int.MinValue;
            int rightMinX = int.MaxValue, rightMaxX = int.MinValue;
            int leftFootY = int.MaxValue, rightFootY = int.MaxValue;

            if (hasLeft)
            {
                for (int i = 0; i < leftFoot.Count; i++)
                {
                    var p = leftFoot[i];
                    if (p.y < minY) minY = p.y;
                    if (p.y < leftFootY) leftFootY = p.y;
                    if (p.x < leftMinX) leftMinX = p.x;
                    if (p.x > leftMaxX) leftMaxX = p.x;
                }
            }

            if (hasRight)
            {
                for (int i = 0; i < rightFoot.Count; i++)
                {
                    var p = rightFoot[i];
                    if (p.y < minY) minY = p.y;
                    if (p.y < rightFootY) rightFootY = p.y;
                    if (p.x < rightMinX) rightMinX = p.x;
                    if (p.x > rightMaxX) rightMaxX = p.x;
                }
            }

            int groundY = frameData.groundPixelY;
            int heightDiff = groundY - minY;
            
            // 获取帧尺寸
            int frameSizeX = _currentAnimData?.frameSize.x ?? 32;
            int frameSizeY = _currentAnimData?.frameSize.y ?? 32;

            // 根据高度差确定阴影模式
            float shadowMode;
            float leftX = 0, rightX = 0, centerX = 0;

            if (heightDiff <= 0)
            {
                // Mode 0: 地面状态 - 脚在基线上或更低
                shadowMode = 0;
            }
            else if (heightDiff <= 2)
            {
                // Mode 1: 离地渲染 - 脚在基线y-1到y-2位置
                shadowMode = 1;
                // 计算左脚到右脚的范围
                int overallMinX = Mathf.Min(hasLeft ? leftMinX : 999, hasRight ? rightMinX : 999);
                int overallMaxX = Mathf.Max(hasLeft ? leftMaxX : -999, hasRight ? rightMaxX : -999);
                leftX = overallMinX / (float)frameSizeX;
                rightX = overallMaxX / (float)frameSizeX;
            }
            else if (heightDiff <= 9)
            {
                // Mode 2: 空中模式 - 脚高于基线3-9格
                shadowMode = 2;
                
                // 判断哪只脚在下方
                bool leftLower = leftFootY <= rightFootY;
                if (leftLower && hasLeft)
                {
                    // 左脚在下，取右边像素
                    centerX = leftMaxX / (float)frameSizeX;
                }
                else if (hasRight)
                {
                    // 右脚在下，取左边像素
                    centerX = rightMinX / (float)frameSizeX;
                }
            }
            else
            {
                // Mode 3: 完全离地 - 脚高于基线10格以上
                shadowMode = 3;
                // 使用帧中心
                centerX = 0.5f;
            }

            // 写入Shader参数
            _shaderMaterial.SetFloat(ShadowModeProp, shadowMode);
            _shaderMaterial.SetFloat(ShadowLeftXProp, leftX);
            _shaderMaterial.SetFloat(ShadowRightXProp, rightX);
            _shaderMaterial.SetFloat(ShadowCenterXProp, centerX);
            _shaderMaterial.SetFloat(ShadowBaseYProp, groundY / (float)frameSizeY);
        }

        /// <summary>
        /// 根据 facing 获取当前动画的装备序列帧（若无动画集则返回 null）
        /// </summary>
        Sprite GetEquipSequenceSprite(EquipmentData equip, CharacterFacing facing)
        {
            if (equip == null)
                return null;

            return equip.TryGetSequenceSpriteByKey(
                _currentAnimName,
                (int)facing,
                _frameIndex
            );
        }

        /// <summary>
        /// Sprite 装备应用（配置驱动）
        /// </summary>
        void ApplySpriteEquipment(EquipmentData equip, EquipTypeConfig cfg)
        {
            if (_shaderMaterial == null)
                return;

            var facing = GetSpriteFacingForPart(cfg.BodyPart);

            // 1. 序列帧
            var seqSprite = GetEquipSequenceSprite(equip, facing);
            Sprite finalSprite = seqSprite;

            // 2. 帧变体 → 基础贴图
            if (finalSprite == null)
            {
                var variant = GetVariantForPart(cfg.BodyPart);
                finalSprite = equip.GetSprite(facing, variant);
            }

            if (finalSprite == null || finalSprite.texture == null)
                return;

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
            if (_shaderMaterial == null || appearance == null)
                return;

            // 头部装备隐藏配置（Helmet / Hat / Mask 均可配置）
            bool hideHair = false;
            bool hideBeard = false;
            var headEquip = GetHeadSlotEquipment();
            if (headEquip != null)
            {
                hideHair = headEquip.hideHair;
                hideBeard = headEquip.hideBeard;
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
                    _shaderMaterial.SetVector(
                        FaceAccessoryRectProp,
                        SpriteUtils.GetUVRect(faceAccessorySprite)
                    );
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
            var headFacingDir = CharacterFrameData.GetFacingDirection(headFacing);
            bool eyesVisible = headFacingDir == FacingDirection.Front;
            _shaderMaterial.SetColor(LeftEyeColorProp, appearance.leftEyeColor);
            _shaderMaterial.SetColor(RightEyeColorProp, appearance.rightEyeColor);
            _shaderMaterial.SetFloat(EnableLeftEyeProp, eyesVisible ? 1f : 0f);
            _shaderMaterial.SetFloat(EnableRightEyeProp, eyesVisible ? 1f : 0f);
        }

        /// <summary>
        /// 颜色装备应用（配置驱动）
        /// </summary>
        void ApplyColorEquipment(EquipmentData equip, EquipTypeConfig cfg)
        {
            if (_shaderMaterial == null)
                return;

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
                    RenderWeaponSlot(_mainHandWeapon, AnchorType.MainHandWeapon, 0);
                    RenderWeaponSlot(_mainHandWeapon, AnchorType.OffHandWeapon, 1);
                }
                else if (_mainHandWeapon.weaponSlotType == WeaponSlotType.TwoHand)
                {
                    // 双手武器：根据配置选择锚点
                    var anchor = _mainHandWeapon.useOffHandAnchor ? AnchorType.OffHandWeapon : AnchorType.MainHandWeapon;
                    RenderWeaponSlot(_mainHandWeapon, anchor, 0);
                }
                else
                {
                    // 单手：使用主手锚点
                    RenderWeaponSlot(_mainHandWeapon, AnchorType.MainHandWeapon, 0);
                }
            }

            // 副手武器
            if (_offHandWeapon != null)
            {
                RenderWeaponSlot(_offHandWeapon, AnchorType.OffHandWeapon, 1);
            }
        }

        /// <summary>
        /// 更新角色帧 Rect（供 Shader 使用）
        /// </summary>
        void UpdateCharFrameRect()
        {
            if (_shaderMaterial == null)
                return;
            var charSprite = _charRenderer.sprite;
            if (charSprite == null || charSprite.texture == null)
                return;

            var charRect = charSprite.rect;
            float texW = charSprite.texture.width;
            float texH = charSprite.texture.height;
            var charFrameRect = new Vector4(
                charRect.xMin / texW,
                charRect.yMin / texH,
                charRect.xMax / texW,
                charRect.yMax / texH
            );
            _shaderMaterial.SetVector(CharFrameRectProp, charFrameRect);
        }

        #region 武器渲染辅助

        /// <summary>
        /// 按方向索引的武器配置表（SE=0, SW=1, NE=2, NW=3）
        /// HandOffsetX/Y 表示"虚拟左手"相对于贴图几何中心(16,16)的像素偏移：
        ///   像素画镜像限制：东向虚拟左手在(15,16)，西向在(16,16)
        ///   X: 东向 -1（像素15），西向 0（像素16）
        ///   Y: 统一 0（像素16）
        /// LeftSortDelta 用于主手锚点（MainHandWeapon）的排序偏移：
        ///   >0: 主手锚点在前（SW/NW），<0: 主手锚点在后（SE/NE）
        ///   副手锚点（OffHandWeapon）使用 -LeftSortDelta
        /// </summary>
        static readonly WeaponFacingConfig[] WeaponConfigByRow =
        {
            new WeaponFacingConfig(-0.5f, -0.5f, true,  -1), // SE: 东向，主手锚点在后
            new WeaponFacingConfig(-0.5f, -0.5f, true,  +1), // SW: 西向，主手锚点在前
            new WeaponFacingConfig(-0.5f, -0.5f, false, -1), // NE: 东向，主手锚点在后
            new WeaponFacingConfig(-0.5f, -0.5f, false, +1), // NW: 西向，主手锚点在前
        };

        readonly struct WeaponFacingConfig
        {
            public readonly float HandOffsetX;   // 虚拟左手偏移 X
            public readonly float HandOffsetY;   // 虚拟左手偏移 Y
            public readonly bool  IsFront;       // 是否前景（武器在身体前）
            public readonly int   LeftSortDelta; // 左武器排序偏移

            public WeaponFacingConfig(float hx, float hy, bool front, int sortDelta)
            {
                HandOffsetX = hx;
                HandOffsetY = hy;
                IsFront = front;
                LeftSortDelta = sortDelta;
            }
        }

        /// <summary>
        /// 像素坐标转帧内 UV（左下角为原点，Y 向上）
        /// </summary>
        static Vector2 PixelToFrameUV(float pixelX, float pixelY, int frameW, int frameH)
        {
            return new Vector2(pixelX / frameW, 1f - pixelY / frameH);
        }

        /// <summary>
        /// 获取当前方向的武器配置
        /// </summary>
        WeaponFacingConfig GetWeaponConfig(int rowIndex)
        {
            return (rowIndex >= 0 && rowIndex < WeaponConfigByRow.Length)
                ? WeaponConfigByRow[rowIndex]
                : WeaponConfigByRow[0];
        }

        /// <summary>
        /// 获取武器排序偏移
        /// 根据当前朝向下“物理左手/右手”的前后关系以及锚点类型(Main/Off)决定：
        ///   1. LeftSortDelta 表示当前朝向下“左手侧武器”的排序偏移
        ///   2. 通过朝向判断主手锚点是在左手还是右手
        ///   3. 再根据 AnchorType 判断当前锚点是不是左手侧锚点
        /// </summary>
        int GetWeaponSortOffset(AnchorType anchorType, int rowIndex)
        {
            var cfg = GetWeaponConfig(rowIndex);
            var facing = (CharacterFacing)rowIndex;

            bool isLeft = AnchorFacingConfig.IsAnchorOnLeftSide(anchorType, facing);

            return isLeft ? cfg.LeftSortDelta : -cfg.LeftSortDelta;
        }

        #endregion

        /// <summary>
        /// 渲染单个武器槽位
        /// </summary>
        void RenderWeaponSlot(EquipmentData equip, AnchorType anchorType, int shaderSlot)
        {
            if (equip == null) return;

            _weaponRenderers.TryGetValue(equip, out var sr);

            // 武器贴图方向：跟随身体躯干的 spriteFacing（转身时武器一起转向）
            var weaponFacing = GetSpriteFacingForPart(CharacterBodyPart.Torso);
            int weaponRowIndex = (int)weaponFacing;

            // 从当前帧数据中获取对应锚点（后面用于定位与旋转）
            var anchor = _cachedFrame.GetAnchor(anchorType);
            if (anchor == null) return;

            // 当前槽位相对角色的前后：根据朝向下“主/副手锚点”对应的左/右手关系来决定
            // 排序偏移 >0 表示在角色前，<0 表示在角色后
            int sortOffset = GetWeaponSortOffset(anchorType, weaponRowIndex);
            bool slotIsFront = sortOffset > 0;
            
            // 判断武器类型（后面处理盾牌特殊逻辑）
            bool isShield = (equip.type == EquipmentType.Shield);

            // 1. 优先使用序列帧
            var seqSprite = GetEquipSequenceSprite(equip, weaponFacing);
            if (seqSprite != null && sr != null)
            {
                sr.sprite = seqSprite;
                sr.enabled = true;
                sr.transform.localPosition = Vector3.zero;
                sr.transform.localRotation = Quaternion.identity;
                sr.flipX = false;
                sr.sortingLayerID = _charRenderer.sortingLayerID;
                sr.sortingOrder = _charRenderer.sortingOrder + sortOffset;
                return;
            }

            // 2. Shader 武器层
            if (_shaderMaterial == null || _cachedFrame == null) return;

            var weaponSprite = equip.GetSpriteByRow(weaponRowIndex);
            var charSprite = _charRenderer.sprite;
            if (weaponSprite == null || weaponSprite.texture == null || charSprite == null) return;

            // 更新子对象 Transform（用于挂特效）
            if (sr != null)
            {
                float ppu = charSprite.pixelsPerUnit;
                float cx = weaponSprite.rect.width * 0.5f;
                float cy = weaponSprite.rect.height * 0.5f;
                sr.transform.localPosition = new Vector3((anchor.position.x - cx) / ppu, -(anchor.position.y - cy) / ppu, 0);
                sr.transform.localRotation = Quaternion.Euler(0, 0, anchor.GetRotationAngle());
            }

            // 获取当前方向配置（与武器贴图方向一致）
            var cfg = GetWeaponConfig(weaponRowIndex);

            // 西向行（SW=1 / NW=3）是否需要 flipX：
            // 只有在"没有任何西向贴图（SW 也没配）"时才需要从 SE 翻转生成
            // 因为 NW 回退链是 NW → SW → SE，只要有 SW 就不会用到 SE
            bool isWestFacing = (weaponRowIndex == 1 || weaponRowIndex == 3);
            bool hasWestSprite = equip.spriteSW != null; // SW 是西向的基础图
            bool flipX = isWestFacing && !hasWestSprite;

            // 帧尺寸
            var charRect = charSprite.rect;
            int frameW = _currentAnimData != null ? _currentAnimData.frameSize.x : (int)charRect.width;
            int frameH = _currentAnimData != null ? _currentAnimData.frameSize.y : (int)charRect.height;
            frameW = Mathf.Max(frameW, 1);
            frameH = Mathf.Max(frameH, 1);

            // 角色帧中的锚点 UV（手的位置，像素中心）
            float anchorPixelX = anchor.position.x + 0.5f;
            float anchorPixelY = anchor.position.y + 0.5f;
            var anchorFrameUV = PixelToFrameUV(anchorPixelX, anchorPixelY, frameW, frameH);

            // 武器贴图中的"虚拟左手"局部 UV（相对于几何中心的像素偏移）
            float weaponW = weaponSprite.rect.width;
            float weaponH = weaponSprite.rect.height;
            float handLocalU = 0.5f + cfg.HandOffsetX / Mathf.Max(weaponW, 1f);
            float handLocalV = 0.5f + cfg.HandOffsetY / Mathf.Max(weaponH, 1f);
            var anchorAndHandUV = new Vector4(anchorFrameUV.x, anchorFrameUV.y, handLocalU, handLocalV);

            // 旋转：只有在 flipX 时角度才需要取反（从 SE 镜像生成西向时）
            float angleDeg = anchor.GetRotationAngle();
            if (flipX) angleDeg = -angleDeg;
            float angleRad = angleDeg * Mathf.Deg2Rad;
            var rotCosSin = new Vector4(Mathf.Cos(angleRad), Mathf.Sin(angleRad), 0f, 0f);

            // 根据武器类型决定手部遮挡：
            // 盾牌特殊处理：朝南时盾牌在前，朝北时手在前
            // 其他武器：手在前（可以看到手握着武器）
            bool handInFront;
            if (isShield)
            {
                // 盾牌：朝南时盾在前，朝北时手在前
                bool isSouth = (weaponFacing == CharacterFacing.SouthEast || weaponFacing == CharacterFacing.SouthWest);
                handInFront = !isSouth;
            }
            else
            {
                // 普通武器：手在前
                handInFront = true;
            }

            // 设置 Shader 参数
            SetWeaponShaderParams(
                shaderSlot,
                weaponSprite,
                anchorAndHandUV,
                rotCosSin,
                flipX,
                slotIsFront,
                handInFront
            );
        }

        /// <summary>
        /// 设置武器 Shader 参数
        /// </summary>
        void SetWeaponShaderParams(int slot, Sprite sprite, Vector4 anchorAndHandUV, Vector4 rotCosSin, bool flipX, bool isFront, bool handInFront)
        {
            int texProp    = slot == 0 ? Weapon0TexProp           : Weapon1TexProp;
            int rectProp   = slot == 0 ? Weapon0RectProp          : Weapon1RectProp;
            int anchorProp = slot == 0 ? Weapon0AnchorFrameUVProp : Weapon1AnchorFrameUVProp;
            int rotProp    = slot == 0 ? Weapon0RotCosSinProp     : Weapon1RotCosSinProp;
            int flipProp   = slot == 0 ? Weapon0FlipXProp         : Weapon1FlipXProp;
            int depthProp  = slot == 0 ? Weapon0DepthModeProp     : Weapon1DepthModeProp;
            int handInFrontProp = slot == 0 ? Weapon0HandInFrontProp : Weapon1HandInFrontProp;
            int enableProp = slot == 0 ? Weapon0EnabledProp       : Weapon1EnabledProp;

            _shaderMaterial.SetTexture(texProp, sprite.texture);
            _shaderMaterial.SetVector(rectProp, SpriteUtils.GetUVRect(sprite));
            _shaderMaterial.SetVector(anchorProp, anchorAndHandUV);
            _shaderMaterial.SetVector(rotProp, rotCosSin);
            _shaderMaterial.SetFloat(flipProp, flipX ? 1f : 0f);
            _shaderMaterial.SetFloat(depthProp, isFront ? 1f : 0f);
            _shaderMaterial.SetFloat(handInFrontProp, handInFront ? 1f : 0f);
            _shaderMaterial.SetFloat(enableProp, 1f);
        }

        void WriteAllEquipShaderParams()
        {
            if (_shaderMaterial == null)
                return;
            
            // 设置身体朝向参数（用于 Shader 判断渲染顺序）
            var facing = GetSpriteFacingForPart(CharacterBodyPart.Torso);
            bool isSouth = (facing == CharacterFacing.SouthEast || facing == CharacterFacing.SouthWest);
            _shaderMaterial.SetFloat("_BodyInFront", isSouth ? 1f : 0f);

            // 启用标记
            foreach (var cfg in EquipTypeRegistry.All)
            {
                bool enabled = _slots.ContainsKey(cfg.Type) && _slots[cfg.Type] != null;
                if (cfg.RenderMode == EquipRenderMode.Sprite || cfg.RenderMode == EquipRenderMode.Color)
                {
                    _shaderMaterial.SetFloat(cfg.EnableProp, enabled ? 1f : 0f);
                }
            }
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (Application.isPlaying)
                Refresh();
        }
#endif
    }
}
