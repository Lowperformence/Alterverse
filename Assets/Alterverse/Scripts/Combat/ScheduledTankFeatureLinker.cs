using UnityEngine;

namespace Alterverse.Combat
{
    /// <summary>
    /// Connects optional scheduled-tank presentation/formation behaviour without changing the
    /// network lane-unit message. Both host and clients receive the same spawned tank object, so
    /// tracks are added locally while only the authoritative LaneMinion simulates movement/combat.
    /// </summary>
    public sealed class ScheduledTankFeatureLinker : MonoBehaviour
    {
        private const float ScanInterval=.35f;
        private static ScheduledTankFeatureLinker instance;
        private float nextScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntime()=>instance=null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if(instance!=null)return;
            GameObject go=new("DF_ScheduledTankFeatureLinker");
            DontDestroyOnLoad(go);
            instance=go.AddComponent<ScheduledTankFeatureLinker>();
        }

        private void Update()
        {
            if(Time.unscaledTime<nextScan)return;
            nextScan=Time.unscaledTime+ScanInterval;

            // Unity 6.6-safe overload: the previous call used FindObjectsInactive.Exclude as the
            // only argument, which is not a valid overload on the project API and could stop the
            // new tank scripts from compiling at all.
            LaneMinion[] units=FindObjectsByType<LaneMinion>(FindObjectsSortMode.None);
            for(int i=0;i<units.Length;i++)
            {
                LaneMinion unit=units[i];
                if(unit==null||!IsScheduledTank(unit.gameObject))continue;

                // The track component doubles as the one-time marker that this tank has already
                // been linked, avoiding repeated formation resets on every scan.
                if(unit.GetComponent<TankTrackEmitter>()!=null)continue;
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
