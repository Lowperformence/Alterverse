#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Alterverse.EditorTools
{
    public static class ModernHeroArtValidation
    {
        [MenuItem("Alterverse/Validation/Modern Hero Art")]
        public static void Run()
        {
            foreach(string hero in new[]{"Veyr","Kaelon","Nyra","Elyra","Mira","Rook","Traveler"})
            {
                string path="Assets/Alterverse/Resources/AuthoredHeroes/"+hero+".fbx";
                // Explicit reimport also makes this a useful recovery check on an existing Library cache.
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
                GameObject asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Require(asset!=null,"Missing hero: "+hero);
                var renderers=asset.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var anatomy=renderers.FirstOrDefault(r=>r.name=="DF_Anatomy");
                var details=renderers.FirstOrDefault(r=>r.name=="DF_ArticulatedArmorDetails");
                Require(anatomy!=null&&details!=null,"Missing anatomy/detail renderer: "+hero);
                Require(details.sharedMesh!=null&&details.sharedMesh.vertexCount>0,"Empty details: "+hero);
                Require(details.sharedMesh.vertexCount<=512,"Unexpected detail budget: "+hero);
                Require(details.bones.Length==anatomy.bones.Length,"Detail skeleton mismatch: "+hero);
                Require(details.sharedMesh.bindposes.Length==details.bones.Length,"Invalid detail bindposes: "+hero);
                Require(details.sharedMaterials.Length==1&&details.sharedMaterial!=null,"Invalid detail material: "+hero);
                Require(renderers.Any(r=>r.name=="DF_WeaponR"||r.name=="DF_GauntletR"),"Missing right weapon: "+hero);
                foreach(var renderer in renderers)
                    Require(renderer.bones.All(b=>b!=null),"Missing bone reference: "+hero+"/"+renderer.name);
                if(hero=="Traveler")
                {
                    Transform muzzle=asset.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="DF_Muzzle");
                    Require(muzzle!=null&&muzzle.parent.name=="WeaponGripR","Traveler muzzle is not attached to orb grip");
                }
            }
            Shader overlay=Resources.Load<Shader>("HeroArt/WeaponEnergyOverlay");
            Require(overlay!=null&&!ShaderUtil.ShaderHasError(overlay),"Missing or invalid weapon overlay shader");
            Debug.Log("DF modern hero import checks passed. Visual pose, handle removal and Android play checks are still required.");
        }
        private static void Require(bool condition,string message)
        { if(!condition)throw new InvalidOperationException("DF modern hero art: "+message); }
    }
}
#endif
