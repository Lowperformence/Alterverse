using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>Render anchors share the hero's sampled pose; gameplay transforms stay authoritative.</summary>
    public static class HeroPresentationAnchor
    {
        public static Vector3 Position(Transform target)
            => Position(target,target!=null?target.GetComponent<HeroVisualMotion>():null);
        public static Vector3 Position(Transform target,HeroVisualMotion motion)
        {
            if(target==null)return Vector3.zero;
            if(motion!=null)return motion.HasNetworkPresentation?motion.PresentedPosition:target.position;
            var bolt=target.GetComponent<Bolt7Visual>();
            return bolt!=null&&bolt.HasNetworkPresentation?bolt.PresentedPosition:target.position;
        }
        public static Vector3 TransformPoint(Transform target,HeroVisualMotion motion,Vector3 localPoint)
        {
            Vector3 point=target.TransformPoint(localPoint);
            if(motion!=null)return motion.HasNetworkPresentation?
                motion.PresentedPosition+motion.PresentedRotation*(Quaternion.Inverse(target.rotation)*(point-target.position)):point;
            var bolt=target.GetComponent<Bolt7Visual>();
            return bolt!=null&&bolt.HasNetworkPresentation?
                bolt.PresentedPosition+bolt.PresentedRotation*(Quaternion.Inverse(target.rotation)*(point-target.position)):point;
        }
        public static Quaternion Rotation(Transform target,HeroVisualMotion motion)
        {
            if(target==null)return Quaternion.identity;
            if(motion!=null)return motion.HasNetworkPresentation?motion.PresentedRotation:target.rotation;
            var bolt=target.GetComponent<Bolt7Visual>();
            return bolt!=null&&bolt.HasNetworkPresentation?bolt.PresentedRotation:target.rotation;
        }
    }

}
