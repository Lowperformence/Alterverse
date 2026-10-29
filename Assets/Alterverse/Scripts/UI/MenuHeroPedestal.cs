using System;
using System.Collections.Generic;
using Alterverse.Visuals;
using UnityEngine;

namespace Alterverse.UI
{
    /// <summary>One reusable presentation deck, confined to the portrait layer.</summary>
    internal sealed class MenuHeroPedestal : IDisposable
    {
        public const float SurfaceY=-1.05f;
        const float Height=.20f;
        readonly GameObject root;
        readonly Mesh deck,contacts;
        readonly Material[] materials;
        readonly Material contactMaterial;
        readonly List<Vector3> contactVertices=new(8);
        float radius=.88f;
        bool disposed;
        public Bounds Bounds=>new(root.transform.position-Vector3.up*Height*.5f,new Vector3(radius*2,Height,radius*2));

        public MenuHeroPedestal(Transform stage,int layer)
        {
            root=new GameObject("DF_MenuHeroPedestal");root.transform.SetParent(stage,false);
            root.transform.localPosition=new Vector3(0,SurfaceY,0);root.layer=layer;
            materials=new[]{
                RuntimeShaderLibrary.Create(new Color(.032f,.046f,.065f),false,.52f,.36f),
                RuntimeShaderLibrary.Create(new Color(.12f,.155f,.19f),false,.32f,.30f),
                RuntimeShaderLibrary.Create(new Color(.12f,.67f,.85f),true)};
            foreach(Material material in materials)if(material!=null&&material.HasProperty("_ShowcaseLighting"))material.SetFloat("_ShowcaseLighting",1f);
            deck=BuildDeck();root.AddComponent<MeshFilter>().sharedMesh=deck;
            Configure(root.AddComponent<MeshRenderer>(),materials);
            contactMaterial=RuntimeShaderLibrary.CreateGroundTelegraph(new Color(.004f,.008f,.014f),.30f,0f,.25f);
            if(contactMaterial!=null)
            {
                var shadow=new GameObject("DF_MenuSoleContacts");shadow.transform.SetParent(root.transform,false);shadow.layer=layer;
                contacts=new Mesh{name="DF_MenuSoleContacts"};contacts.MarkDynamic();
                contacts.vertices=new Vector3[8];
                contacts.uv=new[]{new Vector2(0,0),new Vector2(0,1),new Vector2(1,1),new Vector2(1,0),
                    new Vector2(0,0),new Vector2(0,1),new Vector2(1,1),new Vector2(1,0)};
                contacts.triangles=new[]{0,1,2,0,2,3,4,5,6,4,6,7};
                shadow.AddComponent<MeshFilter>().sharedMesh=contacts;
                Configure(shadow.AddComponent<MeshRenderer>(),new[]{contactMaterial});
            }
            SetRadius(radius);
        }

        static void Configure(MeshRenderer renderer,Material[] materials)
        {
            renderer.sharedMaterials=materials;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows=false;
            renderer.lightProbeUsage=UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage=UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        public void SetRadius(float value)
        {radius=Mathf.Clamp(value,.88f,1.5f);root.transform.localScale=new Vector3(radius,1,radius);}

        public void UpdateContacts(MenuHeroGrounding feet,Transform actor)
        {
            if(contacts==null||!feet.Ready)return;
            contactVertices.Clear();
            AddContact(feet.LeftCenter,feet.LeftSize,actor);
            AddContact(feet.RightCenter,feet.RightSize,actor);
            contacts.SetVertices(contactVertices);contacts.RecalculateBounds();
        }
        void AddContact(Vector3 center,Vector2 size,Transform actor)
        {
            Vector3 right=actor.right*Mathf.Clamp(size.x*.55f,.07f,.24f);
            Vector3 forward=actor.forward*Mathf.Clamp(size.y*.55f,.10f,.32f);
            Add(center-right-forward);Add(center-right+forward);Add(center+right+forward);Add(center+right-forward);
        }
        void Add(Vector3 point)
        {
            Vector3 local=root.transform.InverseTransformPoint(point);local.y=.0015f;contactVertices.Add(local);
        }

        static Mesh BuildDeck()
        {
            const int segments=64;
            var vertices=new List<Vector3>();var uv=new List<Vector2>();
            var triangles=new[]{new List<int>(),new List<int>(),new List<int>()};
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,int material)
            {
                int start=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(d);
                uv.Add(new Vector2(0,0));uv.Add(new Vector2(0,1));uv.Add(new Vector2(1,1));uv.Add(new Vector2(1,0));
                triangles[material].AddRange(new[]{start,start+2,start+1,start,start+3,start+2});
            }
            Vector3 Point(float angle,float r,float y)=>new(Mathf.Cos(angle)*r,y,Mathf.Sin(angle)*r);
            void Band(float r0,float y0,float r1,float y1,int material,bool segmented=false)
            {
                for(int i=0;i<segments;i++)
                {
                    if(segmented&&i%8==0)continue;
                    float a=i*Mathf.PI*2/segments,b=(i+1)*Mathf.PI*2/segments;
                    Quad(Point(a,r0,y0),Point(b,r0,y0),Point(b,r1,y1),Point(a,r1,y1),material);
                }
            }
            Band(.89f,-Height,1f,-.145f,0);
            Band(1f,-.145f,1f,-.035f,0);
            Band(1f,-.035f,.94f,0,0);
            Band(.94f,0,.90f,0,0);
            Band(.923f,.001f,.909f,.001f,2,true);
            Band(1.001f,-.100f,1.001f,-.087f,2,true);
            // Separate deck cap keeps the top normal flat across the bevel.
            for(int i=0;i<segments;i++)
            {
                float a=i*Mathf.PI*2/segments,b=(i+1)*Mathf.PI*2/segments;
                int start=vertices.Count;vertices.Add(Vector3.zero);vertices.Add(Point(b,.90f,0));vertices.Add(Point(a,.90f,0));
                uv.Add(new Vector2(.5f,.5f));uv.Add(new Vector2(.5f+Mathf.Cos(b)*.5f,.5f+Mathf.Sin(b)*.5f));
                uv.Add(new Vector2(.5f+Mathf.Cos(a)*.5f,.5f+Mathf.Sin(a)*.5f));
                triangles[1].AddRange(new[]{start,start+1,start+2});
            }
            for(int i=0;i<12;i++)
            {
                float angle=i*Mathf.PI/6;
                Vector3 edge=new(-Mathf.Sin(angle)*.0025f,0,Mathf.Cos(angle)*.0025f);
                Vector3 a=Point(angle,.66f,.001f),b=Point(angle,.87f,.001f);
                Quad(a-edge,b-edge,b+edge,a+edge,0);
            }
            var mesh=new Mesh{name="DF_MenuPedestal_BeveledDeck"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.subMeshCount=3;
            for(int i=0;i<3;i++)mesh.SetTriangles(triangles[i],i);
            mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }

        public void Dispose()
        {
            if(disposed)return;disposed=true;
            if(root!=null)UnityEngine.Object.Destroy(root);
            if(deck!=null)UnityEngine.Object.Destroy(deck);
            if(contacts!=null)UnityEngine.Object.Destroy(contacts);
            foreach(Material material in materials)if(material!=null)UnityEngine.Object.Destroy(material);
            if(contactMaterial!=null)UnityEngine.Object.Destroy(contactMaterial);
        }
    }
}
