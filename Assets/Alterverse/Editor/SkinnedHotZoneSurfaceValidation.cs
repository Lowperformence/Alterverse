#if UNITY_EDITOR
using Alterverse.Visuals;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Alterverse.EditorChecks
{
    public sealed class SkinnedHotZoneSurfaceValidation:IPreprocessBuildWithReport
    {
        private static readonly string[] ResourcesToValidate=
        {
            "MarsSkeleton","GreenStalker","NoxaraClockworkSpider",
            "KryonBear","MechanicalScorpion","GJMagentaGolem","PSRCrystalTitan"
        };

        public int callbackOrder=>-75;

        public void OnPreprocessBuild(BuildReport report)
        {
            // Build callbacks only check asset registration. Constructing every monster
            // and reading native mesh buffers belongs to the explicit diagnostic menu.
            foreach(string resource in ResourcesToValidate)
            {
                RequireAsset("Assets/Alterverse/Resources/"+resource+"/Model.bytes");
                RequireAsset("Assets/Alterverse/Resources/"+resource+"/SkinUV.bytes");
                string family=CreatureSurfaceUV.Family(resource);
                RequireAsset("Assets/Alterverse/Resources/AuthoredHotZone/"+family+"Surface.png");
                RequireAsset("Assets/Alterverse/Resources/AuthoredHotZone/"+family+"Normal.png");
            }
            Debug.Log("DIMENSIONS FRONT: Hot Zone surface asset preflight passed.");
        }

        private static void RequireAsset(string path)
        {
            Require(!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)),
                "Missing Hot Zone surface asset: "+path);
        }

        [MenuItem("DIMENSIONS FRONT/Checks/Hot Zone skinned Blender surfaces")]
        public static void Validate()
        {
            foreach(string resource in ResourcesToValidate)
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
                    // Unity's array-valued mesh getters copy the entire buffer.
                    // Read once so this per-vertex check stays linear in mesh size.
                    int vertexCount=mesh.vertexCount;
                    Vector2[] uv=mesh.uv;
                    Vector4[] tangents=mesh.tangents;
                    BoneWeight[] boneWeights=mesh.boneWeights;
                    Require(vertexCount==data.surface.sourceVertices.Length&&uv.Length==vertexCount&&
                        tangents.Length==vertexCount&&boneWeights.Length==vertexCount,
                        "Missing skin/UV/tangent data: "+resource);
                    for(int i=0;i<vertexCount;i++)
                    {
                        Vector4 tangent=tangents[i];
                        Require(float.IsFinite(tangent.x)&&float.IsFinite(tangent.y)&&float.IsFinite(tangent.z)&&
                            boneWeights[i].boneIndex0==data.boneIndices[data.surface.sourceVertices[i]],
                            "Invalid skin binding/tangent: "+resource);
                    }
                    string family=CreatureSurfaceUV.Family(resource);
                    Texture2D surface=Resources.Load<Texture2D>("AuthoredHotZone/"+family+"Surface");
                    Texture2D normal=Resources.Load<Texture2D>("AuthoredHotZone/"+family+"Normal");
                    Require(surface!=null&&normal!=null,"Missing monster textures: "+resource);
                    Material[] materials=renderer.sharedMaterials;
                    for(int slot=0;slot<materials.Length;slot++)
                    {
                        Material material=materials[slot];
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
