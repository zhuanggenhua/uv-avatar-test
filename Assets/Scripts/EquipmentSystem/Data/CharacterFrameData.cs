using System;
using System.Collections.Generic;
using UnityEngine;

namespace EquipmentSystem.Data
{
    #region 枚举
    
    public enum CharacterFacing
    {
        SouthEast = 0,
        SouthWest = 1,
        NorthEast = 2,
        NorthWest = 3
    }

    public enum FacingDirection { Front, Back }
    
    /// <summary>
    /// UV 空间方向配置
    /// UV 坐标系：左下角 (0,0)，右上角 (1,1)
    /// 用对角线方向描述纹理的朝向
    /// </summary>
    public enum UVOrientation
    {
        /// <summary>右上（默认）- 纹理从左下指向右上</summary>
        UpRight = 0,
        /// <summary>左下 - 纹理从右上指向左下（旋转180°）</summary>
        DownLeft = 1,
        /// <summary>左上 - 纹理从右下指向左上（逆时针旋转90°）</summary>
        UpLeft = 2,
        /// <summary>右下 - 纹理从左上指向右下（顺时针旋转90°）</summary>
        DownRight = 3
    }
    
    #endregion

    #region 锚点 - 武器挂点
    
    /// <summary>
    /// 锚点类型 - 只用于武器
    /// </summary>
    public enum AnchorType
    {
        LeftWeapon,     // 左手武器
        RightWeapon     // 右手武器
    }
    
    [Serializable]
    public class AnchorPoint
    {
        public AnchorType type;
        public Vector2Int position;
        public UVOrientation orientation;
        public bool flipX;  // 水平翻转（角色转头时用）
        
        public float GetRotationAngle()
        {
            switch (orientation)
            {
                case UVOrientation.DownLeft: return 180f;
                case UVOrientation.UpLeft: return 90f;
                case UVOrientation.DownRight: return -90f;
                default: return 0f;  // UpRight
            }
        }
    }
    
    #endregion

    #region 部位涂色 - 服装/手套/鞋子用
    
    /// <summary>
    /// 身体部位类型
    /// </summary>
    public enum CharacterBodyPart
    {
        Head,       // 头部 4x3
        Torso,      // 身体 2x3 (衣服映射)
        LeftHand,   // 左手 1px
        RightHand,  // 右手 1px
        LeftFoot,   // 左脚 1px
        RightFoot   // 右脚 1px
    }
    
    /// <summary>
    /// 部位像素标记（单个像素）
    /// </summary>
    [Serializable]
    public class BodyPartPixel
    {
        public CharacterBodyPart part;
        public Vector2Int position;
        public Color32 color;  // 原始颜色（用于匹配）
        
        /// <summary>
        /// UV 坐标：直接存储要采样装备贴图的 UV 位置
        /// 从 UV 画板拷贝而来，或由扩展算法从边界像素复制
        /// </summary>
        public Vector2 uv = new Vector2(-1, -1);  // -1 表示未设置
        
        /// <summary>
        /// UV 是否已设置
        /// </summary>
        public bool HasUV => uv.x >= 0 && uv.y >= 0;
    }
    
    /// <summary>
    /// 部位区域
    /// </summary>
    [Serializable]
    public class BodyPartRegion
    {
        public CharacterBodyPart part;
        public UVOrientation orientation = UVOrientation.UpRight;
        
        /// <summary>
        /// 贴图方向（用于转头等场景，指定该部位使用哪个方向的装备贴图）
        /// </summary>
        public CharacterFacing spriteFacing = CharacterFacing.SouthEast;
        
        public List<BodyPartPixel> pixels = new List<BodyPartPixel>();
        
        /// <summary>
        /// 获取实际使用的装备贴图方向
        /// </summary>
        public CharacterFacing GetSpriteFacing(int rowIndex)
        {
            return spriteFacing;
        }
        
        /// <summary>
        /// 获取区域的包围盒
        /// </summary>
        public RectInt GetBounds()
        {
            if (pixels.Count == 0) return new RectInt(0, 0, 0, 0);
            
            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;
            foreach (var px in pixels)
            {
                minX = Mathf.Min(minX, px.position.x);
                maxX = Mathf.Max(maxX, px.position.x);
                minY = Mathf.Min(minY, px.position.y);
                maxY = Mathf.Max(maxY, px.position.y);
            }
            return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }
    }

    #endregion

    #region 帧数据
    
    [Serializable]
    public class FrameData
    {
        public int frameIndex;
        public int rowIndex;
        
        [Header("锚点 - 挂件")]
        public List<AnchorPoint> anchors = new List<AnchorPoint>();
        
        [Header("部位区域 - 服装/手套/鞋")]
        public List<BodyPartRegion> bodyRegions = new List<BodyPartRegion>();
        
        public AnchorPoint GetAnchor(AnchorType type) => anchors.Find(a => a.type == type);
        
        public void SetAnchor(AnchorType type, Vector2Int pos, UVOrientation orientation)
        {
            var a = GetAnchor(type);
            if (a == null)
            {
                a = new AnchorPoint { type = type };
                anchors.Add(a);
            }
            a.position = pos;
            a.orientation = orientation;
        }
        
        /// <summary>
        /// 获取指定部位区域
        /// </summary>
        public BodyPartRegion GetRegion(CharacterBodyPart part)
        {
            return bodyRegions.Find(r => r.part == part);
        }
        
        /// <summary>
        /// 获取或创建部位区域
        /// </summary>
        public BodyPartRegion GetOrCreateRegion(CharacterBodyPart part)
        {
            var r = GetRegion(part);
            if (r == null)
            {
                r = new BodyPartRegion { part = part };
                bodyRegions.Add(r);
            }
            return r;
        }
        
        /// <summary>
        /// 检查部位是否可见（有涂色像素）
        /// </summary>
        public bool IsPartVisible(CharacterBodyPart part)
        {
            var r = GetRegion(part);
            return r != null && r.pixels.Count > 0;
        }
        
        /// <summary>
        /// 获取单像素部位的位置（手/脚），没有则返回null
        /// </summary>
        public BodyPartPixel GetSinglePixelPart(CharacterBodyPart part)
        {
            var r = GetRegion(part);
            return r?.pixels.Count > 0 ? r.pixels[0] : null;
        }
    }

    [Serializable]
    public class AnimationData
    {
        public string animationName;  // 动画名称（从Animator或SpriteLibrary获取）
        
        [Header("Spritesheet 配置")]
        public Texture2D spritesheet;
        public Vector2Int frameSize = new Vector2Int(32, 32);
        public int framesPerRow = 8;
        public int rowCount = 4;
        
        [Header("武器显示配置")]
        public bool hideLeftWeapon;   // 隐藏左手武器
        public bool hideRightWeapon;  // 隐藏右手武器
        
        [Header("GPU 换装 - 双层 UV Map")]
        [Tooltip("身体层 UV Map (衣服、手套、鞋子)")]
        public Texture2D bodyUVMap;
        [Tooltip("头部层 UV Map (头盔、胡子、头发)")]
        public Texture2D headUVMap;
        
        public List<FrameData> frames = new List<FrameData>();
        
        public FrameData GetFrame(int frameIndex, int rowIndex)
        {
            return frames.Find(f => f.frameIndex == frameIndex && f.rowIndex == rowIndex);
        }
        
        public FrameData GetOrCreateFrame(int frameIndex, int rowIndex)
        {
            var f = GetFrame(frameIndex, rowIndex);
            if (f == null)
            {
                f = new FrameData { frameIndex = frameIndex, rowIndex = rowIndex };
                frames.Add(f);
            }
            return f;
        }
    }

    #endregion

    #region 检测配置
    
    /// <summary>
    /// 自动检测配置
    /// 手脚颜色是固定的阴影色，不受朝向影响
    /// 即使角色面向SW/NW时左手在画面右边，其颜色仍然是左手颜色
    /// </summary>
    [Serializable]
    public class DetectConfig
    {
        [Header("手脚阴影色 (用于自动检测，与朝向无关)")]
        [Tooltip("左手的阴影色，不论朝向如何，左手始终使用此颜色")]
        public Color32 leftHandColor = new Color32(221,183,143, 255);
        [Tooltip("右手的阴影色，不论朝向如何，右手始终使用此颜色")]
        public Color32 rightHandColor = new Color32(250,203,166, 255);
        [Tooltip("左脚的阴影色，不论朝向如何，左脚始终使用此颜色")]
        public Color32 leftFootColor = new Color32(205,172,133, 255);
        [Tooltip("右脚的阴影色，不论朝向如何，右脚始终使用此颜色")]
        public Color32 rightFootColor = new Color32(238,195,154, 255);
        
        [Header("检测参数")]
        public int outlineThreshold = 30;
        
        /// <summary>
        /// 是否为描边/黑色像素
        /// </summary>
        public bool IsOutline(Color32 c) => (c.r + c.g + c.b) < outlineThreshold && c.a > 0;
        
        /// <summary>
        /// 是否为有色非黑色像素
        /// </summary>
        public bool IsColoredPixel(Color32 c) => c.a > 0 && !IsOutline(c);
        
        /// <summary>
        /// 颜色是否完全匹配
        /// </summary>
        public bool ColorMatch(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a > 0 && b.a > 0;
    }

    #endregion

    [CreateAssetMenu(fileName = "CharacterFrameData", menuName = "Equipment System/Character Frame Data")]
    public class CharacterFrameData : ScriptableObject
    {
        [Header("头部区域扩展配置")]
        [Tooltip("头部向上扩展的像素数")]
        public int headExpandUp = 5;
        [Tooltip("头部向左右扩展的像素数")]
        public int headExpandSide = 5;
        [Tooltip("头部向下扩展的像素数")]
        public int headExpandDown = 3;
        
        [Header("身体区域扩展配置")]
        [Tooltip("身体向上扩展的像素数")]
        public int bodyExpandUp = 3;
        [Tooltip("身体向左右扩展的像素数")]
        public int bodyExpandSide = 3;
        [Tooltip("身体向下扩展的像素数")]
        public int bodyExpandDown = 2;
        
        [Header("UV 参考帧配置")]
        [Tooltip("是否已设置参考帧（头部装备贴图的绘制基准）")]
        public bool hasReferenceFrame = false;
        [Tooltip("参考帧的头部区域中心（帧内坐标），所有帧的 UV 都基于此位置计算")]
        public Vector2 referenceHeadCenter;
        
        [Header("检测配置")]
        public DetectConfig detectConfig = new DetectConfig();
        
        [Header("动画列表")]
        public List<AnimationData> animations = new List<AnimationData>();
        
        /// <summary>
        /// 获取动画数据
        /// </summary>
        public AnimationData GetAnimation(string animName)
        {
            return animations.Find(x => 
                string.Equals(x.animationName, animName, System.StringComparison.OrdinalIgnoreCase));
        }
        
        /// <summary>
        /// 获取帧数据
        /// </summary>
        public FrameData GetFrameData(string animName, int rowIndex, int frame)
        {
            var a = GetAnimation(animName);
            if (a == null) return null;
            return a.GetFrame(frame, rowIndex);
        }
        
        /// <summary>
        /// 获取或创建动画数据
        /// </summary>
        public AnimationData GetOrCreateAnimation(string animName)
        {
            var a = GetAnimation(animName);
            if (a == null)
            {
                a = new AnimationData { animationName = animName };
                animations.Add(a);
            }
            return a;
        }
        
        /// <summary>
        /// 获取所有动画名称
        /// </summary>
        public List<string> GetAnimationNames()
        {
            var names = new List<string>();
            foreach (var a in animations)
                if (!string.IsNullOrEmpty(a.animationName))
                    names.Add(a.animationName);
            return names;
        }
        
        public static FacingDirection GetFacingDirection(CharacterFacing f)
            => (f == CharacterFacing.SouthEast || f == CharacterFacing.SouthWest) ? FacingDirection.Front : FacingDirection.Back;
    }
}
