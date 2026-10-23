using Alterverse.Characters;
using Alterverse.Shop;
using System.Collections.Generic;
using UnityEngine;

namespace Alterverse.Visuals
{
    [DisallowMultipleComponent]
    public sealed class HeadwearPresentation : MonoBehaviour
    {
        Transform visualRoot;
        GameObject mounted;
        Mesh mesh;
        Material[] materials;
        public Transform Head {get;private set;}
        public Bounds HeadBounds {get;private set;}
        public Vector3 SeatInHeadSpace {get;private set;}
        public string PreviewResource {get;private set;}
        public UniversalHeroCosmetic Current {get;private set;}
        public string LastFitError {get;private set;}

        public static void Configure(GameObject actor,Transform root,HeroId hero,SkinPreviewSelection preview)
        {
            var view=actor.GetComponent<HeadwearPresentation>()??actor.AddComponent<HeadwearPresentation>();
            view.visualRoot=root;
            var selected=preview!=null?preview.Headwear:
                LocalHeroContext.IsLocal(actor)?HeadwearSelection.EquippedFor(hero):UniversalHeroCosmetic.Original;
            view.Apply(selected);
        }

        // Also used for remote, server-relayed cosmetic state. Never changes ownership.
        public bool Apply(UniversalHeroCosmetic selected)
        {
            if(!HeadwearSelection.Supported(selected))return false;
            if(PreviewResource==null&&selected==Current&&
                (selected==UniversalHeroCosmetic.Original&&mounted==null||selected!=UniversalHeroCosmetic.Original&&mounted!=null))return true;
            return ApplyVisual(selected,null);
        }

#if UNITY_EDITOR
        // Test models are preview-only until their unlock path has been chosen.
        // This method never grants ownership, writes a profile or changes the network cosmetic IDs.
        public bool ApplyPrototype(string resource)
        {
            if(resource!="WizardHat"&&resource!="PropellerCap"&&resource!="MushroomHat")return false;
            if(PreviewResource==resource&&mounted!=null)return true;
            return ApplyVisual(UniversalHeroCosmetic.Original,resource);
        }
#endif
        bool ApplyVisual(UniversalHeroCosmetic selected,string prototype)
        {
            Clear();
            if(selected==UniversalHeroCosmetic.Original&&prototype==null)return true;
            if(visualRoot==null)visualRoot=transform.Find("DF_VisualRoot");
            Transform head=null;Bounds bounds=default;Vector3 seat=default;string error=null;
            if(visualRoot==null||!TryFindHead(visualRoot,out head,out bounds,out seat,out error))
            {LastFitError=visualRoot==null?"Visual root not ready.":error;return false;}
            Shader shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
            if(shader==null){LastFitError="Headwear material shader unavailable.";return false;}
            Head=head;HeadBounds=bounds;
            Vector3 size=bounds.size;
            bool ufo=selected==UniversalHeroCosmetic.UfoTestHat;
            bool asset=ufo||prototype!=null;
            Vector3 scale=asset?new Vector3(size.x*.54f,Mathf.Min(size.x,size.z)*.54f,size.z*.60f):new Vector3(size.x*.54f,size.y*.45f,size.z*.56f);
            Matrix4x4 fit=head.worldToLocalMatrix*visualRoot.localToWorldMatrix*Matrix4x4.TRS(seat,Quaternion.identity,scale);
            SeatInHeadSpace=fit.MultiplyPoint3x4(Vector3.zero);
            if(asset)
            {
                if(!UfoHatMesh.TryBuildResource(prototype??"UfoTestHat",fit,shader,out mesh,out materials,out string message))
                {LastFitError=message;return false;}
            }
            else
            {
            mesh=BetaCapMesh.Build(fit);
            Color[] colors={new Color(.055f,.085f,.15f),new Color(.28f,.72f,.94f),new Color(.94f,.96f,1f)};
            materials=new Material[colors.Length];
            for(int i=0;i<materials.Length;i++)
            {
                var material=new Material(shader){name="DF_BetaCap_"+i};
                if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",colors[i]);
                if(material.HasProperty("_Color"))material.SetColor("_Color",colors[i]);
                if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",i==0?.18f:.3f);
                materials[i]=material;
            }
            }
            mounted=new GameObject("DF_Headwear_"+(prototype??(ufo?"UfoTestHat":"BetaCap")));mounted.layer=visualRoot.gameObject.layer;
            mounted.transform.SetParent(head,false);
            mounted.AddComponent<MeshFilter>().sharedMesh=mesh;
            mounted.AddComponent<MeshRenderer>().sharedMaterials=materials;
            Current=selected;PreviewResource=prototype;LastFitError=null;
            return true;
        }

        public static bool TryFindHead(Transform root,out Transform head,out Bounds bounds,out string error)
            =>TryFindHead(root,out head,out bounds,out _,out error);

        static bool TryFindHead(Transform root,out Transform head,out Bounds bounds,out Vector3 seat,out string error)
        {
            head=null;bounds=default;seat=default;error=null;bool found=false;
            var surface=new List<Vector3>();
            foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Mesh source=skin.sharedMesh;
                if(source==null||!source.isReadable)continue;
                var bones=skin.bones;int index=-1;
                for(int i=0;i<bones.Length;i++)if(bones[i]!=null&&bones[i].name=="Head") {index=i;break;}
                if(index<0||head!=null&&head!=bones[index])continue;
                var weights=source.boneWeights;
                if(weights.Length!=source.vertexCount)continue;
                var baked=new Mesh();
                try
                {
                    skin.BakeMesh(baked);
                    var points=baked.vertices;
                    Matrix4x4 convert=root.worldToLocalMatrix*skin.transform.localToWorldMatrix;
                    for(int i=0;i<points.Length&&i<weights.Length;i++)
                    {
                        BoneWeight w=weights[i];
                        float weight=(w.boneIndex0==index?w.weight0:0)+(w.boneIndex1==index?w.weight1:0)+
                            (w.boneIndex2==index?w.weight2:0)+(w.boneIndex3==index?w.weight3:0);
                        if(weight<.5f)continue;
                        Vector3 point=convert.MultiplyPoint3x4(points[i]);
                        if(!Finite(point))continue;
                        surface.Add(point);
                        if(!found){bounds=new Bounds(point,Vector3.zero);found=true;}else bounds.Encapsulate(point);
                        head=bones[index];
                    }
                }
                finally{Release(baked);}
            }
            if(!found)
            {
                foreach(Transform part in root.GetComponentsInChildren<Transform>(true))
                {
                    if(part.name!="DF_Head")continue;
                    Mesh source=part.GetComponent<MeshFilter>()?.sharedMesh;
                    if(source==null)continue;
                    Bounds local=source.bounds;
                    Matrix4x4 convert=root.worldToLocalMatrix*part.localToWorldMatrix;
                    for(int i=0;i<8;i++)
                    {
                        Vector3 p=local.center+Vector3.Scale(local.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                        p=convert.MultiplyPoint3x4(p);
                        surface.Add(p);
                        if(!found){bounds=new Bounds(p,Vector3.zero);found=true;}else bounds.Encapsulate(p);
                    }
                    head=part;break;
                }
            }
            if(!found||head==null||!Finite(bounds.size)||bounds.size.x<=.01f||bounds.size.y<=.01f||bounds.size.z<=.01f)
            {error="No readable head geometry and matching head bone found.";return false;}
            // Center on the upper contour, not on chin, face, fins or the whole-body pivot.
            // The mesh's head-seat plane meets the actual top with no fixed floating offset.
            bool upper=false;Bounds crown=default;
            float threshold=bounds.max.y-bounds.size.y*.10f;
            foreach(Vector3 point in surface)
                if(point.y>=threshold)
                {if(!upper){crown=new Bounds(point,Vector3.zero);upper=true;}else crown.Encapsulate(point);}
            seat=new Vector3(crown.center.x,bounds.max.y,crown.center.z);
            return true;
        }
        static bool Finite(Vector3 v)=>!float.IsNaN(v.x)&&!float.IsInfinity(v.x)&&
            !float.IsNaN(v.y)&&!float.IsInfinity(v.y)&&!float.IsNaN(v.z)&&!float.IsInfinity(v.z);
        static void Release(Object value){if(value==null)return;if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
        void Clear()
        {
            if(mounted!=null){mounted.SetActive(false);Release(mounted);}
            Release(mesh);
            if(materials!=null)foreach(var material in materials)Release(material);
            mounted=null;mesh=null;materials=null;PreviewResource=null;Current=UniversalHeroCosmetic.Original;
        }
        void OnDestroy()=>Clear();
    }
}

