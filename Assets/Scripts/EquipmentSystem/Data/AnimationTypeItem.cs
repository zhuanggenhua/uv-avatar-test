using UnityEngine;

namespace EquipmentSystem.Data
{
    /// <summary>
    /// 装备变体循环类型
    /// </summary>
    public enum EquipVariantLoopType
    {
        Idle = 0,
        Move = 1
    }
    
    /// <summary>
    /// 动画类型枚举项
    /// 资源名 = 类型名（与 Animator Bool 参数名一致）
    /// </summary>
    [CreateAssetMenu(fileName = "NewAnimationType", menuName = "Equipment System/Animation Type Item")]
    public class AnimationTypeItem : ScriptableObject
    {
        [Tooltip("该动画使用的装备变体循环类型")]
        public EquipVariantLoopType variantLoopType = EquipVariantLoopType.Idle;
        
        public override string ToString() => name;
    }
}
