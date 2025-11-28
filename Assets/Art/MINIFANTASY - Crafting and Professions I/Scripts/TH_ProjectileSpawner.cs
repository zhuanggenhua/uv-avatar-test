using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Minifantasy.TrueHeroes
{
    public class TH_ProjectileSpawner : MonoBehaviour
    {
        [SerializeField] private TH_Projectile projectile;
        [SerializeField] private List<Vector3> groupSpawnPoints = new List<Vector3>();
        [SerializeField] private Vector3 spawnPointLeft;
        [SerializeField] private Vector3 spawnPointRight;

        private void SpawnAllProjectiles()
        {
            foreach (Vector3 spawnPoint in groupSpawnPoints)
            {
                Instantiate(projectile, spawnPoint, Quaternion.identity);
            }
        }

        private void SpawnProjectileRight()
        {
            Instantiate(projectile, spawnPointRight, Quaternion.identity);
        }

        private void SpawnProjectileLeft()
        {
            Instantiate(projectile, spawnPointLeft, Quaternion.identity);
        }
    }
}
