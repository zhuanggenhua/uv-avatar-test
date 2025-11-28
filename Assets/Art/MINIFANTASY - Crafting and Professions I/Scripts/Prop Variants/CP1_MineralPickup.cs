using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Minifantasy.CraftingAndProfessionsI
{
    public class CP1_MineralPickup : MonoBehaviour
    {
        [Tooltip("Select a Prop Variant.")]
        [SerializeField] private MineralPickup mineralPickupSelection = MineralPickup.Iron;
        [SerializeField] private bool isGrounded = false;

        [Header("Sprites")]
        [SerializeField] private Sprite coalPickup;
        [SerializeField] private Sprite coalPickupGrounded;
        [SerializeField] private Sprite ironPickup;
        [SerializeField] private Sprite ironPickupGrounded;
        [SerializeField] private Sprite tinPickup;
        [SerializeField] private Sprite tinPickupGrounded;
        [SerializeField] private Sprite copperPickup;
        [SerializeField] private Sprite copperPickupGrounded;
        [SerializeField] private Sprite silverPickup;
        [SerializeField] private Sprite silverPickupGrounded;
        [SerializeField] private Sprite goldPickup;
        [SerializeField] private Sprite goldPickupGrounded;
        [SerializeField] private Sprite rockPickup;
        [SerializeField] private Sprite rockPickupGrounded;

        private void OnValidate()
        {
            Sprite selectedSprite = null;
            Sprite selectedShadow = null;

            switch (mineralPickupSelection)
            {
                case MineralPickup.Coal:
                    if (isGrounded)
                    {
                        selectedSprite = coalPickupGrounded;
                    }
                    else
                    {
                        selectedSprite = coalPickup;
                    }
                    break;
                case MineralPickup.Iron:
                    if (isGrounded)
                    {
                        selectedSprite = ironPickupGrounded;
                    }
                    else
                    {
                        selectedSprite = ironPickup;
                    }
                    break;
                case MineralPickup.Tin:
                    if (isGrounded)
                    {
                        selectedSprite = tinPickupGrounded;
                    }
                    else
                    {
                        selectedSprite = tinPickup;
                    }
                    break;
                case MineralPickup.Copper:
                    if (isGrounded)
                    {
                        selectedSprite = copperPickupGrounded;
                    }
                    else
                    {
                        selectedSprite = copperPickup;
                    }
                    break;
                case MineralPickup.Silver:
                    if (isGrounded)
                    {
                        selectedSprite = silverPickupGrounded;
                    }
                    else
                    {
                        selectedSprite = silverPickup;
                    }
                    break;
                case MineralPickup.Gold:
                    if (isGrounded)
                    {
                        selectedSprite = goldPickupGrounded;
                    }
                    else
                    {
                        selectedSprite = goldPickup;
                    }
                    break;
                case MineralPickup.Rock:
                    if (isGrounded)
                    {
                        selectedSprite = rockPickupGrounded;
                    }
                    else
                    {
                        selectedSprite = rockPickup;
                    }
                    break;
            }
            GetComponent<SpriteRenderer>().sprite = selectedSprite;
            transform.Find("Shadow").GetComponent<SpriteRenderer>().sprite = selectedShadow;
        }

        private enum MineralPickup
        {
            Coal,
            Iron,
            Tin,
            Copper,
            Silver,
            Gold,
            Rock,
        }
    }
}