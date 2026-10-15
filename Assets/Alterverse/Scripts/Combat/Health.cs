using System;
using UnityEngine;
using Alterverse.Match;

namespace Alterverse.Combat
{
    public sealed class Health : MonoBehaviour
    {
        private float HeroBaseShieldCapacity => GetComponent<Alterverse.Characters.HeroIdentity>()?.Hero switch
        {
            Alterverse.Characters.HeroId.VoidKnight => 60f,
            Alterverse.Characters.HeroId.Nerok => 90f,
            Alterverse.Characters.HeroId.Veyr => 70f,
            Alterverse.Characters.HeroId.Kaelon => 70f,
            Alterverse.Characters.HeroId.Rook => 80f,
            Alterverse.Characters.HeroId.Timmy => Alterverse.Characters.TimmyBalance.BaseShield,
            Alterverse.Characters.HeroId.Nyra => 30f,
            _ => 50f
        };
        private const float HeroBaseShieldRechargeDelay = 15f;
        private const float AurenPassiveTickSeconds = 1f;

        private static readonly System.Collections.Generic.List<Health> active = new(128);
        public static System.Collections.Generic.IReadOnlyList<Health> Active => active;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => active.Clear();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RebuildRegistry()
        {
            active.Clear();
            foreach (Health h in FindObjectsByType<Health>())
                if (h.isActiveAndEnabled)
                    active.Add(h);
        }

        private void OnEnable()
        {
            if (!active.Contains(this))
                active.Add(this);
        }

        private void OnDisable()
        {
            aurenPassiveElapsed = 0f;
            active.Remove(this);
        }

        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField] private bool destroyOnDeath;

        // Existing short-lived ability/buff shield.
        private float shield;
        private float shieldUntil;

        // Independent rechargeable base shield; capacity depends on the hero.
        private bool heroBaseShieldEnabled;
        private float heroBaseShield;
        private float heroBaseShieldRechargeAt = -1f;

        // Separate from the rechargeable base shield; this passive restores health only.
        private bool aurenPassiveEnabled;
        private float aurenPassiveElapsed;

        private float TransientShield => Time.time < shieldUntil ? shield : 0f;
        /// <summary>Ability shield only, excluding the permanent regenerating hero shield.</summary>
        public float TemporaryShield => TransientShield;

        /// <summary>Total currently active shield, including temporary ability shields.</summary>
        public float Shield => TransientShield + heroBaseShield;
        public float HeroBaseShield => heroBaseShield;
        public float HeroBaseShieldMax => heroBaseShieldEnabled ? HeroBaseShieldCapacity : 0f;
        public float HeroBaseShieldRechargeRemaining =>
            heroBaseShieldEnabled && heroBaseShield < HeroBaseShieldCapacity && heroBaseShieldRechargeAt >= 0f
                ? Mathf.Max(0f, heroBaseShieldRechargeAt - Time.time)
                : 0f;

        public void AddShield(float amount, float duration) => AddShield(amount, duration, true);

        public void AddShield(float amount, float duration, bool playAudio)
        {
            if (!Alterverse.Multiplayer.CoopAuthority.CanMutateAuthoritativeState || IsDead || amount <= 0)
                return;

            // Keep temporary shields independent from the permanent hero base shield.
            float previousShield=TransientShield;
            shield = Mathf.Max(TransientShield, Mathf.Min(amount, maxHealth * .4f));
            shieldUntil = Mathf.Max(shieldUntil, Time.time + Mathf.Max(0, duration));
            if (playAudio && shield > previousShield)
                Alterverse.Audio.DimensionAudio.PlayAt(Alterverse.Audio.DimensionSound.Shield, transform.position, .3f);
            Changed?.Invoke(Current, maxHealth);
        }

        private void ClearTransient()
        {
            shield = shieldUntil = 0;
            GetComponent<HeroCombatStatus>()?.Clear();
        }

        private void ConfigureHeroBaseShield(bool full)
        {
            var hero = GetComponent<Alterverse.Characters.HeroIdentity>();
            heroBaseShieldEnabled = hero != null;
            aurenPassiveEnabled = hero != null && hero.Hero == Alterverse.Characters.HeroId.Kaelon;
            aurenPassiveElapsed = 0f;
            heroBaseShield = heroBaseShieldEnabled && full ? HeroBaseShieldCapacity : 0f;
            heroBaseShieldRechargeAt = -1f;
        }

        private void RefreshHeroBaseShield()
        {
            if (!Alterverse.Multiplayer.CoopAuthority.CanMutateAuthoritativeState ||
                IsDead ||
                !heroBaseShieldEnabled ||
                heroBaseShield >= HeroBaseShieldCapacity ||
                heroBaseShieldRechargeAt < 0f ||
                Time.time < heroBaseShieldRechargeAt)
                return;

            heroBaseShield = HeroBaseShieldCapacity;
            heroBaseShieldRechargeAt = -1f;
            Changed?.Invoke(Current, maxHealth);
        }

        public float Current { get; private set; }
        public float Max => maxHealth;
        public bool IsDead { get; private set; }
        public float Normalized => maxHealth <= 0 ? 0 : Current / maxHealth;
        public GameObject LastDamageSource { get; private set; }

        public event Action<float, float> Changed;
        public event Action<float, float, GameObject> Damaged;
        public event Action Died;
        public event Action<Health, GameObject> DiedWithSource;
        public Func<bool> DamageGate { get; set; }

        private void Awake()
        {
            Current = maxHealth;
            ConfigureHeroBaseShield(true);
        }

        private void Update()
        {
            RefreshHeroBaseShield();
            RefreshAurenPassive();
        }

        private void RefreshAurenPassive()
        {
            // Paused time, dead heroes, remote clients and full health never grant regeneration.
            // An elapsed-time accumulator keeps healing at 1 tick/second and avoids per-frame
            // Changed events, telemetry writes and network state churn.
            if (!aurenPassiveEnabled || IsDead ||
                !Alterverse.Multiplayer.CoopAuthority.CanMutateAuthoritativeState ||
                Current >= maxHealth)
            {
                aurenPassiveElapsed = 0f;
                return;
            }

            aurenPassiveElapsed += Time.deltaTime;
            if (aurenPassiveElapsed < AurenPassiveTickSeconds) return;
            int ticks = Mathf.FloorToInt(aurenPassiveElapsed / AurenPassiveTickSeconds);
            aurenPassiveElapsed -= ticks * AurenPassiveTickSeconds;
            Heal(ticks * Alterverse.Characters.HeroAbilityKit.AurenPassiveRegenPerSecond, false);
        }

        public void Configure(float newMaxHealth, bool shouldDestroyOnDeath = false)
        {
            maxHealth = Mathf.Max(1, newMaxHealth);
            destroyOnDeath = shouldDestroyOnDeath;
            ClearTransient();
            ConfigureHeroBaseShield(true);
            IsDead = false;
            Current = maxHealth;
            LastDamageSource = null;
            Changed?.Invoke(Current, maxHealth);
        }

        public void TakeDamage(float amount) => TakeDamage(amount, null);

        public void TakeDamage(float amount, GameObject source)
        {
            if (!Alterverse.Multiplayer.CoopAuthority.CanMutateAuthoritativeState ||
                IsDead ||
                amount <= 0 ||
                IsFriendlySource(source) ||
                (DamageGate != null && !DamageGate()))
                return;

            // Apply Brakk's 5% bonus once at the damage boundary, including abilities/projectiles.
            var attackerHero = source != null ? source.GetComponentInParent<Alterverse.Characters.HeroIdentity>() : null;
            if (attackerHero != null && attackerHero.Hero == Alterverse.Characters.HeroId.Rook)
                amount *= 1.05f;

            // Flux boosts all outgoing damage from a member of the capturing team, including
            // abilities and minion attacks that bypass HeroCombatRules.Damage.
            PlanetBuffReceiver attackerBuff = source != null
                ? source.GetComponentInParent<PlanetBuffReceiver>() : null;
            if (attackerBuff != null)
                amount *= attackerBuff.OutgoingDamageMultiplier;

            Alterverse.AI.MobaBotBrain bot = GetComponent<Alterverse.AI.MobaBotBrain>();
            TeamMember team = GetComponent<TeamMember>();
            if (bot != null && team != null && team.Team == 1)
                amount *= bot.Difficulty.DamageTakenMultiplier;

            PlanetBuffReceiver buff = GetComponent<PlanetBuffReceiver>();
            if (buff != null)
                amount *= buff.DamageTakenMultiplier;

            amount=GetComponent<Alterverse.Characters.VoidKnightState>()?.ReduceFrontalDamage(amount,source)??amount;

            // If 15 seconds have already elapsed, the shield is restored before this hit lands.
            RefreshHeroBaseShield();

            // Any valid incoming hit restarts the recharge delay while the hero shield is damaged.
            if (heroBaseShieldEnabled && heroBaseShield < HeroBaseShieldCapacity)
                heroBaseShieldRechargeAt = Time.time + HeroBaseShieldRechargeDelay;

            // Temporary ability shields are consumed first so an expiring buff is not wasted.
            float transientAbsorbed = Mathf.Min(TransientShield, amount);
            shield = TransientShield - transientAbsorbed;
            amount -= transientAbsorbed;

            float heroAbsorbed = Mathf.Min(heroBaseShield, amount);
            heroBaseShield -= heroAbsorbed;
            amount -= heroAbsorbed;

            // A hit that actually reduced the hero shield starts/restarts the 15 second delay.
            if (heroBaseShieldEnabled && (heroAbsorbed > 0f || heroBaseShield < HeroBaseShieldCapacity))
                heroBaseShieldRechargeAt = Time.time + HeroBaseShieldRechargeDelay;

            float absorbed = transientAbsorbed + heroAbsorbed;
            float applied = Mathf.Min(amount, Current);

            if (source != null)
            {
                LastDamageSource = source;
                MatchScoreboard.Instance?.RecordDamage(this, source);
                MatchTelemetry.Instance?.RecordDamage(this, source, applied);
            }

            Current = Mathf.Max(0, Current - amount);
            Changed?.Invoke(Current, maxHealth);
            Damaged?.Invoke(applied, absorbed, source);

            if (applied > 0 && GetComponent<RealityCore>() != null)
                Alterverse.Audio.DimensionAudio.PlayAt(Alterverse.Audio.DimensionSound.CoreHit, transform.position, .65f);

            if (Current <= 0)
            {
                if (GetComponent<LaneMinion>() != null)
                    Alterverse.Audio.DimensionAudio.PlayAt(Alterverse.Audio.DimensionSound.MinionDeath, transform.position, .22f);

                if (GetComponent<Alterverse.Characters.HeroIdentity>() != null)
                {
                    Alterverse.Audio.DimensionAudio.Play(Alterverse.Audio.DimensionSound.HeroDeath, .78f);
                    var killer = source != null ? source.GetComponentInParent<Alterverse.Characters.HeroIdentity>() : null;
                    if (killer != null && killer.gameObject != gameObject)
                        Alterverse.Audio.HeroCombatAudio.ConfirmedKill(killer.gameObject);
                    FractureCorePickup.Spawn(transform.position);
                }

                IsDead = true;
                ClearTransient();
                ConfigureHeroBaseShield(false);
                Died?.Invoke();
                DiedWithSource?.Invoke(this, LastDamageSource);
                MatchTelemetry.Instance?.RecordDeath(this, LastDamageSource);
                if (destroyOnDeath)
                    Destroy(gameObject);
            }
        }

        private bool IsFriendlySource(GameObject source)
        {
            if (source == null)
                return false;

            TeamMember victimTeam = GetComponent<TeamMember>();
            TeamMember sourceTeam = source.GetComponentInParent<TeamMember>();
            return victimTeam != null && sourceTeam != null && victimTeam.Team == sourceTeam.Team;
        }

        public void Heal(float amount) => Heal(amount, true);

        public void Heal(float amount, bool playAudio)
        {
            if (!Alterverse.Multiplayer.CoopAuthority.CanMutateAuthoritativeState || IsDead || amount <= 0)
                return;

            float applied = Mathf.Min(amount, maxHealth - Current);
            Current = Mathf.Min(maxHealth, Current + amount);
            if (applied > 0)
            {
                MatchTelemetry.Instance?.RecordHealing(this, applied);
                if (playAudio)
                    Alterverse.Audio.DimensionAudio.PlayAt(Alterverse.Audio.DimensionSound.Heal, transform.position, .18f);
            }
            Changed?.Invoke(Current, maxHealth);
        }

        public void ApplyNetworkSnapshot(float current, float currentShield, bool dead)
        {
            bool wasDead = IsDead;
            Current = Mathf.Clamp(current, 0, maxHealth);

            // Clients display the host's combined shield snapshot as one presentation value.
            heroBaseShieldEnabled = GetComponent<Alterverse.Characters.HeroIdentity>() != null;
            heroBaseShield = 0f;
            heroBaseShieldRechargeAt = -1f;
            float snapshotShieldLimit = maxHealth * .4f + (heroBaseShieldEnabled ? HeroBaseShieldCapacity : 0f);
            shield = Mathf.Clamp(currentShield, 0, snapshotShieldLimit);
            shieldUntil = shield > 0 ? Time.time + .35f : 0;

            IsDead = dead || Current <= 0;
            if (!wasDead && IsDead && GetComponent<Alterverse.Characters.HeroIdentity>() != null)
                Alterverse.Audio.DimensionAudio.Play(Alterverse.Audio.DimensionSound.HeroDeath, .78f);
            if (!IsDead && Current <= 0)
                Current = Mathf.Min(1, maxHealth);
            Changed?.Invoke(Current, maxHealth);
        }

        public void RestoreFull()
        {
            if (!Alterverse.Multiplayer.CoopAuthority.CanMutateAuthoritativeState)
                return;

            ClearTransient();
            ConfigureHeroBaseShield(true);
            IsDead = false;
            Current = maxHealth;
            LastDamageSource = null;
            Changed?.Invoke(Current, maxHealth);
        }
    }
}
