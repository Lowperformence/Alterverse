using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>Keeps the generated high-detail weapon shells locked to the authored gameplay pivots.</summary>
    [DefaultExecutionOrder(2100)]
    internal sealed class EpicWeaponAnchorFollowSystem : MonoBehaviour
    {
        private static EpicWeaponAnchorFollowSystem instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if(instance!=null)return;
            GameObject go=new GameObject("DF_EpicWeaponAnchorFollow");
            DontDestroyOnLoad(go);
            instance=go.AddComponent<EpicWeaponAnchorFollowSystem>();
        }

        private void LateUpdate()
        {
            EpicWeaponExpansionPresentation[] presentations=FindObjectsByType<EpicWeaponExpansionPresentation>(FindObjectsSortMode.None);
            for(int i=0;i<presentations.Length;i++)Follow(presentations[i]);
        }

        private static void Follow(EpicWeaponExpansionPresentation presentation)
        {
            if(presentation==null)return;
            Transform visual=FindRecursive(presentation.transform,"DF_VisualRoot");
            if(visual==null)return;
            Transform generated=FindDirect(visual,"DF_EpicWeaponVariant_2")??FindDirect(visual,"DF_EpicWeaponVariant_3");
            if(generated==null)return;

            for(int i=0;i<generated.childCount;i++)
            {
                Transform anchor=generated.GetChild(i);
                string pivotName=PivotFor(anchor.name);
                Transform pivot=FindRecursive(visual,pivotName);
                if(pivot==null)continue;
                anchor.SetPositionAndRotation(pivot.position,pivot.rotation);
                Vector3 parentScale=anchor.parent.lossyScale;
                Vector3 targetScale=pivot.lossyScale;
                anchor.localScale=new Vector3(
                    SafeDivide(targetScale.x,parentScale.x),
                    SafeDivide(targetScale.y,parentScale.y),
                    SafeDivide(targetScale.z,parentScale.z));
            }
        }

        private static string PivotFor(string anchorName)
        {
            if(anchorName.Contains("VeyrL"))return "DF_WeaponL";
            if(anchorName.Contains("VeyrR"))return "DF_WeaponR";
            if(anchorName.Contains("RookL"))return "DF_GauntletL";
            if(anchorName.Contains("RookR"))return "DF_GauntletR";
            return "DF_WeaponR";
        }

        private static float SafeDivide(float value,float divisor)
        {
            return Mathf.Abs(divisor)<.0001f?1f:value/divisor;
        }

        private static Transform FindDirect(Transform root,string name)
        {
            for(int i=0;i<root.childCount;i++)if(root.GetChild(i).name==name)return root.GetChild(i);
            return null;
        }

        private static Transform FindRecursive(Transform root,string name)
        {
            if(root==null)return null;
            if(root.name==name)return root;
            for(int i=0;i<root.childCount;i++)
            {
                Transform found=FindRecursive(root.GetChild(i),name);
                if(found!=null)return found;
            }
            return null;
        }
    }
}
