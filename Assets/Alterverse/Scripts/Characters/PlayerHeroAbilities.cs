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
        private SoftTargeting targeting;
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
            kit = GetComponent<HeroAbilityKit>(); identity = GetComponent<HeroIdentity>(); motor=GetComponent<HeroMotor>();
            targeting = GetComponent<SoftTargeting>() ?? gameObject.AddComponent<SoftTargeting>();
            execution = GetComponent<HeroAbilityExecution>() ?? gameObject.AddComponent<HeroAbilityExecution>();
        }
        private void Update()
        {
            if (Time.timeScale <= 0 || Keyboard.current == null) return;
            Alterverse.Multiplayer.CoopHeroSlot coopSlot=GetComponent<Alterverse.Multiplayer.CoopHeroSlot>();
            if(coopSlot!=null&&coopSlot.IsHuman&&!coopSlot.IsLocalHuman)return;
            if (Keyboard.current.qKey.wasPressedThisFrame) CastQ();
            if (Keyboard.current.eKey.wasPressedThisFrame) CastW();
            if (Keyboard.current.rKey.wasPressedThisFrame) CastE();
            if (Keyboard.current.fKey.wasPressedThisFrame) CastUltimate();
        }
        public AbilitySpec SpecFor(int slot) => kit.Spec(slot);
        public float RemainingFor(int slot) => execution != null ? execution.Remaining(slot) : 0;
        public float CooldownFor(int slot) => execution.Cooldown(slot);
        public void ApplyNetworkCooldown(int slot,float remaining)=>execution?.ApplyNetworkCooldown(slot,remaining);
        public float PreviewRange(int slot) => kit.Spec(slot).Range > 0 ? kit.Spec(slot).Range : kit.Spec(slot).Radius;
        public string Describe(int slot)
        {
            AbilitySpec s = kit.Spec(slot); HeroId h = identity.Hero;
            if ((h == HeroId.Veyr && slot == 2) || (h == HeroId.Nyra && slot == 1) || (h == HeroId.Elyra && slot == 2) || (h == HeroId.Traveler && slot == 1)) return h==HeroId.Traveler?$"Time-skip {s.Range:0.#}m through paired rifts. Uses movement direction.":$"Move {s.Range:0.#}m. Uses movement direction; otherwise evade/face direction.";
            if (h == HeroId.Mira && slot == 1) return $"Heal the weakest nearby hero for {s.Power:0} HP.";
            if (h == HeroId.Mira && slot == 2) return $"Protect a wounded nearby hero with {s.Power:0} shield for 4s.";
            if (h == HeroId.Mira && slot == 3) return $"After {s.Windup:0.#}s: heal allied heroes in {s.Radius:0.#}m for {s.Power:0} and grant 25 shield.";
            if (h == HeroId.Kaelon && slot == 1) return $"Gain {s.Power:0} shield for 4s.";
            if (h == HeroId.Rook && slot == 1) return $"Recover {s.Power:0} HP and gain 20 shield for 3s.";
            if (h == HeroId.Kaelon && slot == 0) return $"{s.Power:0} damage and a 0.55s stun. Brief stun immunity follows.";
            if (h == HeroId.Nyra && slot == 3) return $"Four shots, {s.Power:0} total damage. Leaving range or stunning interrupts the volley.";
            if (h == HeroId.Veyr && slot == 3) return $"{s.Power:0} damage after 0.4s, up to 35% more against wounded heroes. Target can leave range.";
            if (s.Radius > 0) return $"After {s.Windup:0.#}s: {s.Power:0} damage in {s.Radius:0.#}m." + (h == HeroId.Kaelon ? " Slows enemies; ultimate shields allies." : h == HeroId.Elyra && slot != 0 ? " Slows enemies for 2s." : h == HeroId.Traveler ? (slot==3?" Paradox field slows enemies for 1.8s.":" Chrono field slows enemies for 1.5s.") : h == HeroId.Rook && slot == 3 ? " Gain 65 shield for 4s." : "");
            return $"{s.Power:0} damage to one enemy." + (slot == 1 || slot == 2 || h == HeroId.Mira || h == HeroId.Rook ? " Briefly slows the target." : "");
        }
        public bool CastQ() => Cast(0);
        public bool CastW() => Cast(1);
        public bool CastE() => Cast(2);
        public bool CastUltimate() => Cast(3);
        private Vector3 DirectionFor(Health target) => motor != null && motor.HasMoveInput ? motor.CurrentMoveDirection.normalized :
            target != null && identity.Hero != HeroId.Veyr ? transform.position-target.transform.position : transform.forward;
        public Vector3 PreviewDirection(int slot)
        {
            // Same auto-target and movement fallback as Cast; this is not manual aim.
            return DirectionFor(targeting.Acquire(Mathf.Max(kit.Spec(slot).Range,1f),transform.forward,true));
        }
        private bool Cast(int slot)
        {
            if(slot<0||slot>3||execution==null||execution.Remaining(slot)>0)return false;
            Health target = targeting.Acquire(Mathf.Max(kit.Spec(slot).Range, 1f), transform.forward, true);
            Vector3 direction = DirectionFor(target);

            if(Alterverse.Multiplayer.CoopGameplayCommands.ShouldRouteClientCommand(gameObject))
            {
                if(!Alterverse.Multiplayer.CoopGameplayCommands.TrySendAbility(gameObject,slot))return false;
                execution.MarkNetworkRequestCooldown(slot);
                targeting.RegisterOffensiveAction();
                Alterverse.Onboarding.OnboardingSessionGuard.Ability(this);
                return true;
            }

            return CastAuthoritative(slot,target,direction);
        }

        public bool CastNetworkAuthoritative(int slot,Vector3 requestedDirection)
        {
            if(slot<0||slot>3)return false;
            Health target=targeting.Acquire(Mathf.Max(kit.Spec(slot).Range,1f),transform.forward,true);
            Vector3 direction=DirectionFor(target);
            if(UsesMovementDirection(slot)&&requestedDirection.sqrMagnitude>.0001f)direction=requestedDirection.normalized;
            return CastAuthoritative(slot,target,direction);
        }

        private bool UsesMovementDirection(int slot)
        {
            HeroId hero=identity!=null?identity.Hero:HeroId.Veyr;
            return (hero==HeroId.Veyr&&slot==2)||(hero==HeroId.Nyra&&slot==1)||(hero==HeroId.Elyra&&slot==2)||(hero==HeroId.Traveler&&slot==1);
        }

        private bool CastAuthoritative(int slot,Health target,Vector3 direction)
        {
            bool success=execution.TryCast(slot,target,direction);
            if(success)
            {
                targeting.RegisterOffensiveAction();
                Alterverse.Onboarding.OnboardingSessionGuard.Ability(this);
                // Battle items are device-local inventory. Disable them in private co-op until
                // per-player inventory state is explicitly networked; never consume the host's
                // item on behalf of a remote friend.
                if(slot==3&&!Alterverse.Multiplayer.CoopAuthority.Active)
                    BattleItemRuntime.TryActivateEquipped(gameObject,target);
            }
            return success;
        }
    }
}
