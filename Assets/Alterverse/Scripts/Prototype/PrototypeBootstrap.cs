using Alterverse.CameraSystem;
using Alterverse.Characters;
using Alterverse.Combat;
using UnityEngine;

namespace Alterverse.Prototype
{
    public sealed class PrototypeBootstrap : MonoBehaviour
    {
        [SerializeField] private bool buildOnStart = true;

        private void Start()
        {
            if (buildOnStart) BuildPrototype();
        }

        [ContextMenu("Build Prototype")]
        public void BuildPrototype()
        {
            if (GameObject.Find("AV_Player") != null) return;

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "AV_Arena";
            floor.transform.localScale = new Vector3(3f, 1f, 3f);

            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "AV_Player";
            player.transform.position = new Vector3(0f, 1f, 0f);
            Destroy(player.GetComponent<CapsuleCollider>());
            CharacterController controller = player.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.5f;
            player.AddComponent<Health>();
            player.AddComponent<HeroMotor>();
            player.AddComponent<BasicAttack>();

            GameObject dummy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            dummy.name = "AV_TargetDummy";
            dummy.transform.position = new Vector3(4f, 1f, 2f);
            dummy.AddComponent<Health>();

            UnityEngine.Camera cam = UnityEngine.Camera.main;
            if (cam == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                cam = cameraObject.AddComponent<UnityEngine.Camera>();
            }

            MobaCamera mobaCamera = cam.GetComponent<MobaCamera>();
            if (mobaCamera == null) mobaCamera = cam.gameObject.AddComponent<MobaCamera>();
            mobaCamera.SetTarget(player.transform);
            cam.transform.position = player.transform.position + new Vector3(0f, 12f, -9f);

            Debug.Log("ALTERVERSE prototype ready: WASD to move, SPACE to attack the nearby dummy.");
        }
    }
}
