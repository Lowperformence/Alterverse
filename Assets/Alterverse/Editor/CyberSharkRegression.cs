#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Alterverse.Audio;
using Alterverse.Characters;
using Alterverse.Content;
using Alterverse.Shop;
using Alterverse.UI;
using UnityEditor;
using UnityEngine;

namespace Alterverse.EditorChecks
{
    public static class CyberSharkRegression
    {
        [Serializable] private sealed class Submesh { public int[] triangles; }
        [Serializable] private sealed class Geometry
        {
            public float[] vertices,normals;
            public int[] boneIndices,groups;
            public string[] boneNames;
            public Submesh[] submeshes;
        }
        [MenuItem("DIMENSIONS FRONT/Checks/Cyber shark integration (non-destructive)")]
        public static void Run()
        {
            Require((int)HeroId.Traveler==6&&(int)HeroId.Nerok==7,"Existing save identifiers changed.");
            Require(Enum.GetValues(typeof(HeroId)).Length==HeroIdentity.HeroCount,"Roster count mismatch.");
            HeroAbilityLoadout kit=HeroAbilityKit.LoadoutFor(HeroId.Nerok);
            HeroCombatStats stats=HeroCombatProfile.StatsFor(HeroId.Nerok);
            Require(HeroIdentity.BaseHealth(HeroId.Traveler)<HeroIdentity.BaseHealth(HeroId.Nerok)&&
                HeroIdentity.BaseHealth(HeroId.Nerok)<HeroIdentity.BaseHealth(HeroId.Kaelon),"Bruiser durability outside its role.");
            Require(HeroIdentity.AttackDamage(HeroId.Nerok)/stats.AttackInterval <
                HeroIdentity.AttackDamage(HeroId.Rook)/HeroCombatProfile.StatsFor(HeroId.Rook).AttackInterval,"Control bruiser exceeds pure bruiser basic DPS.");
            Require(kit.Q.Range<=HeroCombatProfile.StatsFor(HeroId.Nyra).AttackRange&&kit.Q.Cooldown>=10,"Hook has no ranged counterplay window.");
            Require(kit.Ultimate.Power<=HeroAbilityKit.LoadoutFor(HeroId.Elyra).Ultimate.Power&&kit.Ultimate.Cooldown>=55,"Control ultimate exceeds mage damage budget.");
            Require(HeroAttackCatalog.For(HeroId.Nerok).Length==5,"Missing combat cards.");
            Require(Array.Exists(HeroFlavorContent.Heroes,h=>h.Hero==HeroId.Nerok),"Missing hero lore.");
            Require(WeaponSkinCatalog.VariantCount(HeroId.Nerok)==2,"Missing Epic anchors.");
            for(int i=0;i<2;i++)Require(WeaponSkinCatalog.For(HeroId.Nerok,i).Price==WeaponSkinCatalog.Price,"Epic price differs from other heroes.");
            GameObject template=Resources.Load<GameObject>("AuthoredHeroes/Rook");
            Require(template!=null,"Shared skeletal template missing.");
            bool hasBody=false,hasRight=false;
            foreach(SkinnedMeshRenderer renderer in template.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if(renderer.name=="DF_Anatomy")hasBody=true;
                if(renderer.name=="DF_GauntletR"||renderer.name=="DF_WeaponR")hasRight=true;
            }
            Require(hasBody&&hasRight,"Shared rig cannot receive shark body and anchor meshes.");
            TextAsset resource=Resources.Load<TextAsset>("CyberShark/Model");Require(resource!=null,"Blender model missing.");
            Geometry model;
            using(var stream=new GZipStream(new MemoryStream(resource.bytes),CompressionMode.Decompress))
            using(var reader=new StreamReader(stream))model=JsonUtility.FromJson<Geometry>(reader.ReadToEnd());
            Require(model.vertices.Length==model.normals.Length&&model.vertices.Length==model.groups.Length*3,"Invalid mesh channels.");
            Require(model.submeshes.Length==6,"Material slots changed.");
            int weaponVertices=0,hookVertices=0;
            for(int i=0;i<model.groups.Length;i++)
            {
                if(model.groups[i]==2){weaponVertices++;Require(model.boneNames[model.boneIndices[i]]=="WeaponGripR","Anchor bound to wrong hand.");}
                if(model.groups[i]==1){hookVertices++;Require(model.boneNames[model.boneIndices[i]]=="WeaponGripL","Chain hook bound to wrong hand.");}
            }
            Require(weaponVertices>0,"Anchor mesh absent.");
            Require(hookVertices>0,"Free-hand chain hook mesh absent.");
            foreach(string mesh in new[]{"AnchorProjectile","WaterSpiral","WaveCrest","AnchorChain"})
                Require(Resources.Load<TextAsset>("CyberShark/"+mesh)!=null,"Missing Blender VFX: "+mesh);
            for(int i=0;i<5;i++)Require(Resources.Load<Texture2D>("CyberShark/Icons/"+i)!=null,"Missing attack icon "+i);
            Require(Resources.Load<AudioClip>("Audio/Heroes/Nerok/Ultimate_Selected")!=null,"User's ultimate MP3 missing.");
            var cues=new HashSet<int>();
            for(int hero=0;hero<HeroIdentity.HeroCount;hero++)for(int action=0;action<5;action++)for(int phase=0;phase<3;phase++)
            {
                int cue=HeroCombatAudio.Cue(hero,action,phase);
                Require(cue>=0&&cue<HeroCombatAudio.CueCount&&cues.Add(cue),"Audio cue collision.");
            }
            Debug.Log("DF: Nerok catalog, balance bounds, skeleton, Blender model/VFX/icons, Epic anchors and audio integration passed. Playtest hook collision and online reconciliation separately.");
        }
        private static void Require(bool ok,string message){if(!ok)throw new InvalidOperationException("Cyber shark regression: "+message);}
    }
}
#endif

