using UnityEngine;
namespace Alterverse.Combat
{
    // Only the strongest slow is kept; durations never add. Short post-stun immunity prevents chains.
    public sealed class HeroCombatStatus : MonoBehaviour
    {
        private float slowUntil, slowFactor = 1f, stunUntil, stunImmuneUntil, speedBoostUntil, speedBoostFactor = 1f;
        private bool networkOverride,networkStunned;
        private float networkMoveMultiplier=1f;
        public bool Stunned => networkOverride?networkStunned:Time.time < stunUntil;
        public float MoveMultiplier
        {
            get
            {
                if(networkOverride)return networkStunned?0f:networkMoveMultiplier;
                if(Stunned)return 0f;
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
            speedBoostFactor=Mathf.Max(speedBoostFactor,Mathf.Clamp(factor,1f,1.5f));
            speedBoostUntil=Mathf.Max(speedBoostUntil,Time.time+Mathf.Clamp(seconds,0f,10f));
        }
        public void ApplyNetworkSnapshot(bool stunned,float moveMultiplier)
        {
            networkOverride=true;
            networkStunned=stunned;
            networkMoveMultiplier=Mathf.Clamp(moveMultiplier,0f,1.5f);
        }
        public void ClearNetworkSnapshot(){networkOverride=false;networkStunned=false;networkMoveMultiplier=1f;}
        public void Clear() { slowUntil = stunUntil = stunImmuneUntil = speedBoostUntil = 0; slowFactor = speedBoostFactor = 1f; networkOverride=false;networkStunned=false;networkMoveMultiplier=1f; }
        public static bool CanAct(GameObject actor) => actor.GetComponent<HeroCombatStatus>()?.Stunned != true;
        public static float Speed(GameObject actor) => actor.GetComponent<HeroCombatStatus>()?.MoveMultiplier ?? 1f;
        public static void Apply(Health target, float slow, float duration, float stun = 0f)
        {
            if (target == null || target.IsDead || (target.GetComponent<Alterverse.Characters.HeroIdentity>() == null && target.GetComponent<LaneMinion>() == null)) return;
            var status = target.GetComponent<HeroCombatStatus>() ?? target.gameObject.AddComponent<HeroCombatStatus>();
            if (slow < 1f) status.Slow(slow, duration);
            if (stun > 0) status.Stun(stun);
        }
    }
}
