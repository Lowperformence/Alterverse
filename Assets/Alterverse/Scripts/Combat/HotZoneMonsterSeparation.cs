using Alterverse.Characters;
using UnityEngine;

namespace Alterverse.Combat
{
    /// <summary>
    /// Global personal-space controller for every HotZoneTank variant.
    /// Keeps multiple Hot Zone rewards from walking through each other without enlarging colliders,
    /// changing targets, changing attack ranges or taking control away from the existing AI.
    /// </summary>
    [DefaultExecutionOrder(900)]
    public sealed class HotZoneMonsterSeparation : MonoBehaviour
    {
        // Start a gentle side-step before the bodies touch, then add a stronger radial correction
        // only if their centres enter the preferred personal-space radius.
        private const float PreferredDistance=3.25f;
        private const float AvoidanceDistance=4.35f;
        private const float SideStepSpeed=.72f;
        private const float HardSeparationSpeed=2.35f;
        private const float ArenaHalfWidth=39f;
        private const float ArenaHalfHeight=32f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if(GameObject.Find("DF_HotZoneMonsterSeparation")!=null)return;
            GameObject go=new("DF_HotZoneMonsterSeparation");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<HotZoneMonsterSeparation>();
        }

        private void LateUpdate()
        {
            if(Time.timeScale<=0f||!Alterverse.Multiplayer.CoopAuthority.CanSimulateHostGameplay)return;

            var monsters=HotZoneTank.Active;
            for(int i=0;i<monsters.Count;i++)
            {
                HotZoneTank a=monsters[i];
                if(!Alive(a))continue;

                for(int j=i+1;j<monsters.Count;j++)
                {
                    HotZoneTank b=monsters[j];
                    if(!Alive(b))continue;
                    SeparatePair(a,b);
                }
            }
        }

        private static bool Alive(HotZoneTank tank)
        {
            return tank!=null&&tank.isActiveAndEnabled&&tank.TankHealth!=null&&!tank.TankHealth.IsDead;
        }

        private static void SeparatePair(HotZoneTank a,HotZoneTank b)
        {
            Vector3 delta=a.transform.position-b.transform.position;
            delta.y=0f;
            float sqr=delta.sqrMagnitude;
            if(sqr>=AvoidanceDistance*AvoidanceDistance)return;

            float distance=Mathf.Sqrt(Mathf.Max(sqr,.0001f));
            Vector3 away;
            if(sqr<.0001f)
            {
                // Stable emergency direction if both roots ever land on exactly the same point.
                // Pair ordering in HotZoneTank.Active supplies the opposite correction for B.
                away=Vector3.right;
                distance=0f;
            }
            else away=delta/distance;

            // Both monsters take opposite sides around one another. This reads as avoidance rather
            // than as a physics shove and does not alter either monster's facing or combat target.
            Vector3 tangent=Vector3.Cross(Vector3.up,away).normalized;

            float soft=Mathf.Clamp01((AvoidanceDistance-distance)/(AvoidanceDistance-PreferredDistance));
            float sideStep=SideStepSpeed*soft*Time.deltaTime;
            float overlap=Mathf.Max(0f,PreferredDistance-distance);
            float hardStep=Mathf.Min(overlap*.5f,HardSeparationSpeed*Time.deltaTime);

            Vector3 moveA=tangent*sideStep+away*hardStep;
            Vector3 moveB=-tangent*sideStep-away*hardStep;
            bool deeplyOverlapped=distance<PreferredDistance*.62f;

            MoveSafely(a,moveA,deeplyOverlapped);
            MoveSafely(b,moveB,deeplyOverlapped);
        }

        private static void MoveSafely(HotZoneTank tank,Vector3 correction,bool allowEmergencyUnstick)
        {
            correction.y=0f;
            if(correction.sqrMagnitude<.0000001f)return;

            Vector3 current=tank.transform.position;
            Vector3 next=current+correction;
            next.x=Mathf.Clamp(next.x,-ArenaHalfWidth,ArenaHalfWidth);
            next.z=Mathf.Clamp(next.z,-ArenaHalfHeight,ArenaHalfHeight);
            correction=next-current;
            if(correction.sqrMagnitude<.0000001f)return;

            HeroLocomotion locomotion=tank.GetComponent<HeroLocomotion>();
            if(locomotion==null)return;

            // Use the same collision-aware mover as the normal HotZoneTank movement. Do not call
            // FaceMovement here: separation must not steal facing from attacks or target tracking.
            Vector3 moved=locomotion.Move(correction,ArenaHalfWidth,ArenaHalfHeight);

            // If two roots are already deeply interpenetrating, a cast can report itself blocked.
            // Apply only a tiny final root correction in that exceptional state so the pair can
            // recover on the next frame instead of remaining permanently welded together.
            if(allowEmergencyUnstick&&moved.sqrMagnitude<correction.sqrMagnitude*.08f)
            {
                Vector3 fallback=tank.transform.position+correction*.28f;
                fallback.x=Mathf.Clamp(fallback.x,-ArenaHalfWidth,ArenaHalfWidth);
                fallback.z=Mathf.Clamp(fallback.z,-ArenaHalfHeight,ArenaHalfHeight);
                tank.transform.position=fallback;
            }
        }
    }
}
