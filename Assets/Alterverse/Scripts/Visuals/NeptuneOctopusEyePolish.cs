using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Replaces the original single Neptune eye with two large orange emissive eyes.
    /// Visual-only; combat, hitboxes and Hot Zone behaviour remain unchanged.
    /// </summary>
    public sealed class NeptuneOctopusEyePolish : MonoBehaviour
    {
        const string VisualRootName="DF_NeptuneOctopusVisual";
        const string EyeRootName="DF_NeptuneOrangeEyes";
        Transform eyeRoot;
        Material orangeEye;
        float nextResolve;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if(GameObject.Find("DF_NeptuneOctopusEyePolishManager")!=null)return;
            GameObject go=new("DF_NeptuneOctopusEyePolishManager");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<NeptuneOctopusEyePolishManager>();
        }

        void Update()
        {
            if(eyeRoot!=null)return;
            if(Time.unscaledTime<nextResolve)return;
            nextResolve=Time.unscaledTime+.20f;

            Transform visual=transform.Find(VisualRootName);
            if(visual==null)return;
            BuildEyes(visual);
        }

        void BuildEyes(Transform visual)
        {
            Transform existing=visual.Find(EyeRootName);
            if(existing!=null){eyeRoot=existing;return;}

            DisableOldEye(visual,"SingleEye");
            DisableOldEye(visual,"EyePupil");

            orangeEye=MakeMaterial("DF_NeptuneOctopusOrangeEye",new Color(1f,.24f,.015f,1f),true,4.8f,.72f);

            eyeRoot=new GameObject(EyeRootName).transform;
            eyeRoot.SetParent(visual,false);
            eyeRoot.localPosition=Vector3.zero;
            eyeRoot.localRotation=Quaternion.identity;

            BuildEye(eyeRoot,-.48f);
            BuildEye(eyeRoot,.48f);
        }

        void BuildEye(Transform parent,float x)
        {
            GameObject eye=Part(PrimitiveType.Sphere,x<0?"OrangeEyeL":"OrangeEyeR",parent,
                new Vector3(x,.98f,.55f),new Vector3(.52f,.46f,.20f),orangeEye);
            eye.transform.localRotation=Quaternion.identity;
        }

        static void DisableOldEye(Transform root,string childName)
        {
            Transform child=root.Find(childName);
            if(child==null)return;
            Renderer[] renderers=child.GetComponentsInChildren<Renderer>(true);
            for(int i=0;i<renderers.Length;i++)renderers[i].enabled=false;
        }

        static GameObject Part(PrimitiveType type,string name,Transform parent,Vector3 localPosition,Vector3 localScale,Material material)
        {
            GameObject go=GameObject.CreatePrimitive(type);
            go.name=name;
            go.transform.SetParent(parent,false);
            go.transform.localPosition=localPosition;
            go.transform.localScale=localScale;
            Collider collider=go.GetComponent<Collider>();
            if(collider!=null)Object.Destroy(collider);
            Renderer renderer=go.GetComponent<Renderer>();
            if(renderer!=null)renderer.sharedMaterial=material;
            return go;
        }

        static Material MakeMaterial(string name,Color color,bool emission,float emissionPower,float smoothness)
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit");
            if(shader==null)shader=Shader.Find("Standard");
            if(shader==null)return null;
            Material material=new(shader){name=name};
            if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);
            if(material.HasProperty("_Color"))material.SetColor("_Color",color);
            if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",smoothness);
            if(emission)
            {
                material.EnableKeyword("_EMISSION");
                if(material.HasProperty("_EmissionColor"))material.SetColor("_EmissionColor",color*emissionPower);
            }
            return material;
        }

        void OnDestroy()
        {
            if(orangeEye!=null)Object.Destroy(orangeEye);
        }
    }

    internal sealed class NeptuneOctopusEyePolishManager : MonoBehaviour
    {
        float nextScan;

        void Update()
        {
            if(Time.unscaledTime<nextScan)return;
            nextScan=Time.unscaledTime+.25f;
            if(!Alterverse.Match.ArenaVariantRuntime.IsNeptune)return;

            NeptuneOctopusVisualOverride[] octopuses=Object.FindObjectsByType<NeptuneOctopusVisualOverride>(FindObjectsInactive.Exclude,FindObjectsSortMode.None);
            for(int i=0;i<octopuses.Length;i++)
            {
                NeptuneOctopusVisualOverride octopus=octopuses[i];
                if(octopus==null)continue;
                if(octopus.GetComponent<NeptuneOctopusEyePolish>()==null)
                    octopus.gameObject.AddComponent<NeptuneOctopusEyePolish>();
            }
        }
    }
}
