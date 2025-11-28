using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Minifantasy.CraftingAndProfessionsI
{
    public class CP1_TreePickup : MonoBehaviour
    {
        [Tooltip("Select a Prop Variant.")]
        [SerializeField] private TreePickup treePickupSelection = TreePickup.Hickory;

        [Header("Sprites")]
        [SerializeField] private Sprite birchPickup;
        [SerializeField] private Sprite hickoryPickup;
        [SerializeField] private Sprite oakPickup;
        [SerializeField] private Sprite pinePickup;
        [SerializeField] private Sprite willowPickup;

        private void OnValidate()
        {
            Sprite selectedSprite = null;
            Sprite selectedShadow = null;

            switch (treePickupSelection)
            {
                case TreePickup.Birch:
                        selectedSprite = birchPickup;
                    break;
                case TreePickup.Hickory:
                        selectedSprite = hickoryPickup;
                    break;
                case TreePickup.Oak:
                        selectedSprite = oakPickup;
                    break;
                case TreePickup.Pine:
                        selectedSprite = pinePickup;
                    break;
                case TreePickup.Willow:
                        selectedSprite = willowPickup;
                    break;
            }
            GetComponent<SpriteRenderer>().sprite = selectedSprite;
            transform.Find("Shadow").GetComponent<SpriteRenderer>().sprite = selectedShadow;
        }

        private enum TreePickup
        {
            Birch,
            Hickory,
            Oak,
            Pine,
            Willow,
        }
    }
}