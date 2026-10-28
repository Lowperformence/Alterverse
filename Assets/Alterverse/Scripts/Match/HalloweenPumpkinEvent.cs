using System;
using System.Collections.Generic;
using Alterverse.AI;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Multiplayer;
using Alterverse.Profile;
using Alterverse.Visuals;
using Unity.Netcode;
using UnityEngine;

namespace Alterverse.Match
{
    /// <summary>One pumpkin every 120 seconds of active match time, in offline and server-owned matches.</summary>
    public sealed partial class HalloweenPumpkinEvent : MonoBehaviour
    {
        sealed class Pickup
        {
            public Vector3 Ground;
            public GameObject Visual;
        }
        readonly HalloweenPumpkinRules rules = new();
        readonly Dictionary<int,Pickup> pickups = new();
        readonly List<int> scratch = new();
        DimensionsFront front;
        string matchId,account;
        uint round;
        float nextScan,nextSpawnAttempt;
        int localSlot;
        bool networkMatch;

        bool Authority => !networkMatch || (CoopAuthority.Active && NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening && NetworkManager.Singleton.IsServer);
        bool Presentation => NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening ||
            !NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsClient;

        void Awake()
        {
            networkMatch = CoopAuthority.Active;
            matchId = networkMatch ? CoopAuthority.ActivePlan?.MatchId : Guid.NewGuid().ToString("N");
            account = AccountProfilePrefs.Account ?? "";
            localSlot = networkMatch ? CoopRosterBuilder.FindLocalHumanSlot(CoopAuthority.ActivePlan) : 0;
            round = networkMatch ? CoopPlanHandshake.Fingerprint(CoopAuthority.ActivePlan) : 0;
            front = FindAnyObjectByType<DimensionsFront>();
            if (string.IsNullOrEmpty(matchId)) { enabled = false; return; }
            UpdateTransport();
        }

        void Update()
        {
            UpdateTransport(); // Unscaled: reconnect/snapshot delivery also works during a pause/result screen.
            if (front == null || front.HasEnded || Time.timeScale <= 0 || !Authority) return;
            if (rules.Advance(Time.deltaTime) && Time.time >= nextSpawnAttempt)
            {
                nextSpawnAttempt = Time.time + 1f;
                if (TryFindGround(out Vector3 ground))
                {
                    int id = rules.Spawned();
                    ShowPickup(id,ground);
                    BroadcastSpawn(id,ground);
                }
            }
            if (Time.time < nextScan) return;
            nextScan = Time.time + .1f;
            ScanCollectors();
        }

        static bool TryFindGround(out Vector3 ground)
        {
            for (int attempt = 0; attempt < 48; attempt++)
            {
                Vector3 p = new(UnityEngine.Random.Range(-ArenaLayout.HalfLength+4f,ArenaLayout.HalfLength-4f),0,
                    UnityEngine.Random.Range(-ArenaLayout.HalfBreadth+4f,ArenaLayout.HalfBreadth-4f));
                if (!GameplayCollision.ContainsRoundedArena(p,ArenaLayout.MovementHalfWidth,ArenaLayout.MovementHalfHeight,2f)) continue;
                if (ArenaLayout.IsInsideHealingArea(p,ArenaLayout.HealingPosition(0)) ||
                    ArenaLayout.IsInsideHealingArea(p,ArenaLayout.HealingPosition(1))) continue;
                p.y = LunarSurfaceField.WorldSurface(p.x,p.z).y;
                if (!MatchNetworkProtocol.Finite(p.y)) continue;
                // Reject steep relief and buildings. Decorations do not become new movement walls.
                if (Mathf.Abs(LunarSurfaceField.WorldSurface(p.x+.6f,p.z).y-p.y) > .8f ||
                    Mathf.Abs(LunarSurfaceField.WorldSurface(p.x-.6f,p.z).y-p.y) > .8f ||
                    Mathf.Abs(LunarSurfaceField.WorldSurface(p.x,p.z+.6f).y-p.y) > .8f ||
                    Mathf.Abs(LunarSurfaceField.WorldSurface(p.x,p.z-.6f).y-p.y) > .8f) continue;
                if (Physics.CheckSphere(p+Vector3.up*.8f,.65f,GameplayCollision.SightMask,QueryTriggerInteraction.Ignore)) continue;
                ground = p + Vector3.up*.08f;
                return true;
            }
            ground = default;
            return false; // Retry a second later; never place a reward inside a wall or outside the map.
        }

        void ShowPickup(int id,Vector3 ground)
        {
            if (id <= 0 || pickups.ContainsKey(id)) return;
            var pickup = new Pickup { Ground = ground };
            if (Presentation) pickup.Visual = HalloweenPumpkinVisual.Create(transform,ground,id);
            pickups.Add(id,pickup);
        }

        void RemovePickup(int id)
        {
            if (!pickups.TryGetValue(id,out Pickup pickup)) return;
            if (pickup.Visual != null) Destroy(pickup.Visual);
            pickups.Remove(id);
        }

        void ScanCollectors()
        {
            scratch.Clear();
            foreach (int id in pickups.Keys) scratch.Add(id);
            foreach (int id in scratch)
            {
                if (!pickups.TryGetValue(id,out Pickup pickup)) continue;
                for (int i = 0; i < Health.Active.Count; i++)
                {
                    Health health = Health.Active[i];
                    if (!TryHuman(health,out int slot)) continue;
                    Vector3 delta = health.transform.position-pickup.Ground;
                    bool eligible = HalloweenPumpkinRules.Eligible(true,!health.IsDead,health.isActiveAndEnabled,
                        delta.x*delta.x+delta.z*delta.z,delta.y);
                    // Removal in the rules ledger precedes every reward/send: two players cannot win the same pumpkin.
                    if (!rules.TryCollect(id,slot,eligible,out int total)) continue;
                    RemovePickup(id);
                    ConfirmReward(slot,total);
                    BroadcastClaim(id,slot,total);
                    if (Presentation)
                        PrototypeVfx.Ring(pickup.Ground,new Color(1f,.40f,.04f),1.15f,.28f);
                    break;
                }
            }
        }

        bool TryHuman(Health health,out int slot)
        {
            slot = -1;
            if (health == null || health.IsDead || !health.isActiveAndEnabled ||
                health.GetComponent<HeroIdentity>() == null || health.GetComponent<MobaBotBrain>() != null) return false;
            if (!networkMatch)
            {
                // Only the offline input-controlled hero has HeroMotor; AI/minions never qualify.
                HeroMotor input = health.GetComponent<HeroMotor>();
                if (input == null || !input.isActiveAndEnabled ||
                    !string.Equals(account,AccountProfilePrefs.Account ?? "",StringComparison.Ordinal)) return false;
                slot = 0;
                return true;
            }
            CoopHeroSlot actor = health.GetComponent<CoopHeroSlot>();
            MatchPlayerSlot[] roster = CoopAuthority.ActivePlan?.Session?.Slots;
            if (actor == null || !actor.IsHuman || roster == null || actor.Slot < 0 || actor.Slot >= roster.Length) return false;
            MatchPlayerSlot player = roster[actor.Slot];
            if (player == null || player.IsBot || !player.Connected || string.IsNullOrEmpty(player.PlayerId) ||
                player.PlayerId != actor.PlayerId || !ConnectedHuman(player.PlayerId,actor.IsLocalHuman)) return false;
            slot = actor.Slot;
            return true;
        }

        static bool ConnectedHuman(string playerId,bool localHuman)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsListening || !manager.IsServer) return false;
            if (manager.IsHost && localHuman) return true;
            if (manager.ConnectedClientsIds == null) return false;
            foreach (ulong client in manager.ConnectedClientsIds)
                if (client != NetworkManager.ServerClientId && MatchNetworkProtocol.Authorized(client,playerId)) return true;
            return false;
        }

        void ConfirmReward(int slot,int total)
        {
            if (localSlot < 0 || slot != localSlot) return; // A dedicated server never writes a player's wallet.
            HalloweenPumpkinRewards.Apply(matchId,total,account);
        }

        void OnDisable() { DetachTransport(); }
        void OnDestroy() { DetachTransport(); }
    }
}
