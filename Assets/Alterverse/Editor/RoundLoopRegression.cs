#if UNITY_EDITOR
using System;
using Alterverse.Characters;
using Alterverse.Multiplayer;
using UnityEditor;
using UnityEngine;

namespace Alterverse.EditorTools
{
    public static class RoundLoopRegression
    {
        [MenuItem("DIMENSIONS FRONT/Checks/Round Loop Roster")]
        public static void Run()
        {
            foreach (bool online in new[] { false, true })
            {
                CoopMatchPlan initial = online ? new OnlineMatchPlan() : new CoopMatchPlan();
                initial.MatchId = "round-0";
                initial.Session = new MatchSession(initial.MatchId, online ? SessionMode.OnlineCasual : SessionMode.Private);
                for (int i = 0; i < MatchSession.MaxPlayers; i++)
                    Require(initial.Session.TryAssign(i, new MatchPlayerSlot {
                        PlayerId = "player-" + i, DisplayName = "Player " + i, Team = i / MatchSession.TeamSize,
                        Hero = (HeroId)(i % 6), IsBot = i > 2, IsHost = i == 0, IsLocal = i == 1,
                        Connected = true, Ready = true
                    }), "initial roster");
                CoopMatchPlan previous = initial;
                for (int round = 1; round <= 3; round++)
                {
                    // Player 2 leaves after round one. Their original team/hero slot becomes a bot.
                    int mask = round == 1 ? 7 : 3;
                    CoopMatchPlan next = CoopMatchPlan.NextRound(previous, "round-" + round, mask);
                    Require(next.Session.IsFull, "eight slots survive each restart");
                    Require(next.MatchId != previous.MatchId && next.Session.SessionId == next.MatchId, "fresh reward receipt namespace");
                    Require((next is OnlineMatchPlan) == online, "online/private mode retained");
                    Require(next.HumanCount == (round == 1 ? 3 : 2), "remaining humans counted");
                    Require(next.Session.Slots[1].IsLocal, "local player retained");
                    Require(!next.Session.Slots[0].IsBot && next.Session.Slots[0].IsHost, "host retained");
                    for (int i = 0; i < MatchSession.MaxPlayers; i++)
                    {
                        Require(next.Session.Slots[i].Team == initial.Session.Slots[i].Team, "team retained");
                        Require(next.Session.Slots[i].Hero == initial.Session.Slots[i].Hero, "hero retained");
                    }
                    if (round > 1) Require(next.Session.Slots[2].IsBot && !next.Session.Slots[2].IsLocal, "departed player replaced");
                    Require(previous.Session.Slots[1].PlayerId == "player-1", "previous plan not mutated");
                    previous = next;
                }
                bool rejected = false;
                try { CoopMatchPlan.NextRound(previous, previous.MatchId, 3); } catch (InvalidOperationException) { rejected = true; }
                Require(rejected, "duplicate round ID rejected");
                rejected = false;
                try { CoopMatchPlan.NextRound(previous, "empty", 0); } catch (InvalidOperationException) { rejected = true; }
                Require(rejected, "empty session cannot restart");
            }
            Debug.Log("DF: round-loop roster regressions passed.");
        }
        static void Require(bool condition, string message) { if (!condition) throw new Exception("Round loop: " + message); }
    }
}
#endif
