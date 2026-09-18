using System.Collections.Generic;
using UnityEngine;

namespace Alterverse.Visuals
{
    public sealed class DimensionArenaArchitecture : MonoBehaviour
    {
        private readonly Dictionary<Color,Material> materials=new Dictionary<Color,Material>();
        private void Start()
        {
            Color stone=StylizedArtDirection.ArenaStone;
            Color zero=StylizedArtDirection.ZeroGround,lumen=StylizedArtDirection.LumenGround;
            // Low terrace edges establish the island boundary without covering the lanes.
            foreach(int side in new[]{-1,1})
            {
                Color faction=side<0?zero:lumen;
                for(int i=0;i<12;i++)
                {
                    float z=-33+i*6f;
                    Block("DF_TerraceFoot",new Vector3(side*42.8f,-.6f,z),new Vector3(2f,1.4f,5.5f),stone);
                    Block("DF_TerraceCap",new Vector3(side*42.7f,.18f,z),new Vector3(1.7f,.25f,5.3f),faction);
                }
                foreach(float z in new[]{-25f,-9f,9f,25f})
                {
                    if(side<0)Ruin(new Vector3(-44.8f,0,z),zero);
                    else Arch(new Vector3(44.8f,0,z),lumen);
                }
            }
            BuildLaneLandmarks(zero,lumen);BuildBaseApproaches(zero,lumen);\n            foreach(int side in new[]{-1,1})\n            for(int i=0;i<12;i++)
            {
                float x=-38.5f+i*7f;
                Block("DF_OuterBorder",new Vector3(x,-.25f,side*37f),new Vector3(6.5f,.6f,1.7f),stone);
            }
        }



        private void BuildBaseApproaches(Color zero,Color lumen)
        {
            foreach(int side in new[]{-1,1})
            {
                Color faction=side<0?zero:lumen,energy=side<0?StylizedArtDirection.ZeroEnergy:StylizedArtDirection.LumenEnergy;
                for(int i=0;i<3;i++)
                {
                    float z=(i-1)*4.2f;
                    Block("DF_BaseStep",new Vector3(side*32.2f,.08f,z),new Vector3(5.2f-i*.55f,.16f,3.2f),Color.Lerp(faction,StylizedArtDirection.ArenaStone,.28f));
                    Block("DF_BaseInlay",new Vector3(side*(30.1f-i*.35f),.19f,z),new Vector3(.12f,.05f,1.55f),energy);
                }
            }
        }

        private void BuildLaneLandmarks(Color zero,Color lumen)
        {
            foreach(float z in new[]{-16f,16f})
            {
                // ZERO approaches are jagged and broken; LUMEN approaches are ordered and geometric.
                for(int i=0;i<3;i++)
                {
                    float x=-29f+i*7.5f;
                    Block("DF_ZeroLaneShard",new Vector3(x,.42f,z+(i%2==0?3.7f:-3.7f)),new Vector3(.42f,1.15f+i*.18f,.5f),Color.Lerp(zero,StylizedArtDirection.ZeroEnergy,.3f),new Vector3(12,i*31,-18+i*8));
                    x=29f-i*7.5f;
                    Block("DF_LumenLanePylon",new Vector3(x,.48f,z+(i%2==0?-3.7f:3.7f)),new Vector3(.48f,1.25f,.48f),Color.Lerp(lumen,StylizedArtDirection.LumenEnergy,.24f));
                    Block("DF_LumenLaneCap",new Vector3(x,.98f,z+(i%2==0?-3.7f:3.7f)),new Vector3(.72f,.12f,.72f),StylizedArtDirection.LumenEnergy,new Vector3(0,45,0));
                }
            }
        }

        private void Ruin(Vector3 p,Color color)
        {
            Block("DF_ZeroRockBase",p+Vector3.up*.3f,new Vector3(3.6f,.6f,3.2f),color,new Vector3(0,18,0));
            Block("DF_ZeroBrokenPillar",p+new Vector3(-.35f,1.4f,0),new Vector3(1.25f,2.3f,1.4f),color,new Vector3(0,24,-13));
            Block("DF_ZeroCrystal",p+new Vector3(.45f,2.1f,.1f),new Vector3(.23f,2.9f,.38f),StylizedArtDirection.ZeroEnergy,new Vector3(0,35,-19));
            Block("DF_ZeroFragment",p+new Vector3(.9f,.4f,1),new Vector3(1.4f,.55f,.9f),color,new Vector3(0,-27,12));
        }

        private void Arch(Vector3 p,Color color)
        {
            Block("DF_LumenPlinth",p+Vector3.up*.22f,new Vector3(2.8f,.44f,4.7f),color);
            foreach(int side in new[]{-1,1})
            {
                Block("DF_LumenPillar",p+new Vector3(0,1.25f,side*1.75f),new Vector3(.8f,2.5f,.8f),color);
                Block("DF_LumenInlay",p+new Vector3(-.43f,1.35f,side*1.75f),new Vector3(.05f,1.65f,.15f),StylizedArtDirection.LumenEnergy);
            }
            Block("DF_LumenLintel",p+Vector3.up*2.65f,new Vector3(.9f,.42f,4.35f),StylizedArtDirection.ArenaStoneLight);
            Block("DF_LumenKeystone",p+Vector3.up*2.98f,new Vector3(1.05f,.32f,.65f),color);
        }

        private void Block(string label,Vector3 position,Vector3 scale,Color color,Vector3 rotation=default)
        {
            GameObject g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=label;
            g.transform.SetParent(transform,false);g.transform.position=position;g.transform.localScale=scale;g.transform.rotation=Quaternion.Euler(rotation);
            Collider collider=g.GetComponent<Collider>();if(collider!=null)collider.enabled=false;
            if(!materials.TryGetValue(color,out Material material))
            {
                Shader shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)shader=Shader.Find("Standard");
                if(shader==null)return;
                material=new Material(shader){color=color};
                if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",.16f);
                materials.Add(color,material);
            }
            g.GetComponent<Renderer>().sharedMaterial=material;
        }
        private void OnDestroy(){foreach(Material material in materials.Values)if(material!=null)Destroy(material);}
    }
}
