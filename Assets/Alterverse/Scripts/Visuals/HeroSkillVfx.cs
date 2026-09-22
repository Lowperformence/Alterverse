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
        private static void Projectile(GameObject actor,Vector3 end,int slot,float travel=0,Transform target=null)
        {
            HeroId h=Hero(actor);Color c=h==HeroId.Nyra?new Color(1f,.045f,.025f):Accent(actor);Vector3 start=Muzzle(actor);end+=Vector3.up*.25f;
            float strength=slot<0?1:slot==3?1.9f:1.5f;
            if(h==HeroId.Traveler)
            {
                TravelerProjectile(actor,start,end,slot,travel,target,strength);
                return;
            }
            if(travel<=0)
            {
                InstantTrace(actor,start,end,slot,strength);
                return;
            }
            float flight=travel;
            HeroFxShape shape=h==HeroId.Nyra?HeroFxShape.Bolt:h==HeroId.Mira?HeroFxShape.Seed:HeroFxShape.Orb;
            Vector3 size=h==HeroId.Nyra?new Vector3(.48f,.48f,.66f)*strength:Vector3.one*.36f*strength;
            HeroVfxPool.Emit(shape,start,end,size,c,flight,HeroFxMotion.Travel,Face(start,end),actor,endFollow:target);
            // A second cached mesh adds a feathered halo or a long narrow tracer tail.
            HeroVfxPool.Emit(h==HeroId.Nyra?HeroFxShape.Bolt:HeroFxShape.Halo,start,end,
                h==HeroId.Nyra?new Vector3(.26f,.26f,1.25f)*strength:Vector3.one*.42f*strength,
                new Color(c.r,c.g,c.b,.48f),flight,HeroFxMotion.Travel,Face(start,end),actor,endFollow:target);
            // Short physical muzzle core, rather than a full-distance beam.
            Fx(HeroFxShape.Burst,start,Color.Lerp(c,Color.white,.65f),.32f*strength,.09f,rotation:Face(start,end),actor:actor);
            if(h==HeroId.Elyra)Ring(actor,start,.28f*strength,.15f);
            if(h==HeroId.Mira)Fx(HeroFxShape.Shard,start,c,.22f,.12f,rotation:Quaternion.Euler(35,0,25));
        }
        private static void TravelerProjectile(GameObject actor,Vector3 start,Vector3 end,int slot,float travel,Transform target,float strength)
        {
            Color gold=Accent(actor);
            Color light=Color.Lerp(gold,Color.white,.62f);
            Color red=new(1f,.035f,.025f,.72f);
            Quaternion face=Face(start,end);
            if(travel<=0)
            {
                Vector3 delta=end-start;
                float distance=Mathf.Max(.01f,delta.magnitude);
                HeroVfxPool.Emit(HeroFxShape.Bolt,start,end,new Vector3(.15f,.15f,distance*.56f)*strength,
                    light,.11f,HeroFxMotion.Hold,face,actor);
                HeroVfxPool.Emit(HeroFxShape.Halo,start,end,new Vector3(.23f,.23f,distance*.62f)*strength,
                    new Color(gold.r,gold.g,gold.b,.40f),.13f,HeroFxMotion.Hold,face,actor);
                Fx(HeroFxShape.Ring,start,gold,.30f*strength,.13f,HeroFxMotion.Contract,face*Quaternion.Euler(90,0,0),actor);
                return;
            }

            HeroVfxPool.Emit(HeroFxShape.Bolt,start,end,new Vector3(.22f,.22f,.76f)*strength,
                light,travel,HeroFxMotion.Travel,face,actor,endFollow:target);
            HeroVfxPool.Emit(HeroFxShape.Halo,start,end,new Vector3(.34f,.34f,.95f)*strength,
                new Color(gold.r,gold.g,gold.b,.38f),travel,HeroFxMotion.Travel,face,actor,endFollow:target);
            Fx(HeroFxShape.Ring,start,gold,.30f*strength,.14f,HeroFxMotion.Contract,face*Quaternion.Euler(90,0,0),actor);
            Fx(HeroFxShape.Rift,start,DarkCore,.22f*strength,.12f,HeroFxMotion.Contract,face,actor);
            Fx(HeroFxShape.Shard,start+actor.transform.right*.08f,red,.15f,.11f,HeroFxMotion.Contract,actor:actor);
        }

        // Immediate gameplay hits need immediate visual contact, not a projectile arriving later.
        private static void InstantTrace(GameObject actor,Vector3 start,Vector3 end,int slot,float strength)
        {
            HeroId h=Hero(actor);Color c=h==HeroId.Nyra?new Color(1f,.045f,.025f):Accent(actor);Vector3 delta=end-start;
            Quaternion direction=delta.sqrMagnitude>.0001f?Quaternion.LookRotation(delta):Quaternion.identity;
            if(h==HeroId.Nyra)
            {
                // Bolt mesh spans -1.4..+.55 on Z. Fit those ends exactly to muzzle/contact.
                float length=delta.magnitude/1.95f;
                Vector3 center=start+delta*(1.4f/1.95f);
                HeroVfxPool.Emit(HeroFxShape.Bolt,center,center,new Vector3(.25f*strength,.25f*strength,length),c,.09f,HeroFxMotion.Hold,direction,actor);
            }
            else
            {
                HeroFxShape shape=h==HeroId.Mira?HeroFxShape.Seed:HeroFxShape.Orb;
                HeroVfxPool.Emit(shape,end,end,Vector3.one*.34f*strength,c,.14f,HeroFxMotion.Contract,direction,actor);
                for(int i=1;i<=2;i++)
                {
                    Vector3 p=Vector3.Lerp(start,end,i/3f);
                    Fx(h==HeroId.Mira?HeroFxShape.Seed:HeroFxShape.Runes,p,new Color(c.r,c.g,c.b,.38f),
                        (.16f+i*.035f)*strength,.12f,HeroFxMotion.Contract,direction,actor);
                }
            }
            Fx(h==HeroId.Elyra?HeroFxShape.Ring:h==HeroId.Mira?HeroFxShape.Seed:HeroFxShape.Burst,
                start,Color.Lerp(c,Color.white,.5f),.25f*strength,.09f,rotation:direction,actor:actor);
        }
        private static void HeavyContact(GameObject actor,Vector3 impact,int slot)
        {
            bool tank=Hero(actor)==HeroId.Kaelon;float strength=slot<0?1:1.45f;
            Vector3 p=impact+Vector3.up*.20f;Quaternion direction=Face(Muzzle(actor),p);
            if(tank)
            {
                Fx(HeroFxShape.Hex,p,Accent(actor),.48f*strength,.20f,HeroFxMotion.Contract,Quaternion.Euler(25,0,0),actor);
                Fx(HeroFxShape.Cracks,Ground(impact),Accent(actor),.36f*strength,.20f,actor:actor);
            }
            else
            {
                Vector3 start=Muzzle(actor),center=Vector3.Lerp(start,p,.70f);
                HeroVfxPool.Emit(HeroFxShape.Bolt,center,center,new Vector3(.34f,.30f,.38f)*strength,
                    Accent(actor),.11f,HeroFxMotion.Contract,direction,actor);
                Ring(actor,p,.42f*strength,.18f);
                // Brakk's gauntlet contact reads through a short ground fracture rather than
                // Auren's geometric shield/hex language.
                Fx(HeroFxShape.Cracks,Ground(impact),Accent(actor),.34f*strength,.18f,actor:actor);
            }
            Shards(actor,p,.58f*strength,2);
        }
        private static void Slash(GameObject actor,Vector3 point,int slot,bool heavy=false)
        {
            float size=slot<0?.75f:slot==3?1.55f:1.12f;Color c=Accent(actor);
            Quaternion facing=Face(actor.transform.position,point);
            // Span from the weapon side toward contact instead of drawing a detached arc behind the victim.
            Vector3 p=Vector3.Lerp(Muzzle(actor),point+Vector3.up*.16f,.62f);
            HeroVfxPool.Emit(HeroFxShape.Slash,p,p,new Vector3(size,1,size*(heavy?.75f:1)),c,heavy?.26f:.16f,HeroFxMotion.Sweep,facing,actor);
            if(!heavy)Fx(HeroFxShape.Slash,p+Vector3.up*.025f,Color.Lerp(c,Color.white,.7f),size*.78f,.09f,HeroFxMotion.Sweep,facing,actor);
            if(heavy)Fx(HeroFxShape.Burst,point+Vector3.up*.15f,c,size*.7f,.17f,rotation:facing);
            Shards(actor,p,size,slot<0?2:3);
        }
        private static void Rift(GameObject actor,Vector3 p,float size,float life)
        {
            Quaternion facing=Face(actor.transform.position,p);p+=Vector3.up*.20f;
            HeroVfxPool.Emit(HeroFxShape.Rift,p,p,new Vector3(size*1.2f,size,.9f),Accent(actor),life,HeroFxMotion.Contract,facing,actor);
            Fx(HeroFxShape.Halo,p,Accent(actor)*new Color(1,1,1,.35f),size*.65f,life,HeroFxMotion.Contract,actor:actor);
            Fx(HeroFxShape.Burst,p,Accent(actor),size*.8f,.14f,rotation:facing);
            HeroVfxPool.Emit(HeroFxShape.Rift,p-facing*Vector3.forward*.015f,p,new Vector3(size*.55f,size*.9f,.9f),DarkCore,life,HeroFxMotion.Contract,facing,actor);
        }
        public static void CancelWarnings(GameObject actor)=>HeroVfxPool.CancelWarnings(actor);
        public static void CancelProjectiles(GameObject actor)=>HeroVfxPool.CancelProjectiles(actor);
        public static void Cast(GameObject actor,int slot)
        {
            if(actor==null)return;
            HeroId h=Hero(actor);Color c=Accent(actor);float size=slot==3?.45f:.22f;Vector3 p=Muzzle(actor);
            Quaternion facing=actor.transform.rotation;
            switch(h)
            {
                case HeroId.Veyr:
                    Fx(HeroFxShape.Rift,p,c,size*1.35f,.13f,HeroFxMotion.Contract,facing,actor);
                    Fx(HeroFxShape.Slash,p+actor.transform.forward*.08f,Color.Lerp(c,Color.white,.55f),size*1.15f,.09f,HeroFxMotion.Sweep,facing,actor);
                    break;
                case HeroId.Kaelon:
                    Fx(HeroFxShape.Hex,p,c,size*1.25f,.15f,HeroFxMotion.Contract,Quaternion.Euler(25,0,0),actor);
                    Fx(HeroFxShape.Shard,p,c,size*.75f,.12f,HeroFxMotion.Contract,actor:actor);
                    break;
                case HeroId.Nyra:
                    {
                        Color sniperRed=new(1f,.045f,.025f);
                        Fx(HeroFxShape.Bolt,p,Color.Lerp(sniperRed,Color.white,.40f),size*1.28f,.10f,HeroFxMotion.Contract,facing,actor);
                        Fx(HeroFxShape.Burst,p,sniperRed,size*.86f,.09f,rotation:facing,actor:actor);
                        Fx(HeroFxShape.Ring,p,new Color(sniperRed.r,sniperRed.g,sniperRed.b,.48f),size*.72f,.12f,HeroFxMotion.Contract,facing,actor);
                        break;
                    }
                case HeroId.Elyra:
                    Fx(HeroFxShape.Runes,p,c,size*1.30f,.16f,HeroFxMotion.Orbit,facing,actor);
                    Fx(HeroFxShape.Orb,p,Color.Lerp(c,Color.white,.35f),size*.82f,.14f,HeroFxMotion.Contract,actor:actor);
                    break;
                case HeroId.Mira:
                    {
                        Color holy=Color.Lerp(c,Color.white,.28f);
                        if(slot==0)
                        {
                            // Offensive seed keeps its sharp identity, now framed by Tahla's ascendant light.
                            Fx(HeroFxShape.Seed,p,holy,size*1.18f,.14f,HeroFxMotion.Contract,facing,actor);
                            Fx(HeroFxShape.Shard,p,Color.Lerp(c,Color.white,.68f),size*.62f,.11f,HeroFxMotion.Contract,actor:actor);
                            Fx(HeroFxShape.Ring,p,new Color(c.r,c.g,c.b,.45f),size*.72f,.12f,HeroFxMotion.Contract,facing,actor);
                        }
                        else if(slot==2)
                        {
                            Fx(HeroFxShape.Shield,p,holy,size*.98f,.17f,HeroFxMotion.Contract,facing,actor);
                            Fx(HeroFxShape.Ring,p,new Color(c.r,c.g,c.b,.58f),size*.94f,.16f,HeroFxMotion.Expand,actor:actor);
                            Fx(HeroFxShape.Halo,p,Color.Lerp(c,Color.white,.55f),size*.62f,.14f,HeroFxMotion.Contract,actor:actor);
                        }
                        else
                        {
                            Fx(HeroFxShape.Petals,p,new Color(c.r,c.g,c.b,.68f),size*1.02f,.17f,HeroFxMotion.Orbit,actor:actor);
                            Fx(HeroFxShape.Plus,p,Color.Lerp(c,Color.white,.65f),size*.60f,.15f,HeroFxMotion.Contract,actor:actor);
                            Fx(HeroFxShape.Halo,p,holy,size*(slot==3?.92f:.62f),slot==3?.22f:.14f,HeroFxMotion.Contract,actor:actor);
                        }
                        break;
                    }
                case HeroId.Traveler:
                    {
                        Color light=Color.Lerp(c,Color.white,.66f);
                        Color red=new(1f,.035f,.025f);
                        Fx(HeroFxShape.Ring,p,c,size*(slot==3?1.45f:1.05f),slot==3?.24f:.15f,HeroFxMotion.Contract,facing*Quaternion.Euler(90,0,0),actor);
                        Fx(HeroFxShape.Rift,p,DarkCore,size*(slot==3?1.25f:.82f),.16f,HeroFxMotion.Contract,facing,actor);
                        Fx(HeroFxShape.Burst,p,light,size*(slot==3?1.15f:.68f),.10f,rotation:facing,actor:actor);
                        Fx(HeroFxShape.Shard,p+actor.transform.right*.06f,new Color(red.r,red.g,red.b,.78f),size*.42f,.12f,HeroFxMotion.Contract,actor:actor);
                        break;
                    }
                case HeroId.Traveler:
                    {
                        Color light=Color.Lerp(c,Color.white,.68f);
                        Color red=new(1f,.035f,.025f);
                        Ring(actor,p,radius,slot==3?.44f:.32f,true);
                        Fx(HeroFxShape.Rift,p+Vector3.up*.40f,DarkCore,slot==3?1.05f:.58f,aftermath,HeroFxMotion.Contract);
                        Fx(HeroFxShape.Ring,p+Vector3.up*.42f,light,radius*(slot==3?.62f:.42f),aftermath,HeroFxMotion.Contract,Quaternion.Euler(28,0,24));
                        if(slot==3)
                        {
                            HeroVfxPool.Emit(HeroFxShape.ShockDome,p,p,new Vector3(radius,.80f,radius),
                                new Color(light.r,light.g,light.b,.48f),.34f,HeroFxMotion.Contract);
                            Fx(HeroFxShape.Burst,p+Vector3.up*.18f,light,radius*.48f,.18f);
                            for(int i=0;i<4;i++)Fx(HeroFxShape.Shard,p+Vector3.up*.22f,new Color(red.r,red.g,red.b,.72f),radius*.18f,.20f,HeroFxMotion.Orbit,Quaternion.Euler(20,i*90,45));
                        }
                        break;
                    }
                case HeroId.Traveler:
                    {
                        Color light=Color.Lerp(c,Color.white,.70f);
                        Color red=new(1f,.035f,.025f,.78f);
                        Fx(HeroFxShape.Halo,p,light,strength*1.55f,.13f,HeroFxMotion.Contract,actor:actor);
                        Fx(HeroFxShape.Ring,p,c,strength*1.35f,.16f,HeroFxMotion.Contract,Face(actor.transform.position,p)*Quaternion.Euler(90,0,0),actor);
                        Fx(HeroFxShape.Rift,p,DarkCore,strength*.85f,.13f,HeroFxMotion.Contract,Face(actor.transform.position,p),actor);
                        if(slot>=0)Fx(HeroFxShape.Shard,p,red,strength*.65f,.12f,HeroFxMotion.Contract,actor:actor);
                        break;
                    }
                case HeroId.Rook:
                    {
                        Color hot=new(1f,.34f,.035f);
                        Fx(HeroFxShape.Burst,p,hot,size*1.28f,.13f,rotation:facing,actor:actor);
                        Fx(HeroFxShape.Ring,Ground(actor.transform.position),new Color(hot.r,hot.g,hot.b,.62f),size*.95f,.16f,HeroFxMotion.Expand,actor:actor);
                        Fx(HeroFxShape.Cracks,Ground(actor.transform.position),c,size*.92f,.18f,actor:actor);
                        break;
                    }
                default:
                    Fx(HeroFxShape.Burst,p,c,size*1.12f,.12f,rotation:facing,actor:actor);
                    Fx(HeroFxShape.Cracks,Ground(actor.transform.position),c,size*.85f,.16f,actor:actor);
                    break;
            }
            if(slot==3)
            {
                if(h==HeroId.Rook||h==HeroId.Kaelon)Ring(actor,Chest(actor),.65f,.22f,true,h==HeroId.Kaelon);
                else if(h==HeroId.Elyra)Fx(HeroFxShape.Ring,Chest(actor),c,.72f,.22f,HeroFxMotion.Contract,Quaternion.Euler(28,0,0),actor);
                else if(h==HeroId.Traveler)
                {
                    Fx(HeroFxShape.Halo,Chest(actor),Color.Lerp(c,Color.white,.55f),.86f,.26f,HeroFxMotion.Contract,actor:actor);
                    Fx(HeroFxShape.Rift,Chest(actor),DarkCore,.68f,.24f,HeroFxMotion.Contract,actor:actor);
                }
                else if(h==HeroId.Veyr)Fx(HeroFxShape.Halo,Chest(actor),new Color(c.r,c.g,c.b,.42f),.68f,.18f,HeroFxMotion.Contract,actor:actor);
            }
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
                Fx(HeroFxShape.Ring,p,beneficial?c:new Color(1,.48f,.16f),radius,life,HeroFxMotion.Hold,actor:actor,warning:true);
                if(h==HeroId.Elyra)
                {
                    Fx(HeroFxShape.Orb,p+Vector3.up*.5f,slot==3?DarkCore:c,slot==3?.85f:.45f,life,HeroFxMotion.Contract,actor:actor,warning:true);
                    Fx(HeroFxShape.Ring,p+Vector3.up*.5f,c,radius*.48f,life,HeroFxMotion.Contract,Quaternion.Euler(32,0,0),actor,warning:true);
                }
                else if(h==HeroId.Kaelon&&slot==2)
                {Fx(HeroFxShape.Shard,p+Vector3.up*.35f,c,.55f,life,HeroFxMotion.Hold,actor:actor,warning:true);Ring(actor,p,radius*.78f,life,true);}
                else if(h==HeroId.Traveler)
                {
                    Fx(HeroFxShape.Runes,p+Vector3.up*.025f,new Color(c.r,c.g,c.b,.60f),radius*.72f,life,HeroFxMotion.Orbit,actor:actor,warning:true);
                    Fx(HeroFxShape.Rift,p+Vector3.up*.34f,DarkCore,slot==3?.82f:.52f,life,HeroFxMotion.Contract,actor:actor,warning:true);
                    Fx(HeroFxShape.Ring,p+Vector3.up*.34f,Color.Lerp(c,Color.white,.48f),radius*.40f,life,HeroFxMotion.Contract,Quaternion.Euler(28,0,18),actor,warning:true);
                }
                return;
            }
            float aftermath=slot==3?.48f:.30f;
            // Inner decoration never replaces or enlarges the authoritative warning boundary.
            if(h==HeroId.Mira)
                Fx(HeroFxShape.Petals,p+Vector3.up*.04f,c,radius*.85f,.6f,HeroFxMotion.Expand);
            else if(h==HeroId.Elyra)
            {
                Fx(HeroFxShape.Runes,p+Vector3.up*.03f,c,radius*.9f,aftermath,HeroFxMotion.Orbit);
                if(slot==3)Fx(HeroFxShape.Halo,p+Vector3.up*.06f,new Color(c.r,c.g,c.b,.6f),radius*.7f,.22f,HeroFxMotion.Contract);
            }
            else if(h==HeroId.Traveler)
            {
                Fx(HeroFxShape.Runes,p+Vector3.up*.025f,c,radius*.86f,aftermath,HeroFxMotion.Orbit);
                Fx(HeroFxShape.Halo,p+Vector3.up*.08f,Color.Lerp(c,Color.white,.55f),radius*(slot==3?.72f:.52f),aftermath,HeroFxMotion.Contract);
            }
            else if(h==HeroId.Kaelon||h==HeroId.Rook)
                HeroVfxPool.Emit(HeroFxShape.ShockDome,p,p,new Vector3(radius,slot==3?1.2f:.55f,radius),
                    new Color(c.r,c.g,c.b,.65f),slot==3?.4f:.26f,HeroFxMotion.Expand);
            if(slot==3)Fx(HeroFxShape.Burst,p+Vector3.up*.08f,Color.Lerp(c,Color.white,.35f),radius*.72f,.20f);
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
                    {
                        Color holy=Color.Lerp(c,Color.white,.42f);
                        Ring(actor,p,radius,.55f);
                        Fx(HeroFxShape.Plus,p+Vector3.up*.12f,holy,.82f,.45f);
                        Fx(HeroFxShape.Petals,p+Vector3.up*.05f,new Color(c.r,c.g,c.b,.58f),radius*.72f,.42f,HeroFxMotion.Orbit);
                        Fx(HeroFxShape.Runes,p+Vector3.up*.02f,new Color(holy.r,holy.g,holy.b,.45f),radius*.62f,.34f,HeroFxMotion.Contract);
                        if(slot==3)Fx(HeroFxShape.Halo,p+Vector3.up*.10f,holy,radius*.52f,.30f,HeroFxMotion.Expand);
                        break;
                    }
                case HeroId.Kaelon:
                    Ring(actor,p,radius,.42f,slot==2,true);
                    if(slot==3)HeroVfxPool.Emit(HeroFxShape.Shield,p,p,new Vector3(radius,.65f,radius),new Color(c.r,c.g,c.b,.60f),.40f,HeroFxMotion.Expand);
                    else Fx(HeroFxShape.Shard,p+Vector3.up*.25f,c,.5f,.2f);break;
                case HeroId.Rook:
                    {
                        Color hot=new(1f,.34f,.035f);
                        Ring(actor,p,radius,.28f);
                        Fx(HeroFxShape.Burst,p+Vector3.up*.08f,hot,radius*.34f,.16f);
                        Fx(HeroFxShape.Cracks,p+Vector3.up*.012f,c,radius*.90f,.38f);
                        Shards(actor,p,radius*.65f,4);
                        break;
                    }
                default:Ring(actor,p,radius,.3f);break;
            }
        }
        public static void Spell(GameObject actor,Vector3 impact,int slot,float travelTime=0,Transform target=null)
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
                    if(slot==0)HeavyContact(actor,impact,slot);break;
                case HeroId.Elyra:
                    if(slot==0)Projectile(actor,impact,slot,travelTime,target);
                    // Field and singularity form at their authoritative warning center.
                    break;
                case HeroId.Nyra:
                    Projectile(actor,impact,slot,travelTime,target);
                    if(slot==3)actor.GetComponent<HeroVisualMotion>()?.PulseAbility(3,0);
                    break;
                case HeroId.Mira:Projectile(actor,impact,slot,travelTime,target);break;
                case HeroId.Traveler:
                    if(slot==0)Projectile(actor,impact,slot,travelTime,target);
                    break;
            }
        }
        public static void Ultimate(GameObject actor,Vector3 target)
        {if(actor!=null)Cast(actor,3);}
        public static void Impact(GameObject actor,Health target,int slot)
        {
            if(actor==null||target==null)return;
            Vector3 p=target.transform.position+Vector3.up*.35f;Color c=Accent(actor);
            float strength=slot<0?.26f:slot==3?.58f:.40f;
            HeroId hero=Hero(actor);
            // Contact core is deliberately small. The hero-specific silhouette below is what
            // communicates who hit you, instead of one generic white explosion for every attack.
            Fx(slot<0&&hero==HeroId.Nyra?HeroFxShape.Burst:HeroFxShape.Halo,p,
                Color.Lerp(c,Color.white,.68f),strength*(slot<0?1.35f:1.8f),.10f);

            switch(hero)
            {
                case HeroId.Veyr:
                    Fx(HeroFxShape.Slash,p,c,strength*2,.16f,HeroFxMotion.Sweep,Face(actor.transform.position,p)*Quaternion.Euler(25,0,30));
                    if(slot<0)Fx(HeroFxShape.Rift,p,DarkCore,strength*1.15f,.12f,HeroFxMotion.Contract,Face(actor.transform.position,p),actor);
                    break;
                case HeroId.Kaelon:
                    Fx(HeroFxShape.Hex,p,c,strength*1.7f,.19f,rotation:Quaternion.Euler(30,0,0));
                    if(slot<0)Fx(HeroFxShape.Cracks,Ground(target.transform.position),c,strength*1.35f,.18f,actor:actor);
                    break;
                case HeroId.Nyra:
                    {
                        Color sniperRed=new(1f,.045f,.025f);
                        Fx(HeroFxShape.Burst,p,sniperRed,strength*2.15f,.13f,rotation:Face(actor.transform.position,p));
                        Fx(HeroFxShape.Halo,p,Color.Lerp(sniperRed,Color.white,.35f),strength*.92f,.11f,HeroFxMotion.Contract,actor:actor);
                        break;
                    }
                case HeroId.Elyra:
                    Fx(HeroFxShape.Runes,p,c,strength*2,.24f,HeroFxMotion.Contract);
                    if(slot<0)Fx(HeroFxShape.Orb,p,DarkCore,strength*.82f,.16f,HeroFxMotion.Contract,actor:actor);
                    break;
                case HeroId.Mira:
                    Fx(HeroFxShape.Seed,p,c,strength*2,.14f,HeroFxMotion.Contract,Face(actor.transform.position,p));
                    if(slot<0)Fx(HeroFxShape.Shard,p,Color.Lerp(c,Color.white,.40f),strength*.92f,.13f,HeroFxMotion.Contract,actor:actor);
                    break;
                case HeroId.Rook:
                    {
                        Color hot=new(1f,.34f,.035f);
                        HeroVfxPool.Emit(HeroFxShape.ShockDome,p,p,new Vector3(strength*2.15f,strength*1.05f,strength*2.15f),
                            new Color(hot.r,hot.g,hot.b,.78f),.18f);
                        Fx(HeroFxShape.Burst,p,Color.Lerp(hot,Color.white,.28f),strength*1.25f,.12f,rotation:Face(actor.transform.position,p),actor:actor);
                        if(slot<0)Fx(HeroFxShape.Cracks,Ground(target.transform.position),c,strength*1.65f,.22f,actor:actor);
                        break;
                    }
            }
            if(Hero(actor)==HeroId.Nyra&&slot==2)
            {
                Color sniperRed=new(1f,.045f,.025f);
                Fx(HeroFxShape.Ring,p,sniperRed,.67f,.5f,HeroFxMotion.Contract,Quaternion.Euler(65,0,0),actor,target.transform);
                Fx(HeroFxShape.Ring,p,sniperRed,.70f,.5f,HeroFxMotion.Contract,Quaternion.Euler(65,70,0),actor,target.transform);
            }
            else if(slot>=0&&Hero(actor)==HeroId.Kaelon)
                Fx(HeroFxShape.Hex,p+Vector3.up*.6f,c,.34f,.45f,HeroFxMotion.Orbit,actor:actor,follow:target.transform);
        }
        public static void ShieldContact(GameObject victim,GameObject source)
        {
            if(victim==null)return;
            Vector3 p=victim.transform.position+Vector3.up*.35f;
            Quaternion face=source!=null?Face(source.transform.position,p):Quaternion.identity;
            Fx(HeroFxShape.Hex,p,new Color(.55f,.90f,1f),.72f,.16f,HeroFxMotion.Contract,
                face*Quaternion.Euler(65,0,0),follow:victim.transform);
        }
        public static void Support(GameObject actor,Health target,bool shield,float duration)
        {
            if(actor==null||target==null)return;
            HeroId hero=Hero(actor);
            Color c=Accent(actor);
            Color holy=hero==HeroId.Mira?Color.Lerp(c,Color.white,.38f):c;
            Vector3 p=target.transform.position+Vector3.down*.70f;
            if(shield)
            {
                HeroVfxPool.Emit(HeroFxShape.Shield,p,p,new Vector3(.87f,1.1f,.87f),holy,duration,HeroFxMotion.Hold,owner:actor,follow:target.transform,shield:target);
                if(hero==HeroId.Mira)
                {
                    Fx(HeroFxShape.Ring,p+Vector3.up*.07f,new Color(c.r,c.g,c.b,.55f),.88f,.32f,HeroFxMotion.Expand,actor:actor,follow:target.transform);
                    Fx(HeroFxShape.Halo,target.transform.position+Vector3.up*.42f,holy,.52f,.24f,HeroFxMotion.Contract,actor:actor,follow:target.transform);
                }
                if(hero==HeroId.Rook)actor.GetComponent<HeroVisualMotion>()?.Empower(Mathf.Min(duration,1.2f));
            }
            else
            {
                Color heal=hero==HeroId.Mira?new Color(.68f,.97f,1f):new Color(.22f,1,.52f);
                Fx(HeroFxShape.Plus,target.transform.position+Vector3.up*.45f,heal,.52f,.48f,actor:actor,follow:target.transform);
                Fx(HeroFxShape.Petals,p+Vector3.up*.08f,new Color(heal.r,heal.g,heal.b,.68f),.86f,.45f,HeroFxMotion.Orbit,actor:actor,follow:target.transform);
                HeroVfxPool.Emit(HeroFxShape.Ring,p,p+Vector3.up*.8f,Vector3.one*.62f,heal,.40f,HeroFxMotion.Travel,owner:actor);
                if(hero==HeroId.Mira)
                    Fx(HeroFxShape.Halo,target.transform.position+Vector3.up*.25f,Color.Lerp(heal,Color.white,.45f),.58f,.28f,HeroFxMotion.Contract,actor:actor,follow:target.transform);
            }
        }
        public static void SupportLink(GameObject actor,Health target)
        {
            if(actor==null||target==null)return;
            Vector3 from=Muzzle(actor),to=target.transform.position+Vector3.up*.3f;
            Color link=Hero(actor)==HeroId.Mira?new Color(.68f,.97f,1f):new Color(.25f,1,.55f);
            for(int i=0;i<3;i++)
                HeroVfxPool.Emit(HeroFxShape.Petals,from,to,Vector3.one*.25f,link,.16f,HeroFxMotion.Travel,Face(from,to),actor,delay:i*.035f);
            if(Hero(actor)==HeroId.Mira)
                HeroVfxPool.Emit(HeroFxShape.Bolt,from,to,new Vector3(.10f,.10f,.48f),new Color(link.r,link.g,link.b,.42f),.14f,HeroFxMotion.Travel,Face(from,to),actor);
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
        public static void Basic(GameObject actor,Vector3 impact,float travelTime=0,Transform target=null,bool animate=true)
        {
            if(actor==null)return;if(animate)actor.GetComponent<HeroVisualMotion>()?.PulseAttack(false);
            switch(Hero(actor))
            {
                case HeroId.Veyr:Slash(actor,impact,-1);break;
                case HeroId.Kaelon:
                case HeroId.Rook:HeavyContact(actor,impact,-1);break;
                default:Projectile(actor,impact,-1,travelTime,target);break;
            }
        }
        public static void Link(GameObject actor,Vector3 impact,bool ultimate=false)
        {Basic(actor,impact);if(ultimate)Spell(actor,impact,3);}
        public static void Dash(GameObject actor,Vector3 from,Vector3 to)
        {
            if(actor==null)return;Color c=Accent(actor);HeroId h=Hero(actor);
            Quaternion face=Face(from,to);float distance=Vector3.Distance(from,to);
            if(h==HeroId.Traveler)
            {
                Color light=Color.Lerp(c,Color.white,.58f);
                Color red=new(1f,.035f,.025f,.72f);
                Quaternion portal=face*Quaternion.Euler(90,0,0);
                Fx(HeroFxShape.Ring,from,c,.72f,.22f,HeroFxMotion.Contract,portal,actor);
                Fx(HeroFxShape.Rift,from,DarkCore,.62f,.20f,HeroFxMotion.Contract,face,actor);
                Fx(HeroFxShape.Ring,to,light,.76f,.23f,HeroFxMotion.Expand,portal,actor);
                Fx(HeroFxShape.Rift,to,DarkCore,.66f,.21f,HeroFxMotion.Contract,face,actor);
                for(int i=0;i<4;i++)
                {
                    Vector3 pos=Vector3.Lerp(from,to,(i+.5f)/4f);
                    HeroVfxPool.Emit(HeroFxShape.Rift,pos,pos,new Vector3(.13f,.34f,Mathf.Min(.55f,distance*.18f)),
                        i%2==0?new Color(c.r,c.g,c.b,.42f):red,.15f,HeroFxMotion.Contract,face,actor,delay:i*.025f);
                }
                Fx(HeroFxShape.Burst,to,light,.34f,.11f,rotation:face,actor:actor);
                actor.GetComponent<HeroVisualMotion>()?.PulseDash();
                return;
            }
            if(h!=HeroId.Nyra)
            {
                Fx(HeroFxShape.Runes,from,c,.72f,.17f,HeroFxMotion.Contract,face*Quaternion.Euler(70,0,0));
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

