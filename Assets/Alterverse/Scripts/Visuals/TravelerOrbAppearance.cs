using System;
using System.Collections.Generic;
using UnityEngine;
namespace Alterverse.Visuals
{
    [DisallowMultipleComponent]
    public sealed class TravelerOrbAppearance : MonoBehaviour
    {
        Mesh mesh;Material[] materials;
        public static void Apply(Transform model)
        {
            if(model.GetComponent<TravelerOrbAppearance>()!=null)return;
            SkinnedMeshRenderer weapon=null;
            foreach(var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))if(r.name=="DF_WeaponR"){weapon=r;break;}
            var all=model.GetComponentsInChildren<Transform>(true);
            Transform grip=Array.Find(all,t=>t.name=="WeaponGripR"),hand=Array.Find(all,t=>t.name=="HandR"),elbow=Array.Find(all,t=>t.name=="ForearmR");
            if(weapon==null||grip==null||hand==null||elbow==null)return;
            var owner=model.gameObject.AddComponent<TravelerOrbAppearance>();owner.Build(model,weapon,grip,hand,elbow,all);
        }
        void Build(Transform model,SkinnedMeshRenderer renderer,Transform grip,Transform hand,Transform elbow,Transform[] all)
        {
            float radius=Vector3.Distance(elbow.position,hand.position)*.40f;
            Vector3 center=hand.position+model.up*(radius*2.4f)+model.forward*(radius*.65f);
            var vertices=new List<Vector3>();var glow=new List<int>();var metal=new List<int>();
            Vector3 Point(Vector3 p)=>renderer.transform.InverseTransformPoint(center+model.right*p.x+model.up*p.y+model.forward*p.z);
            const int rows=24,cols=40;
            for(int y=0;y<=rows;y++)for(int x=0;x<=cols;x++)
            {float a=y*Mathf.PI/rows,b=x*Mathf.PI*2/cols;vertices.Add(Point(new Vector3(Mathf.Sin(a)*Mathf.Cos(b),Mathf.Cos(a),Mathf.Sin(a)*Mathf.Sin(b))*radius));}
            for(int y=0;y<rows;y++)for(int x=0;x<cols;x++){int a=y*(cols+1)+x;Quad(glow,a,a+1,a+cols+2,a+cols+1);}
            for(int ring=0;ring<3;ring++)
            {
                int start=vertices.Count;Quaternion rotation=Quaternion.Euler(45+ring*48,ring*60,ring*31);const int segments=64,sides=8;
                for(int i=0;i<=segments;i++)for(int j=0;j<sides;j++)
                {
                    float a=i*Mathf.PI*2/segments,b=j*Mathf.PI*2/sides;
                    Vector3 radial=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                    Vector3 p=radial*(radius*(1.29f+ring*.12f)+Mathf.Cos(b)*radius*.035f)+Vector3.up*(Mathf.Sin(b)*radius*.035f);
                    vertices.Add(Point(rotation*p));
                }
                for(int i=0;i<segments;i++)for(int j=0;j<sides;j++){int a=start+i*sides+j,b=start+i*sides+(j+1)%sides;Quad(metal,a,b,b+sides,a+sides);}
            }
            // Orient the generated closed surfaces; no vertices from the former staff are retained.
            Vector3 localCenter=renderer.transform.InverseTransformPoint(center);
            for(int i=0;i<glow.Count;i+=3){Vector3 a=vertices[glow[i]],b=vertices[glow[i+1]],c=vertices[glow[i+2]];if(Vector3.Dot(Vector3.Cross(b-a,c-a),a-localCenter)<0){int t=glow[i+1];glow[i+1]=glow[i+2];glow[i+2]=t;}}
            mesh=new Mesh{name="DF_TravelerFloatingOrb_NoHandle"};mesh.SetVertices(vertices);mesh.subMeshCount=2;mesh.SetTriangles(glow,0);mesh.SetTriangles(metal,1);
            var weights=new BoneWeight[vertices.Count];for(int i=0;i<weights.Length;i++)weights[i]=new BoneWeight{boneIndex0=0,weight0=1};
            mesh.boneWeights=weights;mesh.bindposes=new[]{grip.worldToLocalMatrix*renderer.transform.localToWorldMatrix};mesh.RecalculateNormals();mesh.RecalculateBounds();
            materials=new[]{RuntimeShaderLibrary.Create(new Color(.13f,.26f,.34f),false,.25f,.70f,new Color(.10f,.43f,.65f)*.55f),RuntimeShaderLibrary.Create(new Color(.47f,.52f,.55f),false,.78f,.48f,Color.black)};
            renderer.sharedMesh=mesh;renderer.bones=new[]{grip};renderer.rootBone=grip;renderer.sharedMaterials=materials;renderer.localBounds=mesh.bounds;renderer.updateWhenOffscreen=true;
            Transform muzzle=Array.Find(all,t=>t.name=="DF_Muzzle");if(muzzle!=null){muzzle.SetParent(grip,true);muzzle.position=center;}
        }
        static void Quad(List<int> t,int a,int b,int c,int d){t.Add(a);t.Add(b);t.Add(c);t.Add(a);t.Add(c);t.Add(d);}
        void OnDestroy(){if(mesh!=null)Destroy(mesh);if(materials!=null)foreach(var m in materials)if(m!=null)Destroy(m);}
    }
}
