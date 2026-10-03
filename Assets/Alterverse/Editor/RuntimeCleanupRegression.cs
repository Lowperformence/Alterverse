#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using Alterverse.Characters;
using Alterverse.Shop;
using Alterverse.Visuals;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Alterverse.EditorChecks
{
    public static class RuntimeCleanupRegression
    {
        [MenuItem("DIMENSIONS FRONT/Checks/Runtime cleanup (Play Mode)")]
        public static void Run()
        {
            Require(Application.isPlaying,"Enter Play Mode first.");
            CheckMaterials();
            foreach(HeroId hero in Enum.GetValues(typeof(HeroId)))CheckColorways(hero);
            Debug.Log("DIMENSIONS FRONT: material fallback and all seven weapon colorway checks passed.");
        }

        static void CheckMaterials()
        {
            GameObject go=new GameObject("DF_MaterialRegression");
            Shader standard=Shader.Find("Standard");
            Require(standard!=null,"Editor Standard shader missing.");
            Material source=new Material(standard);
            Material valid=RuntimeShaderLibrary.Create(Color.cyan);
            Material unlit=new Material(Shader.Find("Unlit/Texture"));
            try
            {
                source.SetTexture("_MainTex",Texture2D.whiteTexture);
                source.SetTextureScale("_MainTex",new Vector2(3,4));
                source.SetTextureOffset("_MainTex",new Vector2(.2f,.3f));
                source.SetFloat("_Glossiness",.73f);
                Renderer renderer=go.AddComponent<MeshRenderer>();
                renderer.sharedMaterials=new[]{source,valid,unlit,null};
                MethodInfo repair=typeof(AndroidMaterialCompatibilityGuard).GetMethod("RepairRenderer",BindingFlags.NonPublic|BindingFlags.Static);
                Require(repair!=null,"Material repair entry point missing.");
                repair.Invoke(null,new object[]{renderer});
                Material[] result=renderer.sharedMaterials;
                Require(result[0]!=source&&source.shader==standard,"Shared source material was mutated.");
                Require(result[0].GetTexture("_BaseMap")==Texture2D.whiteTexture,"Texture lost.");
                Require(result[0].GetTextureScale("_BaseMap")==new Vector2(3,4),"Texture tiling lost.");
                Require(result[0].GetTextureOffset("_BaseMap")==new Vector2(.2f,.3f),"Texture offset lost.");
                Require(Mathf.Abs(result[0].GetFloat("_Smoothness")-.73f)<.001f,"Legacy glossiness lost.");
                Require(result[1]==valid,"Valid project material was replaced.");
                Require(result[2].shader==RuntimeShaderLibrary.Unlit,"Unlit material became lit.");
                Require(result[3]!=null&&go.GetComponent<RuntimeMaterialPalette>()!=null,"Missing material is not owned.");
                repair.Invoke(null,new object[]{renderer});
                Require(result.SequenceEqual(renderer.sharedMaterials),"Repeated repair allocated replacement materials.");
                RuntimeMaterialPalette.Tint(go,renderer,Color.cyan);
                Material cyan=renderer.sharedMaterial;
                RuntimeMaterialPalette.Tint(go,renderer,Color.cyan);
                Require(renderer.sharedMaterial==cyan,"Repeated tint allocated another material.");
                RuntimeMaterialPalette.Tint(go,renderer,Color.green);
                RuntimeMaterialPalette.Tint(go,renderer,Color.cyan);
                Require(renderer.sharedMaterial==cyan,"Returning to a tint failed to reuse its material.");
            }
            finally {Object.Destroy(go);Object.Destroy(source);Object.Destroy(valid);Object.Destroy(unlit);}
        }

        static void CheckColorways(HeroId hero)
        {
            GameObject go=new GameObject("DF_ColorwayRegression_"+hero);
            try
            {
                HeroIdentity identity=go.AddComponent<HeroIdentity>();identity.Configure(hero);
                SkinPreviewSelection preview=go.AddComponent<SkinPreviewSelection>();
                go.AddComponent<StylizedHeroVisual>().Build();
                MeshFilter[] filters=go.GetComponentsInChildren<MeshFilter>(true);
                Mesh[] meshes=filters.Select(f=>f.sharedMesh).ToArray();
                Renderer[] renderers=go.GetComponentsInChildren<Renderer>(true);
                Material[][] original=renderers.Select(r=>r.sharedMaterials).ToArray();
                Type stateType=typeof(StylizedHeroVisual).Assembly.GetType("Alterverse.Visuals.OriginalWeaponColorwayState");
                Component state=go.AddComponent(stateType);
                MethodInfo sync=stateType.GetMethod("Sync");
                AuthoredHeroPresentation authored=go.GetComponent<AuthoredHeroPresentation>();
                if(authored!=null)
                {
                    Transform firstRoot=go.transform.Find("DF_VisualRoot");
                    Require(AuthoredHeroPresentation.TryCreate(go,hero,out Transform reused)&&reused==firstRoot,
                        hero+": repeated authored build did not reuse the rig.");
                    Require(go.GetComponentsInChildren<AuthoredHeroPresentation>(true).Length==1,
                        hero+": duplicate authored presentation.");
                }
                Renderer[] previousOverlays=null;
                int previousVariant=int.MinValue;
                foreach(int variant in new[]{0,0,1,1,-1,-1,0,-1})
                {
                    preview.WeaponSkinVariant=variant;sync.Invoke(state,new object[]{identity});
                    Require(meshes.SequenceEqual(filters.Select(f=>f.sharedMesh)),hero+": colorway replaced original meshes.");
                    for(int i=0;i<renderers.Length;i++)
                        Require(original[i].SequenceEqual(renderers[i].sharedMaterials),hero+": original weapon materials changed.");
                    // Destroy is deferred; only active layers represent the currently equipped design.
                    Renderer[] overlays=go.GetComponentsInChildren<Renderer>(true)
                        .Where(r=>r.name=="DF_WeaponEnergyOverlay"&&r.gameObject.activeInHierarchy).ToArray();
                    if(variant>=0)
                    {
                        Require(overlays.Length>0,hero+": weapon energy layer missing.");
                        if(authored!=null)Require(overlays.Length==authored.WeaponRenderers.Distinct().Count(r=>r!=null),
                            hero+": duplicate weapon energy layers.");
                        Require(overlays.All(r=>r.sharedMaterials.Any(m=>m!=null&&m.HasProperty("_GlowColor")&&
                            Mathf.Abs(m.GetColor("_GlowColor").a-.5f)<.001f)),hero+": weapon transparency changed.");
                    }
                    else Require(overlays.Length==0,hero+": unequip left a visible energy layer.");
                    if(variant==previousVariant)Require(previousOverlays.SequenceEqual(overlays),
                        hero+": repeated sync recreated weapon layers.");
                    previousOverlays=overlays;previousVariant=variant;
                }
                Require(!go.GetComponentsInChildren<Transform>(true).Any(t=>t.name.StartsWith("DF_Signature")||t.name.StartsWith("DF_Legendary")||t.name.StartsWith("DF_EpicWeapon")),hero+": retired weapon geometry remains.");
            }
            finally {Object.Destroy(go);}
        }

        static void Require(bool condition,string message)
        {if(!condition)throw new InvalidOperationException("Runtime cleanup regression: "+message);}
    }
}
#endif
