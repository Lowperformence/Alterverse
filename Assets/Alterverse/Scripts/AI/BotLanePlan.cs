using UnityEngine;
using Alterverse.Combat;

namespace Alterverse.AI
{
    // Historical component name kept for scene/component compatibility.
    // It no longer contains lane logic: hero bots free-roam between open arena sectors.
    public sealed class BotLanePlan : MonoBehaviour
    {
        [SerializeField] private int team;
        private Vector3 roamTarget;
        private float nextRoamPick;
        private uint seed;
        private int roamStep;

        public Vector3 Home => new(team == 0 ? -ArenaLayout.HealingX : ArenaLayout.HealingX, transform.position.y, 0f);

        public void Initialize(int assignedTeam, float legacyLaneZ)
        {
            team = Mathf.Clamp(assignedTeam, 0, 1);
            seed=2166136261u;
            foreach(char ch in gameObject.name){seed^=ch;seed*=16777619u;}
            seed^=(uint)(team+1)*2246822519u;
            ResetRoute();
        }

        public void ResetRoute()
        {
            nextRoamPick=0f;
            roamStep=0;
            roamTarget=transform.position;
        }

        public Vector3 GetDestination(MobaBotBrain.BotState state)
        {
            if(state==MobaBotBrain.BotState.Retreat||state==MobaBotBrain.BotState.Recover)
                return Home;

            Vector3 delta=roamTarget-transform.position;delta.y=0;
            if(Time.time>=nextRoamPick||delta.sqrMagnitude<1.8f*1.8f)
                PickRoamTarget();
            return roamTarget;
        }

        private void PickRoamTarget()
        {
            roamStep++;
            uint value=seed+(uint)(roamStep*747796405);
            int slot=(int)(value%8u);
            float forward=team==0?1f:-1f;

            // Deliberately cover open combat sectors rather than the old z=+/-16 lanes.
            Vector3 point=slot switch
            {
                0=>new Vector3(0f,0f,0f),
                1=>new Vector3(6f*forward,0f,9f),
                2=>new Vector3(6f*forward,0f,-9f),
                3=>new Vector3(-4f*forward,0f,13f),
                4=>new Vector3(-4f*forward,0f,-13f),
                5=>new Vector3(12f*forward,0f,4f),
                6=>new Vector3(12f*forward,0f,-4f),
                _=>new Vector3(2f*forward,0f,0f)
            };

            point.y=transform.position.y;
            roamTarget=point;
            // The match is under constant time pressure: do not spend too long on a low-value
            // search sector. Reaching the point still causes an immediate repick above.
            nextRoamPick=Time.time+Mathf.Lerp(2.8f,4.5f,(value%101u)/100f);
        }
    }
}

