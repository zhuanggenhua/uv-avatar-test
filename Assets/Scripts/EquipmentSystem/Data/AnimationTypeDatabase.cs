using System.Collections.Generic;
using UnityEngine;

namespace EquipmentSystem.Data
{
    /// <summary>
    /// 动画类型数据库
    /// 集中管理所有动画类型枚举项，提供快速查找
    /// </summary>
    [CreateAssetMenu(fileName = "AnimationTypeDatabase", menuName = "Equipment System/Animation Type Database")]
    public class AnimationTypeDatabase : ScriptableObject
    {
        [SerializeField]
        [Tooltip("动画类型列表（编辑器维护）")]
        private List<AnimationTypeItem> _items = new List<AnimationTypeItem>();
        
        // 只读视图缓存
        private IReadOnlyList<AnimationTypeItem> _itemsReadOnly;
        
        // 基于 Key 的快速查找缓存（延迟构建）
        [System.NonSerialized]
        private Dictionary<string, AnimationTypeItem> _dictByKey;
        
        /// <summary>
        /// 可写列表（用于编辑器维护）
        /// </summary>
        public List<AnimationTypeItem> Items => _items;
        
        /// <summary>
        /// 运行期只读视图
        /// </summary>
        public IReadOnlyList<AnimationTypeItem> ItemsReadOnly
        {
            get
            {
                if (_itemsReadOnly == null)
                    _itemsReadOnly = _items;
                return _itemsReadOnly;
            }
        }
        
        /// <summary>
        /// 元素数量
        /// </summary>
        public int Count => _items.Count;
        
        /// <summary>
        /// 索引访问
        /// </summary>
        public AnimationTypeItem this[int index] => _items[index];
        
        /// <summary>
        /// 重建缓存
        /// </summary>
        public void RebuildCache()
        {
            _dictByKey = null;
        }
        
        private void EnsureKeyDict()
        {
            if (_dictByKey != null) return;
            
            _dictByKey = new Dictionary<string, AnimationTypeItem>();
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                if (item == null) continue;
                
                var key = item.name;
                if (!string.IsNullOrEmpty(key) && !_dictByKey.ContainsKey(key))
                {
                    _dictByKey.Add(key, item);
                }
            }
        }
        
        /// <summary>
        /// 通过 Key 尝试获取元素（O(1)）
        /// </summary>
        public bool TryGet(string key, out AnimationTypeItem item)
        {
            EnsureKeyDict();
            return _dictByKey.TryGetValue(key, out item);
        }
        
        /// <summary>
        /// 通过 Key 获取元素，未命中返回 null
        /// </summary>
        public AnimationTypeItem GetByKey(string key)
        {
            EnsureKeyDict();
            _dictByKey.TryGetValue(key, out var item);
            return item;
        }
        
        /// <summary>
        /// 带边界检查的下标访问
        /// </summary>
        public bool TryGetByIndex(int index, out AnimationTypeItem item)
        {
            if (index >= 0 && index < _items.Count)
            {
                item = _items[index];
                return item != null;
            }
            item = null;
            return false;
        }
        
        /// <summary>
        /// 获取元素索引
        /// </summary>
        public int IndexOf(AnimationTypeItem item)
        {
            return _items.IndexOf(item);
        }
        
        /// <summary>
        /// 检查是否包含指定元素
        /// </summary>
        public bool Contains(AnimationTypeItem item)
        {
            return _items.Contains(item);
        }
        
        /// <summary>
        /// 获取所有名称数组（用于编辑器下拉）
        /// </summary>
        public string[] GetAllDisplayNames()
        {
            var names = new string[_items.Count];
            for (int i = 0; i < _items.Count; i++)
            {
                names[i] = _items[i] != null ? _items[i].name : "";
            }
            return names;
        }
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            RebuildCache();
        }
        
        /// <summary>
        /// 编辑器：添加项目（自动去重）
        /// </summary>
        public void EditorAddItem(AnimationTypeItem item)
        {
            if (item != null && !_items.Contains(item))
            {
                _items.Add(item);
                RebuildCache();
            }
        }
        
        /// <summary>
        /// 编辑器：移除项目
        /// </summary>
        public void EditorRemoveItem(AnimationTypeItem item)
        {
            if (_items.Remove(item))
            {
                RebuildCache();
            }
        }
#endif
    }
}
