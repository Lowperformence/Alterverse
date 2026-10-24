using UnityEngine;
namespace Alterverse.Visuals
{
    // Compatibility facade. The opaque six-ray comic marker is replaced by one
    // quiet purchased contact; hero attacks already supply their own hit variant.
    public sealed class CombatImpactPool:MonoBehaviour
    {
        public static void Warm(Transform matchRoot){HeroVfxPool.Warm();}
        public static void Emit(Vector3 point,Color color,float strength)
        {
            var camera=Camera.main;if(camera==null)return;
            Vector3 screen=camera.WorldToViewportPoint(point);
            if(screen.z<=0||screen.x<-.05f||screen.x>1.05f||screen.y<-.05f||screen.y>1.05f)return;
            color.a=Mathf.Min(color.a,.4f);
            HeroVfxPool.Emit(HeroFxShape.ContactStar,point,point,Vector3.one*.1f,color,.12f,
                HeroFxMotion.Hold,epicKey:"MiniHit",effectRadius:Mathf.Lerp(.09f,.16f,Mathf.Clamp01(strength)));
        }
        private void Awake(){enabled=false;}
    }
}
