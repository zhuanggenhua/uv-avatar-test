using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using EquipmentSystem.Data;

namespace EquipmentSystem.Runtime
{
    /// <summary>
    /// 装备与动画测试工具
    /// 自动查找场景中的 EquipmentRenderer 并提供控制 UI
    /// 多个角色时可通过下拉框切换
    /// </summary>
    public class EquipmentDemoExtension : MonoBehaviour
    {
        [Header("装备库")]
        public List<EquipmentData> availableEquipments = new List<EquipmentData>();
        
        [Header("UI 设置")]
        public float panelWidth = 200f;
        public float panelMargin = 10f;
        
        // 场景中所有的 EquipmentRenderer
        List<EquipmentRenderer> _allRenderers = new List<EquipmentRenderer>();
        int _selectedRendererIndex = 0;
        
        // 当前选中的角色
        EquipmentRenderer _currentEquipRenderer;
        AnimationController _currentAnimController;
        
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
            
            RefreshRendererList();
        }
        
        /// <summary>
        /// 刷新场景中的 EquipmentRenderer 列表
        /// </summary>
        public void RefreshRendererList()
        {
            // 包含 inactive 对象
            _allRenderers = FindObjectsByType<EquipmentRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None).ToList();
            
            if (_allRenderers.Count > 0)
            {
                _selectedRendererIndex = 0;
                SelectRenderer(_allRenderers[0]);
            }
            else
            {
                _currentEquipRenderer = null;
                _currentAnimController = null;
            }
        }
        
        void SelectRenderer(EquipmentRenderer renderer)
        {
            if (renderer == _currentEquipRenderer) return;
            
            _currentEquipRenderer = renderer;
            _currentAnimController = renderer != null ? renderer.GetComponent<AnimationController>() : null;
            
            SyncSelectionFromEquipped();
            _selectedAnimIndex = 0;
            _selectedDirIndex = 0;
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
        
        // 动画控制状态
        int _selectedAnimIndex = 0;
        int _selectedDirIndex = 0;
        bool _shadowEnabled = true;
        int _openAnimDropdown = 0;  // 0=无, 1=动画, 2=方向
        bool _openCharDropdown = false;
        
        // 方向名称
        static readonly string[] DirectionNames = { "SE", "SW", "NE", "NW" };
        
        void OnGUI()
        {
            if (_equipmentsByType == null) return;
            
            float lineHeight = 28f;
            float labelWidth = 50f;
            float dropdownWidth = panelWidth - labelWidth - 5f;
            float spacing = 8f;
            
            // 计算面板高度 (角色选择 + 装备 + 动画控制)
            int typeCount = 0;
            foreach (EquipmentType type in System.Enum.GetValues(typeof(EquipmentType)))
                if (_equipmentsByType[type].Count > 0) typeCount++;
            
            // 额外: 角色选择 + 分隔 + 动画标题 + 动画下拉 + 方向下拉 + 阴影开关
            float charSelectHeight = lineHeight + spacing + 10;
            float animSectionHeight = 15 + 30 + (lineHeight + spacing) * 2 + 30;
            float panelHeight = charSelectHeight + 45 + typeCount * (lineHeight + spacing) + 40 + animSectionHeight;
            
            // 右侧居中
            float x = Screen.width - panelWidth - panelMargin;
            float y = (Screen.height - panelHeight) / 2f;
            
            // 收集所有下拉框位置
            var dropdownRects = new List<(EquipmentType type, Rect rect, string[] options)>();
            var animDropdownRects = new List<(int id, Rect rect, string[] options, int selected)>();
            Rect charDropdownRect = Rect.zero;
            
            // 背景
            GUI.Box(new Rect(x - 10, y - 10, panelWidth + 20, panelHeight + 20), "", GetBoxStyle());
            
            // 角色选择下拉框
            bool hasSelection = _currentEquipRenderer != null;
            bool anyDropdownOpen = _openDropdown != null || _openAnimDropdown != 0 || _openCharDropdown;
            
            GUI.Label(new Rect(x, y + 4, labelWidth, lineHeight), "角色:", GetLabelStyle());
            charDropdownRect = new Rect(x + labelWidth, y, dropdownWidth - 35, lineHeight);
            string charLabel = hasSelection ? _currentEquipRenderer.gameObject.name : "(无)";
            GUI.enabled = !anyDropdownOpen || _openCharDropdown;
            if (GUI.Button(charDropdownRect, charLabel, GetDropdownStyle()))
            {
                _openCharDropdown = !_openCharDropdown;
            }
            // 刷新按钮
            GUI.enabled = !anyDropdownOpen;
            if (GUI.Button(new Rect(x + labelWidth + dropdownWidth - 30, y, 30, lineHeight), "↻"))
            {
                RefreshRendererList();
            }
            y += charSelectHeight;
            
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
                GUI.enabled = hasSelection && (!anyDropdownOpen || _openDropdown == type);
                
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
            GUI.enabled = hasSelection && !anyDropdownOpen;
            if (GUI.Button(new Rect(x, y, panelWidth, 30), "卸下全部", GetButtonStyle()))
            {
                UnequipAll();
            }
            y += 40;
            
            // ===== 动画控制区域 =====
            GUI.enabled = true;
            GUI.Label(new Rect(x, y, panelWidth, 25), "动画控制", GetTitleStyle());
            y += 30;
            
            // 获取动画选项
            string[] animOptions = _currentAnimController?.animationNames ?? System.Array.Empty<string>();
            string[] dirOptions = _currentAnimController?.GetDirectionNames() ?? DirectionNames;
            
            bool hasAnimDropdownOpen = _openAnimDropdown != 0;
            
            // 动画下拉框
            GUI.enabled = hasSelection && (!anyDropdownOpen || _openAnimDropdown == 1);
            GUI.Label(new Rect(x, y + 4, labelWidth, lineHeight), "动画:", GetLabelStyle());
            Rect animRect = new Rect(x + labelWidth, y, dropdownWidth, lineHeight);
            string animLabel = _selectedAnimIndex < animOptions.Length ? animOptions[_selectedAnimIndex] : "---";
            if (GUI.Button(animRect, animLabel, GetDropdownStyle()))
            {
                _openAnimDropdown = _openAnimDropdown == 1 ? 0 : 1;
            }
            animDropdownRects.Add((1, animRect, animOptions, _selectedAnimIndex));
            y += lineHeight + spacing;
            
            // 方向下拉框
            GUI.enabled = hasSelection && (!anyDropdownOpen || _openAnimDropdown == 2);
            GUI.Label(new Rect(x, y + 4, labelWidth, lineHeight), "方向:", GetLabelStyle());
            Rect dirRect = new Rect(x + labelWidth, y, dropdownWidth, lineHeight);
            string dirLabel = _selectedDirIndex < dirOptions.Length ? dirOptions[_selectedDirIndex] : "---";
            if (GUI.Button(dirRect, dirLabel, GetDropdownStyle()))
            {
                _openAnimDropdown = _openAnimDropdown == 2 ? 0 : 2;
            }
            animDropdownRects.Add((2, dirRect, dirOptions, _selectedDirIndex));
            y += lineHeight + spacing;
            
            // 阴影开关 (寻找 Shadow 子对象)
            GUI.enabled = hasSelection && !anyDropdownOpen;
            bool newShadow = GUI.Toggle(new Rect(x, y, panelWidth, 25), _shadowEnabled, " 显示阴影");
            if (newShadow != _shadowEnabled)
            {
                _shadowEnabled = newShadow;
                SetShadowVisible(_shadowEnabled);
            }
            
            GUI.enabled = true;
            
            // 绘制装备下拉列表
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
            
            // 绘制动画下拉列表
            if (_openAnimDropdown != 0)
            {
                foreach (var (id, rect, options, selected) in animDropdownRects)
                {
                    if (id == _openAnimDropdown)
                    {
                        int newIndex = DrawDropdownList(rect, selected, options);
                        if (newIndex != selected)
                        {
                            if (id == 1)
                            {
                                _selectedAnimIndex = newIndex;
                                ApplyAnimation(newIndex);
                            }
                            else if (id == 2)
                            {
                                _selectedDirIndex = newIndex;
                                ApplyDirection(newIndex);
                            }
                            _openAnimDropdown = 0;
                        }
                        break;
                    }
                }
            }
            
            // 绘制角色下拉列表
            if (_openCharDropdown && _allRenderers.Count > 0)
            {
                var charOptions = _allRenderers.Select(r => r.gameObject.name).ToArray();
                int newIndex = DrawDropdownList(charDropdownRect, _selectedRendererIndex, charOptions);
                if (newIndex != _selectedRendererIndex)
                {
                    _selectedRendererIndex = newIndex;
                    SelectRenderer(_allRenderers[newIndex]);
                    _openCharDropdown = false;
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
        
        void ApplyAnimation(int index)
        {
            if (_currentAnimController != null)
                _currentAnimController.SetAnimation(index);
        }
        
        void ApplyDirection(int index)
        {
            if (_currentAnimController != null)
                _currentAnimController.SetDirection(index);
        }
        
        void SetShadowVisible(bool visible)
        {
            if (_currentEquipRenderer == null) return;
            
            // 使用 AnimationController 控制阴影
            if (_currentAnimController != null)
            {
                _currentAnimController.SetShadowEnabled(visible);
            }
        }
        
        string GetTypeName(EquipmentType type)
        {
            switch (type)
            {
                case EquipmentType.Weapon: return "武器";
                case EquipmentType.Clothing: return "服装";
                case EquipmentType.Helmet: return "头盔";
                case EquipmentType.Gloves: return "手套";
                case EquipmentType.Shoes: return "鞋子";
                default: return type.ToString();
            }
        }
        
        // 当前打开的下拉框
        EquipmentType? _openDropdown = null;
        
        // 样式缓存
        GUIStyle _titleStyle, _labelStyle, _boxStyle, _dropdownStyle, _buttonStyle, _listItemStyle, _charNameStyle;
        
        GUIStyle GetCharNameStyle()
        {
            if (_charNameStyle == null)
            {
                _charNameStyle = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold,
                    fontSize = 14,
                    alignment = TextAnchor.MiddleCenter
                };
                _charNameStyle.normal.textColor = new Color(0.8f, 0.9f, 1f);
            }
            return _charNameStyle;
        }
        
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
                    CloseAllDropdowns();
                }
            }
            
            return result;
        }
        
        void CloseAllDropdowns()
        {
            _openDropdown = null;
            _openAnimDropdown = 0;
            _openCharDropdown = false;
        }
    }
}
