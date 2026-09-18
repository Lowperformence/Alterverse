#if UNITY_EDITOR
using Alterverse.Combat;
using Alterverse.UI;
using UnityEditor;
using UnityEngine;
namespace Alterverse.EditorChecks
{
    public static class CombatQualityRegression
    {
        private static void Check(bool valid,string message)
        {if(!valid)throw new System.InvalidOperationException("DIMENSIONS FRONT combat check: "+message);}
        [MenuItem("DIMENSIONS FRONT/Checks/Combat HUD safe areas")]
        public static void LayoutChecks()
        {
            Rect[] screens={new Rect(0,0,1920,1080),new Rect(96,24,2208,1056),new Rect(0,0,2048,1536),new Rect(48,0,1232,720),new Rect(0,0,960,540)};
            foreach(Rect safe in screens)
            {
                CombatHudLayout layout=new CombatHudLayout(safe);
                Check(layout.Point(new Vector2(safe.x,safe.yMax)).sqrMagnitude<.001f,"safe top-left conversion");
                for(int i=0;i<5;i++)
                {
                    Rect action=layout.Action(i);
                    Check(action.xMin>=0&&action.yMin>=0&&action.xMax<=layout.Width&&action.yMax<=layout.Height,"action outside safe area");
                    Check(action.width>=80&&action.height>=80,"logical touch size");
                    Check(layout.Hit(action.center)==i,"button center routed incorrectly");
                    Check(!layout.ContainsWithMargin(i,action.center+Vector2.right*300),"drag out did not cancel");
                    for(int j=i+1;j<5;j++)Check(!action.Overlaps(layout.Action(j)),"overlapping actions");
                    Check(!action.Overlaps(layout.MoveCapture),"attack and joystick capture overlap");
                }
            }
            Debug.Log("DIMENSIONS FRONT: HUD geometry checks passed. Device touch/readability still require testing.");
        }
        [MenuItem("DIMENSIONS FRONT/Checks/Health registry and shield feedback (Play mode)")]
        public static void HealthChecks()
        {
            if(!Application.isPlaying){Debug.LogWarning("Enter Play mode for health lifecycle checks.");return;}
            int count=Health.Active.Count;GameObject actor=new GameObject("DF_HealthRegression");
            try
            {
                Health health=actor.AddComponent<Health>();health.Configure(100);
                Check(Health.Active.Count==count+1,"enabled health not registered");
                float damage=-1,absorbed=-1;int events=0;
                health.Damaged+=(d,s,source)=>{damage=d;absorbed=s;events++;};
                health.AddShield(40,4);health.TakeDamage(25);
                Check(events==1&&damage==0&&absorbed==25&&health.Current==100,"shield-only feedback must not damage HP");
                health.TakeDamage(20);
                Check(events==2&&damage==5&&absorbed==15&&health.Current==95,"shield spillover payload");
                actor.SetActive(false);Check(Health.Active.Count==count,"disabled health still registered");
                actor.SetActive(true);Check(Health.Active.Count==count+1,"re-enabled health missing/duplicated");
            }
            finally{actor.SetActive(false);Object.Destroy(actor);}
            Check(Health.Active.Count==count,"test leaked registered actor");
            Debug.Log("DIMENSIONS FRONT: health registry and shield feedback checks passed.");
        }
    }
}
#endif
