using System;
using System.Collections.Generic;
using UnityEngine;

namespace EquipmentSystem.Data
{
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
        
        /// <summary>
        /// 根据帧索引获取 Sprite（循环播放）
        /// </summary>
        public Sprite GetFrame(int frameIndex)
        {
            if (frames == null || frames.Count == 0) return null;
            if (frames.Count == 1) return frames[0];
            return frames[frameIndex % frames.Count];
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

            return strip?.GetFrame(frameIndex);
        }
        
        /// <summary>
        /// 尝试获取指定行索引和帧索引的 Sprite
        /// </summary>
        public Sprite TryGetSpriteByRow(int rowIndex, int frameIndex)
        {
            return TryGetSprite((CharacterFacing)rowIndex, frameIndex);
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
    
    /// <summary>
    /// 装备动画集资源（一整套动画，可被多个装备共享）
    /// 
    /// 用途：
    /// - 武器：一套武器动画（Idle/Walk/Attack/Die 等）
    /// - 服装/头盔：特殊服装的动画集（如斗篷）
    /// 
    /// 特点：
    /// - 包含多个动画（Idle、Walk、Attack、Die 等）
    /// - 每个动画支持 4 向或单向序列帧
    /// - 不同武器/服装可共用同一个动画集
    /// </summary>
    [CreateAssetMenu(fileName = "EquipAnimSet", menuName = "Equipment System/Equip Anim Set")]
    public class EquipAnimSetAsset : ScriptableObject
    {
        [Header("基础信息")]
        [Tooltip("动画集 ID（可选，用于调试）")]
        public string setId;
        
        [Tooltip("动画集描述")]
        public string description;
        
        [Header("动画列表")]
        [Tooltip("包含的所有动画（Idle/Walk/Attack/Die 等）")]
        public List<AnimSequenceEntry> animations = new List<AnimSequenceEntry>();
        
        /// <summary>
        /// 根据动画类型获取对应的动画条目
        /// </summary>
        public AnimSequenceEntry GetAnimation(AnimationTypeItem animType)
        {
            if (animations == null || animType == null)
                return null;
            return animations.Find(a => a.animationType == animType);
        }
        
        /// <summary>
        /// 根据 Key 获取对应的动画条目（用于与 Animator 参数匹配）
        /// </summary>
        public AnimSequenceEntry GetAnimationByKey(string key)
        {
            if (animations == null || string.IsNullOrEmpty(key))
                return null;
            return animations.Find(a => 
                a.animationType != null && 
                string.Equals(a.animationType.name, key, StringComparison.OrdinalIgnoreCase));
        }
        
        /// <summary>
        /// 尝试获取指定动画、方向、帧索引的 Sprite
        /// </summary>
        /// <param name="animType">动画类型</param>
        /// <param name="rowIndex">行索引（方向）</param>
        /// <param name="frameIndex">帧索引</param>
        /// <returns>找到则返回 Sprite，否则返回 null（表示应回退到默认行为）</returns>
        public Sprite TryGetSprite(AnimationTypeItem animType, int rowIndex, int frameIndex)
        {
            var anim = GetAnimation(animType);
            return anim?.TryGetSpriteByRow(rowIndex, frameIndex);
        }
        
        /// <summary>
        /// 尝试获取指定动画 Key、方向、帧索引的 Sprite（用于与 Animator 匹配）
        /// </summary>
        public Sprite TryGetSpriteByKey(string key, int rowIndex, int frameIndex)
        {
            var anim = GetAnimationByKey(key);
            return anim?.TryGetSpriteByRow(rowIndex, frameIndex);
        }
        
        /// <summary>
        /// 检查是否包含指定动画类型
        /// </summary>
        public bool HasAnimation(AnimationTypeItem animType)
        {
            return GetAnimation(animType) != null;
        }
        
        /// <summary>
        /// 获取所有动画类型列表
        /// </summary>
        public List<AnimationTypeItem> GetAnimationTypes()
        {
            var types = new List<AnimationTypeItem>();
            if (animations != null)
            {
                foreach (var anim in animations)
                {
                    if (anim.animationType != null)
                        types.Add(anim.animationType);
                }
            }
            return types;
        }
    }
}
