using UnityEngine;
using Alterverse.Characters;
using Alterverse.Combat;
namespace Alterverse.AI
{
    [RequireComponent(typeof(Health), typeof(BotTargetSensor), typeof(HeroIdentity))]
    [RequireComponent(typeof(HeroAbilityKit))]
    public sealed class HeroBotAbilities : MonoBehaviour
    {
        private Health self;
        private BotTargetSensor sensor;
        private HeroIdentity identity;
        private HeroAbilityKit kit;
        private HeroAbilityExecution execution;
        private MobaBotBrain brain;
        private float nextDecision;
        private void Awake()
        {
            self = GetComponent<Health>(); sensor = GetComponent<BotTargetSensor>();
            identity = GetComponent<HeroIdentity>(); kit = GetComponent<HeroAbilityKit>(); brain = GetComponent<MobaBotBrain>();
            execution = GetComponent<HeroAbilityExecution>() ?? gameObject.AddComponent<HeroAbilityExecution>();
            nextDecision = Time.time + Random.Range(.7f,1.8f);
        }
        private void Update()
        {
            if (Time.timeScale <= 0 || self.IsDead || !HeroCombatStatus.CanAct(gameObject) || Time.time < nextDecision) return;
            var difficulty=brain!=null?brain.Difficulty:BotDifficultyProfile.For(BotDifficulty.Normal);
            float rhythm=brain!=null?brain.DecisionRhythm:1f;
            nextDecision = Time.time + Random.Range(.28f,.48f)*difficulty.DecisionScale*rhythm;
            Health target = sensor.CurrentTarget;
            HeroId h = identity.Hero;
            bool fighting = brain == null || brain.State == MobaBotBrain.BotState.Fight;
            float distance = target != null ? HeroCombatRules.Distance(transform.position,target.transform.position) : float.MaxValue;
            float discipline=difficulty.AbilityDiscipline;
            if (h == HeroId.Mira)
            {
                Health ally = execution.WoundedAlly(kit.Ultimate.Radius);
                float supportUltThreshold=Mathf.Lerp(.30f,.62f,discipline);
                if (ally != null && ally.Normalized < supportUltThreshold && TryCast(3,target)) return;
                if (TryCast(1,target) || TryCast(2,target)) return;
            }
            float defensiveThreshold=Mathf.Lerp(.55f,.82f,discipline);
            if ((h == HeroId.Kaelon || h == HeroId.Rook) && self.Normalized < defensiveThreshold && TryCast(1,target)) return;
            if (target == null) return;
            if (distance < 3.5f && ((h == HeroId.Nyra && TryCast(1,target)) || (h == HeroId.Elyra && TryCast(2,target)))) return;
            if (h == HeroId.Veyr && ((!fighting && self.Normalized < .5f) || (fighting && distance > 3.1f && distance < 7f)) && TryCast(2,target)) return;
            if (!fighting) return;
            // Missed opportunities delay a decision, never reset or shorten real cooldowns.
            if(brain!=null&&brain.Chance(difficulty.AbilityMistake))return;
            bool isHero=target.GetComponent<HeroIdentity>()!=null;
            bool ultimateWindow;
            if(h==HeroId.Veyr)
                ultimateWindow=isHero&&target.Normalized<Mathf.Lerp(.35f,.62f,discipline);
            else if(h==HeroId.Kaelon)
                ultimateWindow=isHero&&distance<=kit.Ultimate.Radius&&
                    (discipline<.70f||sensor.NearbyEnemies>=2||target.Normalized<.45f);
            else
                ultimateWindow=isHero&&target.Normalized<Mathf.Lerp(.45f,.78f,discipline);

            if(discipline>=.82f&&isHero&&sensor.NearbyEnemies>=2&&h!=HeroId.Veyr&&h!=HeroId.Kaelon)
                ultimateWindow=true;
            if(h==HeroId.Rook)ultimateWindow=ultimateWindow&&distance<=kit.Ultimate.Radius;
            if (h != HeroId.Mira && ultimateWindow && TryCast(3,target)) return;
            if (TryCast(0,target)) return;
            if ((h == HeroId.Veyr || h == HeroId.Elyra) && TryCast(1,target)) return;
            if ((h == HeroId.Kaelon || h == HeroId.Nyra || h == HeroId.Rook) && distance <= Mathf.Max(kit.E.Range,kit.E.Radius)) TryCast(2,target);
        }
        private bool TryCast(int slot, Health target)
        {
            Vector3 direction = target != null ? transform.position-target.transform.position : transform.forward;
            if (identity.Hero == HeroId.Veyr && (brain == null || brain.State == MobaBotBrain.BotState.Fight)) direction = -direction;
            return execution.TryCast(slot,target,direction);
        }
    }
}
