using UnityEngine;

namespace EquipmentSystem.Data
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
        
        [Header("肤色")]
        public Color skinColor = new Color(1f, 0.85f, 0.7f, 1f);
        
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
        /// 根据行索引获取头发贴图 (0=SE, 1=SW, 2=NE, 3=NW)
        /// </summary>
        public Sprite GetHairByRow(int rowIndex)
        {
            return GetHairSprite((CharacterFacing)rowIndex);
        }
        
        /// <summary>
        /// 根据行索引获取胡子贴图 (0=SE, 1=SW, 2=NE, 3=NW)
        /// </summary>
        public Sprite GetBeardByRow(int rowIndex)
        {
            return GetBeardSprite((CharacterFacing)rowIndex);
        }
        
        /// <summary>
        /// 是否有头发
        /// </summary>
        public bool HasHair => hairSE != null;
        
        /// <summary>
        /// 是否有胡子
        /// </summary>
        public bool HasBeard => beardSE != null;
    }
}
