using System.Collections.Generic;
using UnityEngine;

namespace Alterverse.Combat
{
    // Only the strongest slow is kept; durations never add. Short post-stun immunity prevents chains.
    public sealed class HeroCombatStatus : MonoBehaviour
    {
        private static readonly Dictionary<GameObject, HeroCombatStatus> statusByActor = new(32);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticLookup() => statusByActor.Clear();

        private float slowUntil, slowFactor = 1f, stunUntil, stunImmuneUntil, speedBoostUntil, speedBoostFactor = 1f;
        private bool networkOverride,networkStunned;
        private float networkMoveMultiplier=1f;
        private Object rootOwner;
        private float rootUntil;
        private bool rootLocksActions;

        private void Awake()
        {
            if (gameObject != null) statusByActor[gameObject] = this;
        }

        private void OnEnable()
        {
            if (gameObject != null) statusByActor[gameObject] = this;
        }

        private void OnDestroy()
        {
            if (gameObject != null) statusByActor.Remove(gameObject);
        }

        public bool Rooted => networkOverride ? !networkStunned && networkMoveMultiplier<=.001f : rootOwner != null && Time.time < rootUntil;
        public bool ActionLocked => Rooted && rootLocksActions;
        public bool Stunned => networkOverride?networkStunned:Time.time < stunUntil;
        public float MoveMultiplier
        {
            get
            {
                if(networkOverride)return networkStunned?0f:networkMoveMultiplier;
                if(Stunned||Rooted)return 0f;
                float slow=Time.time<slowUntil?slowFactor:1f;
                float boost=Time.time<speedBoostUntil?speedBoostFactor:1f;
                return slow*boost;
            }
        }
        public void Slow(float factor, float seconds)
        {
            if (Time.time >= slowUntil) slowFactor = 1f;
            slowFactor = Mathf.Min(slowFactor, Mathf.Clamp(factor, .35f, 1f));
            slowUntil = Mathf.Max(slowUntil, Time.time + Mathf.Clamp(seconds, 0f, 3f));
        }
        public void Stun(float seconds)
        {
            if (Time.time < stunImmuneUntil) return;
            stunUntil = Time.time + Mathf.Clamp(seconds, 0f, .8f);
            stunImmuneUntil = stunUntil + 1.5f;
        }
        public void BoostSpeed(float factor,float seconds)
        {
            if (Time.time >= speedBoostUntil) speedBoostFactor = 1f;
            speedBoostFactor=Mathf.Max(speedBoostFactor,Mathf.Clamp(factor,1f,1.5f));
            speedBoostUntil=Mathf.Max(speedBoostUntil,Time.time+Mathf.Clamp(seconds,0f,10f));
        }
        // A source-owned root is separate from the existing short-stun/immunity rules.
        // Refreshes never add duration; releasing one source cannot erase another effect.
        public bool TryRoot(Object source,float seconds,bool lockActions=false)
        {
            if(source==null||(Rooted&&rootOwner!=source))return false;
            rootOwner=source;rootUntil=Time.time+Mathf.Clamp(seconds,0f,2.5f);
            rootLocksActions=lockActions;return true;
        }
        public void ReleaseRoot(Object source)
        {
            if(rootOwner!=source)return;
            rootOwner=null;rootUntil=0;rootLocksActions=false;
        }
        public void ApplyNetworkSnapshot(bool stunned,float moveMultiplier)
        {
            networkOverride=true;
            networkStunned=stunned;
            networkMoveMultiplier=Mathf.Clamp(moveMultiplier,0f,1.5f);
        }
        public void ClearNetworkSnapshot(){networkOverride=false;networkStunned=false;networkMoveMultiplier=1f;}
        public void Clear() { slowUntil = stunUntil = stunImmuneUntil = speedBoostUntil = rootUntil = 0; rootOwner=null;rootLocksActions=false;slowFactor = speedBoostFactor = 1f; networkOverride=false;networkStunned=false;networkMoveMultiplier=1f; }

        private static HeroCombatStatus FindStatus(GameObject actor)
        {
            if (actor == null) return null;
            if (statusByActor.TryGetValue(actor, out HeroCombatStatus cached))
            {
                if (cached != null) return cached;
                statusByActor.Remove(actor);
            }
            return null;
        }

        public static bool CanAct(GameObject actor)
        {
            var status = FindStatus(actor);
            return status == null || !status.Stunned && !status.ActionLocked;
        }

        public static float Speed(GameObject actor) => FindStatus(actor)?.MoveMultiplier ?? 1f;

        public static void Apply(Health target, float slow, float duration, float stun = 0f)
        {
            if (target == null || target.IsDead || (target.GetComponent<Alterverse.Characters.HeroIdentity>() == null && target.GetComponent<LaneMinion>() == null)) return;
            var status = FindStatus(target.gameObject) ?? target.GetComponent<HeroCombatStatus>() ?? target.gameObject.AddComponent<HeroCombatStatus>();
            statusByActor[target.gameObject] = status;
            if (slow < 1f) status.Slow(slow, duration);
            if (stun > 0) status.Stun(stun);
        }
    }
}
