using System.Collections.Generic;
using UnityEngine;

namespace EquipmentSystem.Data
{
    /// <summary>
    /// 装备渲染模式
    /// </summary>
    public enum EquipRenderMode
    {
        None,       // 不渲染（如 Bag）
        Sprite,     // 贴图类（Clothing/Pants/Cloak/Helmet）
        Color,      // 颜色类（Gloves/Shoes）
        Weapon,     // 武器（特殊处理）
    }

    /// <summary>
    /// 装备类型配置 - 描述一个类型如何渲染和显示
    /// </summary>
    public class EquipTypeConfig
    {
        public EquipmentType Type;
        public string DisplayName;
        public EquipRenderMode RenderMode;
        public CharacterBodyPart BodyPart;      // Sprite 模式用
        public string TexProp, RectProp, EnableProp;   // Shader 属性名
        public string LeftColorProp, RightColorProp;   // Color 模式用
        public int RenderOrder;                 // 渲染顺序（同 BodyPart 内）
        public bool HandInFrontForWeapon = true; // 仅 RenderMode=Weapon 时有效：true=手在前，false=武器在前（典型：盾牌）
        
        // 缓存的 Shader 属性 ID
        public int TexPropId { get; private set; }
        public int RectPropId { get; private set; }
        public int EnablePropId { get; private set; }
        public int LeftColorPropId { get; private set; }
        public int RightColorPropId { get; private set; }
        
        public void CachePropertyIDs()
        {
            if (!string.IsNullOrEmpty(TexProp)) TexPropId = Shader.PropertyToID(TexProp);
            if (!string.IsNullOrEmpty(RectProp)) RectPropId = Shader.PropertyToID(RectProp);
            if (!string.IsNullOrEmpty(EnableProp)) EnablePropId = Shader.PropertyToID(EnableProp);
            if (!string.IsNullOrEmpty(LeftColorProp)) LeftColorPropId = Shader.PropertyToID(LeftColorProp);
            if (!string.IsNullOrEmpty(RightColorProp)) RightColorPropId = Shader.PropertyToID(RightColorProp);
        }
    }

    /// <summary>
    /// 装备类型注册表 - 集中管理所有装备类型的配置
    /// 新增类型时只需在这里加一条配置
    /// </summary>
    public static class EquipTypeRegistry
    {
        static readonly List<EquipTypeConfig> _configs = new List<EquipTypeConfig>();
        static readonly Dictionary<EquipmentType, EquipTypeConfig> _byType = new Dictionary<EquipmentType, EquipTypeConfig>();
        static bool _initialized = false;

        public static IReadOnlyList<EquipTypeConfig> All => _configs;

        public static EquipTypeConfig Get(EquipmentType type)
        {
            EnsureInit();
            return _byType.TryGetValue(type, out var cfg) ? cfg : null;
        }

        public static string GetDisplayName(EquipmentType type)
        {
            var cfg = Get(type);
            return cfg != null ? cfg.DisplayName : type.ToString();
        }

        public static IEnumerable<EquipTypeConfig> GetByRenderMode(EquipRenderMode mode)
        {
            EnsureInit();
            foreach (var cfg in _configs)
                if (cfg.RenderMode == mode)
                    yield return cfg;
        }

        static void EnsureInit()
        {
            if (_initialized) return;
            _initialized = true;

            // ========== 在这里注册所有装备类型 ==========
            // 新增类型时只需加一条 Register(...)
            
            // Body 层 Sprite 类（渲染顺序：Pants → Clothing → Cloak）
            Register(new EquipTypeConfig
            {
                Type = EquipmentType.Pants,
                DisplayName = "裤子",
                RenderMode = EquipRenderMode.Sprite,
                BodyPart = CharacterBodyPart.Torso,
                TexProp = "_PantsTex", RectProp = "_PantsRect", EnableProp = "_EnablePants",
                RenderOrder = 0,
            });
            Register(new EquipTypeConfig
            {
                Type = EquipmentType.Clothing,
                DisplayName = "服装",
                RenderMode = EquipRenderMode.Sprite,
                BodyPart = CharacterBodyPart.Torso,
                TexProp = "_ClothTex", RectProp = "_ClothRect", EnableProp = "_EnableCloth",
                RenderOrder = 1,
            });
            Register(new EquipTypeConfig
            {
                Type = EquipmentType.Cloak,
                DisplayName = "斗篷",
                RenderMode = EquipRenderMode.Sprite,
                BodyPart = CharacterBodyPart.Torso,
                TexProp = "_CloakTex", RectProp = "_CloakRect", EnableProp = "_EnableCloak",
                RenderOrder = 2,
            });

            // Head 层 Sprite 类
            Register(new EquipTypeConfig
            {
                Type = EquipmentType.Helmet,
                DisplayName = "头盔",
                RenderMode = EquipRenderMode.Sprite,
                BodyPart = CharacterBodyPart.Head,
                TexProp = "_HelmetTex", RectProp = "_HelmetRect", EnableProp = "_EnableHelmet",
                RenderOrder = 0,
            });

            // 头部插槽的其他类型：帽子 / 面罩（与 Helmet 复用同一 Shader 通道）
            Register(new EquipTypeConfig
            {
                Type = EquipmentType.Hat,
                DisplayName = "帽子",
                RenderMode = EquipRenderMode.Sprite,
                BodyPart = CharacterBodyPart.Head,
                TexProp = "_HelmetTex", RectProp = "_HelmetRect", EnableProp = "_EnableHelmet",
                RenderOrder = 0,
            });
            Register(new EquipTypeConfig
            {
                Type = EquipmentType.Mask,
                DisplayName = "面罩",
                RenderMode = EquipRenderMode.Sprite,
                BodyPart = CharacterBodyPart.Head,
                TexProp = "_HelmetTex", RectProp = "_HelmetRect", EnableProp = "_EnableHelmet",
                RenderOrder = 0,
            });

            // 颜色类
            Register(new EquipTypeConfig
            {
                Type = EquipmentType.Gloves,
                DisplayName = "手套",
                RenderMode = EquipRenderMode.Color,
                LeftColorProp = "_LeftHandColor", RightColorProp = "_RightHandColor",
                EnableProp = "_EnableGloves",
            });
            Register(new EquipTypeConfig
            {
                Type = EquipmentType.Shoes,
                DisplayName = "鞋子",
                RenderMode = EquipRenderMode.Color,
                LeftColorProp = "_LeftFootColor", RightColorProp = "_RightFootColor",
                EnableProp = "_EnableShoes",
            });

            // 武器
            Register(new EquipTypeConfig
            {
                Type = EquipmentType.Weapon,
                DisplayName = "武器",
                RenderMode = EquipRenderMode.Weapon,
                HandInFrontForWeapon = true,
            });

            // 盾牌（仍走武器渲染模式，使用武器锚点与深度规则）
            Register(new EquipTypeConfig
            {
                Type = EquipmentType.Shield,
                DisplayName = "盾牌",
                RenderMode = EquipRenderMode.Weapon,
                HandInFrontForWeapon = false,
            });

            // 背包（暂不渲染）
            Register(new EquipTypeConfig
            {
                Type = EquipmentType.Bag,
                DisplayName = "背包",
                RenderMode = EquipRenderMode.None,
            });
        }

        static void Register(EquipTypeConfig cfg)
        {
            cfg.CachePropertyIDs();
            _configs.Add(cfg);
            _byType[cfg.Type] = cfg;
        }
    }
}
