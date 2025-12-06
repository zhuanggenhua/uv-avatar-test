using UnityEngine;

namespace EquipmentSystem
{
    /// <summary>
    /// 角色外观数据 - 用于捏人系统
    /// 包含头发、胡子等角色基础外观（非装备）
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterAppearance", menuName = "Equipment System/Character Appearance")]
    public class CharacterAppearance : ScriptableObject
    {
        [Header("头发 (4方向)")]
        [Tooltip("东南方向 (必填，其他方向为空时回退到此)")]
        public Sprite hairSE;
        public Sprite hairSW;
        public Sprite hairNE;
        public Sprite hairNW;
        
        [Header("胡子 (4方向)")]
        [Tooltip("东南方向 (可选)")]
        public Sprite beardSE;
        public Sprite beardSW;
        public Sprite beardNE;
        public Sprite beardNW;
        
        [Header("面部装饰 (朝南时显示)")]
        [Tooltip("只在朝南(SE/SW)时显示，朝北不显示")]
        public Sprite faceAccessorySE;
        public Sprite faceAccessorySW;
        
        [Header("肤色")]
        public Color skinColor = new Color(1f, 0.85f, 0.7f, 1f);
        
        [Header("眼睛颜色")]
        public Color leftEyeColor = Color.black;
        public Color rightEyeColor = Color.black;
        
        /// <summary>
        /// 获取指定方向的头发贴图
        /// </summary>
        public Sprite GetHairSprite(CharacterFacing facing)
        {
            return DirectionalSpriteHelper.GetByFacing(facing, hairSE, hairSW, hairNE, hairNW);
        }
        
        /// <summary>
        /// 获取指定方向的胡子贴图
        /// </summary>
        public Sprite GetBeardSprite(CharacterFacing facing)
        {
            return DirectionalSpriteHelper.GetByFacing(facing, beardSE, beardSW, beardNE, beardNW);
        }
        
        /// <summary>
        /// 获取指定方向的面部装饰贴图
        /// 特殊处理：不回退，未填写的方向返回 null
        /// </summary>
        public Sprite GetFaceAccessorySprite(CharacterFacing facing)
        {
            switch (facing)
            {
                case CharacterFacing.SouthEast: return faceAccessorySE;
                case CharacterFacing.SouthWest: return faceAccessorySW;
                default: return null;
            }
        }
        
        /// <summary>
        /// 是否有头发
        /// </summary>
        public bool HasHair => hairSE != null;
        
        /// <summary>
        /// 是否有胡子
        /// </summary>
        public bool HasBeard => beardSE != null;
        
        /// <summary>
        /// 是否有面部装饰（任一方向有即可）
        /// </summary>
        public bool HasFaceAccessory => faceAccessorySE != null || faceAccessorySW != null;
    }
}
