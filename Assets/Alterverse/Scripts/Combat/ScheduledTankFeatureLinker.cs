using Alterverse.Match;
using Alterverse.Multiplayer;
using UnityEngine;

namespace Alterverse.Combat
{
    /// <summary>
    /// Match-scoped scheduler and safety linker for the 04:00 / 08:00 team tanks.
    /// It is independent from individual lane-spawner lifetimes, so destroying/recreating
    /// a LaneWaveSpawner cannot silently cancel the scheduled reinforcement.
    /// </summary>
    public sealed class ScheduledTankFeatureLinker : MonoBehaviour
    {
        private const float ScanInterval=.25f;
        private static ScheduledTankFeatureLinker instance;

        private DimensionsFront activeFront;
        private float nextScan;
        private float matchElapsed;
        private int nextOccurrence;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntime()=>instance=null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if(instance!=null)return;
            GameObject go=new("DF_ScheduledTankDirector");
            DontDestroyOnLoad(go);
            instance=go.AddComponent<ScheduledTankFeatureLinker>();
        }

        private void Update()
        {
            if(Time.unscaledTime>=nextScan)
            {
                nextScan=Time.unscaledTime+ScanInterval;
                RefreshMatch();
                EnsureTankFeatures();
            }

            if(activeFront==null||activeFront.HasEnded||!CoopAuthority.CanSimulateHostGameplay||Time.timeScale<=0f)return;

            // Time.deltaTime is deliberately used so countdown/menu pauses do not advance the
            // schedule and the tank really arrives after four/eight minutes of active match time.
            matchElapsed+=Time.deltaTime;
            while(nextOccurrence<LaneWaveSpawner.ScheduledTankCount&&
                  matchElapsed>=LaneWaveSpawner.ScheduledTankTime(nextOccurrence))
            {
                LaneWaveSpawner.TrySpawnScheduledTanks(nextOccurrence);
                nextOccurrence++;
            }
        }

        private void RefreshMatch()
        {
            DimensionsFront found=FindAnyObjectByType<DimensionsFront>();
            if(found==activeFront)return;

            activeFront=found;
            matchElapsed=0f;
            nextOccurrence=0;
            if(activeFront!=null)LaneWaveSpawner.ResetScheduledTanksForMatch();
        }

        private static void EnsureTankFeatures()
        {
            LaneMinion[] units=FindObjectsByType<LaneMinion>(FindObjectsSortMode.None);
            for(int i=0;i<units.Length;i++)
            {
                LaneMinion unit=units[i];
                if(unit==null||!IsScheduledTank(unit.gameObject))continue;

                if(unit.GetComponent<TankTrackEmitter>()==null)
                    unit.gameObject.AddComponent<TankTrackEmitter>();
                unit.ConfigureTeamSupport();
            }
        }

        private static bool IsScheduledTank(GameObject go)
        {
            if(go==null)return false;
            return go.name=="DF_ZeroScheduledTank"||go.name=="DF_LumenScheduledTank";
        }
    }
}
