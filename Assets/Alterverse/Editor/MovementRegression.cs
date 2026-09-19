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
                CheckInputDirections(actor);
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
                Debug.Log("DIMENSIONS FRONT: movement input direction, wall, slide, dash distance, stun and death checks passed.");
            }
            finally {Destroy(gameObject);}
        }
        private void CheckInputDirections(GameObject actor)
        {
            var motor=actor.AddComponent<HeroMotor>();motor.enabled=false;
            var cameraBasis=new GameObject("DF_InputCameraBasis");cameraBasis.transform.SetParent(transform);
            cameraBasis.transform.rotation=Quaternion.Euler(45,90,0);
            typeof(HeroMotor).GetField("cameraTransform",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(motor,cameraBasis.transform);
            // No Update/yield between inputs: ability release must see this frame's direction.
            motor.SetMoveInput(Vector2.up);
            Check(Vector3.Dot(motor.CurrentMoveDirection,Vector3.right)>.999f,"current input ignored camera yaw");
            motor.SetMoveInput(Vector2.down);
            Check(Vector3.Dot(motor.CurrentMoveDirection,Vector3.left)>.999f,"same-frame reversal retained stale direction");
            motor.SetMoveInput(Vector2.one);
            Check(Mathf.Abs(motor.CurrentMoveDirection.magnitude-1)<.001f,"diagonal input increased speed");
            motor.SetMoveInput(new Vector2(0,.25f));
            Check(Mathf.Abs(motor.CurrentMoveDirection.magnitude-.25f)<.001f,"analog magnitude was lost");
            motor.SetMoveInput(Vector2.zero);
            Check(!motor.HasMoveInput&&motor.CurrentMoveDirection==Vector3.zero,"released input retained movement");
            motor.SetMoveInput(Vector2.up);motor.ClearInput();
            Check(!motor.HasMoveInput&&motor.CurrentMoveDirection==Vector3.zero,"canceled input retained movement");
        }
        private static void Relocate(GameObject actor,CharacterController cc,HeroLocomotion body)
        {cc.enabled=false;actor.transform.position=new Vector3(0,80,0);cc.enabled=true;body.ResetMotion();}
    }
}
#endif
