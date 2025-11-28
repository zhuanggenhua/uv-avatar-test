using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Minifantasy.CraftingAndProfessionsI
{
    public class CP1_HerbNode : MonoBehaviour
    {
        [Tooltip("Select a Prop Variant.")]
        [SerializeField] private Herb herbSelection = Herb.Mandrake;
        [SerializeField] private bool isDepleted = false;

        [Header("Sprites")]
        [SerializeField] private Sprite mandrakeNode;
        [SerializeField] private Sprite mandrakeNodeDepleted;
        [SerializeField] private Sprite mushroomNode;
        [SerializeField] private Sprite mushroomNodeDepleted;
        [SerializeField] private Sprite henbaneNode;
        [SerializeField] private Sprite henbaneNodeDepleted;
        [SerializeField] private Sprite belladonnaNode;
        [SerializeField] private Sprite belladonnaNodeDepleted;
        [SerializeField] private Sprite poppyNode;
        [SerializeField] private Sprite poppyNodeDepleted;

        [Header("Shadows")]
        [SerializeField] private Sprite mandrakeNodeShadow;
        [SerializeField] private Sprite mushroomNodeShadow;
        [SerializeField] private Sprite henbaneNodeShadow;
        [SerializeField] private Sprite belladonnaNodeShadow;
        [SerializeField] private Sprite poppyNodeShadow;
        [SerializeField] private Sprite mushroomNodeDepletedShadow;
        [SerializeField] private Sprite henbaneNodeDepletedShadow;
        [SerializeField] private Sprite belladonnaNodeDepletedShadow;
        [SerializeField] private Sprite poppyNodeDepletedShadow;

        private void OnValidate()
        {
            Sprite selectedSprite = null;
            Sprite selectedShadow = null;

            switch (herbSelection)
            {
                case Herb.Mandrake:
                    if (isDepleted)
                    {
                        selectedSprite = mandrakeNodeDepleted;
                        selectedShadow = null;
                    }
                    else
                    {
                        selectedSprite = mandrakeNode;
                        selectedShadow = mandrakeNodeShadow;
                    }
                    break;
                case Herb.Mushroom:
                    if (isDepleted)
                    {
                        selectedSprite = mushroomNodeDepleted;
                        selectedShadow = mushroomNodeDepletedShadow;
                    }
                    else
                    {
                        selectedSprite = mushroomNode;
                        selectedShadow = mushroomNodeShadow;
                    }
                    break;
                case Herb.Henbane:
                    if (isDepleted)
                    {
                        selectedSprite = henbaneNodeDepleted;
                        selectedShadow = henbaneNodeDepletedShadow;
                    }
                    else
                    {
                        selectedSprite = henbaneNode;
                        selectedShadow = henbaneNodeShadow;
                    }
                    break;
                case Herb.Belladonna:
                    if (isDepleted)
                    {
                        selectedSprite = belladonnaNodeDepleted;
                        selectedShadow = belladonnaNodeDepletedShadow;
                    }
                    else
                    {
                        selectedSprite = belladonnaNode;
                        selectedShadow = belladonnaNodeShadow;
                    }
                    break;
                case Herb.Poppy:
                    if (isDepleted)
                    {
                        selectedSprite = poppyNodeDepleted;
                        selectedShadow = poppyNodeDepletedShadow;
                    }
                    else
                    {
                        selectedSprite = poppyNode;
                        selectedShadow = poppyNodeShadow;
                    }
                    break;
            }
            GetComponent<SpriteRenderer>().sprite = selectedSprite;
            transform.Find("Shadow").GetComponent<SpriteRenderer>().sprite = selectedShadow;
        }

        private enum Herb
        {
            Mandrake,
            Mushroom,
            Henbane,
            Belladonna,
            Poppy,
        }
    }
}