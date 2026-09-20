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
        Material stone,energy,riftDark; Mesh fracture,brace,riftBed,riftRims;
        Vector3[] bedVertices,rimVertices;float nextGroundTick;
        readonly LineRenderer[] veins=new LineRenderer[Count];
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
                GameObject vein=new("DF_FrontGroundBranch");vein.transform.SetParent(transform,false);
                veins[i]=vein.AddComponent<LineRenderer>();veins[i].sharedMaterial=energy;veins[i].useWorldSpace=true;
                veins[i].positionCount=4;veins[i].widthMultiplier=.045f;veins[i].shadowCastingMode=ShadowCastingMode.Off;
                var block=new MaterialPropertyBlock();Color tint=StylizedArtDirection.LumenEnergy*.6f;tint.a=1;
                block.SetColor("_BaseColor",tint);block.SetColor("_Color",tint);braces[i].GetComponent<Renderer>().SetPropertyBlock(block);
            }
            for(int i=0;i<arcs.Length;i++)
            {
                GameObject go=new("DF_CollisionDischarge");go.transform.SetParent(transform,false);
                var line=go.AddComponent<LineRenderer>();line.sharedMaterial=energy;line.positionCount=7;
                line.useWorldSpace=true;line.widthMultiplier=.065f;line.shadowCastingMode=ShadowCastingMode.Off;line.enabled=false;arcs[i]=line;
            }
            if(front!=null){pressure=front.LumenControl;front.ControlChanged+=Control;}
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
            UpdateGroundFault(seam);
            pressure=Mathf.Lerp(pressure,front!=null?front.LumenControl:50,1-Mathf.Exp(-dt*4));
            float dominance=Mathf.Abs(pressure-50)/50;
            float intensity=Mathf.Clamp01(elapsed/540)*.45f+dominance*.35f+(elapsed<eventUntil?.2f:0);
            float release=breakthrough?Mathf.Sin(Mathf.Clamp01((Time.unscaledTime-breakAt)/1.25f)*Mathf.PI):0;
            for(int i=0;i<Count;i++)
            {
                int index=1+Mathf.FloorToInt(i*(seam.Length-3f)/(Count-1));Vector3 p=seam[index];
                // Preserve quiet gaps at both lane crossings; heroes and warnings remain primary.
                bool corridor=Mathf.Abs(Mathf.Abs(p.z)-16)<2.8f||Mathf.Abs(p.x)<4.5f&&Mathf.Abs(p.z)<13f;
                fragments[i].gameObject.SetActive(!corridor);braces[i].gameObject.SetActive(!corridor);
                veins[i].enabled=!corridor;
                if(!corridor)
                {
                    float side=i%2==0?-1:1;Color tint=side<0?StylizedArtDirection.ZeroEnergy:StylizedArtDirection.LumenEnergy;
                    float weight=side<0?1-pressure/100:pressure/100;
                    tint=Color.Lerp(new Color(.12f,.12f,.16f),tint,.35f+weight*.4f+release*.2f);
                    veins[i].startColor=veins[i].endColor=tint;
                    for(int j=0;j<4;j++)veins[i].SetPosition(j,p+new Vector3(side*(.18f+j*.48f),.035f,Mathf.Sin(i*2.1f+j*1.9f)*.28f));
                }
                float phase=i*2.39f;
                float zeroWeight=1-pressure/100, lumenWeight=pressure/100;
                fragments[i].position=p+new Vector3(-.5f-zeroWeight*.5f-release*1.4f,.32f+Mathf.Sin(elapsed*.7f+phase)*.12f+i%3*.17f,Mathf.Sin(phase)*.5f);
                fragments[i].localScale=new Vector3(.38f,.68f,.44f)*(1+zeroWeight*.45f+release*.25f);
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
        void UpdateGroundFault(Vector3[] seam)
        {
            if(Time.time<nextGroundTick)return;nextGroundTick=Time.time+.1f;
            if(riftBed==null)
            {
                Shader shader=Shader.Find("Universal Render Pipeline/Unlit")??Shader.Find("Unlit/Color");if(shader==null)return;
                riftDark=new Material(shader){name="DF_RiftDepth",color=new Color(.012f,.014f,.022f)};
                riftBed=new Mesh{name="DF_RiftGroundShadow"};riftRims=new Mesh{name="DF_RiftBrokenBanks"};riftBed.MarkDynamic();riftRims.MarkDynamic();
                bedVertices=new Vector3[seam.Length*2];rimVertices=new Vector3[seam.Length*4];
                int[] bt=new int[(seam.Length-1)*6],rt=new int[(seam.Length-1)*12];
                for(int i=0;i<seam.Length-1;i++)
                {
                    int n=i*2,q=i*6;bt[q]=n;bt[q+1]=n+2;bt[q+2]=n+1;bt[q+3]=n+1;bt[q+4]=n+2;bt[q+5]=n+3;
                    for(int side=0;side<2;side++){n=i*4+side*2;q=i*12+side*6;rt[q]=n;rt[q+1]=n+4;rt[q+2]=n+1;rt[q+3]=n+1;rt[q+4]=n+4;rt[q+5]=n+5;}
                }
                riftBed.vertices=bedVertices;riftBed.triangles=bt;riftRims.vertices=rimVertices;riftRims.triangles=rt;
                Part("DF_FrontGroundDepth",riftBed,riftDark);Part("DF_FrontBrokenBanks",riftRims,stone);
            }
            for(int i=0;i<seam.Length;i++)
            {
                Vector3 p=seam[i];bool quiet=Mathf.Abs(Mathf.Abs(p.z)-16)<3||Mathf.Abs(p.x)<4.5f&&Mathf.Abs(p.z)<13;
                float half=quiet?.16f:.58f+Mathf.Sin(i*7.1f)*.16f;
                bedVertices[i*2]=transform.InverseTransformPoint(p+new Vector3(-half,.006f,0));
                bedVertices[i*2+1]=transform.InverseTransformPoint(p+new Vector3(half,.006f,0));
                rimVertices[i*4]=transform.InverseTransformPoint(p+new Vector3(-half-(quiet?.03f:.27f),.012f,0));
                rimVertices[i*4+1]=transform.InverseTransformPoint(p+new Vector3(-half,.045f,0));
                rimVertices[i*4+2]=transform.InverseTransformPoint(p+new Vector3(half,.045f,0));
                rimVertices[i*4+3]=transform.InverseTransformPoint(p+new Vector3(half+(quiet?.03f:.32f),.012f,0));
            }
            riftBed.vertices=bedVertices;riftBed.RecalculateBounds();
            riftRims.vertices=rimVertices;riftRims.RecalculateNormals();riftRims.RecalculateBounds();
        }
        void OnDestroy()
        {
            if(front!=null)front.ControlChanged-=Control;
            DimensionPlanetDirector.PlanetArrived-=PlanetPulse;
            if(riftBed!=null)Destroy(riftBed);if(riftRims!=null)Destroy(riftRims);if(riftDark!=null)Destroy(riftDark);
            if(stone!=null)Destroy(stone);if(energy!=null)Destroy(energy);if(fracture!=null)Destroy(fracture);if(brace!=null)Destroy(brace);
        }
    }
}
