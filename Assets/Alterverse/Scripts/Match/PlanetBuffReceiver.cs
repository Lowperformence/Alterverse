using Alterverse.Combat;
using UnityEngine;

namespace Alterverse.Match
{
    public enum PlanetBuffType { Flux, Guard }

    public enum MidMatchPerkType
    {
        None = 0,
        Movement = 1,
        Damage = 2,
        MaxHealth = 3
    }

    [RequireComponent(typeof(Health))]
    public sealed class PlanetBuffReceiver : MonoBehaviour
    {
        private float fluxUntil, guardUntil;
        private MidMatchPerkType selectedPerk;

        public const float FluxDamageBonus = .15f, GuardDamageReduction = .20f;
        public const float MidMatchMoveBonus = .15f;
        public const float MidMatchDamageBonus = .15f;
        public const float MidMatchHealthBonus = .15f;

        public bool FluxActive => Time.time < fluxUntil;
        public bool GuardActive => Time.time < guardUntil;
        public float FluxRemaining => Mathf.Max(0, fluxUntil - Time.time);
        public float GuardRemaining => Mathf.Max(0, guardUntil - Time.time);
        public float ActiveRemaining => Mathf.Max(FluxRemaining, GuardRemaining);

        public MidMatchPerkType SelectedPerk => selectedPerk;
        public bool HasSelectedMidMatchPerk => selectedPerk != MidMatchPerkType.None;

        public float CooldownMultiplier => 1f;

        // Read automatically by HeroMotor.MoveSpeed and MobaBotMover.MoveSpeed
        public float MoveSpeedMultiplier =>
            selectedPerk == MidMatchPerkType.Movement ? 1f + MidMatchMoveBonus : 1f;

        // Read automatically by Health.TakeDamage for all basic attacks, skillshots & abilities
        public float OutgoingDamageMultiplier =>
            (FluxActive ? 1f + FluxDamageBonus : 1f) *
            (selectedPerk == MidMatchPerkType.Damage ? 1f + MidMatchDamageBonus : 1f);

        public float DamageTakenMultiplier => GuardActive ? 1f - GuardDamageReduction : 1f;

        public string ActiveLabel =>
            FluxActive && GuardActive ? "FLUX + GUARD" :
            FluxActive ? "FLUX • +15% DAMAGE" :
            GuardActive ? "GUARD • -20% DAMAGE TAKEN" : "";

        public string MidMatchPerkLabel => selectedPerk switch
        {
            MidMatchPerkType.Movement => "PASSIVE • +15% MOVEMENT",
            MidMatchPerkType.Damage => "PASSIVE • +15% DAMAGE",
            MidMatchPerkType.MaxHealth => "PASSIVE • +15% MAX HP",
            _ => ""
        };

        public void Apply(PlanetBuffType type, float duration)
        {
            float until = Time.time + Mathf.Max(1, duration);
            if (type == PlanetBuffType.Flux) fluxUntil = Mathf.Max(fluxUntil, until);
            else guardUntil = Mathf.Max(guardUntil, until);
        }

        public bool SelectMidMatchPerk(MidMatchPerkType perk)
        {
            if (perk == MidMatchPerkType.None || selectedPerk != MidMatchPerkType.None)
                return false;

            selectedPerk = perk;

            if (perk == MidMatchPerkType.MaxHealth)
            {
                Health health = GetComponent<Health>();
                if (health != null)
                    health.ApplyMidMatchVitalityBonus(MidMatchHealthBonus);
            }

            if (Alterverse.Characters.LocalHeroContext.IsLocal(gameObject))
                Alterverse.Audio.DimensionAudio.Play(Alterverse.Audio.DimensionSound.Heal, .55f);

            return true;
        }

        public void ResetMidMatchPerk()
        {
            selectedPerk = MidMatchPerkType.None;
        }
    }
}
