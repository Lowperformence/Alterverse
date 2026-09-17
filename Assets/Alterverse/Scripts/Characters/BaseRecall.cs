using System.Collections;
using Alterverse.Combat;
using UnityEngine;

namespace Alterverse.Characters
{
    [RequireComponent(typeof(Health))]
    public sealed class BaseRecall : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float channelDuration = 5f;
        [SerializeField] private float movementCancelDistance = 0.18f;

        private Health health;
        private Vector3 spawnPoint;
        private Vector3 channelStart;
        private float healthAtStart;
        private Coroutine recallRoutine;

        public bool IsRecalling => recallRoutine != null;
        public float Progress { get; private set; }

        private void Awake()
        {
            health = GetComponent<Health>();
            spawnPoint = transform.position;
            health.Died += CancelRecall;
        }

        private void OnDestroy()
        {
            if (health != null) health.Died -= CancelRecall;
        }

        public void BeginRecall()
        {
            if (health == null || health.IsDead || IsRecalling) return;
            recallRoutine = StartCoroutine(RecallRoutine());
        }

        public void CancelRecall()
        {
            if (recallRoutine != null) StopCoroutine(recallRoutine);
            recallRoutine = null;
            Progress = 0f;
        }

        private IEnumerator RecallRoutine()
        {
            channelStart = transform.position;
            healthAtStart = health.Current;
            Progress = 0f;

            while (Progress < 1f)
            {
                if (health.IsDead || health.Current < healthAtStart || HorizontalDistance(channelStart, transform.position) > movementCancelDistance)
                {
                    recallRoutine = null;
                    Progress = 0f;
                    yield break;
                }

                Progress += Time.deltaTime / channelDuration;
                yield return null;
            }

            CharacterController controller = GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            transform.position = spawnPoint;
            if (controller != null) controller.enabled = true;

            recallRoutine = null;
            Progress = 0f;
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
