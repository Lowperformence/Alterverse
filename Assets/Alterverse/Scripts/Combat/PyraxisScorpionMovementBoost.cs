using System.Collections.Generic;
using Alterverse.Characters;
using Alterverse.Match;
using UnityEngine;

namespace Alterverse.Combat
{
    /// <summary>
    /// Gives the Pyraxis Hot Zone scorpion an additional 200% movement speed without changing
    /// any of the other Hot Zone rewards. The normal HotZoneTank movement remains the source of
    /// direction/pathing; this adds two extra collision-aware movement steps for 3x total speed.
    /// </summary>
    [DefaultExecutionOrder(1200)]
    public sealed class PyraxisScorpionMovementBoost : MonoBehaviour
    {
        private const float ScanInterval=.20f;
        private const string ManagerName="DF_PyraxisScorpionMovementBoost";

        private static bool installed;
        private float nextScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics(){installed=false;}

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if(installed)return;
            installed=true;
            GameObject manager=new(ManagerName);
            Object.DontDestroyOnLoad(manager);
            manager.AddComponent<PyraxisScorpionMovementBoost>();
        }

        private void Update()
        {
            if(!ArenaVariantRuntime.IsPyraxis||Time.unscaledTime<nextScan)return;
            nextScan=Time.unscaledTime+ScanInterval;

            IReadOnlyList<HotZoneTank> active=HotZoneTank.Active;
            for(int i=0;i<active.Count;i++)
            {
                HotZoneTank tank=active[i];
                if(tank==null||tank.GetComponent<PyraxisScorpionSpeedDriver>()!=null)continue;
                tank.gameObject.AddComponent<PyraxisScorpionSpeedDriver>();
            }
        }
    }

    [DefaultExecutionOrder(1250)]
    internal sealed class PyraxisScorpionSpeedDriver : MonoBehaviour
    {
        private const float ExtraMovementMultiplier=2f;
        private const float ArenaHalfWidth=39f;
        private const float ArenaHalfHeight=32f;

        private HeroLocomotion locomotion;
        private Vector3 previousPosition;
        private bool initialized;

        private void OnEnable()
        {
            locomotion=GetComponent<HeroLocomotion>();
            previousPosition=transform.position;
            initialized=true;
        }

        private void LateUpdate()
        {
            Vector3 current=transform.position;
            if(!initialized)
            {
                previousPosition=current;
                initialized=true;
                return;
            }

            if(!ArenaVariantRuntime.IsPyraxis||
               !Alterverse.Multiplayer.CoopAuthority.CanSimulateHostGameplay||
               locomotion==null)
            {
                previousPosition=current;
                return;
            }

            Vector3 baseMovement=current-previousPosition;
            baseMovement.y=0f;
            if(baseMovement.sqrMagnitude>.000001f)
            {
                // +200% means two additional copies of the normal movement, for 300% total.
                Vector3 extraRequested=baseMovement*ExtraMovementMultiplier;
                locomotion.Move(extraRequested,ArenaHalfWidth,ArenaHalfHeight);
            }

            previousPosition=transform.position;
        }
    }
}
