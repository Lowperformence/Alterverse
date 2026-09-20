#if UNITY_EDITOR
using System.Collections;
using Alterverse.Characters;
using Alterverse.Combat;
using UnityEngine;

namespace Alterverse.Diagnostics
{
    // Play-mode runner lives outside an Editor folder so Unity can attach it to a GameObject.
    // The UNITY_EDITOR guard keeps it out of Android/player builds.
    public sealed class MovementRegressionRunner : MonoBehaviour
    {
        public void Begin()=>StartCoroutine(Run());

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
                CheckFreeRoamRoutesAndSolidRange();
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

        private void CheckFreeRoamRoutesAndSolidRange()
        {
            var actor=new GameObject("DF_RouteCheck");actor.transform.SetParent(transform);
            var route=actor.AddComponent<Alterverse.AI.BotLanePlan>();
            for(int team=0;team<2;team++)
            {
                float homeX=team==0?-27.5f:27.5f;
                actor.transform.position=new Vector3(homeX,80,0);
                route.Initialize(team,16f); // legacy lane argument must not recreate lane routing.
                Vector3 roam=route.GetDestination(Alterverse.AI.MobaBotBrain.BotState.Roam);
                Check(Mathf.Abs(roam.x)<=12.1f&&Mathf.Abs(roam.z)<=13.1f,
                    "free-roam target left the intended open combat sectors");
                Check(Mathf.Abs(Mathf.Abs(roam.z)-16f)>1f,
                    "legacy z=+/-16 lane target returned");
                Vector3 retreat=route.GetDestination(Alterverse.AI.MobaBotBrain.BotState.Retreat);
                Check(Mathf.Abs(retreat.x-homeX)<.01f&&Mathf.Abs(retreat.z)<.01f,
                    "retreat destination is not the team home");
                route.ResetRoute();
                Vector3 resetRoam=route.GetDestination(Alterverse.AI.MobaBotBrain.BotState.Roam);
                Check(Mathf.Abs(resetRoam.x)<=12.1f&&Mathf.Abs(resetRoam.z)<=13.1f,
                    "route reset did not return to a free-roam combat sector");
            }
            actor.transform.position=new Vector3(0,80,-3);
            actor.AddComponent<Health>();var locomotion=actor.AddComponent<HeroLocomotion>();
            var obstacle=GameObject.CreatePrimitive(PrimitiveType.Capsule);obstacle.transform.SetParent(transform);
            obstacle.transform.position=new Vector3(0,80,0);
            var capsule=obstacle.GetComponent<CapsuleCollider>();capsule.radius=.72f;capsule.height=2.05f;
            var hp=obstacle.AddComponent<Health>();Physics.SyncTransforms();
            Check(locomotion.TryFindObstacleWaypoint(capsule,new Vector3(0,80,3),1,out Vector3 point),"guardian has no swept-clear local waypoint");
            Check(Mathf.Abs(point.x)>1.2f,"guardian waypoint lacks capsule clearance");
            locomotion.Move(point-actor.transform.position,40,33);
            Check(Vector3.Distance(actor.transform.position,point)<.05f,"waypoint probe disagrees with real movement");
            Vector3 attackOrigin=new Vector3(0,80,-2);
            Check(HeroCombatRules.BasicDistance(attackOrigin,hp)<1.4f,"solid surface remains out of melee range");
            obstacle.AddComponent<HeroIdentity>();
            Check(Mathf.Abs(HeroCombatRules.BasicDistance(attackOrigin,hp)-2)<.01f,"hero centre range changed");
            obstacle.SetActive(false);actor.SetActive(false);
        }

        private void CheckInputDirections(GameObject actor)
        {
            var motor=actor.AddComponent<HeroMotor>();motor.enabled=false;
            var cameraBasis=new GameObject("DF_InputCameraBasis");cameraBasis.transform.SetParent(transform);
            cameraBasis.transform.rotation=Quaternion.Euler(45,90,0);
            typeof(HeroMotor).GetField("cameraTransform",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(motor,cameraBasis.transform);
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
