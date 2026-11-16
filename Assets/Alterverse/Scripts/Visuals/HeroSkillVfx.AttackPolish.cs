using System.Collections.Generic;
using Alterverse.Characters;
using Alterverse.Combat;
using UnityEngine;

namespace Alterverse.Visuals
{
    public sealed partial class HeroSkillVfx
    {
        // These are extra presentation layers inside the existing semantic cue. They
        // never publish packets, resolve hits, change windups, or replace danger bounds.
        private static readonly Dictionary<int, int> actorComboCounter = new();

        private static int NextAttackOpticVariant(GameObject actor)
        {
            if (actor == null) return 0;
            int id = actor.GetEntityId().GetHashCode();
            int next = (actorComboCounter.TryGetValue(id, out int cur) ? cur : 0) + 1;
            actorComboCounter[id] = next;
            return next % 3;
        }

        private static Color AttackColour(GameObject actor,int slot)
        {
            HeroId hero=Hero(actor);
            if(hero==HeroId.Nyra)return SivraSkillColor(slot);
            if(hero==HeroId.Traveler&&slot>=0)return TravelerColor(slot);
            if(hero==HeroId.Bolt7)return slot==3?new Color(1f,.78f,.24f,.92f):new Color(1f,.64f,.20f,.86f);
            if(hero==HeroId.VoidKnight)return slot==3?new Color(.82f,.28f,1f,.92f):new Color(.66f,.20f,1f,.88f);
            if(hero==HeroId.Timmy)return new Color(.28f,.88f,1f,.88f);
            if(hero==HeroId.Rook)return new Color(1f,.38f,.09f,.85f);
            if(hero==HeroId.Nerok)return new Color(.10f,.76f,1f,.78f);
            if(hero==HeroId.Kaelon&&slot==0)return new Color(.68f,.20f,1f,.90f);
            if(hero==HeroId.Kaelon&&slot==2)return new Color(.08f,.46f,1f,.90f);
            if(hero==HeroId.Kaelon&&slot==3)return new Color(1f,.84f,.06f,.80f);
            return Accent(actor);
        }

        private static void PolishCast(GameObject actor,int slot)
        {
            HeroId hero=Hero(actor);
            bool guard=slot==1&&(hero==HeroId.VoidKnight||hero==HeroId.Kaelon||hero==HeroId.Nerok||hero==HeroId.Bolt7);
            HeroFxSprite sprite=guard?HeroFxSprite.ShieldGlyph:
                hero==HeroId.Nyra||hero==HeroId.Bolt7?HeroFxSprite.Muzzle:
                hero==HeroId.Mira?HeroFxSprite.SoftStar:
                hero==HeroId.Elyra||hero==HeroId.Traveler?HeroFxSprite.Rune:HeroFxSprite.Flare;
            Color tint=AttackColour(actor,slot);
            if(hero==HeroId.Mira&&slot>0)tint=new Color(.25f,1f,.58f,.70f);
            float size=slot==3?.68f:.36f;
            Vector3 p=Muzzle(actor);
            HeroVfxPool.Sprite(sprite,p,p,new Vector2(size,size),tint,.18f,actor,follow:actor.transform);
        }

        private static void PolishRelease(GameObject actor,Vector3 point,int slot,float travel,Transform target)
        {
            HeroId hero=Hero(actor);Color tint=AttackColour(actor,slot);
            int opticVariant=NextAttackOpticVariant(actor);
            Color bright=Color.Lerp(tint,Color.white,opticVariant==2?.42f:.22f);
            bright.a=tint.a;
            Vector3 from=Muzzle(actor),to=point+Vector3.up*.25f;
            if(hero==HeroId.Timmy&&slot==1)return; // The canister owns its flight and dust landing.
            if(!HeroAttackDelivery.IsRanged(hero)&&!(hero==HeroId.Kaelon&&(slot==0||slot==2))&&
                !(hero==HeroId.Veyr&&slot==1))
            {
                HeroFxSprite slash=opticVariant==2&&(hero==HeroId.Veyr||hero==HeroId.VoidKnight)?HeroFxSprite.SlashCross:
                    hero==HeroId.VoidKnight?HeroFxSprite.SlashHeavy:
                    hero==HeroId.Timmy?HeroFxSprite.SlashClaw:hero==HeroId.Nerok?HeroFxSprite.SlashLunge:
                    hero==HeroId.Rook||hero==HeroId.Bolt7?HeroFxSprite.Fireball:
                    opticVariant==1?HeroFxSprite.SlashHeavy:HeroFxSprite.SlashLight;
                float size=(hero==HeroId.VoidKnight?slot<0?1.55f:2.1f:hero==HeroId.Rook||hero==HeroId.Bolt7?.86f:1.18f)*(opticVariant==2?1.14f:1f);
                Vector3 p=Vector3.Lerp(from,to,.60f);
                float tilt=opticVariant==1?-28f:opticVariant==2?24f:0f;
                Quaternion plane=Face(actor.transform.position,point)*Quaternion.Euler(90f,0f,tilt);
                HeroVfxPool.Sprite(slash,p,p,new Vector2(size,size),opticVariant==2?bright:tint,
                    hero==HeroId.VoidKnight?.26f:.20f,actor,rotation:plane,billboard:false);
                if(opticVariant==1)
                {
                    Quaternion counterPlane=Face(actor.transform.position,point)*Quaternion.Euler(68f,0f,32f);
                    HeroVfxPool.Sprite(HeroFxSprite.Flare,from,p,new Vector2(size*.45f,size*.45f),bright,.15f,
                        actor,HeroFxMotion.Travel,rotation:counterPlane);
                }
                else if(opticVariant==2)
                {
                    HeroVfxPool.Sprite(HeroFxSprite.ShockRing,p,p,new Vector2(size*.92f,size*.92f),
                        new Color(bright.r,bright.g,bright.b,.52f),.22f,actor,HeroFxMotion.Expand,
                        Quaternion.Euler(90f,0f,0f),false);
                }
                return;
            }
            // A laser stays a beam: its only new release layer is a compact muzzle flash.
            if(travel<=0||hero==HeroId.Nyra&&slot==0)
            {
                HeroVfxPool.Sprite(hero==HeroId.Nyra||hero==HeroId.Bolt7?HeroFxSprite.Muzzle:HeroFxSprite.Flare,
                    from,from,new Vector2(.42f,.42f)*(opticVariant==2?1.18f:1f),opticVariant==2?bright:tint,.10f,actor);
                return;
            }
            HeroFxSprite core=hero==HeroId.Elyra?HeroFxSprite.Fireball:hero==HeroId.Veyr?HeroFxSprite.SlashLunge:
                hero==HeroId.Mira?HeroFxSprite.SoftStar:hero==HeroId.Kaelon?HeroFxSprite.Rune:
                opticVariant==1?HeroFxSprite.Star:HeroFxSprite.Glow;
            float diameter=(slot==3?.56f:slot<0?.29f:.40f)*(opticVariant==2?1.15f:1f);
            HeroVfxPool.Sprite(core,from,to,new Vector2(diameter,diameter),opticVariant==2?bright:tint,travel,actor,
                motion:HeroFxMotion.Travel,endFollow:hero==HeroId.Kaelon?null:target,roll:opticVariant*35f);
        }

        private static void PolishImpact(GameObject actor,Health target,int slot)
        {
            HeroId hero=Hero(actor);Vector3 p=target.transform.position+Vector3.up*.35f;
            int id=actor!=null?actor.GetEntityId().GetHashCode():0;
            int opticVariant=(actorComboCounter.TryGetValue(id,out int cur)?cur:0)%3;
            HeroFxSprite sprite=opticVariant==2&&(hero==HeroId.Rook||hero==HeroId.Kaelon||hero==HeroId.Nerok)?HeroFxSprite.ShockRing:
                hero==HeroId.Rook||hero==HeroId.Bolt7?HeroFxSprite.Fireball:
                hero==HeroId.Veyr||hero==HeroId.VoidKnight?HeroFxSprite.SlashCross:
                hero==HeroId.Timmy?HeroFxSprite.ClawMark:
                hero==HeroId.Mira?HeroFxSprite.Star:HeroFxSprite.Burst;
            float size=(slot<0?.48f:slot==3?1.08f:.76f)*(opticVariant==2?1.16f:1f);
            float rollAngle=hero==HeroId.Timmy?(opticVariant==1?28f:-25f):(opticVariant==1?32f:opticVariant==2?-28f:0f);
            Color tint=AttackColour(actor,slot);
            HeroVfxPool.Sprite(sprite,p,p,new Vector2(size,size),tint,
                hero==HeroId.Rook||hero==HeroId.Bolt7?.26f:.19f,actor,roll:rollAngle);
            if(opticVariant==2)
            {
                Color accent=Color.Lerp(tint,Color.white,.45f);accent.a=.62f;
                HeroVfxPool.Sprite(HeroFxSprite.Star,p,p,new Vector2(size*.72f,size*.72f),accent,.16f,
                    actor,HeroFxMotion.Expand,roll:45f);
            }
        }

        private static void PolishArea(GameObject actor,Vector3 center,float radius,int slot,bool beneficial)
        {
            HeroId hero=Hero(actor);
            HeroFxSprite sprite=hero==HeroId.Nerok?HeroFxSprite.Vortex:
                hero==HeroId.Mira?HeroFxSprite.SoftStar:hero==HeroId.Rook?HeroFxSprite.Dust:
                hero==HeroId.Elyra||hero==HeroId.Traveler?HeroFxSprite.Rune:HeroFxSprite.ShockRing;
            float diameter=Mathf.Min(Mathf.Max(0,radius)*1.05f,slot==3?2.6f:1.8f);
            Color tint=beneficial?new Color(.25f,1f,.58f,.48f):AttackColour(actor,slot);
            tint.a=Mathf.Min(tint.a,.55f);
            Vector3 p=Ground(center)+Vector3.up*.035f;
            HeroVfxPool.Sprite(sprite,p,p,new Vector2(diameter,diameter),tint,slot==3?.40f:.27f,
                actor,HeroFxMotion.Expand,Quaternion.Euler(90f,0f,0f),false);
        }

        private static void PolishSupport(GameObject actor,Health target,bool shield,bool shieldUsesTotal)
        {
            Color tint=Hero(actor)==HeroId.Mira?new Color(.25f,1f,.58f,.64f):AttackColour(actor,1);
            Vector3 p=target.transform.position+Vector3.up*.48f;
            HeroVfxPool.Sprite(shield?HeroFxSprite.ShieldGlyph:HeroFxSprite.SoftStar,p,p,
                new Vector2(.72f,.72f),tint,.32f,actor,HeroFxMotion.Expand,follow:target.transform,shield:shield?target:null,shieldUsesTotal:shieldUsesTotal);
        }

        private static void PolishDash(GameObject actor,Vector3 from,Vector3 to)
        {
            if(Hero(actor)==HeroId.Timmy)return; // Jet layers are attached to the actual two nozzles.
            Color tint=AttackColour(actor,2);tint.a=.42f;
            Vector3 p=from+Vector3.up*.22f;
            HeroVfxPool.Sprite(Hero(actor)==HeroId.Traveler||Hero(actor)==HeroId.Elyra?
                HeroFxSprite.Portal:HeroFxSprite.Smoke,p,p,new Vector2(.80f,.80f),tint,.22f,
                actor,HeroFxMotion.Expand);
        }
    }
}
