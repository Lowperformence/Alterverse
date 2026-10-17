using Alterverse.Combat;
using UnityEngine;
namespace Alterverse.Characters
{
    public static class HeroAimPrediction
    {
        // Partial lead, frozen when the warning appears. Turns and dashes still beat it.
        public static Vector3 Point(Vector3 origin,Vector3 position,Vector3 velocity,float delay,float quality,float range,bool clampToArena=true)
        {
            velocity.y=0;velocity=Vector3.ClampMagnitude(velocity,8f);
            float time=Mathf.Min(.24f,Mathf.Max(0,delay)*Mathf.Clamp01(quality));
            Vector3 lead=Vector3.ClampMagnitude(velocity*time,1.2f);
            Vector3 offset=position+lead-origin;offset.y=0;
            Vector3 result=origin+Vector3.ClampMagnitude(offset,range);
            result.y=position.y;
            return clampToArena?GameplayCollision.ClampToRoundedArena(result,ArenaLayout.MovementHalfWidth,ArenaLayout.MovementHalfHeight):result;
        }
    }
}
