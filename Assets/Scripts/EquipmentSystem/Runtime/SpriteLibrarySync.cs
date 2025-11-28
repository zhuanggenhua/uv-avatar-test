using UnityEngine;
using UnityEngine.U2D.Animation;
using EquipmentSystem.Data;

namespace EquipmentSystem.Runtime
{
    /// <summary>
    /// 将 SpriteLibrary 与 EquipmentRenderer 同步
    /// - 监听 SpriteResolver 的 Category/Label 变化
    /// - 自动更新 EquipmentRenderer 的动画和帧
    /// </summary>
    [RequireComponent(typeof(SpriteResolver))]
    public class SpriteLibrarySync : MonoBehaviour
    {
        [Header("组件引用")]
        public EquipmentRenderer equipmentRenderer;
        
        [Header("配置")]
        [Tooltip("Category 对应动画名称")]
        public bool categoryAsAnimation = true;
        
        [Tooltip("Label 格式: Row_Frame (如 0_0, 1_2)")]
        public bool labelAsRowFrame = true;
        
        SpriteResolver _resolver;
        string _lastCategory;
        string _lastLabel;
        
        void Awake()
        {
            _resolver = GetComponent<SpriteResolver>();
            if (equipmentRenderer == null)
                equipmentRenderer = GetComponent<EquipmentRenderer>();
        }
        
        void LateUpdate()
        {
            if (_resolver == null || equipmentRenderer == null) return;
            
            var category = _resolver.GetCategory();
            var label = _resolver.GetLabel();
            
            // 检查变化
            bool changed = false;
            
            if (category != _lastCategory)
            {
                _lastCategory = category;
                if (categoryAsAnimation && !string.IsNullOrEmpty(category))
                {
                    equipmentRenderer.SetAnimation(category);
                    changed = true;
                }
            }
            
            if (label != _lastLabel)
            {
                _lastLabel = label;
                if (labelAsRowFrame && !string.IsNullOrEmpty(label))
                {
                    // 解析 Label: "Row_Frame" 格式
                    var parts = label.Split('_');
                    if (parts.Length >= 2)
                    {
                        if (int.TryParse(parts[0], out int row))
                            equipmentRenderer.SetRow(row);
                        if (int.TryParse(parts[1], out int frame))
                            equipmentRenderer.SetFrame(frame);
                        changed = true;
                    }
                    else if (int.TryParse(label, out int frameOnly))
                    {
                        // 只有帧号
                        equipmentRenderer.SetFrame(frameOnly);
                        changed = true;
                    }
                }
            }
            
            if (changed)
                equipmentRenderer.Refresh();
        }
        
        /// <summary>
        /// 手动同步
        /// </summary>
        public void Sync()
        {
            if (_resolver == null) return;
            
            _lastCategory = _resolver.GetCategory();
            _lastLabel = _resolver.GetLabel();
            
            if (equipmentRenderer != null)
            {
                if (categoryAsAnimation && !string.IsNullOrEmpty(_lastCategory))
                    equipmentRenderer.SetAnimation(_lastCategory);
                    
                if (labelAsRowFrame && !string.IsNullOrEmpty(_lastLabel))
                {
                    var parts = _lastLabel.Split('_');
                    if (parts.Length >= 2)
                    {
                        if (int.TryParse(parts[0], out int row))
                            equipmentRenderer.SetRow(row);
                        if (int.TryParse(parts[1], out int frame))
                            equipmentRenderer.SetFrame(frame);
                    }
                }
                
                equipmentRenderer.Refresh();
            }
        }
    }
}
