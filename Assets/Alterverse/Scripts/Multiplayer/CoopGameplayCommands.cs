using System.Collections.Generic;
using Alterverse.Characters;
using Alterverse.Combat;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Alterverse.Multiplayer
{
    /// <summary>
    /// Routes non-host player combat intentions to the Relay host. The host remains authoritative,
    /// but receives the client's selected target so soft-targeting cannot silently switch targets.
    /// </summary>
    public sealed class CoopGameplayCommands : MonoBehaviour
    {
        private const string BasicMessage = "DF_COOP_BASIC_V1";
        private const string AbilityMessage = "DF_COOP_ABILITY_V1";

        private const int TargetNone = 0;
        private const int TargetHero = 1;
        private const int TargetMinion = 2;
        private const int TargetTower = 3;
        private const int TargetCore = 4;
        private const int TargetPlanet = 5;
        private const int TargetOther = 6;

        public static CoopGameplayCommands Instance { get; private set; }

        private CustomMessagingManager messaging;
        private bool attached;
        private readonly Dictionary<ulong, string> senderOwners = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Ensure()
        {
            if (Instance == null) new GameObject("DF_CoopGameplayCommands").AddComponent<CoopGameplayCommands>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsListening || !CoopAuthority.Active)
            {
                Detach();
                return;
            }
            Attach(manager.CustomMessagingManager);
        }

        public static bool ShouldRouteClientCommand(GameObject actor)
        {
            if (!CoopAuthority.Active || CoopAuthority.LocalIsHost || actor == null) return false;
            CoopHeroSlot slot = actor.GetComponent<CoopHeroSlot>();
            return slot != null && slot.IsHuman && slot.IsLocalHuman;
        }

        public static bool TrySendBasic(GameObject actor, Health target)
        {
            CoopGameplayCommands service = Instance;
            return service != null && service.SendCommand(actor, -1, target);
        }

        public static bool TrySendAbility(GameObject actor, int abilitySlot, Health target)
        {
            if (abilitySlot < 0 || abilitySlot > 3) return false;
            CoopGameplayCommands service = Instance;
            return service != null && service.SendCommand(actor, abilitySlot, target);
        }

        private bool SendCommand(GameObject actor, int abilitySlot, Health target)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || manager.IsHost || !manager.IsConnectedClient || messaging == null) return false;

            CoopHeroSlot heroSlot = actor != null ? actor.GetComponent<CoopHeroSlot>() : null;
            Health health = actor != null ? actor.GetComponent<Health>() : null;
            if (heroSlot == null || !heroSlot.IsLocalHuman || health == null || health.IsDead) return false;

            FixedString128Bytes id = new(heroSlot.PlayerId ?? "");
            using FastBufferWriter writer = new(224, Allocator.Temp);
            writer.WriteValueSafe(heroSlot.Slot);
            writer.WriteValueSafe(id);
            WriteTarget(writer, target);

            string message = abilitySlot < 0 ? BasicMessage : AbilityMessage;
            if (abilitySlot >= 0)
            {
                writer.WriteValueSafe(abilitySlot);
                HeroMotor motor = actor.GetComponent<HeroMotor>();
                Vector3 direction = motor != null && motor.CurrentMoveDirection.sqrMagnitude > .0001f
                    ? motor.CurrentMoveDirection.normalized
                    : actor.transform.forward;
                writer.WriteValueSafe(direction.x);
                writer.WriteValueSafe(direction.y);
                writer.WriteValueSafe(direction.z);
            }

            messaging.SendNamedMessage(message, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
            return true;
        }

        private static void WriteTarget(FastBufferWriter writer, Health target)
        {
            bool hasTarget = target != null && !target.IsDead && target.isActiveAndEnabled;
            writer.WriteValueSafe(hasTarget);
            if (!hasTarget) return;

            int kind = TargetKind(target);
            int heroSlot = -1;
            if (kind == TargetHero)
            {
                CoopHeroSlot slot = target.GetComponent<CoopHeroSlot>();
                if (slot != null) heroSlot = slot.Slot;
            }

            int targetTeam = TargetTeam(target);
            Vector3 position = target.transform.position;
            writer.WriteValueSafe(kind);
            writer.WriteValueSafe(heroSlot);
            writer.WriteValueSafe(targetTeam);
            writer.WriteValueSafe(position.x);
            writer.WriteValueSafe(position.y);
            writer.WriteValueSafe(position.z);
        }

        private void Attach(CustomMessagingManager value)
        {
            if (value == null || attached && messaging == value) return;
            Detach();
            messaging = value;
            messaging.RegisterNamedMessageHandler(BasicMessage, OnBasic);
            messaging.RegisterNamedMessageHandler(AbilityMessage, OnAbility);
            attached = true;
        }

        private void Detach()
        {
            if (!attached || messaging == null)
            {
                messaging = null;
                attached = false;
                senderOwners.Clear();
                return;
            }

            messaging.UnregisterNamedMessageHandler(BasicMessage);
            messaging.UnregisterNamedMessageHandler(AbilityMessage);
            messaging = null;
            attached = false;
            senderOwners.Clear();
        }

        private void OnBasic(ulong senderClientId, FastBufferReader reader)
        {
            if (!TryReadAuthorizedActor(senderClientId, reader, out GameObject actor)) return;
            Health requestedTarget = ReadTarget(actor, reader);
            actor.GetComponent<BasicAttack>()?.TryAttackNetworkAuthoritative(requestedTarget);
        }

        private void OnAbility(ulong senderClientId, FastBufferReader reader)
        {
            if (!TryReadAuthorizedActor(senderClientId, reader, out GameObject actor)) return;
            Health requestedTarget = ReadTarget(actor, reader);

            reader.ReadValueSafe(out int slot);
            reader.ReadValueSafe(out float dx);
            reader.ReadValueSafe(out float dy);
            reader.ReadValueSafe(out float dz);
            if (slot < 0 || slot > 3) return;

            Vector3 requestedDirection = new(dx, dy, dz);
            requestedDirection.y = 0f;
            if (requestedDirection.sqrMagnitude > .0001f) requestedDirection.Normalize();
            actor.GetComponent<PlayerHeroAbilities>()?.CastNetworkAuthoritative(slot, requestedDirection, requestedTarget);
        }

        private static Health ReadTarget(GameObject actor, FastBufferReader reader)
        {
            reader.ReadValueSafe(out bool hasTarget);
            if (!hasTarget) return null;

            reader.ReadValueSafe(out int kind);
            reader.ReadValueSafe(out int heroSlot);
            reader.ReadValueSafe(out int targetTeam);
            reader.ReadValueSafe(out float x);
            reader.ReadValueSafe(out float y);
            reader.ReadValueSafe(out float z);

            if (kind == TargetHero && heroSlot >= 0)
            {
                CoopHeroSlot[] heroes = FindObjectsByType<CoopHeroSlot>(FindObjectsInactive.Exclude);
                for (int i = 0; i < heroes.Length; i++)
                {
                    CoopHeroSlot slot = heroes[i];
                    if (slot == null || slot.Slot != heroSlot) continue;
                    Health health = slot.GetComponent<Health>();
                    return health != null && HeroCombatRules.Enemy(actor, health) ? health : null;
                }
                return null;
            }

            Vector3 requestedPosition = new(x, y, z);
            Health best = null;
            float bestSqr = float.MaxValue;
            var candidates = Health.Active;
            for (int i = 0; i < candidates.Count; i++)
            {
                Health candidate = candidates[i];
                if (candidate == null || !candidate.isActiveAndEnabled || candidate.IsDead) continue;
                if (!HeroCombatRules.Enemy(actor, candidate)) continue;
                if (!MatchesTargetKind(candidate, kind, targetTeam)) continue;

                Vector3 delta = candidate.transform.position - requestedPosition;
                delta.y = 0f;
                float sqr = delta.sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = candidate;
                }
            }
            return best;
        }

        private static int TargetKind(Health target)
        {
            if (target == null) return TargetNone;
            if (target.GetComponent<HeroIdentity>() != null) return TargetHero;
            if (target.GetComponent<LaneMinion>() != null) return TargetMinion;
            if (target.GetComponent<LaneTower>() != null) return TargetTower;
            if (target.GetComponent<RealityCore>() != null) return TargetCore;
            if (target.GetComponent<DimensionPlanet>() != null) return TargetPlanet;
            return TargetOther;
        }

        private static int TargetTeam(Health target)
        {
            if (target == null) return int.MinValue;
            RealityCore core = target.GetComponent<RealityCore>();
            if (core != null) return core.Team;
            LaneMinion minion = target.GetComponent<LaneMinion>();
            if (minion != null) return minion.Team;
            LaneTower tower = target.GetComponent<LaneTower>();
            if (tower != null) return tower.Team;
            TeamMember member = target.GetComponent<TeamMember>();
            return member != null ? member.Team : int.MinValue;
        }

        private static bool MatchesTargetKind(Health candidate, int kind, int targetTeam)
        {
            if (TargetKind(candidate) != kind) return false;
            if (targetTeam == int.MinValue) return true;
            return TargetTeam(candidate) == targetTeam;
        }

        private bool TryReadAuthorizedActor(ulong senderClientId, FastBufferReader reader, out GameObject actor)
        {
            actor = null;
            NetworkManager manager = NetworkManager.Singleton;
            if (!CoopAuthority.Active || !CoopAuthority.LocalIsHost || manager == null || !manager.IsHost) return false;

            reader.ReadValueSafe(out int rosterSlot);
            reader.ReadValueSafe(out FixedString128Bytes fixedId);
            string playerId = fixedId.ToString();

            CoopMatchPlan plan = CoopAuthority.ActivePlan;
            if (plan?.Session?.Slots == null || rosterSlot < 0 || rosterSlot >= MatchSession.TeamSize) return false;
            MatchPlayerSlot planned = plan.Session.Slots[rosterSlot];
            if (planned == null || planned.IsBot || planned.IsHost || planned.PlayerId != playerId) return false;

            if (!CoopPlanHandshake.IsAuthorizedClient(senderClientId, playerId)) return false;
            senderOwners[senderClientId] = playerId;

            CoopHeroSlot[] heroes = FindObjectsByType<CoopHeroSlot>(FindObjectsInactive.Exclude);
            for (int i = 0; i < heroes.Length; i++)
            {
                CoopHeroSlot h = heroes[i];
                if (h != null && h.Slot == rosterSlot && h.PlayerId == playerId)
                {
                    actor = h.gameObject;
                    return true;
                }
            }
            return false;
        }

        private void OnDestroy()
        {
            Detach();
            if (Instance == this) Instance = null;
        }
    }
}
