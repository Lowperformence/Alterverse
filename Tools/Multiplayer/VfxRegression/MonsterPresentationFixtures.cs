using UnityEngine;

// Only environment/art sockets are substituted. MonsterAttackVfx and PrototypeVfx
// compile unchanged and use the production scheduler/material selection.
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
    public class MechMachineGunVisual:MonoBehaviour
    {
        public Transform Muzzle,EjectionPort;
        public bool BurstPresented;
        public float FlightDuration=.2f;
        public void MarkBurstPresented(){BurstPresented=true;}
    }
}
