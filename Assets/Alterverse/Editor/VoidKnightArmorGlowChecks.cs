#if UNITY_EDITOR
using System;
using Alterverse.Visuals;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.EditorTools
{
    public static class VoidKnightArmorGlowChecks
    {
        [MenuItem("DIMENSIONS FRONT/Checks/Void Knight armor glow (non-destructive)")]
        public static void Check()
        {
            foreach(string resource in new[]{"VoidArmorEnergy","VoidArmorHalo"})
            {
                Shader shader=Resources.Load<Shader>("HeroArt/"+resource);
                Require(shader!=null&&!ShaderUtil.ShaderHasError(shader),"Missing or invalid glow shader: "+resource);
            }
            GameObject asset=Resources.Load<GameObject>("AuthoredHeroes/VoidKnight");
            Require(asset!=null,"Missing Void Knight FBX.");
            GameObject instance=UnityEngine.Object.Instantiate(asset);
            instance.hideFlags=HideFlags.HideAndDontSave;
            try
            {
                SkinnedMeshRenderer body=null;
                foreach(var skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if(skin.name=="DF_Anatomy")body=skin;
                Require(body!=null,"Missing anatomy skin.");
                Mesh original=body.sharedMesh;
                Material[] before=body.sharedMaterials;
                VoidKnightArmorGlow.Apply(instance.transform);
                Require(instance.GetComponent<VoidKnightArmorGlow>()!=null,"Glow was not installed.");
                Require(body.sharedMesh==original,"Glow changed the original armor mesh.");
                int energy=0;
                for(int i=0;i<before.Length;i++)
                {
                    bool emissive=before[i]!=null&&before[i].name.StartsWith("Void ")&&before[i].name.ToLowerInvariant().Contains("energy");
                    if(!emissive){Require(body.sharedMaterials[i]==before[i],"A non-energy material changed.");continue;}
                    energy++;
                    Require(body.sharedMaterials[i].shader.name=="DimensionsFront/VoidArmorEnergy","Inlay still depends on Lit emission variants.");
                }
                Require(energy==2,"Expected two energy slots.");
                Transform child=body.transform.Find("DF_VoidArmorHalo");
                Require(child!=null,"Missing local halo.");
                var halo=child.GetComponent<SkinnedMeshRenderer>();
                Require(halo!=null&&halo.sharedMesh!=null&&halo.sharedMesh.vertexCount<original.vertexCount,"Halo did not compact the energy geometry.");
                Require(halo.sharedMesh.bindposes.Length==original.bindposes.Length&&halo.bones.Length==body.bones.Length,"Halo rig differs from armor rig.");
                Require(halo.shadowCastingMode==ShadowCastingMode.Off&&!halo.receiveShadows,"Halo must not render shadow passes.");
                for(int i=0;i<body.bones.Length;i++)Require(halo.bones[i]==body.bones[i],"Halo does not follow an original bone.");
                foreach(var weight in halo.sharedMesh.boneWeights)
                    Require(Mathf.Abs(weight.weight0+weight.weight1+weight.weight2+weight.weight3-1f)<.0001f,"Invalid halo skin weight.");
                int count=instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;
                VoidKnightArmorGlow.Apply(instance.transform);
                Require(instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length==count,"Duplicate glow on repeated initialization.");
                Debug.Log("DF: Void Knight self-lit inlays, compact skinned halo, bone bindings and shaders verified. LDR portrait and Android appearance require visual checks.");
            }
            finally{UnityEngine.Object.DestroyImmediate(instance);}
        }
        private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}
#endif
