#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Alterverse.Characters;
using UnityEditor;
using UnityEngine;

namespace Alterverse.EditorChecks
{
    public static class HeroPresentationChecks
    {
        [MenuItem("DIMENSIONS FRONT/Checks/Hero presentation assets and live meshes")]
        public static void Run()
        {
            foreach(string path in new[]{"HeroArt/HeroSurface","CombatVfx/HeroEnergy"})
            {
                Material material=Resources.Load<Material>(path);
                Require(material!=null,"Missing material: "+path);
                Require(material.shader!=null&&!ShaderUtil.ShaderHasError(material.shader),"Shader errors: "+path);
            }
            int checkedHeroes=0;
            foreach(HeroIdentity hero in UnityEngine.Object.FindObjectsByType<HeroIdentity>(FindObjectsInactive.Include))
            {
                Transform root=hero.transform.Find("DF_VisualRoot");if(root==null)continue;
                Require(root.GetComponentsInChildren<Collider>(true).Length==0,"Visual rig contains a gameplay collider.");
                var materials=new HashSet<Material>();int count=0;
                foreach(MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if(!renderer.enabled)continue;
                    MeshFilter filter=renderer.GetComponent<MeshFilter>();
                    Require(filter!=null&&filter.sharedMesh!=null,"Missing mesh on "+renderer.name);
                    Mesh mesh=filter.sharedMesh;
                    Require(mesh.vertexCount>0&&mesh.triangles.Length>0,"Empty mesh on "+renderer.name);
                    if(renderer.sharedMaterial!=null&&renderer.sharedMaterial.shader.name=="DimensionsFront/HeroSurface")
                        Require(mesh.colors.Length==mesh.vertexCount,"Missing vertex palette on "+renderer.name);
                    materials.Add(renderer.sharedMaterial);count++;
                }
                Require(!materials.Contains(null)&&materials.Count<=6,"Material budget exceeded on "+hero.DisplayName);
                // Cosmetic wings can have additional renderers; base heroes retain the existing 24-renderer gate.
                if(hero.GetComponent<Alterverse.Visuals.VoidWingPresentation>()==null)
                    Require(count<=24,"Renderer budget exceeded on "+hero.DisplayName+": "+count);
                if(hero.Hero==HeroId.Veyr||hero.Hero==HeroId.Kaelon)
                {
                    Require(root.Find("DF_ArmL/DF_WeaponL")!=null,"Left weapon detached on "+hero.DisplayName);
                    Require(root.Find("DF_ArmR/DF_WeaponR")!=null,"Right weapon detached on "+hero.DisplayName);
                }
                if(hero.Hero==HeroId.Nyra||hero.Hero==HeroId.Mira)
                    Require(root.Find("DF_ArmR/DF_WeaponR")!=null,"Weapon detached on "+hero.DisplayName);
                checkedHeroes++;
            }
            Debug.Log("DIMENSIONS FRONT: presentation asset checks passed; live hero rigs checked: "+checkedHeroes+
                ". This check does not replace visual, 30-attack, touch or Android performance testing.");
        }
        private static void Require(bool valid,string message)
        {if(!valid)throw new InvalidOperationException("Hero presentation: "+message);}
    }
}
#endif
