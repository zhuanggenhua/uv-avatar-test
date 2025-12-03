using UnityEngine;

namespace EquipmentSystem.Data
{
    /// <summary>
    /// 动画类型枚举项
    /// 资源名 = 类型名（与 Animator Bool 参数名一致）
    /// </summary>
    [CreateAssetMenu(fileName = "NewAnimationType", menuName = "Equipment System/Animation Type Item")]
    public class AnimationTypeItem : ScriptableObject
    {
        public override string ToString() => name;
    }
}
