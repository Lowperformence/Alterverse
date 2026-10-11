using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Performance;
using UnityEngine;

namespace Alterverse.Visuals
{
    // Semantic effects facade shared by players, bots, support items and founder presentation.
    public sealed partial class HeroSkillVfx:MonoBehaviour
    {
        private static readonly Color DarkCore=new Color(.018f,.009f,.035f,.96f);
        private static HeroId Hero(GameObject actor)=>actor!=null&&actor.TryGetComponent(out HeroIdentity id)?id.Hero:HeroId.Veyr;
        private static Color Accent(GameObject actor)=>Hero(actor)==HeroId.Mira?new Color(1f,.72f,.28f,1f):StylizedArtDirection.HeroAccent(Hero(actor));
        private static Vector3 Chest(GameObject actor)=>actor.transform.position+Vector3.up*.25f;
        private static Vector3 Ground(Vector3 p)=>new Vector3(p.x,.20f,p.z);
        private static Quaternion Face(Vector3 from,Vector3 to)
        {Vector3 d=to-from;d.y=0;return d.sqrMagnitude>.0001f?Quaternion.LookRotation(d):Quaternion.identity;}
        private static void Fx(HeroFxShape shape,Vector3 p,Color color,float size,float life,HeroFxMotion motion=HeroFxMotion.Expand,
            Quaternion rotation=default,GameObject actor=null,Transform follow=null,bool warning=false,float delay=0,bool decorative=false)
        {HeroVfxPool.Emit(shape,p,p,Vector3.one*size,color,life,motion,rotation,actor,warning,follow,null,delay,decorative:decorative);}
        private static void Ring(GameObject actor,Vector3 p,float radius,float life,bool inward=false,bool hex=false,bool warning=false,Transform follow=null)
        {Fx(hex?HeroFxShape.Hex:HeroFxShape.Ring,p,Accent(actor),radius,life,warning?HeroFxMotion.Hold:inward?HeroFxMotion.Contract:HeroFxMotion.Expand,actor:actor,follow:follow,warning:warning);}
        private static void Shards(GameObject actor,Vector3 p,float size,int count=3)
        {
            if(!AdaptiveVfxBudget.AllowDecorative)return;
            Color c=Accent(actor);
            count=AdaptiveVfxBudget.Count(count);
            for(int i=0;i<count;i++)
            {
                float a=(i*.618034f+Time.frameCount*.013f)*Mathf.PI*2;
                Vector3 d=new Vector3(Mathf.Cos(a),-.08f,Mathf.Sin(a));
                HeroVfxPool.Emit(HeroFxShape.Shard,p,p+d*size,new Vector3(.07f,.14f,.07f)*size,
                    c,.26f,HeroFxMotion.Scatter,Quaternion.Euler(24,a*Mathf.Rad2Deg,45),actor,decorative:true);
            }
        }
        private static Vector3 Muzzle(GameObject actor)
        {
            HeroVisualMotion motion=actor.GetComponent<HeroVisualMotion>();
            Vector3 muzzle = motion!=null?motion.ReleasePoint:Chest(actor)+actor.transform.forward*.5f;
            Vector3? origin = actor.GetComponent<HeroAttackDelivery>()?.VisualOrigin;
            return origin.HasValue ? new Vector3(origin.Value.x,muzzle.y,origin.Value.z) : muzzle;
        }
        private static void Projectile(GameObject actor,Vector3 end,int slot,float travel=0,Transform target=null)
        {
            HeroId h=Hero(actor);Color c=h==HeroId.Nyra?new Color(1f,.045f,.025f):Accent(actor);Vector3 start=Muzzle(actor);end+=Vector3.up*.25f;
            float strength=slot<0?1:slot==3?1.9f:1.5f;
            if(h==HeroId.Nyra&&slot==0)
            {
                PrismLaser(actor,start,end,travel,target);
                return;
            }
            if(h==HeroId.Nyra&&slot==2)
            {
                // Light Snare replaces the red shot with one small white light sphere.
                if(travel>0)
                    HeroVfxPool.Emit(HeroFxShape.Orb,start,end,Vector3.one*.34f,Color.white,travel,
                        HeroFxMotion.Travel,Face(start,end),actor,endFollow:target);
                else Fx(HeroFxShape.Orb,end,Color.white,.34f,.16f,HeroFxMotion.Contract,actor:actor);
                Fx(HeroFxShape.Burst,start,Color.white,.16f,.09f,rotation:Face(start,end),actor:actor);
                return;
            }
            if(h==HeroId.Nyra&&slot==3)
            {
                // Three red spheres: 30% smaller and 30% transparent (70% opacity).
                Color orbRed=new(c.r,c.g,c.b,.70f);
                if(travel>0)
                    HeroVfxPool.Emit(HeroFxShape.Orb,start,end,Vector3.one*(.62f*.70f),orbRed,travel,
                        HeroFxMotion.Travel,Face(start,end),actor,endFollow:target);
                else Fx(HeroFxShape.Orb,end,orbRed,.62f*.70f,.16f,HeroFxMotion.Contract,actor:actor);
                Fx(HeroFxShape.Burst,start,c,.22f,.09f,rotation:Face(start,end),actor:actor);
                return;
            }
            if(h==HeroId.Traveler)
            {
                if(slot<0)TravelerBasicProjectile(actor,start,end,slot,travel,target,strength);
                else TravelerProjectile(actor,start,end,slot,travel,target);
                return;
            }
            if(travel<=0)
            {
                InstantTrace(actor,start,end,slot,strength);
                return;
            }
            float flight=travel;
            HeroFxShape shape=h==HeroId.Nyra?HeroFxShape.Bolt:h==HeroId.Mira?HeroFxShape.Seed:HeroFxShape.Orb;
            Vector3 size=h==HeroId.Nyra?new Vector3(.55f,.55f,1.05f)*strength:Vector3.one*.72f*strength;
            HeroVfxPool.Emit(shape,start,end,size,c,flight,HeroFxMotion.Travel,Face(start,end),actor,endFollow:target);
            // A second cached mesh adds a feathered halo or a long narrow tracer tail.
            HeroVfxPool.Emit(h==HeroId.Nyra?HeroFxShape.Streak:HeroFxShape.Halo,start,end,
                h==HeroId.Nyra?new Vector3(.42f,.42f,1.75f)*strength:Vector3.one*.78f*strength,
                new Color(c.r,c.g,c.b,.68f),flight,HeroFxMotion.Travel,Face(start,end),actor,endFollow:target);
            // A narrow, bright core and staggered fragments make the projectile read as motion.
            HeroVfxPool.Emit(h==HeroId.Mira?HeroFxShape.Seed:HeroFxShape.Bolt,start,end,
                new Vector3(.21f,.21f,.58f)*strength,Color.Lerp(c,Color.white,.88f),
                flight,HeroFxMotion.Travel,Face(start,end),actor,endFollow:target);
            for(int streak=0;streak<(slot<0?1:2);streak++)
            {
                // Every layer reaches contact together, including point-blank shots.
                float delay=flight*(streak+1)*.12f;
                HeroVfxPool.Emit(h==HeroId.Mira?HeroFxShape.Seed:HeroFxShape.Shard,start,end,
                    new Vector3(.075f,.14f,.09f)*strength,new Color(c.r,c.g,c.b,.62f),
                    flight-delay,h==HeroId.Nyra?HeroFxMotion.Travel:HeroFxMotion.Spiral,
                    Face(start,end)*Quaternion.Euler(0,0,streak==0?36:-144),
                    actor,endFollow:target,delay:delay,decorative:true);
            }
            // Short physical muzzle core, rather than a full-distance beam.
            Fx(HeroFxShape.Burst,start,Color.Lerp(c,Color.white,.65f),.32f*strength,.09f,rotation:Face(start,end),actor:actor);
            if(h==HeroId.Elyra)
            {
                Ring(actor,start,.38f*strength,.16f);
                if(slot>=0)HeroVfxPool.Emit(HeroFxShape.Runes,start,end,Vector3.one*.55f*strength,
                    new Color(c.r,c.g,c.b,.82f),flight,HeroFxMotion.Travel,Face(start,end),actor,endFollow:target);
            }
            if(h==HeroId.Mira)
            {
                Fx(HeroFxShape.Shard,start,Color.Lerp(c,Color.white,.75f),.32f,.12f,rotation:Quaternion.Euler(35,0,25),actor:actor);
                if(slot>=0)HeroVfxPool.Emit(HeroFxShape.Petals,start,end,Vector3.one*.52f*strength,
                    new Color(c.r,c.g,c.b,.80f),flight,HeroFxMotion.Travel,Face(start,end),actor,endFollow:target);
            }
        }
        private static Color SivraSkillColor(int slot)=>slot==0?new Color(.06f,.38f,1f,1f):
            slot==2?Color.white:new Color(1f,.045f,.025f,1f);
        private static void PrismLaser(GameObject actor,Vector3 start,Vector3 end,float travel,Transform target)
        {
            float range=HeroAbilityKit.LoadoutFor(HeroId.Nyra).Q.Range;
            float duration=Mathf.Max(.12f,travel);
            // No travelling shot: the full blue beam is visible from release through contact.
            HeroVfxPool.Emit(HeroFxShape.Laser,start,end,new Vector3(.26f,.26f,1f),
                SivraSkillColor(0),duration,HeroFxMotion.LaserBeam,owner:actor,follow:target!=null?actor.transform:null,
                endFollow:target,cancelOnOwnerDeath:true,maxRange:range);
            HeroVfxPool.Emit(HeroFxShape.Laser,start,end,new Vector3(.075f,.075f,1f),
                new Color(.50f,.78f,1f,.94f),duration,HeroFxMotion.LaserBeam,owner:actor,follow:target!=null?actor.transform:null,
                endFollow:target,cancelOnOwnerDeath:true,maxRange:range);
            Fx(HeroFxShape.Burst,start,SivraSkillColor(0),.18f,.09f,actor:actor);
        }
        // Vertical portals and light arcs stay above the battlefield rather than covering the floor.
        private static void TravelerPortal(GameObject actor,Vector3 center,float size,float life,bool arrival=false)
        {
            Color violet=new(.62f,.24f,1f,.88f);
            Color pearl=new(.91f,.67f,1f,.91f);
            Quaternion upright=Quaternion.LookRotation(actor.transform.forward)*Quaternion.Euler(90f,0f,0f);
            Fx(HeroFxShape.Ring,center,violet,size,life,arrival?HeroFxMotion.Expand:HeroFxMotion.Contract,upright,actor);
            Fx(HeroFxShape.Ring,center,pearl,size*.73f,life*.82f,HeroFxMotion.Contract,upright*Quaternion.Euler(0,0,38),actor);
            Fx(HeroFxShape.Rift,center,DarkCore,size*.82f,life,HeroFxMotion.Contract,
                Quaternion.LookRotation(actor.transform.forward),actor);
            Fx(HeroFxShape.Slash,center+actor.transform.right*(size*.22f),pearl,size*.70f,life*.70f,
                HeroFxMotion.Sweep,upright*Quaternion.Euler(0,0,38),actor);
            Fx(HeroFxShape.Slash,center-actor.transform.right*(size*.22f),violet,size*.70f,life*.70f,
                HeroFxMotion.Sweep,upright*Quaternion.Euler(0,0,-38),actor);
        }
        private static void TravelerBasicProjectile(GameObject actor,Vector3 start,Vector3 end,int slot,float travel,Transform target,float strength)
        {
            Color violet=new(.64f,.28f,1f,.98f);
            Color white=new(.95f,.75f,1f,.96f);
            Quaternion face=Face(start,end);
            if(travel<=0)
            {
                Vector3 delta=end-start;
                float distance=delta.magnitude;
                if(distance<.001f)return;
                Quaternion traceFacing=Quaternion.LookRotation(delta/distance);
                // Fit the cached bolt's asymmetric -1.4..+.55 span to the actual endpoints.
                // Strength widens the core, never stretches it past the selected target.
                Vector3 center=start+delta*(1.4f/1.95f);
                float width=(slot==0?.29f:.14f)*strength;
                HeroVfxPool.Emit(HeroFxShape.Bolt,center,center,
                    new Vector3(width,width,distance/1.95f),
                    white,.14f,HeroFxMotion.Hold,traceFacing,actor);
                HeroVfxPool.Emit(HeroFxShape.Halo,(start+end)*.5f,(start+end)*.5f,
                    new Vector3(.27f*strength,.27f*strength,distance*.5f),
                    violet,.17f,HeroFxMotion.Hold,traceFacing,actor);
                TravelerPortal(actor,start,slot==0?.54f:.32f,.23f);
                return;
            }
            HeroVfxPool.Emit(HeroFxShape.Bolt,start,end,new Vector3(.29f,.29f,.98f)*strength,
                white,travel,HeroFxMotion.Travel,face,actor,endFollow:target);
            HeroVfxPool.Emit(HeroFxShape.Halo,start,end,new Vector3(.43f,.43f,1.25f)*strength,
                violet,travel,HeroFxMotion.Travel,face,actor,endFollow:target);
            TravelerPortal(actor,start,slot==0?.55f:.35f,.24f);
        }

        private static Color TravelerColor(int slot)=>slot==3?Color.white:
            slot==0||slot==1?new Color(1f,.82f,.08f,1f):new Color(.64f,.24f,1f,1f);

        // Pooled spherical cores stay visible even when decorative effects are disabled.
        private static void TravelerOrb(GameObject actor,Vector3 point,int slot,float size,float life,
            HeroFxMotion motion=HeroFxMotion.Contract,bool warning=false)
        {
            Color color=TravelerColor(slot);
            Fx(HeroFxShape.Orb,point,color,size,life,motion,actor:actor,warning:warning);
            Camera view=Camera.main;
            Fx(HeroFxShape.Halo,point,new Color(color.r,color.g,color.b,.32f),size*.70f,life,
                motion,view!=null?view.transform.rotation*Quaternion.Euler(90f,0f,0f):Quaternion.Euler(90f,0f,0f),
                actor,warning:warning,decorative:!warning);
        }
        private static void TravelerProjectile(GameObject actor,Vector3 start,Vector3 end,int slot,float travel,Transform target)
        {
            Color color=TravelerColor(slot);
            float size=slot==3?1.05f:slot<0?.30f:.42f;
            if(travel<=0)
            {
                // Instant damage stays instant: show the sphere at contact rather than a late projectile.
                TravelerOrb(actor,end,slot,size,.20f);
                TravelerOrb(actor,start,slot,size*.60f,.12f);
                return;
            }
            HeroVfxPool.Emit(HeroFxShape.Orb,start,end,Vector3.one*size,color,travel,
                HeroFxMotion.Travel,Face(start,end),actor,endFollow:target,
                cancelOnOwnerDeath:slot>=0);
            TravelerOrb(actor,start,slot,size*.55f,.12f);
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
                HeroVfxPool.Emit(HeroFxShape.Bolt,center,center,new Vector3(.27f*strength,.27f*strength,length),c,.11f,HeroFxMotion.Hold,direction,actor);
                HeroVfxPool.Emit(HeroFxShape.Bolt,center,center,new Vector3(.10f*strength,.10f*strength,length),
                    Color.Lerp(c,Color.white,.92f),.075f,HeroFxMotion.Hold,direction,actor);
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
                Vector3 arc=Vector3.Lerp(Muzzle(actor),p,.62f);
                Fx(HeroFxShape.Slash,arc,Color.Lerp(Accent(actor),Color.white,.42f),
                    .90f*strength,.22f,HeroFxMotion.Sweep,direction*Quaternion.Euler(0,0,32),actor);
                Fx(HeroFxShape.Hex,p,Accent(actor),.48f*strength,.20f,HeroFxMotion.Contract,Quaternion.Euler(25,0,0),actor);
                Fx(HeroFxShape.Cracks,Ground(impact),Accent(actor),.36f*strength,.20f,actor:actor);
            }
            else
            {
                Vector3 start=Muzzle(actor),center=Vector3.Lerp(start,p,.70f);
                HeroVfxPool.Emit(HeroFxShape.Bolt,center,center,new Vector3(.34f,.30f,.38f)*strength,
                    Accent(actor),.14f,HeroFxMotion.Contract,direction,actor);
                Fx(HeroFxShape.Slash,center,Color.Lerp(Accent(actor),Color.white,.48f),
                    .75f*strength,.20f,HeroFxMotion.Sweep,direction*Quaternion.Euler(0,0,-32),actor);
                Ring(actor,p,.42f*strength,.18f);
                // Brakk's gauntlet contact reads through a short ground fracture rather than
                // Auren's geometric shield/hex language.
                Fx(HeroFxShape.Cracks,Ground(impact),Accent(actor),.34f*strength,.18f,actor:actor);
            }
            if(slot>=0)
                Fx(tank?HeroFxShape.Shield:HeroFxShape.ShockDome,p+Vector3.up*.16f,
                    Color.Lerp(Accent(actor),Color.white,.72f),.55f*strength,.12f,
                    HeroFxMotion.Expand,actor:actor);
            Shards(actor,p,.58f*strength,2);
        }
        private static void Slash(GameObject actor,Vector3 point,int slot,bool heavy=false)
        {
            float size=slot<0?.84f:slot==3?2.05f:1.46f;Color c=Accent(actor);
            Quaternion facing=Face(actor.transform.position,point);
            // Span from the weapon side toward contact instead of drawing a detached arc behind the victim.
            Vector3 p=Vector3.Lerp(Muzzle(actor),point+Vector3.up*.16f,.62f);
            HeroVfxPool.Emit(HeroFxShape.Slash,p,p,new Vector3(size,1,size*(heavy?.75f:1)),c,heavy?.30f:.22f,HeroFxMotion.Sweep,facing,actor);
            // A second opposed edge makes the assassin's slash identifiable even in a crowded fight.
            if(slot>=0)Fx(HeroFxShape.Slash,p+Vector3.up*.10f,new Color(c.r,c.g,c.b,.82f),
                size*.78f,.16f,HeroFxMotion.Sweep,facing*Quaternion.Euler(0,180,0),actor);
            if(!heavy)Fx(HeroFxShape.Slash,p+Vector3.up*.025f,Color.Lerp(c,Color.white,.88f),size*.72f,.12f,HeroFxMotion.Sweep,facing,actor);
            if(heavy)Fx(HeroFxShape.Burst,point+Vector3.up*.15f,Color.Lerp(c,Color.white,.72f),size*.82f,.14f,rotation:facing);
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
            if(Hero(actor)==HeroId.Timmy){TimmyCast(actor,slot);return;}
            HeroId h=Hero(actor);Color c=h==HeroId.Mira&&(slot==1||slot==2||slot==3)?new Color(.24f,1f,.42f):Accent(actor);float size=slot==3?.45f:.22f;Vector3 p=Muzzle(actor);
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
                        Color light=SivraSkillColor(slot);
                        Fx(slot==2?HeroFxShape.Orb:HeroFxShape.Bolt,p,Color.Lerp(light,Color.white,.40f),size*1.28f,.10f,HeroFxMotion.Contract,facing,actor);
                        Fx(HeroFxShape.Burst,p,light,size*.86f,.09f,rotation:facing,actor:actor);
                        Fx(HeroFxShape.Ring,p,new Color(light.r,light.g,light.b,.48f),size*.72f,.12f,HeroFxMotion.Contract,facing,actor);
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
                    if(slot==1)TravelerPortal(actor,Chest(actor),.42f,.22f);
                    else TravelerOrb(actor,p,slot,slot==3?.78f:.36f,slot==3?.30f:.18f);
                    return;
                case HeroId.Nerok:
                    Fx(HeroFxShape.WaveCrest,p,Accent(actor),.75f,.22f,HeroFxMotion.Sweep,facing,actor);break;
                case HeroId.Rook:
                    {
                        Color hot=new(1f,.34f,.035f);
                        Fx(HeroFxShape.Burst,p,hot,size*1.28f,.13f,rotation:facing,actor:actor);
                        if(slot==3)
                        {
                            Vector3 center=Ground(actor.transform.position);
                            OrunOverdrive(actor,center);
                            Alterverse.Multiplayer.CoopHeroStateSync.BroadcastOrunOverdrive(actor,center);
                        }
                        else Fx(HeroFxShape.Ring,Ground(actor.transform.position),new Color(hot.r,hot.g,hot.b,.62f),size*.95f,.16f,HeroFxMotion.Expand,actor:actor);
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
            if(slot>=0)CastSignature(actor,h,p,slot,c,facing);
            if(slot==3&&AdaptiveVfxBudget.AllowDecorative)
            {
                // Two thin opposed arcs frame the release without adding another filled floor disc.
                Color rim=Color.Lerp(c,Color.white,.28f);rim.a=.56f;
                Quaternion upright=facing*Quaternion.Euler(70f,0f,0f);
                Fx(HeroFxShape.Slash,p,rim,.62f,.24f,HeroFxMotion.Sweep,upright,actor,decorative:true);
                Fx(HeroFxShape.Slash,p,c,.54f,.28f,HeroFxMotion.Sweep,
                    upright*Quaternion.Euler(0f,180f,0f),actor,delay:.035f,decorative:true);
            }
        }
        // Two reused pool meshes per skill keep every hero recognizable without extra lights or emitters.
        private static void CastSignature(GameObject actor,HeroId hero,Vector3 point,int slot,Color accent,Quaternion facing)
        {
            float size=slot==3?1.20f:.72f;
            Color core=Color.Lerp(accent,Color.white,.87f);
            HeroFxShape outer=hero==HeroId.Veyr?HeroFxShape.Slash:
                hero==HeroId.Nyra?HeroFxShape.Bolt:hero==HeroId.Kaelon?HeroFxShape.Hex:
                hero==HeroId.Rook?HeroFxShape.Burst:hero==HeroId.Elyra?HeroFxShape.Runes:
                hero==HeroId.Mira?HeroFxShape.Petals:HeroFxShape.Rift;
            HeroFxShape inner=hero==HeroId.Veyr?HeroFxShape.Rift:
                hero==HeroId.Nyra?HeroFxShape.Burst:hero==HeroId.Kaelon?HeroFxShape.Shield:
                hero==HeroId.Rook?HeroFxShape.ShockDome:hero==HeroId.Elyra?HeroFxShape.Orb:
                hero==HeroId.Mira?HeroFxShape.Plus:HeroFxShape.Halo;
            Fx(outer,point,accent,size,.24f,hero==HeroId.Elyra||hero==HeroId.Mira?
                HeroFxMotion.Orbit:HeroFxMotion.Expand,facing,actor);
            Fx(inner,point+Vector3.up*.08f,core,size*.46f,.11f,
                HeroFxMotion.Contract,facing,actor);
            if(slot==3)
                Fx(outer,point+Vector3.up*.24f,Color.Lerp(accent,Color.white,.55f),
                    size*1.38f,.28f,HeroFxMotion.Expand,facing,actor);
        }
        public static void TargetWarning(GameObject actor,Health target,float seconds)
        {
            if(actor==null||target==null)return;
            Vector3 p=Ground(target.transform.position);
            Fx(HeroFxShape.Hex,p,new Color(1,.48f,.16f),.82f,seconds,HeroFxMotion.Hold,actor:actor,follow:target.transform,warning:true);
            Fx(HeroFxShape.Rift,target.transform.position+Vector3.up*.25f,Accent(actor),.6f,seconds,HeroFxMotion.Contract,Face(actor.transform.position,target.transform.position),actor,target.transform,true);
        }
        public static void SharkAnchor(GameObject actor,Vector3 impact,float flight,Transform target=null)
        {
            Vector3 from=Muzzle(actor),to=impact+Vector3.up*.35f;
            Quaternion facing=Face(from,to);Color cyan=new Color(.04f,.70f,1f,.94f);
            HeroVfxPool.Emit(HeroFxShape.AnchorProjectile,from,to,Vector3.one*.78f,cyan,
                flight,HeroFxMotion.TravelFixedFrame,facing,actor,endFollow:target);
            float distance=Vector3.Distance(from,to);
            HeroVfxPool.Emit(HeroFxShape.Streak,to,to,new Vector3(.13f,.13f,distance/1.4f),
                new Color(.34f,.77f,.95f,.65f),flight+.15f,HeroFxMotion.Hold,facing,actor);
            Fx(HeroFxShape.WaveCrest,from,cyan,.32f,.18f,HeroFxMotion.Expand,facing,actor);
        }
        public static void SharkPullChain(GameObject actor,Health target,float duration)
        {
            if(actor==null||target==null||target.IsDead)return;
            Vector3 from=Muzzle(actor),to=target.transform.position+Vector3.up*.4f;
            HeroVfxPool.Emit(HeroFxShape.AnchorChain,from,to,new Vector3(.9f,.9f,1f),
                new Color(.45f,.80f,1f,1f),duration,HeroFxMotion.Tether,
                owner:actor,follow:actor.transform,endFollow:target.transform);
        }
        public static void SharkVortex(GameObject actor,Vector3 center,float radius,float duration)
        {
            Vector3 p=Ground(center);Color water=new Color(.02f,.48f,.95f,.65f);
            Fx(HeroFxShape.WaterSpiral,p,water,radius,duration,HeroFxMotion.Orbit,actor:actor);
            Fx(HeroFxShape.WaterSpiral,p+Vector3.up*.075f,new Color(.2f,.85f,1f,.42f),radius*.76f,
                duration,HeroFxMotion.Orbit,Quaternion.Euler(0,60,0),actor);
            // A clear boundary and low crests make the real area readable without hiding units.
            Fx(HeroFxShape.Ring,p,new Color(.16f,.70f,1f,.40f),radius,duration,HeroFxMotion.Hold,actor:actor);
            for(int i=0;i<3;i++)
                Fx(HeroFxShape.WaveCrest,p,water,radius*.9f,duration,HeroFxMotion.Orbit,
                    Quaternion.Euler(0,i*120,0),actor);
            for(int i=0;i<6;i++)
            {
                float angle=i*Mathf.PI/3;Vector3 offset=new Vector3(Mathf.Cos(angle),.22f,Mathf.Sin(angle))*radius*.60f;
                HeroVfxPool.Emit(HeroFxShape.Orb,p+offset,p+Vector3.up*.38f,Vector3.one*.075f,
                    new Color(.55f,.9f,1f,.65f),duration*.55f,HeroFxMotion.Spiral,owner:actor,
                    delay:i*.12f,decorative:true);
            }
        }
        public static void Area(GameObject actor,Vector3 center,float radius,float duration,bool warning,bool beneficial=false,int slot=-1)
        {
            if(actor==null)return;
            HeroId hero=Hero(actor);
            if(hero==HeroId.Timmy&&slot==1)
            {
                TimmyEffect(actor,warning?0:1,center,center,radius,warning?duration:TimmyBalance.DustVfxSeconds);
                return;
            }
            Color accent=hero==HeroId.Mira&&beneficial?new Color(.24f,1f,.42f):Accent(actor);
            Vector3 ground=Ground(center);
            if(hero==HeroId.Nerok&&!warning)
            {
                Fx(HeroFxShape.WaveCrest,ground,accent,radius,.4f,HeroFxMotion.Expand,
                    Face(actor.transform.position,center),actor);
                Ring(actor,ground,radius,.32f);
                Alterverse.Multiplayer.CoopHeroStateSync.BroadcastSharkEffect(actor,5,center,radius,.4f);
                return;
            }
            if(warning)
            {
                // Keep the real hit radius legible. Decorative rings must never imply another hit zone.
                Color boundary=beneficial?accent:new Color(1f,.48f,.16f);
                boundary.a=.70f;
                Fx(HeroFxShape.Ring,ground,boundary,radius,Mathf.Max(.06f,duration),
                    HeroFxMotion.Hold,actor:actor,warning:true);
                if(hero==HeroId.Kaelon&&slot==3)
                {
                    // Hollow yellow pulse travels from Auren to the exact Kest Citadel boundary.
                    // RadialPulse keeps the supplied gameplay radius instead of the decorative 1.5x scale.
                    HeroVfxPool.Emit(HeroFxShape.PressureWave,ground,ground,Vector3.one*radius,
                        new Color(1f,.84f,.06f,.95f),Mathf.Max(.06f,duration),
                        HeroFxMotion.RadialPulse,owner:actor,warning:true,cancelOnOwnerDeath:true);
                }
                // Traveler's sphere is launched by Spell and reaches this fixed warning center at resolution.
                if(hero==HeroId.Traveler)return;
                // A small elevated emblem identifies the caster while the boundary alone marks damage radius.
                HeroFxShape emblem=hero==HeroId.Elyra?HeroFxShape.Runes:
                    hero==HeroId.Mira?HeroFxShape.Petals:hero==HeroId.Kaelon?HeroFxShape.Hex:
                    hero==HeroId.Rook?HeroFxShape.Shard:hero==HeroId.Traveler?HeroFxShape.Rift:HeroFxShape.Burst;
                Fx(emblem,center+Vector3.up*.55f,accent,.43f,
                    Mathf.Min(.42f,Mathf.Max(.18f,duration)),HeroFxMotion.Contract,actor:actor);
                if(hero==HeroId.Elyra&&slot==1)
                {
                    // Invert Field gets a visible vertical lens during its warning, not only a floor ring.
                    Quaternion plane=Quaternion.LookRotation(actor.transform.forward)*Quaternion.Euler(90f,0f,0f);
                    Color inverse=Color.Lerp(accent,new Color(.53f,.22f,1f,1f),.42f);
                    Fx(HeroFxShape.Rift,center+Vector3.up*.48f,DarkCore,.62f,Mathf.Max(.18f,duration),
                        HeroFxMotion.Contract,actor:actor,warning:true);
                    Fx(HeroFxShape.Halo,center+Vector3.up*.50f,inverse,.92f,Mathf.Max(.18f,duration),
                        HeroFxMotion.Orbit,plane,actor,warning:true);
                }
                if(hero==HeroId.Elyra&&slot==3)
                {
                    // The falling meteor is the unique cast cue, above the ground warning.
                    Vector3 sky=ground+Vector3.up*7.5f;
                    HeroVfxPool.Emit(HeroFxShape.Orb,sky,ground+Vector3.up*.34f,
                        new Vector3(.72f,.72f,.72f),new Color(1f,.34f,.10f,.84f),
                        Mathf.Max(.06f,duration),HeroFxMotion.Travel,Quaternion.identity,actor,true);
                }
                return;
            }

            // The gameplay warning has ended. A compact contact accent replaces the old full-radius
            // stack of rings, runes, cracks and shock domes that covered the battlefield.
            float size=Mathf.Min(radius*.44f,slot==3?1.48f:.95f);
            float life=slot==3?.34f:.22f;
            Vector3 contact=ground+Vector3.up*.12f;
            switch(hero)
            {
                case HeroId.Elyra:
                    if(slot==1)
                    {
                        // Invert Field resolves as a brief spatial inversion: a wide floor pulse,
                        // rotating rune plane and a contracting lens above the contact point.
                        Color inverse=Color.Lerp(accent,new Color(.53f,.22f,1f,1f),.42f);
                        Quaternion plane=Quaternion.LookRotation(actor.transform.forward)*Quaternion.Euler(90f,0f,0f);
                        Fx(HeroFxShape.Ring,ground+Vector3.up*.025f,inverse,radius*1.12f,.42f,
                            HeroFxMotion.Expand,actor:actor);
                        Fx(HeroFxShape.Runes,ground+Vector3.up*.045f,Color.Lerp(inverse,Color.white,.38f),radius*.82f,.40f,
                            HeroFxMotion.Orbit,plane,actor);
                        Fx(HeroFxShape.Rift,contact+Vector3.up*.58f,DarkCore,.92f,.40f,
                            HeroFxMotion.Contract,actor:actor);
                        Fx(HeroFxShape.Halo,contact+Vector3.up*.58f,inverse,1.12f,.36f,
                            HeroFxMotion.Expand,plane,actor);
                        Shards(actor,contact+Vector3.up*.14f,radius*.48f,5);
                    }
                    else
                    {
                        Fx(HeroFxShape.Runes,contact+Vector3.up*.24f,accent,size,life,HeroFxMotion.Contract,actor:actor);
                        if(slot==3)
                        {
                            Fx(HeroFxShape.Orb,contact+Vector3.up*.58f,DarkCore,.88f,.32f,HeroFxMotion.Contract,actor:actor);
                            Fx(HeroFxShape.Halo,contact+Vector3.up*.58f,Color.Lerp(accent,Color.white,.52f),.82f,.28f,HeroFxMotion.Contract,actor:actor);
                        }
                    }
                    break;
                case HeroId.Mira:
                    Fx(HeroFxShape.Petals,contact+Vector3.up*.25f,accent,size,life,HeroFxMotion.Expand,actor:actor);
                    if(slot==3)
                    {
                        Fx(HeroFxShape.Plus,contact+Vector3.up*.75f,Color.Lerp(accent,Color.white,.70f),
                            .92f,.34f,HeroFxMotion.Contract,actor:actor);
                        Fx(HeroFxShape.Halo,contact+Vector3.up*.62f,accent,.87f,.34f,HeroFxMotion.Contract,actor:actor);
                    }
                    break;
                case HeroId.Kaelon:
                    Fx(HeroFxShape.Hex,contact+Vector3.up*.18f,accent,size,life,HeroFxMotion.Contract,actor:actor);
                    if(slot==3)Fx(HeroFxShape.Shield,contact+Vector3.up*.70f,
                        Color.Lerp(accent,Color.white,.48f),.86f,.31f,HeroFxMotion.Contract,actor:actor);
                    break;
                case HeroId.Rook:
                    Fx(HeroFxShape.Cracks,ground+Vector3.up*.015f,accent,size,life,HeroFxMotion.Expand,actor:actor);
                    if(slot==3)Fx(HeroFxShape.Burst,contact+Vector3.up*.45f,
                        new Color(1f,.38f,.06f),.84f,.27f,HeroFxMotion.Contract,actor:actor);
                    break;
                case HeroId.Traveler:
                    TravelerOrb(actor,contact+Vector3.up*.56f,slot,slot==3?1.35f:.58f,
                        slot==3?.40f:.28f,HeroFxMotion.Expand);
                    return;
                default:
                    Fx(HeroFxShape.Burst,contact,accent,size,life,HeroFxMotion.Expand,actor:actor);
                    break;
            }
            if(slot>=0)AreaSignature(actor,hero,contact,Mathf.Min(radius*.56f,slot==3?2.2f:1.45f),slot,accent);
        }
        private static void AreaSignature(GameObject actor,HeroId hero,Vector3 contact,float size,int slot,Color accent)
        {
            // The exact-radius warning has already ended. These compact motifs stay at the contact center.
            HeroFxShape motif=hero==HeroId.Veyr?HeroFxShape.Slash:
                hero==HeroId.Kaelon?HeroFxShape.Hex:hero==HeroId.Nyra?HeroFxShape.Bolt:
                hero==HeroId.Elyra?HeroFxShape.Runes:hero==HeroId.Mira?HeroFxShape.Petals:
                hero==HeroId.Rook?HeroFxShape.Cracks:HeroFxShape.Rift;
            Color rim=new Color(accent.r,accent.g,accent.b,.90f);
            Fx(motif,contact+Vector3.up*.27f,rim,size,slot==3?.32f:.23f,
                HeroFxMotion.Expand,actor:actor);
            Fx(hero==HeroId.Veyr?HeroFxShape.Rift:hero==HeroId.Kaelon?HeroFxShape.Shield:
                hero==HeroId.Nyra?HeroFxShape.Burst:hero==HeroId.Elyra?HeroFxShape.Orb:
                hero==HeroId.Mira?HeroFxShape.Plus:hero==HeroId.Rook?HeroFxShape.Burst:HeroFxShape.Rift,
                contact+Vector3.up*.42f,Color.Lerp(accent,Color.white,.89f),size*.42f,.10f,
                HeroFxMotion.Contract,actor:actor);
        }
        private static void RiftScarProjectile(GameObject actor,Vector3 impact,float travelTime,Transform target)
        {
            Vector3 start=Muzzle(actor),end=impact+Vector3.up*.65f;
            float flight=travelTime>0?travelTime:Mathf.Clamp(Vector3.Distance(start,end)/16f,.16f,.28f);
            Camera camera=Camera.main;
            Quaternion facing=camera!=null?camera.transform.rotation:Face(start,end);
            Color violet=new Color(.66f,.16f,1f,1f);
            // Presentation only: existing range, damage and hit timing stay unchanged.
            HeroVfxPool.Emit(HeroFxShape.RiftCross,start,end,new Vector3(.88f,.88f,1),
                violet,flight,HeroFxMotion.TravelFixedFrame,facing,actor,
                endFollow:travelTime>0?target:null);
        }
        private static void VacantEchoSickle(GameObject actor,Vector3 impact)
        {
            HeroAttackDelivery delivery=actor.GetComponent<HeroAttackDelivery>();
            Vector3 origin=delivery?.VisualOrigin??actor.transform.position;
            Vector3 direction=delivery?.VisualDirection??(impact-origin);
            direction.y=0f;
            if(direction.sqrMagnitude<.0001f)direction=actor.transform.forward;
            direction.y=0f;
            if(direction.sqrMagnitude<.0001f)direction=Vector3.forward;
            direction.Normalize();
            float range=AimingSettings.Current.For(HeroId.Veyr,3).castRange;
            if(range<=0f)return;
            Vector3 start=Muzzle(actor),end=origin+direction*range;
            end.y=start.y;
            float flight=Mathf.Max(.16f,Vector3.Distance(start,end)/18f);
            // Presentation only: fixed aim and full range, even when melee contact is closer.
            // No target following or new hitbox; the existing Vacant Echo hit still resolves immediately.
            HeroVfxPool.Emit(HeroFxShape.EchoSickle,start,end,new Vector3(1.12f,.12f,1.12f),
                new Color(.72f,.12f,1.35f,.55f),flight,HeroFxMotion.TravelFixedFrame,
                Quaternion.LookRotation(direction),actor,cancelOnOwnerDeath:true);
        }
        public static void Spell(GameObject actor,Vector3 impact,int slot,float travelTime=0,Transform target=null)
        {
            if(actor==null)return;HeroId h=Hero(actor);
            if(h==HeroId.Timmy)
            {
                if(slot==0||slot==3)TimmyClaws(actor,impact,slot);
                else if(slot==1&&travelTime>0)TimmyEffect(actor,5,Muzzle(actor),impact,0,travelTime);
                return;
            }
            switch(h)
            {
                case HeroId.Veyr:
                    if(slot==1)RiftScarProjectile(actor,impact,travelTime,target);
                    else if(slot==3)
                    {
                        VacantEchoSickle(actor,impact);
                        Quaternion direction=Face(actor.transform.position,impact);
                        Fx(HeroFxShape.Slash,impact+Vector3.up*.25f,Accent(actor),1.5f,.18f,HeroFxMotion.Sweep,direction*Quaternion.Euler(45,0,25),actor);
                        Fx(HeroFxShape.Slash,impact+Vector3.up*.25f,Color.Lerp(Accent(actor),Color.white,.6f),1.35f,.16f,HeroFxMotion.Sweep,direction*Quaternion.Euler(-45,180,-25),actor);
                        Rift(actor,impact,.85f,.32f);
                    }
                    else Slash(actor,impact,slot);break;
                case HeroId.Kaelon:
                    if(slot==0)
                    {
                        Vector3 direction=impact-actor.transform.position;direction.y=0f;
                        if(direction.sqrMagnitude<.0001f)direction=actor.transform.forward;
                        direction.y=0f;direction.Normalize();
                        float range=HeroAbilityKit.LoadoutFor(HeroId.Kaelon).Q.Range;
                        Vector3 start=Muzzle(actor),end=actor.transform.position+direction*range;
                        if (actor.GetComponent<HeroAttackDelivery>()?.VisualOrigin != null) end = impact;
                        end.y=start.y;
                        Camera view=Camera.main;
                        Quaternion facing=view!=null?view.transform.rotation:Face(start,end);
                        float flight=travelTime>0?travelTime:Mathf.Max(.16f,Vector3.Distance(start,end)/14f);
                        // Visual punch pulse continues to the true range limit even when the victim is closer.
                        HeroVfxPool.Emit(HeroFxShape.AnchorStrikeZero,start,end,Vector3.one*.86f,
                            new Color(.68f,.20f,1f,1f),flight,HeroFxMotion.TravelFixedFrame,
                            facing,actor,cancelOnOwnerDeath:true);
                    }
                    else if(slot==2)
                    {
                        Vector3 start=Muzzle(actor),end=impact+Vector3.up*.55f;
                        Camera view=Camera.main;
                        Quaternion facing=view!=null?view.transform.rotation:Face(start,end);
                        // Flight ends at the fixed Gravity Anchor warning center when its windup resolves.
                        float flight=travelTime>0?travelTime:HeroAbilityKit.LoadoutFor(HeroId.Kaelon).E.Windup;
                        HeroVfxPool.Emit(HeroFxShape.GravityEight,start,end,Vector3.one*.80f,
                            new Color(.08f,.46f,1f,1f),flight,HeroFxMotion.TravelFixedFrame,
                            facing,actor,cancelOnOwnerDeath:true);
                    }
                    break;
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
                    else if(slot==2||slot==3)
                        TravelerProjectile(actor,Muzzle(actor),impact+Vector3.up*.68f,slot,
                            travelTime,null);
                    break;
            }
        }
        public static void OrunOverdrive(GameObject actor,Vector3 center)
        {
            if(actor==null||Hero(actor)!=HeroId.Rook)return;
            AbilitySpec ultimate=HeroAbilityKit.LoadoutFor(HeroId.Rook).Ultimate;
            HeroVfxPool.Emit(HeroFxShape.PressureWave,Ground(center),Ground(center),Vector3.one*ultimate.Radius,
                new Color(1f,.34f,.035f,.92f),Mathf.Max(.32f,ultimate.Windup),HeroFxMotion.RadialPulse,
                owner:actor,cancelOnOwnerDeath:true);
        }

        public static void Ultimate(GameObject actor,Vector3 target)
        {if(actor!=null)Cast(actor,3);}
        // A short second beat distinguishes metal, shadow, magic and sacred energy.
        // These cached meshes are optional: contact cores and danger markers keep priority.
        private static void ImpactAftermath(GameObject actor,Vector3 point,HeroId hero,int slot,float strength)
        {
            if(!AdaptiveVfxBudget.AllowDecorative)return;
            Color tint=hero==HeroId.Rook?new Color(1f,.34f,.035f,.80f):
                hero==HeroId.Nyra?(slot==0||slot==2?SivraSkillColor(slot):new Color(1f,.08f,.03f,.80f)):Accent(actor);
            tint.a=.68f;
            Quaternion face=Face(actor.transform.position,point);
            if(hero==HeroId.Mira||hero==HeroId.Elyra||hero==HeroId.Traveler)
            {
                HeroFxShape motif=hero==HeroId.Mira?HeroFxShape.Petals:
                    hero==HeroId.Traveler?HeroFxShape.Slash:HeroFxShape.Runes;
                Fx(motif,point,tint,strength*1.25f,.30f,HeroFxMotion.Orbit,
                    face*Quaternion.Euler(65f,0f,25f),actor,delay:.035f,decorative:true);
                HeroVfxPool.Emit(hero==HeroId.Mira?HeroFxShape.Seed:HeroFxShape.Shard,
                    point,point+Vector3.up*(.32f+strength*.30f),
                    new Vector3(.12f,.20f,.12f)*strength,Color.Lerp(tint,Color.white,.35f),
                    .32f,HeroFxMotion.Spiral,face,actor,delay:.035f,decorative:true);
                return;
            }
            int count=AdaptiveVfxBudget.Count(slot==3?5:slot<0?2:3);
            for(int i=0;i<count;i++)
            {
                float fan=count>1?Mathf.Lerp(-65f,65f,(float)i/(count-1)):0f;
                Vector3 direction=face*Quaternion.Euler(0f,fan,0f)*Vector3.forward;
                Vector3 end=point+direction*(.28f+strength*(.45f+i*.10f))-Vector3.up*.06f;
                Vector3 velocity=end-point;
                HeroVfxPool.Emit(HeroFxShape.Spark,point,end,new Vector3(.65f,.65f,.36f+i*.07f)*strength,
                    Color.Lerp(tint,Color.white,.32f),.22f+i*.025f,HeroFxMotion.Drift,
                    velocity.sqrMagnitude>.0001f?Quaternion.LookRotation(velocity):face,
                    actor,delay:i*.012f,decorative:true);
            }
            if(slot>=0)
                Fx(hero==HeroId.Kaelon?HeroFxShape.Hex:HeroFxShape.Slash,point,tint,
                    strength*.82f,.25f,HeroFxMotion.Expand,face*Quaternion.Euler(70f,0f,0f),
                    actor,delay:.045f,decorative:true);
        }

        public static void Impact(GameObject actor,Health target,int slot)
        {
            if(actor==null||target==null)return;
            Vector3 p=target.transform.position+Vector3.up*.35f;Color c=Accent(actor);
            float strength=slot<0?.35f:slot==3?.90f:.62f;
            HeroId hero=Hero(actor);
            if(hero==HeroId.Nyra&&(slot==0||slot==2))c=SivraSkillColor(slot);
            if(hero==HeroId.Traveler&&slot>=0)
            {
                // Area abilities already own one central sphere; each victim gets only a small contact flash.
                TravelerOrb(actor,p,slot,slot==3?.42f:slot<0?.28f:.34f,.16f);
                return;
            }
            // One camera-facing flash identifies the authoritative hit, followed by the class silhouette.
            Camera view=Camera.main;
            Quaternion contactFacing=view!=null?view.transform.rotation:Quaternion.Euler(55f,0f,0f);
            Fx(HeroFxShape.ContactStar,
                p,Color.Lerp(c,Color.white,.91f),strength*(slot<0?.95f:1.30f),.085f,
                HeroFxMotion.Contract,contactFacing,actor);

            switch(hero)
            {
                case HeroId.Veyr:
                    Fx(HeroFxShape.ArcRibbon,p,c,strength*2,.16f,HeroFxMotion.Sweep,Face(actor.transform.position,p)*Quaternion.Euler(25,0,30));
                    if(slot<0)Fx(HeroFxShape.Rift,p,DarkCore,strength*1.15f,.12f,HeroFxMotion.Contract,Face(actor.transform.position,p),actor);
                    break;
                case HeroId.Kaelon:
                    Fx(HeroFxShape.Hex,p,c,strength*1.7f,.19f,rotation:Quaternion.Euler(30,0,0));
                    if(slot<0)Fx(HeroFxShape.Cracks,Ground(target.transform.position),c,strength*1.35f,.18f,actor:actor);
                    break;
                case HeroId.Nyra:
                    {
                        Color light=SivraSkillColor(slot);
                        Fx(HeroFxShape.Burst,p,light,strength*2.15f,.16f,rotation:Face(actor.transform.position,p));
                        Fx(HeroFxShape.Halo,p,Color.Lerp(light,Color.white,.35f),strength*.92f,.17f,HeroFxMotion.Contract,actor:actor);
                        if(slot==2)Fx(HeroFxShape.Runes,p,light,strength*1.5f,.24f,HeroFxMotion.Contract,actor:actor);
                        if(slot==3)Fx(HeroFxShape.Shard,p+Vector3.up*.20f,
                            Color.Lerp(light,Color.white,.62f),strength*1.7f,.18f,HeroFxMotion.Contract,actor:actor);
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
                case HeroId.Traveler:
                    {
                        Color light=Color.Lerp(c,Color.white,.70f);
                        Color red=new(.74f,.30f,1f,.86f);
                        Fx(HeroFxShape.Halo,p,light,strength*1.55f,.13f,HeroFxMotion.Contract,actor:actor);
                        Fx(HeroFxShape.Ring,p,c,strength*1.35f,.16f,HeroFxMotion.Contract,Face(actor.transform.position,p)*Quaternion.Euler(90,0,0),actor);
                        Fx(HeroFxShape.Rift,p,DarkCore,strength*.85f,.13f,HeroFxMotion.Contract,Face(actor.transform.position,p),actor);
                        if(slot>=0)Fx(HeroFxShape.Shard,p,red,strength*.65f,.12f,HeroFxMotion.Contract,actor:actor);
                        break;
                    }
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
            if(slot>=0)SkillImpactSignature(actor,hero,p,c,strength,slot);
            ImpactAftermath(actor,p,hero,slot,strength);
            if(Hero(actor)==HeroId.Nyra&&slot==2)
            {
                Fx(HeroFxShape.Ring,p,Color.white,.67f,.5f,HeroFxMotion.Contract,Quaternion.Euler(65,0,0),actor,target.transform);
                Fx(HeroFxShape.Ring,p,Color.white,.70f,.5f,HeroFxMotion.Contract,Quaternion.Euler(65,70,0),actor,target.transform);
            }
            else if(slot>=0&&Hero(actor)==HeroId.Kaelon)
                Fx(HeroFxShape.Hex,p+Vector3.up*.6f,c,.34f,.45f,HeroFxMotion.Orbit,actor:actor,follow:target.transform);
        }
        private static void SkillImpactSignature(GameObject actor,HeroId hero,Vector3 point,Color accent,float strength,int slot)
        {
            // Short class silhouette around the contact core, stronger for an ultimate.
            float size=strength*(slot==3?2.05f:1.45f);
            HeroFxShape shape=hero==HeroId.Veyr?HeroFxShape.Slash:
                hero==HeroId.Kaelon?HeroFxShape.Shield:hero==HeroId.Nyra?(slot==2?HeroFxShape.Orb:HeroFxShape.Bolt):
                hero==HeroId.Elyra?HeroFxShape.Runes:hero==HeroId.Mira?HeroFxShape.Petals:
                hero==HeroId.Rook?HeroFxShape.ShockDome:HeroFxShape.Rift;
            Fx(shape,point+Vector3.up*.10f,new Color(accent.r,accent.g,accent.b,.84f),
                size,slot==3?.27f:.19f,HeroFxMotion.Expand,
                Face(actor.transform.position,point),actor);
            if(slot==3)Fx(shape,point+Vector3.up*.25f,Color.Lerp(accent,Color.white,.77f),
                size*.72f,.12f,HeroFxMotion.Contract,actor:actor);
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
            Color c=hero==HeroId.Mira?new Color(.22f,1f,.52f):Accent(actor);
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
                Color heal=hero==HeroId.Mira?new Color(.22f,1f,.52f):new Color(.22f,1,.52f);
                if(hero==HeroId.Mira)
                {
                    for(int i=0;i<3;i++)
                    {
                        float angle=i*Mathf.PI*2f/3f;
                        Vector3 offset=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*.26f;
                        Vector3 start=p+offset-Vector3.up*.18f;
                        HeroVfxPool.Emit(HeroFxShape.Leaf,start,start+Vector3.up*.75f+offset*.35f,
                            Vector3.one*.28f,heal,.46f,HeroFxMotion.Drift,
                            Camera.main!=null?Camera.main.transform.rotation:Quaternion.identity,actor,
                            delay:i*.035f,decorative:true);
                    }
                }
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
            Color link=Hero(actor)==HeroId.Mira?new Color(.22f,1f,.52f):new Color(.25f,1,.55f);
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
            if(Hero(actor)==HeroId.Timmy){TimmyClaws(actor,impact,-1);return;}
            switch(Hero(actor))
            {
                case HeroId.Veyr:Slash(actor,impact,-1);break;
                case HeroId.Kaelon:
                case HeroId.Nerok:
                case HeroId.Rook:HeavyContact(actor,impact,-1);break;
                default:Projectile(actor,impact,-1,travelTime,target);break;
            }
        }
        public static void Link(GameObject actor,Vector3 impact,bool ultimate=false)
        {Basic(actor,impact);if(ultimate)Spell(actor,impact,3);}
        public static void Dash(GameObject actor,Vector3 from,Vector3 to,int slot=2,float duration=.16f)
        {
            if(actor==null)return;Color c=Accent(actor);HeroId h=Hero(actor);
            if(h==HeroId.Timmy){TimmyEffect(actor,slot==3?3:2,from,to,0,duration);return;}
            Quaternion face=Face(from,to);float distance=Vector3.Distance(from,to);
            if(h==HeroId.Traveler)
            {
                TravelerPortal(actor,from+Vector3.up*.55f,.50f,.26f);
                TravelerPortal(actor,to+Vector3.up*.55f,.58f,.28f,true);
                actor.GetComponent<HeroVisualMotion>()?.PulseDash();
                return;
            }
            if(h==HeroId.Veyr)
            {
                Rift(actor,from,.62f,.23f);
                Fx(HeroFxShape.Slash,to+Vector3.up*.4f,c,.84f,.23f,HeroFxMotion.Sweep,face,actor);
            }
            else if(h==HeroId.Elyra)
            {
                Fx(HeroFxShape.Runes,from+Vector3.up*.4f,c,.78f,.24f,HeroFxMotion.Contract,face,actor);
                Fx(HeroFxShape.Orb,to+Vector3.up*.45f,DarkCore,.63f,.24f,HeroFxMotion.Contract,face,actor);
            }
            else if(h==HeroId.Nyra)
            {
                Fx(HeroFxShape.Burst,from+Vector3.up*.42f,new Color(1f,.045f,.025f),.62f,.16f,rotation:face,actor:actor);
                Fx(HeroFxShape.Bolt,to+Vector3.up*.42f,Color.Lerp(c,Color.white,.65f),.48f,.16f,HeroFxMotion.Contract,face,actor);
            }
            for(int i=0;i<3;i++)
            {
                Vector3 p=Vector3.Lerp(from,to,(i+.5f)/3)+Vector3.up*.30f;
                HeroVfxPool.Emit(h==HeroId.Nyra?HeroFxShape.Bolt:h==HeroId.Elyra?HeroFxShape.Runes:HeroFxShape.Rift,
                    p,p,new Vector3(.26f,.58f,Mathf.Min(.95f,distance*.30f)),
                    new Color(c.r,c.g,c.b,.72f),.18f,HeroFxMotion.Contract,face,actor,delay:i*.035f);
            }
            actor.GetComponent<HeroVisualMotion>()?.PulseDash();
        }
    }
}
