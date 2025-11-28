using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Minifantasy.CraftingAndProfessionsI
{
    public class CP1_GemPickup : MonoBehaviour
    {
        [Tooltip("Select a Prop Variant.")]
        [SerializeField] private GemPickup mineralPickupSelection = GemPickup.Diamond;
        [SerializeField] private bool isGrounded = false;

        [Header("Sprites")]
        [SerializeField] private Sprite amethystPickup;
        [SerializeField] private Sprite amethystPickupGrounded;
        [SerializeField] private Sprite diamondPickup;
        [SerializeField] private Sprite diamondPickupGrounded;
        [SerializeField] private Sprite emeraldPickup;
        [SerializeField] private Sprite emeraldPickupGrounded;
        [SerializeField] private Sprite rubyPickup;
        [SerializeField] private Sprite rubyPickupGrounded;
        [SerializeField] private Sprite topazPickup;
        [SerializeField] private Sprite topazPickupGrounded;

        private void OnValidate()
        {
            Sprite selectedSprite = null;
            Sprite selectedShadow = null;

            switch (mineralPickupSelection)
            {
                case GemPickup.Amethyst:
                    if (isGrounded)
                    {
                        selectedSprite = amethystPickupGrounded;
                    }
                    else
                    {
                        selectedSprite = amethystPickup;
                    }
                    break;
                case GemPickup.Diamond:
                    if (isGrounded)
                    {
                        selectedSprite = diamondPickupGrounded;
                    }
                    else
                    {
                        selectedSprite = diamondPickup;
                    }
                    break;
                case GemPickup.Emerald:
                    if (isGrounded)
                    {
                        selectedSprite = emeraldPickupGrounded;
                    }
                    else
                    {
                        selectedSprite = emeraldPickup;
                    }
                    break;
                case GemPickup.Ruby:
                    if (isGrounded)
                    {
                        selectedSprite = rubyPickupGrounded;
                    }
                    else
                    {
                        selectedSprite = rubyPickup;
                    }
                    break;
                case GemPickup.Topaz:
                    if (isGrounded)
                    {
                        selectedSprite = topazPickupGrounded;
                    }
                    else
                    {
                        selectedSprite = topazPickup;
                    }
                    break;
            }
            GetComponent<SpriteRenderer>().sprite = selectedSprite;
            transform.Find("Shadow").GetComponent<SpriteRenderer>().sprite = selectedShadow;
        }

        private enum GemPickup
        {
            Amethyst,
            Diamond,
            Emerald,
            Ruby,
            Topaz,
        }
    }
}