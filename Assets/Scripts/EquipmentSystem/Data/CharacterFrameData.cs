using System;
using System.Collections.Generic;
using UnityEngine;

namespace EquipmentSystem
{
    #region 枚举

    public enum CharacterFacing
    {
        SouthEast = 0,
        SouthWest = 1,
        NorthEast = 2,
        NorthWest = 3
    }

    public enum FacingDirection
    {
        Front,
        Back
    }

    public enum FrameVariant
    {
        Base = 0,
        Up = 1,
        Down = 2,
        Left = 3,
        Right = 4
    }

    /// <summary>
    /// 区域扩展姿态（控制头/身体区域扩展轴相对于屏幕的方向）
    /// </summary>
    public enum RegionExpandPose
    {
        HeadUp = 0,    // 头在上：扩展轴不旋转（默认站立）
        HeadLeft = 1,  // 头在左：扩展轴逆时针旋转 90 度
        HeadRight = 2, // 头在右：扩展轴顺时针旋转 90 度
        HeadDown = 3   // 头在下：扩展轴旋转 180 度（倒立）
    }

    /// <summary>
    /// UV 空间方向配置（部位 UV 用）
    /// </summary>
    public enum UVOrientation
    {
        UpRight = 0,
        DownLeft = 1,
        UpLeft = 2,
        DownRight = 3
    }

    /// <summary>
    /// 武器锚点朝向（仅用于武器旋转）
    /// 以 South 为默认（枚举值 0），逆时针递增
    /// </summary>
    public enum AnchorDirection
    {
        South = 0, // 默认，武器朝下
        SouthWest = 1,
        West = 2,
        NorthWest = 3,
        North = 4,
        NorthEast = 5,
        East = 6,
        SouthEast = 7
    }

    #endregion

    #region 锚点 - 武器挂点

    /// <summary>
    /// 锚点类型 - 只用于武器
    /// </summary>
    public enum AnchorType
    {
        MainHandWeapon, // 主手武器锚点
        OffHandWeapon // 副手武器锚点
    }

    [Serializable]
    public class AnchorPoint
    {
        public AnchorType type;
        public Vector2Int position;
        public AnchorDirection direction; // 默认 South（枚举值 0）

        /// <summary>
        /// 获取旋转角度（以 South 为基准 0 度，逆时针为正）
        /// </summary>
        public float GetRotationAngle()
        {
            switch (direction)
            {
                case AnchorDirection.South:
                    return 0f;
                case AnchorDirection.SouthEast:
                    return 45f;
                case AnchorDirection.East:
                    return 90f;
                case AnchorDirection.NorthEast:
                    return 135f;
                case AnchorDirection.North:
                    return 180f;
                case AnchorDirection.NorthWest:
                    return -135f;
                case AnchorDirection.West:
                    return -90f;
                case AnchorDirection.SouthWest:
                    return -45f;
                default:
                    return 0f; // South
            }
        }
    }

    #endregion

    #region 锚点映射

    public static class AnchorFacingConfig
    {
        /// <summary>
        /// 判断指定锚点在当前朝向下是否位于角色左侧
        /// </summary>
        public static bool IsAnchorOnLeftSide(AnchorType anchorType, CharacterFacing facing)
        {
            bool mainHandOnLeft = IsMainHandAnchorOnLeft(facing);
            return (anchorType == AnchorType.MainHandWeapon) == mainHandOnLeft;
        }

        /// <summary>
        /// 判断主手锚点在当前朝向下是否位于角色左侧（内部用）
        // SE：主手在左，副手在右 
        // SW：主手在右，副手在左 
        // NE：主手在右，副手在左 
        // NW：主手在左，副手在右 
        // SE：右手在前，左手在后
        // SW：右手在后，左手在前 
        // NE：右手在前，左手在后 
        // NW：右手在后，左手在前 
        /// </summary>
        static bool IsMainHandAnchorOnLeft(CharacterFacing facing)
        {
            return facing == CharacterFacing.SouthEast || facing == CharacterFacing.NorthWest;
        }
    }

    #endregion

    #region 部位涂色 - 服装/手套/鞋子用

    /// <summary>
    /// 身体部位类型
    /// </summary>
    public enum CharacterBodyPart
    {
        Head, // 头部 4x3
        Torso, // 身体 2x3 (衣服映射)
        LeftHand, // 左手 1px
        RightHand, // 右手 1px
        LeftFoot, // 左脚 1px
        RightFoot, // 右脚 1px
        LeftEye, // 左眼（头部区域内的黑色像素）
        RightEye // 右眼（头部区域内的黑色像素）
    }

    /// <summary>
    /// 部位像素标记（单个像素）
    /// </summary>
    [Serializable]
    public class BodyPartPixel
    {
        public CharacterBodyPart part;
        public Vector2Int position;
        public Color32 color; // 原始颜色（用于匹配）

        public bool isCore;

        /// <summary>
        /// UV 坐标：直接存储要采样装备贴图的 UV 位置
        /// 从 UV 画板拷贝而来，或由扩展算法从边界像素复制
        /// </summary>
        public Vector2 uv = new Vector2(-1, -1); // -1 表示未设置

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

        /// <summary>
        /// 贴图变体（基础/向上/向下 等）
        /// </summary>
        public FrameVariant variant = FrameVariant.Base;

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
            if (pixels.Count == 0)
                return new RectInt(0, 0, 0, 0);

            int minX = int.MaxValue,
                maxX = int.MinValue;
            int minY = int.MaxValue,
                maxY = int.MinValue;
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

    /// <summary>
    /// 手脚蒙版数据（用于颜色替换，不需要UV映射）
    /// </summary>
    [Serializable]
    public class LimbMask
    {
        public List<Vector2Int> leftHand = new List<Vector2Int>();
        public List<Vector2Int> rightHand = new List<Vector2Int>();
        public List<Vector2Int> leftFoot = new List<Vector2Int>();
        public List<Vector2Int> rightFoot = new List<Vector2Int>();
        public List<Vector2Int> leftEye = new List<Vector2Int>();
        public List<Vector2Int> rightEye = new List<Vector2Int>();

        public List<Vector2Int> GetPixels(CharacterBodyPart part)
        {
            switch (part)
            {
                case CharacterBodyPart.LeftHand:
                    return leftHand;
                case CharacterBodyPart.RightHand:
                    return rightHand;
                case CharacterBodyPart.LeftFoot:
                    return leftFoot;
                case CharacterBodyPart.RightFoot:
                    return rightFoot;
                case CharacterBodyPart.LeftEye:
                    return leftEye;
                case CharacterBodyPart.RightEye:
                    return rightEye;
                default:
                    return null;
            }
        }

        public void SetPixels(CharacterBodyPart part, IEnumerable<Vector2Int> pixels)
        {
            var list = GetPixels(part);
            if (list != null)
            {
                list.Clear();
                list.AddRange(pixels);
            }
        }

        public void Clear()
        {
            leftHand.Clear();
            rightHand.Clear();
            leftFoot.Clear();
            rightFoot.Clear();
            leftEye.Clear();
            rightEye.Clear();
        }

        public bool IsEmpty =>
            leftHand.Count == 0
            && rightHand.Count == 0
            && leftFoot.Count == 0
            && rightFoot.Count == 0
            && leftEye.Count == 0
            && rightEye.Count == 0;
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

        [Header("部位区域 - UV贴图（头/身体）")]
        public List<BodyPartRegion> bodyRegions = new List<BodyPartRegion>();

        [Header("手脚蒙版 - 颜色替换")]
        public LimbMask limbMask = new LimbMask();

        public bool leftEyeClosed;
        public bool rightEyeClosed;
        public bool hitOutlineFrame;

        // 武器序列帧渲染时的额外偏移（像素），仅用于在无锚点或需要细调时调整武器位置
        public Vector2Int sequenceOffset;

        public AnchorPoint GetAnchor(AnchorType type) => anchors.Find(a => a.type == type);

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
            // 手脚检查 limbMask
            if (IsLimbPart(part))
            {
                var pixels = limbMask?.GetPixels(part);
                return pixels != null && pixels.Count > 0;
            }
            // UV部位检查 bodyRegions
            var r = GetRegion(part);
            return r != null && r.pixels.Count > 0;
        }

        /// <summary>
        /// 获取手脚蒙版像素
        /// </summary>
        public List<Vector2Int> GetLimbPixels(CharacterBodyPart part)
        {
            return limbMask?.GetPixels(part);
        }

        /// <summary>
        /// 判断是否为手脚部位
        /// </summary>
        public static bool IsLimbPart(CharacterBodyPart part)
        {
            return part == CharacterBodyPart.LeftHand
                || part == CharacterBodyPart.RightHand
                || part == CharacterBodyPart.LeftFoot
                || part == CharacterBodyPart.RightFoot;
        }
    }

    [Serializable]
    public class AnimationData
    {
        [Tooltip("动画类型")]
        public AnimationTypeItem animationType;

        /// <summary>
        /// 获取动画类型名
        /// </summary>
        public string GetKey() => animationType != null ? animationType.name : null;

        [Header("Spritesheet 配置")]
        public Texture2D spritesheet;
        public Vector2Int frameSize = new Vector2Int(32, 32);
        public int framesPerRow = 8;
        public int rowCount = 4;

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
        public Color32 leftHandColor = new Color32(221, 183, 143, 255);

        [Tooltip("右手的阴影色，不论朝向如何，右手始终使用此颜色")]
        public Color32 rightHandColor = new Color32(250, 203, 166, 255);

        [Tooltip("左脚的阴影色，不论朝向如何，左脚始终使用此颜色")]
        public Color32 leftFootColor = new Color32(205, 172, 133, 255);

        [Tooltip("右脚的阴影色，不论朝向如何，右脚始终使用此颜色")]
        public Color32 rightFootColor = new Color32(238, 195, 154, 255);

        [Header("检测参数")]
        [Tooltip("描边检测阈值：RGB之和小于此值视为描边（建议50-100）")]
        public int outlineThreshold = 80;

        [Tooltip("手脚颜色匹配容差（RGB差值之和，默认30可容忍轻微色差）")]
        public int limbColorThreshold = 30;

        public Color32 closedEyeColor = new Color32(0, 0, 0, 0);
        public int closedEyeColorThreshold = 30;

        /// <summary>
        /// 是否为描边/黑色像素
        /// </summary>
        public bool IsOutline(Color32 c) => (c.r + c.g + c.b) < outlineThreshold && c.a > 0;

        /// <summary>
        /// 手脚颜色是否匹配（带容差）
        /// </summary>
        public bool IsLimbColorMatch(Color32 pixel, Color32 limbColor)
        {
            if (pixel.a == 0 || IsOutline(pixel))
                return false;
            return ColorSimilar(pixel, limbColor, limbColorThreshold);
        }

        /// <summary>
        /// 是否为有色非黑色像素
        /// 额外约定：纯白 (255,255,255) 不视为有效有色像素，用于避免高光/辅助标记干扰自动检测
        /// </summary>
        public bool IsColoredPixel(Color32 c)
        {
            if (c.a == 0)
                return false;

            // 忽略纯白像素
            if (c.r == 255 && c.g == 255 && c.b == 255)
                return false;

            return !IsOutline(c);
        }

        /// <summary>
        /// 判断颜色是否与皮肤色相近（用手部颜色作为参考）
        /// </summary>
        public bool IsSkinLike(Color32 c)
        {
            if (c.a == 0 || IsOutline(c))
                return false;

            // 与任一手部颜色相近即可，复用 limbColorThreshold
            return ColorSimilar(c, leftHandColor, limbColorThreshold)
                || ColorSimilar(c, rightHandColor, limbColorThreshold);
        }

        public bool IsClosedEyeColor(Color32 pixel)
        {
            // 闭眼颜色比较时忽略 alpha，只比较 RGB
            if (closedEyeColor.r == 0 && closedEyeColor.g == 0 && closedEyeColor.b == 0)
                return false;
            if (pixel.a == 0 || IsOutline(pixel))
                return false;
            return ColorSimilarRGB(pixel, closedEyeColor, closedEyeColorThreshold);
        }

        /// <summary>
        /// 判断两个颜色的 RGB 是否相近（忽略 alpha）
        /// </summary>
        public bool ColorSimilarRGB(Color32 a, Color32 b, int threshold)
        {
            int dr = Mathf.Abs(a.r - b.r);
            int dg = Mathf.Abs(a.g - b.g);
            int db = Mathf.Abs(a.b - b.b);
            return (dr + dg + db) < threshold;
        }

        /// <summary>
        /// 判断两个颜色是否相近
        /// </summary>
        public bool ColorSimilar(Color32 a, Color32 b, int threshold)
        {
            int dr = Mathf.Abs(a.r - b.r);
            int dg = Mathf.Abs(a.g - b.g);
            int db = Mathf.Abs(a.b - b.b);
            return (dr + dg + db) < threshold;
        }
    }

    #endregion

    [CreateAssetMenu(
        fileName = "CharacterFrameData",
        menuName = "Equipment System/Character Frame Data"
    )]
    public class CharacterFrameData : ScriptableObject
    {
        [Header("编辑器配置")]
        [Tooltip("动画类型数据库")]
        public AnimationTypeDatabase animDatabase;

        [Tooltip("UV 画板参考底图")]
        public Sprite paletteRefSprite;

        [Tooltip("UV 画板尺寸")]
        public Vector2Int paletteSize = new Vector2Int(32, 32);

        [Tooltip("头部 UV 源区域（画板上）")]
        public RectInt headUVRegion = new RectInt(0, 0, 4, 3);

        [Tooltip("身体 UV 源区域（画板上）")]
        public RectInt torsoUVRegion = new RectInt(0, 3, 3, 2);

        [Tooltip("头部检测目标区域大小")]
        public Vector2Int headDetectSize = new Vector2Int(4, 3);

        [Tooltip("身体检测目标区域大小")]
        public Vector2Int torsoDetectSize = new Vector2Int(3, 2);

        [Header("头部区域扩展配置")]
        [Tooltip("头部向上扩展的像素数")]
        public int headExpandUp = 10;

        [Tooltip("头部向左右扩展的像素数")]
        public int headExpandSide = 10;

        [Tooltip("头部向下扩展的像素数")]
        public int headExpandDown = 3;

        [Header("身体区域扩展配置")]
        [Tooltip("身体向上扩展的像素数")]
        public int bodyExpandUp = 20;

        [Tooltip("身体向上扩展的起始步长（1 表示紧贴身体向上，>1 表示跳过若干行再开始扩展）")]
        public int bodyExpandUpStartStep = 1;

        [Tooltip("身体向左右扩展的像素数")]
        public int bodyExpandSide = 10;

        [Tooltip("身体向下扩展的像素数")]
        public int bodyExpandDown = 2;

        [Tooltip("身体向下扩展的起始步长（1 表示紧贴身体向下，>1 表示跳过若干行再开始扩展）")]
        public int bodyExpandDownStartStep = 1;

        [Header("区域扩展姿态")]
        [Tooltip("控制区域扩展时角色头相对于屏幕的方向：头在上/左/右/下，对应扩展轴旋转 0°/±90°/180°")]
        public RegionExpandPose regionExpandPose = RegionExpandPose.HeadUp;

        [Header("阴影配置")]
        [Tooltip("帧内地面Y坐标（像素），所有角色共用同一基准线")]
        public int groundPixelY = 8;

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
        /// 获取动画数据（按类型）
        /// </summary>
        public AnimationData GetAnimation(AnimationTypeItem animType)
        {
            if (animType == null)
                return null;
            return animations.Find(x => x.animationType == animType);
        }

        /// <summary>
        /// 获取动画数据（按 Key，用于与 Animator 参数匹配）
        /// </summary>
        public AnimationData GetAnimationByKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return null;
            return animations.Find(x =>
                x.animationType != null
                && string.Equals(
                    x.animationType.name,
                    key,
                    System.StringComparison.OrdinalIgnoreCase
                )
            );
        }

        /// <summary>
        /// 获取帧数据（按动画类型）
        /// </summary>
        public FrameData GetFrameData(AnimationTypeItem animType, int rowIndex, int frame)
        {
            var a = GetAnimation(animType);
            if (a == null)
                return null;
            return a.GetFrame(frame, rowIndex);
        }

        /// <summary>
        /// 获取帧数据（按 Key）
        /// </summary>
        public FrameData GetFrameDataByKey(string key, int rowIndex, int frame)
        {
            var a = GetAnimationByKey(key);
            if (a == null)
                return null;
            return a.GetFrame(frame, rowIndex);
        }

        /// <summary>
        /// 获取或创建动画数据
        /// </summary>
        public AnimationData GetOrCreateAnimation(AnimationTypeItem animType)
        {
            if (animType == null)
                return null;
            var a = GetAnimation(animType);
            if (a == null)
            {
                a = new AnimationData { animationType = animType };
                animations.Add(a);
            }
            return a;
        }

        /// <summary>
        /// 获取所有动画类型
        /// </summary>
        public List<AnimationTypeItem> GetAnimationTypes()
        {
            var types = new List<AnimationTypeItem>();
            foreach (var a in animations)
            {
                if (a.animationType != null)
                    types.Add(a.animationType);
            }
            return types;
        }

        public static FacingDirection GetFacingDirection(CharacterFacing f) =>
            (f == CharacterFacing.SouthEast || f == CharacterFacing.SouthWest)
                ? FacingDirection.Front
                : FacingDirection.Back;
    }
}
