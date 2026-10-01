#if UNITY_EDITOR
using System;
using Alterverse.Visuals;
using UnityEditor;
using UnityEngine;

namespace Alterverse.Editor
{
    public static class AuthoredHotZoneArtValidation
    {
        [MenuItem("Alterverse/Validation/Hot Zone Blender Art")]
        public static void Validate()
        {
            string[] families={"Machine","Skeleton","Xeno","Spider","Ice","Sandstone","Crystal"};
            foreach(string family in families)
            {
                GameObject host=new("HotZoneArtValidation");Material material=new(Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard"));
                try
                {
                    foreach(PrimitiveType type in new[]{PrimitiveType.Cube,PrimitiveType.Sphere,PrimitiveType.Capsule,PrimitiveType.Cylinder})
                    {
                        GameObject child=GameObject.CreatePrimitive(type);child.transform.SetParent(host.transform,false);
                        child.transform.localPosition=new Vector3(1.2f,2.3f,-.7f);child.transform.localRotation=Quaternion.Euler(12,23,34);child.transform.localScale=new Vector3(.6f,1.4f,.9f);
                        child.GetComponent<MeshRenderer>().sharedMaterial=material;
                    }
                    GameObject plate=new("CustomPlate");plate.transform.SetParent(host.transform,false);
                    Mesh custom=new(){name="ValidationOriginalPlate",vertices=new[]{new Vector3(-1,-.5f,-.1f),new Vector3(1,-.5f,-.1f),new Vector3(0,.7f,.1f)},triangles=new[]{0,1,2}};custom.RecalculateNormals();custom.RecalculateBounds();
                    plate.AddComponent<MeshFilter>().sharedMesh=custom;plate.AddComponent<MeshRenderer>().sharedMaterial=material;
                    MeshFilter[] filters=host.GetComponentsInChildren<MeshFilter>();Bounds[] bounds=new Bounds[filters.Length];Vector3[] pos=new Vector3[filters.Length],scale=new Vector3[filters.Length];Quaternion[] rot=new Quaternion[filters.Length];
                    for(int i=0;i<filters.Length;i++){bounds[i]=filters[i].sharedMesh.bounds;pos[i]=filters[i].transform.localPosition;scale[i]=filters[i].transform.localScale;rot[i]=filters[i].transform.localRotation;}
                    Collider[] colliders=host.GetComponentsInChildren<Collider>();Bounds[] colliderBounds=new Bounds[colliders.Length];for(int i=0;i<colliders.Length;i++)colliderBounds[i]=colliders[i].bounds;
                    AuthoredHotZoneArt.Apply(host.transform,family);
                    Require(host.GetComponent<AuthoredHotZoneArt>()!=null,"Library missing");
                    for(int i=0;i<filters.Length;i++)
                    {
                        Bounds now=filters[i].sharedMesh.bounds;Require((now.min-bounds[i].min).sqrMagnitude<1e-10f&&(now.max-bounds[i].max).sqrMagnitude<1e-10f,"Size changed: "+family);
                        Require(pos[i]==filters[i].transform.localPosition&&scale[i]==filters[i].transform.localScale&&rot[i]==filters[i].transform.localRotation,"Rig transform changed");
                        Require(filters[i].sharedMesh.uv.Length==filters[i].sharedMesh.vertexCount,"Missing UVs");
                    }
                    Require(host.GetComponentsInChildren<Collider>().Length==colliders.Length,"Collider count changed");
                    for(int i=0;i<colliders.Length;i++)Require(colliders[i].bounds==colliderBounds[i],"Collider changed");
                    Color tint=new(.31f,.42f,.53f);material.color=tint;host.SendMessage("LateUpdate");
                    Require(filters[0].GetComponent<MeshRenderer>().sharedMaterial.color==tint,"Animated tint disconnected");
                    AuthoredHotZoneArt.Apply(host.transform,family);Require(host.GetComponents<AuthoredHotZoneArt>().Length==1,"Duplicate upgrade");
                    UnityEngine.Object.DestroyImmediate(custom);
                }
                finally{UnityEngine.Object.DestroyImmediate(host);UnityEngine.Object.DestroyImmediate(material);}
            }
            Debug.Log("Hot Zone Blender art: all seven families preserve part bounds, pivots, colliders and animated tint.");
        }
        static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}
#endif
