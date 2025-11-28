using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Minifantasy.TrueHeroes
{
    public class TH_DemoManager : MonoBehaviour
    {
        enum ActiveCharacter { Barbarian, Druid, ForestBeast, ForestHound, ForestOwl, Rogue }
        [SerializeField] ActiveCharacter startingCharacter = ActiveCharacter.Barbarian;

        [System.Serializable]
        public class CharacterGroup
        {
            public List<GameObject> characterGroupItems;
        }

        [SerializeField] public List<CharacterGroup> characterGroups = new List<CharacterGroup>();
        private GameObject activeCharacter = null;
        private Animator currentAnimator = null;
        private string currentParamter = null;

        private void Start()
        {
            TurnOnOffGroupUI(startingCharacter.ToString());
        }

        public void TurnOnOffGroupUI(string name)
        {
            bool isTargetGroup = false;

            foreach (CharacterGroup characterGroup in characterGroups)
            {
                foreach (GameObject characterGroupItem in characterGroup.characterGroupItems)
                {
                    string characterName = RemoveSpace(characterGroupItem.name);
                    isTargetGroup = characterName.Contains(name);

                    // Turn group on / off
                    characterGroupItem.SetActive(isTargetGroup);

                    // Set active character for outside access
                    if (isTargetGroup)
                    {
                        activeCharacter = characterGroupItem;
                        currentAnimator = activeCharacter.GetComponentInChildren<Animator>();
                    }
                }
            }
            resetAnimatorParameters();
            setAnimationParameterDirection("SE");
        }

        public void setAnimationParameterBool(string parameter)
        {
            if (currentAnimator)
            {
                if (currentParamter != null) { currentAnimator.SetBool(currentParamter, false); }
                currentAnimator.SetBool(parameter, true);
                currentParamter = parameter;
            }
        }

        private void resetAnimatorParameters()
        {
            foreach (var param in currentAnimator.parameters)
            {
                if (param.type == AnimatorControllerParameterType.Bool)
                {
                    currentAnimator.SetBool(param.name, false);
                }
            }
        }

        public void setAnimationParameterDirection(string direction)
        {
            float x = 0;
            float y = 0;

            switch (direction)
            {
                case ("NE"):
                    x = 1;
                    y = 1;
                    break;
                case ("NW"):
                    x = -1;
                    y = 1;
                    break;
                case ("SE"):
                    x = 1;
                    y = -1;
                    break;
                case ("SW"):
                    x = -1;
                    y = -1;
                    break;
            }

            if (currentAnimator)
            {
                currentAnimator.SetFloat("X", x);
                currentAnimator.SetFloat("Y", y);
            }
        }

        // helper
        private string RemoveSpace(string input)
        {
            return String.Concat(input.Where(c => !Char.IsWhiteSpace(c)));
        }
    }
}
