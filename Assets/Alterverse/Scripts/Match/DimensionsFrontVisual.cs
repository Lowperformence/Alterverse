using Alterverse.Visuals;
using UnityEngine;
using UnityEngine.Rendering;
namespace Alterverse.Match
{
    public sealed class DimensionsFrontVisual:MonoBehaviour
    {
        [SerializeField]private float arenaHalfWidth=39f,frontLength=72f,smoothing=6f,waveAmplitude=.8f,waveSpeed=1.15f;
        [SerializeField]private int segmentCount=25;
        private DimensionsFront front;
        private float targetX,impactZ,impactStrength,impactTime=-99;
        private Vector3[] center,zero,lumen;
        private LineRenderer fault,zeroEdge,lumenEdge;
        private Material material;
        private FrontCollisionPresentation collision;
        private void Start()
        {
            front=FindAnyObjectByType<DimensionsFront>();segmentCount=Mathf.Max(9,segmentCount);
            center=new Vector3[segmentCount];zero=new Vector3[segmentCount];lumen=new Vector3[segmentCount];
            Shader shader=Shader.Find("Universal Render Pipeline/Particles/Unlit")??Shader.Find("Sprites/Default");
            if(shader==null){enabled=false;return;}material=new Material(shader){name="DF_FrontFractureInk"};
            fault=Stroke("DF_GeologicalFront",.19f,new Color(.035f,.045f,.065f));
            zeroEdge=Stroke("DF_ZeroFractureEdge",.038f,Color.Lerp(StylizedArtDirection.ZeroEnergy,Color.gray,.35f));
            lumenEdge=Stroke("DF_LumenFractureEdge",.038f,Color.Lerp(StylizedArtDirection.LumenEnergy,Color.gray,.35f));
            if(front!=null){targetX=ControlToX(front.LumenControl);front.ControlChanged+=OnControlChanged;}
            for(int i=0;i<segmentCount;i++)center[i]=new Vector3(targetX,.14f,-frontLength*.5f+i*frontLength/(segmentCount-1));
            UpdateLines(1);
            collision=gameObject.AddComponent<FrontCollisionPresentation>();collision.Initialize(front);
        }
        private LineRenderer Stroke(string label,float width,Color color)
        {
            GameObject obj=new GameObject(label);obj.transform.SetParent(transform,false);
            LineRenderer line=obj.AddComponent<LineRenderer>();line.sharedMaterial=material;line.useWorldSpace=true;
            line.positionCount=segmentCount;line.widthMultiplier=width;line.startColor=line.endColor=color;
            line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;line.numCapVertices=0;line.numCornerVertices=0;return line;
        }
        public void PulseImpact(float worldZ,int scoringTeam)
        {impactZ=Mathf.Clamp(worldZ,-frontLength*.5f,frontLength*.5f);impactStrength=scoringTeam==1?-1:1;impactTime=Time.time;}
        private void Update(){if(fault!=null&&Time.timeScale>0)UpdateLines(1-Mathf.Exp(-smoothing*Time.deltaTime));if(collision!=null)collision.Tick(center);}
        private void UpdateLines(float blend)
        {
            float age=Time.time-impactTime,fade=age>=0&&age<1.8f?1-age/1.8f:0;
            for(int i=0;i<center.Length;i++)
            {
                float distance=Mathf.Abs(center[i].z-impactZ),impact=fade*Mathf.Exp(-distance*.16f)*impactStrength*1.8f;
                float fracture=Mathf.Sin(i*2.37f)*waveAmplitude*.28f+Mathf.Sin(Time.time*waveSpeed+i*.72f)*waveAmplitude*.12f;
                Vector3 p=center[i];p.x=Mathf.Lerp(p.x,targetX+fracture+impact,blend);center[i]=p;
                zero[i]=p+new Vector3(-.085f,.012f,0);lumen[i]=p+new Vector3(.085f,.012f,0);
            }
            fault.SetPositions(center);zeroEdge.SetPositions(zero);lumenEdge.SetPositions(lumen);
        }
        private void OnControlChanged(float zeroControl,float lumenControl)=>targetX=ControlToX(lumenControl);
        private float ControlToX(float value)=>Mathf.Lerp(arenaHalfWidth,-arenaHalfWidth,value/100);
        private void OnDestroy(){if(front!=null)front.ControlChanged-=OnControlChanged;if(material!=null)Destroy(material);}
    }
}
