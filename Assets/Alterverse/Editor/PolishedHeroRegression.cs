#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Alterverse.Characters;
using Alterverse.Shop;
using Alterverse.Visuals;
using UnityEditor;
using UnityEngine;

namespace Alterverse.EditorChecks
{
    public static class PolishedHeroRegression
    {
        [MenuItem("DIMENSIONS FRONT/Checks/Polished heroes (Play Mode)")]
        public static void Run()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Start Play Mode before running this check.");
            foreach(string path in new[]{"HeroArt/HeroSurface","CombatVfx/HeroEnergy"})
            {
                Material material=Resources.Load<Material>(path);
                Require(material!=null&&material.shader!=null&&!ShaderUtil.ShaderHasError(material.shader),"Shader failed: "+path);
            }
            int triangles=0;
            foreach(HeroId hero in Enum.GetValues(typeof(HeroId)))
            {
                var actor=new GameObject("DF_PolishRegression_"+hero);
                try
                {
                    actor.AddComponent<HeroIdentity>().Configure(hero);
                    actor.AddComponent<SkinPreviewSelection>().UniversalCosmetic=UniversalHeroCosmetic.Original;
                    actor.AddComponent<StylizedHeroVisual>().Build();
                    Transform root=actor.transform.Find("DF_VisualRoot");
                    Require(root!=null,"Visual root missing: "+hero);
                    Require(root.GetComponentsInChildren<Collider>(true).Length==0,"Visual collider added: "+hero);
                    var materials=new HashSet<Material>();int renderers=0;
                    foreach(MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>())
                    {
                        if(!renderer.enabled)continue;
                        Mesh mesh=renderer.GetComponent<MeshFilter>()?.sharedMesh;
                        Require(mesh!=null&&mesh.vertexCount>0,"Empty mesh: "+hero);
                        renderers++;materials.Add(renderer.sharedMaterial);
                        Vector3[] vertices=mesh.vertices,normals=mesh.normals;
                        foreach(Vector3 vertex in vertices)Require(Finite(vertex.x)&&Finite(vertex.y)&&Finite(vertex.z),"Non-finite vertex: "+hero);
                        Require(normals.Length==vertices.Length,"Missing normals: "+hero);
                        foreach(Vector3 normal in normals)Require(Finite(normal.sqrMagnitude)&&normal.sqrMagnitude>.5f,"Invalid normal: "+hero);
                        if(renderer.sharedMaterial!=null&&renderer.sharedMaterial.shader.name=="DimensionsFront/HeroSurface")
                        {
                            Require(mesh.colors.Length==vertices.Length,"Missing palette: "+hero);
                            Require(mesh.uv2.Length==vertices.Length,"Missing material response: "+hero);
                        }
                        if(hero==HeroId.Nyra)triangles+=mesh.triangles.Length/3;
                    }
                    Require(renderers<=24&&!materials.Contains(null)&&materials.Count<=6,"Presentation budget exceeded: "+hero);
                    if(hero==HeroId.Nyra)
                    {
                        Transform weapon=root.Find("DF_ArmR/DF_WeaponR");
                        Require(weapon!=null&&weapon.Find("DF_Muzzle")!=null,"Marksman weapon or muzzle detached.");
                        Require((weapon.Find("DF_Muzzle").localPosition-new Vector3(0,.015f,1.41f)).sqrMagnitude<.000001f,"Muzzle contract changed.");
                        Require(triangles<=25000,"Scout mesh budget exceeded: "+triangles);
                    }
                }
                finally{actor.SetActive(false);UnityEngine.Object.Destroy(actor);}
            }
            Debug.Log("DF polish: seven hero rigs, shader compilation, material channels and muzzle attachment passed. Scout triangles: "+triangles+". Device FPS and visual review are separate checks.");
        }
        private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        private static void Require(bool valid,string message){if(!valid)throw new InvalidOperationException(message);}
    }
}
#endif
