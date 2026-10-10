using System;
using System.Collections.Generic;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>Open-face mint helmet fitted to Tahla's head and attached to its animation bone.</summary>
    [DisallowMultipleComponent]
    public sealed class TahlaHelmet : MonoBehaviour
    {
        readonly List<Mesh> ownedMeshes=new List<Mesh>();
        Material[] materials;

        public static void Apply(Transform model)
        {
            if(model==null||model.GetComponent<TahlaHelmet>()!=null||RuntimeShaderLibrary.Lit==null)return;
            Transform head=null,chest=null;
            foreach(var bone in model.GetComponentsInChildren<Transform>(true))
            {if(bone.name=="Head")head=bone;if(bone.name=="Chest")chest=bone;}
            if(head==null||chest==null)return;
            Vector3 origin=model.InverseTransformPoint(head.position);
            float unit=Vector3.Distance(origin,model.InverseTransformPoint(chest.position))/.22f;
            if(unit<.001f)return;
            var owner=model.gameObject.AddComponent<TahlaHelmet>();
            owner.materials=new[]{
                RuntimeShaderLibrary.Create(new Color(.52f,.80f,.67f),false,.25f,.48f,Color.black),
                RuntimeShaderLibrary.Create(new Color(.075f,.12f,.10f),false,.02f,.18f,Color.black),
                RuntimeShaderLibrary.Create(new Color(.65f,.47f,.23f),false,.72f,.38f,Color.black)};
            foreach(var material in owner.materials)if(material==null){Destroy(owner);return;}
            owner.materials[0].name="DF_Tahla_MintHelmetPaint";
            owner.materials[1].name="DF_Tahla_HelmetPadding";
            owner.materials[2].name="DF_Tahla_HelmetFasteners";
            Bounds skull=owner.FitAndRemoveHair(model,head,origin,unit);
            owner.Build(model,head,skull,origin,unit);
        }

        Bounds FitAndRemoveHair(Transform model,Transform head,Vector3 origin,float unit)
        {
            Bounds skull=default;bool found=false;
            foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Mesh source=skin.sharedMesh;
                if(skin.name!="DF_Anatomy"||source==null||!source.isReadable)continue;
                int bone=Array.IndexOf(skin.bones,head);
                if(bone<0||bone>=source.bindposes.Length)continue;
                var vertices=source.vertices;var weights=source.boneWeights;var slots=skin.sharedMaterials;
                if(vertices.Length!=weights.Length)continue;
                Matrix4x4 toModel=model.worldToLocalMatrix*head.localToWorldMatrix*source.bindposes[bone];
                Mesh copy=null;
                for(int sub=0;sub<source.subMeshCount&&sub<slots.Length;sub++)
                {
                    if(slots[sub]==null)continue;
                    string name=slots[sub].name;
                    bool hair=name.StartsWith("Tahla white swept hair",StringComparison.OrdinalIgnoreCase)||
                        name.StartsWith("Hair",StringComparison.OrdinalIgnoreCase);
                    bool flower=name.StartsWith("Tahla pink flower",StringComparison.OrdinalIgnoreCase)||
                        name.StartsWith("Tahla engraved warm gold",StringComparison.OrdinalIgnoreCase);
                    bool skinSlot=name.StartsWith("Skin",StringComparison.OrdinalIgnoreCase);
                    if(!hair&&!flower&&!skinSlot)continue;
                    int[] triangles=source.GetTriangles(sub);var kept=new List<int>(triangles.Length);
                    for(int i=0;i<triangles.Length;i+=3)
                    {
                        bool headOnly=true,brow=true,scalpFlower=true;
                        for(int j=0;j<3;j++)
                        {
                            int index=triangles[i+j];
                            if(HeadWeight(weights[index],bone)<.9f){headOnly=false;break;}
                            Vector3 p=toModel.MultiplyPoint3x4(vertices[index]);
                            float height=(p.y-origin.y)/unit;
                            brow&=height<.294f&&(p.z-origin.z)/unit>.07f;
                            scalpFlower&=height>.34f;
                            // Long pointed ears must not enlarge the helmet to the full ear span.
                            if(skinSlot&&height>.14f&&Mathf.Abs(p.x-origin.x)<.115f*unit)
                            {if(!found){skull=new Bounds(p,Vector3.zero);found=true;}else skull.Encapsulate(p);}
                        }
                        bool remove=headOnly&&((hair&&!brow)||(flower&&scalpFlower));
                        if(!remove){kept.Add(triangles[i]);kept.Add(triangles[i+1]);kept.Add(triangles[i+2]);}
                    }
                    if(kept.Count==triangles.Length)continue;
                    if(copy==null){copy=Instantiate(source);copy.name=source.name+"_TahlaHelmet";ownedMeshes.Add(copy);}
                    copy.SetTriangles(kept,sub,false);
                }
                if(copy!=null)skin.sharedMesh=copy;
            }
            if(!found||skull.size.y<.05f*unit)
                skull=new Bounds(origin+Vector3.up*.26f*unit,new Vector3(.20f,.24f,.23f)*unit);
            return skull;
        }

        void Build(Transform model,Transform head,Bounds skull,Vector3 origin,float unit)
        {
            var vertices=new List<Vector3>();var faces=new[]{new List<int>(),new List<int>(),new List<int>()};
            Vector3 center=new Vector3(origin.x,origin.y+.26f*unit,skull.center.z);
            Vector3 radius=new Vector3(Mathf.Clamp(skull.extents.x/unit,.095f,.108f)+.014f,
                Mathf.Max((skull.max.y-center.y)/unit+.018f,.13f),
                Mathf.Clamp(skull.extents.z/unit,.105f,.13f)+.018f)*unit;
            int Add(Vector3 p){vertices.Add(p);return vertices.Count-1;}
            void Face(int a,int b,int c,int surface,Vector3 expected)
            {
                if(Vector3.Dot(Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]),expected)<0f)
                {int temp=b;b=c;c=temp;}
                faces[surface].Add(a);faces[surface].Add(b);faces[surface].Add(c);
            }
            void Tri(Vector3 a,Vector3 b,Vector3 c,int surface,Vector3 expected)=>Face(Add(a),Add(b),Add(c),surface,expected);
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,int surface,Vector3 expected)
            {Tri(a,b,c,surface,expected);Tri(a,c,d,surface,expected);}
            const int segments=32,rows=12;
            float Limit(float angle)=>1.08f+.88f*(1f-Mathf.Cos(angle))*.5f;
            Vector3 Cap(float angle,float fraction,bool inner)
            {
                float polar=Limit(angle)*fraction;
                Vector3 r=radius-(inner?Vector3.one*.006f*unit:Vector3.zero);
                return center+new Vector3(Mathf.Sin(angle)*Mathf.Sin(polar)*r.x,
                    Mathf.Cos(polar)*r.y,Mathf.Cos(angle)*Mathf.Sin(polar)*r.z);
            }
            var rings=new int[2,rows,segments];var poles=new int[2];
            for(int layer=0;layer<2;layer++)
            {
                bool inner=layer==1;
                poles[layer]=Add(Cap(0,0,inner));
                for(int row=0;row<rows;row++)for(int i=0;i<segments;i++)
                    rings[layer,row,i]=Add(Cap(i*Mathf.PI*2/segments,(row+1f)/rows,inner));
                for(int i=0;i<segments;i++)
                {
                    int next=(i+1)%segments;
                    Face(poles[layer],rings[layer,0,i],rings[layer,0,next],inner?1:0,inner?Vector3.down:Vector3.up);
                    for(int row=0;row<rows-1;row++)
                    {
                        int a=rings[layer,row,i],b=rings[layer,row,next],c=rings[layer,row+1,next],d=rings[layer,row+1,i];
                        Vector3 expected=(vertices[a]+vertices[b]+vertices[c]+vertices[d])*.25f-center;
                        if(inner)expected=-expected;
                        Face(a,b,c,inner?1:0,expected);Face(a,c,d,inner?1:0,expected);
                    }
                }
            }
            for(int i=0;i<segments;i++)
            {
                int j=(i+1)%segments;
                int a=rings[0,rows-1,i],b=rings[0,rows-1,j],c=rings[1,rows-1,j],d=rings[1,rows-1,i];
                float angle=(i+.5f)*Mathf.PI*2/segments,polar=Limit(angle);
                Vector3 downRim=new Vector3(Mathf.Sin(angle)*Mathf.Cos(polar),-Mathf.Sin(polar),Mathf.Cos(angle)*Mathf.Cos(polar));
                Face(a,b,c,1,downRim);Face(a,c,d,1,downRim);
            }
            // Short raised brow peak; the eyes and face remain entirely open.
            Vector3 Peak(float angle,bool outer,bool bottom)
            {
                float reach=.008f+.025f*(1f-angle*angle/(.95f*.95f));
                return Cap(angle,1,false)+new Vector3(Mathf.Sin(angle)*(outer?reach:0),
                    (outer?.005f:0)-(bottom?.006f:0),Mathf.Cos(angle)*(outer?reach:0))*unit;
            }
            for(int i=0;i<16;i++)
            {
                float a=-.95f+i*1.9f/16,b=-.95f+(i+1)*1.9f/16;
                Quad(Peak(a,false,false),Peak(a,true,false),Peak(b,true,false),Peak(b,false,false),0,Vector3.up);
                Quad(Peak(a,false,true),Peak(b,false,true),Peak(b,true,true),Peak(a,true,true),1,Vector3.down);
                Quad(Peak(a,true,false),Peak(a,true,true),Peak(b,true,true),Peak(b,true,false),0,new Vector3(Mathf.Sin((a+b)*.5f),0,Mathf.Cos((a+b)*.5f)));
                Quad(Peak(a,false,false),Peak(b,false,false),Peak(b,false,true),Peak(a,false,true),1,Vector3.back);
            }
            foreach(float a in new[]{-.95f,.95f})
                Quad(Peak(a,false,false),Peak(a,false,true),Peak(a,true,true),Peak(a,true,false),0,Vector3.right*Mathf.Sign(a));
            void Tube(Vector3 a,Vector3 b,float r,int surface)
            {
                const int sides=10;Vector3 axis=(b-a).normalized;
                Vector3 across=Vector3.Cross(axis,Mathf.Abs(axis.y)<.9f?Vector3.up:Vector3.right).normalized;
                Vector3 up=Vector3.Cross(axis,across);
                Vector3 Ring(Vector3 p,int i)=>p+(across*Mathf.Cos(i*Mathf.PI*2/sides)+up*Mathf.Sin(i*Mathf.PI*2/sides))*r;
                for(int i=0;i<sides;i++)
                {
                    Vector3 p=Ring(a,i),q=Ring(a,i+1),s=Ring(b,i),t=Ring(b,i+1);
                    Quad(p,q,t,s,surface,(p+q)*.5f-a);Tri(a,q,p,surface,-axis);Tri(b,s,t,surface,axis);
                }
            }
            foreach(float side in new[]{-1f,1f})
            {
                // Small side hinges sit in front of the elf ear roots.
                Vector3 hinge=center+new Vector3(side*(radius.x+.003f*unit),.025f*unit,radius.z*.48f);
                Tube(hinge-Vector3.right*side*.009f*unit,hinge+Vector3.right*side*.009f*unit,.023f*unit,0);
                Tube(hinge+Vector3.right*side*.010f*unit,hinge+Vector3.right*side*.013f*unit,.010f*unit,2);
                Vector3 jaw=new Vector3(origin.x+side*.085f*unit,origin.y+.15f*unit,skull.max.z-.025f*unit);
                Vector3 chin=new Vector3(origin.x,origin.y+.13f*unit,skull.max.z+.005f*unit);
                Tube(hinge,jaw,.005f*unit,1);Tube(jaw,chin,.005f*unit,1);
            }
            for(int i=0;i<vertices.Count;i++)vertices[i]=head.InverseTransformPoint(model.TransformPoint(vertices[i]));
            var mesh=new Mesh{name="DF_Tahla_MintHelmet"};ownedMeshes.Add(mesh);
            mesh.SetVertices(vertices);mesh.subMeshCount=3;
            for(int i=0;i<faces.Length;i++)mesh.SetTriangles(faces[i],i);
            mesh.RecalculateNormals();mesh.RecalculateBounds();
            var go=new GameObject(mesh.name);go.layer=head.gameObject.layer;go.transform.SetParent(head,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterials=materials;
        }

        static float HeadWeight(BoneWeight w,int bone)=>(w.boneIndex0==bone?w.weight0:0)+
            (w.boneIndex1==bone?w.weight1:0)+(w.boneIndex2==bone?w.weight2:0)+(w.boneIndex3==bone?w.weight3:0);
        void OnDestroy()
        {
            foreach(var mesh in ownedMeshes)if(mesh!=null)Destroy(mesh);
            if(materials!=null)foreach(var material in materials)if(material!=null)Destroy(material);
        }
    }
}
