using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using EquipmentSystem.Data;
using Minifantasy.Creatures;

namespace EquipmentSystem.Runtime
{
    /// <summary>
    /// CTR_AnimateCreature 的装备系统扩展
    /// 挂在同一个 GameObject 上，自动获取 CTR_AnimateCreature 并扩展其功能
    /// </summary>
    [RequireComponent(typeof(CTR_AnimateCreature))]
    public class CTR_EquipmentExtension : MonoBehaviour
    {
        [Header("装备库")]
        public List<EquipmentData> availableEquipments = new List<EquipmentData>();
        
        [Header("UI")]
        public Transform equipmentButtonContainer;  // 装备按钮的父对象
        public GameObject equipmentButtonPrefab;    // 装备按钮预制体 (可选，没有则自动创建)
        
        CTR_AnimateCreature _animateCreature;
        EquipmentRenderer _currentEquipRenderer;
        AnimatorEquipmentSync _currentAnimSync;
        
        List<Button> _equipButtons = new List<Button>();
        
        void Awake()
        {
            _animateCreature = GetComponent<CTR_AnimateCreature>();
        }
        
        void Start()
        {
            // 创建装备按钮
            CreateEquipmentButtons();
        }
        
        void CreateEquipmentButtons()
        {
            if (equipmentButtonContainer == null) return;
            
            foreach (var equip in availableEquipments)
            {
                if (equip == null) continue;
                
                GameObject btnObj;
                if (equipmentButtonPrefab != null)
                {
                    btnObj = Instantiate(equipmentButtonPrefab, equipmentButtonContainer);
                }
                else
                {
                    // 自动创建简单按钮
                    btnObj = new GameObject($"Btn_{equip.equipmentId}");
                    btnObj.transform.SetParent(equipmentButtonContainer);
                    btnObj.transform.localScale = Vector3.one;
                    
                    var btn = btnObj.AddComponent<Button>();
                    var image = btnObj.AddComponent<Image>();
                    image.color = new Color(0.3f, 0.3f, 0.3f);
                    
                    // 添加文字
                    var textObj = new GameObject("Text");
                    textObj.transform.SetParent(btnObj.transform);
                    textObj.transform.localScale = Vector3.one;
                    var text = textObj.AddComponent<Text>();
                    text.text = equip.equipmentId;
                    text.alignment = TextAnchor.MiddleCenter;
                    text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    text.fontSize = 14;
                    text.color = Color.white;
                    
                    var textRect = textObj.GetComponent<RectTransform>();
                    textRect.anchorMin = Vector2.zero;
                    textRect.anchorMax = Vector2.one;
                    textRect.offsetMin = Vector2.zero;
                    textRect.offsetMax = Vector2.zero;
                }
                
                var button = btnObj.GetComponent<Button>();
                if (button != null)
                {
                    var capturedEquip = equip;
                    button.onClick.AddListener(() => ToggleEquipment(capturedEquip, button));
                    _equipButtons.Add(button);
                    
                    // 设置按钮文字
                    var txt = btnObj.GetComponentInChildren<Text>();
                    if (txt != null) txt.text = equip.equipmentId;
                }
            }
        }
        
        /// <summary>
        /// 当角色切换时调用（从 CTR_AnimateCreature.UpdateActiveCharacter 后调用）
        /// </summary>
        public void OnCharacterChanged(GameObject character)
        {
            // 获取新角色的装备组件
            _currentEquipRenderer = character.GetComponentInChildren<EquipmentRenderer>();
            _currentAnimSync = character.GetComponentInChildren<AnimatorEquipmentSync>();
            
            // 刷新按钮状态
            RefreshButtonStates();
        }
        
        /// <summary>
        /// 切换装备穿戴状态
        /// </summary>
        public void ToggleEquipment(EquipmentData equip, Button button = null)
        {
            if (_currentEquipRenderer == null || equip == null) return;
            
            bool isEquipped = _currentEquipRenderer.equipments.Contains(equip);
            
            if (isEquipped)
            {
                _currentEquipRenderer.Unequip(equip);
            }
            else
            {
                _currentEquipRenderer.Equip(equip);
            }
            
            // 更新按钮视觉
            if (button != null)
                UpdateButtonVisual(button, !isEquipped);
        }
        
        /// <summary>
        /// 穿上指定装备
        /// </summary>
        public void Equip(EquipmentData equip)
        {
            if (_currentEquipRenderer != null && equip != null)
                _currentEquipRenderer.Equip(equip);
            RefreshButtonStates();
        }
        
        /// <summary>
        /// 卸下指定装备
        /// </summary>
        public void Unequip(EquipmentData equip)
        {
            if (_currentEquipRenderer != null && equip != null)
                _currentEquipRenderer.Unequip(equip);
            RefreshButtonStates();
        }
        
        /// <summary>
        /// 卸下全部装备
        /// </summary>
        public void UnequipAll()
        {
            if (_currentEquipRenderer == null) return;
            
            foreach (var e in new List<EquipmentData>(_currentEquipRenderer.equipments))
                _currentEquipRenderer.Unequip(e);
            
            RefreshButtonStates();
        }
        
        void RefreshButtonStates()
        {
            for (int i = 0; i < _equipButtons.Count && i < availableEquipments.Count; i++)
            {
                var equip = availableEquipments[i];
                var btn = _equipButtons[i];
                
                bool isEquipped = _currentEquipRenderer != null && 
                                  _currentEquipRenderer.equipments.Contains(equip);
                UpdateButtonVisual(btn, isEquipped);
            }
        }
        
        void UpdateButtonVisual(Button btn, bool isEquipped)
        {
            var image = btn.GetComponent<Image>();
            if (image != null)
                image.color = isEquipped ? new Color(0.2f, 0.6f, 0.2f) : new Color(0.3f, 0.3f, 0.3f);
            
            var text = btn.GetComponentInChildren<Text>();
            if (text != null)
            {
                int idx = _equipButtons.IndexOf(btn);
                if (idx >= 0 && idx < availableEquipments.Count)
                {
                    string prefix = isEquipped ? "✓ " : "";
                    text.text = prefix + availableEquipments[idx].equipmentId;
                }
            }
        }
        
        void LateUpdate()
        {
            // 自动检测角色切换
            // CTR_AnimateCreature 通过 UpdateActiveCharacter 切换角色
            // 我们需要检测 activeCharacter 变化
            CheckCharacterChange();
        }
        
        GameObject _lastCharacter;
        void CheckCharacterChange()
        {
            // 遍历子对象找到激活的角色
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i).gameObject;
                if (child.activeSelf && child != _lastCharacter)
                {
                    _lastCharacter = child;
                    OnCharacterChanged(child);
                    break;
                }
            }
        }
    }
}
