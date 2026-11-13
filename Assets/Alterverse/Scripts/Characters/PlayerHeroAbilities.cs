using Alterverse.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Alterverse.Characters
{
    [RequireComponent(typeof(Health), typeof(HeroIdentity), typeof(HeroAbilityKit))]
    public sealed class PlayerHeroAbilities : MonoBehaviour
    {
        private HeroAbilityExecution execution;
        private HeroAbilityKit kit;
        private HeroIdentity identity;
        private HeroMotor motor;

        public float QRemaining => RemainingFor(0);
        public float WRemaining => RemainingFor(1);
        public float ERemaining => RemainingFor(2);
        public float UltRemaining => RemainingFor(3);
        public string QName => kit.Q.Name;
        public string WName => kit.W.Name;
        public string EName => kit.E.Name;
        public string UltName => kit.Ultimate.Name;

        private void Awake() => Refresh();

        public void Refresh()
        {
            kit = GetComponent<HeroAbilityKit>();
            identity = GetComponent<HeroIdentity>();
            motor = GetComponent<HeroMotor>();
            if (GetComponent<SoftTargeting>() == null) gameObject.AddComponent<SoftTargeting>();
            execution = GetComponent<HeroAbilityExecution>() ?? gameObject.AddComponent<HeroAbilityExecution>();
        }

        private void Update()
        {
            if (Time.timeScale <= 0) return;
            Alterverse.Multiplayer.CoopHeroSlot coopSlot = GetComponent<Alterverse.Multiplayer.CoopHeroSlot>();
            if (coopSlot != null && !coopSlot.IsLocalHuman) return;
            if (bufferedSlot >= 0)
            {
                if (Time.unscaledTime > bufferExpiresAt || GetComponent<Health>().IsDead)
                {
                    bufferedSlot = -1;
                }
                else if (execution != null && execution.Remaining(bufferedSlot) <= 0f && HeroCombatStatus.CanAct(gameObject))
                {
                    int slotToCast = bufferedSlot;
                    AimPose poseToCast = bufferedPose;
                    bufferedSlot = -1;
                    CastAimed(slotToCast, poseToCast);
                }
            }
            if (Keyboard.current == null) return;
            if (Keyboard.current.qKey.wasPressedThisFrame) CastQ();
            if (Keyboard.current.eKey.wasPressedThisFrame) CastW();
            if (Keyboard.current.rKey.wasPressedThisFrame) CastE();
            if (Keyboard.current.fKey.wasPressedThisFrame) CastUltimate();
        }

        public AbilitySpec SpecFor(int slot) => kit.Spec(slot);
        public AimSpec AimFor(int slot) => AimingSettings.Current.For(identity.Hero, slot);
        public float RemainingFor(int slot) => execution != null ? execution.Remaining(slot) : 0;
        public float CooldownFor(int slot) => execution.Cooldown(slot);
        public void ApplyNetworkCooldown(int slot, float remaining,uint acknowledgedCommand=0) => execution?.ApplyNetworkCooldown(slot, remaining,acknowledgedCommand);
        public float PreviewRange(int slot) => AimFor(slot).castRange > 0 ? AimFor(slot).castRange : AimFor(slot).aoeRadius;

        public string Describe(int slot)
        {
            AbilitySpec s = kit.Spec(slot);
            HeroId h = identity.Hero;
            if(h==HeroId.Bolt7)return slot switch {
                0=>$"Grenade: 35 centre / 15 edge damage, {s.Radius:0.###}m radius, {s.Range:0.###}m range.",
                1=>"Rush 2m and hit every enemy in the marked 3m, 70 degree cone for 40 damage and 0.8m knockback.",
                2=>$"Impulse grenade: 25 area damage in {s.Radius:0.###}m, {s.Range:0.###}m throw range, 0.8m outward knockback. Walls block it.",
                3=>"After 0.9s, remove all enemy shields in 3m. No health damage. New shields work immediately; normal shield recharge applies.",
                _=>""
            };
            if(h==HeroId.VoidKnight)return Alterverse.UI.HeroAttackCatalog.For(h)[slot+1].Effect;
            if (h == HeroId.Timmy) return Alterverse.UI.HeroAttackCatalog.For(h)[slot+1].Effect +
                (AimFor(slot).aimType == AimType.Dash ? " Tap to jump in your movement direction; drag to aim." : "");
            if (h == HeroId.Elyra && slot == 2)
                return Alterverse.UI.HeroAttackCatalog.For(h)[slot+1].Effect + " Drag to choose direction; tap uses movement direction.";
            if ((h == HeroId.Veyr && slot == 2) || (h == HeroId.Nyra && slot == 1) || (h == HeroId.Elyra && slot == 2) || (h == HeroId.Traveler && slot == 1))
                return h == HeroId.Traveler
                    ? $"Time-skip {s.Range:0.#}m through paired rifts. Drag to choose direction; tap uses movement direction."
                    : $"Move {s.Range:0.#}m. Drag to choose direction; tap uses movement direction.";
            if (h == HeroId.Nerok) return slot switch
            {
                0 => $"Aim the anchor: {s.Power:0} damage; pull a struck mobile enemy up to 4.2m. Walls block it.",
                1 => $"Gain {s.Power:0} shield for 3s and 15% speed for 2s.",
                2 => $"{s.Power:0} area damage and 25% slow for 1.2s.",
                _ => $"{s.Power:0} total damage over 3s in {s.Radius:0.#}m. Pulls mobile enemies inward at their running speed; running directly outward cancels the pull."
            };
            if (h == HeroId.Mira && slot == 1) return $"Heal the aimed allied hero for {s.Power:0} HP. Without an aimed teammate, heal yourself.";
            if (h == HeroId.Mira && slot == 2) return $"Give the aimed allied hero {s.Power:0} shield for 4s. Without an aimed teammate, shield yourself.";
            if (h == HeroId.Mira && slot == 3) return $"After {s.Windup:0.#}s: heal allied heroes in {s.Radius:0.#}m for {s.Power:0} and grant 25 shield.";
            if (h == HeroId.Kaelon && slot == 1) return $"Gain {s.Power:0} shield for 4s.";
            if (h == HeroId.Rook && slot == 1) return $"Recover {s.Power:0} HP and gain 20 shield for 3s.";
            if (h == HeroId.Kaelon && slot == 0) return $"{s.Power:0} damage and a 0.55s stun. Brief stun immunity follows.";
            if (h == HeroId.Nyra && slot == 3) return $"Three cone shots at one enemy, {s.Power:0} total damage. Stunning interrupts unreleased shots.";
            if (h == HeroId.Veyr && slot == 3) return $"{s.Power:0} damage after {s.Windup:0.##}s, up to 35% more against wounded heroes. Targets can leave the aimed cone.";
            if (s.Radius > 0)
                return $"After {s.Windup:0.#}s: {s.Power:0} damage in {s.Radius:0.#}m." +
                       (h == HeroId.Kaelon ? " Slows enemies; ultimate shields allies." :
                        h == HeroId.Elyra && slot != 0 ? " Slows enemies for 2s." :
                        h == HeroId.Traveler ? (slot == 3 ? " Paradox field slows enemies for 1.8s." : " Chrono field slows enemies for 1.5s.") :
                        h == HeroId.Rook && slot == 3 ? " Gain 65 shield for 4s." : "");
            return $"{s.Power:0} damage on cone contact." +
                   (slot == 1 || slot == 2 || h == HeroId.Mira || h == HeroId.Rook ? " Briefly slows the target." : "");
        }

        public bool CastQ() => Cast(0);
        public bool CastW() => Cast(1);
        public bool CastE() => Cast(2);
        public bool CastUltimate() => Cast(3);

        public Vector3 PreviewDirection(int slot)
        {
            ManualAiming aim = GetComponent<ManualAiming>();
            return aim != null && aim.Active ? aim.Pose.Direction : AimFor(slot).aimType == AimType.Dash
                ? AimGeometry.TapPose(gameObject, AimFor(slot)).Direction : AimGeometry.Direction(transform.forward);
        }

        private bool Cast(int slot)
        {
            if (slot < 0 || slot > 3 || !AimFor(slot).allowTapCast) return false;
            return CastAimed(slot, AimGeometry.TapPose(gameObject, AimFor(slot)));
        }

        public bool CastAimed(int slot, AimPose pose)
        {
            if (!isActiveAndEnabled || Time.timeScale <= 0f || slot < 0 || slot > 3 || execution == null ||
                GetComponent<Health>().IsDead || !AimGeometry.Finite(pose.Direction) || !AimGeometry.Finite(pose.Point)) return false;
            if (execution.Remaining(slot) > 0f || !HeroCombatStatus.CanAct(gameObject))
            {
                if (execution.Remaining(slot) <= 0.22f)
                {
                    bufferedSlot = slot;
                    bufferedPose = pose;
                    bufferExpiresAt = Time.unscaledTime + 0.22f;
                }
                return false;
            }
            bufferedSlot = -1;
            pose = new AimPose(pose.Direction, AimGeometry.ClampPoint(transform.position, pose.Point, AimFor(slot).castRange), pose.Target, pose.HasAimPoint, pose.AllowDashTargetCapture);
            if (Alterverse.Multiplayer.CoopGameplayCommands.ShouldRouteClientCommand(gameObject))
            {
                if (!Alterverse.Multiplayer.CoopGameplayCommands.TrySendAbility(gameObject, slot, pose)) return false;
                execution.MarkNetworkRequestCooldown(slot);
                NotifyMotorAttack(slot, pose.Target, pose.CastDirection(transform.position));
                Alterverse.Onboarding.OnboardingSessionGuard.Ability(this);
                return true;
            }
            return CastAuthoritative(slot, pose);
        }

        public bool CastNetworkAuthoritative(int slot, Vector3 requestedDirection)
            => CastNetworkAuthoritative(slot, requestedDirection, null);
        public bool CastNetworkAuthoritative(int slot, Vector3 requestedDirection, Health requestedTarget)
        {
            if (slot < 0 || slot > 3) return false;
            Vector3 point = transform.position + AimGeometry.Direction(requestedDirection) * AimFor(slot).castRange * .65f;
            return CastAuthoritative(slot, new AimPose(requestedDirection, point, requestedTarget));
        }
        public bool CastNetworkAuthoritative(int slot, AimPose pose)
            => slot >= 0 && slot <= 3 && CastAuthoritative(slot, pose);

        private bool CastAuthoritative(int slot, AimPose pose)
        {
            bool success = execution.TryCastAimed(slot, pose);
            if (success)
            {
                NotifyMotorAttack(slot, pose.Target, pose.CastDirection(transform.position));
                Alterverse.Onboarding.OnboardingSessionGuard.Ability(this);
            }
            return success;
        }

        private void NotifyMotorAttack(int slot, Health target, Vector3 direction)
        {
            if (motor == null || kit == null) return;

            Vector3 facing = direction;
            if (target != null)
            {
                facing = target.transform.position - transform.position;
                facing.y = 0f;
            }

            float duration = Mathf.Max(.18f, kit.Spec(slot).Windup + .18f);
            motor.NotifyAttack(duration, facing);
        }
    }
}

