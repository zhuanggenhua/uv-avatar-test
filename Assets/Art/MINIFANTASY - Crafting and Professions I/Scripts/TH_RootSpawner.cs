using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Minifantasy.TrueHeroes
{
    public class TH_RootSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Vector3 spawnPointLeft;
        [SerializeField] private Vector3 spawnPointRight;

        private void SpawnRootRight()
        {
            Instantiate(root, spawnPointRight, Quaternion.identity);
        }

        private void SpawnRootLeft()
        {
            Instantiate(root, spawnPointLeft, Quaternion.identity);
        }
    }
}
