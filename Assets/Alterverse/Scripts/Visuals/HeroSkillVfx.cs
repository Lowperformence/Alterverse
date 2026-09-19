using Alterverse.Characters;
using Alterverse.Combat;
using UnityEngine;

namespace Alterverse.Visuals
{
    // Semantic effects facade shared by players, bots, support items and founder presentation.
    public sealed class HeroSkillVfx:MonoBehaviour
    {
        private static readonly Color DarkCore=new Color(.018f,.009f,.035f,.96f);
        private static HeroId Hero(GameObject actor)=>actor!=null&&actor.TryGetComponent(out HeroIdentity id)?id.Hero:HeroId.Veyr;
        private static Color Accent(GameObject actor)=>StylizedArtDirection.HeroAccent(Hero(actor));
        private static Vector3 Chest(GameObject actor)=>actor.transform.position+Vector3.up*.25f;
        private static Vector3 Ground(Vector3 p)=>new Vector3(p.x,.20f,p.z);
        private static Quaternion Face(Vector3 from,Vector3 to)
        {Vector3 d=to-from;d.y=0;return d.sqrMagnitude>.0001f?Quaternion.LookRotation(d):Quaternion.identity;}
        private static void Fx(HeroFxShape shape,Vector3 p,Color color,float size,float life,HeroFxMotion motion=HeroFxMotion.Expand,
            Quaternion rotation=default,GameObject actor=null,Transform follow=null,bool warning=false,float delay=0)
        {HeroVfxPool.Emit(shape,p,p,Vector3.one*size,color,life,motion,rotation,actor,warning,follow,null,delay);}
        private static void Ring(GameObject actor,Vector3 p,float radius,float life,bool inward=false,bool hex=false,bool warning=false,Transform follow=null)
        {Fx(hex?HeroFxShape.Hex:HeroFxShape.Ring,p,Accent(actor),radius,life,warning?HeroFxMotion.Hold:inward?HeroFxMotion.Contract:HeroFxMotion.Expand,actor:actor,follow:follow,warning:warning);}
        private static void Shards(GameObject actor,Vector3 p,float size,int count=3)
        {
            Color c=Accent(actor);
            for(int i=0;i<count;i++)
            {
                float a=(i*.618034f+Time.frameCount*.013f)*Mathf.PI*2;Vector3 d=new Vector3(Mathf.Cos(a),.3f,Mathf.Sin(a));
                HeroVfxPool.Emit(HeroFxShape.Shard,p,p+d*size,new Vector3(.07f,.14f,.07f)*size,c,.20f,HeroFxMotion.Travel,Quaternion.Euler(24,a*Mathf.Rad2Deg,45));
            }
        }
        private static Vector3 Muzzle(GameObject actor)
        {
            HeroVisualMotion motion=actor.GetComponent<HeroVisualMotion>();
            return motion!=null?motion.ReleasePoint:Chest(actor)+actor.transform.forward*.5f;
        }
        private static void Projectile(GameObject actor,Vector3 end,int slot,float travel=0)
        {
            HeroId h=Hero(actor);Color c=Accent(actor);Vector3 start=Muzzle(actor);end+=Vector3.up*.25f;
            float strength=slot<0?1:slot==3?1.9f:1.5f;
            float flight=travel>0?travel:.065f;
            HeroFxShape shape=h==HeroId.Nyra?HeroFxShape.Bolt:HeroFxShape.Orb;
            Vector3 size=h==HeroId.Nyra?new Vector3(.48f,.48f,.66f)*strength:Vector3.one*.26f*strength;
            HeroVfxPool.Emit(shape,start,end,size,c,flight,HeroFxMotion.Travel,Face(start,end),actor);
            // Short physical muzzle core, rather than a full-distance beam.
            Fx(HeroFxShape.Orb,start,Color.Lerp(c,Color.white,.65f),.20f*strength,.07f,actor:actor);
            if(h==HeroId.Elyra)Ring(actor,start,.28f*strength,.15f);
            if(h==HeroId.Mira)Fx(HeroFxShape.Shard,start,c,.22f,.12f,rotation:Quaternion.Euler(35,0,25));
        }
        private static void Slash(GameObject actor,Vector3 point,int slot,bool heavy=false)
        {
            float size=slot<0?.75f:slot==3?1.55f:1.12f;Color c=Accent(actor);
            Quaternion facing=Face(actor.transform.position,point);
            Vector3 p=point+Vector3.up*.16f;
            HeroVfxPool.Emit(HeroFxShape.Slash,p,p,new Vector3(size,1,size*(heavy?.75f:1)),c,heavy?.26f:.16f,HeroFxMotion.Sweep,facing,actor);
            if(!heavy)Fx(HeroFxShape.Slash,p+Vector3.up*.025f,Color.Lerp(c,Color.white,.7f),size*.78f,.09f,HeroFxMotion.Sweep,facing,actor);
            Shards(actor,p,size,slot<0?2:3);
        }
        private static void Rift(GameObject actor,Vector3 p,float size,float life)
        {
            Quaternion facing=Face(actor.transform.position,p);p+=Vector3.up*.20f;
            HeroVfxPool.Emit(HeroFxShape.Rift,p,p,new Vector3(size*1.2f,size,.9f),Accent(actor),life,HeroFxMotion.Contract,facing,actor);
            HeroVfxPool.Emit(HeroFxShape.Rift,p-facing*Vector3.forward*.015f,p,new Vector3(size*.55f,size*.9f,.9f),DarkCore,life,HeroFxMotion.Contract,facing,actor);
        }
        public static void CancelWarnings(GameObject actor)=>HeroVfxPool.CancelWarnings(actor);
        public static void Cast(GameObject actor,int slot)
        {
            if(actor==null)return;
            HeroId h=Hero(actor);float size=slot==3?.45f:.22f;Vector3 p=Muzzle(actor);
            Fx(h==HeroId.Mira?HeroFxShape.Shard:HeroFxShape.Orb,p,Accent(actor),size,.12f,HeroFxMotion.Contract,actor:actor);
            if(slot==3&&(h==HeroId.Rook||h==HeroId.Kaelon))Ring(actor,Chest(actor),.65f,.22f,true,h==HeroId.Kaelon);
        }
        public static void TargetWarning(GameObject actor,Health target,float seconds)
        {
            if(actor==null||target==null)return;
            Vector3 p=Ground(target.transform.position);
            Fx(HeroFxShape.Hex,p,new Color(1,.48f,.16f),.82f,seconds,HeroFxMotion.Hold,actor:actor,follow:target.transform,warning:true);
            Fx(HeroFxShape.Rift,target.transform.position+Vector3.up*.25f,Accent(actor),.6f,seconds,HeroFxMotion.Contract,Face(actor.transform.position,target.transform.position),actor,target.transform,true);
        }
        public static void Area(GameObject actor,Vector3 center,float radius,float duration,bool warning,bool beneficial=false,int slot=-1)
        {
            if(actor==null)return;
            HeroId h=Hero(actor);Color c=Accent(actor);Vector3 p=Ground(center);float life=Mathf.Max(.06f,duration);
            if(warning)
            {
                // Exactly the authoritative area radius, never a growing fake hit zone.
                Fx(h==HeroId.Kaelon?HeroFxShape.Hex:HeroFxShape.Ring,p,beneficial?c:new Color(1,.48f,.16f),radius,life,HeroFxMotion.Hold,actor:actor,warning:true);
                if(h==HeroId.Elyra)
                {
                    Fx(HeroFxShape.Orb,p+Vector3.up*.5f,slot==3?DarkCore:c,slot==3?.85f:.45f,life,HeroFxMotion.Contract,actor:actor,warning:true);
                    Fx(HeroFxShape.Ring,p+Vector3.up*.5f,c,radius*.48f,life,HeroFxMotion.Contract,Quaternion.Euler(32,0,0),actor,warning:true);
                }
                else if(h==HeroId.Kaelon&&slot==2)
                {Fx(HeroFxShape.Shard,p+Vector3.up*.35f,c,.55f,life,HeroFxMotion.Hold,actor:actor,warning:true);Ring(actor,p,radius*.78f,life,true);}
                return;
            }
            float aftermath=slot==3?.48f:.30f;
            switch(h)
            {
                case HeroId.Elyra:
                    if(slot==0){Ring(actor,p,radius,aftermath);Shards(actor,p+Vector3.up*.2f,radius*.55f,4);}
                    else
                    {
                        Fx(HeroFxShape.Orb,p+Vector3.up*.45f,DarkCore,slot==3?1.25f:.60f,aftermath,HeroFxMotion.Contract);
                        Ring(actor,p,radius,aftermath,true);
                        Fx(HeroFxShape.Ring,p+Vector3.up*.45f,c,radius*.7f,aftermath,HeroFxMotion.Contract,Quaternion.Euler(30,0,20));
                        if(slot==3)Ring(actor,p,radius,.16f);
                    }break;
                case HeroId.Mira:
                    Ring(actor,p,radius,.55f);Fx(HeroFxShape.Plus,p+Vector3.up*.12f,c,.8f,.45f);break;
                case HeroId.Kaelon:
                    Ring(actor,p,radius,.42f,slot==2,true);
                    if(slot==3)HeroVfxPool.Emit(HeroFxShape.Shield,p,p,new Vector3(radius,.65f,radius),new Color(c.r,c.g,c.b,.60f),.40f,HeroFxMotion.Expand);
                    else Fx(HeroFxShape.Shard,p+Vector3.up*.25f,c,.5f,.2f);break;
                case HeroId.Rook:
                    Ring(actor,p,radius,.28f);Fx(HeroFxShape.Cracks,p+Vector3.up*.012f,c,radius*.86f,.38f);Shards(actor,p,radius*.6f,4);break;
                default:Ring(actor,p,radius,.3f);break;
            }
        }
        public static void Spell(GameObject actor,Vector3 impact,int slot,float travelTime=0)
        {
            if(actor==null)return;HeroId h=Hero(actor);
            switch(h)
            {
                case HeroId.Veyr:
                    if(slot==1)Rift(actor,impact,1,.28f);
                    else if(slot==3)
                    {
                        Quaternion direction=Face(actor.transform.position,impact);
                        Fx(HeroFxShape.Slash,impact+Vector3.up*.25f,Accent(actor),1.5f,.18f,HeroFxMotion.Sweep,direction*Quaternion.Euler(45,0,25),actor);
                        Fx(HeroFxShape.Slash,impact+Vector3.up*.25f,Color.Lerp(Accent(actor),Color.white,.6f),1.35f,.16f,HeroFxMotion.Sweep,direction*Quaternion.Euler(-45,180,-25),actor);
                        Rift(actor,impact,.85f,.32f);
                    }
                    else Slash(actor,impact,slot);break;
                case HeroId.Kaelon:
                    if(slot==0){Ring(actor,Ground(impact),.9f,.24f,false,true);Fx(HeroFxShape.Cracks,Ground(impact),Accent(actor),.75f,.22f);Shards(actor,impact,1,3);}break;
                case HeroId.Rook:
                    if(slot==0)Slash(actor,impact,slot,true);break;
                case HeroId.Elyra:
                    if(slot==0)Projectile(actor,impact,slot,travelTime);
                    // Field and singularity form at their authoritative warning center.
                    break;
                case HeroId.Nyra:
                    Projectile(actor,impact,slot,travelTime);
                    if(slot==3)actor.GetComponent<HeroVisualMotion>()?.PulseAbility(3,0);
                    break;
                case HeroId.Mira:Projectile(actor,impact,slot,travelTime);break;
            }
        }
        public static void Ultimate(GameObject actor,Vector3 target)
        {if(actor!=null)Cast(actor,3);}
        public static void Impact(GameObject actor,Health target,int slot)
        {
            if(actor==null||target==null)return;
            Vector3 p=target.transform.position+Vector3.up*.35f;Color c=Accent(actor);
            float strength=slot<0?.26f:slot==3?.58f:.40f;
            Fx(HeroFxShape.Orb,p,Color.Lerp(c,Color.white,.7f),strength,.10f);
            if(Hero(actor)==HeroId.Nyra&&slot==2)
            {
                Fx(HeroFxShape.Ring,p,c,.67f,.5f,HeroFxMotion.Contract,Quaternion.Euler(65,0,0),actor,target.transform);
                Fx(HeroFxShape.Ring,p,c,.70f,.5f,HeroFxMotion.Contract,Quaternion.Euler(65,70,0),actor,target.transform);
            }
            else if(slot>=0&&Hero(actor)==HeroId.Kaelon)
                Fx(HeroFxShape.Hex,p+Vector3.up*.6f,c,.34f,.45f,HeroFxMotion.Orbit,actor:actor,follow:target.transform);
        }
        public static void Support(GameObject actor,Health target,bool shield,float duration)
        {
            if(actor==null||target==null)return;Color c=Accent(actor);Vector3 p=target.transform.position+Vector3.down*.70f;
            if(shield)
            {
                // Color follows the caster: Auren amber, Tahla green. Lifespan follows real shield health.
                HeroVfxPool.Emit(HeroFxShape.Shield,p,p,new Vector3(.87f,1.1f,.87f),c,duration,HeroFxMotion.Hold,owner:actor,follow:target.transform,shield:target);
                if(Hero(actor)==HeroId.Rook)actor.GetComponent<HeroVisualMotion>()?.Empower(Mathf.Min(duration,1.2f));
            }
            else
            {
                Color heal=new Color(.22f,1,.52f);
                Fx(HeroFxShape.Plus,target.transform.position+Vector3.up*.45f,heal,.48f,.48f,actor:actor,follow:target.transform);
                HeroVfxPool.Emit(HeroFxShape.Ring,p,p+Vector3.up*.8f,Vector3.one*.58f,heal,.40f,HeroFxMotion.Travel,owner:actor);
            }
        }
        public static void SupportLink(GameObject actor,Health target)
        {
            if(actor==null||target==null)return;Vector3 from=Muzzle(actor),to=target.transform.position+Vector3.up*.3f;
            for(int i=0;i<3;i++)HeroVfxPool.Emit(HeroFxShape.Shard,from,to,Vector3.one*.18f,new Color(.25f,1,.55f),.16f,HeroFxMotion.Travel,Face(from,to),actor,delay:i*.035f);
        }
        public static void Debuff(GameObject actor,Health target)
        {
            if(actor==null||target==null||target.GetComponent<HeroIdentity>()==null)return;
            HeroId h=Hero(actor);if(h==HeroId.Nyra)return; // Dedicated snare bands already show this effect.
            Ring(actor,Ground(target.transform.position),.6f,.35f,h==HeroId.Elyra,h==HeroId.Kaelon,follow:target.transform);
        }
        public static void PlayerHit(GameObject victim,GameObject source)
        {
            if(victim==null||source==null)return;
            Fx(HeroFxShape.Slash,Chest(victim),new Color(1,.50f,.28f),.43f,.10f,HeroFxMotion.Contract,Face(victim.transform.position,source.transform.position));
        }
        public static void Basic(GameObject actor,Vector3 impact)
        {
            if(actor==null)return;actor.GetComponent<HeroVisualMotion>()?.PulseAttack(false);
            switch(Hero(actor))
            {
                case HeroId.Veyr:Slash(actor,impact,-1);break;
                case HeroId.Kaelon:Slash(actor,impact,-1,true);Ring(actor,Ground(impact),.48f,.18f,false,true);break;
                case HeroId.Rook:Slash(actor,impact,-1,true);Ring(actor,Ground(impact),.43f,.16f);break;
                default:Projectile(actor,impact,-1);break;
            }
        }
        public static void Link(GameObject actor,Vector3 impact,bool ultimate=false)
        {Basic(actor,impact);if(ultimate)Spell(actor,impact,3);}
        public static void Dash(GameObject actor,Vector3 from,Vector3 to)
        {
            if(actor==null)return;Color c=Accent(actor);HeroId h=Hero(actor);
            Quaternion face=Face(from,to);float distance=Vector3.Distance(from,to);
            if(h!=HeroId.Nyra)
            {
                Fx(HeroFxShape.Ring,from,c,.64f,.17f,HeroFxMotion.Contract,face*Quaternion.Euler(90,0,0));
                Fx(HeroFxShape.Ring,from,c,.64f,.17f,HeroFxMotion.Expand,face*Quaternion.Euler(90,0,0),actor,actor.transform,delay:.12f);
            }
            for(int i=0;i<3;i++)
            {
                Vector3 p=Vector3.Lerp(from,to,(i+.5f)/3);
                HeroVfxPool.Emit(h==HeroId.Nyra?HeroFxShape.Bolt:HeroFxShape.Rift,p,p,new Vector3(.18f,.48f,Mathf.Min(.7f,distance*.25f)),new Color(c.r,c.g,c.b,.48f),.16f,HeroFxMotion.Contract,face,actor,delay:i*.035f);
            }
            actor.GetComponent<HeroVisualMotion>()?.PulseDash();
        }
    }
}
