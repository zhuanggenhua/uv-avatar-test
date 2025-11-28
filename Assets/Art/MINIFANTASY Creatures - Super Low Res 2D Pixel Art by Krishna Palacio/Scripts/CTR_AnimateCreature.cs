using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Minifantasy.Creatures
{
    public class CTR_AnimateCreature : MonoBehaviour
    {
        private GameObject activeCharacter;
        private Animator currentAnimator;
        private string currentParameter = "Idle";
        [SerializeField] private Text creatureName;

        private void Start()
        {
            //make sure all the prefabs are off at start
            for (int i = 0; i < transform.childCount; i++)
            {
                transform.GetChild(i).gameObject.SetActive(false);
            }

            //set default active character
            activeCharacter = transform.GetChild(0).gameObject;
            currentAnimator = activeCharacter.GetComponentInChildren<Animator>();
        }

        public void TurnOffCurrentParameter()
        {
            currentAnimator.SetBool(currentParameter, false);
        }

        public void ToggleAnimation(string nextParameter)
        {
            currentAnimator.SetTrigger("Clicked");
            currentAnimator.SetBool(nextParameter, true);
            currentParameter = nextParameter;
        }

        public void ToggleXDirection(float x)
        {
            currentAnimator.SetFloat("X", x);
        }
        public void ToggleYDirection(float y)
        {
            currentAnimator.SetFloat("Y", y);
        }

        public void TurnOffActiveCharacter()
        {
            activeCharacter.SetActive(false);
        }

        public void UpdateActiveCharacter(GameObject selectedCharacter)
        {
            activeCharacter = selectedCharacter;
            activeCharacter.SetActive(true);
            currentAnimator = activeCharacter.GetComponentInChildren<Animator>();
            creatureName.text = activeCharacter.name;
        }
    }
}
