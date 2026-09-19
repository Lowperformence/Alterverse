using Alterverse.Combat;
using Alterverse.Match;
using UnityEngine;
using UnityEngine.InputSystem;
namespace Alterverse.Characters
{
    [DefaultExecutionOrder(-25)]
    [RequireComponent(typeof(CharacterController),typeof(Health))]
    public sealed class HeroMotor : MonoBehaviour
    {
        [SerializeField,Min(.1f)] private float fallbackMoveSpeed=6.5f;
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private float arenaHalfWidth=41f,arenaHalfHeight=35f;
        private HeroLocomotion body;
        private HeroCombatProfile profile;
        private PlanetBuffReceiver planetBuff;
        private Vector2 moveInput;
        private Vector3 lastMoveDirection=Vector3.forward;
        private int externalFrame=-1;
        public Vector3 LastMoveDirection=>lastMoveDirection;
        // Touch abilities run before this component's Update. Resolve their direction from
        // the current input instead of the previous movement frame.
        public Vector3 CurrentMoveDirection
        {
            get
            {
                if(cameraTransform==null&&Camera.main!=null)cameraTransform=Camera.main.transform;
                Vector3 forward=cameraTransform!=null?cameraTransform.forward:Vector3.forward;
                forward.y=0;if(forward.sqrMagnitude<.001f)forward=Vector3.forward;forward.Normalize();
                return Vector3.ClampMagnitude(forward*moveInput.y+Vector3.Cross(Vector3.up,forward)*moveInput.x,1);
            }
        }
        public bool HasMoveInput=>moveInput.sqrMagnitude>.0001f;
        public float MoveSpeed=>(profile!=null?profile.MoveSpeed:fallbackMoveSpeed)*(planetBuff!=null?planetBuff.MoveSpeedMultiplier:1f)*body.SpeedMultiplier;
        private void Awake()
        {
            body=GetComponent<HeroLocomotion>()??gameObject.AddComponent<HeroLocomotion>();
            profile=GetComponent<HeroCombatProfile>();
            planetBuff=GetComponent<PlanetBuffReceiver>()??gameObject.AddComponent<PlanetBuffReceiver>();
        }
        private void Update()
        {
            if(!body.CanMove){ClearInput();return;}
            if(externalFrame!=Time.frameCount)ReadKeyboardInput();
            Vector3 direction=CurrentMoveDirection;
            if(direction.sqrMagnitude>.0001f)lastMoveDirection=direction.normalized;
            if(body.OwnsMovementFrame)return;
            // No input smoothing or release inertia. Weight belongs to the visual pose.
            body.Move(direction*MoveSpeed*Time.deltaTime,arenaHalfWidth,arenaHalfHeight);
            body.FaceMovement(direction);
        }
        private void ReadKeyboardInput()
        {
            var k=Keyboard.current;
            moveInput=k==null?Vector2.zero:Vector2.ClampMagnitude(new Vector2((k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0),(k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0)),1);
        }
        public void SetMoveInput(Vector2 input){moveInput=Vector2.ClampMagnitude(input,1);externalFrame=Time.frameCount;}
        public void ClearInput(){moveInput=Vector2.zero;externalFrame=-1;}
        private void OnDisable(){ClearInput();if(body!=null)body.ResetMotion();}
        private void OnApplicationFocus(bool focused){if(!focused)ClearInput();}
        private void OnApplicationPause(bool paused){if(paused)ClearInput();}
    }
}
