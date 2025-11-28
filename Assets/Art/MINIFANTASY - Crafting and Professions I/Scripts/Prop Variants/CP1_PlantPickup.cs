using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Minifantasy.CraftingAndProfessionsI
{
    public class CP1_PlantPickup : MonoBehaviour
    {
        [Tooltip("Select a Prop Variant.")]
        [SerializeField] private PlantPickup plantPickupSelection = PlantPickup.Linen;
        [SerializeField] private bool isGrounded = false;

        [Header("Sprites")]
        [SerializeField] private Sprite linenPickup;
        [SerializeField] private Sprite linenPickupGrounded;
        [SerializeField] private Sprite cottonPickup;
        [SerializeField] private Sprite cottonPickupGrounded;
        [SerializeField] private Sprite hempPickup;
        [SerializeField] private Sprite hempPickupGrounded;
        [SerializeField] private Sprite ramiePickup;
        [SerializeField] private Sprite ramiePickupGrounded;
        [SerializeField] private Sprite agavePickup;
        [SerializeField] private Sprite agavePickupGrounded;

        private void OnValidate()
        {
            Sprite selectedSprite = null;
            Sprite selectedShadow = null;

            switch (plantPickupSelection)
            {
                case PlantPickup.Linen:
                    if (isGrounded)
                    {
                        selectedSprite = linenPickupGrounded;
                    }
                    else
                    {
                        selectedSprite = linenPickup;
                    }
                    break;
                case PlantPickup.Cotton:
                    if (isGrounded)
                    {
                        selectedSprite = cottonPickupGrounded;
                    }
                    else
                    {
                        selectedSprite = cottonPickup;
                    }
                    break;
                case PlantPickup.Hemp:
                    if (isGrounded)
                    {
                        selectedSprite = hempPickupGrounded;
                    }
                    else
                    {
                        selectedSprite = hempPickup;
                    }
                    break;
                case PlantPickup.Ramie:
                    if (isGrounded)
                    {
                        selectedSprite = ramiePickupGrounded;
                    }
                    else
                    {
                        selectedSprite = ramiePickup;
                    }
                    break;
                case PlantPickup.Agave:
                    if (isGrounded)
                    {
                        selectedSprite = agavePickupGrounded;
                    }
                    else
                    {
                        selectedSprite = agavePickup;
                    }
                    break;
            }
            GetComponent<SpriteRenderer>().sprite = selectedSprite;
            transform.Find("Shadow").GetComponent<SpriteRenderer>().sprite = selectedShadow;
        }

        private enum PlantPickup
        {
            Linen,
            Cotton,
            Hemp,
            Ramie,
            Agave,
        }
    }
}