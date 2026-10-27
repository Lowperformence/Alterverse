using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>Owns visual resources and follows the original pickup availability transform without moving it.</summary>
    [DefaultExecutionOrder(1400)]
    public sealed class StationBuildingVisual : MonoBehaviour
    {
        Renderer[] original;
        bool[] originalEnabled;
        Material[] materials;
        GameObject[] external;
        Transform flatSymbol,signal;
        public void Configure(Renderer[] previous,Material[] owned,GameObject[] attached,Transform symbol,Transform availability)
        {
            original=previous;materials=owned;external=attached;flatSymbol=symbol;signal=availability;
            originalEnabled=new bool[previous.Length];
            for(int i=0;i<previous.Length;i++){originalEnabled[i]=previous[i].enabled;previous[i].enabled=false;}
            LateUpdate();
        }
        void LateUpdate()
        {
            if(flatSymbol==null||signal==null)return;
            // Legacy plus still owns SetActive for collection/respawn. Cancel its visual bob and scale
            // on this child only; never write the existing source transform or availability state.
            flatSymbol.position=transform.position;flatSymbol.rotation=transform.rotation;
            Vector3 scale=signal.lossyScale;
            flatSymbol.localScale=new Vector3(1f/Mathf.Max(.001f,Mathf.Abs(scale.x)),1f/Mathf.Max(.001f,Mathf.Abs(scale.y)),1f/Mathf.Max(.001f,Mathf.Abs(scale.z)));
        }
        void OnDestroy()
        {
            if(original!=null)for(int i=0;i<original.Length;i++)if(original[i]!=null)original[i].enabled=originalEnabled[i];
            if(external!=null)foreach(var obj in external)if(obj!=null)Destroy(obj);
            if(materials!=null)foreach(var material in materials)if(material!=null)Destroy(material);
        }
    }
}
