using System.Collections.Generic;
using Alterverse.Characters;
using Alterverse.Combat;
using UnityEngine;
namespace Alterverse.Visuals
{
    [DefaultExecutionOrder(4100)]
    public sealed class CosmicNebulaConcealment:MonoBehaviour
    {
        private readonly Dictionary<Renderer,bool> hidden=new();
        private readonly List<Renderer> scan=new();
        private Health health;
        private void LateUpdate()
        {
            if(Application.isBatchMode)return;
            health??=GetComponent<Health>();
            var viewer=LocalHeroContext.Member;
            if(viewer==null||!CosmicNebulaField.HiddenFrom(viewer.gameObject,health)){Restore();return;}
            scan.Clear();GetComponentsInChildren(true,scan);
            foreach(var renderer in scan)
            {
                if(!hidden.ContainsKey(renderer))hidden.Add(renderer,renderer.forceRenderingOff);
                renderer.forceRenderingOff=true;
            }
        }
        private void Restore()
        {foreach(var pair in hidden)if(pair.Key!=null)pair.Key.forceRenderingOff=pair.Value;hidden.Clear();}
        private void OnDisable()=>Restore();
        private void OnDestroy()=>Restore();
    }
}
