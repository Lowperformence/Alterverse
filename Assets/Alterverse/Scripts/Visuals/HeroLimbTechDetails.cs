using System;
using System.Collections.Generic;
using Alterverse.Characters;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    /// <summary>Small fitted limb panels, with one renderer and no gameplay components.</summary>
    [DisallowMultipleComponent]
    public sealed class HeroLimbTechDetails : MonoBehaviour
    {
        Mesh mesh;
        readonly List<Material> materials=new();
        readonly List<Vector3> vertices=new();
        readonly List<BoneWeight> weights=new();
        readonly List<int>[] triangles={new(),new(),new()};

        public static void Apply(Transform model,HeroId hero)
        {
            if(model.GetComponent<HeroLimbTechDetails>()!=null)return;
            SkinnedMeshRenderer anatomy=null;
            foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if(skin.name=="DF_Anatomy"){anatomy=skin;break;}
            if(anatomy==null||anatomy.sharedMesh==null||!anatomy.sharedMesh.isReadable)return;
            var detail=model.gameObject.AddComponent<HeroLimbTechDetails>();
            detail.Build(anatomy,hero);
        }

        void Build(SkinnedMeshRenderer anatomy,HeroId hero)
        {
            Mesh source=anatomy.sharedMesh;
            Vector3[] points=source.vertices;
            BoneWeight[] skinWeights=source.boneWeights;
            Matrix4x4[] bind=source.bindposes;
            foreach(string side in new[]{"L","R"})
            {
                Panel("UpperArm"+side,"Forearm"+side,.64f,.16f);
                Panel("Forearm"+side,"Hand"+side,.52f,.32f);
                Panel("Thigh"+side,"Shin"+side,.58f,.20f);
                Panel("Shin"+side,"Foot"+side,.54f,.36f);
            }
            if(vertices.Count==0)return;
            Color accent=StylizedArtDirection.HeroAccent(hero);
            Material metal=RuntimeShaderLibrary.Create(new Color(.095f,.115f,.135f),false,.55f,.32f);
            Material circuits=RuntimeShaderLibrary.Create(Color.Lerp(accent,Color.white,.15f),false,.15f,.4f,accent*.8f);
            Material screws=RuntimeShaderLibrary.Create(new Color(.36f,.30f,.20f),false,.65f,.35f);
            materials.Add(metal);materials.Add(circuits);materials.Add(screws);
            if(metal==null||circuits==null||screws==null)return;
            metal.name="DF_LimbMicroArmor";circuits.name="DF_LimbCircuitAccents";screws.name="DF_LimbFasteners";
            mesh=new Mesh{name="DF_"+hero+"_LimbMicroDetails"};
            mesh.SetVertices(vertices);mesh.boneWeights=weights.ToArray();mesh.bindposes=bind;
            mesh.subMeshCount=3;
            for(int i=0;i<3;i++)mesh.SetTriangles(triangles[i],i);
            mesh.RecalculateNormals();mesh.RecalculateBounds();
            var go=new GameObject("DF_LimbTechDetails");go.layer=anatomy.gameObject.layer;
            go.transform.SetParent(anatomy.transform,false);
            var renderer=go.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh=mesh;renderer.sharedMaterials=materials.ToArray();
            renderer.bones=anatomy.bones;renderer.rootBone=anatomy.rootBone;
            Bounds bounds=anatomy.localBounds;bounds.Encapsulate(mesh.bounds);renderer.localBounds=bounds;
            renderer.quality=anatomy.quality;renderer.updateWhenOffscreen=anatomy.updateWhenOffscreen;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;
            // CPU construction buffers are no longer needed once the mesh owns its data.
            vertices.Clear();weights.Clear();foreach(var list in triangles)list.Clear();

            void Panel(string boneName,string nextName,float fraction,float span)
            {
                int bone=Array.FindIndex(anatomy.bones,b=>b!=null&&b.name==boneName);
                int next=Array.FindIndex(anatomy.bones,b=>b!=null&&b.name==nextName);
                if(bone<0||next<0||bone>=bind.Length||next>=bind.Length)return;
                Matrix4x4 toBone=bind[bone];
                Vector3 end=toBone.MultiplyPoint3x4(bind[next].inverse.MultiplyPoint3x4(Vector3.zero));
                float length=end.magnitude;if(length<.01f)return;
                Vector3 axis=end/length;
                Vector3 front=Vector3.ProjectOnPlane(toBone.MultiplyVector(Vector3.forward),axis).normalized;
                if(front.sqrMagnitude<.01f)front=Vector3.ProjectOnPlane(toBone.MultiplyVector(Vector3.right),axis).normalized;
                Vector3 across=Vector3.Cross(axis,front).normalized;
                float along=length*fraction;
                float face=float.NegativeInfinity,left=float.PositiveInfinity,right=float.NegativeInfinity;
                int samples=0;
                // Coarse imported meshes may have no ring at the panel midpoint.
                for(int pass=0;pass<2&&samples<3;pass++)
                {
                    face=float.NegativeInfinity;left=float.PositiveInfinity;right=float.NegativeInfinity;samples=0;
                for(int i=0;i<points.Length&&i<skinWeights.Length;i++)
                {
                    var w=skinWeights[i];
                    float influence=(w.boneIndex0==bone?w.weight0:0)+(w.boneIndex1==bone?w.weight1:0)
                        +(w.boneIndex2==bone?w.weight2:0)+(w.boneIndex3==bone?w.weight3:0);
                    if(influence<.5f)continue;
                    Vector3 p=toBone.MultiplyPoint3x4(points[i]);
                    if(Mathf.Abs(Vector3.Dot(p,axis)-along)>length*(pass==0?.19f:.55f))continue;
                    face=Mathf.Max(face,Vector3.Dot(p,front));
                    float horizontal=Vector3.Dot(p,across);left=Mathf.Min(left,horizontal);right=Mathf.Max(right,horizontal);samples++;
                }
                }
                if(samples<3||right-left<.005f)return;
                float width=Mathf.Clamp((right-left)*.38f,length*.12f,length*.32f);
                float height=length*span,depth=length*.022f;
                Vector3 center=axis*along+across*((left+right)*.5f)+front*(face+depth*.55f);
                Matrix4x4 toMesh=bind[bone].inverse;
                // A slim dark plate, a short circuit and three tiny raised contact pads.
                Box(center,new Vector3(width,height,depth),0);
                for(int j=0;j<3;j++)
                {
                    float y=(j-1)*height*.24f;
                    Box(center+axis*y+front*depth*.72f,new Vector3(width*.45f,height*.025f,depth*.35f),1);
                    Box(center+axis*y+across*width*.31f+front*depth*.72f,
                        new Vector3(width*.11f,height*.09f,depth*.42f),1);
                }
                foreach(int x in new[]{-1,1})foreach(int y in new[]{-1,1})
                    Box(center+across*(x*width*.39f)+axis*(y*height*.39f)+front*depth*.65f,
                        new Vector3(width*.07f,width*.07f,depth*.3f),2);

                void Box(Vector3 c,Vector3 size,int material)
                {
                    Vector3[] corners=new Vector3[8];int k=0;
                    for(int z=-1;z<=1;z+=2)for(int y=-1;y<=1;y+=2)for(int x=-1;x<=1;x+=2)
                        corners[k++]=c+across*(x*size.x*.5f)+axis*(y*size.y*.5f)+front*(z*size.z*.5f);
                    // Duplicate face vertices keep flat, crisp normals on these tiny details.
                    int[] faces={0,2,3,1,4,5,7,6,0,1,5,4,2,6,7,3,0,4,6,2,1,3,7,5};
                    for(int f=0;f<faces.Length;f+=4)
                    {
                        int start=vertices.Count;
                        for(int j=0;j<4;j++)
                        {vertices.Add(toMesh.MultiplyPoint3x4(corners[faces[f+j]]));weights.Add(new BoneWeight{boneIndex0=bone,weight0=1});}
                        triangles[material].AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
                    }
                }
            }
        }
        void OnDestroy()
        {
            if(mesh!=null)Destroy(mesh);
            foreach(Material material in materials)if(material!=null)Destroy(material);
        }
    }
}
