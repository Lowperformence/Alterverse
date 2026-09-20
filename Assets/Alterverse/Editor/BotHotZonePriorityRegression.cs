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

        [MenuItem("DIMENSIONS FRONT/Checks/Bot priority: combat before Hot Zone")]
        public static void Run()
        {
            // Priority 1: a perceived combat target always blocks an objective rotation.
            Check(!MobaBotMover.ShouldPursueHotZone(MobaBotBrain.BotState.Lane,true,true),
                "visible enemy did not override Hot Zone while brain was still in Lane");
            Check(!MobaBotMover.ShouldPursueHotZone(MobaBotBrain.BotState.Fight,true,true),
                "Fight state was allowed to rotate to Hot Zone");

            // Priority 2: when no hero combat target is perceived, an active Hot Zone is a legal objective.
            Check(MobaBotMover.ShouldPursueHotZone(MobaBotBrain.BotState.Lane,false,true),
                "active Hot Zone was not available from free-roam Lane state");

            // Non-hero objectives/units must not steal Priority 2 from an active Hot Zone.
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

            Debug.Log("Bot Hot Zone priority regression passed: perceived hero combat > Hot Zone rotation, with retreat/recovery preserved.");
        }
    }
}
#endif
