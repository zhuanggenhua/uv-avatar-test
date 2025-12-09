using System;
using System.Collections.Generic;
using UnityEngine;

namespace EquipmentSystem
{
    public enum FrameDepthMode
    {
        [InspectorName("身前")] Front,
        [InspectorName("背后")] Back
    }

    /// <summary>
    /// 某一方向的序列帧条（时间轴）
    /// </summary>
    [Serializable]
    public class DirectionalStrip
    {
        [Tooltip("该 strip 对应的角色朝向")]
        public CharacterFacing facing;
        
        [Tooltip("时间序列帧；长度为 1 时视为静态图")]
        public List<Sprite> frames = new List<Sprite>();
        public List<FrameDepthMode> depthModes = new List<FrameDepthMode>();

        void EnsureDepthListLength()
        {
            if (frames == null) return;
            int targetCount = frames.Count;
            if (targetCount <= 0) return;
            if (depthModes == null)
                depthModes = new List<FrameDepthMode>(targetCount);

            while (depthModes.Count < targetCount)
            {
                depthModes.Add(FrameDepthMode.Front);
            }
        }

        /// <summary>
        /// 根据帧索引获取 Sprite（循环播放）
        /// </summary>
        public Sprite GetFrame(int frameIndex)
        {
            FrameDepthMode _;
            return GetFrame(frameIndex, out _);
        }

        public Sprite GetFrame(int frameIndex, out FrameDepthMode depthMode)
        {
            depthMode = FrameDepthMode.Front;
            if (frames == null || frames.Count == 0) return null;

            int index = frames.Count == 1 ? 0 : frameIndex % frames.Count;
            if (index < 0) index = 0;

            EnsureDepthListLength();
            if (depthModes != null && index < depthModes.Count)
                depthMode = depthModes[index];

            return frames[index];
        }
        
        /// <summary>
        /// 是否有效（有帧数据）
        /// </summary>
        public bool IsValid => frames != null && frames.Count > 0;
    }
    
    /// <summary>
    /// 单个动画的序列帧数据（内嵌在动画集中）
    /// </summary>
    [Serializable]
    public class AnimSequenceEntry
    {
        [Tooltip("动画类型")]
        public AnimationTypeItem animationType;
        
        [Tooltip("各方向的序列帧条；4 向时填 4 条，单向时只填 1 条")]
        public List<DirectionalStrip> strips = new List<DirectionalStrip>();
        
        /// <summary>
        /// 获取动画类型名
        /// </summary>
        public string GetKey() => animationType != null ? animationType.name : null;
        
        /// <summary>
        /// 根据方向获取对应的 strip
        /// </summary>
        public DirectionalStrip GetStrip(CharacterFacing facing)
        {
            return strips.Find(s => s.facing == facing && s.IsValid);
        }
        
        /// <summary>
        /// 尝试获取指定方向和帧索引的 Sprite
        /// </summary>
        public Sprite TryGetSprite(CharacterFacing facing, int frameIndex)
        {
            FrameDepthMode _;
            return TryGetSprite(facing, frameIndex, out _);
        }

        public Sprite TryGetSprite(CharacterFacing facing, int frameIndex, out FrameDepthMode depthMode)
        {
            depthMode = FrameDepthMode.Front;

            // 1. 先按精确方向查找 strip
            var strip = GetStrip(facing);

            // 2. 若 NE/NW 缺失，按统一规则回退：
            //    NE: 优先自身，其次 SE
            //    NW: 优先自身，其次 SW，再其次 SE
            if (strip == null)
            {
                switch (facing)
                {
                    case CharacterFacing.NorthEast:
                        strip = GetStrip(CharacterFacing.SouthEast);
                        break;
                    case CharacterFacing.NorthWest:
                        strip = GetStrip(CharacterFacing.SouthWest) ?? GetStrip(CharacterFacing.SouthEast);
                        break;
                }
            }

            if (strip == null)
                return null;

            return strip.GetFrame(frameIndex, out depthMode);
        }
        
        /// <summary>
        /// 尝试获取指定行索引和帧索引的 Sprite
        /// </summary>
        public Sprite TryGetSpriteByRow(int rowIndex, int frameIndex)
        {
            return TryGetSprite((CharacterFacing)rowIndex, frameIndex);
        }

        public Sprite TryGetSpriteByRow(int rowIndex, int frameIndex, out FrameDepthMode depthMode)
        {
            return TryGetSprite((CharacterFacing)rowIndex, frameIndex, out depthMode);
        }
        
        /// <summary>
        /// 检查是否有任何有效的 strip
        /// </summary>
        public bool HasAnyStrip => strips != null && strips.Exists(s => s.IsValid);
        
        /// <summary>
        /// 检查是否为完整 4 向序列帧
        /// </summary>
        public bool IsFull4Direction
        {
            get
            {
                return GetStrip(CharacterFacing.SouthEast) != null &&
                       GetStrip(CharacterFacing.SouthWest) != null &&
                       GetStrip(CharacterFacing.NorthEast) != null &&
                       GetStrip(CharacterFacing.NorthWest) != null;
            }
        }
    }
}
