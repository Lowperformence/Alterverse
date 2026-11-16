using System;
using System.Collections.Generic;
using Alterverse.Characters;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    /// <summary>Fitted front and rear limb & dorsal armor panels, with one renderer and no gameplay components.</summary>
    [DisallowMultipleComponent]
    public sealed class HeroLimbTechDetails : MonoBehaviour
    {
        Mesh mesh;
        readonly List<Material> materials=new();
        readonly List<Vector3> vertices=new();
        readonly List<Vector3> normals=new();
        readonly List<Vector2> uvs=new();
        readonly List<BoneWeight> weights=new();
        readonly List<int>[] triangles={new(),new(),new()};

        public static void Apply(Transform model,HeroId hero)
        {
            // Brakk now keeps smooth arms and legs; his equipment has separate authored parts.
            if(hero==HeroId.Rook||model==null)return;
            // Tahla Venn has her own full front-and-back Royal Sanctuary armor suite.
            if(hero==HeroId.Mira&&model.GetComponent<HealerLegArmorAppearance>()!=null)return;
            if(model.GetComponent<HeroLimbTechDetails>()!=null)return;
            SkinnedMeshRenderer anatomy=null;
            foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if(skin.name=="DF_Anatomy"){anatomy=skin;break;}
            if(anatomy==null||anatomy.sharedMesh==null||!anatomy.sharedMesh.isReadable)return;
            var detail=model.gameObject.AddComponent<HeroLimbTechDetails>();
            detail.Build(model,anatomy,hero);
        }

        void Build(Transform model,SkinnedMeshRenderer anatomy,HeroId hero)
        {
            Mesh source=anatomy.sharedMesh;
            Vector3[] points=source.vertices;
            BoneWeight[] skinWeights=source.boneWeights;
            Matrix4x4[] bind=source.bindposes;
            Matrix4x4 modelToMesh=anatomy.transform.worldToLocalMatrix*model.localToWorldMatrix;
            Vector3 meshForward=modelToMesh.MultiplyVector(Vector3.forward).normalized;
            if(meshForward.sqrMagnitude<.01f)meshForward=Vector3.forward;

            bool hasComponentFront=
                (hero==HeroId.Nyra&&model.GetComponent<CyberRangerAppearance>()!=null)||
                (hero==HeroId.Elyra&&model.GetComponent<UmbralMageAppearance>()!=null)||
                (hero==HeroId.Traveler&&model.GetComponent<ChronoTravelerAppearance>()!=null);

            // Dorsal Torso & Lumbar Back Armor Plates (so no hero has an empty back)
            Panel("Chest","Spine",.38f,.42f,-1f,.54f);
            Panel("Spine","Hips",.48f,.44f,-1f,.46f);
            Panel("Hips","Spine",.18f,.32f,-1f,.42f);

            foreach(string side in new[]{"L","R"})
            {
                if(!hasComponentFront)
                {
                    if(hero!=HeroId.Rook)
                    {
                        Panel("UpperArm"+side,"Forearm"+side,.64f,.16f,1f,.38f);
                        Panel("Forearm"+side,"Hand"+side,.52f,.32f,1f,.38f);
                    }
                    Panel("Thigh"+side,"Shin"+side,.58f,.20f,1f,.38f);
                    Panel("Shin"+side,"Foot"+side,.54f,.36f,1f,.38f);
                }

                // Contoured Rear Limb Armor (Tricep, Rear Forearm, Hamstring, and Calf plates)
                if(hero!=HeroId.Rook)
                {
                    Panel("UpperArm"+side,"Forearm"+side,.52f,.26f,-1f,.44f);
                    Panel("Forearm"+side,"Hand"+side,.48f,.34f,-1f,.42f);
                }
                Panel("Thigh"+side,"Shin"+side,.38f,.24f,-1f,.48f);
                Panel("Thigh"+side,"Shin"+side,.68f,.22f,-1f,.44f);
                Panel("Shin"+side,"Foot"+side,.36f,.26f,-1f,.46f);
                Panel("Shin"+side,"Foot"+side,.66f,.24f,-1f,.42f);
            }
            if(vertices.Count==0)return;
            Color accent= hero == HeroId.Nyra ? new Color(.025f,.72f,.40f) : StylizedArtDirection.HeroAccent(hero);
            Color plateColor = hero switch
            {
                HeroId.Elyra => new Color(.22f,.19f,.29f),
                HeroId.Kaelon => new Color(.38f,.35f,.22f),
                HeroId.Mira => new Color(.19f,.30f,.25f),
                HeroId.Nerok => new Color(.43f,.47f,.49f),
                HeroId.Nyra => new Color(.085f,.120f,.125f),
                HeroId.Traveler => new Color(.23f,.26f,.29f),
                HeroId.Veyr => new Color(.055f,.050f,.075f),
                _ => new Color(.095f,.115f,.135f)
            };
            Material metal=RuntimeShaderLibrary.Create(plateColor,false,hero==HeroId.Nyra?.68f:hero==HeroId.Rook?.55f:.52f,hero==HeroId.Nyra?.56f:hero==HeroId.Rook?.32f:.44f);
            Material circuits=RuntimeShaderLibrary.Create(Color.Lerp(accent,Color.white,.15f),false,.15f,.45f,accent*.85f);
            Color fastenerColor = hero == HeroId.Rook || hero == HeroId.Kaelon || hero == HeroId.Mira
                ? new Color(.36f,.30f,.20f)
                : hero == HeroId.Nyra ? new Color(.46f,.57f,.57f)
                : new Color(.42f,.45f,.49f);
            Material screws=RuntimeShaderLibrary.Create(fastenerColor,false,.72f,.46f);
            materials.Add(metal);materials.Add(circuits);materials.Add(screws);
            if(metal==null||circuits==null||screws==null)return;
            metal.name="DF_LimbMicroArmor";circuits.name="DF_LimbCircuitAccents";screws.name="DF_LimbFasteners";
            mesh=new Mesh{name="DF_"+hero+"_LimbMicroDetails"};
            mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uvs);
            mesh.boneWeights=weights.ToArray();mesh.bindposes=bind;
            mesh.subMeshCount=3;
            for(int i=0;i<3;i++)mesh.SetTriangles(triangles[i],i);
            mesh.RecalculateBounds();mesh.RecalculateTangents();
            var go=new GameObject("DF_LimbTechDetails");go.layer=anatomy.gameObject.layer;
            go.transform.SetParent(anatomy.transform,false);
            var renderer=go.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh=mesh;renderer.sharedMaterials=materials.ToArray();
            renderer.bones=anatomy.bones;renderer.rootBone=anatomy.rootBone;
            Bounds bounds=anatomy.localBounds;bounds.Encapsulate(mesh.bounds);renderer.localBounds=bounds;
            renderer.quality=anatomy.quality;renderer.updateWhenOffscreen=anatomy.updateWhenOffscreen;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;
            vertices.Clear();normals.Clear();uvs.Clear();weights.Clear();foreach(var list in triangles)list.Clear();

            void Panel(string boneName,string nextName,float fraction,float span,float facingSign,float widthFactor)
            {
                int bone=Array.FindIndex(anatomy.bones,b=>b!=null&&b.name==boneName);
                int next=Array.FindIndex(anatomy.bones,b=>b!=null&&b.name==nextName);
                if(bone<0||next<0||bone>=bind.Length||next>=bind.Length)return;
                Matrix4x4 toBone=bind[bone];
                Vector3 end=toBone.MultiplyPoint3x4(bind[next].inverse.MultiplyPoint3x4(Vector3.zero));
                float length=end.magnitude;if(length<.01f)return;
                Vector3 axis=end/length;
                Vector3 front=Vector3.ProjectOnPlane(toBone.MultiplyVector(meshForward*facingSign),axis).normalized;
                if(front.sqrMagnitude<.01f)front=Vector3.ProjectOnPlane(toBone.MultiplyVector(Vector3.forward*facingSign),axis).normalized;
                if(front.sqrMagnitude<.01f)front=Vector3.ProjectOnPlane(toBone.MultiplyVector(Vector3.right*facingSign),axis).normalized;
                Vector3 across=Vector3.Cross(axis,front).normalized;
                float along=length*fraction;
                float face=float.NegativeInfinity,left=float.PositiveInfinity,right=float.NegativeInfinity;
                int samples=0;
                for(int pass=0;pass<2&&samples<3;pass++)
                {
                    face=float.NegativeInfinity;left=float.PositiveInfinity;right=float.NegativeInfinity;samples=0;
                    for(int i=0;i<points.Length&&i<skinWeights.Length;i++)
                    {
                        var w=skinWeights[i];
                        float influence=(w.boneIndex0==bone?w.weight0:0)+(w.boneIndex1==bone?w.weight1:0)
                            +(w.boneIndex2==bone?w.weight2:0)+(w.boneIndex3==bone?w.weight3:0);
                        if(influence<.45f)continue;
                        Vector3 p=toBone.MultiplyPoint3x4(points[i]);
                        if(Mathf.Abs(Vector3.Dot(p,axis)-along)>length*(pass==0?.19f:.55f))continue;
                        face=Mathf.Max(face,Vector3.Dot(p,front));
                        float horizontal=Vector3.Dot(p,across);left=Mathf.Min(left,horizontal);right=Mathf.Max(right,horizontal);samples++;
                    }
                }
                if(samples<3||right-left<.005f||face<=0f)return;
                float width=Mathf.Clamp((right-left)*widthFactor,length*.11f,length*.42f);
                float height=length*span,depth=length*.018f;
                Vector3 center=axis*along+across*((left+right)*.5f)+front*(face+depth*.45f);
                Matrix4x4 toMesh=bind[bone].inverse;
                Matrix4x4 normalMatrix=toMesh.inverse.transpose;
                Box(center,new Vector3(width,height,depth),0);
                Box(center+front*depth*.52f,new Vector3(width*.76f,height*.76f,depth*.32f),2);
                for(int j=0;j<3;j++)
                {
                    float y=(j-1)*height*.24f;
                    Box(center+axis*y+front*depth*.74f,new Vector3(width*.45f,height*.032f,depth*.35f),1);
                    Box(center+axis*y+across*width*.31f+front*depth*.74f,
                        new Vector3(width*.11f,height*.09f,depth*.42f),1);
                }
                foreach(int x in new[]{-1,1})foreach(int y in new[]{-1,1})
                    Box(center+across*(x*width*.39f)+axis*(y*height*.39f)+front*depth*.68f,
                        new Vector3(width*.07f,width*.07f,depth*.3f),2);

                void Box(Vector3 c,Vector3 size,int material)
                {
                    const int cornerSteps=3,contourCount=cornerSteps*4;
                    float radius=Mathf.Min(size.x,size.y)*.22f,bevel=Mathf.Min(radius*.45f,size.z*.38f);
                    int start=vertices.Count;
                    for(int ring=0;ring<4;ring++)
                    {
                        float angle=ring<2?0f:(ring==2?Mathf.PI*.25f:Mathf.PI*.5f);
                        float inset=bevel*(1f-Mathf.Cos(angle));
                        float z=ring==0?-size.z*.5f:size.z*.5f-bevel+bevel*Mathf.Sin(angle);
                        for(int corner=0;corner<4;corner++)for(int step=0;step<cornerSteps;step++)
                        {
                            float a=(corner*90f+step*90f/(cornerSteps-1))*Mathf.Deg2Rad;
                            Vector2 outward=new Vector2(Mathf.Cos(a),Mathf.Sin(a));
                            float sx=corner==0||corner==3?1f:-1f,sy=corner<2?1f:-1f;
                            Vector2 xy=new Vector2(sx*(size.x*.5f-radius),sy*(size.y*.5f-radius))+outward*(radius-inset);
                            Vector3 p=c+across*xy.x+axis*xy.y+front*z;
                            Vector3 n=(across*outward.x+axis*outward.y)*Mathf.Cos(angle)+front*Mathf.Sin(angle);
                            vertices.Add(toMesh.MultiplyPoint3x4(p));
                            normals.Add(normalMatrix.MultiplyVector(n).normalized);
                            uvs.Add(new Vector2(xy.x/size.x+.5f,xy.y/size.y+.5f));
                            weights.Add(new BoneWeight{boneIndex0=bone,weight0=1});
                        }
                    }
                    for(int ring=0;ring<3;ring++)for(int j=0;j<contourCount;j++)
                    {
                        int a=start+ring*contourCount+j,b=start+ring*contourCount+(j+1)%contourCount;
                        int d=a+contourCount,e=b+contourCount;
                        triangles[material].AddRange(new[]{a,b,d,b,e,d});
                    }
                    Cap(3,1);Cap(0,-1);
                    void Cap(int ring,int direction)
                    {
                        int cap=vertices.Count;
                        Vector3 center=c+front*(direction*size.z*.5f);
                        Vector3 normal=normalMatrix.MultiplyVector(front*direction).normalized;
                        vertices.Add(toMesh.MultiplyPoint3x4(center));normals.Add(normal);uvs.Add(new Vector2(.5f,.5f));
                        weights.Add(new BoneWeight{boneIndex0=bone,weight0=1});
                        for(int j=0;j<contourCount;j++)
                        {
                            int edge=start+ring*contourCount+j;
                            vertices.Add(vertices[edge]);normals.Add(normal);uvs.Add(uvs[edge]);
                            weights.Add(new BoneWeight{boneIndex0=bone,weight0=1});
                        }
                        for(int j=0;j<contourCount;j++)
                        {
                            int a=cap+1+j,b=cap+1+(j+1)%contourCount;
                            triangles[material].AddRange(direction>0?new[]{cap,a,b}:new[]{cap,b,a});
                        }
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
