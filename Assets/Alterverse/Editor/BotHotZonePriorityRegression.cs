#if UNITY_EDITOR
using System;
using Alterverse.AI;
using UnityEditor;
using UnityEngine;

namespace Alterverse.EditorChecks
{
    public static class BotHotZonePriorityRegression
    {
        private static void Check(bool value,string reason)
        {
            if(!value)throw new InvalidOperationException("Bot Hot Zone priority regression: "+reason);
        }

        [MenuItem("DIMENSIONS FRONT/Checks/Bot priority: planet buffs before Hot Zone")]
        public static void Run()
        {
            // Priority 1: a perceived combat target always blocks an objective rotation.
            Check(!MobaBotMover.ShouldPursueHotZone(MobaBotBrain.BotState.Lane,true,true),
                "visible enemy did not override Hot Zone while brain was still in Lane");
            Check(!MobaBotMover.ShouldPursueHotZone(MobaBotBrain.BotState.Fight,true,true),
                "Fight state was allowed to rotate to Hot Zone");

            // Priority 2: eligible planets override both local and shared Hot Zone rotations.
            Check(MobaBotMover.ShouldPursuePlanet(MobaBotBrain.BotState.Roam,false,true),
                "eligible planet was not prioritized from free roam");
            Check(!MobaBotMover.ShouldPursueHotZone(MobaBotBrain.BotState.Roam,false,true,true),
                "active Hot Zone overrode an eligible planet");
            Check(!MobaBotMover.ShouldFollowTeamObjective(true,true),
                "team Hot Zone call overrode a local planet rotation");
            Check(MobaBotMover.ShouldFollowTeamObjective(true,false),
                "planet priority blocked a defence or non-Hot-Zone team call");
            Check(MobaBotMover.ShouldFollowTeamObjective(false,true),
                "Hot Zone team call remained blocked without a planet rotation");
            Check(!MobaBotMover.ShouldPursuePlanet(MobaBotBrain.BotState.Roam,false,false),
                "unavailable or ineligible planet triggered a rotation");
            Check(!MobaBotMover.ShouldPursuePlanet(MobaBotBrain.BotState.Roam,true,true),
                "planet rotation overrode perceived combat");
            foreach(var state in new[]{MobaBotBrain.BotState.Fight,MobaBotBrain.BotState.Retreat,MobaBotBrain.BotState.Recover})
                Check(!MobaBotMover.ShouldPursuePlanet(state,false,true),
                    "planet rotation overrode combat, retreat or recovery");

            // Priority 3: an active Hot Zone remains legal when no eligible planet takes priority.
            Check(MobaBotMover.ShouldPursueHotZone(MobaBotBrain.BotState.Lane,false,true),
                "active Hot Zone was not available from free-roam Lane state");

            // Planets remain targetable for attacks and team observations while the Hot Zone is active.
            Check(BotTargetSensor.HotZoneAllowsTarget(true,false,true),
                "active Hot Zone suppressed a planet target");
            // Other non-hero units retain the existing Hot Zone suppression.
            Check(BotTargetSensor.HotZoneAllowsTarget(true,true),
                "visible hero was incorrectly suppressed during an active Hot Zone");
            Check(!BotTargetSensor.HotZoneAllowsTarget(true,false),
                "non-hero target was allowed to pre-empt an active Hot Zone");
            Check(BotTargetSensor.HotZoneAllowsTarget(false,false),
                "non-hero target remained suppressed after the Hot Zone ended");

            // Survival/recovery and inactive objectives must never be mistaken for Hot Zone rotations.
            Check(!MobaBotMover.ShouldPursueHotZone(MobaBotBrain.BotState.Retreat,false,true),
                "Retreat state tried to rotate to Hot Zone");
            Check(!MobaBotMover.ShouldPursueHotZone(MobaBotBrain.BotState.Recover,false,true),
                "Recover state tried to rotate to Hot Zone");
            Check(!MobaBotMover.ShouldPursueHotZone(MobaBotBrain.BotState.Lane,false,false),
                "inactive Hot Zone was treated as an objective");

            Debug.Log("Bot objective priority regression passed: perceived combat > eligible planet buffs > Hot Zone, with retreat/recovery preserved.");
        }
    }
}
#endif
