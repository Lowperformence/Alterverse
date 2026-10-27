using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>Owner-supplied R35, with separate game materials and animated original engine/hatch pivots.</summary>
    public sealed class InsertionTransportModel : MonoBehaviour
    {
        private Transform r35Hatch, portEngine, starboardEngine;
        private bool built;
        public void Build(int team)
        {
            if(built)return;
            var prefab=Resources.Load<GameObject>("InsertionTransport/R35");
            if(prefab==null)throw new System.InvalidOperationException("R35 insertion prefab is missing: Resources/InsertionTransport/R35");
            GameObject ship=Instantiate(prefab,transform,false);
            ship.name="R35 / DIMENSIONS FRONT";
            ship.transform.localPosition=Vector3.zero;
            ship.transform.localRotation=Quaternion.identity;
            ship.transform.localScale=Vector3.one*1.05f;
            r35Hatch=ship.transform.Find("Dropship_Hatch");
            portEngine=ship.transform.Find("Dropship_R35-Engine1");
            starboardEngine=ship.transform.Find("Dropship_R35-Engine2");
            var hull=Resources.Load<Material>(team==0?"InsertionTransport/R35_Zero":"InsertionTransport/R35_Lumen");
            // Shared imported meshes and materials: no per-renderer material copies or flight-time allocation.
            if(hull!=null)
                foreach(var renderer in ship.GetComponentsInChildren<Renderer>(true))renderer.sharedMaterial=hull;
            built=true;
            Pose(0f,0f);
        }

        public void Pose(float doorOpen,float flightProgress)
        {
            if(!built)return;
            float arrive=MatchInsertionTransport.FlightEase(flightProgress);
            float depart=MatchInsertionTransport.FlightEase((flightProgress-2f)*.5f);
            float tilt=-82f*arrive*(1f-depart);
            if(portEngine!=null)portEngine.localRotation=Quaternion.Euler(tilt,0f,0f);
            if(starboardEngine!=null)starboardEngine.localRotation=Quaternion.Euler(tilt,0f,0f);
            if(r35Hatch!=null)r35Hatch.localRotation=Quaternion.Euler(-49.4f*Mathf.Clamp01(doorOpen)*(1f-depart),0f,0f);
        }
    }
}

