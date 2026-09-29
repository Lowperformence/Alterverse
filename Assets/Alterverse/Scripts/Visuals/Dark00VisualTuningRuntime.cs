using System.Collections.Generic;
using Alterverse.Combat;
using Alterverse.Match;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Small DARK 00 presentation tuning layer: enlarges the Hot Zone orb eye and gives
    /// the healing guard mechs a neutral grey body while preserving their white light parts.
    /// Visual-only: combat, colliders and navigation are untouched.
    /// </summary>
    public sealed class Dark00VisualTuningRuntime : MonoBehaviour
    {
        private readonly HashSet<HealingGuardMech> styledGuards=new();
        private readonly Dictionary<Renderer,Material> originalMaterials=new();
        private readonly List<Material> ownedMaterials=new();
        private GameObject activeWorld;
        private float nextScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if(GameObject.Find("DF_Dark00VisualTuningRuntime")!=null)return;
            GameObject go=new("DF_Dark00VisualTuningRuntime");
            DontDestroyOnLoad(go);
            go.AddComponent<Dark00VisualTuningRuntime>();
        }

        private void Update()
        {
            if(Time.unscaledTime<nextScan)return;
            nextScan=Time.unscaledTime+.20f;

            if(!ArenaVariantRuntime.IsDark00)
            {
                if(activeWorld!=null)ResetState();
                return;
            }

            GameObject world=GameObject.Find("DF_MatchWorld");
            if(world==null)return;
            if(activeWorld!=world)
            {
                ResetState();
                activeWorld=world;
            }

            StyleGuards(world.transform);
            EnlargeOrbEyes(world.transform);
        }

        private void StyleGuards(Transform world)
        {
            HealingGuardMech[] guards=world.GetComponentsInChildren<HealingGuardMech>(true);
            for(int i=0;i<guards.Length;i++)
            {
                HealingGuardMech guard=guards[i];
                if(guard==null||styledGuards.Contains(guard))continue;

                Renderer[] renderers=guard.GetComponentsInChildren<Renderer>(true);
                for(int r=0;r<renderers.Length;r++)
                {
                    Renderer renderer=renderers[r];
                    Material source=renderer!=null?renderer.sharedMaterial:null;
                    if(renderer==null||source==null)continue;

                    originalMaterials[renderer]=source;
                    Material material=new(source){name=source.name+"_Dark00Grey"};
                    ownedMaterials.Add(material);

                    string n=renderer.gameObject.name;
                    bool luminous=n.Contains("Strip")||n.Contains("Visor")||n.Contains("SensorEye")||
                                  n.Contains("PodLens")||n.Contains("AntennaTip")||n.Contains("Energy");
                    Color color=luminous?new Color(.96f,.98f,1f,1f):new Color(.34f,.36f,.39f,1f);

                    if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);
                    if(material.HasProperty("_Color"))material.SetColor("_Color",color);
                    if(luminous)
                    {
                        material.EnableKeyword("_EMISSION");
                        if(material.HasProperty("_EmissionColor"))material.SetColor("_EmissionColor",color*4.2f);
                    }
                    else if(material.HasProperty("_EmissionColor"))
                    {
                        material.SetColor("_EmissionColor",Color.black);
                    }

                    renderer.sharedMaterial=material;
                }
                styledGuards.Add(guard);
            }
        }

        private static void EnlargeOrbEyes(Transform world)
        {
            HotZoneTank[] tanks=world.GetComponentsInChildren<HotZoneTank>(true);
            for(int i=0;i<tanks.Length;i++)
            {
                HotZoneTank tank=tanks[i];
                if(tank==null)continue;
                Transform[] parts=tank.GetComponentsInChildren<Transform>(true);
                for(int p=0;p<parts.Length;p++)
                {
                    Transform part=parts[p];
                    if(part==null||part.name!="RedEye")continue;
                    // Original DARK 00 eye is .31/.31/.12. This is exactly 3x larger.
                    part.localScale=new Vector3(.93f,.93f,.36f);
                }
            }
        }

        private void ResetState()
        {
            foreach(KeyValuePair<Renderer,Material> entry in originalMaterials)
                if(entry.Key!=null&&entry.Value!=null)entry.Key.sharedMaterial=entry.Value;

            for(int i=0;i<ownedMaterials.Count;i++)
                if(ownedMaterials[i]!=null)Destroy(ownedMaterials[i]);

            ownedMaterials.Clear();
            originalMaterials.Clear();
            styledGuards.Clear();
            activeWorld=null;
        }

        private void OnDestroy()=>ResetState();
    }
}
