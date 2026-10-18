using Alterverse.Characters;
using UnityEngine;

namespace Alterverse.Visuals
{
    public sealed partial class HeroSkillVfx
    {
        private static void TemporalLaser(GameObject actor,Vector3 start,Vector3 end,float duration)
        {
            // A fixed, full-length white laser appears on the authoritative release.
            // No target tracking: movement after release cannot redirect the cast.
            end.y=start.y;
            float range=HeroAbilityKit.LoadoutFor(HeroId.Traveler).Q.Range;
            float life=Mathf.Max(.12f,duration);
            HeroVfxPool.Emit(HeroFxShape.Laser,start,end,new Vector3(.24f,.24f,1f),
                new Color(1f,1f,1f,.38f),life,HeroFxMotion.LaserBeam,owner:actor,
                cancelOnOwnerDeath:true,maxRange:range);
            HeroVfxPool.Emit(HeroFxShape.Laser,start,end,new Vector3(.075f,.075f,1f),
                Color.white,life,HeroFxMotion.LaserBeam,owner:actor,
                cancelOnOwnerDeath:true,maxRange:range);
            Fx(HeroFxShape.Burst,start,Color.white,.18f,.09f,actor:actor);
        }
    }
}
