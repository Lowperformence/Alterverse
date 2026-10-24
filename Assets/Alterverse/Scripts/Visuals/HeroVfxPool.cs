using System.Collections.Generic;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Performance;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    internal enum HeroFxShape { Ring,Hex,Slash,Rift,Orb,Bolt,Shield,Plus,Shard,Cracks,Halo,Burst,Petals,ShockDome,Runes,Seed,RiftCross,ContactStar,Spark,Streak,ArcRibbon,Leaf,AnchorProjectile,WaterSpiral,WaveCrest,AnchorChain,MonsterRock,MonsterDiamond,MonsterSlime,MonsterBeam,PressureWave,GravityEight,AnchorStrikeZero,Laser,EchoSickle,Mist,Claw,TimmyGrenade,TexturedQuad,Count }
    internal enum HeroFxMotion { Hold,Expand,Contract,Travel,Orbit,Sweep,Scatter,Spiral,TravelFixedFrame,Drift,Tether,Lob,RadialPulse,LaserBeam,Sustained,AttachedJet }
    // Presentation-only scheduler. No damage, collision, targeting, coroutines, or runtime mesh rebuilds.
    [DefaultExecutionOrder(1600)]
    public sealed partial class HeroVfxPool:MonoBehaviour
    {
        private const int Capacity=192,WarningReserve=32,CoreReserve=32;
        private sealed class Slot
        {
            public EpicVisual Epic;public bool EpicBeam;public HeroFxShape Shape;
            public Transform Transform;public MeshRenderer Renderer;public MeshFilter Filter;
            public MaterialPropertyBlock Block;
            public Vector3 From,To,Size,AuthoredSize,Offset;public Quaternion Rotation;
            public Color Color;public float Start,Duration,SurfaceStyle,MaxRange;public bool Active,Warning,Decorative,CancelOnOwnerDeath;
            public int DeliveryId;public HeroFxMotion Motion;public Transform Follow,EndFollow;public bool Follows,TracksEnd;
            public Vector3 EndOffset;public Health EndHealth;
            public HeroVisualMotion FollowMotion,EndMotion;
            public GameObject Owner;public Health OwnerHealth,Shield;public bool ShieldBound;public float ShieldGraceUntil;
            public HeroFxSprite Sprite;public bool Billboard;public float Roll;
            public string ExplicitEpicKey;public float ExplicitRadius;
        }
        private static HeroVfxPool instance;
        private static int nextDeliveryId, deliveryScope;
        internal static int BeginDelivery() { deliveryScope = ++nextDeliveryId; return deliveryScope; }
        internal static void EndDelivery() => deliveryScope = 0;
        internal static void CancelDelivery(int id)
        {
            if (id <= 0 || instance == null) return;
            foreach (Slot slot in instance.slots)
                if (slot != null && slot.Active && slot.DeliveryId == id && IsProjectile(slot)) instance.Hide(slot);
        }
        private static bool IsProjectile(Slot slot) => slot.TracksEnd || slot.Motion == HeroFxMotion.Travel ||
            slot.Motion == HeroFxMotion.TravelFixedFrame || slot.Motion == HeroFxMotion.LaserBeam ||
            slot.Motion == HeroFxMotion.Spiral || slot.Motion == HeroFxMotion.Lob;
        private readonly Slot[] slots=new Slot[Capacity];
        private readonly Mesh[] shapes=new Mesh[(int)HeroFxShape.Count];
        private readonly Mesh[] warningShapes=new Mesh[(int)HeroFxShape.Count];
        private Material material;
        private Material anchorMetal;
        private Material timmyGrenadeMetal;
        private Material monsterStone,monsterCrystal,monsterSlime;
        private bool ownsMaterial;
        private int cursor;
        private static readonly int Life=Shader.PropertyToID("_Life"),SurfaceStyle=Shader.PropertyToID("_SurfaceStyle");
        private static readonly int Polish=Shader.PropertyToID("_Polish");
        private static readonly int DetailQuality=Shader.PropertyToID("_DetailQuality");
        private static readonly int Tint=Shader.PropertyToID("_Tint"),BaseColor=Shader.PropertyToID("_BaseColor");
        public static void Warm()
        {if(instance==null)new GameObject("DF_HeroVfxPool").AddComponent<HeroVfxPool>();}
        // Shared instantaneous world trace. Its geometry starts at the muzzle and
        // ends at contact, even though the pool enlarges decorative cross-sections.
        public static void Trace(Vector3 from,Vector3 to,Color color,float width,float duration)
        {
            Vector3 delta=to-from;
            if(delta.sqrMagnitude<.0001f)return;
            Vector3 center=from+delta*(1.4f/1.95f);
            float crossSection=Mathf.Max(.006f,width)/.48f;
            Vector3 up=Mathf.Abs(Vector3.Dot(delta.normalized,Vector3.up))>.999f?Vector3.forward:Vector3.up;
            Emit(HeroFxShape.Bolt,center,center,new Vector3(crossSection,crossSection,delta.magnitude/1.95f),
                color,duration,HeroFxMotion.Hold,Quaternion.LookRotation(delta,up));
        }
        private void Awake()
        {
            instance=this;
            // A dedicated server has no graphics device. Publishing semantic cues happens
            // before rendering; combat must continue without constructing any meshes.
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null){material=null;enabled=false;return;}
            material=Resources.Load<Material>("CombatVfx/HeroEnergy");
            if(material==null||material.shader==null||!material.shader.isSupported||
                material.shader.name!="DimensionsFront/HeroEnergy")
            {
                // The generic Unlit fallback loses vertex alpha and defaults to opaque/back-face
                // culling, so planar spell meshes can disappear or turn into solid tiles on Android.
                // Load the authored shader directly from Resources, retaining its two-sided blend.
                Shader shader=Resources.Load<Shader>("CombatVfx/HeroEnergy");
                if(shader==null||!shader.isSupported)
                {
                    // Purchased particles have their own supported shader. A broken legacy shader
                    // must not prevent the entire NEW backend from starting or erase telegraphs.
                    if(!TryRecoverEpicSurface())
                    {
                        material=null;enabled=false;
                        Debug.LogError("DF: HeroEnergy shader unavailable on this graphics device. Rebuild with the Android runtime shader check.");
                        return;
                    }
                }
                else
                {
                    material=new Material(shader){name="DF_HeroEnergyRecovered"};ownsMaterial=true;
                    Debug.LogWarning("DF: recovered HeroEnergy material from its Resources shader.");
                }
            }
            LoadAttackAtlas();
            LoadEpicLibrary();
            for(int i=0;i<shapes.Length;i++)
            {
                HeroFxShape shape=(HeroFxShape)i;shapes[i]=BuildShape(shape);
                warningShapes[i]=shape==HeroFxShape.Orb||shape==HeroFxShape.Bolt||shape==HeroFxShape.Burst||shape==HeroFxShape.Ring||shape==HeroFxShape.Hex
                    ?BuildShape(shape,false):shapes[i];
            }
            for(int i=0;i<Capacity;i++)
            {
                var go=new GameObject("Hero effect");go.transform.SetParent(transform,false);
                var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;renderer.enabled=false;
                slots[i]=new Slot{Block=new MaterialPropertyBlock(),Transform=go.transform,Renderer=renderer,Filter=go.AddComponent<MeshFilter>()};
            }
        }
        internal static void Emit(HeroFxShape shape,Vector3 from,Vector3 to,Vector3 size,Color color,float duration,
            HeroFxMotion motion=HeroFxMotion.Expand,Quaternion rotation=default,GameObject owner=null,
            bool warning=false,Transform follow=null,Health shield=null,float delay=0,Transform endFollow=null,bool decorative=false,
            float elapsed=0,bool cancelOnOwnerDeath=false,float maxRange=0,
            HeroFxSprite sprite=HeroFxSprite.None,bool billboard=false,float roll=0,string epicKey=null,float effectRadius=0)
        {
            // Warnings are never decorative; flourishes cannot consume the last core slots.
            if(warning)decorative=false;
            if(decorative&&!AdaptiveVfxBudget.AllowDecorative)return;
            elapsed+=HeroVfxEvents.ReplayElapsed;
            Warm();if(instance==null||instance.material==null)return;
            // An unavailable atlas only omits its extra layer; never show an opaque fallback quad.
            if(sprite!=HeroFxSprite.None&&(warning||instance.attackAtlas==null&&instance.epicLibrary==null))return;
            instance.Show(shape,from,to,size,color,duration,motion,rotation,owner,warning,follow,shield,delay,endFollow,decorative,elapsed,cancelOnOwnerDeath,maxRange,sprite,billboard,roll,epicKey,effectRadius);
        }
        private void Show(HeroFxShape shape,Vector3 from,Vector3 to,Vector3 size,Color color,float duration,
            HeroFxMotion motion,Quaternion rotation,GameObject owner,bool warning,Transform follow,Health shield,float delay,Transform endFollow,bool decorative,
            float elapsed,bool cancelOnOwnerDeath,float maxRange,HeroFxSprite sprite,bool billboard,float roll,string epicKey,float effectRadius)
        {
            if(elapsed>=Mathf.Max(0,delay)+Mathf.Max(.04f,duration))return;
            int begin=warning?Capacity-WarningReserve:0;
            int end=warning?Capacity:Capacity-WarningReserve-(decorative?CoreReserve:0);
            Slot slot=null;
            for(int n=0;n<end-begin;n++)
            {
                int i=begin+(cursor+n)%(end-begin);
                if(!slots[i].Active){slot=slots[i];cursor=(i-begin+1)%(end-begin);break;}
            }
            // A core shot can reclaim an optional flourish when combat fills the pool.
            // Warning slots are excluded, and a flourish never displaces another core.
            if(slot==null&&!warning&&!decorative)
            {
                for(int i=0;i<end;i++)
                {
                    Slot candidate=slots[i];
                    if(candidate.Active&&candidate.Decorative&&
                        (slot==null||candidate.Start+candidate.Duration<slot.Start+slot.Duration))slot=candidate;
                }
                if(slot!=null)Hide(slot);
            }
            // Never steal a visible warning; optional flourishes are dropped at saturation.
            if(slot==null)return;
            slot.Shape=shape;
            slot.ExplicitEpicKey=epicKey;slot.ExplicitRadius=Mathf.Max(0,effectRadius);
            slot.AuthoredSize=size;
            slot.DeliveryId=deliveryScope;slot.Active=true;slot.Warning=warning;slot.Decorative=decorative;slot.CancelOnOwnerDeath=cancelOnOwnerDeath||owner!=null&&owner.GetComponent<HeroIdentity>()?.Hero==HeroId.Nerok;slot.Owner=owner;slot.OwnerHealth=owner!=null?owner.GetComponent<Health>():null;
            slot.Sprite=sprite;slot.Billboard=billboard;slot.Roll=roll;
            slot.Follow=follow;slot.Follows=follow!=null;slot.FollowMotion=follow!=null?follow.GetComponent<HeroVisualMotion>():null;slot.Offset=slot.Follows?from-follow.position:Vector3.zero;
            slot.Shield=shield;slot.ShieldBound=shield!=null;
            slot.ShieldGraceUntil=shield!=null&&shield.Shield<=0&&HeroVfxEvents.ReplayOrigin.HasValue?Time.time+.5f:0f;
            slot.EndFollow=endFollow;slot.TracksEnd=endFollow!=null;
            slot.EndOffset=endFollow!=null?to-endFollow.position:Vector3.zero;
            slot.EndHealth=endFollow!=null?endFollow.GetComponent<Health>():null;
            slot.EndMotion=endFollow!=null?endFollow.GetComponent<HeroVisualMotion>():null;
            slot.MaxRange=Mathf.Max(0,maxRange);
            // Scale presentation meshes only. Telegraph geometry stays at the exact gameplay radius.
            slot.From=from;slot.To=to;slot.Size=size*(sprite!=HeroFxSprite.None||warning||shape==HeroFxShape.TimmyGrenade||motion==HeroFxMotion.RadialPulse||motion==HeroFxMotion.Sustained?1f:1.5f);slot.Color=color;slot.Motion=motion;
            // A hitscan bolt must end at contact even though other presentation dimensions are enlarged.
            if(!warning&&shape==HeroFxShape.Bolt&&motion==HeroFxMotion.Hold)slot.Size.z=size.z;
            slot.SurfaceStyle=warning?0f:shape==HeroFxShape.Mist?5f:shape==HeroFxShape.Orb?1f:
                shape==HeroFxShape.Seed||shape==HeroFxShape.Leaf||shape==HeroFxShape.Petals?4f:
                shape==HeroFxShape.Shield||shape==HeroFxShape.Hex||shape==HeroFxShape.Runes?2f:3f;
            // Quaternion == compares rotation dot products, so an all-zero optional
            // argument does not even compare equal to itself. Check the sentinel components.
            slot.Rotation=rotation.x==0f&&rotation.y==0f&&rotation.z==0f&&rotation.w==0f?Quaternion.identity:rotation;
            if(motion==HeroFxMotion.AttachedJet&&follow!=null)
            {slot.Offset=follow.InverseTransformPoint(from);slot.Rotation=Quaternion.Inverse(follow.rotation)*slot.Rotation;}
            slot.Start=Time.time+Mathf.Max(0,delay)-Mathf.Max(0,elapsed);slot.Duration=Mathf.Max(.04f,duration);
            slot.Filter.sharedMesh=warning?warningShapes[(int)shape]:shapes[(int)shape];slot.Renderer.enabled=false;
            if (!warning && shape == HeroFxShape.AnchorProjectile && anchorMetal == null)
                anchorMetal = NerokAnchorMaterial.Create(false);
            Material surface=sprite!=HeroFxSprite.None?attackAtlas:material;
            if(!warning&&shape==HeroFxShape.AnchorProjectile&&anchorMetal!=null)surface=anchorMetal;
            else if(!warning&&shape==HeroFxShape.TimmyGrenade)
                surface=timmyGrenadeMetal??=RuntimeShaderLibrary.Create(Color.white,false,.55f,.48f);
            else if(!warning&&shape==HeroFxShape.MonsterRock)
                surface=monsterStone??=RuntimeShaderLibrary.Create(Color.white,false,.04f,.18f);
            else if(!warning&&shape==HeroFxShape.MonsterDiamond)
                surface=monsterCrystal??=RuntimeShaderLibrary.Create(Color.white,false,.25f,.88f);
            else if(!warning&&shape==HeroFxShape.MonsterSlime)
                surface=monsterSlime??=RuntimeShaderLibrary.Create(Color.white,false,.02f,.78f);
            slot.Renderer.sharedMaterial=surface!=null?surface:material;
            AttachEpic(slot,shape);
            Draw(slot,Mathf.Clamp01((Time.time-slot.Start)/slot.Duration));
            if(Time.time<slot.Start)slot.Renderer.enabled=false;
        }
        public static void CancelWarnings(GameObject actor)
        {
            if(instance==null)return;
            foreach(Slot slot in instance.slots)if(slot!=null&&slot.Active&&slot.Warning&&slot.Owner==actor)instance.Hide(slot);
        }
        public static void CancelProjectiles(GameObject actor)
        {
            if(instance==null)return;
            foreach(Slot slot in instance.slots)
                if(slot!=null&&slot.Active&&IsProjectile(slot)&&slot.Owner==actor)instance.Hide(slot);
        }
        internal static void CancelShape(GameObject actor,HeroFxShape shape,HeroFxMotion? motion=null)
        {
            if(instance==null)return;
            foreach(Slot slot in instance.slots)
                if(slot!=null&&slot.Active&&slot.Owner==actor&&slot.Shape==shape&&
                    (!motion.HasValue||slot.Motion==motion.Value))instance.Hide(slot);
        }
        private void Hide(Slot s){ReleaseEpic(s);s.Active=false;s.Renderer.enabled=false;s.Follow=null;s.Owner=null;s.OwnerHealth=null;s.Shield=null;s.EndFollow=null;s.EndHealth=null;s.FollowMotion=s.EndMotion=null;s.TracksEnd=false;}
        private void Update()
        {
            if(Time.timeScale<=0)return;
            for(int i=0;i<slots.Length;i++)
            {
                Slot s=slots[i];if(s==null||!s.Active)continue;
                if(s.ShieldBound&&s.Shield!=null&&s.Shield.Shield>0)s.ShieldGraceUntil=0;
                if(s.CancelOnOwnerDeath&&(s.Owner==null||!s.Owner.activeInHierarchy||s.OwnerHealth!=null&&s.OwnerHealth.IsDead)||
                    s.Warning&&(s.Owner==null||!s.Owner.activeInHierarchy||s.OwnerHealth!=null&&s.OwnerHealth.IsDead)||
                    s.TracksEnd&&(s.EndFollow==null||!s.EndFollow.gameObject.activeInHierarchy||s.EndHealth!=null&&s.EndHealth.IsDead)||
                    s.Follows&&s.Follow==null||s.ShieldBound&&(s.Shield==null||s.Shield.IsDead||s.Shield.Shield<=0&&Time.time>=s.ShieldGraceUntil))
                {Hide(s);continue;}
                float age=Time.time-s.Start;if(age<0)continue;
                if(age>=s.Duration){Hide(s);continue;}
            }
        }
        // HeroVisualMotion samples its pose in LateUpdate (order 930). Attached VFX
        // must render after that phase rather than following last frame's pose in Update.
        private void LateUpdate()
        {
            if(Time.timeScale<=0)return;
            Camera view=Camera.main;if(view!=null)billboardRotation=view.transform.rotation;
            for(int i=0;i<slots.Length;i++)
            {
                Slot s=slots[i];if(s==null||!s.Active)continue;
                float age=Time.time-s.Start;
                if(age>=0&&age<s.Duration)Draw(s,age/s.Duration);
            }
        }
        private void Draw(Slot s,float t)
        {
            Vector3 point=s.Follows?HeroPresentationAnchor.Position(s.Follow,s.FollowMotion)+s.Offset:s.From;
            float scale=1;Quaternion rotation=s.Rotation;Vector3 drawnSize=s.Size;
            switch(s.Motion)
            {
                case HeroFxMotion.AttachedJet:
                    if(s.Follow==null){s.Renderer.enabled=false;return;}
                    rotation=HeroPresentationAnchor.Rotation(s.Follow,s.FollowMotion);
                    point=HeroPresentationAnchor.TransformPoint(s.Follow,s.FollowMotion,s.Offset);rotation*=s.Rotation;
                    scale=.92f+.08f*Mathf.Sin(t*Mathf.PI*12f);
                    break;
                case HeroFxMotion.LaserBeam:
                    // The cached laser spans 0..1 on Z: keep both ends exact while tracking movement.
                    if(s.Follows&&s.Owner!=null)point=s.Owner.GetComponent<HeroVisualMotion>()?.ReleasePoint??point;
                    Vector3 beamEnd=s.TracksEnd?HeroPresentationAnchor.Position(s.EndFollow,s.EndMotion)+s.EndOffset:s.To;
                    Vector3 rangeOrigin=s.Follows&&s.Owner!=null?HeroPresentationAnchor.Position(s.Follow,s.FollowMotion):point;
                    Vector3 rangeDelta=beamEnd-rangeOrigin;rangeDelta.y=0;
                    if(s.MaxRange>0&&rangeDelta.sqrMagnitude>s.MaxRange*s.MaxRange)
                    {
                        Vector3 limited=rangeOrigin+rangeDelta.normalized*s.MaxRange;
                        beamEnd=new Vector3(limited.x,beamEnd.y,limited.z);
                    }
                    Vector3 beamDelta=beamEnd-point;
                    if(beamDelta.sqrMagnitude<.0001f){s.Renderer.enabled=false;return;}
                    Vector3 beamUp=Mathf.Abs(Vector3.Dot(beamDelta.normalized,Vector3.up))>.999f?Vector3.forward:Vector3.up;
                    rotation=Quaternion.LookRotation(beamDelta,beamUp);
                    drawnSize.z=beamDelta.magnitude;
                    break;
                case HeroFxMotion.Tether:
                    Vector3 tetherEnd=s.TracksEnd?HeroPresentationAnchor.Position(s.EndFollow,s.EndMotion)+s.EndOffset:s.To;
                    Vector3 tetherDelta=tetherEnd-point;
                    if(tetherDelta.sqrMagnitude>.0001f)rotation=Quaternion.LookRotation(tetherDelta);
                    drawnSize.z=tetherDelta.magnitude;
                    break;
                case HeroFxMotion.Expand:scale=Mathf.Lerp(.24f,1f,1f-Mathf.Pow(1f-t,3f));break;
                case HeroFxMotion.RadialPulse:
                    // Starts at the caster and visibly reaches the exact radius before fading.
                    scale=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(t/.82f));break;
                case HeroFxMotion.Contract:scale=Mathf.Lerp(1,.12f,t*t);break;
                case HeroFxMotion.TravelFixedFrame:
                case HeroFxMotion.Travel:
                    Vector3 end=s.TracksEnd?HeroPresentationAnchor.Position(s.EndFollow,s.EndMotion)+s.EndOffset:s.To;
                    point=Vector3.Lerp(s.From,end,t);
                    if(s.Motion==HeroFxMotion.Travel&&s.TracksEnd&&(end-s.From).sqrMagnitude>.0001f)rotation=Quaternion.LookRotation(end-s.From);
                    break;
                case HeroFxMotion.Drift:
                    // Sparks stretch along a fixed velocity heading instead of tumbling like debris.
                    point=Vector3.Lerp(s.From,s.To,1f-Mathf.Pow(1f-t,2f));
                    point.y+=Mathf.Sin(t*Mathf.PI)*.16f;
                    scale=Mathf.Lerp(1f,.28f,t);
                    break;
                case HeroFxMotion.Scatter:
                    // Ballistic-looking debris, entirely presentation-only and collision-free.
                    point=Vector3.Lerp(s.From,s.To,t);
                    point.y+=4f*t*(1f-t)*Mathf.Min(.42f,Vector3.Distance(s.From,s.To)*.32f);
                    rotation*=Quaternion.Euler(t*140f,t*210f,t*85f);
                    scale=Mathf.Lerp(1f,.35f,t);
                    break;
                case HeroFxMotion.Lob:
                    Vector3 lobEnd=s.TracksEnd?HeroPresentationAnchor.Position(s.EndFollow,s.EndMotion)+s.EndOffset:s.To;
                    point=Vector3.Lerp(s.From,lobEnd,t);
                    point.y+=4f*t*(1f-t)*Mathf.Min(.85f,Vector3.Distance(s.From,lobEnd)*.18f);
                    rotation*=Quaternion.Euler(t*115f,t*185f,t*65f);
                    break;
                case HeroFxMotion.Spiral:
                    Vector3 destination=s.TracksEnd?HeroPresentationAnchor.Position(s.EndFollow,s.EndMotion)+s.EndOffset:s.To;
                    Vector3 heading=destination-s.From;
                    if(heading.sqrMagnitude>.0001f)rotation=Quaternion.LookRotation(heading);
                    float angle=t*Mathf.PI*3f+s.Rotation.eulerAngles.z*Mathf.Deg2Rad;
                    float radius=Mathf.Sin(t*Mathf.PI)*Mathf.Min(.22f,s.Size.magnitude*.65f);
                    point=Vector3.Lerp(s.From,destination,t)+
                        rotation*new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius,0f);
                    break;
                case HeroFxMotion.Orbit:
                    rotation*=s.Sprite!=HeroFxSprite.None?Quaternion.AngleAxis(t*180f,Vector3.forward):Quaternion.Euler(0,t*180,0);
                    scale=1-t*.22f;break;
                case HeroFxMotion.Sweep:rotation*=Quaternion.Euler(0,Mathf.Lerp(-35,35,Mathf.SmoothStep(0f,1f,t)),0);scale=.88f+.12f*Mathf.Sin(t*Mathf.PI);break;
            }
            if(s.Sprite!=HeroFxSprite.None&&s.Billboard)
            {
                float roll=s.Roll+(s.Motion==HeroFxMotion.Orbit?t*180f:0);
                if(s.Sprite==HeroFxSprite.Flame||s.Sprite==HeroFxSprite.Trace)
                {
                    Vector3 axis=rotation*(s.Motion==HeroFxMotion.AttachedJet?Vector3.back:Vector3.forward);
                    Vector3 screenAxis=Quaternion.Inverse(billboardRotation)*axis;
                    if(screenAxis.x*screenAxis.x+screenAxis.y*screenAxis.y>.0001f)
                        roll+=Mathf.Atan2(-screenAxis.x,screenAxis.y)*Mathf.Rad2Deg;
                }
                rotation=billboardRotation*Quaternion.AngleAxis(roll,Vector3.forward);
            }
            s.Transform.SetPositionAndRotation(point,rotation);s.Transform.localScale=drawnSize*scale;
            // Projectile cores remain readable until contact. Decorative flashes die away
            // smoothly; exact warning outlines retain their original size and opacity.
            bool flight=s.Motion==HeroFxMotion.LaserBeam||s.Motion==HeroFxMotion.Tether||s.Motion==HeroFxMotion.Travel||s.Motion==HeroFxMotion.TravelFixedFrame||s.Motion==HeroFxMotion.Spiral||s.Motion==HeroFxMotion.Lob;
            float fade=s.Warning||flight?1f:1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.20f,1f,t));
            if(s.Motion==HeroFxMotion.RadialPulse)fade=1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.82f,1f,t));
            if(s.Motion==HeroFxMotion.Sustained||s.Motion==HeroFxMotion.AttachedJet)
                fade=1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.90f,1f,t));
            Color color=s.Color;
            if(!s.Warning){color.r*=1.28f;color.g*=1.28f;color.b*=1.28f;}
            color.a*=fade;
            if(s.Epic!=null&&DrawEpic(s,Mathf.Max(0,Time.time-s.Start),fade))
            {
                s.Renderer.enabled=false;return;
            }
            // The new renderers are disabled whenever their emitter has no particles or
            // supported material. Only this one mesh then supplies temporary feedback.
            s.Block.SetFloat(Life,t);s.Block.SetFloat(SurfaceStyle,s.SurfaceStyle);
            if(s.Sprite!=HeroFxSprite.None)s.Block.SetFloat(AtlasCell,AtlasFrame(s.Sprite,t));
            s.Block.SetFloat(DetailQuality,s.Warning?0f:AdaptiveVfxBudget.SurfaceDetail);
            s.Block.SetFloat(Polish,s.Warning?0f:1f);s.Block.SetColor(Tint,color);s.Block.SetColor(BaseColor,color);s.Renderer.SetPropertyBlock(s.Block);s.Renderer.enabled=true;
        }
        private void OnDestroy()
        {
            if(instance==this)instance=null;
            for(int i=0;i<shapes.Length;i++)
            {
                if(warningShapes[i]!=null&&warningShapes[i]!=shapes[i])Destroy(warningShapes[i]);
                if(shapes[i]!=null)Destroy(shapes[i]);
            }
            if(epicBeam!=null)Destroy(epicBeam);
            if(epicTrace!=null)Destroy(epicTrace);
            if(ownsMaterial&&material!=null)Destroy(material);
            if(anchorMetal!=null)Destroy(anchorMetal);
            if(timmyGrenadeMetal!=null)Destroy(timmyGrenadeMetal);
            if(monsterStone!=null)Destroy(monsterStone);
            if(monsterCrystal!=null)Destroy(monsterCrystal);
            if(monsterSlime!=null)Destroy(monsterSlime);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset(){instance=null;}

        private static Mesh BuildShape(HeroFxShape shape,bool polished=true)
        {
            if(shape==HeroFxShape.TexturedQuad)return BuildSpriteQuad();
            if(shape==HeroFxShape.MonsterRock||shape==HeroFxShape.MonsterDiamond||shape==HeroFxShape.MonsterSlime||shape==HeroFxShape.MonsterBeam)
                return MonsterEffectMeshes.Build(shape);
            if(shape==HeroFxShape.AnchorProjectile||shape==HeroFxShape.WaterSpiral||shape==HeroFxShape.WaveCrest||shape==HeroFxShape.AnchorChain)
                return CyberSharkEffectMeshes.Load(shape.ToString());
            var v=new List<Vector3>();var colors=new List<Color>();var triangles=new List<int>();
            void Tri(Vector3 a,Vector3 b,Vector3 c,float alpha=1)
            {int i=v.Count;v.Add(a);v.Add(b);v.Add(c);colors.Add(new Color(1,1,1,alpha));colors.Add(new Color(1,1,1,alpha));colors.Add(new Color(1,1,1,alpha));triangles.Add(i);triangles.Add(i+1);triangles.Add(i+2);}
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float alpha=1){Tri(a,b,c,alpha);Tri(a,c,d,alpha);}
            // Feathered geometry stores luminous cores and transparent margins in vertex colors.
            // Built once during Awake; all attacks reuse the resulting meshes.
            void SoftQuad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color edgeA,Color edgeB)
            {
                int n=v.Count;v.Add(a);v.Add(b);v.Add(c);v.Add(d);
                colors.Add(edgeA);colors.Add(edgeB);colors.Add(edgeB);colors.Add(edgeA);
                triangles.Add(n);triangles.Add(n+1);triangles.Add(n+2);
                triangles.Add(n);triangles.Add(n+2);triangles.Add(n+3);
            }
            Color clear=new Color(1,1,1,0),hot=new Color(1.4f,1.4f,1.4f,1),soft=new Color(1,1,1,.55f);
            void FeatherBand(float start,float end,float inner,float peak,float outer,int count,bool taper=false)
            {
                for(int i=0;i<count;i++)
                {
                    float u=(float)i/count,w=(float)(i+1)/count;
                    Vector3 a=new Vector3(Mathf.Sin(Mathf.Lerp(start,end,u)),0,Mathf.Cos(Mathf.Lerp(start,end,u)));
                    Vector3 b=new Vector3(Mathf.Sin(Mathf.Lerp(start,end,w)),0,Mathf.Cos(Mathf.Lerp(start,end,w)));
                    float ta=taper?Mathf.Sin(u*Mathf.PI):1,tb=taper?Mathf.Sin(w*Mathf.PI):1;
                    Vector3 ia=a*inner,ib=b*inner,pa=a*Mathf.Lerp(inner,peak,ta),pb=b*Mathf.Lerp(inner,peak,tb);
                    Vector3 oa=a*Mathf.Lerp(inner,outer,ta),ob=b*Mathf.Lerp(inner,outer,tb);
                    SoftQuad(ia,pa,pb,ib,clear,hot);SoftQuad(pa,oa,ob,pb,hot,clear);
                }
            }
            void Ray(Vector3 end,Vector3 width)
            {
                SoftQuad(-width,Vector3.zero,end,end-width*.12f,clear,hot);
                SoftQuad(Vector3.zero,width,end+width*.12f,end,hot,clear);
            }
            void Corona(Vector3 right,Vector3 up,float radius,float opacity)
            {
                if(!polished)return;
                Color core=new Color(1.45f,1.45f,1.45f,opacity);
                for(int i=0;i<24;i++)
                {
                    float a=i*Mathf.PI/12,b=(i+1)*Mathf.PI/12;
                    Vector3 x=(right*Mathf.Cos(a)+up*Mathf.Sin(a))*radius;
                    Vector3 y=(right*Mathf.Cos(b)+up*Mathf.Sin(b))*radius;
                    SoftQuad(x*.42f,x,y,y*.42f,core,clear);
                }
            }
            void Band(float start,float end,float inner,float outer,int count,float y=0)
            {
                for(int i=0;i<count;i++)
                {
                    float a=Mathf.Lerp(start,end,(float)i/count),b=Mathf.Lerp(start,end,(float)(i+1)/count);
                    Vector3 pa=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a)),pb=new Vector3(Mathf.Sin(b),0,Mathf.Cos(b));
                    float taper=shape==HeroFxShape.Slash?Mathf.Sin((i+.5f)/count*Mathf.PI):1;
                    Quad(pa*inner+Vector3.up*y,pa*Mathf.Lerp(inner,outer,taper)+Vector3.up*y,pb*Mathf.Lerp(inner,outer,taper)+Vector3.up*y,pb*inner+Vector3.up*y);
                }
            }
            switch(shape)
            {
                case HeroFxShape.TimmyGrenade:
                    // Cached closed canister with bevelled shoulders and a narrower top cap.
                    float[] heights={-.7f,-.6f,-.46f,.42f,.56f,.60f,.75f};
                    float[] radii={.30f,.46f,.50f,.50f,.42f,.24f,.24f};
                    for(int row=0;row<heights.Length-1;row++)for(int i=0;i<16;i++)
                    {
                        float a=i*Mathf.PI/8f,b=(i+1)*Mathf.PI/8f;
                        Vector3 x=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)),y=new Vector3(Mathf.Cos(b),0,Mathf.Sin(b));
                        Quad(x*radii[row]+Vector3.up*heights[row],x*radii[row+1]+Vector3.up*heights[row+1],
                            y*radii[row+1]+Vector3.up*heights[row+1],y*radii[row]+Vector3.up*heights[row]);
                    }
                    for(int i=0;i<16;i++)
                    {
                        float a=i*Mathf.PI/8f,b=(i+1)*Mathf.PI/8f;
                        Vector3 x=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)),y=new Vector3(Mathf.Cos(b),0,Mathf.Sin(b));
                        Tri(Vector3.up*heights[0],x*radii[0]+Vector3.up*heights[0],y*radii[0]+Vector3.up*heights[0]);
                        int last=heights.Length-1;
                        Tri(Vector3.up*heights[last],y*radii[last]+Vector3.up*heights[last],x*radii[last]+Vector3.up*heights[last]);
                    }
                    break;
                case HeroFxShape.Mist:
                    // A cached, soft cloud surface; outer vertices stay inside radius one.
                    for(int ring=0;ring<4;ring++)for(int i=0;i<48;i++)
                    {
                        float a=i*Mathf.PI/24f,b=(i+1)*Mathf.PI/24f;
                        Vector3 x=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)),y=new Vector3(Mathf.Cos(b),0,Mathf.Sin(b));
                        float inner=ring*.25f,outer=(ring+1)*.25f;
                        float alphaInner=ring==0?.30f:ring==1?.75f:ring==2?.65f:.28f;
                        float alphaOuter=ring==0?.75f:ring==1?.65f:ring==2?.28f:0;
                        SoftQuad(x*inner,x*outer,y*outer,y*inner,new Color(1,1,1,alphaInner),new Color(1,1,1,alphaOuter));
                    }
                    break;
                case HeroFxShape.Claw:
                    // Curved blade with a fine luminous spine and a pointed tip, along +Z.
                    for(int i=0;i<24;i++)
                    {
                        float t=i/24f,u=(i+1)/24f;
                        Vector3 a=new Vector3(.26f*(1-t)*(1-t),0,Mathf.Lerp(-.8f,.8f,t));
                        Vector3 b=new Vector3(.26f*(1-u)*(1-u),0,Mathf.Lerp(-.8f,.8f,u));
                        Vector3 wa=Vector3.right*(.085f*Mathf.Sin(t*Mathf.PI)),wb=Vector3.right*(.085f*Mathf.Sin(u*Mathf.PI));
                        SoftQuad(a-wa,a,b,b-wb,clear,hot);SoftQuad(a,a+wa,b+wb,b,hot,clear);
                    }
                    break;
                case HeroFxShape.Streak:
                    // Crossed feathered ribbons taper to the actual projectile head at local Z=0.
                    foreach(Vector3 side in new[]{Vector3.right*.10f,Vector3.up*.10f})
                    {
                        Vector3 tail=Vector3.back*1.4f,shoulder=Vector3.back*.18f;
                        SoftQuad(tail,shoulder-side,shoulder,Vector3.zero,clear,hot);
                        SoftQuad(tail,shoulder,shoulder+side,Vector3.zero,hot,clear);
                    }
                    break;
                case HeroFxShape.ArcRibbon:
                    FeatherBand(-1.42f,1.42f,.70f,.84f,.92f,40,true);
                    FeatherBand(-1.20f,1.28f,.96f,.99f,1.025f,32,true);
                    break;
                case HeroFxShape.EchoSickle:
                    // Broad crescent blade in the ground-facing XZ plane, pointed at both ends.
                    // Vertex-alpha feathering keeps the violet body translucent and its rim luminous.
                    FeatherBand(-1.65f,1.65f,.48f,.78f,1f,48,true);
                    break;
                case HeroFxShape.Leaf:
                    // An asymmetric luminous leaf, with a fine central vein and soft contour.
                    SoftQuad(new Vector3(0,-.55f,0),new Vector3(-.22f,.05f,0),new Vector3(0,.10f,.035f),new Vector3(.055f,.50f,0),clear,hot);
                    SoftQuad(new Vector3(0,-.55f,0),new Vector3(0,.10f,.035f),new Vector3(.22f,.09f,0),new Vector3(.055f,.50f,0),hot,clear);
                    break;
                case HeroFxShape.ContactStar:
                    // Camera-facing sharp star with feathered ray borders; twelve triangles pairs reused.
                    for(int i=0;i<8;i++)
                    {
                        float a=i*Mathf.PI/4f;
                        Vector3 d=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0);
                        Vector3 side=new Vector3(-d.y,d.x,0)*.08f;
                        Vector3 tip=d*(i%2==0?1f:.56f);
                        SoftQuad(side,Vector3.zero,tip,tip+side*.08f,clear,hot);
                        SoftQuad(Vector3.zero,-side,tip-side*.08f,tip,hot,clear);
                    }
                    break;
                case HeroFxShape.Spark:
                    // Two crossed planes stay visible from oblique gameplay views.
                    foreach(Vector3 side in new[]{Vector3.right*.065f,Vector3.up*.065f})
                    {
                        Vector3 tail=Vector3.back*.48f,tip=Vector3.forward*.48f;
                        SoftQuad(tail-side,tail,tip,tip-side*.10f,clear,hot);
                        SoftQuad(tail,tail+side,tip+side*.10f,tip,hot,clear);
                    }
                    break;
                case HeroFxShape.Ring:
                    // Circumscribed polygon: the outer edge never falls inside a radius-1 circle.
                    if(polished)FeatherBand(0,Mathf.PI*2,.90f,.965f,1.025f,64);
                    else Band(0,Mathf.PI*2,.92f,1f/Mathf.Cos(Mathf.PI/48),48);break;
                case HeroFxShape.AnchorStrikeZero:
                    // Tall hollow zero in the local XY plane, with a bright rim and soft edges.
                    for(int i=0;i<48;i++)
                    {
                        float a=i*Mathf.PI/24f,b=(i+1)*Mathf.PI/24f;
                        Vector3 x=new Vector3(Mathf.Cos(a)*.28f,Mathf.Sin(a)*.44f,0f);
                        Vector3 y=new Vector3(Mathf.Cos(b)*.28f,Mathf.Sin(b)*.44f,0f);
                        SoftQuad(x*.76f,x*.90f,y*.90f,y*.76f,clear,hot);
                        SoftQuad(x*.90f,x,y,y*.90f,hot,clear);
                    }
                    break;
                case HeroFxShape.GravityEight:
                    // Upright numeral eight: two hollow, feathered loops in the local XY plane.
                    for(int lobe=-1;lobe<=1;lobe+=2)
                    {
                        Vector3 center=Vector3.up*(lobe*.24f);
                        for(int i=0;i<32;i++)
                        {
                            float a=i*Mathf.PI/16f,b=(i+1)*Mathf.PI/16f;
                            Vector3 x=new Vector3(Mathf.Cos(a)*.22f,Mathf.Sin(a)*.26f,0f);
                            Vector3 y=new Vector3(Mathf.Cos(b)*.22f,Mathf.Sin(b)*.26f,0f);
                            SoftQuad(center+x*.76f,center+x*.90f,center+y*.90f,center+y*.76f,clear,hot);
                            SoftQuad(center+x*.90f,center+x,center+y,center+y*.90f,hot,clear);
                        }
                    }
                    break;
                case HeroFxShape.PressureWave:
                    // Hollow feathered band, with no vertices beyond the requested radius.
                    FeatherBand(0,Mathf.PI*2,.86f,.95f,1f,64);break;
                case HeroFxShape.Hex:
                    if(polished)FeatherBand(0,Mathf.PI*2,.86f,.94f,1.02f,6);
                    else Band(0,Mathf.PI*2,.89f,1,6);break;
                case HeroFxShape.Slash:FeatherBand(-1.55f,1.55f,.48f,.87f,1,32,true);break;
                case HeroFxShape.RiftCross:
                    // Two luminous diagonal blades form a literal X in the local XY plane.
                    // Feathered edges retain the silhouette without a solid square background.
                    for(int sign=-1;sign<=1;sign+=2)
                    {
                        Vector3 a=new Vector3(-.68f,-.68f*sign,0);
                        Vector3 b=new Vector3(.68f,.68f*sign,0);
                        Vector3 side=new Vector3(-sign,1,0).normalized*.085f;
                        SoftQuad(a-side*2,a,b,b-side*2,clear,hot);
                        SoftQuad(a,a+side*2,b+side*2,b,hot,clear);
                    }
                    break;
                case HeroFxShape.Rift:
                    for(int i=0;i<5;i++)
                    {
                        float y=-.8f+i*.32f,x=i%2==0?-.06f:.07f;
                        if(polished)
                        {
                            Vector3 a=new Vector3(x,y,0),b=new Vector3(-x+.02f,y+.33f,0);
                            SoftQuad(a-Vector3.right*.14f,a,b,b-Vector3.right*.14f,clear,hot);
                            SoftQuad(a,a+Vector3.right*.14f,b+Vector3.right*.14f,b,hot,clear);
                        }
                        else Quad(new Vector3(x-.08f,y,0),new Vector3(x+.055f,y,0),new Vector3(-x+.09f,y+.33f,0),new Vector3(-x-.05f,y+.33f,0));
                    }break;
                case HeroFxShape.Laser:
                    // Straight, uniform-width light in two planes, from muzzle (Z=0) to endpoint (Z=1).
                    SoftQuad(new Vector3(-.5f,0,0),Vector3.zero,Vector3.forward,new Vector3(-.5f,0,1),clear,hot);
                    SoftQuad(Vector3.zero,new Vector3(.5f,0,0),new Vector3(.5f,0,1),Vector3.forward,hot,clear);
                    SoftQuad(new Vector3(0,-.5f,0),Vector3.zero,Vector3.forward,new Vector3(0,-.5f,1),clear,hot);
                    SoftQuad(Vector3.zero,new Vector3(0,.5f,0),new Vector3(0,.5f,1),Vector3.forward,hot,clear);
                    break;
                case HeroFxShape.Bolt:
                    // Crossed tapered luminous streaks remain visible from the gameplay camera.
                    SoftQuad(new Vector3(-.16f,0,-1.4f),new Vector3(0,0,-1.4f),new Vector3(0,0,.55f),new Vector3(-.025f,0,.55f),clear,hot);
                    SoftQuad(new Vector3(0,0,-1.4f),new Vector3(.16f,0,-1.4f),new Vector3(.025f,0,.55f),new Vector3(0,0,.55f),hot,clear);
                    SoftQuad(new Vector3(0,-.12f,-1.4f),new Vector3(0,0,-1.4f),new Vector3(0,0,.55f),new Vector3(0,-.02f,.55f),clear,hot);
                    SoftQuad(new Vector3(0,0,-1.4f),new Vector3(0,.12f,-1.4f),new Vector3(0,.02f,.55f),new Vector3(0,0,.55f),hot,clear);
                    Corona(Vector3.right,Vector3.up,.23f,.52f);break;
                case HeroFxShape.Seed:
                    // Pointed leaf in two planes: offensive seed, deliberately not a healing flower.
                    SoftQuad(new Vector3(0,0,-.60f),new Vector3(-.22f,0,-.08f),new Vector3(0,0,.62f),new Vector3(.22f,0,-.08f),soft,hot);
                    SoftQuad(new Vector3(0,0,-.60f),new Vector3(0,-.13f,-.08f),new Vector3(0,0,.62f),new Vector3(0,.13f,-.08f),soft,hot);
                    break;
                case HeroFxShape.Halo:
                    for(int i=0;i<32;i++)
                    {
                        float a=i*Mathf.PI/16,b=(i+1)*Mathf.PI/16;
                        SoftQuad(Vector3.zero,new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)),new Vector3(Mathf.Cos(b),0,Mathf.Sin(b)),Vector3.zero,hot,clear);
                    }break;
                case HeroFxShape.Burst:
                    for(int i=0;i<9;i++)
                    {
                        float a=i*Mathf.PI*2/9;Vector3 d=new Vector3(Mathf.Cos(a),.12f,Mathf.Sin(a));
                        Ray(d*(i%2==0?1:.65f),Vector3.Cross(Vector3.up,d)*.09f);
                    }
                    Corona(Vector3.right,Vector3.forward,.40f,.65f);break;
                case HeroFxShape.Petals:
                    for(int i=0;i<6;i++)
                    {
                        float a=i*Mathf.PI/3;Vector3 d=new Vector3(Mathf.Cos(a),.18f,Mathf.Sin(a)),side=Vector3.Cross(Vector3.up,d)*.22f;
                        SoftQuad(d*.18f,d*.60f+side,d,d*.60f-side,clear,soft);
                        Ray(d*.90f,side*.17f);
                    }break;
                case HeroFxShape.ShockDome:
                    for(int j=0;j<6;j++)for(int i=0;i<32;i++)
                    {
                        float y0=j/6f,y1=(j+1)/6f,r0=Mathf.Sqrt(1-y0*y0),r1=Mathf.Sqrt(1-y1*y1);
                        float a=i*Mathf.PI/16,b=(i+1)*Mathf.PI/16;
                        Vector3 d=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)),e=new Vector3(Mathf.Cos(b),0,Mathf.Sin(b));
                        SoftQuad(d*r0+Vector3.up*y0,d*r1+Vector3.up*y1,e*r1+Vector3.up*y1,e*r0+Vector3.up*y0,
                            new Color(1,1,1,.28f*(1-y0)),new Color(1,1,1,.28f*(1-y1)));
                    }
                    FeatherBand(0,Mathf.PI*2,.80f,.95f,1,40);break;
                case HeroFxShape.Runes:
                    for(int i=0;i<8;i++)
                    {
                        float a=i*Mathf.PI/4;Band(a+.06f,a+.44f,.80f,.86f,4);
                        Vector3 d=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a)),side=Vector3.Cross(Vector3.up,d);
                        Quad(d*.60f-side*.025f,d*.78f-side*.025f,d*.78f+side*.025f,d*.60f+side*.025f,.7f);
                    }break;
                case HeroFxShape.Orb:
                    // Rounded volume with a baked luminous upper hemisphere, no texture or dynamic light.
                    for(int j=0;j<12;j++)for(int i=0;i<24;i++)
                    {
                        float a=i*Mathf.PI/12,b=(i+1)*Mathf.PI/12;
                        float lat0=-Mathf.PI*.5f+j*Mathf.PI/12,lat1=lat0+Mathf.PI/12;
                        Vector3 d=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)),e=new Vector3(Mathf.Cos(b),0,Mathf.Sin(b));
                        float light0=.45f+1.05f*(Mathf.Sin(lat0)*.5f+.5f),light1=.45f+1.05f*(Mathf.Sin(lat1)*.5f+.5f);
                        SoftQuad((d*Mathf.Cos(lat0)+Vector3.up*Mathf.Sin(lat0))*.5f,
                            (d*Mathf.Cos(lat1)+Vector3.up*Mathf.Sin(lat1))*.5f,
                            (e*Mathf.Cos(lat1)+Vector3.up*Mathf.Sin(lat1))*.5f,
                            (e*Mathf.Cos(lat0)+Vector3.up*Mathf.Sin(lat0))*.5f,
                            new Color(light0,light0,light0,1),new Color(light1,light1,light1,1));
                    }
                    Corona(Vector3.right,Vector3.up,.66f,.32f);
                    Corona(Vector3.right,Vector3.forward,.66f,.32f);
                    Corona(Vector3.forward,Vector3.up,.66f,.32f);break;
                case HeroFxShape.Shard:
                    for(int i=0;i<8;i++)
                    {
                        float a=i*Mathf.PI/4,b=(i+1)*Mathf.PI/4;
                        Vector3 p=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*.5f,q=new Vector3(Mathf.Cos(b),0,Mathf.Sin(b))*.5f;
                        Tri(p,Vector3.up*(shape==HeroFxShape.Shard?1:.6f),q);Tri(q,Vector3.down*.6f,p);
                    }break;
                case HeroFxShape.Shield:
                    for(int i=0;i<6;i++)
                    {
                        float a=i*Mathf.PI/3+.08f,b=(i+1)*Mathf.PI/3-.08f;
                        Vector3 p=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)),q=new Vector3(Mathf.Cos(b),0,Mathf.Sin(b));
                        Quad(p*.84f,q*.84f,q*.72f+Vector3.up*1.2f,p*.72f+Vector3.up*1.2f,.16f);
                        Quad(p*.84f,p*.88f,p*.76f+Vector3.up*1.2f,p*.72f+Vector3.up*1.2f,.9f);
                    }
                    Band(0,Mathf.PI*2,.79f,.84f,6);Band(0,Mathf.PI*2,.67f,.72f,6,1.2f);break;
                case HeroFxShape.Plus:
                    Quad(new Vector3(-.15f,0,-.5f),new Vector3(.15f,0,-.5f),new Vector3(.15f,0,.5f),new Vector3(-.15f,0,.5f));
                    Quad(new Vector3(-.5f,0,-.15f),new Vector3(.5f,0,-.15f),new Vector3(.5f,0,.15f),new Vector3(-.5f,0,.15f));break;
                case HeroFxShape.Cracks:
                    for(int i=0;i<7;i++)
                    {
                        float a=i*Mathf.PI*2/7;Vector3 d=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)),s=Vector3.Cross(Vector3.up,d);
                        Tri(d*.16f-s*.04f,d*.85f+s*.13f,d*.35f+s*.035f);
                        Tri(d*.35f+s*.035f,d*.85f+s*.13f,d*1.1f+s*.21f);
                    }break;
            }
            Mesh mesh=new Mesh{name="DF_AuthoredFx_"+shape};mesh.SetVertices(v);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }
}


