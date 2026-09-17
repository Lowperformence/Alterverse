#if UNITY_EDITOR
using System.Collections;
using Alterverse.Combat;
using UnityEngine;

namespace Alterverse.Diagnostics
{
    // An isolated play-mode regression probe. Never included in player builds.
    public sealed class MinionCombatProbe : MonoBehaviour
    {
        private GameObject left, right;
        [UnityEditor.MenuItem("DIMENSIONS FRONT/Validation/Run Minion Combat Probe")]
        private static void Run()
        {
            if (!Application.isPlaying || Time.timeScale <= 0f)
            {
                Debug.LogError("Start an active match before running the minion combat probe.");
                return;
            }
            if (FindAnyObjectByType<MinionCombatProbe>() != null) return;
            new GameObject("DF_MinionCombatProbe").AddComponent<MinionCombatProbe>();
        }
        private IEnumerator Start()
        {
            left = Spawn(0, new Vector3(998, 1, 1000), new Vector3(1002, 1, 1000));
            right = Spawn(1, new Vector3(1002, 1, 1000), new Vector3(998, 1, 1000));
            Health a = left.GetComponent<Health>(), b = right.GetComponent<Health>();
            yield return new WaitForSeconds(4f);
            bool exchangedDamage = a != null && b != null && a.Current < a.Max && b.Current < b.Max;
            if (exchangedDamage) Debug.Log("PASS: opposing lane minions acquired each other and both dealt damage.");
            else Debug.LogError("FAIL: opposing lane minions did not exchange damage within four game seconds.");
            Destroy(gameObject);
        }
        private static GameObject Spawn(int team, Vector3 position, Vector3 destination)
        {
            GameObject unit = new GameObject("DF_ProbeMinion_" + team);
            unit.transform.position = position;
            unit.AddComponent<Health>().Configure(100);
            unit.AddComponent<LaneMinion>().Initialize(team, destination);
            return unit;
        }
        private void OnDestroy()
        {
            if (left != null) Destroy(left);
            if (right != null) Destroy(right);
        }
    }
}
#endif
