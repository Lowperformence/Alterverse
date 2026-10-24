using Alterverse.Characters;
using Alterverse.Combat;
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

        private static Vector3 Muzzle(GameObject actor)
        {
            HeroVisualMotion motion=actor.GetComponent<HeroVisualMotion>();
            Vector3 muzzle = motion!=null?motion.ReleasePoint:Chest(actor)+actor.transform.forward*.5f;
            Vector3? origin = actor.GetComponent<HeroAttackDelivery>()?.VisualOrigin;
            Vector3? networkOrigin=HeroVfxEvents.ReplayOrigin;
            if(networkOrigin.HasValue)return new Vector3(networkOrigin.Value.x,muzzle.y,networkOrigin.Value.z);
            return origin.HasValue ? new Vector3(origin.Value.x,muzzle.y,origin.Value.z) : muzzle;
        }
        private static void Projectile(GameObject actor,Vector3 end,int slot,float travel=0,Transform target=null)
        {
            HeroId h=Hero(actor);Vector3 start=Muzzle(actor);end+=Vector3.up*.25f;
            if(h==HeroId.Nyra&&slot==0){PrismLaser(actor,start,end,travel,target);return;}
            if(h==HeroId.Traveler){TravelerProjectile(actor,start,end,slot,travel,target);return;}
            Color c=AttackColour(actor,slot);float size=slot==3?.62f:slot<0?.32f:.44f;
            if(h==HeroId.Nyra&&slot==2){c=Color.white;size=.34f;}
            if(h==HeroId.Nyra&&slot==3){c.a=.70f;size=.62f*.70f;}
            if(travel<=0){InstantTrace(actor,start,end,slot,1f);return;}
            HeroVfxPool.Emit(h==HeroId.Mira?HeroFxShape.Seed:HeroFxShape.Orb,start,end,Vector3.one*size,c,
                travel,HeroFxMotion.Travel,Face(start,end),actor,endFollow:target);
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

            Fx(HeroFxShape.Burst,start,SivraSkillColor(0),.18f,.09f,actor:actor);
        }
        // Vertical portals and light arcs stay above the battlefield rather than covering the floor.
        private static void TravelerPortal(GameObject actor,Vector3 center,float size,float life,bool arrival=false)
        {
            Fx(HeroFxShape.Rift,center,TravelerColor(1),size,life,HeroFxMotion.Hold,
                Face(actor.transform.position,center),actor);
        }


        private static Color TravelerColor(int slot)=>slot==3?Color.white:
            slot==0||slot==1?new Color(1f,.82f,.08f,1f):new Color(.64f,.24f,1f,1f);

        // Pooled spherical cores stay visible even when decorative effects are disabled.
        private static void TravelerOrb(GameObject actor,Vector3 point,int slot,float size,float life,
            HeroFxMotion motion=HeroFxMotion.Contract,bool warning=false)
        {
            Fx(HeroFxShape.Orb,point,TravelerColor(slot),size,life,motion,actor:actor,warning:warning);
        }
        private static void TravelerProjectile(GameObject actor,Vector3 start,Vector3 end,int slot,float travel,Transform target)
        {
            Color color=slot<0?new Color(.64f,.28f,1f,.98f):TravelerColor(slot);
            float size=slot==3?1.05f:slot<0?.30f:.42f;
            if(travel<=0){InstantTrace(actor,start,end,slot,1f);return;}
            HeroVfxPool.Emit(HeroFxShape.Orb,start,end,Vector3.one*size,color,travel,
                HeroFxMotion.Travel,Face(start,end),actor,endFollow:target,cancelOnOwnerDeath:slot>=0);
        }

        // Immediate gameplay hits need immediate visual contact, not a projectile arriving later.
        private static void InstantTrace(GameObject actor,Vector3 start,Vector3 end,int slot,float strength)
        {
            Vector3 delta=end-start;if(delta.sqrMagnitude<.0001f)return;
            // Exact existing -1.4..+.55 beam span: visual width changes, contact does not.
            Vector3 center=start+delta*(1.4f/1.95f);
            HeroVfxPool.Emit(HeroFxShape.Bolt,center,center,new Vector3(.14f,.14f,delta.magnitude/1.95f),
                AttackColour(actor,slot),.11f,HeroFxMotion.Hold,Quaternion.LookRotation(delta),actor);
        }
        private static void HeavyContact(GameObject actor,Vector3 impact,int slot)
        {
            Fx(HeroFxShape.AnchorStrikeZero,impact+Vector3.up*.25f,AttackColour(actor,slot),
                slot<0?.40f:.65f,.20f,HeroFxMotion.Hold,Face(actor.transform.position,impact),actor);
        }
        private static void Slash(GameObject actor,Vector3 point,int slot,bool heavy=false)
        {
            Fx(HeroFxShape.Slash,Vector3.Lerp(Muzzle(actor),point+Vector3.up*.25f,.6f),
                AttackColour(actor,slot),heavy?.95f:.65f,heavy?.30f:.22f,HeroFxMotion.Hold,
                Face(actor.transform.position,point),actor);
        }
        private static void Rift(GameObject actor,Vector3 p,float size,float life)
        {
            Fx(HeroFxShape.Rift,p,Accent(actor),size,life,HeroFxMotion.Hold,
                Face(actor.transform.position,p),actor);
        }
        public static void CancelWarnings(GameObject actor)
        {
            if(Hero(actor)==HeroId.Nerok){using var original=HeroVfxPool.PreserveNerokOriginal();NerokOriginalCancelWarnings(actor);return;}using var cue=HeroVfxEvents.Begin(HeroVfxCue.CancelWarnings,actor);HeroVfxPool.CancelWarnings(actor);}
        public static void CancelProjectiles(GameObject actor)
        {
            if(Hero(actor)==HeroId.Nerok){using var original=HeroVfxPool.PreserveNerokOriginal();NerokOriginalCancelProjectiles(actor);return;}using var cue=HeroVfxEvents.Begin(HeroVfxCue.CancelProjectiles,actor);HeroVfxPool.CancelProjectiles(actor);}
        public static void Cast(GameObject actor,int slot,float windup=-1f)
        {
            if(Hero(actor)==HeroId.Nerok){using var original=HeroVfxPool.PreserveNerokOriginal();NerokOriginalCast(actor,slot,windup);return;}
            if(windup<0&&actor!=null&&slot>=0&&slot<4)
            {
                var loadout=HeroAbilityKit.LoadoutFor(Hero(actor));
                AbilitySpec spec=actor.GetComponent<HeroAbilityKit>()?.Spec(slot)??
                    (slot switch {0=>loadout.Q,1=>loadout.W,2=>loadout.E,_=>loadout.Ultimate});
                windup=HeroAttackDelivery.AbilityWindup(Hero(actor),slot,spec);
            }
            using var cue=HeroVfxEvents.Begin(HeroVfxCue.Cast,actor,slot:slot,duration:Mathf.Max(0,windup));
            if(actor==null)return;
            HeroId h=Hero(actor);
            if(h==HeroId.Rook&&slot==3)
            {
                Vector3 center=Ground(actor.transform.position);OrunOverdrive(actor,center);
                Alterverse.Multiplayer.CoopHeroStateSync.BroadcastOrunOverdrive(actor,center);
                return;
            }
            bool defensive=slot==1&&(h==HeroId.Kaelon||h==HeroId.Nerok||h==HeroId.VoidKnight||h==HeroId.Rook)||h==HeroId.Mira&&slot==2;
            HeroFxShape shape=defensive?HeroFxShape.Runes:h==HeroId.Mira&&slot>0?HeroFxShape.Plus:
                h==HeroId.Elyra||h==HeroId.Traveler?HeroFxShape.Runes:h==HeroId.Veyr?HeroFxShape.Rift:HeroFxShape.Burst;
            Color tint=h==HeroId.Mira&&slot>0?new Color(.22f,1f,.52f):AttackColour(actor,slot);
            Fx(shape,Muzzle(actor),tint,slot==3?.48f:.28f,Mathf.Max(.06f,Mathf.Min(.24f,windup)),
                HeroFxMotion.Hold,actor.transform.rotation,actor,actor.transform);
        }

        public static void TargetWarning(GameObject actor,Health target,float seconds)
        {
            if(Hero(actor)==HeroId.Nerok){using var original=HeroVfxPool.PreserveNerokOriginal();NerokOriginalTargetWarning(actor,target,seconds);return;}
            using var cue=HeroVfxEvents.Begin(HeroVfxCue.TargetWarning,actor,duration:seconds,target:target!=null?target.gameObject:null);
            if(actor==null||target==null)return;
            Vector3 p=Ground(target.transform.position);
            Fx(HeroFxShape.Hex,p,new Color(1,.48f,.16f),.82f,seconds,HeroFxMotion.Hold,actor:actor,follow:target.transform,warning:true);
            Fx(HeroFxShape.Rift,target.transform.position+Vector3.up*.25f,Accent(actor),.6f,seconds,HeroFxMotion.Contract,Face(actor.transform.position,target.transform.position),actor,target.transform,true);
        }
        public static void SharkAnchor(GameObject actor,Vector3 impact,float flight,Transform target=null)
        {
            if(Hero(actor)==HeroId.Nerok){using var original=HeroVfxPool.PreserveNerokOriginal();NerokOriginalSharkAnchor(actor,impact,flight,target);return;}
            Vector3 from=Muzzle(actor),to=impact+Vector3.up*.35f;
            HeroVfxPool.Emit(HeroFxShape.AnchorProjectile,from,to,Vector3.one*.78f,new Color(.04f,.70f,1f,.94f),
                flight,HeroFxMotion.TravelFixedFrame,Face(from,to),actor,endFollow:target);
        }
        public static void SharkPullChain(GameObject actor,Health target,float duration)
        {
            if(Hero(actor)==HeroId.Nerok){using var original=HeroVfxPool.PreserveNerokOriginal();NerokOriginalSharkPullChain(actor,target,duration);return;}
            if(actor==null||target==null||target.IsDead)return;
            Vector3 from=Muzzle(actor),to=target.transform.position+Vector3.up*.4f;
            HeroVfxPool.Emit(HeroFxShape.AnchorChain,from,to,new Vector3(.9f,.9f,1f),
                new Color(.45f,.80f,1f,1f),duration,HeroFxMotion.Tether,
                owner:actor,follow:actor.transform,endFollow:target.transform);
        }
        public static void SharkVortex(GameObject actor,Vector3 center,float radius,float duration)
        {
            if(Hero(actor)==HeroId.Nerok){using var original=HeroVfxPool.PreserveNerokOriginal();NerokOriginalSharkVortex(actor,center,radius,duration);return;}
            Vector3 p=Ground(center);
            Fx(HeroFxShape.WaterSpiral,p,new Color(.02f,.48f,.95f,.65f),radius,duration,HeroFxMotion.Sustained,actor:actor);
        }
        public static void Area(GameObject actor,Vector3 center,float radius,float duration,bool warning,bool beneficial=false,int slot=-1)
        {
            if(Hero(actor)==HeroId.Nerok){using var original=HeroVfxPool.PreserveNerokOriginal();NerokOriginalArea(actor,center,radius,duration,warning,beneficial,slot);return;}
            using var cue=HeroVfxEvents.Begin(HeroVfxCue.Area,actor,center,slot,duration,radius,flag:warning,secondary:beneficial);
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


            HeroFxShape shape=hero==HeroId.Mira?HeroFxShape.Plus:hero==HeroId.Rook?HeroFxShape.Burst:
                hero==HeroId.Elyra||hero==HeroId.Traveler?HeroFxShape.Rift:HeroFxShape.PressureWave;
            float size=Mathf.Min(radius*.62f,slot==3?1.75f:1.1f);
            Fx(shape,ground+Vector3.up*.12f,beneficial?new Color(.22f,1f,.52f):AttackColour(actor,slot),
                size,slot==3?.40f:.27f,HeroFxMotion.Hold,actor:actor);
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
            if(Hero(actor)==HeroId.Nerok){using var original=HeroVfxPool.PreserveNerokOriginal();NerokOriginalSpell(actor,impact,slot,travelTime,target);return;}
            using var cue=HeroVfxEvents.Begin(HeroVfxCue.Spell,actor,impact,slot,duration:travelTime,target:target!=null?target.gameObject:null);
            if(actor==null)return;HeroId h=Hero(actor);

            if(h==HeroId.Timmy)
            {
                if(slot==0||slot==3)TimmyClaws(actor,impact,slot);
                else if(slot==1&&travelTime>0)TimmyEffect(actor,5,Muzzle(actor),impact,0,travelTime);
                return;
            }
            switch(h)
            {
                case HeroId.VoidKnight:
                    if(slot==0)Slash(actor,impact,slot,true);
                    break;
                case HeroId.Veyr:
                    if(slot==1)RiftScarProjectile(actor,impact,travelTime,target);
                    else if(slot==3)VacantEchoSickle(actor,impact);
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
            if(Hero(actor)==HeroId.Nerok){using var original=HeroVfxPool.PreserveNerokOriginal();NerokOriginalOrunOverdrive(actor,center);return;}
            if(actor==null||Hero(actor)!=HeroId.Rook)return;
            AbilitySpec ultimate=HeroAbilityKit.LoadoutFor(HeroId.Rook).Ultimate;
            HeroVfxPool.Emit(HeroFxShape.PressureWave,Ground(center),Ground(center),Vector3.one*ultimate.Radius,
                new Color(1f,.34f,.035f,.92f),Mathf.Max(.32f,ultimate.Windup),HeroFxMotion.RadialPulse,
                owner:actor,cancelOnOwnerDeath:true);
        }

        public static void Ultimate(GameObject actor,Vector3 target)
        {
            if(Hero(actor)==HeroId.Nerok){using var original=HeroVfxPool.PreserveNerokOriginal();NerokOriginalUltimate(actor,target);return;}
            using var cue=HeroVfxEvents.Begin(HeroVfxCue.Ultimate,actor,target,slot:3);if(actor!=null)Cast(actor,3);}


        public static void Impact(GameObject actor,Health target,int slot)
        {
            if(Hero(actor)==HeroId.Nerok){using var original=HeroVfxPool.PreserveNerokOriginal();NerokOriginalImpact(actor,target,slot);return;}
            using var cue=HeroVfxEvents.Begin(HeroVfxCue.Impact,actor,slot:slot,target:target!=null?target.gameObject:null);
            if(actor==null||target==null)return;
            HeroId hero=Hero(actor);Color tint=AttackColour(actor,slot);
            if(hero==HeroId.Nyra&&slot==2)
            {
                // One persistent snare cue replaces the old stacked bands and contact flash.
                Fx(HeroFxShape.Runes,Ground(target.transform.position),tint,.65f,.50f,
                    HeroFxMotion.Hold,actor:actor,follow:target.transform);
                return;
            }
            HeroFxShape shape=hero==HeroId.Rook?HeroFxShape.Burst:hero==HeroId.Nerok?HeroFxShape.WaveCrest:
                hero==HeroId.Mira?HeroFxShape.Petals:HeroFxShape.ContactStar;
            Fx(shape,target.transform.position+Vector3.up*.35f,tint,slot<0?.42f:slot==3?.9f:.65f,
                .22f,HeroFxMotion.Hold,Face(actor.transform.position,target.transform.position),actor);
        }

        public static void ShieldContact(GameObject victim,GameObject source)
        {
            if(Hero(victim)==HeroId.Nerok){using var original=HeroVfxPool.PreserveNerokOriginal();NerokOriginalShieldContact(victim,source);return;}
            using var cue=HeroVfxEvents.Begin(HeroVfxCue.ShieldContact,victim,target:source);
            if(victim==null)return;
            Vector3 p=victim.transform.position+Vector3.up*.35f;
            Quaternion face=source!=null?Face(source.transform.position,p):Quaternion.identity;
            Fx(HeroFxShape.Hex,p,new Color(.55f,.90f,1f),.72f,.16f,HeroFxMotion.Contract,
                face*Quaternion.Euler(65,0,0),follow:victim.transform);
        }
        public static void Support(GameObject actor,Health target,bool shield,float duration)
        {
            if(Hero(actor)==HeroId.Nerok){using var original=HeroVfxPool.PreserveNerokOriginal();NerokOriginalSupport(actor,target,shield,duration);return;}
            using var cue=HeroVfxEvents.Begin(HeroVfxCue.Support,actor,duration:duration,target:target!=null?target.gameObject:null,flag:shield);
            if(actor==null||target==null)return;
            Color tint=!shield||Hero(actor)==HeroId.Mira?new Color(.22f,1f,.52f):Accent(actor);
            Vector3 point=target.transform.position+(shield?Vector3.down*.70f:Vector3.up*.45f);
            if(shield&&Hero(actor)==HeroId.Mira)tint=Color.Lerp(tint,Color.white,.38f);
            HeroVfxPool.Emit(shield?HeroFxShape.Shield:HeroFxShape.Plus,point,point,
                shield?new Vector3(.87f,1.1f,.87f):Vector3.one*.85f,
                tint,shield?duration:.48f,HeroFxMotion.Hold,owner:actor,follow:target.transform,shield:shield?target:null);
            if(shield&&Hero(actor)==HeroId.Rook)actor.GetComponent<HeroVisualMotion>()?.Empower(Mathf.Min(duration,1.2f));
        }
        public static void SupportLink(GameObject actor,Health target)
        {
            if(Hero(actor)==HeroId.Nerok){using var original=HeroVfxPool.PreserveNerokOriginal();NerokOriginalSupportLink(actor,target);return;}
            using var cue=HeroVfxEvents.Begin(HeroVfxCue.SupportLink,actor,target:target!=null?target.gameObject:null);
            if(actor==null||target==null)return;
            Vector3 from=Muzzle(actor),to=target.transform.position+Vector3.up*.3f;
            HeroVfxPool.Emit(HeroFxShape.Seed,from,to,Vector3.one*.25f,new Color(.22f,1f,.52f),
                .16f,HeroFxMotion.Travel,Face(from,to),actor);
        }
        public static void Debuff(GameObject actor,Health target)
        {
            if(Hero(actor)==HeroId.Nerok){using var original=HeroVfxPool.PreserveNerokOriginal();NerokOriginalDebuff(actor,target);return;}
            using var cue=HeroVfxEvents.Begin(HeroVfxCue.Debuff,actor,target:target!=null?target.gameObject:null);
            if(actor==null||target==null||target.GetComponent<HeroIdentity>()==null)return;
            HeroId h=Hero(actor);if(h==HeroId.Nyra)return; // Dedicated snare bands already show this effect.
            Ring(actor,Ground(target.transform.position),.6f,.35f,h==HeroId.Elyra,h==HeroId.Kaelon,follow:target.transform);
        }
        public static void PlayerHit(GameObject victim,GameObject source)
        {
            if(Hero(victim)==HeroId.Nerok){using var original=HeroVfxPool.PreserveNerokOriginal();NerokOriginalPlayerHit(victim,source);return;}
            using var cue=HeroVfxEvents.Begin(HeroVfxCue.PlayerHit,victim,target:source);
            if(victim==null||source==null)return;
            Fx(HeroFxShape.Slash,Chest(victim),new Color(1,.50f,.28f),.43f,.10f,HeroFxMotion.Contract,Face(victim.transform.position,source.transform.position));
        }
        public static void Basic(GameObject actor,Vector3 impact,float travelTime=0,Transform target=null,bool animate=true)
        {
            if(Hero(actor)==HeroId.Nerok){using var original=HeroVfxPool.PreserveNerokOriginal();NerokOriginalBasic(actor,impact,travelTime,target,animate);return;}
            using var cue=HeroVfxEvents.Begin(HeroVfxCue.Basic,actor,impact,slot:-1,duration:travelTime,target:target!=null?target.gameObject:null,flag:animate);
            if(actor==null)return;if(animate)actor.GetComponent<HeroVisualMotion>()?.PulseAttack(false);

            if(Hero(actor)==HeroId.Timmy){TimmyClaws(actor,impact,-1);return;}
            switch(Hero(actor))
            {
                case HeroId.VoidKnight:Slash(actor,impact,-1,true);break;
                case HeroId.Veyr:Slash(actor,impact,-1);break;
                case HeroId.Kaelon:
                case HeroId.Nerok:
                case HeroId.Rook:HeavyContact(actor,impact,-1);break;
                default:Projectile(actor,impact,-1,travelTime,target);break;
            }
        }
        public static void Link(GameObject actor,Vector3 impact,bool ultimate=false)
        {
            if(Hero(actor)==HeroId.Nerok){using var original=HeroVfxPool.PreserveNerokOriginal();NerokOriginalLink(actor,impact,ultimate);return;}Basic(actor,impact);if(ultimate)Spell(actor,impact,3);}
        public static void Dash(GameObject actor,Vector3 from,Vector3 to,int slot=2,float duration=.16f)
        {
            if(Hero(actor)==HeroId.Nerok){using var original=HeroVfxPool.PreserveNerokOriginal();NerokOriginalDash(actor,from,to,slot,duration);return;}
            if(actor==null)return;
            HeroId hero=Hero(actor);
            // The generic movement caller omits its optional VFX slot. Sivra/Traveler jump on W.
            if(slot==2&&(hero==HeroId.Nyra||hero==HeroId.Traveler))slot=1;
            using var cue=HeroVfxEvents.Begin(HeroVfxCue.Dash,actor,to,slot,duration,origin:from);
            if(hero==HeroId.Timmy){TimmyEffect(actor,slot==3?3:2,from,to,0,duration);return;}
            // This is a jump presentation, not a travelling spell projectile. Dust marks
            // take-off and the actual actor's landing; the existing motion owns the jump.
            Color tint=AttackColour(actor,slot);Vector3 foot=from+Vector3.up*.08f;
            HeroVfxPool.Emit(HeroFxShape.Mist,foot,foot,Vector3.one*.34f,tint,.18f,
                HeroFxMotion.Expand,owner:actor,cancelOnOwnerDeath:true);
            HeroVfxPool.Emit(HeroFxShape.Mist,foot,foot,Vector3.one*.38f,tint,.20f,
                HeroFxMotion.Expand,owner:actor,follow:actor.transform,delay:duration,cancelOnOwnerDeath:true);
            actor.GetComponent<HeroVisualMotion>()?.PulseDash();
        }
    }
}


