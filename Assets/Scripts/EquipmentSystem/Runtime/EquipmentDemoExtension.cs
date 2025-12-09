using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EquipmentSystem
{
    /// <summary>
    /// 装备与动画测试工具
    /// 自动检测场景中激活的 EquipmentRenderer
    /// 提供装备切换和动画控制 UI
    /// </summary>
    public class EquipmentDemoExtension : MonoBehaviour
    {
        [Header("装备库")]
        public List<EquipmentRenderData> availableEquipments = new List<EquipmentRenderData>();
        
        [Header("外观库")]
        public List<CharacterAppearance> availableAppearances = new List<CharacterAppearance>();
        
        [Header("UI 设置")]
        public float panelWidth = 200f;
        public float panelMargin = 10f;
        
        // 当前选中的角色
        EquipmentRenderer _currentEquipRenderer;
        AnimationController _currentAnimController;
        
        // 按类型分组的装备
        Dictionary<EquipmentType, List<EquipmentRenderData>> _equipmentsByType;
        Dictionary<EquipmentType, int> _selectedIndex;  // 每个类型的当前选择 (0=无)
        
        // 武器分组（主手/副手）
        List<EquipmentRenderData> _mainHandWeapons = new List<EquipmentRenderData>();
        List<EquipmentRenderData> _offHandWeapons = new List<EquipmentRenderData>();
        int _selectedMainHandIndex = 0;  // 0 = 无
        int _selectedOffHandIndex = 0;   // 0 = 无
        
        // 外观选择
        int _selectedAppearanceIndex = 0;  // 0 = 无
        
        void Start()
        {
            // 按类型分组装备
            _equipmentsByType = new Dictionary<EquipmentType, List<EquipmentRenderData>>();
            _selectedIndex = new Dictionary<EquipmentType, int>();
            
            foreach (EquipmentType type in System.Enum.GetValues(typeof(EquipmentType)))
            {
                _equipmentsByType[type] = availableEquipments.Where(e => e != null && e.type == type).ToList();
                _selectedIndex[type] = 0;  // 0 = 无
            }
            
            // 武器按槽位类型分组
            var weapons = _equipmentsByType[EquipmentType.Weapon];
            _mainHandWeapons = weapons.Where(w => 
                w.weaponSlotType == WeaponSlotType.MainHand ||
                w.weaponSlotType == WeaponSlotType.TwoHand ||
                w.weaponSlotType == WeaponSlotType.DualWield).ToList();
            _offHandWeapons = weapons.Where(w => w.weaponSlotType == WeaponSlotType.OffHand).ToList();
        }
        
        void Update()
        {
            // 在场景中查找当前激活的 EquipmentRenderer
            if (_currentEquipRenderer == null || !_currentEquipRenderer.gameObject.activeInHierarchy)
            {
                // 查找场景中激活的 EquipmentRenderer
                var renderer = FindFirstObjectByType<EquipmentRenderer>(FindObjectsInactive.Exclude);
                if (renderer != null && renderer != _currentEquipRenderer)
                {
                    SelectRenderer(renderer);
                }
            }
        }
        
        /// <summary>
        /// 设置目标角色（供外部调用）
        /// </summary>
        public void SetTarget(GameObject target)
        {
            if (target == null) return;
            
            var renderer = target.GetComponent<EquipmentRenderer>()
                        ?? target.GetComponentInParent<EquipmentRenderer>()
                        ?? target.GetComponentInChildren<EquipmentRenderer>();
            
            if (renderer != null)
                SelectRenderer(renderer);
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
            
            // 根据当前穿戴的装备同步下拉框选择（跳过武器，单独处理）
            foreach (var type in _equipmentsByType.Keys)
            {
                if (type == EquipmentType.Weapon) continue;
                
                _selectedIndex[type] = 0;
                var list = _equipmentsByType[type];
                for (int i = 0; i < list.Count; i++)
                {
                    var equipped = _currentEquipRenderer.GetEquipped(type);
                    if (equipped == list[i])
                    {
                        _selectedIndex[type] = i + 1;
                        break;
                    }
                }
            }
            
            // 同步主手武器选择
            _selectedMainHandIndex = 0;
            var mainHand = _currentEquipRenderer.GetMainHandWeapon();
            if (mainHand != null)
            {
                int idx = _mainHandWeapons.IndexOf(mainHand);
                if (idx >= 0) _selectedMainHandIndex = idx + 1;
            }
            
            // 同步副手武器选择
            _selectedOffHandIndex = 0;
            var offHand = _currentEquipRenderer.GetOffHandWeapon();
            if (offHand != null)
            {
                int idx = _offHandWeapons.IndexOf(offHand);
                if (idx >= 0) _selectedOffHandIndex = idx + 1;
            }
            
            // 同步外观选择
            _selectedAppearanceIndex = 0;
            if (_currentEquipRenderer.appearance != null)
            {
                for (int i = 0; i < availableAppearances.Count; i++)
                {
                    if (availableAppearances[i] == _currentEquipRenderer.appearance)
                    {
                        _selectedAppearanceIndex = i + 1;
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
        
        // 方向名称
        static readonly string[] DirectionNames = { "SE", "SW", "NE", "NW" };
        
        void OnGUI()
        {
            if (_equipmentsByType == null) return;
            
            float lineHeight = 28f;
            float labelWidth = 50f;
            float dropdownWidth = panelWidth - labelWidth - 5f;
            float spacing = 8f;
            
            // 计算面板高度 (角色显示 + 装备 + 武器 + 动画控制)
            int typeCount = 0;
            foreach (EquipmentType type in System.Enum.GetValues(typeof(EquipmentType)))
            {
                if (type == EquipmentType.Weapon) continue; // 武器单独计算
                if (_equipmentsByType[type].Count > 0) typeCount++;
            }
            // 主手/副手武器行数
            if (_mainHandWeapons.Count > 0) typeCount++;
            if (_offHandWeapons.Count > 0) typeCount++;
            
            // 额外: 角色显示 + 分隔 + 动画标题 + 动画下拉 + 方向下拉 + 阴影开关
            float charDisplayHeight = lineHeight + spacing;
            float animSectionHeight = 15 + 30 + (lineHeight + spacing) * 2 + 30;
            float appearanceHeight = availableAppearances.Count > 0 ? (lineHeight + spacing) : 0;
            float panelHeight = charDisplayHeight + 45 + typeCount * (lineHeight + spacing) + appearanceHeight + 40 + animSectionHeight;
            
            // 右侧居中
            float x = Screen.width - panelWidth - panelMargin;
            float y = (Screen.height - panelHeight) / 2f;
            
            // 收集所有下拉框位置
            var dropdownRects = new List<(EquipmentType type, Rect rect, string[] options)>();
            var animDropdownRects = new List<(int id, Rect rect, string[] options, int selected)>();
            Rect appearanceDropdownRect = Rect.zero;
            string[] appearanceOptions = null;
            
            // 背景
            GUI.Box(new Rect(x - 10, y - 10, panelWidth + 20, panelHeight + 20), "", GetBoxStyle());
            
            // 当前角色显示（只读）
            bool hasSelection = _currentEquipRenderer != null;
            bool anyDropdownOpen = _openDropdown != null || _openAnimDropdown != 0;
            
            GUI.Label(new Rect(x, y + 4, labelWidth, lineHeight), "角色:", GetLabelStyle());
            string charLabel = hasSelection ? _currentEquipRenderer.gameObject.name : "(选中场景对象)";
            GUI.Label(new Rect(x + labelWidth, y + 4, dropdownWidth, lineHeight), charLabel, GetCharNameStyle());
            y += charDisplayHeight;
            
            GUI.Label(new Rect(x, y, panelWidth, 30), "装备预览", GetTitleStyle());
            y += 35;
            
            // 如果有下拉框打开，禁用其他控件
            bool hasOpenDropdown = _openDropdown != null;
            anyDropdownOpen = _openDropdown != null || _openAnimDropdown != 0 || _openAppearanceDropdown 
                || _openMainHandDropdown || _openOffHandDropdown;
            
            // 非武器类型的装备下拉框
            foreach (EquipmentType type in System.Enum.GetValues(typeof(EquipmentType)))
            {
                if (type == EquipmentType.Weapon) continue; // 武器单独处理
                
                var list = _equipmentsByType[type];
                if (list.Count == 0) continue;
                
                GUI.Label(new Rect(x, y + 4, labelWidth, lineHeight), GetTypeName(type), GetLabelStyle());
                
                var options = new List<string> { "(无)" };
                options.AddRange(list.Select(e => e.name));
                
                Rect dropRect = new Rect(x + labelWidth, y, dropdownWidth, lineHeight);
                dropdownRects.Add((type, dropRect, options.ToArray()));
                
                int selected = _selectedIndex[type];
                string label = selected >= 0 && selected < options.Count ? options[selected] : "(无)";
                
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
            
            // ========== 主手武器下拉框 ==========
            if (_mainHandWeapons.Count > 0)
            {
                GUI.Label(new Rect(x, y + 4, labelWidth, lineHeight), "主手:", GetLabelStyle());
                
                var mainOptions = new List<string> { "(无)" };
                mainOptions.AddRange(_mainHandWeapons.Select(e => e.name));
                
                Rect mainRect = new Rect(x + labelWidth, y, dropdownWidth, lineHeight);
                string mainLabel = _selectedMainHandIndex >= 0 && _selectedMainHandIndex < mainOptions.Count 
                    ? mainOptions[_selectedMainHandIndex] : "(无)";
                
                GUI.enabled = hasSelection && (!anyDropdownOpen || _openMainHandDropdown);
                
                if (GUI.Button(mainRect, mainLabel, GetDropdownStyle()))
                {
                    _openMainHandDropdown = !_openMainHandDropdown;
                    if (_openMainHandDropdown) { _openDropdown = null; _openOffHandDropdown = false; _openAnimDropdown = 0; _openAppearanceDropdown = false; }
                }
                
                y += lineHeight + spacing;
            }
            
            // ========== 副手武器下拉框 ==========
            if (_offHandWeapons.Count > 0)
            {
                GUI.Label(new Rect(x, y + 4, labelWidth, lineHeight), "副手:", GetLabelStyle());
                
                var offOptions = new List<string> { "(无)" };
                offOptions.AddRange(_offHandWeapons.Select(e => e.name));
                
                Rect offRect = new Rect(x + labelWidth, y, dropdownWidth, lineHeight);
                string offLabel = _selectedOffHandIndex >= 0 && _selectedOffHandIndex < offOptions.Count 
                    ? offOptions[_selectedOffHandIndex] : "(无)";
                
                // 检查是否允许副手
                bool canEquipOffHand = _currentEquipRenderer == null || _currentEquipRenderer.CanEquipOffHand();
                GUI.enabled = hasSelection && canEquipOffHand && (!anyDropdownOpen || _openOffHandDropdown);
                
                if (!canEquipOffHand)
                    GUI.color = new Color(0.7f, 0.7f, 0.7f);
                
                if (GUI.Button(offRect, canEquipOffHand ? offLabel : "(禁用)", GetDropdownStyle()))
                {
                    if (canEquipOffHand)
                    {
                        _openOffHandDropdown = !_openOffHandDropdown;
                        if (_openOffHandDropdown) { _openDropdown = null; _openMainHandDropdown = false; _openAnimDropdown = 0; _openAppearanceDropdown = false; }
                    }
                }
                
                GUI.color = Color.white;
                y += lineHeight + spacing;
            }
            
            y += 5;
            
            // 外观下拉框
            if (availableAppearances.Count > 0)
            {
                GUI.Label(new Rect(x, y + 4, labelWidth, lineHeight), "外观:", GetLabelStyle());
                
                var appOptions = new List<string> { "(无)" };
                appOptions.AddRange(availableAppearances.Where(a => a != null).Select(a => a.name));
                appearanceOptions = appOptions.ToArray();
                
                appearanceDropdownRect = new Rect(x + labelWidth, y, dropdownWidth, lineHeight);
                
                string appLabel = _selectedAppearanceIndex >= 0 && _selectedAppearanceIndex < appearanceOptions.Length 
                    ? appearanceOptions[_selectedAppearanceIndex] : "(无)";
                
                GUI.enabled = hasSelection && (!anyDropdownOpen || _openAppearanceDropdown);
                
                if (GUI.Button(appearanceDropdownRect, appLabel, GetDropdownStyle()))
                {
                    _openAppearanceDropdown = !_openAppearanceDropdown;
                    if (_openAppearanceDropdown)
                    {
                        _openDropdown = null;
                        _openAnimDropdown = 0;
                    }
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
            string[] animOptions = GetAnimationDisplayNames(_currentAnimController);
            string[] dirOptions = _currentAnimController?.GetDirectionNames() ?? DirectionNames;

            // 数字键快捷切换动画（1~9）
            HandleAnimHotkeys(animOptions);
            
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
            
            // 绘制外观下拉列表
            if (_openAppearanceDropdown && appearanceOptions != null)
            {
                int newIndex = DrawDropdownList(appearanceDropdownRect, _selectedAppearanceIndex, appearanceOptions);
                if (newIndex != _selectedAppearanceIndex)
                {
                    OnAppearanceChanged(_selectedAppearanceIndex, newIndex);
                    _selectedAppearanceIndex = newIndex;
                    _openAppearanceDropdown = false;
                }
            }
            
            // 绘制主手武器下拉列表
            if (_openMainHandDropdown && _mainHandWeapons.Count > 0)
            {
                var mainOptions = new List<string> { "(无)" };
                mainOptions.AddRange(_mainHandWeapons.Select(e => e.name));
                Rect mainRect = new Rect(x + labelWidth, 0, dropdownWidth, lineHeight); // y 会在 DrawDropdownList 里重新计算
                
                // 找到主手下拉框的实际位置
                float mainY = (Screen.height - panelHeight) / 2f + charDisplayHeight + 35;
                foreach (EquipmentType t in System.Enum.GetValues(typeof(EquipmentType)))
                {
                    if (t == EquipmentType.Weapon) continue;
                    if (_equipmentsByType[t].Count > 0) mainY += lineHeight + spacing;
                }
                mainRect.y = mainY;
                
                int newIndex = DrawDropdownList(mainRect, _selectedMainHandIndex, mainOptions.ToArray());
                if (newIndex != _selectedMainHandIndex)
                {
                    OnMainHandChanged(_selectedMainHandIndex, newIndex);
                    _selectedMainHandIndex = newIndex;
                    _openMainHandDropdown = false;
                }
            }
            
            // 绘制副手武器下拉列表
            if (_openOffHandDropdown && _offHandWeapons.Count > 0)
            {
                var offOptions = new List<string> { "(无)" };
                offOptions.AddRange(_offHandWeapons.Select(e => e.name));
                
                // 找到副手下拉框的实际位置
                float offY = (Screen.height - panelHeight) / 2f + charDisplayHeight + 35;
                foreach (EquipmentType t in System.Enum.GetValues(typeof(EquipmentType)))
                {
                    if (t == EquipmentType.Weapon) continue;
                    if (_equipmentsByType[t].Count > 0) offY += lineHeight + spacing;
                }
                if (_mainHandWeapons.Count > 0) offY += lineHeight + spacing;
                Rect offRect = new Rect(x + labelWidth, offY, dropdownWidth, lineHeight);
                
                int newIndex = DrawDropdownList(offRect, _selectedOffHandIndex, offOptions.ToArray());
                if (newIndex != _selectedOffHandIndex)
                {
                    OnOffHandChanged(_selectedOffHandIndex, newIndex);
                    _selectedOffHandIndex = newIndex;
                    _openOffHandDropdown = false;
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
        
        void OnMainHandChanged(int oldIndex, int newIndex)
        {
            if (_currentEquipRenderer == null) return;
            
            // 卸下旧主手
            if (oldIndex > 0 && oldIndex <= _mainHandWeapons.Count)
            {
                _currentEquipRenderer.Unequip(_mainHandWeapons[oldIndex - 1]);
            }
            
            // 装备新主手
            if (newIndex > 0 && newIndex <= _mainHandWeapons.Count)
            {
                _currentEquipRenderer.Equip(_mainHandWeapons[newIndex - 1]);
            }
            
            // 同步副手选择（可能被禁用）
            SyncOffHandSelection();
        }
        
        void OnOffHandChanged(int oldIndex, int newIndex)
        {
            if (_currentEquipRenderer == null) return;
            
            // 卸下旧副手
            if (oldIndex > 0 && oldIndex <= _offHandWeapons.Count)
            {
                _currentEquipRenderer.Unequip(_offHandWeapons[oldIndex - 1]);
            }
            
            // 装备新副手
            if (newIndex > 0 && newIndex <= _offHandWeapons.Count)
            {
                _currentEquipRenderer.Equip(_offHandWeapons[newIndex - 1]);
            }
        }
        
        void SyncOffHandSelection()
        {
            // 如果当前主手禁止副手，清空副手选择
            if (_currentEquipRenderer != null && !_currentEquipRenderer.CanEquipOffHand())
            {
                if (_selectedOffHandIndex > 0)
                {
                    _selectedOffHandIndex = 0;
                }
            }
        }
        
        void UnequipAll()
        {
            if (_currentEquipRenderer == null) return;
            
            _currentEquipRenderer.UnequipAll();
            
            foreach (var type in _selectedIndex.Keys.ToList())
                _selectedIndex[type] = 0;
            
            _selectedMainHandIndex = 0;
            _selectedOffHandIndex = 0;
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
            return EquipTypeRegistry.GetDisplayName(type);
        }
        
        // 当前打开的下拉框
        EquipmentType? _openDropdown = null;
        bool _openAppearanceDropdown = false;
        bool _openMainHandDropdown = false;
        bool _openOffHandDropdown = false;
        
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
        
        /// <summary>
        /// 从 AnimationController 获取动画显示名数组
        /// </summary>
        string[] GetAnimationDisplayNames(AnimationController controller)
        {
            if (controller == null || controller.animDatabase == null)
                return System.Array.Empty<string>();
            
            return controller.animDatabase.GetAllDisplayNames();
        }

        /// <summary>
        /// 数字键快捷切换动画（1~9 映射到索引 0~8）
        /// </summary>
        void HandleAnimHotkeys(string[] animOptions)
        {
            var e = Event.current;
            if (e == null || e.type != EventType.KeyDown)
                return;

            if (_currentAnimController == null || animOptions == null || animOptions.Length == 0)
                return;

            // 任意下拉框打开时，不响应快捷键，避免干扰选择
            if (_openAnimDropdown != 0 || _openDropdown != null || _openAppearanceDropdown || _openMainHandDropdown || _openOffHandDropdown)
                return;

            int targetIndex = -1;
            switch (e.keyCode)
            {
                case KeyCode.Alpha1:
                case KeyCode.Keypad1: targetIndex = 0; break;
                case KeyCode.Alpha2:
                case KeyCode.Keypad2: targetIndex = 1; break;
                case KeyCode.Alpha3:
                case KeyCode.Keypad3: targetIndex = 2; break;
                case KeyCode.Alpha4:
                case KeyCode.Keypad4: targetIndex = 3; break;
                case KeyCode.Alpha5:
                case KeyCode.Keypad5: targetIndex = 4; break;
                case KeyCode.Alpha6:
                case KeyCode.Keypad6: targetIndex = 5; break;
                case KeyCode.Alpha7:
                case KeyCode.Keypad7: targetIndex = 6; break;
                case KeyCode.Alpha8:
                case KeyCode.Keypad8: targetIndex = 7; break;
                case KeyCode.Alpha9:
                case KeyCode.Keypad9: targetIndex = 8; break;
                default:
                    return;
            }

            if (targetIndex < 0 || targetIndex >= animOptions.Length)
                return;

            _selectedAnimIndex = targetIndex;
            ApplyAnimation(targetIndex);
            e.Use();
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
            _openAppearanceDropdown = false;
        }
        
        void OnAppearanceChanged(int oldIndex, int newIndex)
        {
            if (_currentEquipRenderer == null) return;
            
            CharacterAppearance newAppearance = null;
            if (newIndex > 0 && newIndex <= availableAppearances.Count)
            {
                newAppearance = availableAppearances[newIndex - 1];
            }
            
            _currentEquipRenderer.SetAppearance(newAppearance);
        }
    }
}
