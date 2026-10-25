using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    /// <summary>Original ATLAS cargo lander. Flat-shaded authored geometry, no asset downloads or colliders.</summary>
    public sealed class InsertionTransportModel : MonoBehaviour
    {
        private readonly List<Mesh> meshes = new();
        private readonly List<Material> materials = new();
        private Transform leftDoor, rightDoor, exhaust;

        public void Build(int team)
        {
            Color accent = team == 0 ? StylizedArtDirection.ZeroEnergy : StylizedArtDirection.LumenEnergy;
            Material hull = Material("Titanium", new Color(.32f,.38f,.43f), false);
            Material trim = Material("Graphite", new Color(.055f,.075f,.095f), false);
            Material panels = Material("Cargo ivory", new Color(.70f,.73f,.67f), false);
            Material energy = Material("Team running lights", accent, true);
            Material amber = Material("Hazard amber", new Color(1f,.55f,.12f), true);
            var shell = new Geometry(); var detail = new Geometry();
            var plates = new Geometry(); var lights = new Geometry(); var warning = new Geometry();
            // A solid cargo roof conceals the spawn from the overhead camera. Delivery is through the belly.
            for (int side = -1; side <= 1; side += 2)
            {
                shell.Box(new Vector3(side*3.05f,2.35f,0),new Vector3(1.15f,2.4f,7.4f));
                plates.Box(new Vector3(side*3.65f,2.7f,-.2f),new Vector3(.16f,1.15f,5.8f));
                shell.Box(new Vector3(side*4.55f,2.8f,.1f),new Vector3(2.1f,.35f,4.8f));
                // Four vertical nacelles, armored collars and inset luminous nozzles.
                for (int end = -1; end <= 1; end += 2)
                {
                    Vector3 p = new Vector3(side*4.8f,2.05f,end*2.05f);
                    shell.Cylinder(p, .82f, .68f, 2.4f, 10);
                    detail.Cylinder(p+Vector3.down*1.16f, .85f, .85f, .25f, 10);
                    lights.Cylinder(p+Vector3.down*1.33f, .55f, .62f, .09f, 10);
                    warning.Box(new Vector3(side*3.72f,2.6f,end*2.6f),new Vector3(.05f,.65f,.22f));
                }
                for(int rib=0;rib<5;rib++)
                {
                    detail.Box(new Vector3(side*3.76f,2.7f,-2.2f+rib),new Vector3(.06f,.85f,.09f));
                    lights.Box(new Vector3(side*2.43f,2.1f,-2.2f+rib),new Vector3(.08f,.12f,.36f));
                }
                shell.Box(new Vector3(side*2.9f,4.05f,-3.05f),new Vector3(.24f,1.4f,1.8f));
                lights.Box(new Vector3(side*2.92f,4.74f,-3.1f),new Vector3(.3f,.08f,1.1f));
            }
            // Tapered cockpit: visibly a cargo aircraft, not a cube assembled from primitives.
            shell.Wedge(new Vector3(0,2.75f,4.15f), 5.8f,2.1f,2.5f);
            detail.Wedge(new Vector3(0,3.58f,4.25f),4.65f,.55f,1.8f);
            lights.Box(new Vector3(0,3.65f,5.13f),new Vector3(3.5f,.12f,.045f));
            shell.Box(new Vector3(0,3.45f,-3.8f),new Vector3(6.3f,.35f,.65f));
            detail.Box(new Vector3(0,1.65f,-3.8f),new Vector3(5.1f,.25f,.5f));
            shell.Box(new Vector3(0,3.7f,-.2f),new Vector3(5.1f,.28f,6.7f));
            for(int i=0;i<4;i++)
                plates.Box(new Vector3(0,3.89f,-2.6f+i*1.6f),new Vector3(4.75f,.1f,1.32f));
            lights.Box(new Vector3(0,3.97f,-.2f),new Vector3(.12f,.06f,5.5f));
            Part("Airframe",shell,hull); Part("Nacelles and ribs",detail,trim);
            Part("Armor panels",plates,panels); Part("Navigation lights",lights,energy,false);
            Part("Caution bars",warning,amber,false);
            leftDoor=Door("Port belly hatch",-1,trim);
            rightDoor=Door("Starboard belly hatch",1,trim);
            var jets=new Geometry();
            for(int side=-1;side<=1;side+=2)for(int end=-1;end<=1;end+=2)
                jets.Cylinder(new Vector3(side*4.8f,-.05f,end*2.05f),.10f,.5f,1.5f,8);
            exhaust=Part("Four VTOL exhausts",jets,energy,false);
            Pose(0f,1f);
        }

        private Transform Door(string name,int side,Material material)
        {
            var g=new Geometry();g.Box(Vector3.zero,new Vector3(2.5f,.16f,6.7f));
            Transform door=Part(name,g,material);door.localPosition=new Vector3(side*1.25f,1.15f,-.2f);
            return door;
        }

        public void Pose(float doorOpen,float thrust)
        {
            if(leftDoor==null)return;
            leftDoor.localPosition=new Vector3(-1.25f-2.45f*doorOpen,1.15f,-.2f);
            rightDoor.localPosition=new Vector3(1.25f+2.45f*doorOpen,1.15f,-.2f);
            exhaust.localScale=new Vector3(1,Mathf.Lerp(.15f,1f,thrust),1);
        }

        private Material Material(string label,Color color,bool emissive)
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
            var material=new Material(shader){name="ATLAS / "+label};
            material.SetColor("_BaseColor",color);material.SetColor("_Color",color);
            material.SetFloat("_Smoothness",.28f);material.SetFloat("_Metallic",emissive?0f:.48f);
            if(emissive){material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",color*2.2f);}
            materials.Add(material);return material;
        }

        private Transform Part(string label,Geometry geometry,Material material,bool shadows=true)
        {
            var part=new GameObject(label);part.transform.SetParent(transform,false);
            Mesh mesh=geometry.Create(label);meshes.Add(mesh);
            part.AddComponent<MeshFilter>().sharedMesh=mesh;
            MeshRenderer renderer=part.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=shadows?ShadowCastingMode.On:ShadowCastingMode.Off;
            renderer.receiveShadows=shadows;
            return part.transform;
        }

        private void OnDestroy()
        {
            foreach(var mesh in meshes)if(mesh!=null)Destroy(mesh);
            foreach(var material in materials)if(material!=null)Destroy(material);
        }

        // Geometry is allocated once under the loading curtain. Every face has a flat normal.
        private sealed class Geometry
        {
            private readonly List<Vector3> vertices=new();
            private readonly List<int> triangles=new();
            private void Triangle(Vector3 a,Vector3 b,Vector3 c)
            {
                int i=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);
                triangles.Add(i);triangles.Add(i+1);triangles.Add(i+2);
            }
            private void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
            {Triangle(a,b,c);Triangle(a,c,d);}
            public void Box(Vector3 p,Vector3 size)
            {
                Vector3 h=size*.5f;
                Vector3 a=p+new Vector3(-h.x,-h.y,-h.z),b=p+new Vector3(h.x,-h.y,-h.z),
                    c=p+new Vector3(h.x,h.y,-h.z),d=p+new Vector3(-h.x,h.y,-h.z),
                    e=p+new Vector3(-h.x,-h.y,h.z),f=p+new Vector3(h.x,-h.y,h.z),
                    g=p+new Vector3(h.x,h.y,h.z),h0=p+new Vector3(-h.x,h.y,h.z);
                Quad(a,d,c,b);Quad(e,f,g,h0);Quad(a,e,h0,d);Quad(b,c,g,f);
                Quad(d,h0,g,c);Quad(a,b,f,e);
            }
            public void Wedge(Vector3 p,float width,float height,float length)
            {
                float x=width*.5f,y=height*.5f,z=length*.5f;
                Vector3 a=p+new Vector3(-x,-y,-z),b=p+new Vector3(x,-y,-z),
                    c=p+new Vector3(x,y,-z),d=p+new Vector3(-x,y,-z),
                    e=p+new Vector3(-x*.7f,-y,z),f=p+new Vector3(x*.7f,-y,z),
                    g=p+new Vector3(x*.7f,-y+height*.3f,z),h=p+new Vector3(-x*.7f,-y+height*.3f,z);
                Quad(a,d,c,b);Quad(e,f,g,h);Quad(a,e,h,d);Quad(b,c,g,f);Quad(d,h,g,c);Quad(a,b,f,e);
            }
            public void Cylinder(Vector3 p,float bottom,float top,float height,int sides)
            {
                for(int i=0;i<sides;i++)
                {
                    float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;
                    Vector3 lo=p+Vector3.down*(height*.5f),hi=p+Vector3.up*(height*.5f);
                    Vector3 v0=lo+new Vector3(Mathf.Cos(a)*bottom,0,Mathf.Sin(a)*bottom);
                    Vector3 v1=lo+new Vector3(Mathf.Cos(b)*bottom,0,Mathf.Sin(b)*bottom);
                    Vector3 v2=hi+new Vector3(Mathf.Cos(b)*top,0,Mathf.Sin(b)*top);
                    Vector3 v3=hi+new Vector3(Mathf.Cos(a)*top,0,Mathf.Sin(a)*top);
                    Quad(v0,v3,v2,v1);Triangle(lo,v0,v1);Triangle(hi,v2,v3);
                }
            }
            public Mesh Create(string name)
            {
                var mesh=new Mesh{name="ATLAS / "+name};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);
                mesh.RecalculateNormals();mesh.RecalculateBounds();mesh.UploadMeshData(false);return mesh;
            }
        }
    }
}
