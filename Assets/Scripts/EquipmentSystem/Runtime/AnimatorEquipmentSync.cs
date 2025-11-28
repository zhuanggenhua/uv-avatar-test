using UnityEngine;

namespace EquipmentSystem.Runtime
{
    /// <summary>
    /// 同步 Animator 状态到 EquipmentRenderer
    /// 自动检测当前播放的动画和帧索引
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(EquipmentRenderer))]
    public class AnimatorEquipmentSync : MonoBehaviour
    {
        public enum DirectionMode
        {
            IntParameter,  // 使用单个 int 参数 (0=SE, 1=SW, 2=NE, 3=NW)
            XYFloat        // 使用 X/Y float 参数 (参考 CTR_AnimateCreature)
        }
        
        [Header("方向设置")]
        public DirectionMode directionMode = DirectionMode.XYFloat;
        
        [Tooltip("整数方向参数名 (DirectionMode=IntParameter 时使用)")]
        public string intDirectionParam = "Direction";
        
        [Tooltip("X 方向参数名 (DirectionMode=XYFloat 时使用)")]
        public string xParam = "X";
        [Tooltip("Y 方向参数名 (DirectionMode=XYFloat 时使用)")]
        public string yParam = "Y";
        
        Animator _animator;
        EquipmentRenderer _equipRenderer;
        SpriteRenderer _spriteRenderer;
        
        int _lastFrame = -1;
        int _lastRow = -1;
        string _lastAnimName;
        Sprite _lastSprite;
        
        void Awake()
        {
            _animator = GetComponent<Animator>();
            _equipRenderer = GetComponent<EquipmentRenderer>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }
        
        void LateUpdate()
        {
            if (_animator == null || _equipRenderer == null) return;
            
            bool needRefresh = false;
            
            // 检测动画名称变化
            var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            string animName = GetCurrentAnimationName(stateInfo);
            if (animName != _lastAnimName)
            {
                _lastAnimName = animName;
                _equipRenderer.currentAnimation = animName;
                needRefresh = true;
            }
            
            // 检测行（方向）变化
            int row = GetCurrentRow();
            if (row != _lastRow)
            {
                _lastRow = row;
                _equipRenderer.rowIndex = row;
                needRefresh = true;
            }
            
            // 检测帧变化（通过 Sprite 变化判断）
            if (_spriteRenderer != null && _spriteRenderer.sprite != _lastSprite)
            {
                _lastSprite = _spriteRenderer.sprite;
                int frame = GetFrameIndexFromSprite(_lastSprite);
                if (frame != _lastFrame)
                {
                    _lastFrame = frame;
                    _equipRenderer.SetFrame(frame);
                    needRefresh = false; // SetFrame 已经调用 Refresh
                }
            }
            
            if (needRefresh)
                _equipRenderer.Refresh();
        }
        
        /// <summary>
        /// 获取当前行索引
        /// </summary>
        int GetCurrentRow()
        {
            if (directionMode == DirectionMode.IntParameter)
            {
                if (!string.IsNullOrEmpty(intDirectionParam))
                    return _animator.GetInteger(intDirectionParam);
                return 0;
            }
            else // XYFloat
            {
                float x = !string.IsNullOrEmpty(xParam) ? _animator.GetFloat(xParam) : 0;
                float y = !string.IsNullOrEmpty(yParam) ? _animator.GetFloat(yParam) : 0;
                
                // X/Y 转换为行索引
                // Y > 0: 朝上 (NE/NW), Y <= 0: 朝下 (SE/SW)
                // X >= 0: 朝右 (SE/NE), X < 0: 朝左 (SW/NW)
                bool facingUp = y > 0;
                bool facingRight = x >= 0;
                
                if (!facingUp && facingRight) return 0;  // SE
                if (!facingUp && !facingRight) return 1; // SW
                if (facingUp && facingRight) return 2;   // NE
                return 3; // NW
            }
        }
        
        /// <summary>
        /// 从 Animator 状态获取动画名称
        /// </summary>
        string GetCurrentAnimationName(AnimatorStateInfo stateInfo)
        {
            // 尝试从 EquipmentRenderer 的 frameData 中匹配
            if (_equipRenderer.frameData != null)
            {
                foreach (var animName in _equipRenderer.frameData.animationNames)
                {
                    if (stateInfo.IsName(animName))
                        return animName;
                }
            }
            
            // 回退到当前名称
            return _equipRenderer.currentAnimation;
        }
        
        /// <summary>
        /// 从 Sprite 名称解析帧索引
        /// 假设命名格式: xxx_0, xxx_1, xxx_2 等
        /// </summary>
        int GetFrameIndexFromSprite(Sprite sprite)
        {
            if (sprite == null) return 0;
            
            string name = sprite.name;
            int lastUnderscore = name.LastIndexOf('_');
            if (lastUnderscore >= 0 && lastUnderscore < name.Length - 1)
            {
                string indexStr = name.Substring(lastUnderscore + 1);
                if (int.TryParse(indexStr, out int index))
                {
                    // 需要考虑行偏移
                    // 假设切片是按行排列的: 0-7=第0行, 8-15=第1行...
                    int framesPerRow = _equipRenderer.frameData != null 
                        ? _equipRenderer.frameData.framesPerRow 
                        : 8;
                    return index % framesPerRow;
                }
            }
            
            return 0;
        }
        
        /// <summary>
        /// 设置方向 (X/Y 模式)
        /// </summary>
        public void SetDirection(float x, float y)
        {
            if (_animator == null) return;
            if (!string.IsNullOrEmpty(xParam)) _animator.SetFloat(xParam, x);
            if (!string.IsNullOrEmpty(yParam)) _animator.SetFloat(yParam, y);
        }
        
        /// <summary>
        /// 设置方向 (Int 模式)
        /// </summary>
        public void SetDirection(int row)
        {
            if (_animator == null) return;
            if (!string.IsNullOrEmpty(intDirectionParam))
                _animator.SetInteger(intDirectionParam, row);
        }
    }
}
