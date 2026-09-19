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
        [MenuItem("DIMENSIONS FRONT/Checks/Hit flash cache recovery (Play mode)")]
        public static void FlashCacheChecks()
        {
            if(!Application.isPlaying){Debug.LogWarning("Enter Play mode for hit-flash checks.");return;}
            Camera camera=Camera.main;
            Check(camera!=null,"main camera required");
            GameObject actor=GameObject.CreatePrimitive(PrimitiveType.Cube);
            actor.name="DF_FlashCacheRegression";
            actor.transform.position=camera.transform.position+camera.transform.forward*5-Vector3.up*1.1f;
            try
            {
                Health health=actor.AddComponent<Health>();health.Configure(100);
                var feedback=actor.AddComponent<Alterverse.Combat.CombatFeedback>();
                Renderer renderer=actor.GetComponent<Renderer>();
                var original=new MaterialPropertyBlock();original.SetFloat("_DFRegressionMarker",.37f);renderer.SetPropertyBlock(original);
                feedback.RefreshVisuals();
                // Reproduce the missing native-block destination with a surviving renderer cache.
                var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                var blocks=(MaterialPropertyBlock[])typeof(Alterverse.Combat.CombatFeedback).GetField("saved",flags).GetValue(feedback);
                blocks[0]=null;
                health.TakeDamage(1);health.TakeDamage(1);
                Check(blocks[0]!=null,"missing destination not rebuilt");
                feedback.enabled=false;
                var restored=new MaterialPropertyBlock();renderer.GetPropertyBlock(restored);
                Check(Mathf.Approximately(restored.GetFloat("_DFRegressionMarker"),.37f),"hit flash lost original renderer state");
                feedback.enabled=true;health.TakeDamage(1);feedback.enabled=false;
                renderer.GetPropertyBlock(restored);
                Check(Mathf.Approximately(restored.GetFloat("_DFRegressionMarker"),.37f),"enable cycle lost renderer state");
            }
            finally{actor.SetActive(false);Object.Destroy(actor);}
            Debug.Log("DIMENSIONS FRONT: missing hit-flash destination and enable-cycle recovery checks passed.");
        }
        [MenuItem("DIMENSIONS FRONT/Checks/Recall cancellation (Play mode)")]
        public static void RecallChecks()
        {
            if(!Application.isPlaying||Time.timeScale<=0){Debug.LogWarning("Start a running match for recall checks.");return;}
            var actor=new GameObject("DF_RecallRegression");actor.transform.position=new Vector3(0,80,0);
            try
            {
                var health=actor.AddComponent<Health>();health.Configure(100);
                var recall=actor.AddComponent<Alterverse.Characters.BaseRecall>();
                recall.BeginRecall();Check(recall.IsRecalling,"recall did not start");
                health.TakeDamage(5);health.Heal(5,false);
                Check(!recall.IsRecalling&&health.Current==100,"same-frame healing concealed the interrupt");
                health.AddShield(40,4,false);recall.BeginRecall();health.TakeDamage(5);
                Check(!recall.IsRecalling&&health.Current==100,"shield-only hit did not interrupt");
                recall.BeginRecall();recall.enabled=false;
                Check(!recall.IsRecalling&&recall.Progress==0,"disabled component retained a channel");
                recall.BeginRecall();Check(!recall.IsRecalling,"disabled component accepted recall");
                recall.enabled=true;recall.BeginRecall();Check(recall.IsRecalling,"recall could not restart");
                actor.SetActive(false);Check(!recall.IsRecalling,"inactive actor retained a channel");
            }
            finally{actor.SetActive(false);Object.Destroy(actor);}
            Debug.Log("DIMENSIONS FRONT: recall hit, shield and disable-cycle checks passed.");
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
