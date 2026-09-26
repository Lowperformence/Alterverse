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
            CheckCombatLineTransforms();
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
                    if(hero.Hero==HeroId.Veyr)
                    {
                        Vector3 expectedGrip=new(0,-.59f,.055f);
                        Require(Vector3.Distance(root.Find("DF_ArmL/DF_WeaponL").localPosition,expectedGrip)<.01f,
                            "Left assassin sword grip is detached from the hand.");
                        Require(Vector3.Distance(root.Find("DF_ArmR/DF_WeaponR").localPosition,expectedGrip)<.01f,
                            "Right assassin sword grip is detached from the hand.");
                    }
                }
                if(hero.Hero==HeroId.Nyra||hero.Hero==HeroId.Elyra||hero.Hero==HeroId.Mira||hero.Hero==HeroId.Traveler)
                    Require(root.Find("DF_ArmR/DF_WeaponR")!=null,"Weapon detached on "+hero.DisplayName);
                if(hero.Hero==HeroId.Elyra)
                    Require(root.Find("DF_ArmR/DF_WeaponR/DF_Orb")!=null,"Mage staff crystal/release point detached on "+hero.DisplayName);
                if(hero.Hero==HeroId.Traveler)
                    Require(root.Find("DF_ArmR/DF_WeaponR/DF_Orb")!=null,"Traveler Chronolux core/release point detached on "+hero.DisplayName);
                checkedHeroes++;
            }
            Debug.Log("DIMENSIONS FRONT: presentation asset checks passed; live hero rigs checked: "+checkedHeroes+
                ". This check does not replace visual, 30-attack, touch or Android performance testing.");
        }
        [MenuItem("DIMENSIONS FRONT/Checks/Mobile range line transforms")]
        public static void CheckCombatLineTransforms()
        {
            // Exercise the actual drawing transform without requiring an OnGUI event. Adjacent
            // projected segments must share endpoints at every resolution and safe-area offset.
            Type glyphs=typeof(Alterverse.UI.CombatHudLayout).Assembly.GetType("Alterverse.UI.CombatGlyphs",true);
            var method=glyphs.GetMethod("LineMatrix",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
            Require(method!=null,"Combat line transform is missing.");
            foreach(float scale in new[]{.5f,1f,690f/540f,2f,3f})
            foreach(Vector2 offset in new[]{Vector2.zero,new Vector2(84,24),new Vector2(0,65)})
            {
                Matrix4x4 hud=Matrix4x4.TRS(new Vector3(offset.x,offset.y,0),Quaternion.identity,Vector3.one*scale);
                Vector2 center=new Vector2(480,270);
                for(int i=0;i<48;i++)
                {
                    float first=i*Mathf.PI*2/48,second=(i+1)*Mathf.PI*2/48;
                    Vector2 a=center+new Vector2(Mathf.Cos(first)*160,Mathf.Sin(first)*80);
                    Vector2 b=center+new Vector2(Mathf.Cos(second)*160,Mathf.Sin(second)*80);
                    Matrix4x4 line=(Matrix4x4)method.Invoke(null,new object[]{hud,a,b});
                    Vector3 expectedA=hud.MultiplyPoint3x4(new Vector3(a.x,a.y,0));
                    Vector3 expectedB=hud.MultiplyPoint3x4(new Vector3(b.x,b.y,0));
                    Require(Vector3.Distance(line.MultiplyPoint3x4(Vector3.zero),expectedA)<.01f,
                        "Range segment start drift at HUD scale "+scale);
                    Require(Vector3.Distance(line.MultiplyPoint3x4(new Vector3((b-a).magnitude,0,0)),expectedB)<.01f,
                        "Range segment end drift at HUD scale "+scale);
                }
            }
            Debug.Log("DIMENSIONS FRONT: 720 combat line segments retain their endpoints across HUD scales and safe areas.");
        }
        private static void Require(bool valid,string message)
        {if(!valid)throw new InvalidOperationException("Hero presentation: "+message);}
    }
}
#endif
