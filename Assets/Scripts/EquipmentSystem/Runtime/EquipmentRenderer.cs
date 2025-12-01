using UnityEngine;
using EquipmentSystem.Data;
using System.Collections.Generic;

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
        
        [Header("装备")]
        public List<EquipmentData> equipments = new List<EquipmentData>();
        
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
        Dictionary<EquipmentData, SpriteRenderer> _equipRenderers = new Dictionary<EquipmentData, SpriteRenderer>();
        
        // 帧同步
        Sprite _lastSprite;
        int _frameIndex;
        int _rowIndex;
        FrameData _cachedFrame;
        AnimationData _currentAnimData;
        
        // Shader 换装材质
        Material _shaderMaterial;
        
        // Shader 属性 ID - 双层 UV Map
        static readonly int BodyUVMapProp = Shader.PropertyToID("_BodyUVMap");
        static readonly int HeadUVMapProp = Shader.PropertyToID("_HeadUVMap");
        static readonly int ClothTexProp = Shader.PropertyToID("_ClothTex");
        // 头部三层贴图
        static readonly int HairTexProp = Shader.PropertyToID("_HairTex");
        static readonly int BeardTexProp = Shader.PropertyToID("_BeardTex");
        static readonly int HelmetTexProp = Shader.PropertyToID("_HelmetTex");
        static readonly int UVMapFrameRectProp = Shader.PropertyToID("_UVMapFrameRect");
        // 装备贴图的 Sprite Rect (UV 偏移和缩放)
        static readonly int ClothRectProp = Shader.PropertyToID("_ClothRect");
        static readonly int HairRectProp = Shader.PropertyToID("_HairRect");
        static readonly int BeardRectProp = Shader.PropertyToID("_BeardRect");
        static readonly int HelmetRectProp = Shader.PropertyToID("_HelmetRect");
        // 颜色属性
        static readonly int LeftHandColorProp = Shader.PropertyToID("_LeftHandColor");
        static readonly int RightHandColorProp = Shader.PropertyToID("_RightHandColor");
        static readonly int LeftFootColorProp = Shader.PropertyToID("_LeftFootColor");
        static readonly int RightFootColorProp = Shader.PropertyToID("_RightFootColor");
        // 启用开关
        static readonly int EnableHairProp = Shader.PropertyToID("_EnableHair");
        static readonly int EnableBeardProp = Shader.PropertyToID("_EnableBeard");
        static readonly int EnableHelmetProp = Shader.PropertyToID("_EnableHelmet");
        static readonly int EnableClothProp = Shader.PropertyToID("_EnableCloth");
        static readonly int EnableGlovesProp = Shader.PropertyToID("_EnableGloves");
        static readonly int EnableShoesProp = Shader.PropertyToID("_EnableShoes");
        
        void Awake()
        {
            _charRenderer = GetComponent<SpriteRenderer>();
            _animator = GetComponentInChildren<Animator>();
            InitMaterial();
        }
        
        void Start()
        {
            foreach (var e in equipments)
                if (e != null && e.type == EquipmentType.Weapon)
                    CreateWeaponRenderer(e);
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
        
        // 动画关键字列表 - 用于匹配 Animator Bool 参数
        static readonly string[] AnimKeywords = { "Idle", "Walk", "Run", "Attack", "Hurt", "Die", "Jump", "Fall" };
        
        /// <summary>
        /// 从 Animator Bool 参数同步当前动画名称
        /// CTR_AnimateCreature 使用 SetBool("Idle", true) 等方式切换动画
        /// </summary>
        void SyncAnimationName()
        {
            if (_animator == null || frameData == null) return;
            
            // 从 Animator 的 Bool 参数找到当前激活的动画
            string activeParam = null;
            foreach (var keyword in AnimKeywords)
            {
                // 检查 Animator 是否有这个 Bool 参数且为 true
                try
                {
                    if (_animator.GetBool(keyword))
                    {
                        activeParam = keyword;
                        break;
                    }
                }
                catch { } // 参数不存在时会抛异常，忽略
            }
            
            _debugAnimatorState = activeParam ?? "(none)";
            
            // 如果没找到激活的参数，默认用 Idle 或第一个动画
            if (string.IsNullOrEmpty(activeParam))
            {
                activeParam = "Idle";
            }
            
            // 在 frameData 中找到包含该关键字的动画
            string newAnimName = FindAnimationByKeyword(activeParam);
            
            if (!string.IsNullOrEmpty(newAnimName) && newAnimName != _currentAnimName)
            {
                _currentAnimName = newAnimName;
                _currentAnimData = frameData.GetAnimation(_currentAnimName);
                _debugCurrentAnim = _currentAnimName;
                
                if (_currentAnimData != null)
                {
                    Debug.Log($"[EquipmentRenderer] 动画同步: {activeParam} -> {_currentAnimName}");
                    UpdateUVMapTexture();
                }
            }
        }
        
        /// <summary>
        /// 根据关键字在 frameData 中找到对应的动画
        /// </summary>
        string FindAnimationByKeyword(string keyword)
        {
            if (frameData == null) return null;
            
            string keywordLower = keyword.ToLowerInvariant();
            
            // 先尝试精确匹配
            foreach (var anim in frameData.animations)
            {
                if (string.Equals(anim.animationName, keyword, System.StringComparison.OrdinalIgnoreCase))
                    return anim.animationName;
            }
            
            // 再尝试包含匹配
            foreach (var anim in frameData.animations)
            {
                if (anim.animationName.ToLowerInvariant().Contains(keywordLower))
                    return anim.animationName;
            }
            
            // 默认返回第一个
            if (frameData.animations.Count > 0)
                return frameData.animations[0].animationName;
            
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
            
            Debug.Log($"[EquipmentRenderer] Shader 加载成功: {shader.name}");
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
            
            if (!equipments.Contains(equip))
            {
                equipments.Add(equip);
                if (equip.type == EquipmentType.Weapon)
                    CreateWeaponRenderer(equip);
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
        
        void CreateWeaponRenderer(EquipmentData equip)
        {
            if (_equipRenderers.ContainsKey(equip)) return;
            
            var go = new GameObject($"Weapon_{equip.name}");
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
            
            _cachedFrame = frameData.GetFrameData(_currentAnimName, _rowIndex, _frameIndex);
            
            // 获取当前动画配置
            if (_currentAnimData == null)
                _currentAnimData = frameData.GetAnimation(_currentAnimName);
            
            if (_currentAnimData == null)
            {
                // 静默失败，等待动画同步
                return;
            }
            
            bool hideLeftWeapon = _currentAnimData?.hideLeftWeapon ?? false;
            bool hideRightWeapon = _currentAnimData?.hideRightWeapon ?? false;
            
            // 设置 UV Map 贴图
            UpdateUVMapTexture();
            
            // 重置装备状态
            ResetEquipmentState();
            
            // 应用角色外观 (头发/胡子 - 来自捏人数据)
            ApplyAppearanceToShader();
            
            // 处理所有装备
            foreach (var equip in equipments)
            {
                if (equip == null) continue;
                
                Debug.Log($"[EquipmentRenderer] 处理装备: {equip.name}, 类型: {equip.type}");
                
                switch (equip.type)
                {
                    case EquipmentType.Weapon:
                        if (_equipRenderers.TryGetValue(equip, out var sr))
                            RenderWeapon(equip, sr, hideLeftWeapon, hideRightWeapon);
                        break;
                    case EquipmentType.Clothing:
                        ApplyClothingToShader(equip);
                        break;
                    case EquipmentType.Helmet:
                        ApplyHelmetToShader(equip);
                        break;
                    case EquipmentType.Gloves:
                        ApplyGlovesToShader(equip);
                        break;
                    case EquipmentType.Shoes:
                        ApplyShoesToShader(equip);
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
            {
                _shaderMaterial.SetTexture(BodyUVMapProp, _currentAnimData.bodyUVMap);
                Debug.Log($"[EquipmentRenderer] 身体层 UV Map: {_currentAnimData.bodyUVMap.name}");
            }
            else
            {
                Debug.LogWarning($"[EquipmentRenderer] 动画 '{_currentAnimName}' 没有身体层 UV Map");
            }
            
            // 设置头部层 UV Map
            if (_currentAnimData.headUVMap != null)
            {
                _shaderMaterial.SetTexture(HeadUVMapProp, _currentAnimData.headUVMap);
                Debug.Log($"[EquipmentRenderer] 头部层 UV Map: {_currentAnimData.headUVMap.name}");
            }
            
            // 简化：UVMap 与 SpriteRenderer 在运行时共用同一 spritesheet 坐标系，直采 i.uv
            // 如需 Atlas 适配，可再恢复 _UVMapFrameRect 分支。
        }
        
        // 计算当前帧在“UVMap使用的spritesheet”中的UV矩形（与UVMap贴图同坐标系）
        void UpdateUVMapFrameRect()
        {
            if (_shaderMaterial == null || _currentAnimData == null) return;

            // 以 headUV 或 bodyUV 的尺寸为基准，避免与 SpriteAtlas 混淆
            var uvMapTex = _currentAnimData.headUVMap != null ? _currentAnimData.headUVMap : _currentAnimData.bodyUVMap;
            var sheet = uvMapTex != null ? uvMapTex : _currentAnimData.spritesheet;
            if (sheet == null)
            {
                Debug.LogWarning("[EquipmentRenderer] 无法计算 _UVMapFrameRect：缺少 UVMap 或 spritesheet");
                return;
            }

            int texW = sheet.width;
            int texH = sheet.height;
            int frameW = _currentAnimData.frameSize.x;
            int frameH = _currentAnimData.frameSize.y;

            // 注意：纹理UV原点在左下角，行号从上到下需要转换
            float minU = (float)(_frameIndex * frameW) / texW;
            float maxU = (float)((_frameIndex + 1) * frameW) / texW;
            float minV = (float)(texH - (_rowIndex + 1) * frameH) / texH;
            float maxV = (float)(texH - _rowIndex * frameH) / texH;

            var rect = new Vector4(minU, minV, maxU, maxV);
            _shaderMaterial.SetVector(UVMapFrameRectProp, rect);
            Debug.Log($"[EquipmentRenderer] _UVMapFrameRect: ({minU:F3}, {minV:F3}, {maxU:F3}, {maxV:F3}), Frame={_frameIndex}, Row={_rowIndex}, UVMapTex={sheet.width}x{sheet.height}");
        }
        
        void ResetEquipmentState()
        {
            if (_shaderMaterial == null) return;
            
            // 重置所有装备层为禁用
            _shaderMaterial.SetFloat(EnableHairProp, 0);
            _shaderMaterial.SetFloat(EnableBeardProp, 0);
            _shaderMaterial.SetFloat(EnableHelmetProp, 0);
            _shaderMaterial.SetFloat(EnableClothProp, 0);
            _shaderMaterial.SetFloat(EnableGlovesProp, 0);
            _shaderMaterial.SetFloat(EnableShoesProp, 0);
            
            // 同步清空对应纹理与 Rect，避免 UI 切换为空后仍采样到上一次纹理（尤其在 Debug 模式下）
            ClearTextureAndRect(ClothTexProp, ClothRectProp);
            ClearTextureAndRect(HairTexProp, HairRectProp);
            ClearTextureAndRect(BeardTexProp, BeardRectProp);
            ClearTextureAndRect(HelmetTexProp, HelmetRectProp);
        }

        void ClearTextureAndRect(int texProp, int rectProp)
        {
            _shaderMaterial.SetTexture(texProp, null);
            _shaderMaterial.SetVector(rectProp, Vector4.zero);
        }
        
        /// <summary>
        /// 设置服装 - 根据方向选择贴图
        /// 支持部位级别的贴图方向覆盖（用于转头等场景）
        /// 
        /// 注意: 必须同时设置 texture 和 rect，因为 sprite.texture 可能是整张 spritesheet
        /// </summary>
        void ApplyClothingToShader(EquipmentData equip)
        {
            if (_shaderMaterial == null) return;
            
            // 获取实际使用的贴图方向（支持部位级别覆盖）
            var facing = GetSpriteFacingForPart(CharacterBodyPart.Torso);
            var clothingSprite = equip.GetSprite(facing);
            if (clothingSprite == null || clothingSprite.texture == null)
            {
                Debug.LogWarning($"[EquipmentRenderer] 服装 {equip.name} 没有方向 {facing} 的贴图");
                return;
            }
            
            // 重要: 同时传递 texture 和 sprite rect，Shader 中用 TransformUV() 转换 UV
            _shaderMaterial.SetTexture(ClothTexProp, clothingSprite.texture);
            var clothRect = SpriteUtils.GetUVRect(clothingSprite);
            _shaderMaterial.SetVector(ClothRectProp, clothRect);
            _shaderMaterial.SetFloat(EnableClothProp, 1);
            Debug.Log($"[EquipmentRenderer] 服装 Rect: ({clothRect.x:F3}, {clothRect.y:F3}, {clothRect.z:F3}, {clothRect.w:F3}), " +
                      $"Sprite尺寸: {clothingSprite.rect.width}x{clothingSprite.rect.height}, " +
                      $"Texture尺寸: {clothingSprite.texture.width}x{clothingSprite.texture.height}, 方向: {facing}");
        }
        
        /// <summary>
        /// 设置头盔 - 根据方向选择贴图
        /// 支持部位级别的贴图方向覆盖（用于转头等场景）
        /// 
        /// 注意: 必须同时设置 texture 和 rect，因为 sprite.texture 可能是整张 spritesheet
        /// </summary>
        void ApplyHelmetToShader(EquipmentData equip)
        {
            if (_shaderMaterial == null) return;
            
            // 获取实际使用的贴图方向（支持部位级别覆盖）
            var facing = GetSpriteFacingForPart(CharacterBodyPart.Head);
            var helmetSprite = equip.GetSprite(facing);
            if (helmetSprite == null || helmetSprite.texture == null)
            {
                Debug.LogWarning($"[EquipmentRenderer] 头盔 {equip.name} 没有方向 {facing} 的贴图");
                return;
            }
            
            // 重要: 同时传递 texture 和 sprite rect，Shader 中用 TransformUV() 转换 UV
            var helmetRect = SpriteUtils.GetUVRect(helmetSprite);
            _shaderMaterial.SetTexture(HelmetTexProp, helmetSprite.texture);
            _shaderMaterial.SetVector(HelmetRectProp, helmetRect);
            _shaderMaterial.SetFloat(EnableHelmetProp, 1);
            
            // 调试: 输出头盔的 sprite rect 信息
            Debug.Log($"[EquipmentRenderer] 头盔 Rect: ({helmetRect.x:F3}, {helmetRect.y:F3}, {helmetRect.z:F3}, {helmetRect.w:F3}), " +
                      $"Sprite尺寸: {helmetSprite.rect.width}x{helmetSprite.rect.height}, " +
                      $"Texture尺寸: {helmetSprite.texture.width}x{helmetSprite.texture.height}, 方向: {facing}");
        }
        
        /// <summary>
        /// 获取指定部位实际使用的贴图方向
        /// 如果部位配置了覆盖，返回覆盖的方向；否则返回当前动画行对应的方向
        /// </summary>
        CharacterFacing GetSpriteFacingForPart(CharacterBodyPart part)
        {
            if (_cachedFrame != null)
            {
                var region = _cachedFrame.GetRegion(part);
                if (region != null)
                {
                    return region.GetSpriteFacing(_rowIndex);
                }
            }
            return (CharacterFacing)_rowIndex;
        }
        
        /// <summary>
        /// 设置角色外观 (头发/胡子) - 来自 CharacterAppearance
        /// 
        /// 注意: 必须同时设置 texture 和 rect，因为 sprite.texture 可能是整张 spritesheet
        /// </summary>
        void ApplyAppearanceToShader()
        {
            if (_shaderMaterial == null || appearance == null) return;
            
            // 设置头发 - 同时传递 texture 和 sprite rect
            if (appearance.HasHair)
            {
                var hairSprite = appearance.GetHairByRow(_rowIndex);
                if (hairSprite != null && hairSprite.texture != null)
                {
                    _shaderMaterial.SetTexture(HairTexProp, hairSprite.texture);
                    _shaderMaterial.SetVector(HairRectProp, SpriteUtils.GetUVRect(hairSprite));
                    _shaderMaterial.SetFloat(EnableHairProp, 1);
                }
            }
            
            // 设置胡子 - 同时传递 texture 和 sprite rect
            if (appearance.HasBeard)
            {
                var beardSprite = appearance.GetBeardByRow(_rowIndex);
                if (beardSprite != null && beardSprite.texture != null)
                {
                    _shaderMaterial.SetTexture(BeardTexProp, beardSprite.texture);
                    _shaderMaterial.SetVector(BeardRectProp, SpriteUtils.GetUVRect(beardSprite));
                    _shaderMaterial.SetFloat(EnableBeardProp, 1);
                }
            }
        }
        
        /// <summary>
        /// 设置手套 - 只需设置颜色参数
        /// </summary>
        void ApplyGlovesToShader(EquipmentData equip)
        {
            if (_shaderMaterial == null) return;
            
            _shaderMaterial.SetColor(LeftHandColorProp, equip.leftColor);
            _shaderMaterial.SetColor(RightHandColorProp, equip.rightColor);
            _shaderMaterial.SetFloat(EnableGlovesProp, 1);
            Debug.Log($"[EquipmentRenderer] 手套已启用: {equip.name}, 左={equip.leftColor}, 右={equip.rightColor}");
        }
        
        /// <summary>
        /// 设置鞋子 - 只需设置颜色参数
        /// </summary>
        void ApplyShoesToShader(EquipmentData equip)
        {
            if (_shaderMaterial == null) return;
            
            _shaderMaterial.SetColor(LeftFootColorProp, equip.leftColor);
            _shaderMaterial.SetColor(RightFootColorProp, equip.rightColor);
            _shaderMaterial.SetFloat(EnableShoesProp, 1);
        }
        
        /// <summary>
        /// 渲染武器 - 用锚点定位，根据方向选择贴图
        /// 
        /// 注意: 武器使用 SpriteRenderer 渲染，直接赋值 sprite 即可。
        /// SpriteRenderer 会自动处理 sprite.rect，不需要手动计算 UV。
        /// 这与 Shader 换装不同，Shader 换装需要手动传递 rect 给 Shader。
        /// </summary>
        void RenderWeapon(EquipmentData equip, SpriteRenderer sr, bool hideLeftWeapon, bool hideRightWeapon)
        {
            // 武器用 SpriteRenderer 渲染，直接赋值 sprite（自动处理 rect）
            sr.sprite = equip.GetSpriteByRow(_rowIndex);
            
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
            int sortOffset = GetWeaponSortOffset(equip.anchorType, _rowIndex);
            sr.sortingOrder = _charRenderer.sortingOrder + sortOffset;
        }
        
        /// <summary>
        /// 根据朝向和左右手计算武器排序偏移
        /// SE(0): 左手在后(-1), 右手在前(+1)
        /// SW(1): 左手在前(+1), 右手在后(-1)
        /// NE(2): 左手在前(+1), 右手在后(-1)
        /// NW(3): 左手在后(-1), 右手在前(+1)
        /// </summary>
        int GetWeaponSortOffset(AnchorType anchorType, int rowIndex)
        {
            bool isLeftWeapon = anchorType == AnchorType.LeftWeapon;
            
            switch (rowIndex)
            {
                case 0: // SE - 东南
                    return isLeftWeapon ? -1 : 1;
                case 1: // SW - 西南
                    return isLeftWeapon ? 1 : -1;
                case 2: // NE - 东北
                    return isLeftWeapon ? 1 : -1;
                case 3: // NW - 西北
                    return isLeftWeapon ? -1 : 1;
                default:
                    return 1;
            }
        }
        
#if UNITY_EDITOR
        void OnValidate()
        {
            if (Application.isPlaying) Refresh();
        }
#endif
    }
}
