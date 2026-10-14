using System.Collections.Generic;
using Alterverse.Match;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>Habitat armour and attached detail on the existing articulated base guard.
    /// No extra lights, colliders, AI, health changes or per-frame renderer scans.</summary>
    [DisallowMultipleComponent]
    public sealed class ExoplanetGuardAppearance:MonoBehaviour
    {
        readonly List<Mesh> meshes=new();
        readonly List<Material> materials=new();
        bool built;
        public ArenaVariant Variant {get;private set;}

        public static Color GuardEnergy(ArenaVariant variant,Color faction)=>Color.Lerp(ExoplanetArenaProfile.Energy(variant),faction,.45f);

        public static void Apply(Transform host,ArenaVariant variant,int team)
        {
            if(host==null||variant!=ArenaVariant.Wasp76b&&variant!=ArenaVariant.Trappist1e&&variant!=ArenaVariant.Toi715b)return;
            var appearance=host.GetComponent<ExoplanetGuardAppearance>()??host.gameObject.AddComponent<ExoplanetGuardAppearance>();
            appearance.Build(variant,team);
        }
        void Build(ArenaVariant variant,int team)
        {
            if(built)return;built=true;Variant=variant;
            bool iron=variant==ArenaVariant.Wasp76b;
            bool rust=variant==ArenaVariant.Toi715b;
            Color armor=rust?new Color(.43f,.15f,.09f):iron?new Color(.27f,.29f,.31f):new Color(.10f,.29f,.30f);
            Color trim=rust?new Color(.82f,.74f,.59f):iron?new Color(.60f,.31f,.14f):new Color(.38f,.24f,.46f);
            Color dark=iron?new Color(.075f,.085f,.095f):new Color(.055f,.13f,.16f);
            Color faction=team==0?StylizedArtDirection.ZeroEnergy:StylizedArtDirection.LumenEnergy;
            Color energy=GuardEnergy(variant,faction);
            foreach(Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                if(!renderer.enabled||!renderer.name.StartsWith("DF_Guard"))continue;
                // Each original guard part already owns its material through RuntimeMaterialPalette.
                // Modify that owned instance; shared project resources are never tinted.
                Material material=renderer.sharedMaterial;if(material==null)continue;
                string name=renderer.name;
                bool emissive=name.Contains("Visor")||name.Contains("Eye")||name.Contains("Lens")||name.Contains("Strip")||name.Contains("Status")||name.Contains("AntennaTip");
                bool badge=name.Contains("IDPlate");
                bool highlight=name.Contains("Plate")||name.Contains("Panel")||name.Contains("Brow")||name.Contains("Cap")||name.Contains("Vent")||name.Contains("Fin")||name.Contains("Rib")||name.Contains("Bolt");
                bool armored=name.Contains("Armor")||name.Contains("Shell")||name.Contains("Pod");
                Color color=badge?faction:emissive?energy:highlight?trim:armored?armor:dark;
                if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);else material.color=color;
                if(material.HasProperty("_Metallic"))material.SetFloat("_Metallic",iron?.70f:.16f);
                if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",iron?.38f:.44f);
                if(material.HasProperty("_EmissionColor"))material.SetColor("_EmissionColor",emissive?energy*.85f:badge?faction*.55f:Color.black);
                if(!emissive&&!badge)AuthoredHotZoneArt.ConfigureSurface(material,iron?"Machine":"Xeno");
            }
            Material detail=Mat("HabitatArmor",trim,iron?.72f:.06f,iron?.38f:.42f);
            Color habitat=ExoplanetArenaProfile.Energy(variant);
            Material accent=Mat("HabitatGlow",habitat,.02f,.4f,habitat*.65f);
            if(detail==null||accent==null)return;
            AuthoredHotZoneArt.ConfigureSurface(detail,iron?"Machine":"Xeno");
            Mesh input=iron?ProductionMeshFactory.BeveledBox():SceneryMeshQuality.CurvedFrond();
            if(!iron)meshes.Add(input);
            for(int side=-1;side<=1;side+=2)
            {
                string suffix=side<0?"L":"R";
                Transform shoulder=transform.Find("DF_GuardShoulderArmor"+suffix);
                Transform forearm=transform.Find("DF_GuardForearmArmor"+suffix);
                if(shoulder!=null)
                {
                    var layers=new List<CombineInstance>();
                    for(int i=0;i<3;i++)
                    {
                        Vector3 p=iron?new Vector3(side*.53f,-.23f+i*.23f,-.16f):new Vector3(side*.42f,.15f,-.24f+i*.20f);
                        Vector3 scale=iron?new Vector3(.12f,.13f,.62f):new Vector3(.45f,.72f,.48f);
                        Quaternion q=Quaternion.Euler(iron?0:-16,iron?0:side*(18+i*12),iron?0:side*20);
                        layers.Add(new CombineInstance{mesh=input,transform=Matrix4x4.TRS(p,q,scale)});
                    }
                    Batch(rust?"DF_IvoryGuardCrownArmor_"+suffix:iron?"DF_IronGuardHeatShroud_"+suffix:"DF_TwilightGuardGillArmor_"+suffix,shoulder,layers,detail);
                }
                if(forearm!=null)
                {
                    var seams=new List<CombineInstance>();
                    for(int i=0;i<3;i++)
                        seams.Add(new CombineInstance{mesh=ProductionMeshFactory.BeveledBox(),transform=Matrix4x4.TRS(new Vector3(-.20f+i*.20f,.13f,.535f),Quaternion.Euler(0,0,iron?0:-18),new Vector3(.065f,.30f,.025f))});
                    Batch("DF_"+variant+"GuardHabitatSeams_"+suffix,forearm,seams,accent);
                }
            }
        }
        void Batch(string name,Transform parent,List<CombineInstance> parts,Material material)
        {
            Mesh mesh=new(){name=name};mesh.CombineMeshes(parts.ToArray(),true,true);mesh.RecalculateBounds();meshes.Add(mesh);
            ProductionMeshFactory.MeshObject(name,parent,mesh,material);
        }
        Material Mat(string name,Color color,float metal,float smooth,Color emission=default)
        {
            Material material=RuntimeShaderLibrary.Create(color,false,metal,smooth,emission);
            if(material!=null){material.name="DF_"+Variant+"Guard_"+name;material.enableInstancing=true;materials.Add(material);}
            return material;
        }
        void OnDestroy()
        {foreach(Mesh mesh in meshes)if(mesh!=null)Destroy(mesh);foreach(Material material in materials)if(material!=null)Destroy(material);}
    }
}

