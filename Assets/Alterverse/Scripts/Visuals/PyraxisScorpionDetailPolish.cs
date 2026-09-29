using System.Collections.Generic;
using Alterverse.Match;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Adds a second visual-detail pass to the Pyraxis Hot Zone scorpion without touching gameplay,
    /// hitboxes or the existing movement/attack logic.
    /// </summary>
    [DefaultExecutionOrder(1320)]
    public sealed class PyraxisScorpionDetailPolish : MonoBehaviour
    {
        private const string ManagerName="DF_PyraxisScorpionDetailPolish";
        private const float ScanInterval=.20f;
        private static bool installed;
        private float nextScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics(){installed=false;}

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if(installed)return;
            installed=true;
            GameObject manager=new(ManagerName);
            Object.DontDestroyOnLoad(manager);
            manager.AddComponent<PyraxisScorpionDetailPolish>();
        }

        private void Update()
        {
            if(!ArenaVariantRuntime.IsPyraxis||Time.unscaledTime<nextScan)return;
            nextScan=Time.unscaledTime+ScanInterval;

            PyraxisScorpionVisualOverride[] scorpions=Object.FindObjectsByType<PyraxisScorpionVisualOverride>(FindObjectsInactive.Exclude,FindObjectsSortMode.None);
            for(int i=0;i<scorpions.Length;i++)
            {
                PyraxisScorpionVisualOverride scorpion=scorpions[i];
                if(scorpion==null||scorpion.GetComponent<PyraxisScorpionDetailRig>()!=null)continue;
                scorpion.gameObject.AddComponent<PyraxisScorpionDetailRig>();
            }
        }
    }

    /// <summary>Visual-only detail rig layered on the procedural Pyraxis scorpion.</summary>
    public sealed class PyraxisScorpionDetailRig : MonoBehaviour
    {
        private const string DetailRootName="DF_PyraxisScorpion_DetailRig";
        private readonly List<Material> ownedMaterials=new();
        private readonly List<Transform> dorsalSpikes=new();
        private Transform visualRoot,detailRoot,leftMandible,rightMandible,tailRoot,leftClaw,rightClaw;
        private Material lavaGlow;
        private Color lavaGlowBase;
        private bool built;

        private void Awake(){TryBuild();}

        private void TryBuild()
        {
            if(built)return;
            visualRoot=transform.Find("DF_PyraxisScorpionVisual");
            if(visualRoot==null)return;
            if(visualRoot.Find(DetailRootName)!=null){built=true;return;}
            built=true;

            Material obsidian=HotZoneCreatureVisualFactory.Material(ownedMaterials,"DF_PyraxisScorpionObsidianDetail",new Color(.010f,.007f,.006f,1f),.58f,.38f);
            Material plated=HotZoneCreatureVisualFactory.Material(ownedMaterials,"DF_PyraxisScorpionPlatedDetail",new Color(.13f,.032f,.008f,1f),.42f,.30f);
            Material edge=HotZoneCreatureVisualFactory.Material(ownedMaterials,"DF_PyraxisScorpionEdgeDetail",new Color(.24f,.055f,.010f,1f),.34f,.33f);
            lavaGlowBase=new Color(1f,.30f,.012f,1f);
            lavaGlow=HotZoneCreatureVisualFactory.Material(ownedMaterials,"DF_PyraxisScorpionLavaDetail",lavaGlowBase,0f,.62f,true,5.0f);

            detailRoot=new GameObject(DetailRootName).transform;
            detailRoot.SetParent(visualRoot,false);

            BuildDorsalArmor(obsidian,plated,edge);
            BuildFace(obsidian,plated,lavaGlow);
            BuildLegDetails(plated,edge,lavaGlow);
            BuildClawDetails(obsidian,plated,lavaGlow);
            BuildTailDetails(obsidian,plated,lavaGlow);
        }

        private void BuildDorsalArmor(Material obsidian,Material plated,Material edge)
        {
            // Layered back plates make the silhouette read as armoured instead of three smooth blobs.
            for(int i=0;i<6;i++)
            {
                float t=i/5f;
                float z=Mathf.Lerp(.92f,-1.08f,t);
                float width=Mathf.Lerp(.88f,.64f,t);
                float y=.98f-Mathf.Abs(t-.38f)*.08f;
                HotZoneCreatureVisualFactory.Primitive(PrimitiveType.Cube,"DorsalPlate_"+i,detailRoot,
                    new Vector3(0,y,z),new Vector3(width,.09f,.40f),Quaternion.Euler(4f+i*1.7f,0,0),i%2==0?plated:obsidian);

                Transform spike=HotZoneCreatureVisualFactory.Primitive(PrimitiveType.Capsule,"DorsalSpike_"+i,detailRoot,
                    new Vector3(0,y+.23f,z-.04f),new Vector3(.075f,.24f-i*.012f,.075f),Quaternion.Euler(9f+i*2f,0,0),edge).transform;
                dorsalSpikes.Add(spike);
            }

            // Molten cracks between the plates give the shell a forged-from-lava identity.
            for(int i=0;i<5;i++)
            {
                float z=.70f-i*.39f;
                HotZoneCreatureVisualFactory.Primitive(PrimitiveType.Cube,"LavaBackSeam_"+i,detailRoot,
                    new Vector3(0,1.055f,z),new Vector3(.32f,.026f,.055f),Quaternion.Euler(0,(i%2==0?-9f:9f),0),lavaGlow);
            }

            // Small asymmetric shoulder horns keep the monster from looking perfectly mirrored.
            HotZoneCreatureVisualFactory.Primitive(PrimitiveType.Capsule,"LeftCarapaceHorn",detailRoot,
                new Vector3(-.77f,1.00f,.78f),new Vector3(.10f,.34f,.10f),Quaternion.Euler(0,0,-54f),edge);
            HotZoneCreatureVisualFactory.Primitive(PrimitiveType.Capsule,"RightCarapaceHorn",detailRoot,
                new Vector3(.80f,.96f,.62f),new Vector3(.09f,.25f,.09f),Quaternion.Euler(6f,0,51f),edge);
        }

        private void BuildFace(Material obsidian,Material plated,Material glow)
        {
            // Extra side eyes and brow plates make the head distinctly arachnid at gameplay distance.
            HotZoneCreatureVisualFactory.Primitive(PrimitiveType.Sphere,"SideEyeL1",detailRoot,new Vector3(-.48f,.79f,2.12f),new Vector3(.075f,.070f,.070f),Quaternion.identity,glow);
            HotZoneCreatureVisualFactory.Primitive(PrimitiveType.Sphere,"SideEyeR1",detailRoot,new Vector3(.48f,.79f,2.12f),new Vector3(.075f,.070f,.070f),Quaternion.identity,glow);
            HotZoneCreatureVisualFactory.Primitive(PrimitiveType.Sphere,"SideEyeL2",detailRoot,new Vector3(-.57f,.72f,1.98f),new Vector3(.055f,.052f,.052f),Quaternion.identity,glow);
            HotZoneCreatureVisualFactory.Primitive(PrimitiveType.Sphere,"SideEyeR2",detailRoot,new Vector3(.57f,.72f,1.98f),new Vector3(.055f,.052f,.052f),Quaternion.identity,glow);

            HotZoneCreatureVisualFactory.Primitive(PrimitiveType.Cube,"BrowL",detailRoot,new Vector3(-.27f,.98f,2.06f),new Vector3(.34f,.07f,.18f),Quaternion.Euler(-10f,-8f,-10f),plated);
            HotZoneCreatureVisualFactory.Primitive(PrimitiveType.Cube,"BrowR",detailRoot,new Vector3(.27f,.98f,2.06f),new Vector3(.34f,.07f,.18f),Quaternion.Euler(-10f,8f,10f),plated);

            leftMandible=HotZoneCreatureVisualFactory.Primitive(PrimitiveType.Capsule,"MandibleL",detailRoot,
                new Vector3(-.23f,.55f,2.28f),new Vector3(.085f,.30f,.085f),Quaternion.Euler(69f,-18f,-12f),obsidian).transform;
            rightMandible=HotZoneCreatureVisualFactory.Primitive(PrimitiveType.Capsule,"MandibleR",detailRoot,
                new Vector3(.23f,.55f,2.28f),new Vector3(.085f,.30f,.085f),Quaternion.Euler(69f,18f,12f),obsidian).transform;

            HotZoneCreatureVisualFactory.Primitive(PrimitiveType.Cube,"MouthGlow",detailRoot,new Vector3(0,.56f,2.26f),new Vector3(.20f,.045f,.11f),Quaternion.identity,glow);
        }

        private void BuildLegDetails(Material plated,Material edge,Material glow)
        {
            // Hip joints, knee armour and glowing heat nodes make all eight legs readable as articulated limbs.
            for(int sideIndex=0;sideIndex<2;sideIndex++)
            {
                float side=sideIndex==0?-1f:1f;
                for(int leg=0;leg<4;leg++)
                {
                    float z=1.18f-leg*.72f;
                    HotZoneCreatureVisualFactory.Primitive(PrimitiveType.Sphere,"HipCap_"+sideIndex+"_"+leg,detailRoot,
                        new Vector3(side*.91f,.60f,z),new Vector3(.18f,.15f,.18f),Quaternion.identity,plated);
                    HotZoneCreatureVisualFactory.Primitive(PrimitiveType.Sphere,"HeatJoint_"+sideIndex+"_"+leg,detailRoot,
                        new Vector3(side*1.48f,.42f,z+.03f),new Vector3(.085f,.075f,.085f),Quaternion.identity,glow);
                    HotZoneCreatureVisualFactory.Primitive(PrimitiveType.Capsule,"LegSpur_"+sideIndex+"_"+leg,detailRoot,
                        new Vector3(side*1.58f,.55f,z-.02f),new Vector3(.045f,.18f,.045f),Quaternion.Euler(0,0,side*58f),edge);
                }
            }
        }

        private void BuildClawDetails(Material obsidian,Material plated,Material glow)
        {
            leftClaw=visualRoot.Find("LeftClaw");
            rightClaw=visualRoot.Find("RightClaw");
            AddClawTeeth(leftClaw,-1f,obsidian,plated,glow);
            AddClawTeeth(rightClaw,1f,obsidian,plated,glow);
        }

        private static void AddClawTeeth(Transform claw,float side,Material obsidian,Material plated,Material glow)
        {
            if(claw==null)return;
            for(int i=0;i<3;i++)
            {
                float z=.90f+i*.16f;
                HotZoneCreatureVisualFactory.Primitive(PrimitiveType.Capsule,"PincerTooth_"+i,claw,
                    new Vector3(side*(1.22f+i*.08f),.02f,z),new Vector3(.045f,.11f,.045f),Quaternion.Euler(74f,0,side*(18f-i*4f)),i==2?glow:obsidian);
            }
            HotZoneCreatureVisualFactory.Primitive(PrimitiveType.Cube,"ClawArmorBand",claw,
                new Vector3(side*.74f,.03f,.52f),new Vector3(.22f,.24f,.14f),Quaternion.Euler(0,side*25f,0),plated);
        }

        private void BuildTailDetails(Material obsidian,Material plated,Material glow)
        {
            tailRoot=visualRoot.Find("Tail");
            if(tailRoot==null)return;

            for(int i=0;i<6;i++)
            {
                float t=i/5f;
                Vector3 p=new(0,.18f+i*.30f,-i*.20f+i*i*.025f);
                float width=Mathf.Lerp(.38f,.20f,t);
                HotZoneCreatureVisualFactory.Primitive(PrimitiveType.Cube,"TailArmorBand_"+i,tailRoot,
                    p+new Vector3(0,.08f,0),new Vector3(width,.055f,width*.82f),Quaternion.Euler(i*3f,0,0),i%2==0?plated:obsidian);
                if(i==2||i==4)
                    HotZoneCreatureVisualFactory.Primitive(PrimitiveType.Cube,"TailHeatBand_"+i,tailRoot,
                        p+new Vector3(0,.145f,.01f),new Vector3(width*.52f,.020f,.055f),Quaternion.identity,glow);
            }

            // A hooked secondary barb makes the stinger silhouette more threatening.
            HotZoneCreatureVisualFactory.Primitive(PrimitiveType.Capsule,"StingerHook",tailRoot,
                new Vector3(0,2.39f,-.35f),new Vector3(.105f,.30f,.105f),Quaternion.Euler(54f,0,0),obsidian);
            HotZoneCreatureVisualFactory.Primitive(PrimitiveType.Sphere,"StingerCore",tailRoot,
                new Vector3(0,2.19f,-.22f),new Vector3(.13f,.13f,.13f),Quaternion.identity,glow);
        }

        private void Update()
        {
            if(!built){TryBuild();return;}
            float t=Time.time;

            if(leftMandible!=null)
                leftMandible.localRotation=Quaternion.Euler(69f,-18f-Mathf.Sin(t*3.4f)*6f,-12f);
            if(rightMandible!=null)
                rightMandible.localRotation=Quaternion.Euler(69f,18f+Mathf.Sin(t*3.4f)*6f,12f);

            for(int i=0;i<dorsalSpikes.Count;i++)
            {
                Transform spike=dorsalSpikes[i];
                if(spike==null)continue;
                float pulse=1f+Mathf.Sin(t*2.1f+i*.48f)*.045f;
                Vector3 s=spike.localScale;
                spike.localScale=new Vector3(s.x,.22f*2f*pulse,s.z);
            }

            if(lavaGlow!=null&&lavaGlow.HasProperty("_EmissionColor"))
            {
                float power=4.6f+Mathf.Sin(t*3.0f)*1.1f;
                lavaGlow.SetColor("_EmissionColor",lavaGlowBase*power);
            }
        }

        private void OnDestroy()=>HotZoneCreatureVisualFactory.Release(ownedMaterials);
    }
}
