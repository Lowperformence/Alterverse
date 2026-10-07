#if UNITY_EDITOR
using System;
using Alterverse.Visuals;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Alterverse.EditorChecks
{
    public sealed class SkinnedHotZoneSurfaceValidation:IPreprocessBuildWithReport
    {
        public int callbackOrder=>-75;
        public void OnPreprocessBuild(BuildReport report)=>Validate();

        [MenuItem("DIMENSIONS FRONT/Checks/Hot Zone skinned Blender surfaces")]
        public static void Validate()
        {
            foreach(string resource in new[]{"MarsSkeleton","GreenStalker","NoxaraClockworkSpider",
                "KryonBear","MechanicalScorpion","GJMagentaGolem","PSRCrystalTitan"})
            {
                ResourceCreatureMesh.Data data=ResourceCreatureMesh.Load(resource);
                Require(data!=null&&data.surface!=null,"Missing/invalid Blender UVs: "+resource);
                GameObject host=new("DF_ValidateSurface_"+resource);
                try
                {
                    foreach(string path in data.bonePaths)
                    {
                        if(string.IsNullOrEmpty(path))continue;
                        Transform parent=host.transform;
                        foreach(string segment in path.Split('/'))
                        {
                            Transform child=parent.Find(segment);
                            if(child==null){child=new GameObject(segment).transform;child.SetParent(parent,false);}
                            parent=child;
                        }
                    }
                    Color[] colors=new Color[data.submeshes.Length];
                    for(int i=0;i<colors.Length;i++)colors[i]=Color.white;
                    Require(ResourceCreatureMesh.Apply(host.transform,resource,colors)!=null,"Skin build failed: "+resource);
                    SkinnedMeshRenderer renderer=host.GetComponent<SkinnedMeshRenderer>();
                    Mesh mesh=renderer.sharedMesh;
                    Require(mesh.vertexCount==data.surface.sourceVertices.Length&&mesh.uv.Length==mesh.vertexCount&&
                        mesh.tangents.Length==mesh.vertexCount&&mesh.boneWeights.Length==mesh.vertexCount,
                        "Missing skin/UV/tangent data: "+resource);
                    for(int i=0;i<mesh.vertexCount;i++)
                    {
                        Vector4 tangent=mesh.tangents[i];
                        Require(float.IsFinite(tangent.x)&&float.IsFinite(tangent.y)&&float.IsFinite(tangent.z)&&
                            mesh.boneWeights[i].boneIndex0==data.boneIndices[data.surface.sourceVertices[i]],
                            "Invalid skin binding/tangent: "+resource);
                    }
                    string family=CreatureSurfaceUV.Family(resource);
                    Texture2D surface=Resources.Load<Texture2D>("AuthoredHotZone/"+family+"Surface");
                    Texture2D normal=Resources.Load<Texture2D>("AuthoredHotZone/"+family+"Normal");
                    Require(surface!=null&&normal!=null,"Missing monster textures: "+resource);
                    for(int slot=0;slot<renderer.sharedMaterials.Length;slot++)
                    {
                        Material material=renderer.sharedMaterials[slot];
                        Require((SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null||material.shader.isSupported)&&!ShaderUtil.ShaderHasError(material.shader),"Monster shader error: "+resource);
                        if(slot==3){Require(material.GetTexture("_BaseMap")!=surface,"Energy layer was textured: "+resource);continue;}
                        Require(material.GetTexture("_BaseMap")==surface&&material.GetTexture("_BumpMap")==normal,
                            "Monster textures not bound: "+resource+"/"+slot);
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(host); }
            }
            Debug.Log("All seven Hot Zone skinned surfaces passed UV, rig, shader and texture binding checks.");
        }

        static void Require(bool condition,string message)
        {if(!condition)throw new BuildFailedException(message);}
    }
}
#endif
