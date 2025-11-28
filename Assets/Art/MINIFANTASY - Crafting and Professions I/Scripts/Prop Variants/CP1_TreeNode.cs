using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Minifantasy.CraftingAndProfessionsI
{
    public class CP1_TreeNode : MonoBehaviour
    {
        [Header("AnimatorOverrideControllers")]
        //Using "OrC" to shorten OverrideController
        [SerializeField] private AnimatorOverrideController birchAnimatorOrC; 
        [SerializeField] private AnimatorOverrideController hickoryAnimatorOrC;
        [SerializeField] private AnimatorOverrideController oakAnimatorOrC;
        [SerializeField] private AnimatorOverrideController pineAnimatorOrC;
        [SerializeField] private AnimatorOverrideController willowAnimatorOrC;

        [Tooltip("Select a Prop Variant.")]
        [SerializeField] private Tree treeSelection = Tree.Hickory;
        [SerializeField] private bool isDepleted = false;

        [Header("Sprites")]
        [SerializeField] private Sprite birchNode;
        [SerializeField] private Sprite birchNodeDepleted;
        [SerializeField] private Sprite hickoryNode;
        [SerializeField] private Sprite hickoryNodeDepleted;
        [SerializeField] private Sprite oakNode;
        [SerializeField] private Sprite oakNodeDepleted;
        [SerializeField] private Sprite pineNode;
        [SerializeField] private Sprite pineNodeDepleted;
        [SerializeField] private Sprite willowNode;
        [SerializeField] private Sprite willowNodeDepleted;

        [Header("Shadows")]
        [SerializeField] private Sprite birchNodeShadow;
        [SerializeField] private Sprite hickoryNodeShadow;
        [SerializeField] private Sprite oakNodeShadow;
        [SerializeField] private Sprite pineNodeShadow;
        [SerializeField] private Sprite willowNodeShadow;
        [SerializeField] private Sprite birchNodeDepletedShadow;
        [SerializeField] private Sprite hickoryNodeDepletedShadow;
        [SerializeField] private Sprite oakNodeDepletedShadow;
        [SerializeField] private Sprite pineNodeDepletedShadow;
        [SerializeField] private Sprite willowNodeDepletedShadow;

        private void OnValidate()
        {
            Sprite selectedSprite = null;
            Sprite selectedShadow = null;
            AnimatorOverrideController selectedAnimatorOrC = null;

            switch (treeSelection)
            {
                case Tree.Birch:
                    if (isDepleted)
                    {
                        selectedSprite = birchNodeDepleted;
                        selectedShadow = birchNodeDepletedShadow;
                    }
                    else
                    {
                        selectedSprite = birchNode;
                        selectedShadow = birchNodeShadow;
                    }
                    selectedAnimatorOrC = birchAnimatorOrC;
                    break;
                case Tree.Hickory:
                    if (isDepleted)
                    {
                        selectedSprite = hickoryNodeDepleted;
                        selectedShadow = hickoryNodeDepletedShadow;
                    }
                    else
                    {
                        selectedSprite = hickoryNode;
                        selectedShadow = hickoryNodeShadow;
                    }
                    selectedAnimatorOrC = hickoryAnimatorOrC;
                    break;
                case Tree.Oak:
                    if (isDepleted)
                    {
                        selectedSprite = oakNodeDepleted;
                        selectedShadow = oakNodeDepletedShadow;
                    }
                    else
                    {
                        selectedSprite = oakNode;
                        selectedShadow = oakNodeShadow;
                    }
                    selectedAnimatorOrC = oakAnimatorOrC;
                    break;
                case Tree.Pine:
                    if (isDepleted)
                    {
                        selectedSprite = pineNodeDepleted;
                        selectedShadow = pineNodeDepletedShadow;
                    }
                    else
                    {
                        selectedSprite = pineNode;
                        selectedShadow = pineNodeShadow;
                    }
                    selectedAnimatorOrC = pineAnimatorOrC;
                    break;
                case Tree.Willow:
                    if (isDepleted)
                    {
                        selectedSprite = willowNodeDepleted;
                        selectedShadow = willowNodeDepletedShadow;
                    }
                    else
                    {
                        selectedSprite = willowNode;
                        selectedShadow = willowNodeShadow;
                    }
                    selectedAnimatorOrC = willowAnimatorOrC;
                    break;
            }
            GetComponent<SpriteRenderer>().sprite = selectedSprite;
            GetComponent<Animator>().runtimeAnimatorController = selectedAnimatorOrC;
            transform.Find("Shadow").GetComponent<SpriteRenderer>().sprite = selectedShadow;
        }

        private enum Tree
        {
            Birch,
            Hickory,
            Oak,
            Pine,
            Willow,
        }
    }
}