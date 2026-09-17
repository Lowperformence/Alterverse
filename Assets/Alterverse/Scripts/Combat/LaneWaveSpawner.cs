using System.Collections;
using UnityEngine;

namespace Alterverse.Combat
{
    public sealed class LaneWaveSpawner : MonoBehaviour
    {
        [SerializeField] private float firstWaveDelay = 3f;
        [SerializeField] private float waveInterval = 22f;
        [SerializeField] private float unitSpacing = 1.1f;
        [SerializeField] private int unitsPerWave = 3;

        private static readonly float[] LaneZ = { 12f, 0f, -12f };
        private static readonly Color ZeroColor = new(0.38f, 0.08f, 0.58f);
        private static readonly Color LumenColor = new(0.06f, 0.58f, 0.64f);

        private IEnumerator Start()
        {
            yield return new WaitForSeconds(firstWaveDelay);
            while (true)
            {
                SpawnAllLanes();
                yield return new WaitForSeconds(waveInterval);
            }
        }

        private void SpawnAllLanes()
        {
            foreach (float laneZ in LaneZ)
            {
                SpawnWave(0, laneZ);
                SpawnWave(1, laneZ);
            }
        }

        private void SpawnWave(int team, float laneZ)
        {
            float startX = team == 0 ? -22f : 22f;
            float destinationX = team == 0 ? 27f : -27f;
            float direction = team == 0 ? -1f : 1f;

            for (int i = 0; i < unitsPerWave; i++)
            {
                Vector3 spawn = new(startX + direction * i * unitSpacing, 0.55f, laneZ);
                CreateMinion(team, spawn, new Vector3(destinationX, 0.55f, laneZ), i);
            }
        }

        private static void CreateMinion(int team, Vector3 spawn, Vector3 destination, int index)
        {
            GameObject minion = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            minion.name = team == 0 ? $"AV_ZeroMinion_{index}" : $"AV_LumenMinion_{index}";
            minion.transform.position = spawn;
            minion.transform.localScale = new Vector3(0.55f, 0.55f, 0.55f);

            Health health = minion.AddComponent<Health>();
            health.Configure(55f, true);

            LaneMinion laneMinion = minion.AddComponent<LaneMinion>();
            laneMinion.Initialize(team, destination);

            Renderer renderer = minion.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = team == 0 ? ZeroColor : LumenColor;

            Object.Destroy(minion, 55f);
        }
    }
}
