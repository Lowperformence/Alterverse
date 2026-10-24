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
        Transform referenceHead;
        Matrix4x4 referenceToHead;
        static readonly float[] WizardHeights={0f,.08f,.40f,1.08f,1.82f,2.42f,2.80f,2.95f};
        static readonly float[] WizardRadii={1f,.94f,.89f,.71f,.52f,.30f,.15f,.06f};
        static readonly float[] WizardOffsets={0f,0f,0f,0f,.05f,.16f,.35f,.62f};
        static readonly float[] MushroomHeights={0f,.20f,.31f,.49f,.82f,1.15f,1.37f,1.43f};
        static readonly float[] MushroomRadii={.98f,1f,1.71f,1.70f,1.48f,1.10f,.55f,.015f};
        public Transform Head {get;private set;}
        public Bounds HeadBounds {get;private set;}
        public Vector3 SeatInHeadSpace {get;private set;}
        public Bounds CrownSection {get;private set;}
        public float ReferenceWidth {get;private set;}
        public UniversalHeroCosmetic Current {get;private set;}
        public string LastFitError {get;private set;}

        public static void Configure(GameObject actor,Transform root,HeroId hero,SkinPreviewSelection preview)
        {
            var view=actor.GetComponent<HeadwearPresentation>()??actor.AddComponent<HeadwearPresentation>();
            view.visualRoot=root;
            TryReferenceFrame(root,out view.referenceHead,out view.referenceToHead);
            var selected=preview!=null?preview.Headwear:
                LocalHeroContext.IsLocal(actor)?HeadwearSelection.EquippedFor(hero):UniversalHeroCosmetic.Original;
            view.Apply(selected);
        }

        // Also used for remote, server-relayed cosmetic state. Never changes ownership.
        public bool Apply(UniversalHeroCosmetic selected)
        {
            if(!HeadwearSelection.Supported(selected))return false;
            if(selected==Current&&
                (selected==UniversalHeroCosmetic.Original&&mounted==null||selected!=UniversalHeroCosmetic.Original&&mounted!=null))return true;
            return ApplyVisual(selected);
        }

#if UNITY_EDITOR
        // Kept for existing editor scripts; uses exactly the production equip renderer.
        public bool ApplyPrototype(string resource)
        {
            foreach(var id in new[]{UniversalHeroCosmetic.WizardHat,UniversalHeroCosmetic.PropellerCap,UniversalHeroCosmetic.MushroomHat})
                if(HeadwearSelection.ResourceFor(id)==resource)return Apply(id);
            return false;
        }
#endif
        bool ApplyVisual(UniversalHeroCosmetic selected)
        {
            Clear();
            if(selected==UniversalHeroCosmetic.Original)return true;
            if(visualRoot==null)visualRoot=transform.Find("DF_VisualRoot");
            Transform head=null;Bounds bounds=default,crown=default;List<Vector3> surface=null;string error=null;
            if(visualRoot==null||!TryFindHead(visualRoot,out head,out bounds,out crown,out surface,out error,referenceHead,referenceToHead))
            {LastFitError=visualRoot==null?"Visual root not ready.":error;return false;}
            Shader shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
            if(shader==null){LastFitError="Headwear material shader unavailable.";return false;}
            Head=head;HeadBounds=bounds;CrownSection=crown;
            string resource=HeadwearSelection.ResourceFor(selected);
            bool asset=selected!=UniversalHeroCosmetic.BetaCap;
            if(!FitOpening(selected,bounds,crown,surface,out Vector3 scale))
            {LastFitError="Head shape needs a manual headwear fit.";return false;}
            Vector3 referenceScale=scale;
            if(selected!=UniversalHeroCosmetic.UfoTestHat&&
                !FitOpening(UniversalHeroCosmetic.UfoTestHat,bounds,crown,surface,out referenceScale))
            {LastFitError="Head shape needs a manual UFO reference fit.";return false;}
            if(!UfoHatMesh.TryGetBounds("UfoTestHat",out Bounds referenceBounds,out string referenceError))
            {LastFitError=referenceError;return false;}
            // The approved reference is 25% larger than the previous one-third UFO.
            float width=referenceBounds.size.x*referenceScale.x*(1.25f/3f);
            if(asset)
            {
                if(!UfoHatMesh.TryBuildResource(resource,Matrix4x4.identity,shader,out mesh,out materials,out string message))
                {LastFitError=message;return false;}
            }
            else
            {
            mesh=BetaCapMesh.Build(Matrix4x4.identity);
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
            // Match the full model width, keeping each hat's fitted proportions.
            // Scale its crown insertion by the same amount so small hats don't sink.
            float size=width/(mesh.bounds.size.x*scale.x);
            scale*=size;
            Vector3 seat=crown.center;
            seat.y=bounds.max.y+(seat.y-bounds.max.y)*size;
            if(referenceHead==null)TryReferenceFrame(visualRoot,out referenceHead,out referenceToHead);
            Matrix4x4 fit=referenceToHead*Matrix4x4.TRS(seat,Quaternion.identity,scale);
            SeatInHeadSpace=fit.MultiplyPoint3x4(Vector3.zero);ReferenceWidth=width;
            var fitted=mesh.vertices;
            for(int i=0;i<fitted.Length;i++)fitted[i]=fit.MultiplyPoint3x4(fitted[i]);
            mesh.vertices=fitted;mesh.RecalculateNormals();mesh.RecalculateBounds();
            mounted=new GameObject("DF_Headwear_"+resource);mounted.layer=visualRoot.gameObject.layer;
            mounted.transform.SetParent(head,false);
            mounted.AddComponent<MeshFilter>().sharedMesh=mesh;
            mounted.AddComponent<MeshRenderer>().sharedMaterials=materials;
            Current=selected;LastFitError=null;
            return true;
        }

        public static bool TryFindHead(Transform root,out Transform head,out Bounds bounds,out string error)
            =>TryFindHead(root,out head,out bounds,out _,out _,out error);

        static bool TryFindHead(Transform root,out Transform head,out Bounds bounds,out Bounds crown,out List<Vector3> surface,out string error,
            Transform knownHead=null,Matrix4x4 knownFrame=default)
        {
            head=null;bounds=default;crown=default;error=null;bool found=false;
            surface=new List<Vector3>();
            if(knownHead==null&&!TryReferenceFrame(root,out knownHead,out knownFrame))
            {error="No readable head geometry and bind frame found.";return false;}
            head=knownHead;
            foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if(!skin.enabled||!skin.gameObject.activeInHierarchy||Decoration(skin.transform,root))continue;
                Mesh source=skin.sharedMesh;
                if(source==null||!source.isReadable)continue;
                var bones=skin.bones;int index=-1;
                for(int i=0;i<bones.Length;i++)if(bones[i]!=null&&bones[i].name=="Head") {index=i;break;}
                if(index<0||head!=null&&head!=bones[index])continue;
                var weights=source.boneWeights;
                if(weights.Length!=source.vertexCount)continue;
                // Removed hair/flowers can remain in a mesh's vertex buffer. Only indexed
                // geometry is visible and may define the hat's contact surface.
                var used=ReferencedVertices(source,skin.sharedMaterials);
                {
                    // Use visible bind-pose geometry: a hat arriving over the network
                    // while the head is tilted must not bake that temporary pose into its fit.
                    var points=source.vertices;
                    Matrix4x4 convert=root.worldToLocalMatrix*skin.transform.localToWorldMatrix;
                    for(int i=0;i<points.Length&&i<weights.Length;i++)
                    {
                        if(!used[i])continue;
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
            }
            if(head==null)
                foreach(Transform part in root.GetComponentsInChildren<Transform>(true))
                    if(part.name=="Head"){head=part;break;}
            // Helmets and hair can be rigid meshes attached to the bone rather than
            // weighted anatomy. Include their visible surface; a floating halo is not a skull.
            if(head!=null)
                foreach(var filter in head.GetComponentsInChildren<MeshFilter>(true))
                {
                    var renderer=filter.GetComponent<MeshRenderer>();
                    if(renderer==null||!renderer.enabled||!filter.gameObject.activeInHierarchy||Decoration(filter.transform,head))continue;
                    Mesh source=filter.sharedMesh;
                    if(source==null||!source.isReadable)continue;
                    var used=ReferencedVertices(source,renderer.sharedMaterials);var points=source.vertices;
                    Matrix4x4 convert=knownFrame.inverse*head.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                    for(int i=0;i<points.Length;i++)
                    {
                        if(!used[i])continue;
                        Vector3 point=convert.MultiplyPoint3x4(points[i]);
                        if(!Finite(point))continue;
                        surface.Add(point);
                        if(!found){bounds=new Bounds(point,Vector3.zero);found=true;}else bounds.Encapsulate(point);
                    }
                }
            if(!found)
            {
                foreach(Transform part in root.GetComponentsInChildren<Transform>(true))
                {
                    if(part.name!="DF_Head")continue;
                    var renderer=part.GetComponent<MeshRenderer>();
                    if(renderer==null||!renderer.enabled||!part.gameObject.activeInHierarchy)continue;
                    Mesh source=part.GetComponent<MeshFilter>()?.sharedMesh;
                    if(source==null)continue;
                    Bounds local=source.bounds;
                    Matrix4x4 convert=knownFrame.inverse*part.worldToLocalMatrix*part.localToWorldMatrix;
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
            // A hollow hat must surround the upper crown: placing its empty opening at
            // the absolute apex leaves the entire hat floating. Fit the inner cavity below.
            bool upper=false;
            float threshold=bounds.max.y-bounds.size.y*.20f;
            foreach(Vector3 point in surface)
                if(point.y>=threshold)
                {if(!upper){crown=new Bounds(point,Vector3.zero);upper=true;}else crown.Encapsulate(point);}
            crown=new Bounds(new Vector3(crown.center.x,threshold,crown.center.z),new Vector3(crown.size.x,0,crown.size.z));
            return true;
        }
        static bool TryReferenceFrame(Transform root,out Transform head,out Matrix4x4 frame)
        {
            head=null;frame=Matrix4x4.identity;if(root==null)return false;
            foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Mesh mesh=skin.sharedMesh;if(mesh==null||!mesh.isReadable)continue;
                var bones=skin.bones;var binds=mesh.bindposes;
                for(int i=0;i<bones.Length&&i<binds.Length;i++)
                    if(bones[i]!=null&&bones[i].name=="Head")
                    {head=bones[i];frame=binds[i]*skin.transform.worldToLocalMatrix*root.localToWorldMatrix;return true;}
            }
            foreach(Transform part in root.GetComponentsInChildren<Transform>(true))
                if(part.name=="DF_Head"&&part.GetComponent<MeshFilter>()!=null&&
                    part.GetComponent<MeshRenderer>()!=null&&part.GetComponent<MeshRenderer>().enabled&&part.gameObject.activeInHierarchy)
                {head=part;frame=part.worldToLocalMatrix*root.localToWorldMatrix;return true;}
            return false;
        }
        static bool FitOpening(UniversalHeroCosmetic selected,Bounds head,Bounds crown,List<Vector3> points,out Vector3 scale)
        {
            float xRadius=selected==UniversalHeroCosmetic.BetaCap?.975f:
                selected==UniversalHeroCosmetic.PropellerCap||selected==UniversalHeroCosmetic.MushroomHat?.98f:1f;
            float depth=selected==UniversalHeroCosmetic.BetaCap?.94f:selected==UniversalHeroCosmetic.UfoTestHat?.9f:1f;
            float sx=Mathf.Max(head.size.x*.15f,crown.extents.x)/xRadius;
            float sz=Mathf.Max(head.size.z*.15f,crown.extents.z)/(xRadius*depth);
            var basis=new Vector3(sx,Mathf.Max(head.size.y*.25f,Mathf.Min(sx,sz)*.65f),sz);
            bool Fits(float factor)
            {
                foreach(Vector3 point in points)
                {
                    if(point.y<crown.center.y)continue;
                    Vector3 relative=point-crown.center;
                    float y=relative.y/(basis.y*factor),allowed=InnerRadius(selected,y);
                    if(allowed<=0)return false;
                    float x=relative.x/(basis.x*factor),z=relative.z/(basis.z*factor*depth);
                    if(selected==UniversalHeroCosmetic.WizardHat)x-=ProfileRadius(y,WizardHeights,WizardOffsets);
                    if(x*x+z*z>allowed*allowed)return false;
                }
                return true;
            }
            float low=0f,high=1f;
            while(high<8f&&!Fits(high))high*=2f;
            if(!Fits(high)){scale=default;return false;}
            // Smallest enclosing fit keeps the inner surface in contact with the crown,
            // rather than fitting an arbitrary oversized bounding box around the skull.
            for(int i=0;i<20;i++)
            {float mid=(low+high)*.5f;if(Fits(mid))high=mid;else low=mid;}
            scale=basis*(high*1.005f); // Half-percent shell clearance, below a visible floating gap.
            return Finite(scale);
        }
        static float InnerRadius(UniversalHeroCosmetic selected,float y)
        {
            if(y<0)return 1f;
            if(selected==UniversalHeroCosmetic.BetaCap)
            {float t=y/1.05f;return t>=1?0:1-.08f*t-.92f*t*t*t*t-.025f;}
            if(selected==UniversalHeroCosmetic.PropellerCap)
                return y>=.99f?0:Mathf.Sqrt(Mathf.Max(0,1-y*y))-.02f;
            if(selected==UniversalHeroCosmetic.UfoTestHat)
            {
                if(y<=.30f)return 1f;
                if(y<=.724f)return 1f+(y-.30f)*1.01f;
                float t=(y-.724f)/1.10f;
                return t>=1?0:1.43f*Mathf.Sqrt(Mathf.Max(0,1-t*t))-.02f;
            }
            if(selected==UniversalHeroCosmetic.WizardHat)
                return ProfileRadius(y,WizardHeights,WizardRadii);
            if(selected==UniversalHeroCosmetic.MushroomHat)
                return ProfileRadius(y,MushroomHeights,MushroomRadii);
            return 0;
        }
        static float ProfileRadius(float y,float[] heights,float[] radii)
        {
            for(int i=1;i<heights.Length;i++)
                if(y<=heights[i])
                {float t=(y-heights[i-1])/(heights[i]-heights[i-1]);return radii[i-1]+(radii[i]-radii[i-1])*t;}
            return 0;
        }
        static bool[] ReferencedVertices(Mesh source,Material[] slots)
        {
            var used=new bool[source.vertexCount];
            for(int sub=0;sub<source.subMeshCount;sub++)
            {
                // HeadAdornmentSkin merges Tahla's halo into DF_Anatomy. It keeps
                // its own material slot even though its vertices have head weights.
                // Skin_FineOrnaments is genuine anatomy and must remain eligible.
                string name=slots!=null&&sub<slots.Length&&slots[sub]!=null?slots[sub].name:null;
                if(name!=null&&(name.IndexOf("Halo",System.StringComparison.OrdinalIgnoreCase)>=0||
                    name.IndexOf("Headwear",System.StringComparison.OrdinalIgnoreCase)>=0||
                    name.IndexOf("VFX",System.StringComparison.OrdinalIgnoreCase)>=0))continue;
                foreach(int index in source.GetTriangles(sub))if(index>=0&&index<used.Length)used[index]=true;
            }
            return used;
        }
        static bool Decoration(Transform part,Transform head)
        {
            for(Transform node=part;node!=null&&node!=head;node=node.parent)
                if(node.name.IndexOf("Headwear",System.StringComparison.OrdinalIgnoreCase)>=0||
                   node.name.IndexOf("Halo",System.StringComparison.OrdinalIgnoreCase)>=0||
                   node.name.IndexOf("Ornament",System.StringComparison.OrdinalIgnoreCase)>=0||
                   node.name.IndexOf("VFX",System.StringComparison.OrdinalIgnoreCase)>=0)return true;
            return false;
        }
        static bool Finite(Vector3 v)=>!float.IsNaN(v.x)&&!float.IsInfinity(v.x)&&
            !float.IsNaN(v.y)&&!float.IsInfinity(v.y)&&!float.IsNaN(v.z)&&!float.IsInfinity(v.z);
        static void Release(Object value){if(value==null)return;if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
        void Clear()
        {
            if(mounted!=null){mounted.SetActive(false);Release(mounted);}
            Release(mesh);
            if(materials!=null)foreach(var material in materials)Release(material);
            mounted=null;mesh=null;materials=null;ReferenceWidth=0;Current=UniversalHeroCosmetic.Original;
        }
        void OnDestroy()=>Clear();
    }
}


