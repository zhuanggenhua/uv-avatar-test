using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using EquipmentSystem.Data;

namespace EquipmentSystem.Runtime
{
    /// <summary>
    /// 装备系统测试扩展
    /// 挂在角色容器上（子对象是多个角色预制体）
    /// 自动检测当前激活的角色并管理装备
    /// </summary>
    public class EquipmentDemoExtension : MonoBehaviour
    {
        [Header("装备库")]
        public List<EquipmentData> availableEquipments = new List<EquipmentData>();
        
        [Header("UI 设置")]
        public float panelWidth = 200f;
        public float panelMargin = 10f;
        
        EquipmentRenderer _currentEquipRenderer;
        GameObject _lastCharacter;
        
        // 按类型分组的装备
        Dictionary<EquipmentType, List<EquipmentData>> _equipmentsByType;
        Dictionary<EquipmentType, int> _selectedIndex;  // 每个类型的当前选择 (0=无)
        
        void Start()
        {
            // 按类型分组装备
            _equipmentsByType = new Dictionary<EquipmentType, List<EquipmentData>>();
            _selectedIndex = new Dictionary<EquipmentType, int>();
            
            foreach (EquipmentType type in System.Enum.GetValues(typeof(EquipmentType)))
            {
                _equipmentsByType[type] = availableEquipments.Where(e => e != null && e.type == type).ToList();
                _selectedIndex[type] = 0;  // 0 = 无
            }
        }
        
        void LateUpdate()
        {
            CheckCharacterChange();
        }
        
        void CheckCharacterChange()
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i).gameObject;
                if (child.activeSelf && child != _lastCharacter)
                {
                    _lastCharacter = child;
                    _currentEquipRenderer = child.GetComponentInChildren<EquipmentRenderer>();
                    SyncSelectionFromEquipped();
                    break;
                }
            }
        }
        
        void SyncSelectionFromEquipped()
        {
            if (_currentEquipRenderer == null) return;
            
            // 根据当前穿戴的装备同步下拉框选择
            foreach (var type in _equipmentsByType.Keys)
            {
                _selectedIndex[type] = 0;
                var list = _equipmentsByType[type];
                for (int i = 0; i < list.Count; i++)
                {
                    if (_currentEquipRenderer.equipments.Contains(list[i]))
                    {
                        _selectedIndex[type] = i + 1;  // +1 因为 0 是 "无"
                        break;
                    }
                }
            }
        }
        
        void OnGUI()
        {
            if (_equipmentsByType == null) return;
            
            float lineHeight = 28f;
            float labelWidth = 50f;
            float dropdownWidth = panelWidth - labelWidth - 5f;
            float spacing = 8f;
            
            // 计算面板高度
            int typeCount = 0;
            foreach (EquipmentType type in System.Enum.GetValues(typeof(EquipmentType)))
                if (_equipmentsByType[type].Count > 0) typeCount++;
            
            float panelHeight = 45 + typeCount * (lineHeight + spacing) + 40;
            
            // 右侧居中
            float x = Screen.width - panelWidth - panelMargin;
            float y = (Screen.height - panelHeight) / 2f;
            
            // 收集所有下拉框位置
            var dropdownRects = new List<(EquipmentType type, Rect rect, string[] options)>();
            
            // 背景
            GUI.Box(new Rect(x - 10, y - 10, panelWidth + 20, panelHeight + 20), "", GetBoxStyle());
            
            GUI.Label(new Rect(x, y, panelWidth, 30), "装备预览", GetTitleStyle());
            y += 35;
            
            // 如果有下拉框打开，禁用其他控件
            bool hasOpenDropdown = _openDropdown != null;
            
            foreach (EquipmentType type in System.Enum.GetValues(typeof(EquipmentType)))
            {
                var list = _equipmentsByType[type];
                if (list.Count == 0) continue;
                
                // 标签
                GUI.Label(new Rect(x, y + 4, labelWidth, lineHeight), GetTypeName(type), GetLabelStyle());
                
                // 下拉框选项
                var options = new List<string> { "(无)" };
                options.AddRange(list.Select(e => e.name));
                
                Rect dropRect = new Rect(x + labelWidth, y, dropdownWidth, lineHeight);
                dropdownRects.Add((type, dropRect, options.ToArray()));
                
                // 绘制下拉框按钮
                int selected = _selectedIndex[type];
                string label = selected >= 0 && selected < options.Count ? options[selected] : "(无)";
                
                // 当有下拉框打开时，只有当前打开的那个可以点击
                GUI.enabled = !hasOpenDropdown || _openDropdown == type;
                
                if (GUI.Button(dropRect, label, GetDropdownStyle()))
                {
                    if (_openDropdown == type)
                        _openDropdown = null;
                    else
                        _openDropdown = type;
                }
                
                y += lineHeight + spacing;
            }
            
            y += 5;
            
            // 全部卸下按钮
            GUI.enabled = !hasOpenDropdown;
            if (GUI.Button(new Rect(x, y, panelWidth, 30), "卸下全部", GetButtonStyle()))
            {
                UnequipAll();
            }
            GUI.enabled = true;
            
            // 最后绘制展开的下拉列表
            if (_openDropdown != null)
            {
                foreach (var (type, rect, options) in dropdownRects)
                {
                    if (type == _openDropdown)
                    {
                        int newIndex = DrawDropdownList(rect, _selectedIndex[type], options);
                        if (newIndex != _selectedIndex[type])
                        {
                            OnSelectionChanged(type, _selectedIndex[type], newIndex);
                            _selectedIndex[type] = newIndex;
                            _openDropdown = null;
                        }
                        break;
                    }
                }
            }
        }
        
        void OnSelectionChanged(EquipmentType type, int oldIndex, int newIndex)
        {
            if (_currentEquipRenderer == null) return;
            
            var list = _equipmentsByType[type];
            
            // 卸下旧装备
            if (oldIndex > 0 && oldIndex <= list.Count)
            {
                _currentEquipRenderer.Unequip(list[oldIndex - 1]);
            }
            
            // 穿上新装备
            if (newIndex > 0 && newIndex <= list.Count)
            {
                _currentEquipRenderer.Equip(list[newIndex - 1]);
            }
        }
        
        void UnequipAll()
        {
            if (_currentEquipRenderer == null) return;
            
            foreach (var e in new List<EquipmentData>(_currentEquipRenderer.equipments))
                _currentEquipRenderer.Unequip(e);
            
            foreach (var type in _selectedIndex.Keys.ToList())
                _selectedIndex[type] = 0;
        }
        
        string GetTypeName(EquipmentType type)
        {
            switch (type)
            {
                case EquipmentType.Accessory: return "挂件";
                case EquipmentType.Clothing: return "服装";
                case EquipmentType.Gloves: return "手套";
                case EquipmentType.Shoes: return "鞋子";
                default: return type.ToString();
            }
        }
        
        // 当前打开的下拉框
        EquipmentType? _openDropdown = null;
        
        // 样式缓存
        GUIStyle _titleStyle, _labelStyle, _boxStyle, _dropdownStyle, _buttonStyle, _listItemStyle;
        
        GUIStyle GetTitleStyle()
        {
            if (_titleStyle == null)
            {
                _titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold,
                    fontSize = 16,
                    alignment = TextAnchor.MiddleCenter
                };
            }
            return _titleStyle;
        }
        
        GUIStyle GetLabelStyle()
        {
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    alignment = TextAnchor.MiddleLeft
                };
            }
            return _labelStyle;
        }
        
        GUIStyle GetBoxStyle()
        {
            if (_boxStyle == null)
            {
                _boxStyle = new GUIStyle(GUI.skin.box);
            }
            return _boxStyle;
        }
        
        GUIStyle GetDropdownStyle()
        {
            if (_dropdownStyle == null)
            {
                _dropdownStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 12,
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(8, 20, 4, 4)
                };
            }
            return _dropdownStyle;
        }
        
        GUIStyle GetButtonStyle()
        {
            if (_buttonStyle == null)
            {
                _buttonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 13
                };
            }
            return _buttonStyle;
        }
        
        GUIStyle GetListItemStyle(bool selected)
        {
            if (_listItemStyle == null)
            {
                _listItemStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(8, 8, 2, 2),
                    hover = { textColor = Color.cyan }
                };
            }
            return _listItemStyle;
        }
        
        int DrawDropdownList(Rect buttonRect, int selected, string[] options)
        {
            float itemHeight = 24f;
            float listHeight = options.Length * itemHeight;
            
            // 向下展开
            Rect listRect = new Rect(buttonRect.x, buttonRect.y + buttonRect.height, buttonRect.width, listHeight);
            
            // 确保不超出屏幕底部
            if (listRect.yMax > Screen.height)
            {
                listRect.y = buttonRect.y - listHeight;
            }
            
            // 背景
            GUI.Box(listRect, "", GetBoxStyle());
            
            int result = selected;
            for (int i = 0; i < options.Length; i++)
            {
                Rect itemRect = new Rect(listRect.x, listRect.y + i * itemHeight, listRect.width, itemHeight);
                
                // 高亮当前选中项
                if (i == selected)
                {
                    GUI.color = new Color(0.3f, 0.6f, 0.3f, 0.8f);
                    GUI.Box(itemRect, "");
                    GUI.color = Color.white;
                }
                
                // 悬停效果
                if (itemRect.Contains(Event.current.mousePosition))
                {
                    GUI.color = new Color(0.4f, 0.4f, 0.5f, 0.5f);
                    GUI.Box(itemRect, "");
                    GUI.color = Color.white;
                }
                
                if (GUI.Button(itemRect, options[i], GetListItemStyle(i == selected)))
                {
                    result = i;
                }
            }
            
            // 点击列表外部关闭
            if (Event.current.type == EventType.MouseDown)
            {
                if (!listRect.Contains(Event.current.mousePosition) && 
                    !buttonRect.Contains(Event.current.mousePosition))
                {
                    _openDropdown = null;
                }
            }
            
            return result;
        }
    }
}
