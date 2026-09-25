using Alterverse.Characters;
using Alterverse.Combat;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Presentation-only reinforcement for ability casts. Every successful hero ability gets an
    /// unmistakable release cue without changing damage, cooldown, range, targeting or hit timing.
    /// The existing hero-specific VFX remain the primary effect; this layer guarantees that a cast
    /// can still be read on a small mobile screen when the battlefield is busy.
    /// </summary>
    [DefaultExecutionOrder(1520)]
    public sealed class HeroAbilityReadabilityBoost : MonoBehaviour
    {
        private HeroIdentity identity;
        private HeroAbilityKit kit;
        private HeroAbilityExecution abilities;
        private HeroVisualMotion motion;
        private Health health;
        private readonly float[] previous = new float[4];
        private bool primed;

        private void Awake()
        {
            identity = GetComponent<HeroIdentity>();
            kit = GetComponent<HeroAbilityKit>();
            abilities = GetComponent<HeroAbilityExecution>();
            motion = GetComponent<HeroVisualMotion>();
            health = GetComponent<Health>();
        }

        private void Start() => Prime();

        private void Prime()
        {
            identity ??= GetComponent<HeroIdentity>();
            kit ??= GetComponent<HeroAbilityKit>();
            abilities ??= GetComponent<HeroAbilityExecution>();
            motion ??= GetComponent<HeroVisualMotion>();
            health ??= GetComponent<Health>();
            for (int i = 0; i < previous.Length; i++)
                previous[i] = abilities != null ? abilities.Remaining(i) : 0f;
            primed = true;
        }

        private void Update()
        {
            if (Time.timeScale <= 0f || health != null && health.IsDead) return;
            if (!primed || identity == null || kit == null || abilities == null) { Prime(); return; }

            for (int slot = 0; slot < 4; slot++)
            {
                float current = abilities.Remaining(slot);
                float cooldown = Mathf.Max(.1f, abilities.Cooldown(slot));
                // Match the real cooldown jump used by the existing presentation layer. The initial
                // ultimate lock is already present when Prime runs, so it cannot create a false cast.
                if (current > cooldown * .68f && previous[slot] < cooldown * .22f)
                    EmitCastConfirmation(identity.Hero, slot, kit.Spec(slot));
                previous[slot] = current;
            }
        }

        private Vector3 ReleasePoint()
        {
            if (motion != null) return motion.ReleasePoint;
            return transform.position + Vector3.up * .65f + transform.forward * .32f;
        }

        private void EmitCastConfirmation(HeroId hero, int slot, AbilitySpec spec)
        {
            Color accent = StylizedArtDirection.HeroAccent(hero);
            Color hot = Color.Lerp(accent, Color.white, .62f);
            bool ultimate = slot == 3;
            float strength = ultimate ? 1.35f : 1f;
            Vector3 chest = transform.position + Vector3.up * .62f;
            Vector3 ground = transform.position + Vector3.up * .055f;
            Vector3 muzzle = ReleasePoint();
            Quaternion facing = Quaternion.LookRotation(transform.forward.sqrMagnitude > .001f ? transform.forward : Vector3.forward);

            // Universal confirmation: bright core at the release point plus a ground pulse. These
            // are deliberately larger/longer than the old micro flashes so the player always sees
            // that the button press became a real cast.
            HeroVfxPool.Emit(HeroFxShape.Halo, muzzle, muzzle, Vector3.one * (.62f * strength), hot,
                ultimate ? .30f : .22f, HeroFxMotion.Expand, facing, gameObject);
            HeroVfxPool.Emit(HeroFxShape.Ring, ground, ground,
                new Vector3(.92f * strength, .055f, .92f * strength), new Color(accent.r, accent.g, accent.b, .92f),
                ultimate ? .38f : .27f, HeroFxMotion.Expand, Quaternion.identity, gameObject);

            switch (hero)
            {
                case HeroId.Veyr:
                    if (slot == 2)
                    {
                        HeroVfxPool.Emit(HeroFxShape.Rift, chest, chest + transform.forward * 1.15f,
                            new Vector3(.82f, 1.30f, .82f) * strength, accent, .25f, HeroFxMotion.Travel, facing, gameObject);
                    }
                    else
                    {
                        HeroVfxPool.Emit(HeroFxShape.Slash, chest + transform.forward * .35f, Vector3.zero,
                            new Vector3(2.15f, .18f, 2.15f) * strength, hot, .24f, HeroFxMotion.Sweep, facing, gameObject);
                        if (ultimate)
                            HeroVfxPool.Emit(HeroFxShape.Rift, chest + transform.forward * .55f, Vector3.zero,
                                new Vector3(1.05f, 1.75f, 1.05f), accent, .32f, HeroFxMotion.Contract, facing, gameObject);
                    }
                    break;

                case HeroId.Kaelon:
                    HeroVfxPool.Emit(HeroFxShape.Hex, chest, Vector3.zero, Vector3.one * (.92f * strength), hot,
                        .28f, HeroFxMotion.Contract, Quaternion.Euler(28f, transform.eulerAngles.y, 0f), gameObject);
                    if (slot == 2 || ultimate)
                        HeroVfxPool.Emit(HeroFxShape.ShockDome, ground, Vector3.zero,
                            new Vector3((ultimate ? 1.65f : 1.25f), ultimate ? .75f : .48f, (ultimate ? 1.65f : 1.25f)),
                            new Color(accent.r, accent.g, accent.b, .72f), .30f, HeroFxMotion.Expand, owner: gameObject);
                    break;

                case HeroId.Nyra:
                    {
                        Color shot = new Color(1f, .075f, .035f, 1f);
                        HeroVfxPool.Emit(HeroFxShape.Bolt, muzzle, muzzle + transform.forward * (ultimate ? 1.7f : 1.05f),
                            new Vector3(.34f, .34f, ultimate ? 1.35f : .90f), Color.Lerp(shot, Color.white, .35f),
                            .18f, HeroFxMotion.Travel, facing, gameObject);
                        HeroVfxPool.Emit(HeroFxShape.Burst, muzzle, Vector3.zero, Vector3.one * (.66f * strength), shot,
                            .16f, HeroFxMotion.Expand, facing, gameObject);
                        break;
                    }

                case HeroId.Elyra:
                    HeroVfxPool.Emit(HeroFxShape.Orb, chest + Vector3.up * .18f, Vector3.zero,
                        Vector3.one * (.72f * strength), hot, .30f, HeroFxMotion.Contract, owner: gameObject);
                    HeroVfxPool.Emit(HeroFxShape.Runes, ground, Vector3.zero,
                        new Vector3(1.15f * strength, .05f, 1.15f * strength), accent, .34f,
                        HeroFxMotion.Orbit, owner: gameObject);
                    break;

                case HeroId.Mira:
                    if (slot == 0)
                        HeroVfxPool.Emit(HeroFxShape.Seed, muzzle, muzzle + transform.forward * .75f,
                            Vector3.one * (.70f * strength), hot, .22f, HeroFxMotion.Travel, facing, gameObject);
                    else
                        HeroVfxPool.Emit(slot == 2 ? HeroFxShape.Shield : HeroFxShape.Plus, chest, Vector3.zero,
                            Vector3.one * (.85f * strength), hot, .30f, HeroFxMotion.Contract, facing, gameObject);
                    HeroVfxPool.Emit(HeroFxShape.Petals, ground + Vector3.up * .06f, Vector3.zero,
                        new Vector3(1.20f * strength, .08f, 1.20f * strength), accent, .34f,
                        HeroFxMotion.Orbit, owner: gameObject);
                    break;

                case HeroId.Rook:
                    {
                        Color impact = new Color(1f, .34f, .035f, 1f);
                        HeroVfxPool.Emit(HeroFxShape.Burst, chest + transform.forward * .25f, Vector3.zero,
                            Vector3.one * (.88f * strength), Color.Lerp(impact, Color.white, .28f), .22f,
                            HeroFxMotion.Expand, facing, gameObject);
                        HeroVfxPool.Emit(HeroFxShape.Cracks, ground, Vector3.zero,
                            new Vector3(1.25f * strength, .05f, 1.25f * strength), accent, .30f,
                            HeroFxMotion.Expand, owner: gameObject);
                        if (slot == 2 || ultimate)
                            HeroVfxPool.Emit(HeroFxShape.ShockDome, ground, Vector3.zero,
                                new Vector3((ultimate ? 1.65f : 1.30f), ultimate ? .85f : .55f, (ultimate ? 1.65f : 1.30f)),
                                new Color(impact.r, impact.g, impact.b, .72f), .30f, HeroFxMotion.Expand, owner: gameObject);
                        break;
                    }

                case HeroId.Traveler:
                    HeroVfxPool.Emit(HeroFxShape.Rift, chest, Vector3.zero,
                        new Vector3(.78f, 1.30f, .78f) * strength, new Color(.025f, .01f, .05f, .95f),
                        .27f, HeroFxMotion.Contract, facing, gameObject);
                    HeroVfxPool.Emit(HeroFxShape.Ring, chest, Vector3.zero,
                        Vector3.one * (.82f * strength), hot, .30f, HeroFxMotion.Contract,
                        facing * Quaternion.Euler(90f, 0f, 0f), gameObject);
                    break;
            }
        }
    }
}
