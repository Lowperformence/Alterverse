using UnityEngine;
namespace Alterverse.Combat
{
    // Only the strongest slow is kept; durations never add. Short post-stun immunity prevents chains.
    public sealed class HeroCombatStatus : MonoBehaviour
    {
        private float slowUntil, slowFactor = 1f, stunUntil, stunImmuneUntil;
        public bool Stunned => Time.time < stunUntil;
        public float MoveMultiplier => Stunned ? 0f : Time.time < slowUntil ? slowFactor : 1f;
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
        public void Clear() { slowUntil = stunUntil = stunImmuneUntil = 0; slowFactor = 1f; }
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
