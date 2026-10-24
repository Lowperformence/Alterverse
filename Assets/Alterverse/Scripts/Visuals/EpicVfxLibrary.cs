using System;
using UnityEngine;

namespace Alterverse.Visuals
{
    // Generated from the user's licensed Epic Toon FX import, never from demo controllers.
    public sealed class EpicVfxLibrary : ScriptableObject
    {
        public const string CurrentVersion = "Epic Toon FX user import; DF replacement 6";
        // Historical import factor. Runtime fitting now takes precedence over this value.
        public const float VisualScaleMultiplier=3f;
        public const float PortalScaleMultiplier=1.5f;
        public const string ResourcePath = "CombatVfx/EpicToon/Library";
        [Serializable] public sealed class Entry
        {
            public string key;
            public GameObject prefab;
            public float scale = 1f;
            public float nativeSeconds = 1f;
            // Measured on the converted composition, before runtime/legacy slot scaling.
            public float boundsRadius = 1f;
        }
        public Entry[] entries;
        public Material beamMaterial;
        public string sourceVersion;
        public enum Formation { Single,Cross,Parallel,Fan,Orbit,Eight,Crown }
        public sealed class AbilityLook
        {
            public readonly string Hero,Name,Trigger,Source,Accent;
            public readonly int Slot;
            public readonly Formation Layout;
            public float Emphasis=>Slot==3?1.4f:1f;
            public string CastSource=>Hero=="Veyr"?"Portal":Hero=="VoidKnight"?(Slot==1?"Rune":"HeavySlash"):
                Hero=="Nyra"?"Muzzle":Hero=="Mira"?"Heal":Hero=="Rook"?"Spark":Hero=="Traveler"?"LightningMuzzle":Hero=="Elyra"?"Vortex":"Rune";
            public string HitSource=>Hero=="Veyr"||Hero=="VoidKnight"||Hero=="Nyra"?"MiniHit":
                Hero=="Kaelon"||Hero=="Rook"?"SoftHit":Hero=="Mira"?(Slot==1?"Heal":"LeafHit"):
                Hero=="Traveler"?"LightningMuzzle":"MagicHit";
            public string Key=>Hero+"_"+(Slot<0?"Basic":Slot==0?"Q":Slot==1?"W":Slot==2?"E":"R")+"_"+Name;
            public AbilityLook(string hero,int slot,string name,string trigger,string source,
                Formation layout=Formation.Single,string accent=null)
            {Hero=hero;Slot=slot;Name=name;Trigger=trigger;Source=source;Layout=layout;Accent=accent;}
        }
        // One explicit visual concept per basic/Q/W/E/R, excluding the two restored heroes.
        // Trigger is the semantic family already requested by the real ability code.
        public static readonly AbilityLook[] AbilityLooks={
            new("Veyr",-1,"NullCutEdge","Slash","Slash"),
            new("Veyr",0,"SplitRiftCut","Slash","HeavySlash",Formation.Cross,"Portal"),
            new("Veyr",1,"RiftScarCrescent","ShadowMissile","BladeWave"),
            new("Veyr",2,"GapfoldTear","Smoke","Portal",Formation.Parallel),
            new("Veyr",3,"VacantEchoScythe","Slash","BladeWave",Formation.Cross),
            new("Kaelon",-1,"KestGauntlet","Punch","Punch"),
            new("Kaelon",0,"AnchorShockFront","Punch","Punch",Formation.Fan,"Rune"),
            new("Kaelon",1,"LumenRampart","Rune","Rune",Formation.Crown),
            new("Kaelon",2,"GravityEight","Rune","Rune",Formation.Eight),
            new("Kaelon",3,"CitadelSeal","Nova","MagicImpact",Formation.Crown,"Rune"),
            new("Nyra",-1,"QuellNeedle","Missile","SharpBolt"),
            new("Nyra",0,"PrismLine","ExactBeam","ExactBeam"),
            new("Nyra",1,"VectorAfterimage","Smoke","Smoke",Formation.Parallel,"Spark"),
            new("Nyra",2,"SnareFork","Missile","SharpBolt",Formation.Parallel,"Rune"),
            new("Nyra",3,"SightlineVolley","Missile","SharpBolt",Formation.Fan,"Impact"),
            new("Elyra",-1,"VaskaArcaneLance","ArcaneMissile","ArcaneMissile"),
            new("Elyra",0,"OrbitFracturePair","ArcaneMissile","ArcaneMissile",Formation.Orbit),
            new("Elyra",1,"InvertWell","Portal","Vortex",Formation.Single,"Rune"),
            new("Elyra",2,"PhaseSplit","Smoke","Portal",Formation.Cross),
            new("Elyra",3,"ImpossibleCollapse","Portal","Vortex",Formation.Single,"MagicImpact"),
            new("Mira",-1,"VennLeafDart","NatureMissile","LeafDart"),
            new("Mira",0,"ResonantThorns","NatureMissile","LeafDart",Formation.Fan),
            new("Mira",1,"GrowthBloom","Heal","Heal",Formation.Orbit,"NatureImpact"),
            new("Mira",2,"ShelterPetals","Rune","NatureImpact",Formation.Crown,"Rune"),
            new("Mira",3,"WildsurgeBloom","NatureNova","NatureNova",Formation.Single,"NatureImpact"),
            new("Rook",-1,"OrunHammer","Punch","HeavySlash",Formation.Single,"Spark"),
            new("Rook",0,"WreckArc","Punch","HeavySlash",Formation.Cross,"FireImpact"),
            new("Rook",1,"ArmorRivet","Rune","Rune",Formation.Parallel,"HeavyImpact"),
            new("Rook",2,"WreckstormSpin","Spark","SpinStrike",Formation.Single,"FireImpact"),
            new("Rook",3,"OverdriveEruption","FireNova","FireNova",Formation.Single,"Spark"),
            new("Traveler",-1,"ChronoDart","Orb","LightningMissile"),
            new("Traveler",0,"TemporalCrescent","Orb","LightningMissile",Formation.Parallel,"LightningMuzzle"),
            new("Traveler",1,"RiftStepClock","Smoke","SpinPortal",Formation.Single,"LightningMuzzle"),
            new("Traveler",2,"ChronoFigureEight","Portal","Rune",Formation.Eight,"LightningNova"),
            new("Traveler",3,"ParadoxCollapse","Portal","LightningNova",Formation.Single,"Vortex"),
            new("VoidKnight",-1,"VoidEdge","HeavySlash","HeavySlash"),
            new("VoidKnight",0,"RiftSweepCross","HeavySlash","HeavySlash",Formation.Cross),
            new("VoidKnight",1,"NullGuardSeal","Rune","Rune",Formation.Crown,"Portal"),
            new("VoidKnight",2,"LungeBladeWake","Smoke","BladeWave",Formation.Parallel),
            new("VoidKnight",3,"EventideCleaveFan","HeavySlash","BladeWave",Formation.Fan,"SwordImpact")
        };
        public static AbilityLook FindLook(string hero,int slot)
        {foreach(var look in AbilityLooks)if(look.Hero==hero&&look.Slot==slot)return look;return null;}
        public static AbilityLook FindLookByKey(string key)
        {foreach(var look in AbilityLooks)if(look.Key==key||look.Key+"_Cast"==key||look.Key+"_Hit"==key)return look;return null;}
        public Entry Find(string key)
        {
            if(entries != null) foreach(var entry in entries)
                if(entry != null && entry.key == key) return entry;
            return null;
        }
    }
}
