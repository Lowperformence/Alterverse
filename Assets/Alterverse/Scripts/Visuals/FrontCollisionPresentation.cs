using Alterverse.Combat;
using Alterverse.Match;
using UnityEngine;
using UnityEngine.Rendering;
namespace Alterverse.Visuals
{
    // A bounded three-dimensional collision field. No physics, particles, lights or screen distortion.
    public sealed class FrontCollisionPresentation : MonoBehaviour
    {
        const int Count=12;
        readonly Transform[] fragments=new Transform[Count], braces=new Transform[Count];
        readonly LineRenderer[] arcs=new LineRenderer[3];
        readonly float[] arcEnds=new float[3];
        readonly int[] arcSites=new int[3];
        Material stone,energy; Mesh fracture,brace;
        DimensionsFront front; float elapsed,nextArc,eventUntil,breakAt=-100,pressure=50;
        int sequence; bool initialized;
        public void Initialize(DimensionsFront state)
        {
            if(initialized)return; initialized=true;front=state;
            Shader lit=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
            Shader ink=Shader.Find("Universal Render Pipeline/Particles/Unlit")??Shader.Find("Sprites/Default");
            if(lit==null||ink==null){enabled=false;return;}
            stone=new Material(lit){name="DF_FrontStone",color=new Color(.10f,.085f,.14f)};
            energy=new Material(ink){name="DF_FrontEnergy"};
            fracture=LunarFragmentGeometry.Create(79);
            // A split, open chevron construction, not a filled wall across combat sightlines.
            brace=new Mesh{name="DF_ControlledRealityBrace"};
            brace.vertices=new[]{new Vector3(-.4f,0,0),new Vector3(0,.8f,0),new Vector3(.4f,0,0),new Vector3(.30f,0,0),new Vector3(0,.58f,0),new Vector3(-.30f,0,0)};
            brace.triangles=new[]{0,1,4,0,4,5,1,2,3,1,3,4,4,1,0,5,4,0,3,2,1,4,3,1};brace.RecalculateNormals();brace.RecalculateBounds();
            for(int i=0;i<Count;i++)
            {
                fragments[i]=Part("DF_ZeroSuspendedFault",fracture,stone);
                braces[i]=Part("DF_LumenStabilizer",brace,energy);
                var block=new MaterialPropertyBlock();Color tint=StylizedArtDirection.LumenEnergy*.6f;tint.a=1;
                block.SetColor("_BaseColor",tint);block.SetColor("_Color",tint);braces[i].GetComponent<Renderer>().SetPropertyBlock(block);
            }
            for(int i=0;i<arcs.Length;i++)
            {
                GameObject go=new("DF_CollisionDischarge");go.transform.SetParent(transform,false);
                var line=go.AddComponent<LineRenderer>();line.sharedMaterial=energy;line.positionCount=7;
                line.useWorldSpace=true;line.widthMultiplier=.035f;line.shadowCastingMode=ShadowCastingMode.Off;line.enabled=false;arcs[i]=line;
            }
            if(front!=null){pressure=front.LumenControl;front.ControlChanged+=Control;}
            LaneWaveSpawner.SiegeWaveSpawned+=EventPulse;
            DimensionPlanetDirector.PlanetArrived+=PlanetPulse;
        }
        Transform Part(string name,Mesh mesh,Material material)
        {
            GameObject go=new(name);go.transform.SetParent(transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;return go.transform;
        }
        void Control(float zero,float lumen)
        {
            if((zero>=100||lumen>=100)&&breakAt<0){breakAt=Time.unscaledTime;EventPulse();}
        }
        void PlanetPulse(Vector3 position)=>EventPulse();
        void EventPulse(){eventUntil=elapsed+2.2f;nextArc=elapsed;}
        public void Tick(Vector3[] seam)
        {
            if(stone==null||seam==null||seam.Length<2)return;
            bool breakthrough=Time.unscaledTime-breakAt<1.25f;
            if(MatchClock.InBackground)return;
            float dt=breakthrough?Time.unscaledDeltaTime:Time.deltaTime;
            if(dt<=0){for(int i=0;i<arcs.Length;i++)arcs[i].enabled=false;return;}elapsed+=dt;
            pressure=Mathf.Lerp(pressure,front!=null?front.LumenControl:50,1-Mathf.Exp(-dt*4));
            float dominance=Mathf.Abs(pressure-50)/50;
            float intensity=Mathf.Clamp01(elapsed/540)*.45f+dominance*.35f+(elapsed<eventUntil?.2f:0);
            float release=breakthrough?Mathf.Sin(Mathf.Clamp01((Time.unscaledTime-breakAt)/1.25f)*Mathf.PI):0;
            for(int i=0;i<Count;i++)
            {
                int index=1+Mathf.FloorToInt(i*(seam.Length-3f)/(Count-1));Vector3 p=seam[index];
                // Preserve quiet gaps at both lane crossings; heroes and warnings remain primary.
                bool corridor=Mathf.Abs(Mathf.Abs(p.z)-16)<2.8f;
                fragments[i].gameObject.SetActive(!corridor);braces[i].gameObject.SetActive(!corridor);
                float phase=i*2.39f;
                float zeroWeight=1-pressure/100, lumenWeight=pressure/100;
                fragments[i].position=p+new Vector3(-.5f-zeroWeight*.5f-release*1.4f,.32f+Mathf.Sin(elapsed*.7f+phase)*.12f+i%3*.17f,Mathf.Sin(phase)*.5f);
                fragments[i].localScale=new Vector3(.25f,.45f,.35f)*(1+zeroWeight*.45f+release*.25f);
                fragments[i].localScale*=1+release*(pressure<50?.4f:-.85f);
                fragments[i].rotation=Quaternion.Euler(18+Mathf.Sin(elapsed*.5f+phase)*9,i*73,23);
                braces[i].position=p+new Vector3(.5f+lumenWeight*.5f+release*1.4f,.2f+Mathf.Sin(elapsed*.7f)*.04f,0);
                braces[i].localScale=Vector3.one*(.7f+lumenWeight*.4f);braces[i].localScale*=1+release*(pressure>50?.4f:-.85f);braces[i].rotation=Quaternion.Euler(0,90,0);
            }
            if(elapsed>=nextArc)
            {
                int slot=sequence%3;sequence++;int site=(sequence*7)%Count;arcSites[slot]=site;
                arcEnds[slot]=elapsed+.20f+intensity*.14f;
                nextArc=elapsed+Mathf.Lerp(2.3f,.65f,intensity)+Mathf.Sin(sequence*7.1f)*.2f;
            }
            for(int i=0;i<arcs.Length;i++)
            {
                Transform a=fragments[arcSites[i]],b=braces[arcSites[i]];
                bool show=elapsed<arcEnds[i]&&a.gameObject.activeSelf;
                arcs[i].enabled=show;if(!show)continue;
                arcs[i].startColor=StylizedArtDirection.ZeroEnergy;arcs[i].endColor=StylizedArtDirection.LumenEnergy;
                for(int j=0;j<7;j++){float t=j/6f;Vector3 p=Vector3.Lerp(a.position,b.position,t);p.y+=Mathf.Sin(t*Mathf.PI)*(1+intensity*.5f+release);p.z+=Mathf.Sin(j*8.1f+sequence)*.09f;arcs[i].SetPosition(j,p);}
            }
        }
        void OnDestroy()
        {
            if(front!=null)front.ControlChanged-=Control;
            LaneWaveSpawner.SiegeWaveSpawned-=EventPulse;DimensionPlanetDirector.PlanetArrived-=PlanetPulse;
            if(stone!=null)Destroy(stone);if(energy!=null)Destroy(energy);if(fracture!=null)Destroy(fracture);if(brace!=null)Destroy(brace);
        }
    }
}
