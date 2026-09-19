using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Visuals;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Match
{
    /// <summary>
    /// One-shot mid-match objective. Opens at 5:00 for 60 seconds.
    /// Exclusive hero presence earns control time; contested presence earns neither team.
    /// </summary>
    public sealed class HotZoneDirector : MonoBehaviour
    {
        public const float OpenAt=300f;
        public const float ActiveDuration=60f;
        public const float Radius=6.5f;

        private enum State { Waiting, Active, Resolved }
        private State state;
        private static HotZoneDirector owner;
        private bool rewardIssued;
        private float elapsed,activeElapsed,zeroTime,lumenTime,nextScan;
        private int zeroInside,lumenInside;
        private GameObject zoneRoot;
        private Transform pulseRing,innerRing;
        private Renderer[] markerRenderers=System.Array.Empty<Renderer>();
        private Material neutralMat,zeroMat,lumenMat;
        private DimensionsFront front;

        public static event System.Action HotZoneOpening;
        public static event System.Action HotZoneOpened;
        public static event System.Action<int,float,float> HotZoneResolved;

        public float ZeroControlSeconds=>zeroTime;
        public float LumenControlSeconds=>lumenTime;
        public float Remaining=>state==State.Active?Mathf.Max(0,ActiveDuration-activeElapsed):state==State.Waiting?Mathf.Max(0,OpenAt-elapsed):0;
        public bool IsActive=>state==State.Active;
        public int ZeroInside=>zeroInside;
        public int LumenInside=>lumenInside;

        private void Awake()
        {
            if(owner!=null&&owner!=this){enabled=false;Destroy(gameObject);return;}
            owner=this;front=FindAnyObjectByType<DimensionsFront>();
        }
        private void OnDisable(){if(owner==this)owner=null;}

        private void Update()
        {
            if(Time.timeScale<=0f||state==State.Resolved)return;
            if(front==null)front=FindAnyObjectByType<DimensionsFront>();
            if(front!=null&&front.HasEnded){ResolveNoReward();return;}

            elapsed+=Time.deltaTime;
            if(state==State.Waiting)
            {
                if(elapsed>=OpenAt-10f&&elapsed-Time.deltaTime<OpenAt-10f)HotZoneOpening?.Invoke();
                if(elapsed<OpenAt)return;
                Open();
            }

            if(state!=State.Active)return;
            activeElapsed+=Time.deltaTime;
            if(Time.time>=nextScan){nextScan=Time.time+.10f;ScanHeroes();}

            if(zeroInside>0&&lumenInside==0)zeroTime+=Time.deltaTime;
            else if(lumenInside>0&&zeroInside==0)lumenTime+=Time.deltaTime;

            AnimateZone();
            if(activeElapsed>=ActiveDuration)Resolve();
        }

        private void Open()
        {
            state=State.Active;activeElapsed=0;zeroTime=0;lumenTime=0;zeroInside=lumenInside=0;
            BuildZoneVisual();
            PrototypeVfx.RadialBurst(transform.position,new Color(.82f,.86f,1f),Radius*.92f);
            PrototypeVfx.Burst(transform.position,new Color(.82f,.86f,1f),1.15f,10);
            HotZoneOpened?.Invoke();
        }

        private void ScanHeroes()
        {
            zeroInside=0;lumenInside=0;
            float radiusSqr=Radius*Radius;
            for(int i=0;i<Health.Active.Count;i++)
            {
                Health h=Health.Active[i];
                if(h==null||h.IsDead||!h.isActiveAndEnabled||h.GetComponent<HeroIdentity>()==null)continue;
                TeamMember member=h.GetComponent<TeamMember>();if(member==null)continue;
                Vector3 d=h.transform.position-transform.position;d.y=0;
                if(d.sqrMagnitude>radiusSqr)continue;
                if(member.Team==0)zeroInside++;else if(member.Team==1)lumenInside++;
            }
        }

        private void Resolve()
        {
            if(state==State.Resolved)return;
            state=State.Resolved;
            front??=FindAnyObjectByType<DimensionsFront>();
            if(front!=null&&front.HasEnded){ResolveNoReward();return;}

            int winner=zeroTime>lumenTime+.05f?0:lumenTime>zeroTime+.05f?1:-1;
            if(winner>=0&&!rewardIssued)
            {
                rewardIssued=HotZoneTank.Spawn(winner);
                if(!rewardIssued)winner=-1;
            }
            Color resolutionColor=winner==0?StylizedArtDirection.ZeroEnergy:winner==1?StylizedArtDirection.LumenEnergy:new Color(.72f,.74f,.80f);
            PrototypeVfx.RadialBurst(transform.position,resolutionColor,Radius);
            PrototypeVfx.Burst(transform.position,resolutionColor,1.35f,12);
            HotZoneResolved?.Invoke(winner,zeroTime,lumenTime);
            if(zoneRoot!=null)Destroy(zoneRoot,1.2f);
        }

        private void ResolveNoReward()
        {
            state=State.Resolved;rewardIssued=true;
            if(zoneRoot!=null)Destroy(zoneRoot);
        }

        private void BuildZoneVisual()
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
            if(shader==null)return;
            neutralMat=CreateMaterial(shader,new Color(.42f,.44f,.52f,1),1.1f);
            zeroMat=CreateMaterial(shader,StylizedArtDirection.ZeroEnergy,2.1f);
            lumenMat=CreateMaterial(shader,StylizedArtDirection.LumenEnergy,2.1f);

            zoneRoot=new GameObject("DF_HotZoneVisual");zoneRoot.transform.position=transform.position;
            Transform world=GameObject.Find("DF_MatchWorld")?.transform;if(world!=null)zoneRoot.transform.SetParent(world,true);

            pulseRing=CreateRing(zoneRoot.transform,"DF_HotZoneOuter",Radius,.14f,neutralMat,64);
            innerRing=CreateRing(zoneRoot.transform,"DF_HotZoneInner",Radius*.72f,.075f,neutralMat,48);
            CreateRing(zoneRoot.transform,"DF_HotZoneCoreRing",Radius*.34f,.055f,neutralMat,32);
            GameObject center=GameObject.CreatePrimitive(PrimitiveType.Cylinder);center.name="DF_HotZoneCenter";
            center.transform.SetParent(zoneRoot.transform,false);center.transform.localPosition=Vector3.up*.045f;center.transform.localScale=new Vector3(2.1f,.018f,2.1f);
            Collider centerCollider=center.GetComponent<Collider>();if(centerCollider!=null)centerCollider.enabled=false;
            Renderer centerRenderer=center.GetComponent<Renderer>();if(centerRenderer!=null){centerRenderer.sharedMaterial=neutralMat;centerRenderer.shadowCastingMode=ShadowCastingMode.Off;centerRenderer.receiveShadows=false;}
            markerRenderers=new Renderer[8];
            for(int i=0;i<8;i++)
            {
                float a=i*Mathf.PI*2f/8f;
                Vector3 p=new(Mathf.Cos(a)*(Radius-.45f),.18f,Mathf.Sin(a)*(Radius-.45f));
                GameObject shard=GameObject.CreatePrimitive(PrimitiveType.Cube);shard.name="DF_HotZoneMarker";shard.transform.SetParent(zoneRoot.transform,false);
                shard.transform.localPosition=p;shard.transform.localRotation=Quaternion.Euler(0,-a*Mathf.Rad2Deg,45);
                shard.transform.localScale=new Vector3(.10f,.42f,.22f);
                Collider c=shard.GetComponent<Collider>();if(c!=null)c.enabled=false;
                Renderer r=shard.GetComponent<Renderer>();
                if(r!=null){r.sharedMaterial=neutralMat;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;markerRenderers[i]=r;}
            }
        }

        private void AnimateZone()
        {
            if(zoneRoot==null)return;
            bool zero=zeroInside>0&&lumenInside==0,lumen=lumenInside>0&&zeroInside==0;
            Material active=zero?zeroMat:lumen?lumenMat:neutralMat;
            if(pulseRing!=null&&pulseRing.TryGetComponent(out LineRenderer a))a.sharedMaterial=active;
            if(innerRing!=null&&innerRing.TryGetComponent(out LineRenderer b))b.sharedMaterial=active;
            for(int i=0;i<markerRenderers.Length;i++)
                if(markerRenderers[i]!=null)markerRenderers[i].sharedMaterial=active;
            zoneRoot.transform.Rotate(0,9f*Time.deltaTime,0,Space.World);
            float pulse=1f+Mathf.Sin(Time.time*4.2f)*.028f;
            if(pulseRing!=null)pulseRing.localScale=new Vector3(pulse,1,pulse);
        }

        private static Material CreateMaterial(Shader shader,Color color,float emission)
        {
            Material m=new(shader){color=color};
            if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",color);
            if(m.HasProperty("_EmissionColor")){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*emission);}
            if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",.35f);
            return m;
        }

        private static Transform CreateRing(Transform parent,string name,float radius,float width,Material material,int segments)
        {
            GameObject go=new(name);go.transform.SetParent(parent,false);go.transform.localPosition=Vector3.up*.12f;
            LineRenderer lr=go.AddComponent<LineRenderer>();lr.sharedMaterial=material;lr.loop=true;lr.useWorldSpace=false;lr.positionCount=segments;
            lr.widthMultiplier=width;lr.shadowCastingMode=ShadowCastingMode.Off;lr.receiveShadows=false;
            for(int i=0;i<segments;i++){float a=i*Mathf.PI*2f/segments;lr.SetPosition(i,new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius));}
            return go.transform;
        }

        private void OnDestroy()
        {
            if(neutralMat!=null)Destroy(neutralMat);if(zeroMat!=null)Destroy(zeroMat);if(lumenMat!=null)Destroy(lumenMat);
        }
    }
}
