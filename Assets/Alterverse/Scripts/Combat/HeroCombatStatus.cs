using UnityEngine;
namespace Alterverse.Combat
{
    // Only the strongest slow is kept; durations never add. Short post-stun immunity prevents chains.
    public sealed class HeroCombatStatus : MonoBehaviour
    {
        private float slowUntil, slowFactor = 1f, stunUntil, stunImmuneUntil, speedBoostUntil, speedBoostFactor = 1f;
        public bool Stunned => Time.time < stunUntil;
        public float MoveMultiplier
        {
            get
            {
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
        public void Clear() { slowUntil = stunUntil = stunImmuneUntil = speedBoostUntil = 0; slowFactor = speedBoostFactor = 1f; }
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
