using System.Collections.Generic;
using UnityEngine;

namespace Alterverse.Visuals
{
    [DisallowMultipleComponent]
    public sealed class SkinOrnamentPresentation : MonoBehaviour
    {
        readonly List<Mesh> meshes=new();
        readonly List<Material> materials=new();
        public static void Apply(Transform model,Color? skinTint=null)
        {
            if(model.GetComponent<SkinOrnamentPresentation>()!=null)return;
            Shader shader=Resources.Load<Shader>("HeroArt/SkinOrnaments");
            if(shader==null)return;
            var owner=model.gameObject.AddComponent<SkinOrnamentPresentation>();
            foreach(var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if(renderer.name!="DF_Anatomy"||renderer.sharedMesh==null||!renderer.sharedMesh.isReadable)continue;
                Material[] slots=renderer.sharedMaterials;
                bool changed=false;
                for(int i=0;i<slots.Length;i++)
                {
                    Material original=slots[i];if(original==null)continue;
                    string name=original.name.ToLowerInvariant();
                    if(!name.Contains("skin")&&!name.Contains("flesh"))continue;
                    Color color=skinTint??(original.HasProperty("_BaseColor")?original.GetColor("_BaseColor"):
                        original.HasProperty("_Color")?original.GetColor("_Color"):Color.white);
                    Material copy=new Material(original){shader=shader,name=original.name+"_FineOrnaments"};
                    copy.SetColor("_BaseColor",color);
                    // Keep texture and shading; an optional hero tint also covers the hands and elf ears.
                    if(original.HasProperty("_MainTex")&&original.GetTexture("_MainTex")!=null)
                        copy.SetTexture("_BaseMap",original.GetTexture("_MainTex"));
                    owner.materials.Add(copy);slots[i]=copy;changed=true;
                }
                if(!changed)continue;
                Mesh mesh=Instantiate(renderer.sharedMesh);mesh.name=renderer.sharedMesh.name+"_SkinOrnaments";
                mesh.SetUVs(2,new List<Vector3>(mesh.vertices));
                owner.meshes.Add(mesh);renderer.sharedMesh=mesh;renderer.sharedMaterials=slots;
            }
        }
        void OnDestroy()
        {
            foreach(Mesh mesh in meshes)if(mesh!=null)Destroy(mesh);
            foreach(Material material in materials)if(material!=null)Destroy(material);
        }
    }
}
