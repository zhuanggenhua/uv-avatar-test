using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Minifantasy.CraftingAndProfessionsI
{
    public class CP1_PlantNode : MonoBehaviour
    {
        [Tooltip("Select a Prop Variant.")]
        [SerializeField] private Plant plantSelection = Plant.Linen;
        [SerializeField] private bool isDepleted = false;

        [Header("Sprites")]
        [SerializeField] private Sprite linenNode;
        [SerializeField] private Sprite linenNodeDepleted;
        [SerializeField] private Sprite cottonNode;
        [SerializeField] private Sprite cottonNodeDepleted;
        [SerializeField] private Sprite hempNode;
        [SerializeField] private Sprite hempNodeDepleted;
        [SerializeField] private Sprite ramieNode;
        [SerializeField] private Sprite ramieNodeDepleted;
        [SerializeField] private Sprite agaveNode;
        [SerializeField] private Sprite agaveNodeDepleted;

        [Header("Shadows")]
        [SerializeField] private Sprite linenNodeShadow;
        [SerializeField] private Sprite cottonNodeShadow;
        [SerializeField] private Sprite hempNodeShadow;
        [SerializeField] private Sprite ramieNodeShadow;
        [SerializeField] private Sprite agaveNodeShadow;
        [SerializeField] private Sprite linenNodeDepletedShadow;
        [SerializeField] private Sprite cottonNodeDepletedShadow;
        [SerializeField] private Sprite hempNodeDepletedShadow;
        [SerializeField] private Sprite ramieNodeDepletedShadow;
        [SerializeField] private Sprite agaveNodeDepletedShadow;

        private void OnValidate()
        {
            Sprite selectedSprite = null;
            Sprite selectedShadow = null;

            switch (plantSelection)
            {
                case Plant.Linen:
                    if (isDepleted)
                    {
                        selectedSprite = linenNodeDepleted;
                        selectedShadow = linenNodeDepletedShadow;
                    }
                    else
                    {
                        selectedSprite = linenNode;
                        selectedShadow = linenNodeShadow;
                    }
                    break;
                case Plant.Cotton:
                    if (isDepleted)
                    {
                        selectedSprite = cottonNodeDepleted;
                        selectedShadow = cottonNodeDepletedShadow;
                    }
                    else
                    {
                        selectedSprite = cottonNode;
                        selectedShadow = cottonNodeShadow;
                    }
                    break;
                case Plant.Hemp:
                    if (isDepleted)
                    {
                        selectedSprite = hempNodeDepleted;
                        selectedShadow = hempNodeDepletedShadow;
                    }
                    else
                    {
                        selectedSprite = hempNode;
                        selectedShadow = hempNodeShadow;
                    }
                    break;
                case Plant.Ramie:
                    if (isDepleted)
                    {
                        selectedSprite = ramieNodeDepleted;
                        selectedShadow = ramieNodeDepletedShadow;
                    }
                    else
                    {
                        selectedSprite = ramieNode;
                        selectedShadow = ramieNodeShadow;
                    }
                    break;
                case Plant.Agave:
                    if (isDepleted)
                    {
                        selectedSprite = agaveNodeDepleted;
                        selectedShadow = agaveNodeDepletedShadow;
                    }
                    else
                    {
                        selectedSprite = agaveNode;
                        selectedShadow = agaveNodeShadow;
                    }
                    break;
            }
            GetComponent<SpriteRenderer>().sprite = selectedSprite;
            transform.Find("Shadow").GetComponent<SpriteRenderer>().sprite = selectedShadow;
        }

        private enum Plant
        {
            Linen,
            Cotton,
            Hemp,
            Ramie,
            Agave,
        }
    }
}