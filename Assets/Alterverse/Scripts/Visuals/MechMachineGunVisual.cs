using System.Collections.Generic;
using Alterverse.Combat;
using UnityEngine;

namespace Alterverse.Visuals
{
    // Held six-barrel Gatling. Collider-free geometry, combined into four renderer batches.
    [DisallowMultipleComponent,DefaultExecutionOrder(1350)]
    public sealed class MechMachineGunVisual:MonoBehaviour
    {
        readonly List<Mesh> meshes=new();readonly List<Material> materials=new();
        Transform rotor,body;Vector3 bodyRest;float started=-100,contact=.22f;
        Transform actor;Health victim;Vector3 aim;
        public Transform Muzzle {get;private set;}
        public Transform EjectionPort {get;private set;}
        public bool BurstPresented {get;private set;}
        public float FlightDuration=>contact*.68f;
        public static MechMachineGunVisual Build(Transform mount,Vector3 position,float scale=1)
        {
            if(mount==null)return null;
            var root=new GameObject("DF_HeavyMachineGun").transform;root.SetParent(mount,false);
            root.localPosition=position;root.localScale=Vector3.one*scale;
            var gun=root.gameObject.AddComponent<MechMachineGunVisual>();gun.actor=mount.GetComponentInParent<Health>()?.transform;
            gun.BuildParts();return gun;
        }
        void BuildParts()
        {
            Material armor=RuntimeShaderLibrary.Create(new Color(.24f,.28f,.25f),false,.64f,.34f);
            Material steel=RuntimeShaderLibrary.Create(new Color(.12f,.15f,.17f),false,.82f,.46f);
            Material brass=RuntimeShaderLibrary.Create(new Color(.65f,.46f,.20f),false,.70f,.44f);
            foreach(var m in new[]{armor,steel,brass})if(m!=null)materials.Add(m);
            if(armor==null||steel==null||brass==null)return;
            body=new GameObject("DF_GatlingReceiver").transform;body.SetParent(transform,false);bodyRest=body.localPosition;
            rotor=new GameObject("DF_GatlingRotor").transform;rotor.SetParent(body,false);
            var groups=new Dictionary<(Transform,Material),List<CombineInstance>>();
            Mesh cylinder=Cylinder(),box=Box();meshes.Add(cylinder);meshes.Add(box);
            void Part(Transform parent,Material material,bool round,Vector3 point,Vector3 size,Quaternion rotation=default)
            {
                if(rotation.x==0&&rotation.y==0&&rotation.z==0&&rotation.w==0)rotation=Quaternion.identity;
                var key=(parent,material);if(!groups.TryGetValue(key,out var batch)){batch=new();groups.Add(key,batch);}
                batch.Add(new CombineInstance{mesh=round?cylinder:box,transform=Matrix4x4.TRS(point,rotation,size)});
            }
            Quaternion along=Quaternion.Euler(90,0,0);
            Part(body,armor,false,new Vector3(0,.04f,.06f),new Vector3(.48f,.38f,.94f));
            Part(body,steel,false,new Vector3(0,.04f,-.57f),new Vector3(.34f,.28f,.40f));
            Part(body,steel,false,new Vector3(0,-.23f,-.22f),new Vector3(.17f,.43f,.22f),Quaternion.Euler(16,0,0));
            Part(body,armor,false,new Vector3(-.40f,-.13f,.02f),new Vector3(.48f,.48f,.58f));
            Part(body,steel,false,new Vector3(-.40f,-.13f,.33f),new Vector3(.49f,.38f,.08f));
            Part(body,steel,true,new Vector3(0,.02f,.53f),new Vector3(.44f,.22f,.44f),along);
            Part(body,armor,false,new Vector3(0,.31f,-.05f),new Vector3(.39f,.12f,.48f));
            for(int i=-1;i<=1;i+=2)
                Part(body,steel,false,new Vector3(i*.14f,.23f,-.27f),new Vector3(.065f,.25f,.075f));
            for(int i=0;i<5;i++)
            {
                Part(body,steel,false,new Vector3(.25f,.08f,-.28f+i*.14f),new Vector3(.022f,.19f,.055f));
                Part(body,brass,true,new Vector3(-.23f-i*.085f,.09f+i*.014f,.10f),new Vector3(.066f,.25f,.066f),along);
            }
            for(int i=0;i<6;i++)
            {
                float a=i*Mathf.PI/3;Vector3 p=new Vector3(Mathf.Cos(a)*.13f,Mathf.Sin(a)*.13f,1.12f);
                Part(rotor,steel,true,p,new Vector3(.083f,1.18f,.083f),along);
                Part(rotor,steel,true,new Vector3(p.x,p.y,1.76f),new Vector3(.105f,.11f,.105f),along);
            }
            Part(rotor,steel,true,new Vector3(0,0,.72f),new Vector3(.36f,.13f,.36f),along);
            Part(rotor,steel,true,new Vector3(0,0,1.53f),new Vector3(.36f,.11f,.36f),along);
            foreach(var group in groups)
            {
                var mesh=new Mesh{name="DF_GatlingBatch"};mesh.CombineMeshes(group.Value.ToArray(),true,true);mesh.RecalculateBounds();meshes.Add(mesh);
                ProductionMeshFactory.MeshObject("DF_GatlingSurface",group.Key.Item1,mesh,group.Key.Item2);
            }
            Muzzle=new GameObject("DF_GatlingMuzzle").transform;Muzzle.SetParent(body,false);Muzzle.localPosition=new Vector3(0,0,1.83f);
            EjectionPort=new GameObject("DF_GatlingEjectionPort").transform;EjectionPort.SetParent(body,false);EjectionPort.localPosition=new Vector3(.28f,.07f,.02f);
        }
        static Mesh Box()
        {
            Vector3[] normals={Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
            Vector3[] horizontal={Vector3.back,Vector3.forward,Vector3.right,Vector3.right,Vector3.right,Vector3.left};
            Vector3[] vertical={Vector3.up,Vector3.up,Vector3.back,Vector3.forward,Vector3.up,Vector3.up};
            var vertices=new Vector3[24];var indices=new int[36];var uv=new Vector2[24];
            for(int i=0;i<6;i++)
            {
                int v=i*4,t=i*6;Vector3 n=normals[i]*.5f,h=horizontal[i]*.5f,u=vertical[i]*.5f;
                vertices[v]=n-h-u;vertices[v+1]=n+h-u;vertices[v+2]=n+h+u;vertices[v+3]=n-h+u;
                uv[v]=Vector2.zero;uv[v+1]=Vector2.right;uv[v+2]=Vector2.one;uv[v+3]=Vector2.up;
                indices[t]=v;indices[t+1]=v+1;indices[t+2]=v+2;indices[t+3]=v;indices[t+4]=v+2;indices[t+5]=v+3;
            }
            var mesh=new Mesh{name="DF_GatlingBox"};mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=indices;
            mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        static Mesh Cylinder()
        {
            const int sides=12;
            var vertices=new List<Vector3>();var indices=new List<int>();var uv=new List<Vector2>();
            void Tri(Vector3 a,Vector3 b,Vector3 c)
            {
                int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);
                uv.Add(new Vector2(a.x+.5f,a.y+.5f));uv.Add(new Vector2(b.x+.5f,b.y+.5f));uv.Add(new Vector2(c.x+.5f,c.y+.5f));
                indices.Add(n);indices.Add(n+1);indices.Add(n+2);
            }
            for(int i=0;i<sides;i++)
            {
                float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;
                Vector3 p=new Vector3(Mathf.Cos(a)*.5f,-.5f,Mathf.Sin(a)*.5f),q=new Vector3(Mathf.Cos(b)*.5f,-.5f,Mathf.Sin(b)*.5f);
                Vector3 r=p+Vector3.up,s=q+Vector3.up;
                Tri(p,r,s);Tri(p,s,q);Tri(r,Vector3.up*.5f,s);Tri(q,Vector3.down*.5f,p);
            }
            var mesh=new Mesh{name="DF_GatlingCylinder"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);
            mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        public void Pulse(float windup){started=Time.time;contact=Mathf.Max(.01f,windup);BurstPresented=false;}
        public void MarkBurstPresented(){BurstPresented=true;}
        public void SetAim(Vector3 point,Health target){aim=point+Vector3.up*.55f;victim=target;}
        public void Seek(float elapsed){started-=Mathf.Max(0,elapsed);}
        void LateUpdate()
        {
            if(body==null||rotor==null||Time.timeScale<=0)return;
            float age=Time.time-started;
            if(actor!=null)
            {
                Vector3 facing=age>=0&&age<contact+.35f?(victim!=null?victim.transform.position+Vector3.up*.55f:aim)-transform.position:actor.forward;
                if(facing.sqrMagnitude>.0001f)transform.rotation=Quaternion.LookRotation(facing,actor.up);
            }
            if(age>=0&&age<contact+.22f)
            {
                rotor.localRotation*=Quaternion.AngleAxis(900f*Time.deltaTime,Vector3.forward);
                float release=actor!=null&&actor.GetComponent<HealingGuardMech>()!=null?0:contact*.32f;
                float recoil=0;
                for(int i=0;i<3;i++)
                {
                    float shotAge=age-release-i*.05f;
                    if(shotAge>=0)recoil+=Mathf.Exp(-shotAge*32f)*.035f;
                }
                body.localPosition=bodyRest+Vector3.back*recoil;
            }
            else body.localPosition=bodyRest;
        }
        void OnDestroy()
        {
            foreach(var mesh in meshes)if(mesh!=null)Destroy(mesh);
            foreach(var material in materials)if(material!=null)Destroy(material);
        }
    }
}
