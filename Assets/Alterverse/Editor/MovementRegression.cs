#if UNITY_EDITOR
using System.Collections;
using Alterverse.Characters;
using Alterverse.Combat;
using UnityEditor;
using UnityEngine;

namespace Alterverse.EditorChecks
{
    public sealed class MovementRegression : MonoBehaviour
    {
        [MenuItem("DIMENSIONS FRONT/Checks/Movement collision and dash (Play mode)")]
        private static void RunChecks()
        {
            if(!Application.isPlaying||Time.timeScale<=0)
            {Debug.LogWarning("Start a running match before the movement checks.");return;}
            new GameObject("DF_MovementRegression").AddComponent<MovementRegression>().StartCoroutine("Run");
        }
        private static void Check(bool condition,string message)
        {if(!condition)throw new System.InvalidOperationException("Movement regression: "+message);}
        private IEnumerator Run()
        {
            try
            {
                var actor=new GameObject("DF_MovementCheckActor");actor.transform.SetParent(transform);
                actor.transform.position=new Vector3(0,80,0);
                var cc=actor.AddComponent<CharacterController>();cc.height=2;cc.radius=.5f;
                var health=actor.AddComponent<Health>();var body=actor.AddComponent<HeroLocomotion>();
                var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.SetParent(transform);
                floor.transform.position=new Vector3(0,78.8f,0);floor.transform.localScale=new Vector3(20,.2f,20);
                var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.SetParent(transform);
                wall.transform.position=new Vector3(0,80,2);wall.transform.localScale=new Vector3(8,4,.2f);
                Physics.SyncTransforms();
                body.Move(Vector3.forward*6,40,33,false);
                Check(actor.transform.position.z<1.55f,"swept movement crossed a solid wall");
                Relocate(actor,cc,body);Physics.SyncTransforms();
                body.Move(new Vector3(2,0,3),40,33);
                Check(actor.transform.position.x>1.5f&&actor.transform.position.z<1.55f,"wall slide lost its tangent or crossed the wall");
                Relocate(actor,cc,body);Physics.SyncTransforms();
                Check(body.BeginDash(actor.transform.position+Vector3.forward*6),"wall approach dash refused");
                float wallTimeout=Time.unscaledTime+1;
                while(body.IsDashing&&Time.unscaledTime<wallTimeout)yield return null;
                Check(!body.IsDashing&&actor.transform.position.z<1.55f,"dash crossed a solid wall");
                wall.SetActive(false);Relocate(actor,cc,body);Physics.SyncTransforms();
                Check(body.BeginDash(actor.transform.position+Vector3.right*3),"clear dash refused");
                Check(actor.transform.position.x==0,"dash teleported at cast time");
                float timeout=Time.unscaledTime+1;
                while(body.IsDashing&&Time.unscaledTime<timeout)yield return null;
                Check(Mathf.Abs(actor.transform.position.x-3)<.05f,"dash distance changed");
                yield return null;
                Check(body.BeginDash(actor.transform.position+Vector3.right*3),"second dash refused");
                yield return null;
                actor.GetComponent<HeroCombatStatus>().Stun(.5f);
                Vector3 stopped=actor.transform.position;yield return null;
                Check(!body.IsDashing&&(actor.transform.position-stopped).sqrMagnitude<.001f,"stun did not cancel dash");
                actor.GetComponent<HeroCombatStatus>().Clear();yield return null;
                Check(body.BeginDash(actor.transform.position+Vector3.left*3),"dash refused after cleared status");
                health.TakeDamage(1000);stopped=actor.transform.position;yield return null;
                Check(!body.IsDashing&&(actor.transform.position-stopped).sqrMagnitude<.001f,"death did not cancel dash");
                Debug.Log("DIMENSIONS FRONT: movement wall, slide, dash distance, stun and death checks passed.");
            }
            finally {Destroy(gameObject);}
        }
        private static void Relocate(GameObject actor,CharacterController cc,HeroLocomotion body)
        {cc.enabled=false;actor.transform.position=new Vector3(0,80,0);cc.enabled=true;body.ResetMotion();}
    }
}
#endif
