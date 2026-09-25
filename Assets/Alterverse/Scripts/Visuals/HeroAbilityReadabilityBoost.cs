using Alterverse.Characters;
using Alterverse.Combat;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Presentation-only reinforcement for successful ability casts. The existing hero-specific
    /// VFX remain the primary effect; this layer adds one unmistakable, semantically correct release
    /// cue for small mobile screens without changing damage, cooldown, range, targeting or hit timing.
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
                // A real cast is the same large cooldown jump used by the established attack-presentation
                // layer. The initial ultimate lock is already present when Prime runs, so it cannot fake a cast.
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
            float strength = ultimate ? 1.34f : 1f;
            Vector3 forward = transform.forward; forward.y = 0f;
            if (forward.sqrMagnitude < .001f) forward = Vector3.forward;
            forward.Normalize();
            Quaternion facing = Quaternion.LookRotation(forward);
            Vector3 chest = transform.position + Vector3.up * .62f;
            Vector3 ground = transform.position + Vector3.up * .055f;
            Vector3 muzzle = ReleasePoint();

            // One universal confirmation pulse means every button press that became a real cast is readable.
            // It is intentionally compact so the hero/slot-specific cue below remains the dominant silhouette.
            HeroVfxPool.Emit(HeroFxShape.Halo, muzzle, muzzle, Vector3.one * (.54f * strength), hot,
                ultimate ? .28f : .20f, HeroFxMotion.Expand, facing, gameObject);
            HeroVfxPool.Emit(HeroFxShape.Ring, ground, ground,
                new Vector3(.78f * strength, .045f, .78f * strength), new Color(accent.r, accent.g, accent.b, .88f),
                ultimate ? .34f : .24f, HeroFxMotion.Expand, Quaternion.identity, gameObject);

            switch (hero)
            {
                case HeroId.Veyr:
                    if (slot == 0) // Null Cut
                    {
                        HeroVfxPool.Emit(HeroFxShape.Slash, chest + forward * .34f, Vector3.zero,
                            new Vector3(2.05f, .17f, 2.05f), hot, .23f, HeroFxMotion.Sweep, facing, gameObject);
                    }
                    else if (slot == 1) // Rift Scar
                    {
                        HeroVfxPool.Emit(HeroFxShape.Rift, chest + forward * .42f, Vector3.zero,
                            new Vector3(.76f, 1.28f, .76f), accent, .28f, HeroFxMotion.Contract, facing, gameObject);
                        HeroVfxPool.Emit(HeroFxShape.Slash, chest + forward * .48f, Vector3.zero,
                            new Vector3(1.72f, .14f, 1.72f), hot, .20f, HeroFxMotion.Sweep, facing, gameObject);
                    }
                    else if (slot == 2) // Gapfold
                    {
                        HeroVfxPool.Emit(HeroFxShape.Rift, chest, chest + forward * 1.20f,
                            new Vector3(.84f, 1.34f, .84f), accent, .25f, HeroFxMotion.Travel, facing, gameObject);
                    }
                    else // Vacant Echo
                    {
                        HeroVfxPool.Emit(HeroFxShape.Slash, chest + forward * .42f, Vector3.zero,
                            new Vector3(2.70f, .20f, 2.70f), hot, .28f, HeroFxMotion.Sweep, facing, gameObject);
                        HeroVfxPool.Emit(HeroFxShape.Rift, chest + forward * .58f, Vector3.zero,
                            new Vector3(1.02f, 1.72f, 1.02f), accent, .34f, HeroFxMotion.Contract, facing, gameObject);
                    }
                    break;

                case HeroId.Kaelon:
                    if (slot == 0) // Anchor Strike
                    {
                        HeroVfxPool.Emit(HeroFxShape.Burst, chest + forward * .30f, Vector3.zero,
                            Vector3.one * .78f, hot, .21f, HeroFxMotion.Expand, facing, gameObject);
                        HeroVfxPool.Emit(HeroFxShape.Cracks, ground + forward * .30f, Vector3.zero,
                            new Vector3(1.02f, .05f, 1.02f), accent, .27f, HeroFxMotion.Expand, owner: gameObject);
                    }
                    else if (slot == 1) // Lumen Wall
                    {
                        HeroVfxPool.Emit(HeroFxShape.Shield, chest, Vector3.zero, Vector3.one * .94f,
                            hot, .34f, HeroFxMotion.Contract, facing, gameObject);
                        HeroVfxPool.Emit(HeroFxShape.Hex, chest, Vector3.zero, Vector3.one * 1.04f,
                            new Color(accent.r, accent.g, accent.b, .74f), .31f, HeroFxMotion.Expand, facing, gameObject);
                    }
                    else if (slot == 2) // Gravity Anchor
                    {
                        HeroVfxPool.Emit(HeroFxShape.Hex, ground, Vector3.zero, new Vector3(1.12f, .05f, 1.12f),
                            hot, .26f, HeroFxMotion.Orbit, owner: gameObject);
                        HeroVfxPool.Emit(HeroFxShape.ShockDome, ground, Vector3.zero,
                            new Vector3(1.34f, .55f, 1.34f), new Color(accent.r, accent.g, accent.b, .70f),
                            .30f, HeroFxMotion.Expand, owner: gameObject);
                    }
                    else // Kest Citadel
                    {
                        HeroVfxPool.Emit(HeroFxShape.Shield, chest, Vector3.zero, Vector3.one * 1.20f,
                            hot, .38f, HeroFxMotion.Expand, facing, gameObject);
                        HeroVfxPool.Emit(HeroFxShape.ShockDome, ground, Vector3.zero,
                            new Vector3(1.72f, .82f, 1.72f), new Color(accent.r, accent.g, accent.b, .72f),
                            .36f, HeroFxMotion.Expand, owner: gameObject);
                    }
                    break;

                case HeroId.Nyra:
                    {
                        Color shot = new Color(1f, .075f, .035f, 1f);
                        if (slot == 1) // Vector Shift is movement, never fake a projectile.
                        {
                            HeroVfxPool.Emit(HeroFxShape.Burst, ground, Vector3.zero,
                                new Vector3(.88f, .06f, .88f), Color.Lerp(shot, Color.white, .28f),
                                .18f, HeroFxMotion.Expand, owner: gameObject);
                            HeroVfxPool.Emit(HeroFxShape.Ring, ground + forward * .52f, Vector3.zero,
                                new Vector3(.78f, .04f, .78f), shot, .22f, HeroFxMotion.Contract, facing, gameObject);
                        }
                        else if (slot == 2) // Light Snare
                        {
                            HeroVfxPool.Emit(HeroFxShape.Hex, muzzle + forward * .30f, Vector3.zero,
                                Vector3.one * .66f, Color.Lerp(shot, Color.white, .38f), .24f,
                                HeroFxMotion.Contract, facing, gameObject);
                            HeroVfxPool.Emit(HeroFxShape.Burst, muzzle, Vector3.zero, Vector3.one * .56f,
                                shot, .15f, HeroFxMotion.Expand, facing, gameObject);
                        }
                        else // Prism Shot / Sightline Break
                        {
                            float length = ultimate ? 1.68f : 1.05f;
                            HeroVfxPool.Emit(HeroFxShape.Bolt, muzzle, muzzle + forward * length,
                                new Vector3(ultimate ? .40f : .32f, ultimate ? .40f : .32f, ultimate ? 1.35f : .90f),
                                Color.Lerp(shot, Color.white, .35f), .18f, HeroFxMotion.Travel, facing, gameObject);
                            HeroVfxPool.Emit(HeroFxShape.Burst, muzzle, Vector3.zero,
                                Vector3.one * (ultimate ? .78f : .60f), shot, .16f, HeroFxMotion.Expand, facing, gameObject);
                        }
                        break;
                    }

                case HeroId.Elyra:
                    if (slot == 2) // Phase Fold is movement through space.
                    {
                        HeroVfxPool.Emit(HeroFxShape.Rift, chest, chest + forward * .95f,
                            new Vector3(.76f, 1.24f, .76f), accent, .24f, HeroFxMotion.Travel, facing, gameObject);
                    }
                    else if (slot == 1) // Invert Field
                    {
                        HeroVfxPool.Emit(HeroFxShape.Runes, ground, Vector3.zero,
                            new Vector3(1.20f, .05f, 1.20f), accent, .32f, HeroFxMotion.Orbit, owner: gameObject);
                        HeroVfxPool.Emit(HeroFxShape.ShockDome, ground, Vector3.zero,
                            new Vector3(1.24f, .52f, 1.24f), new Color(accent.r, accent.g, accent.b, .62f),
                            .28f, HeroFxMotion.Contract, owner: gameObject);
                    }
                    else // Orbit Fracture / Impossible Center
                    {
                        HeroVfxPool.Emit(HeroFxShape.Orb, chest + Vector3.up * .18f, Vector3.zero,
                            Vector3.one * (ultimate ? .96f : .70f), hot, ultimate ? .36f : .29f,
                            HeroFxMotion.Contract, owner: gameObject);
                        HeroVfxPool.Emit(HeroFxShape.Runes, ground, Vector3.zero,
                            new Vector3(ultimate ? 1.52f : 1.08f, .05f, ultimate ? 1.52f : 1.08f),
                            accent, ultimate ? .40f : .32f, HeroFxMotion.Orbit, owner: gameObject);
                    }
                    break;

                case HeroId.Mira:
                    if (slot == 0) // Resonant Seed
                    {
                        HeroVfxPool.Emit(HeroFxShape.Seed, muzzle, muzzle + forward * .78f,
                            Vector3.one * .70f, hot, .22f, HeroFxMotion.Travel, facing, gameObject);
                    }
                    else if (slot == 1) // Growth Veil
                    {
                        HeroVfxPool.Emit(HeroFxShape.Plus, chest, Vector3.zero,
                            Vector3.one * .86f, hot, .30f, HeroFxMotion.Contract, facing, gameObject);
                    }
                    else if (slot == 2) // Venn Shelter
                    {
                        HeroVfxPool.Emit(HeroFxShape.Shield, chest, Vector3.zero,
                            Vector3.one * .94f, hot, .32f, HeroFxMotion.Contract, facing, gameObject);
                    }
                    else // Wildsurge
                    {
                        HeroVfxPool.Emit(HeroFxShape.Plus, chest, Vector3.zero,
                            Vector3.one * 1.14f, hot, .36f, HeroFxMotion.Expand, facing, gameObject);
                    }
                    HeroVfxPool.Emit(HeroFxShape.Petals, ground + Vector3.up * .06f, Vector3.zero,
                        new Vector3((ultimate ? 1.52f : 1.10f), .08f, (ultimate ? 1.52f : 1.10f)),
                        accent, ultimate ? .40f : .32f, HeroFxMotion.Orbit, owner: gameObject);
                    break;

                case HeroId.Rook:
                    {
                        Color impact = new Color(1f, .34f, .035f, 1f);
                        if (slot == 1) // Armor Grip is defensive, not a ground smash.
                        {
                            HeroVfxPool.Emit(HeroFxShape.Shield, chest, Vector3.zero, Vector3.one * .96f,
                                Color.Lerp(impact, Color.white, .35f), .32f, HeroFxMotion.Contract, facing, gameObject);
                            HeroVfxPool.Emit(HeroFxShape.Halo, chest, Vector3.zero, Vector3.one * 1.05f,
                                new Color(accent.r, accent.g, accent.b, .72f), .30f, HeroFxMotion.Expand, owner: gameObject);
                        }
                        else
                        {
                            HeroVfxPool.Emit(HeroFxShape.Burst, chest + forward * .25f, Vector3.zero,
                                Vector3.one * (ultimate ? 1.05f : .84f), Color.Lerp(impact, Color.white, .28f),
                                .22f, HeroFxMotion.Expand, facing, gameObject);
                            HeroVfxPool.Emit(HeroFxShape.Cracks, ground, Vector3.zero,
                                new Vector3((slot == 2 || ultimate) ? 1.34f : 1.02f, .05f, (slot == 2 || ultimate) ? 1.34f : 1.02f),
                                accent, .30f, HeroFxMotion.Expand, owner: gameObject);
                            if (slot == 2 || ultimate)
                                HeroVfxPool.Emit(HeroFxShape.ShockDome, ground, Vector3.zero,
                                    new Vector3(ultimate ? 1.70f : 1.34f, ultimate ? .86f : .56f, ultimate ? 1.70f : 1.34f),
                                    new Color(impact.r, impact.g, impact.b, .72f), .31f, HeroFxMotion.Expand, owner: gameObject);
                        }
                        break;
                    }

                case HeroId.Traveler:
                    if (slot == 0) // Temporal Beam
                    {
                        HeroVfxPool.Emit(HeroFxShape.Bolt, muzzle, muzzle + forward * 1.08f,
                            new Vector3(.28f, .28f, 1.0f), hot, .20f, HeroFxMotion.Travel, facing, gameObject);
                    }
                    else if (slot == 1) // Rift Step
                    {
                        HeroVfxPool.Emit(HeroFxShape.Rift, chest, chest + forward * 1.02f,
                            new Vector3(.80f, 1.32f, .80f), new Color(.025f, .01f, .05f, .96f),
                            .25f, HeroFxMotion.Travel, facing, gameObject);
                    }
                    else // Chrono Snare / Paradox Collapse
                    {
                        float scale = ultimate ? 1.48f : 1.12f;
                        HeroVfxPool.Emit(HeroFxShape.Runes, ground, Vector3.zero,
                            new Vector3(scale, .05f, scale), accent, ultimate ? .40f : .32f,
                            HeroFxMotion.Orbit, owner: gameObject);
                        HeroVfxPool.Emit(HeroFxShape.Rift, chest, Vector3.zero,
                            new Vector3(.74f, 1.26f, .74f) * strength, new Color(.025f, .01f, .05f, .95f),
                            .27f, HeroFxMotion.Contract, facing, gameObject);
                        if (ultimate)
                            HeroVfxPool.Emit(HeroFxShape.ShockDome, ground, Vector3.zero,
                                new Vector3(1.58f, .78f, 1.58f), new Color(accent.r, accent.g, accent.b, .62f),
                                .35f, HeroFxMotion.Contract, owner: gameObject);
                    }
                    break;
            }
        }
    }
}
