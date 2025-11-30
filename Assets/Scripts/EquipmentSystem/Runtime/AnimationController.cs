using UnityEngine;

namespace EquipmentSystem.Runtime
{
    /// <summary>
    /// 动画控制器组件
    /// 挂在角色上，提供动画和方向控制 API
    /// 替代原 MINIFANTASY 的 CTR_AnimateCreature
    /// </summary>
    public class AnimationController : MonoBehaviour
    {
        [Header("动画配置")]
        [Tooltip("可用的动画名称列表（对应 Animator 的 Bool 参数）")]
        public string[] animationNames = { "Idle", "Walk", "Run", "Attack", "Hurt", "Die" };
        
        [Header("方向配置")]
        [Tooltip("方向名称")]
        public string[] directionNames = { "SE", "SW", "NE", "NW" };
        
        // 方向对应的 X/Y 值: SE(1,-1), SW(-1,-1), NE(1,1), NW(-1,1)
        static readonly Vector2[] DirectionValues = {
            new Vector2(1, -1),   // SE
            new Vector2(-1, -1),  // SW
            new Vector2(1, 1),    // NE
            new Vector2(-1, 1)    // NW
        };
        
        Animator _animator;
        int _currentAnimIndex = 0;
        int _currentDirIndex = 0;
        GameObject _shadowObject;
        bool _shadowEnabled = true;
        
        /// <summary>当前动画索引</summary>
        public int CurrentAnimationIndex => _currentAnimIndex;
        
        /// <summary>当前方向索引</summary>
        public int CurrentDirectionIndex => _currentDirIndex;
        
        /// <summary>阴影是否显示</summary>
        public bool ShadowEnabled => _shadowEnabled;
        
        /// <summary>获取 Animator</summary>
        public Animator Animator => _animator;
        
        void Awake()
        {
            _animator = GetComponentInChildren<Animator>();
            FindShadowObject();
        }
        
        void OnEnable()
        {
            // 激活时应用当前状态
            ApplyAnimation();
            ApplyDirection();
        }
        
        /// <summary>
        /// 设置动画
        /// </summary>
        /// <param name="index">动画索引</param>
        public void SetAnimation(int index)
        {
            if (index < 0 || index >= animationNames.Length) return;
            _currentAnimIndex = index;
            ApplyAnimation();
        }
        
        /// <summary>
        /// 设置动画（按名称）
        /// </summary>
        public void SetAnimation(string animName)
        {
            for (int i = 0; i < animationNames.Length; i++)
            {
                if (animationNames[i] == animName)
                {
                    SetAnimation(i);
                    return;
                }
            }
        }
        
        /// <summary>
        /// 设置方向
        /// </summary>
        /// <param name="index">方向索引 (0=SE, 1=SW, 2=NE, 3=NW)</param>
        public void SetDirection(int index)
        {
            if (index < 0 || index >= DirectionValues.Length) return;
            _currentDirIndex = index;
            ApplyDirection();
        }
        
        /// <summary>
        /// 设置阴影显示
        /// </summary>
        public void SetShadowEnabled(bool enabled)
        {
            _shadowEnabled = enabled;
            if (_shadowObject != null)
                _shadowObject.SetActive(enabled);
        }
        
        /// <summary>
        /// 获取方向名称列表（供 UI 使用）
        /// </summary>
        public string[] GetDirectionNames() => directionNames;
        
        void FindShadowObject()
        {
            _shadowObject = null;
            
            // 尝试按名称查找
            var shadow = transform.Find("Shadow");
            if (shadow != null)
            {
                _shadowObject = shadow.gameObject;
                _shadowEnabled = _shadowObject.activeSelf;
                return;
            }
            
            // 回退: MINIFANTASY 结构 (child[0]/child[0])
            if (transform.childCount > 0)
            {
                var firstChild = transform.GetChild(0);
                if (firstChild.childCount > 0)
                {
                    var possibleShadow = firstChild.GetChild(0).gameObject;
                    if (possibleShadow.name.ToLower().Contains("shadow"))
                    {
                        _shadowObject = possibleShadow;
                        _shadowEnabled = _shadowObject.activeSelf;
                    }
                }
            }
        }
        
        void ApplyAnimation()
        {
            if (_animator == null) return;
            
            // 关闭所有动画
            foreach (var anim in animationNames)
            {
                try { _animator.SetBool(anim, false); } catch { }
            }
            
            // 开启当前动画
            string animName = animationNames[_currentAnimIndex];
            try
            {
                _animator.SetTrigger("Clicked");
                _animator.SetBool(animName, true);
            }
            catch { }
        }
        
        void ApplyDirection()
        {
            if (_animator == null) return;
            
            var dir = DirectionValues[_currentDirIndex];
            try
            {
                _animator.SetFloat("X", dir.x);
                _animator.SetFloat("Y", dir.y);
            }
            catch { }
        }
    }
}
