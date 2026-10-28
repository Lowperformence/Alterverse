using UnityEngine;

// Environment/art sockets and native particle backends are substituted.
// MonsterAttackVfx and PrototypeVfx use the production scheduler/material selection.
namespace Alterverse.Match
{
    public enum ArenaVariant{Moon,Mars,GreenPlanet,Noxara,Kryon,Earth,Pyraxis,Neptune,Dark00,DiamondCore,GJ504b,Wasp76b,Trappist1e,Toi715b}
    public static class ArenaVariantRuntime
    {
        public static ArenaVariant Current;
        public static bool IsMars=>Current==ArenaVariant.Mars;
        public static bool IsGJ504b=>Current==ArenaVariant.GJ504b;
        public static bool IsDiamondCore=>Current==ArenaVariant.DiamondCore;
        public static bool IsGreenPlanet=>Current==ArenaVariant.GreenPlanet;
        public static bool IsToi715b=>Current==ArenaVariant.Toi715b;
        public static bool IsNeptune=>Current==ArenaVariant.Neptune;
        public static bool IsExoplanet=>(int)Current>=10;
    }
    public static class ExoplanetArenaProfile
    {public static Color Energy(ArenaVariant variant)=>new Color(.2f,.7f,1f);}
}
namespace Alterverse.Combat {public class HealingGuardMech:MonoBehaviour {}}
namespace Alterverse.Visuals
{
    // Native backends have separate production-code runners. Preserve BOLT-7's
    // library-unavailable trace adapters so the real shared pool and transport run here.
    public static class Bolt7Vfx
    {
        public static int Calls;
        public static void CancelOwner(GameObject a){}public static void Cast(GameObject a,int s){Calls++;}
        public static void Area(GameObject a,Vector3 p,int s)=>HeroVfxPool.Trace(p,p+Vector3.up*.35f,new Color(1,1,0),.12f,.2f);
        public static void Grenade(GameObject a,Vector3 p,float d,int s)=>HeroVfxPool.Trace(a.transform.position,p,new Color(1,.65f,.25f),.05f,.15f);
        public static void BasicImpact(GameObject a,Vector3 p){}
        public static void Basic(GameObject a,Vector3 p,float d){Calls++;HeroVfxPool.Trace(a.transform.position,p+Vector3.up*.25f,new Color(1,.72f,.25f),.018f/3f,.085f);}
        public static void RamDash(GameObject a,Vector3 p,Vector3 q,float d){}
    }
    public static class AttackExplosionVfx
    {public static bool TryMeteor(Vector3 p,Color c,float elapsed,float radius)=>false;}
    public class MechMachineGunVisual:MonoBehaviour
    {
        public Transform Muzzle,EjectionPort;
        public bool BurstPresented;
        public float FlightDuration=.2f;
        public void MarkBurstPresented(){BurstPresented=true;}
    }
}

