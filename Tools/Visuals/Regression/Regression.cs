using System;
using System.Reflection;
using Alterverse.Audio;
using Alterverse.Performance;
using Alterverse.Visuals;
using UnityEngine;

public static class VisualRegression
{
    private static int checks;
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static void Check(bool ok,string message) { checks++;if(!ok)throw new Exception(message); }
    private static T Get<T>(object target,string name) {return (T)target.GetType().GetField(name,Private|BindingFlags.Public).GetValue(target);}
    private static void Invoke(object target,string name) {target.GetType().GetMethod(name,Private).Invoke(target,null);}
    private static object[] Slots(HeroVfxPool pool) {return (object[])Get<Array>(pool,"slots");}
    private static int Active(HeroVfxPool pool,bool? warning=null,bool? decorative=null)
    {
        int count=0;
        foreach(object slot in Slots(pool))
            if(Get<bool>(slot,"Active")&&(!warning.HasValue||Get<bool>(slot,"Warning")==warning)&&
                (!decorative.HasValue||Get<bool>(slot,"Decorative")==decorative))count++;
        return count;
    }
    private static HeroVfxPool Pool()
    {
        Time.time=0;Time.timeScale=1;MobilePerformanceProfile.Current=MobileQuality.Performance;
        return new GameObject("Regression pool").AddComponent<HeroVfxPool>();
    }
    private static void Emit(bool warning=false,bool decorative=false,float duration=10,float delay=0,GameObject owner=null,Transform follow=null)
    {
        HeroVfxPool.Emit(HeroFxShape.Ring,Vector3.zero,Vector3.zero,Vector3.one*3,new Color(.2f,.7f,1),duration,
            HeroFxMotion.Hold,owner:owner,warning:warning,decorative:decorative,delay:delay,follow:follow);
    }
    private static void PoolChecks()
    {
        HeroVfxPool pool=Pool();GameObject owner=new GameObject("owner");owner.AddComponent<Alterverse.Combat.Health>();
        for(int i=0;i<200;i++)Emit(decorative:true);
        Check(Active(pool,false,true)==128,"decorations consumed core reserve");
        for(int i=0;i<32;i++)Emit();
        Check(Active(pool,false,false)==32,"core reserve unavailable");
        for(int i=0;i<40;i++)Emit(warning:true,owner:owner);
        Check(Active(pool,true)==32,"warning capacity changed");
        for(int i=0;i<128;i++)Emit();
        Check(Active(pool,false,false)==160&&Active(pool,false,true)==0,"cores failed to reclaim decoration");
        Check(Active(pool,true)==32,"core displaced warnings");
        for(int i=0;i<400;i++)Emit(decorative:true);
        Check(Active(pool)==192&&Active(pool,false,false)==160,"decoration displaced core");
        foreach(object slot in Slots(pool))
        {
            if(!Get<bool>(slot,"Active"))continue;
            Quaternion rotation=Get<Quaternion>(slot,"Rotation");
            Check(rotation.w==1f&&rotation.x==0f&&rotation.y==0f&&rotation.z==0f,"default quaternion not identity");
            if(Get<bool>(slot,"Warning"))
            {
                Check(Math.Abs(Get<Vector3>(slot,"Size").x-3)<1e-6,"warning radius scaled");
                var block=Get<MaterialPropertyBlock>(slot,"Block");
                Check(block.GetFloat(Shader.PropertyToID("_Polish"))==0,"warning polish enabled");
                Check(block.GetFloat(Shader.PropertyToID("_DetailQuality"))==0,"warning detail enabled");
            }
        }
        HeroVfxPool.CancelWarnings(owner);
        Check(Active(pool,true)==0&&Active(pool,false,false)==160,"warning cancel affected cores");
        Time.time=20;Time.timeScale=0;Invoke(pool,"Update");
        Check(Active(pool)==160,"paused pool expired effects");
        Time.timeScale=1;Invoke(pool,"Update");Check(Active(pool)==0,"unpaused effects failed to expire");
        Time.time=30;Emit(delay:1);
        object pending=null;foreach(object slot in Slots(pool))if(Get<bool>(slot,"Active"))pending=slot;
        Check(pending!=null&&!Get<MeshRenderer>(pending,"Renderer").enabled,"delayed effect flashed early");
        Time.time=30.5f;Invoke(pool,"Update");Check(!Get<MeshRenderer>(pending,"Renderer").enabled,"delay ignored");
        Time.time=31.25f;Invoke(pool,"Update");Check(Get<MeshRenderer>(pending,"Renderer").enabled,"delayed effect never drew");
        Time.time=50;Invoke(pool,"Update");
        int objects=pool.transform.children.Count;
        for(int i=0;i<1000;i++)
        {
            Time.time=i+100;HeroVfxPool.Trace(Vector3.zero,new Vector3(3,2,5),new Color(1,.2f,.1f),.11f,.1f);
            Time.time+=.2f;Invoke(pool,"Update");
        }
        Check(pool.transform.children.Count==objects&&objects==192,"trace created per-shot objects");
        Invoke(pool,"OnDestroy");
    }
    private static void TraceChecks()
    {
        HeroVfxPool pool=Pool();var random=new Random(14211);
        Vector3[] axes={Vector3.up,-Vector3.up,Vector3.forward,-Vector3.forward,Vector3.right,-Vector3.right};
        for(int i=0;i<500;i++)
        {
            Time.time=i;
            Vector3 from=new Vector3((float)random.NextDouble()*50-25,(float)random.NextDouble()*5,(float)random.NextDouble()*50-25);
            Vector3 to=from+new Vector3((float)random.NextDouble()*12-6,(float)random.NextDouble()*10-5,(float)random.NextDouble()*12-6);
            if(i<axes.Length)to=from+axes[i]*(.6f+i);
            HeroVfxPool.Trace(from,to,new Color(.2f,.8f,1),.11f,.1f);
            object active=null;foreach(object slot in Slots(pool))if(Get<bool>(slot,"Active"))active=slot;
            Transform t=Get<Transform>(active,"Transform");
            Vector3 start=t.TransformPoint(new Vector3(0,0,-1.4f));
            Vector3 end=t.TransformPoint(new Vector3(0,0,.55f));
            Check(Vector3.Distance(start,from)<.00002f,"trace muzzle overshoot");
            Check(Vector3.Distance(end,to)<.00002f,"trace contact overshoot");
            Check(Math.Abs(t.localScale.x*.32f-.11f)<1e-6,"trace width changed");
            Time.time+=.2f;Invoke(pool,"Update");
        }
        HeroVfxPool.Trace(Vector3.zero,Vector3.zero,new Color(1,1,1),.11f,.1f);
        Check(Active(pool)==0,"zero-length trace emitted");Invoke(pool,"OnDestroy");
    }
    private static void BudgetChecks()
    {
        foreach(int target in new[]{30,60,120,144})
        {
            var state=new VfxQualityHysteresis();
            for(int i=0;i<1000;i++) {state.Observe(target*(i%2==0?.80f:.84f),1,target);Check(!state.UnderBudget,"budget flapped near threshold");}
            state.Observe(target*.7f,1,target);state.Observe(target*.7f,1,target);Check(state.UnderBudget,"sustained overload ignored");
            for(int i=0;i<500;i++){state.Observe(target,1,target);state.Observe(target*.93f,1,target);Check(state.UnderBudget,"recovery flapped");}
            for(int i=0;i<3;i++){state.Observe(target,1,target);Check(state.UnderBudget,"recovered too early");}
            state.Observe(target,1,target);Check(!state.UnderBudget,"sustained recovery ignored");
            state.Observe(0,50,target);Check(!state.UnderBudget,"single stall counted as sustained overload");
            state.Reset();state.Observe(float.NaN,1,target);state.Observe(20,float.PositiveInfinity,target);Check(!state.UnderBudget,"invalid sample poisoned state");
            state.Observe(0,1,target);state.Observe(0,1,target);state.Observe(target,1,target+1);Check(!state.UnderBudget,"target change retained old debt");
        }
    }
    private static void AudioChecks()
    {
        float reference=AudioDuckEnvelope.Advance(1,true,.2f);
        foreach(int rate in new[]{30,60,120,144})
        {
            float gain=1;
            for(int i=0;i<rate*2;i++)
            {
                float next=AudioDuckEnvelope.Advance(gain,true,1f/rate);
                Check(next<=gain+1e-6&&next>=.55f,"duck attack not monotonic/bounded");gain=next;
            }
            Check(Math.Abs(gain-.55f)<.00001f,"duck attack target incorrect");
            for(int i=0;i<rate*2;i++)
            {
                float next=AudioDuckEnvelope.Advance(gain,false,1f/rate);
                Check(next>=gain-1e-6&&next<=1,"duck release not monotonic/bounded");gain=next;
            }
            Check(gain>.9999f,"duck release failed");
            gain=1;for(int i=0;i<rate;i++)gain=AudioDuckEnvelope.Advance(gain,true,.2f/rate);
            Check(Math.Abs(gain-reference)<.000005f,"audio envelope depends on frame rate");
        }
        Check(AudioDuckEnvelope.Advance(.7f,true,0)==.7f,"zero delta moved envelope");
        Check(AudioDuckEnvelope.Advance(.7f,true,float.NaN)==.7f,"invalid delta poisoned envelope");
        Check(AudioDuckEnvelope.Advance(float.NaN,false,0)==1,"invalid gain not recovered");
    }
    private static void MonitorChecks()
    {
        Application.isFocused=true;Application.isMobilePlatform=true;Time.timeScale=1;
        MobilePerformanceProfile.Current=MobileQuality.Performance;
        var monitor=new GameObject("Monitor regression").AddComponent<RuntimePerformanceMonitor>();
        Invoke(monitor,"OnEnable");Time.unscaledDeltaTime=1f/40;
        for(int i=0;i<140;i++)Invoke(monitor,"Update");
        Check(RuntimePerformanceMonitor.UnderBudget,"actual monitor ignored sustained overload");
        Check(Math.Abs(RuntimePerformanceMonitor.SmoothedFps-40)<.01f,"actual monitor FPS calculation");
        float fps=RuntimePerformanceMonitor.SmoothedFps;
        Time.timeScale=0;Time.unscaledDeltaTime=2;
        for(int i=0;i<30;i++)Invoke(monitor,"Update");
        Check(RuntimePerformanceMonitor.UnderBudget&&RuntimePerformanceMonitor.SmoothedFps==fps,"pause polluted FPS sample");
        Time.timeScale=1;Time.unscaledDeltaTime=1f/60;
        for(int i=0;i<340;i++)Invoke(monitor,"Update");
        Check(!RuntimePerformanceMonitor.UnderBudget,"actual monitor failed sustained recovery");
        MobilePerformanceProfile.Current=MobileQuality.Battery;Time.unscaledDeltaTime=1f/30;
        for(int i=0;i<100;i++)Invoke(monitor,"Update");
        Check(!RuntimePerformanceMonitor.UnderBudget,"battery target incorrectly remained 60 Hz");
        Check(AdaptiveVfxBudget.SurfaceDetail<.5f&&!AdaptiveVfxBudget.AllowDecorative,"battery requested expensive detail");
        Application.isMobilePlatform=false;Application.targetFrameRate=120;Time.unscaledDeltaTime=1f/120;
        for(int i=0;i<380;i++)Invoke(monitor,"Update");
        Check(!RuntimePerformanceMonitor.UnderBudget,"desktop used mobile setting instead of its target");
        Check(Math.Abs(RuntimePerformanceMonitor.SmoothedFps-120)<.01f,"desktop FPS calculation");
        Invoke(monitor,"OnDisable");Check(RuntimePerformanceMonitor.SmoothedFps==0&&!RuntimePerformanceMonitor.UnderBudget,"disabled monitor left static debt");
        Application.isMobilePlatform=true;Application.targetFrameRate=60;MobilePerformanceProfile.Current=MobileQuality.Performance;
    }
    public static void Main()
    {
        BudgetChecks();AudioChecks();MonitorChecks();PoolChecks();TraceChecks();
        Console.WriteLine("PASS: "+checks+" assertions; production VFX scheduler/Draw/Trace, quality hysteresis and audio envelope. Mesh factory and engine substituted.");
    }
}
