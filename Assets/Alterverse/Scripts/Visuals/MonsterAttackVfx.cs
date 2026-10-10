using Alterverse.Combat;
using Alterverse.Audio;
using Alterverse.Match;
using UnityEngine;

namespace Alterverse.Visuals
{
    public enum MonsterAttackStyle { Mech,Skeleton,Stone,Diamond,Alien,Poison,Frost,Scorpion,Tentacle,Orb,Iron,Bio }

    // Presentation only. Health, attack range, cooldowns and hit validation stay with combat.
    public static class MonsterAttackVfx
    {
        public static event System.Action<GameObject,Vector3,Vector3> Resolved;
        public static event System.Action<GameObject,Health,float> LaserChanged;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset(){Resolved=null;LaserChanged=null;}

        public static MonsterAttackStyle StyleFor(GameObject actor)
        {
            if(actor!=null&&actor.GetComponentInChildren<MechMachineGunVisual>()!=null)return MonsterAttackStyle.Mech;
            bool guard=actor!=null&&actor.GetComponent<HealingGuardMech>()!=null;
            if(guard)
            {
                if(ArenaVariantRuntime.IsMars||ArenaVariantRuntime.IsGJ504b)return MonsterAttackStyle.Stone;
                if(ArenaVariantRuntime.IsDiamondCore)return MonsterAttackStyle.Diamond;
                if(ArenaVariantRuntime.IsGreenPlanet)return MonsterAttackStyle.Alien;
                return MonsterAttackStyle.Mech;
            }
            return ArenaVariantRuntime.Current switch {
                ArenaVariant.Mars=>MonsterAttackStyle.Skeleton,
                ArenaVariant.GreenPlanet=>MonsterAttackStyle.Alien,
                ArenaVariant.Noxara=>MonsterAttackStyle.Poison,
                ArenaVariant.Kryon=>MonsterAttackStyle.Frost,
                ArenaVariant.Pyraxis=>MonsterAttackStyle.Scorpion,
                ArenaVariant.Neptune=>MonsterAttackStyle.Tentacle,
                ArenaVariant.Dark00=>MonsterAttackStyle.Orb,
                ArenaVariant.DiamondCore=>MonsterAttackStyle.Diamond,
                ArenaVariant.GJ504b=>MonsterAttackStyle.Stone,
                ArenaVariant.Wasp76b=>MonsterAttackStyle.Iron,
                ArenaVariant.Trappist1e=>MonsterAttackStyle.Bio,
                _=>MonsterAttackStyle.Mech };
        }
        static Color Tint(GameObject actor,MonsterAttackStyle style)
        {
            switch(style)
            {
                case MonsterAttackStyle.Skeleton:return new Color(.75f,.48f,1f);
                case MonsterAttackStyle.Stone:return ArenaVariantRuntime.IsMars?new Color(.62f,.33f,.17f):new Color(.67f,.27f,.46f);
                case MonsterAttackStyle.Diamond:return new Color(.40f,.84f,1f);
                case MonsterAttackStyle.Alien:return new Color(.47f,.86f,.16f);
                case MonsterAttackStyle.Poison:return new Color(.30f,.94f,.08f);
                case MonsterAttackStyle.Frost:return new Color(.62f,.90f,1f);
                case MonsterAttackStyle.Scorpion:return new Color(1f,.36f,.06f);
                case MonsterAttackStyle.Tentacle:return new Color(.12f,.65f,1f);
                case MonsterAttackStyle.Orb:return new Color(1f,.05f,.025f);
                case MonsterAttackStyle.Iron:return new Color(1f,.56f,.20f);
                case MonsterAttackStyle.Bio:return new Color(.15f,.91f,.71f);
                default:
                    if(ArenaVariantRuntime.IsExoplanet)return ExoplanetArenaProfile.Energy(ArenaVariantRuntime.Current);
                    if(ArenaVariantRuntime.IsNeptune)return new Color(.12f,.65f,1f);
                    var team=actor!=null?actor.GetComponent<TeamMember>():null;
                    return team!=null&&team.Team==1?StylizedArtDirection.LumenEnergy:StylizedArtDirection.ZeroEnergy;
            }
        }
        static bool Melee(MonsterAttackStyle style)=>style==MonsterAttackStyle.Skeleton||style==MonsterAttackStyle.Frost||
            style==MonsterAttackStyle.Scorpion||style==MonsterAttackStyle.Tentacle||style==MonsterAttackStyle.Iron;

        public static void Charge(GameObject actor,Vector3 muzzle,Transform socket,float contact,float elapsed=0)
        {
            MonsterAttackStyle style=StyleFor(actor);if(Melee(style)||style==MonsterAttackStyle.Mech)return;
            HeroFxShape shape=style==MonsterAttackStyle.Stone?HeroFxShape.MonsterRock:
                style==MonsterAttackStyle.Diamond?HeroFxShape.MonsterDiamond:
                style==MonsterAttackStyle.Alien||style==MonsterAttackStyle.Bio?HeroFxShape.MonsterSlime:HeroFxShape.Orb;
            HeroVfxPool.Emit(shape,muzzle,muzzle,Vector3.one*.22f,Tint(actor,style),contact*.32f,
                HeroFxMotion.Expand,owner:actor,follow:socket,decorative:true,elapsed:elapsed,cancelOnOwnerDeath:true);
        }
        public static void Launch(GameObject actor,Vector3 from,Vector3 to,float duration,float elapsed=0,Transform target=null)
        {
            MonsterAttackStyle style=StyleFor(actor);Color color=Tint(actor,style);
            Vector3 delta=to-from;Quaternion heading=delta.sqrMagnitude>.0001f?Quaternion.LookRotation(delta):actor.transform.rotation;
            if(style==MonsterAttackStyle.Mech){MachineGun(actor,from,to,duration,elapsed,target);return;}
            if(Melee(style))
            {
                Vector3 center=Vector3.Lerp(from,to,.38f);
                float reach=Mathf.Clamp(delta.magnitude*.52f,.8f,2.1f);
                Quaternion flat=Quaternion.Euler(0,heading.eulerAngles.y,0);
                int cuts=style==MonsterAttackStyle.Frost?3:style==MonsterAttackStyle.Tentacle||style==MonsterAttackStyle.Scorpion?2:1;
                for(int i=0;i<cuts;i++)
                {
                    Vector3 offset=actor.transform.right*(i-(cuts-1)*.5f)*.16f;
                    HeroFxShape shape=style==MonsterAttackStyle.Tentacle?HeroFxShape.ArcRibbon:HeroFxShape.Slash;
                    HeroVfxPool.Emit(shape,center+offset,center+offset,new Vector3(reach,.25f,reach),color,duration,
                        HeroFxMotion.Sweep,flat*Quaternion.Euler(0,(i-(cuts-1)*.5f)*16f,style==MonsterAttackStyle.Skeleton?18f:0),
                        actor,elapsed:elapsed,cancelOnOwnerDeath:true);
                }
                return;
            }
            HeroFxShape core;Vector3 size;HeroFxMotion motion=HeroFxMotion.Travel;
            switch(style)
            {
                case MonsterAttackStyle.Stone:core=HeroFxShape.MonsterRock;size=new Vector3(.70f,.64f,.68f);motion=HeroFxMotion.Lob;break;
                case MonsterAttackStyle.Diamond:core=HeroFxShape.MonsterDiamond;size=new Vector3(.36f,.36f,.56f);motion=HeroFxMotion.Lob;break;
                case MonsterAttackStyle.Alien:core=HeroFxShape.MonsterSlime;size=new Vector3(.52f,.43f,.65f);break;
                case MonsterAttackStyle.Bio:core=HeroFxShape.MonsterSlime;size=new Vector3(.42f,.50f,.57f);break;
                case MonsterAttackStyle.Poison:core=HeroFxShape.MonsterDiamond;size=new Vector3(.17f,.17f,.55f);break;
                default:core=HeroFxShape.Orb;size=Vector3.one*(style==MonsterAttackStyle.Orb?.40f:.29f);break;
            }
            HeroVfxPool.Emit(core,from,to,size,color,duration,motion,heading,actor,endFollow:target,elapsed:elapsed,cancelOnOwnerDeath:true);
            if(style==MonsterAttackStyle.Orb||style==MonsterAttackStyle.Poison)
                HeroVfxPool.Emit(HeroFxShape.Streak,from,to,new Vector3(.24f,.24f,.45f),color,duration,
                    HeroFxMotion.Travel,heading,actor,endFollow:target,decorative:true,elapsed:elapsed,cancelOnOwnerDeath:true);
            else if(style==MonsterAttackStyle.Diamond)
                for(int i=-1;i<=1;i+=2)
                    HeroVfxPool.Emit(core,from+actor.transform.right*i*.22f,to+actor.transform.right*i*.12f,size*.48f,color,duration,
                        motion,heading,actor,endFollow:target,decorative:true,elapsed:elapsed,cancelOnOwnerDeath:true);
            else if(style==MonsterAttackStyle.Alien||style==MonsterAttackStyle.Bio)
                HeroVfxPool.Emit(core,from-delta.normalized*.25f,to,new Vector3(.20f,.20f,.33f),Color.Lerp(color,Color.white,.22f),duration,
                    HeroFxMotion.Travel,heading,actor,endFollow:target,decorative:true,elapsed:elapsed,cancelOnOwnerDeath:true);
        }
        static void MachineGun(GameObject actor,Vector3 from,Vector3 to,float duration,float elapsed,Transform target)
        {
            var gun=actor.GetComponentInChildren<MechMachineGunVisual>();
            Transform muzzle=gun!=null?gun.Muzzle:null;
            Vector3 delta=to-from;Quaternion heading=delta.sqrMagnitude>.0001f?Quaternion.LookRotation(delta):actor.transform.rotation;
            Color tracer=new Color(1f,.76f,.28f),flash=new Color(1f,.88f,.48f);
            // One recorded three-shot burst uses one existing pooled audio voice.
            gun?.MarkBurstPresented();
            DimensionAudio.PlayAt(DimensionSound.MechGun,from,.5f,elapsed);
            for(int i=0;i<3;i++)
            {
                float delay=duration*i*.33f,flight=Mathf.Max(.04f,duration-delay);
                HeroVfxPool.Emit(HeroFxShape.Streak,from,to,new Vector3(.13f,.13f,.40f),tracer,flight,
                    HeroFxMotion.Travel,heading,actor,delay:delay,endFollow:target,decorative:i>0,elapsed:elapsed,cancelOnOwnerDeath:true);
                HeroVfxPool.Emit(HeroFxShape.ContactStar,from,from,new Vector3(.20f,.20f,.28f),flash,.045f,
                    HeroFxMotion.Expand,heading,actor,follow:muzzle,delay:delay,decorative:true,elapsed:elapsed,cancelOnOwnerDeath:true);
                if(gun!=null&&gun.EjectionPort!=null)
                {
                    Vector3 port=gun.EjectionPort.position;
                    HeroVfxPool.Emit(HeroFxShape.Shard,port,port+actor.transform.right*.52f,new Vector3(.06f,.12f,.06f),new Color(.73f,.53f,.22f),.25f,
                        HeroFxMotion.Scatter,actor.transform.rotation,actor,delay:delay,decorative:true,elapsed:elapsed,cancelOnOwnerDeath:true);
                }
            }
        }
        public static void Resolve(GameObject actor,Vector3 from,Vector3 to)
        {
            PresentImpact(actor,from,to);
            Resolved?.Invoke(actor,from,to);
        }
        public static void PresentImpact(GameObject actor,Vector3 from,Vector3 to,float elapsed=0)
        {
            if(actor==null)return;
            bool guard=actor.GetComponent<HealingGuardMech>()!=null;
            // Base guardians retain their existing immediate damage timing; only the cosmetic shot travels.
            float delay=guard?.16f:0;
            if(guard)Launch(actor,from,to,delay,elapsed);
            MonsterAttackStyle style=StyleFor(actor);Color color=Tint(actor,style);
            if(!guard&&style==MonsterAttackStyle.Mech)
            {
                var gun=actor.GetComponentInChildren<MechMachineGunVisual>();
                // If latency or a slow frame skipped the flight, seek the burst at confirmed contact.
                if(gun!=null&&!gun.BurstPresented)
                {
                    gun.MarkBurstPresented();
                    DimensionAudio.PlayAt(DimensionSound.MechGun,from,.5f,elapsed+gun.FlightDuration);
                }
            }
            HeroVfxPool.Emit(HeroFxShape.Burst,to,to,new Vector3(.48f,.20f,.48f),Color.Lerp(color,Color.white,.35f),.22f,
                owner:actor,delay:delay,elapsed:elapsed);
            if(style==MonsterAttackStyle.Orb||style==MonsterAttackStyle.Mech)
                HeroVfxPool.Emit(style==MonsterAttackStyle.Orb?HeroFxShape.Ring:HeroFxShape.ContactStar,to,to,Vector3.one*.36f,color,.24f,
                    rotation:Quaternion.Euler(65,0,0),owner:actor,delay:delay,decorative:true,elapsed:elapsed);
            int pieces=style==MonsterAttackStyle.Frost?6:4;
            for(int i=0;i<pieces;i++)
            {
                float a=i*Mathf.PI*2/pieces;Vector3 outwards=new Vector3(Mathf.Cos(a),.22f,Mathf.Sin(a));
                HeroFxShape fragment=style==MonsterAttackStyle.Stone?HeroFxShape.MonsterRock:
                    style==MonsterAttackStyle.Diamond||style==MonsterAttackStyle.Frost?HeroFxShape.MonsterDiamond:
                    style==MonsterAttackStyle.Alien||style==MonsterAttackStyle.Bio||style==MonsterAttackStyle.Poison?HeroFxShape.MonsterSlime:HeroFxShape.Spark;
                Vector3 size=fragment==HeroFxShape.MonsterSlime?new Vector3(.19f,.08f,.19f):Vector3.one*.16f;
                HeroVfxPool.Emit(fragment,to,to+outwards*.65f,size,color,.30f,HeroFxMotion.Scatter,
                    Quaternion.LookRotation(outwards),actor,delay:delay,decorative:true,elapsed:elapsed);
            }
        }
        public static void PublishLaser(GameObject actor,Health target,float duration)=>LaserChanged?.Invoke(actor,target,duration);
        public static void PresentLaser(GameObject actor,Transform target,float duration,float elapsed)
        {
            if(actor==null)return;
            HeroVfxPool.CancelShape(actor,HeroFxShape.MonsterBeam);
            if(duration<=0||target==null||elapsed>=duration)return;
            Transform eye=null;
            foreach(var t in actor.GetComponentsInChildren<Transform>(true))if(t.name=="RedEye"){eye=t;break;}
            Vector3 from=eye!=null?eye.position:actor.transform.position+Vector3.up*.70f+actor.transform.forward*1.55f;
            Vector3 to=target.position+Vector3.up*.65f;
            HeroVfxPool.Emit(HeroFxShape.MonsterBeam,from,to,new Vector3(.085f,.085f,1),new Color(1f,.03f,.01f),duration,
                HeroFxMotion.Tether,owner:actor,follow:eye,endFollow:target,elapsed:elapsed,cancelOnOwnerDeath:true);
        }
    }
}
