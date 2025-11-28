using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Minifantasy.CraftingAndProfessionsI
{
    public class CP1_MineralNode : MonoBehaviour
    {
        [Tooltip("Select a Prop Variant.")]
        [SerializeField] private Mineral mineralSelection = Mineral.Iron;
        [SerializeField] private Gem gemSelection = Gem.Amethyst;
        [SerializeField] private bool isDepleted = false;

        [Header("Sprites")]
        [SerializeField] private Sprite coalNode;
        [SerializeField] private Sprite coalNodeDepleted;
        [SerializeField] private Sprite ironNode;
        [SerializeField] private Sprite ironNodeAmethyst;
        [SerializeField] private Sprite ironNodeDiamond;
        [SerializeField] private Sprite ironNodeEmerald;
        [SerializeField] private Sprite ironNodeRuby;
        [SerializeField] private Sprite ironNodeTopaz;
        [SerializeField] private Sprite ironNodeDepleted;
        [SerializeField] private Sprite tinNode;
        [SerializeField] private Sprite tinNodeAmethyst;
        [SerializeField] private Sprite tinNodeDiamond;
        [SerializeField] private Sprite tinNodeEmerald;
        [SerializeField] private Sprite tinNodeRuby;
        [SerializeField] private Sprite tinNodeTopaz;
        [SerializeField] private Sprite tinNodeDepleted;
        [SerializeField] private Sprite copperNode;
        [SerializeField] private Sprite copperNodeAmethyst;
        [SerializeField] private Sprite copperNodeDiamond;
        [SerializeField] private Sprite copperNodeEmerald;
        [SerializeField] private Sprite copperNodeRuby;
        [SerializeField] private Sprite copperNodeTopaz;
        [SerializeField] private Sprite copperNodeDepleted;
        [SerializeField] private Sprite silverNode;
        [SerializeField] private Sprite silverNodeAmethyst;
        [SerializeField] private Sprite silverNodeDiamond;
        [SerializeField] private Sprite silverNodeEmerald;
        [SerializeField] private Sprite silverNodeRuby;
        [SerializeField] private Sprite silverNodeTopaz;
        [SerializeField] private Sprite silverNodeDepleted;
        [SerializeField] private Sprite goldNode;
        [SerializeField] private Sprite goldNodeAmethyst;
        [SerializeField] private Sprite goldNodeDiamond;
        [SerializeField] private Sprite goldNodeEmerald;
        [SerializeField] private Sprite goldNodeRuby;
        [SerializeField] private Sprite goldNodeTopaz;
        [SerializeField] private Sprite goldNodeDepleted;

        [Header("Shadows")]
        [SerializeField] private Sprite coalNodeShadow;
        [SerializeField] private Sprite ironNodeShadow;
        [SerializeField] private Sprite tinNodeShadow;
        [SerializeField] private Sprite copperNodeShadow;
        [SerializeField] private Sprite silverNodeShadow;
        [SerializeField] private Sprite goldNodeShadow;
        [SerializeField] private Sprite mineralNodeDepletedShadow;

        private void OnValidate()
        {
            Sprite selectedSprite = null;
            Sprite selectedShadow = null;

            switch (mineralSelection)
            {
                case Mineral.Coal:
                    if (isDepleted)
                    {
                        selectedSprite = coalNodeDepleted;
                        selectedShadow = mineralNodeDepletedShadow;
                    }
                    else
                    {
                        selectedSprite = coalNode;
                        selectedShadow = coalNodeShadow;
                    }
                    break;
                case Mineral.Iron:
                    if (isDepleted)
                    {
                        selectedSprite = ironNodeDepleted;
                        selectedShadow = mineralNodeDepletedShadow;
                    }
                    else
                    {
                        selectedShadow = ironNodeShadow;
                        switch (gemSelection)
                        {
                            case Gem.None:
                                selectedSprite = ironNode;
                                break;
                            case Gem.Amethyst:
                                selectedSprite = ironNodeAmethyst;
                                break;
                            case Gem.Diamond:
                                selectedSprite = ironNodeDiamond;
                                break;
                            case Gem.Emerald:
                                selectedSprite = ironNodeEmerald;
                                break;
                            case Gem.Ruby:
                                selectedSprite = ironNodeRuby;
                                break;
                            case Gem.Topaz:
                                selectedSprite = ironNodeTopaz;
                                break;
                        }
                        break;
                    }
                    break;
                case Mineral.Tin:
                    if (isDepleted)
                    {
                        selectedSprite = tinNodeDepleted;
                        selectedShadow = mineralNodeDepletedShadow;
                    }
                    else
                    {
                        selectedShadow = tinNodeShadow;
                        switch (gemSelection)
                        {
                            case Gem.None:
                                selectedSprite = tinNode;
                                break;
                            case Gem.Amethyst:
                                selectedSprite = tinNodeAmethyst;
                                break;
                            case Gem.Diamond:
                                selectedSprite = tinNodeDiamond;
                                break;
                            case Gem.Emerald:
                                selectedSprite = tinNodeEmerald;
                                break;
                            case Gem.Ruby:
                                selectedSprite = tinNodeRuby;
                                break;
                            case Gem.Topaz:
                                selectedSprite = tinNodeTopaz;
                                break;
                        }
                        break;
                    }
                    break;
                case Mineral.Copper:
                    if (isDepleted)
                    {
                        selectedSprite = copperNodeDepleted;
                        selectedShadow = mineralNodeDepletedShadow;
                    }
                    else
                    {
                        selectedShadow = copperNodeShadow;
                        switch (gemSelection)
                        {
                            case Gem.None:
                                selectedSprite = copperNode;
                                break;
                            case Gem.Amethyst:
                                selectedSprite = copperNodeAmethyst;
                                break;
                            case Gem.Diamond:
                                selectedSprite = copperNodeDiamond;
                                break;
                            case Gem.Emerald:
                                selectedSprite = copperNodeEmerald;
                                break;
                            case Gem.Ruby:
                                selectedSprite = copperNodeRuby;
                                break;
                            case Gem.Topaz:
                                selectedSprite = copperNodeTopaz;
                                break;
                        }
                        break;
                    }
                    break;
                case Mineral.Silver:
                    if (isDepleted)
                    {
                        selectedSprite = silverNodeDepleted;
                        selectedShadow = mineralNodeDepletedShadow;
                    }
                    else
                    {
                        selectedShadow = silverNodeShadow;
                        switch (gemSelection)
                        {
                            case Gem.None:
                                selectedSprite = silverNode;
                                break;
                            case Gem.Amethyst:
                                selectedSprite = silverNodeAmethyst;
                                break;
                            case Gem.Diamond:
                                selectedSprite = silverNodeDiamond;
                                break;
                            case Gem.Emerald:
                                selectedSprite = silverNodeEmerald;
                                break;
                            case Gem.Ruby:
                                selectedSprite = silverNodeRuby;
                                break;
                            case Gem.Topaz:
                                selectedSprite = silverNodeTopaz;
                                break;
                        }
                        break;
                    }
                    break;
                case Mineral.Gold:
                    if (isDepleted)
                    {
                        selectedSprite = goldNodeDepleted;
                        selectedShadow = mineralNodeDepletedShadow;
                    }
                    else
                    {
                        selectedShadow = goldNodeShadow;
                        switch (gemSelection)
                        {
                            case Gem.None:
                                selectedSprite = goldNode;
                                break;
                            case Gem.Amethyst:
                                selectedSprite = goldNodeAmethyst;
                                break;
                            case Gem.Diamond:
                                selectedSprite = goldNodeDiamond;
                                break;
                            case Gem.Emerald:
                                selectedSprite = goldNodeEmerald;
                                break;
                            case Gem.Ruby:
                                selectedSprite = goldNodeRuby;
                                break;
                            case Gem.Topaz:
                                selectedSprite = goldNodeTopaz;
                                break;
                        }
                        break;
                    }
                    break;
            }
            GetComponent<SpriteRenderer>().sprite = selectedSprite;
            transform.Find("Shadow").GetComponent<SpriteRenderer>().sprite = selectedShadow;
        }

        private enum Mineral
        {
            Coal,
            Iron,
            Tin,
            Copper,
            Silver,
            Gold,
        }

        private enum Gem
        {
            None,
            Amethyst,
            Diamond,
            Emerald,
            Ruby,
            Topaz,
        }
    }
}