using System.Collections.Generic;
using Alterverse.Combat;
using Alterverse.Match;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>Collider-free low-poly reward bodies, batched per animated joint/material.
    /// Uses the shared HotZoneTank authority, locomotion, health and attack-event relay.</summary>
    [DisallowMultipleComponent,DefaultExecutionOrder(1200)]
    public sealed class ExoplanetHotZoneVisual:MonoBehaviour
    {
        readonly List<Mesh> meshes=new();
        readonly List<Material> materials=new();
        readonly Dictionary<(Transform,Material),List<CombineInstance>> batches=new();
        readonly List<Transform> legs=new();
        readonly List<Quaternion> legRest=new();
        Mesh sphere,plate;
        Material shell,dark,edge,glow,crest;
        Transform body;
        Health health;
        Vector3 lastPosition;
        float phase,stride;
        bool built;
        public Transform Muzzle {get;private set;}
        public ArenaVariant Variant {get;private set;}

        public void Build(ArenaVariant variant,int team)
        {
            if(built)return;
            built=true;Variant=variant;health=GetComponent<Health>();lastPosition=transform.position;
            bool iron=variant==ArenaVariant.Wasp76b;
            shell=Mat("Shell",iron?new Color(.27f,.29f,.31f):new Color(.095f,.26f,.28f),iron?.68f:.03f,iron?.38f:.40f);
            dark=Mat("Joints",iron?new Color(.07f,.075f,.08f):new Color(.055f,.11f,.14f),iron?.54f:.02f,.26f);
            edge=Mat("Edges",iron?new Color(.60f,.31f,.14f):new Color(.39f,.22f,.43f),iron?.58f:.02f,.30f);
            Color energy=ExoplanetArenaProfile.Energy(variant);
            Color faction=team==0?StylizedArtDirection.ZeroEnergy:StylizedArtDirection.LumenEnergy;
            glow=Mat("Energy",Color.Lerp(energy,faction,.18f),.02f,.45f,energy*.85f);
            if(!iron)crest=Mat("AmberCrest",new Color(.81f,.42f,.15f),.03f,.38f,new Color(.9f,.37f,.07f)*.15f);
            if(shell==null||dark==null||edge==null||glow==null||!iron&&crest==null){enabled=false;return;}
            foreach(Material material in new[]{shell,dark,edge,crest})
                if(material!=null)AuthoredHotZoneArt.ConfigureSurface(material,iron?"Machine":"Xeno");
            sphere=MakeSphere();meshes.Add(sphere);
            plate=ProductionMeshFactory.BeveledBox(); // Project-owned shared mesh, never destroy it here.
            body=Pivot(iron?"DF_FerroxBody":"DF_VesperaBody",transform,new Vector3(0,-.10f,0));
            if(iron)BuildFerrox();else BuildVespera();
            Flush();
        }

        void BuildFerrox()
        {
            Part("Carapace",body,sphere,shell,Vector3.zero,new Vector3(2.06f,.64f,2.18f));
            Part("Underbody",body,sphere,dark,new Vector3(0,-.19f,0),new Vector3(1.60f,.36f,1.80f));
            for(int i=0;i<5;i++)
            {
                float z=-.75f+i*.34f;
                Part("IronArmor",body,plate,i%2==0?shell:edge,new Vector3(0,.27f,z),new Vector3(1.53f,.15f,.33f),Quaternion.Euler(-i*3,0,0));
                Part("MoltenSeam",body,plate,glow,new Vector3(0,.35f,z+.12f),new Vector3(.65f,.028f,.035f));
            }
            Part("Head",body,sphere,shell,new Vector3(0,.00f,1.05f),new Vector3(.96f,.43f,.59f));
            for(int side=-1;side<=1;side+=2)
            {
                Part("Eye",body,sphere,glow,new Vector3(side*.29f,.10f,1.30f),new Vector3(.13f,.09f,.08f));
                for(int i=0;i<3;i++)BuildLeg(side,i,new Vector3(side*.75f,-.17f,-.66f+i*.65f),true);
                Transform claw=Pivot(side<0?"DF_FerroxClaw_L":"DF_FerroxClaw_R",body,new Vector3(side*.79f,-.02f,.84f));
                Part("ClawArm",claw,plate,shell,new Vector3(side*.20f,-.08f,.38f),new Vector3(.32f,.28f,.93f),Quaternion.Euler(0,side*22,0));
                Part("ClawPalm",claw,sphere,edge,new Vector3(side*.37f,-.08f,.98f),new Vector3(.59f,.39f,.66f));
                for(int finger=-1;finger<=1;finger+=2)
                    Part("Pincer",claw,plate,shell,new Vector3(side*.37f+finger*.17f,-.08f,1.38f),new Vector3(.13f,.19f,.62f),Quaternion.Euler(0,-finger*17,0));
            }
            Muzzle=Pivot("DF_FerroxMuzzle",body,new Vector3(0,.03f,1.39f));
        }

        void BuildVespera()
        {
            Part("Body",body,sphere,shell,new Vector3(0,.02f,-.10f),new Vector3(1.48f,.87f,2.05f));
            Part("Belly",body,sphere,dark,new Vector3(0,-.20f,.10f),new Vector3(1.22f,.51f,1.58f));
            Part("Head",body,sphere,shell,new Vector3(0,.12f,.88f),new Vector3(1.37f,.61f,.94f));
            Part("Jaw",body,sphere,edge,new Vector3(0,-.12f,1.06f),new Vector3(1.06f,.24f,.72f));
            for(int i=0;i<5;i++)
                Part("AmberCrest",body,sphere,crest,new Vector3(0,.50f-i*.037f,.61f-i*.28f),new Vector3(.15f,.38f,.20f),Quaternion.Euler(-18,0,0));
            for(int side=-1;side<=1;side+=2)
            {
                Part("Eye",body,sphere,glow,new Vector3(side*.47f,.26f,1.19f),new Vector3(.17f,.12f,.10f));
                for(int i=0;i<2;i++)BuildLeg(side,i,new Vector3(side*.61f,-.15f,i==0?.60f:-.70f),false);
                Transform gill=Pivot(side<0?"DF_VesperaGill_L":"DF_VesperaGill_R",body,new Vector3(side*.59f,.17f,.57f));
                for(int i=0;i<5;i++)
                {
                    Quaternion q=Quaternion.Euler(0,side*(28+i*12),side*(i-2)*13);
                    Part("GillFan",gill,sphere,edge,new Vector3(side*(.25f+i*.065f),.05f+(i-2)*.07f,-.18f-i*.08f),new Vector3(.14f,.17f,.68f),q);
                    Part("GillGlow",gill,sphere,glow,new Vector3(side*(.25f+i*.065f),.05f+(i-2)*.07f,-.47f-i*.08f),new Vector3(.08f,.09f,.13f),q);
                }
            }
            for(int i=0;i<6;i++)
                Part("BioNode",body,sphere,glow,new Vector3((i%2==0?-1:1)*.61f,.20f,-.68f+(i/2)*.44f),new Vector3(.08f,.065f,.11f));
            Muzzle=Pivot("DF_VesperaMuzzle",body,new Vector3(0,.08f,1.57f));
        }

        void BuildLeg(int side,int index,Vector3 position,bool iron)
        {
            Transform leg=Pivot("DF_ExoplanetLeg_"+legs.Count,body,position);
            legs.Add(leg);legRest.Add(leg.localRotation);
            Part("UpperLeg",leg,iron?plate:sphere,shell,new Vector3(side*.25f,-.14f,0),new Vector3(.67f,.25f,.35f),Quaternion.Euler(0,0,-side*26));
            Part("LowerLeg",leg,iron?plate:sphere,shell,new Vector3(side*.52f,-.42f,.06f),new Vector3(.21f,.58f,.22f),Quaternion.Euler(0,0,side*20));
            Part("Foot",leg,iron?plate:sphere,dark,new Vector3(side*.59f,-.68f,.17f),new Vector3(.34f,.12f,.44f));
        }

        Transform Pivot(string label,Transform parent,Vector3 position)
        {var go=new GameObject(label);go.transform.SetParent(parent,false);go.transform.localPosition=position;return go.transform;}
        void Part(string label,Transform parent,Mesh mesh,Material material,Vector3 position,Vector3 scale,Quaternion rotation=default)
        {
            if(rotation==default)rotation=Quaternion.identity;
            var key=(parent,material);
            if(!batches.TryGetValue(key,out var batch)){batch=new();batches.Add(key,batch);}
            batch.Add(new CombineInstance{mesh=mesh,transform=Matrix4x4.TRS(position,rotation,scale)});
        }
        void Flush()
        {
            foreach(var group in batches)
            {
                Mesh mesh=new(){name="DF_"+Variant+"_JointBatch"};
                mesh.CombineMeshes(group.Value.ToArray(),true,true);mesh.RecalculateBounds();meshes.Add(mesh);
                ProductionMeshFactory.MeshObject(group.Key.Item1.name+"_"+group.Key.Item2.name,group.Key.Item1,mesh,group.Key.Item2);
            }
            batches.Clear();
        }
        Material Mat(string label,Color color,float metal,float smooth,Color emission=default)
        {
            Material material=RuntimeShaderLibrary.Create(color,false,metal,smooth,emission);
            if(material!=null){material.name="DF_"+Variant+"_"+label;material.enableInstancing=true;materials.Add(material);}
            return material;
        }
        static Mesh MakeSphere()
        {
            const int sides=24,rings=12;
            var vertices=new Vector3[(sides+1)*(rings+1)];var normals=new Vector3[vertices.Length];var uv=new Vector2[vertices.Length];
            var triangles=new List<int>(sides*(rings-1)*6);
            for(int ring=0;ring<=rings;ring++)for(int side=0;side<=sides;side++)
            {
                float a=side*Mathf.PI*2f/sides,b=ring*Mathf.PI/rings;
                int i=ring*(sides+1)+side;
                normals[i]=new Vector3(Mathf.Sin(b)*Mathf.Cos(a),Mathf.Cos(b),Mathf.Sin(b)*Mathf.Sin(a));
                vertices[i]=normals[i]*.5f;uv[i]=new Vector2(side/(float)sides,ring/(float)rings);
                if(ring==rings||side==sides)continue;
                int j=i+sides+1;
                if(ring>0){triangles.Add(i);triangles.Add(i+1);triangles.Add(j);}
                if(ring<rings-1){triangles.Add(i+1);triangles.Add(j+1);triangles.Add(j);}
            }
            Mesh mesh=new(){name="DF_ExoplanetSmoothBody",vertices=vertices,normals=normals,uv=uv,triangles=triangles.ToArray()};
            mesh.RecalculateBounds();mesh.RecalculateTangents();return mesh;
        }
        void LateUpdate()
        {
            Vector3 delta=transform.position-lastPosition;delta.y=0;lastPosition=transform.position;
            if(!built||body==null||health!=null&&health.IsDead||Time.timeScale<=0)return;
            float distance=Mathf.Min(delta.magnitude,.30f);
            phase+=distance*6f;
            stride=Mathf.Lerp(stride,Mathf.Clamp01(distance/Mathf.Max(.001f,Time.deltaTime)*.5f),1f-Mathf.Exp(-12f*Time.deltaTime));
            body.localPosition=new Vector3(0,-.10f+Mathf.Sin(Time.time*1.7f)*.009f+Mathf.Sin(phase*2)*.025f*stride,0);
            body.localRotation=Quaternion.identity;
            for(int i=0;i<legs.Count;i++)
                legs[i].localRotation=legRest[i]*Quaternion.Euler(Mathf.Sin(phase+i*Mathf.PI)*12f*stride,Mathf.Cos(phase+i*Mathf.PI)*9f*stride,0);
        }
        void OnDestroy()
        {foreach(Mesh mesh in meshes)if(mesh!=null)Destroy(mesh);foreach(Material material in materials)if(material!=null)Destroy(material);}
    }
}
