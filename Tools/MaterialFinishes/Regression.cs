using System;
using System.Collections.Generic;
using Alterverse.Visuals;
using UnityEngine;

namespace UnityEngine
{
    public class Object {public string name;}
    public class Texture2D:Object {}
    public class Shader:Object
    {
        public bool isSupported=true;
        public static Dictionary<string,object> Globals=new Dictionary<string,object>();
        public static void SetGlobalTexture(string n,Texture2D t)=>Globals[n]=t;
        public static void SetGlobalFloat(string n,float f)=>Globals[n]=f;
    }
    public struct Color {public float r,g,b,a;public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}}
    public struct Vector2 {public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}}
    public static class Mathf {
        public static float Clamp(float f,float a,float b)=>Math.Max(a,Math.Min(b,f));
        public static float Repeat(float f,float n)=>f-(float)Math.Floor(f/n)*n;
        public static float Lerp(float a,float b,float t)=>a+(b-a)*Clamp(t,0,1);
    }
    public class Material:Object
    {
        public Shader shader;public int renderQueue=2000;public Color color;
        public int writes;
        public Dictionary<string,object> Properties=new Dictionary<string,object>();
        public HashSet<string> Keywords=new HashSet<string>();
        public Material(string n,string s){name=n;shader=new Shader{name=s};Properties["_Metallic"]=0f;Properties["_Smoothness"]=.4f;Properties["_BaseColor"]=new Color(.2f,.3f,.5f,.5f);Properties["_DetailNormalMap"]=null;}
        public bool HasProperty(string n)=>Properties.ContainsKey(n);
        public float GetFloat(string n)=>Properties.TryGetValue(n,out object v)&&v is float?(float)v:0;
        public Texture2D GetTexture(string n)=>Properties.TryGetValue(n,out object v)?v as Texture2D:null;
        public Color GetColor(string n)=>(Color)Properties[n];
        public void SetTexture(string n,Texture2D t){Properties[n]=t;writes++;}
        public void SetFloat(string n,float f){Properties[n]=f;writes++;}
        public void SetColor(string n,Color c){Properties[n]=c;writes++;}
        public void SetTextureScale(string n,Vector2 s){Properties[n+"Scale"]=s;writes++;}
        public void EnableKeyword(string n){Keywords.Add(n);writes++;}
        public void DisableKeyword(string n){Keywords.Remove(n);writes++;}
        public void SetOverrideTag(string n,string t){Properties[n]=t;writes++;}
    }
    public static class Resources
    {
        public static Dictionary<string,Object> Assets=new Dictionary<string,Object>();public static int loads;
        public static T Load<T>(string n) where T:Object {loads++;return Assets.TryGetValue(n,out Object o)?o as T:null;}
    }
    public enum RuntimeInitializeLoadType {SubsystemRegistration,BeforeSceneLoad}
    public class RuntimeInitializeOnLoadMethodAttribute:Attribute {public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t){}}
}
class Regression
{
    static int checks;
    static void Check(bool ok,string label){checks++;if(!ok)throw new Exception(label);}
    static void Main()
    {
        foreach(string f in new[]{"Metal","Stone","Concrete","Soil","Sand","Bark","Leaf","Ice","Fabric"})
            foreach(string suffix in new[]{"","_Normal"})Resources.Assets["MaterialFinishes/"+f+suffix]=new Texture2D{name=f+suffix};
        var glass=new Shader{name="Alterverse/RuntimeGlass"};Resources.Assets["DF_RuntimeGlass"]=glass;
        Resources.Assets["CombatVfx/FlowNoise"]=new Texture2D{name="flow"};
        GameMaterialFinishes.Refresh();VisualEffectResources.Initialize();
        Check((float)Shader.Globals["_DF_FinishReady"]==1,"shared data loaded");
        Check((float)Shader.Globals["_DF_VfxFlowReady"]==1,"VFX data loaded");
        foreach(string n in new[]{"energy armor","warning metal","telegraph","particle glass","fur","tabby fur","muzzle cream","skin","face","hair","slime","lava"})
            Check(GameMaterialFinishes.Classify(n,1,false)==GameSurfaceFinish.None,"special surface exclusion: "+n);
        Check(GameMaterialFinishes.Classify("window",0,true)==GameSurfaceFinish.Glass,"glass recognition");
        Check(GameMaterialFinishes.Classify("metal smoke",1,true)==GameSurfaceFinish.None,"other transparency preserved");
        var m=new Material("Steel","Alterverse/RuntimeLit");m.SetFloat("_Metallic",.8f);
        var authored=new Texture2D{name="authored"};m.SetTexture("_BaseMap",authored);m.SetTexture("_BumpMap",authored);
        var tint=m.GetColor("_BaseColor");GameMaterialFinishes.Apply(m);
        Check(m.GetTexture("_FinishData").name=="Metal","runtime baked metal assigned");
        Check(m.GetTexture("_BaseMap")==authored&&m.GetTexture("_BumpMap")==authored,"authored maps preserved");
        Check(m.GetColor("_BaseColor").r==tint.r&&m.GetColor("_BaseColor").a==tint.a,"palette/opacity preserved");
        GameMaterialFinishes.Apply(m,"armor");int writes=m.writes,loads=Resources.loads;
        for(int i=0;i<1000;i++)GameMaterialFinishes.Apply(m,"armor");
        Check(m.writes==writes&&Resources.loads==loads,"repeated renderer pass is cached");
        var renamed=new Material("unnamed","Alterverse/RuntimeLit");renamed.SetFloat("_Metallic",.8f);GameMaterialFinishes.Apply(renamed);
        renamed.name="energy shell";GameMaterialFinishes.Apply(renamed,"energy");
        Check(renamed.GetFloat("_FinishStrength")==0&&renamed.GetFloat("_FinishRoughnessWeight")==0,"factory rename retains energy exclusion");
        var import=new Material("Woven astronaut fabric","Universal Render Pipeline/Lit");
        import.SetTexture("_BaseMap",authored);import.SetTexture("_BumpMap",authored);GameMaterialFinishes.Apply(import);
        Check(import.GetTexture("_DetailNormalMap").name=="Fabric_Normal","secondary fabric normal");
        Check(import.GetTexture("_BaseMap")==authored&&import.GetTexture("_BumpMap")==authored,"Timmy authored PBR retained");
        Check(import.GetFloat("_DetailAlbedoMapScale")==0&&import.Keywords.Contains("_DETAIL_SCALED"),"detail colour neutral");
        var preexisting=new Material("metal","Universal Render Pipeline/Lit");preexisting.SetTexture("_DetailNormalMap",authored);GameMaterialFinishes.Apply(preexisting);
        Check(preexisting.GetTexture("_DetailNormalMap")==authored,"authored detail retained");
        var visor=new Material("Timmy visor glass","Universal Render Pipeline/Lit");visor.renderQueue=3000;GameMaterialFinishes.Apply(visor);
        Check(visor.shader==glass&&visor.renderQueue==3000,"transparent PBR glass selected");
        Check(visor.GetColor("_BaseColor").a==.5f,"Timmy 50 percent opacity exact");
        for(int i=0;i<=1000;i++)
        {
            float t=i*.013f;float breath=HeroIdleBreath.Sample(t);
            Check(!float.IsNaN(breath)&&Math.Abs(breath)<=1.00001f,"bounded Rigify/Rokoko curve");
            Check(Math.Abs(breath-HeroIdleBreath.Sample(t+4.6f))<.00003f,"periodic motion");
        }
        var special=new Material("steel","Alterverse/MapWater");GameMaterialFinishes.Apply(special);Check(special.writes==0,"specialized water unchanged");
        var late=new Material("unnamed","Alterverse/RuntimeLit");GameMaterialFinishes.Apply(late);GameMaterialFinishes.Apply(late,"stone");Check(late.GetTexture("_FinishData").name=="Stone","late renderer hint applied");
        foreach(string f in new[]{"Metal","Stone","Concrete","Soil","Sand","Bark","Leaf","Ice","Fabric"})
        {
            var test=new Material(f,"Alterverse/RuntimeLit");GameMaterialFinishes.Apply(test);Check(test.GetTexture("_FinishData").name==f,"finish "+f);
        }
        Resources.Assets.Clear();GameMaterialFinishes.Refresh();VisualEffectResources.Initialize();
        Check((float)Shader.Globals["_DF_FinishReady"]==0&&(float)Shader.Globals["_DF_VfxFlowReady"]==0,"missing assets produce neutral fallback");
        var missing=new Material("visor glass","Universal Render Pipeline/Lit");missing.renderQueue=3000;GameMaterialFinishes.Apply(missing);Check(missing.shader.name=="Universal Render Pipeline/Lit","missing glass fallback");
        Console.WriteLine("PASS "+checks+" production material and effect-resource assertions (Unity API fixtures).");
    }
}
