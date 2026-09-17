using System.Collections.Generic;
using UnityEngine;
namespace Alterverse.Visuals
{
    public sealed class DimensionTowerShell:MonoBehaviour
    {
        private Transform visualRoot,head,muzzle;
        private Renderer core;
        private readonly List<Material> materials=new List<Material>();
        private Color energy;
        private float flashUntil;
        public Vector3 MuzzlePosition=>muzzle!=null?muzzle.position:transform.position+Vector3.up*1.5f;
        public void Build(int team)
        {
            if(visualRoot!=null)return;
            Renderer placeholder=GetComponent<Renderer>();if(placeholder!=null)placeholder.enabled=false;
            visualRoot=new GameObject("DF_BastionShell").transform;visualRoot.SetParent(transform,false);
            Vector3 scale=transform.lossyScale;
            visualRoot.localScale=new Vector3(1f/scale.x,1f/scale.y,1f/scale.z);
            visualRoot.localPosition=new Vector3(0,(.12f-transform.position.y)/scale.y,0);
            energy=team==0?StylizedArtDirection.ZeroEnergy:StylizedArtDirection.LumenEnergy;
            Material stone=MaterialFor(new Color(.27f,.30f,.35f));
            Material armor=MaterialFor(team==0?new Color(.27f,.19f,.35f):new Color(.17f,.31f,.35f));
            Material trim=MaterialFor(Color.Lerp(energy,Color.gray,.55f));
            Material glow=MaterialFor(energy);
            Part("Foot",PrimitiveType.Cylinder,new Vector3(0,.16f,0),new Vector3(2.6f,.16f,2.6f),stone);
            Part("Plinth",PrimitiveType.Cylinder,new Vector3(0,.44f,0),new Vector3(1.95f,.12f,1.95f),armor);
            Part("Belt",PrimitiveType.Cylinder,new Vector3(0,.59f,0),new Vector3(1.7f,.035f,1.7f),trim);
            Part("ReactorHousing",PrimitiveType.Cube,new Vector3(0,1.15f,0),new Vector3(.95f,1.1f,.95f),armor);
            for(int i=0;i<4;i++)
            {
                float a=(i*90+45)*Mathf.Deg2Rad;
                Part("Buttress",PrimitiveType.Cube,new Vector3(Mathf.Cos(a)*.72f,.96f,Mathf.Sin(a)*.72f),new Vector3(.35f,.95f,.35f),stone);
            }
            head=new GameObject("DF_TowerHead").transform;head.SetParent(visualRoot,false);head.localPosition=Vector3.up*1.72f;
            head.localRotation=Quaternion.Euler(0,team==0?90:-90,0);
            Part("Turret",PrimitiveType.Cube,new Vector3(0,.25f,0),new Vector3(1.32f,.45f,1.08f),armor,head);
            core=Part("EnergyCore",PrimitiveType.Sphere,new Vector3(0,.37f,.5f),Vector3.one*.48f,glow,head).GetComponent<Renderer>();
            Part("BarrelLeft",PrimitiveType.Cube,new Vector3(-.32f,.23f,.77f),new Vector3(.19f,.21f,.85f),stone,head);
            Part("BarrelRight",PrimitiveType.Cube,new Vector3(.32f,.23f,.77f),new Vector3(.19f,.21f,.85f),stone,head);
            Part("Crest",PrimitiveType.Cube,new Vector3(0,.6f,-.3f),new Vector3(team==0?.16f:.68f,team==0?.65f:.16f,.25f),trim,head);
            muzzle=new GameObject("DF_TowerMuzzle").transform;muzzle.SetParent(head,false);muzzle.localPosition=new Vector3(0,.3f,1.2f);
        }
        public void AimAt(Vector3 target)
        {
            if(head==null)return;Vector3 direction=target-head.position;direction.y=0;
            if(direction.sqrMagnitude>.001f)head.rotation=Quaternion.LookRotation(direction);
        }
        public void Fire(){flashUntil=Time.time+.12f;}
        private void Update()
        {
            if(core==null||Time.timeScale<=0)return;
            float glow=Time.time<flashUntil?1f:.12f+.08f*Mathf.Sin(Time.time*2f);
            core.sharedMaterial.color=Color.Lerp(energy,Color.white,glow);
        }
        private Material MaterialFor(Color color)
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)shader=Shader.Find("Standard");
            if(shader==null)return null;
            Material material=new Material(shader){color=color};
            if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",.25f);
            materials.Add(material);return material;
        }
        private GameObject Part(string name,PrimitiveType type,Vector3 position,Vector3 scale,Material material,Transform parent=null)
        {
            GameObject g=GameObject.CreatePrimitive(type);g.name="DF_Tower"+name;g.transform.SetParent(parent!=null?parent:visualRoot,false);
            g.transform.localPosition=position;g.transform.localScale=scale;
            Collider collider=g.GetComponent<Collider>();if(collider!=null)collider.enabled=false;
            if(material!=null)g.GetComponent<Renderer>().sharedMaterial=material;return g;
        }
        private void OnDestroy(){foreach(Material material in materials)if(material!=null)Destroy(material);}
    }
}
