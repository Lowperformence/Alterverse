using UnityEngine;

namespace Alterverse.Visuals
{
    // This one purchased composition belongs only to the neutral bomber detonation.
    public sealed class BomberNukeVfx : MonoBehaviour
    {
        public const string ResourcePath = "CombatVfx/BomberNuke/Explosion";
        public const string ImportVersion = "Bomber Nuke Cone 4";
        [HideInInspector] public float nativeRadius = 1f;
        [HideInInspector] public string sourceSignature;
        [HideInInspector] public float emissionStopAfter = .1f;
        private static GameObject prefab;
        private static bool warned;
        private static bool reported;
        private ParticleSystem[] systems;
        private float started;
        private bool stoppedEmission;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic() { prefab = null; warned = false; reported = false; }

        public static bool TryPlay(Vector3 position, float radius)
        {
            if (!(radius > 0f) || float.IsInfinity(radius)) return false;
            if (prefab == null) prefab = Resources.Load<GameObject>(ResourcePath);
            var definition = prefab != null ? prefab.GetComponent<BomberNukeVfx>() : null;
            if (definition == null || !(definition.nativeRadius > 0f) ||
                float.IsInfinity(definition.nativeRadius))
            {
                if (!warned) Debug.LogWarning("DIMENSIONS FRONT: bomber Nuke Cone is not prepared; keeping the original explosion. Reimport the purchased NukeConeExplosionFire prefab in Edit Mode.");
                warned = true;
                return false;
            }

            // The saved root is inactive: place, scale and clip before any particles start.
            var instance = Instantiate(prefab, position, Quaternion.identity);
            instance.name = "DF_Bomber_NukeConeExplosion";
            instance.transform.localScale = Vector3.one * (radius / definition.nativeRadius);
            var block = new MaterialPropertyBlock();
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.GetPropertyBlock(block);
                block.SetVector("_AttackClip", new Vector4(position.x, position.z, radius, 1f));
                renderer.SetPropertyBlock(block);
            }
            instance.SetActive(true);
            if (!reported)
            {
                Debug.Log("DIMENSIONS FRONT VFX: NukeConeExplosionFire ACTIVE; radius=" + radius +
                    "; nativeRadius=" + definition.nativeRadius + "; scale=" + instance.transform.localScale.x + "; " + definition.sourceSignature);
                reported = true;
            }
            return true;
        }

        private void Awake()
        {
            systems = GetComponentsInChildren<ParticleSystem>(true);
            started = Time.time;
        }

        private void LateUpdate()
        {
            if (Time.time - started < .1f) return;
            if(!stoppedEmission&&Time.time-started>=emissionStopAfter)
            {
                // Stop only the scheduled emitters. A sub-emitter that is triggered by
                // a parent particle's later death must remain available for that event.
                foreach(var system in systems)
                    if(system!=null&&system.main.playOnAwake)system.Stop(false,ParticleSystemStopBehavior.StopEmitting);
                stoppedEmission=true;
            }
            // Do not cut off delayed layers, sub-emitters or smoke tails with a slot timer.
            foreach (var system in systems)
                if (system != null && system.gameObject.activeInHierarchy && system.IsAlive(true)) return;
            Destroy(gameObject);
        }
    }
}
