using UnityEngine;
using EquipmentSystem.Data;
using System.Collections.Generic;
using BodyPart = EquipmentSystem.Data.BodyPart;

namespace EquipmentSystem.Runtime
{
    /// <summary>
    /// 装备渲染器
    /// - 挂件(Accessory): 用锚点定位
    /// - 服装(Clothing): 2x3像素映射到身体标记区域
    /// - 手套(Gloves): 替换手部像素颜色
    /// - 鞋子(Shoes): 替换脚部像素颜色
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class EquipmentRenderer : MonoBehaviour
    {
        [Header("数据")]
        public CharacterFrameData frameData;
        public string currentAnimation = "Idle";
        
        [Header("装备")]
        public List<EquipmentData> equipments = new List<EquipmentData>();
        
        SpriteRenderer _charRenderer;
        Dictionary<EquipmentData, SpriteRenderer> _equipRenderers = new Dictionary<EquipmentData, SpriteRenderer>();
        
        // 帧同步
        Sprite _lastSprite;
        int _frameIndex;
        int _rowIndex;
        FrameData _cachedFrame;
        AnimationData _currentAnimData;
        
        // 装备叠加纹理（CPU生成）
        Texture2D _equipOverlayTex;
        Material _overlayMaterial;
        static readonly int EquipTexProp = Shader.PropertyToID("_EquipTex");
        
        void Awake()
        {
            _charRenderer = GetComponent<SpriteRenderer>();
            InitOverlay();
        }
        
        void Start()
        {
            foreach (var e in equipments)
                if (e != null && e.type == EquipmentType.Accessory)
                    CreateRenderer(e);
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
            if (_equipOverlayTex != null)
                Destroy(_equipOverlayTex);
            if (_overlayMaterial != null)
                Destroy(_overlayMaterial);
        }
        
        void InitOverlay()
        {
            // 加载Shader
            var shader = Shader.Find("EquipmentSystem/EquipmentOverlay");
            if (shader != null)
            {
                _overlayMaterial = new Material(shader);
                _charRenderer.material = _overlayMaterial;
            }
            
            // 初始化叠加纹理
            EnsureOverlayTexture();
        }
        
        void EnsureOverlayTexture()
        {
            // 获取当前动画的帧尺寸
            int w = 32, h = 32;
            if (_currentAnimData != null)
            {
                w = _currentAnimData.frameSize.x;
                h = _currentAnimData.frameSize.y;
            }
            
            // 检查是否需要重建纹理
            if (_equipOverlayTex != null && _equipOverlayTex.width == w && _equipOverlayTex.height == h)
                return;
            
            if (_equipOverlayTex != null)
                Destroy(_equipOverlayTex);
            
            _equipOverlayTex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
        }
        
        /// <summary>
        /// 从 Sprite 的 rect 位置同步帧索引和行索引
        /// 注: 运行时从 Sprite 的 rect 位置计算，使用当前动画的帧尺寸配置
        /// </summary>
        void SyncFromSprite()
        {
            if (_lastSprite == null || frameData == null) return;
            
            // 获取当前动画配置
            _currentAnimData = frameData.GetAnimation(currentAnimation);
            if (_currentAnimData == null) return;
            
            // 确保叠加纹理尺寸正确
            EnsureOverlayTexture();
            
            // 从 Sprite 的 rect 位置计算帧索引和行索引
            var rect = _lastSprite.rect;
            int frameW = _currentAnimData.frameSize.x;
            int frameH = _currentAnimData.frameSize.y;
            
            if (frameW > 0 && frameH > 0)
            {
                // 根据 Sprite 在贴图中的位置计算
                _frameIndex = Mathf.FloorToInt(rect.x / frameW);
                // Unity Sprite 的 Y 是从底部计算的，需要转换
                int textureHeight = _lastSprite.texture.height;
                _rowIndex = Mathf.FloorToInt((textureHeight - rect.y - rect.height) / frameH);
                Refresh();
            }
        }
        
        public void Equip(EquipmentData equip)
        {
            if (!equipments.Contains(equip))
            {
                equipments.Add(equip);
                if (equip.type == EquipmentType.Accessory)
                    CreateRenderer(equip);
            }
            Refresh();
        }
        
        public void Unequip(EquipmentData equip)
        {
            if (_equipRenderers.TryGetValue(equip, out var sr))
            {
                Destroy(sr.gameObject);
                _equipRenderers.Remove(equip);
            }
            equipments.Remove(equip);
        }
        
        void CreateRenderer(EquipmentData equip)
        {
            if (_equipRenderers.ContainsKey(equip)) return;
            
            var go = new GameObject($"Equip_{equip.equipmentId}");
            go.transform.SetParent(transform);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = Vector3.one;
            
            var sr = go.AddComponent<SpriteRenderer>();
            _equipRenderers[equip] = sr;
        }
        
        public void Refresh()
        {
            if (frameData == null) return;
            
            _cachedFrame = frameData.GetFrameData(currentAnimation, _rowIndex, _frameIndex);
            
            // 获取当前动画配置
            if (_currentAnimData == null)
                _currentAnimData = frameData.GetAnimation(currentAnimation);
            
            bool hideLeftWeapon = _currentAnimData?.hideLeftWeapon ?? false;
            bool hideRightWeapon = _currentAnimData?.hideRightWeapon ?? false;
            
            // 清除叠加纹理
            ClearOverlayTexture();
            
            foreach (var equip in equipments)
            {
                switch (equip.type)
                {
                    case EquipmentType.Accessory:
                        if (_equipRenderers.TryGetValue(equip, out var sr))
                            RenderAccessory(equip, sr, hideLeftWeapon, hideRightWeapon);
                        break;
                    case EquipmentType.Clothing:
                        RenderClothing(equip);
                        break;
                    case EquipmentType.Gloves:
                        RenderGloves(equip);
                        break;
                    case EquipmentType.Shoes:
                        RenderShoes(equip);
                        break;
                }
            }
            
            // 应用叠加纹理
            ApplyOverlayTexture();
        }
        
        void ClearOverlayTexture()
        {
            if (_equipOverlayTex == null) return;
            var pixels = _equipOverlayTex.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(0, 0, 0, 0);
            _equipOverlayTex.SetPixels32(pixels);
        }
        
        void ApplyOverlayTexture()
        {
            if (_equipOverlayTex == null || _overlayMaterial == null) return;
            _equipOverlayTex.Apply();
            _overlayMaterial.SetTexture(EquipTexProp, _equipOverlayTex);
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
            
            sr.enabled = true;
            
            // 计算位置 - 直接用像素坐标，不依赖 pivot
            // 角色锚点和装备锚点都是编辑器坐标系（左上角原点，Y向下）
            // 最终位置 = 角色锚点 - 装备锚点
            
            float ppu = _charRenderer.sprite != null ? _charRenderer.sprite.pixelsPerUnit : 16f;
            int charH = _currentAnimData?.frameSize.y ?? 32;
            float equipW = sr.sprite != null ? sr.sprite.rect.width : 0;
            float equipH = sr.sprite != null ? sr.sprite.rect.height : 0;
            
            // 装备自身锚点
            float equipAnchorX = equip.selfAnchor.x;
            float equipAnchorY = equip.selfAnchor.y;
            
            // 翻转时镜像装备锚点 X
            if (anchor.flipX && equipW > 0)
            {
                equipAnchorX = equipW - 1 - equip.selfAnchor.x;
            }
            
            // 像素偏移（编辑器坐标系）
            float deltaX = anchor.position.x - equipAnchorX;
            float deltaY = anchor.position.y - equipAnchorY;
            
            // 转换到 Unity 坐标（Y 取反）
            float posX = deltaX / ppu;
            float posY = -deltaY / ppu;
            
            sr.transform.localPosition = new Vector3(posX, posY, 0);
            
            // 翻转
            sr.flipX = anchor.flipX;
            
            // 旋转
            sr.transform.localRotation = Quaternion.Euler(0, 0, anchor.GetRotationAngle());
            
            // 死区检查 - 如果锚点在死区内则隐藏
            if (_cachedFrame.IsInDeadZone(anchor.position))
            {
                sr.enabled = false;
                return;
            }
            
            // 排序
            sr.sortingLayerID = _charRenderer.sortingLayerID;
            sr.sortingOrder = _charRenderer.sortingOrder + equip.sortingOffset;
        }
        
        /// <summary>
        /// 渲染服装 - 2x3像素映射到身体标记区域
        /// </summary>
        void RenderClothing(EquipmentData equip)
        {
            if (_cachedFrame == null || _equipOverlayTex == null) return;
            
            // 获取身体区域
            var torsoRegion = _cachedFrame.GetRegion(BodyPart.Torso);
            if (torsoRegion == null || torsoRegion.pixels.Count == 0) return;
            
            // 获取服装贴图 (2x3)
            var facingDir = CharacterFrameData.GetFacingDirection((CharacterFacing)_rowIndex);
            var clothingSprite = equip.GetSprite(facingDir);
            if (clothingSprite == null || clothingSprite.texture == null) return;
            
            var clothTex = clothingSprite.texture;
            if (!clothTex.isReadable) return;
            
            // 计算身体区域的边界
            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;
            foreach (var px in torsoRegion.pixels)
            {
                minX = Mathf.Min(minX, px.position.x);
                maxX = Mathf.Max(maxX, px.position.x);
                minY = Mathf.Min(minY, px.position.y);
                maxY = Mathf.Max(maxY, px.position.y);
            }
            
            int regionW = maxX - minX + 1;
            int regionH = maxY - minY + 1;
            
            // 根据方向映射服装像素
            foreach (var px in torsoRegion.pixels)
            {
                // 死区检查
                if (_cachedFrame.IsInDeadZone(px.position)) continue;
                
                // 计算在区域内的相对位置 (0~1)
                float relX = regionW > 1 ? (float)(px.position.x - minX) / (regionW - 1) : 0.5f;
                float relY = regionH > 1 ? (float)(px.position.y - minY) / (regionH - 1) : 0.5f;
                
                // 根据方向旋转映射
                float srcX, srcY;
                switch (torsoRegion.direction)
                {
                    case PartDirection.Left: // 躺下向左: 旋转90°
                        srcX = relY;
                        srcY = 1f - relX;
                        break;
                    case PartDirection.Right: // 躺下向右: 旋转-90°
                        srcX = 1f - relY;
                        srcY = relX;
                        break;
                    case PartDirection.Up: // 倒立: 旋转180°
                        srcX = 1f - relX;
                        srcY = 1f - relY;
                        break;
                    default: // Down: 正常
                        srcX = relX;
                        srcY = relY;
                        break;
                }
                
                // 采样服装贴图
                int clothX = Mathf.Clamp(Mathf.RoundToInt(srcX * (clothTex.width - 1)), 0, clothTex.width - 1);
                int clothY = Mathf.Clamp(Mathf.RoundToInt(srcY * (clothTex.height - 1)), 0, clothTex.height - 1);
                
                Color clothColor = clothTex.GetPixel(clothX, clothTex.height - 1 - clothY);
                if (clothColor.a < 0.01f) continue;
                
                // 写入叠加纹理
                int destY = _equipOverlayTex.height - 1 - px.position.y;
                _equipOverlayTex.SetPixel(px.position.x, destY, clothColor);
            }
        }
        
        /// <summary>
        /// 渲染手套 - 替换手部像素颜色
        /// </summary>
        void RenderGloves(EquipmentData equip)
        {
            if (_cachedFrame == null || _equipOverlayTex == null) return;
            
            // 左手
            RenderSinglePixelPart(BodyPart.LeftHand, equip.leftColor);
            // 右手
            RenderSinglePixelPart(BodyPart.RightHand, equip.rightColor);
        }
        
        /// <summary>
        /// 渲染鞋子 - 替换脚部像素颜色
        /// </summary>
        void RenderShoes(EquipmentData equip)
        {
            if (_cachedFrame == null || _equipOverlayTex == null) return;
            
            // 左脚
            RenderSinglePixelPart(BodyPart.LeftFoot, equip.leftColor);
            // 右脚
            RenderSinglePixelPart(BodyPart.RightFoot, equip.rightColor);
        }
        
        void RenderSinglePixelPart(BodyPart part, Color color)
        {
            if (color.a < 0.01f) return;
            
            var region = _cachedFrame.GetRegion(part);
            if (region == null || region.pixels.Count == 0) return;
            
            foreach (var px in region.pixels)
            {
                if (_cachedFrame.IsInDeadZone(px.position)) continue;
                
                int destY = _equipOverlayTex.height - 1 - px.position.y;
                _equipOverlayTex.SetPixel(px.position.x, destY, color);
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
