using UnityEngine;

namespace EquipmentSystem
{
    /// <summary>
    /// 动画控制器组件
    /// 挂在角色上，提供动画切换、方向控制和阴影开关 API
    /// </summary>
    public class AnimationController : MonoBehaviour
    {
        [Header("动画配置")]
        [Tooltip("动画类型数据库")]
        public AnimationTypeDatabase animDatabase;
        
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
        AnimationTypeItem _lastAnimType;
        
        /// <summary>当前动画索引</summary>
        public int CurrentAnimationIndex => _currentAnimIndex;
        
        /// <summary>当前方向索引</summary>
        public int CurrentDirectionIndex => _currentDirIndex;
        
        /// <summary>阴影是否显示</summary>
        public bool ShadowEnabled => _shadowEnabled;
        
        /// <summary>获取 Animator</summary>
        public Animator Animator => _animator;
        
        /// <summary>
        /// 根据方向索引获取方向向量（供其他组件复用）
        /// </summary>
        public static Vector2 GetDirectionValue(int index)
        {
            if (index < 0 || index >= DirectionValues.Length)
                return DirectionValues[0];
            return DirectionValues[index];
        }
        
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
            if (animDatabase == null || index < 0 || index >= animDatabase.Count) return;
            _currentAnimIndex = index;
            ApplyAnimation();
        }
        
        /// <summary>
        /// 设置动画（按类型）
        /// </summary>
        public void SetAnimation(AnimationTypeItem animType)
        {
            if (animDatabase == null || animType == null) return;
            int index = animDatabase.IndexOf(animType);
            if (index >= 0)
            {
                _currentAnimIndex = index;
                ApplyAnimation();
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
            
            // 按名称查找 Shadow 子对象
            var shadow = transform.Find("Shadow");
            if (shadow != null)
            {
                _shadowObject = shadow.gameObject;
                _shadowEnabled = _shadowObject.activeSelf;
            }
        }
        
        void ApplyAnimation()
        {
            if (_animator == null || animDatabase == null) return;
            
            // 关闭所有动画
            if (_lastAnimType == null)
            {
                foreach (var animType in animDatabase.ItemsReadOnly)
                {
                    if (animType != null)
                    {
                        try { _animator.SetBool(animType.name, false); } catch { }
                    }
                }
            }
            else
            {
                try { _animator.SetBool(_lastAnimType.name, false); } catch { }
            }
            
            // 开启当前动画
            if (animDatabase.TryGetByIndex(_currentAnimIndex, out var currentType) && currentType != null)
            {
                try
                {
                    _animator.SetTrigger("Clicked");
                    _animator.SetBool(currentType.name, true);
                }
                catch { }
                _lastAnimType = currentType;
            }
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
