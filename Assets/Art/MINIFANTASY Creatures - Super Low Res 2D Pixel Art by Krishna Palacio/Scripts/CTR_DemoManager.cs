using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Minifantasy.Creatures
{
    public class CTR_DemoManager : MonoBehaviour
    {
        [Header("Character")]
        [SerializeField] GameObject characterCanvas;
        [SerializeField] GameObject characterContainer;
        [SerializeField] GameObject[] buttonGroups = new GameObject[8];


        [Header("Menu")]
        [SerializeField] GameObject menuCanvas;

        private CTR_AnimateCreature creatures_AnimatedCharacterSelection;

        private void Start()
        {
            creatures_AnimatedCharacterSelection = characterContainer.GetComponent<CTR_AnimateCreature>();

            //start settings: turn off character canvas, turn on menu canvas, turn on character container
            if (characterCanvas.activeSelf != false)
                characterCanvas.SetActive(false);
            if (menuCanvas.activeSelf != true)
                menuCanvas.SetActive(true);
            if (characterContainer.activeSelf != true)
                characterContainer.SetActive(true);
            foreach (var button in buttonGroups)
            {
                if (button.activeSelf == true)
                {
                    button.SetActive(false);
                }
            }
        }

        public void OnReturnToMenu()
        {
            //turn menu on, turn character canvas off, turn off buttons
            menuCanvas.SetActive(true);
            characterCanvas.SetActive(false);
            for (int i = 0; i < buttonGroups.Length; i++)
            {
                buttonGroups[i].SetActive(false);
            }
        }

        public void OnCreatureSelected(GameObject creature)
        {
            characterCanvas.SetActive(true);
            menuCanvas.SetActive(false);
            creatures_AnimatedCharacterSelection.UpdateActiveCharacter(creature);
        }
    }
}