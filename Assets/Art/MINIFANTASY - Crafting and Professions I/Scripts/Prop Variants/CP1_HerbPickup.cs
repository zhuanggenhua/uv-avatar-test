using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Minifantasy.CraftingAndProfessionsI
{
    public class CP1_HerbPickup : MonoBehaviour
    {
        [Tooltip("Select a Prop Variant.")]
        [SerializeField] private HerbPickup herbPickupSelection = HerbPickup.Mandrake;
        [SerializeField] private bool isGrounded = true;

        [Header("Sprites")]
        [SerializeField] private Sprite mandrakePickup;
        [SerializeField] private Sprite mandrakePickupGrounded;
        [SerializeField] private Sprite mushroomPickup;
        [SerializeField] private Sprite mushroomPickupGrounded;
        [SerializeField] private Sprite henbanePickup;
        [SerializeField] private Sprite henbanePickupGrounded;
        [SerializeField] private Sprite belladonnaPickup;
        [SerializeField] private Sprite belladonnaPickupGrounded;
        [SerializeField] private Sprite poppyPickup;
        [SerializeField] private Sprite poppyPickupGrounded;

        private void OnValidate()
        {
            Sprite selectedSprite = null;
            Sprite selectedShadow = null;

            switch (herbPickupSelection)
            {
                case HerbPickup.Mandrake:
                    if (isGrounded)
                    {
                        selectedSprite = mandrakePickupGrounded;
                        selectedShadow = null;
                    }
                    else
                    {
                        selectedSprite = mandrakePickup;
                    }
                    break;
                case HerbPickup.Mushroom:
                    if (isGrounded)
                    {
                        selectedSprite = mushroomPickupGrounded;
                    }
                    else
                    {
                        selectedSprite = mushroomPickup;
                    }
                    break;
                case HerbPickup.Henbane:
                    if (isGrounded)
                    {
                        selectedSprite = henbanePickupGrounded;
                    }
                    else
                    {
                        selectedSprite = henbanePickup;
                    }
                    break;
                case HerbPickup.Belladonna:
                    if (isGrounded)
                    {
                        selectedSprite = belladonnaPickupGrounded;
                    }
                    else
                    {
                        selectedSprite = belladonnaPickup;
                    }
                    break;
                case HerbPickup.Poppy:
                    if (isGrounded)
                    {
                        selectedSprite = poppyPickupGrounded;
                    }
                    else
                    {
                        selectedSprite = poppyPickup;
                    }
                    break;
            }
            GetComponent<SpriteRenderer>().sprite = selectedSprite;
            transform.Find("Shadow").GetComponent<SpriteRenderer>().sprite = selectedShadow;
        }

        private enum HerbPickup
        {
            Mandrake,
            Mushroom,
            Henbane,
            Belladonna,
            Poppy,
        }
    }
}