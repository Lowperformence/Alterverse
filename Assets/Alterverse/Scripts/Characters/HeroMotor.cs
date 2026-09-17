using Alterverse.Combat;
using UnityEngine;
using UnityEngine.InputSystem;
namespace Alterverse.Characters
{
    [RequireComponent(typeof(CharacterController)),RequireComponent(typeof(Health))]
    public sealed class HeroMotor:MonoBehaviour
    {
        [SerializeField,Min(.1f)]private float moveSpeed=6.5f;[SerializeField,Min(0f)]private float rotationSpeed=18f;[SerializeField]private Transform cameraTransform;[SerializeField]private float arenaHalfWidth=41f;[SerializeField]private float arenaHalfHeight=35f;private CharacterController controller;private Health health;private Vector2 moveInput;private bool externalInput;public float MoveSpeed=>moveSpeed;
        private void Awake(){controller=GetComponent<CharacterController>();health=GetComponent<Health>();if(cameraTransform==null&&Camera.main!=null)cameraTransform=Camera.main.transform;}
        private void Update(){if(health.IsDead){moveInput=Vector2.zero;return;}if(!externalInput)ReadKeyboardInput();Move(moveInput);externalInput=false;}
        private void ReadKeyboardInput(){if(Keyboard.current==null){moveInput=Vector2.zero;return;}float x=0,y=0;if(Keyboard.current.aKey.isPressed)x-=1;if(Keyboard.current.dKey.isPressed)x+=1;if(Keyboard.current.sKey.isPressed)y-=1;if(Keyboard.current.wKey.isPressed)y+=1;moveInput=Vector2.ClampMagnitude(new Vector2(x,y),1f);}
        public void SetMoveInput(Vector2 input){moveInput=Vector2.ClampMagnitude(input,1f);externalInput=true;}
        private void Move(Vector2 input){Vector3 forward=cameraTransform!=null?cameraTransform.forward:Vector3.forward,right=cameraTransform!=null?cameraTransform.right:Vector3.right;forward.y=0;right.y=0;forward.Normalize();right.Normalize();Vector3 direction=Vector3.ClampMagnitude(forward*input.y+right*input.x,1f);if(direction.sqrMagnitude>.001f)transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(direction,Vector3.up),rotationSpeed*Time.deltaTime);Vector3 velocity=direction*moveSpeed;if(!controller.isGrounded)velocity.y=-4f;controller.Move(velocity*Time.deltaTime);Vector3 clamped=transform.position;clamped.x=Mathf.Clamp(clamped.x,-arenaHalfWidth,arenaHalfWidth);clamped.z=Mathf.Clamp(clamped.z,-arenaHalfHeight,arenaHalfHeight);if((clamped-transform.position).sqrMagnitude>.0001f){controller.enabled=false;transform.position=clamped;controller.enabled=true;}}
    }
}
