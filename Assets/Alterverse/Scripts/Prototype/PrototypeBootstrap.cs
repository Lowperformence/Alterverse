using Alterverse.AI;
using Alterverse.CameraSystem;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.UI;
using UnityEngine;

namespace Alterverse.Prototype
{
    public sealed class PrototypeBootstrap : MonoBehaviour
    {
        [SerializeField] private bool buildOnStart = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (Object.FindFirstObjectByType<PrototypeBootstrap>() != null) return;
            new GameObject("AV_PrototypeBootstrap").AddComponent<PrototypeBootstrap>();
        }

        private void Start()
        {
            if (buildOnStart) BuildPrototype();
        }

        [ContextMenu("Build Prototype")]
        public void BuildPrototype()
        {
            if (GameObject.Find("AV_Player") != null) return;

            BuildArena();

            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "AV_Player";
            player.transform.position = new Vector3(-4f, 1f, -4f);
            Destroy(player.GetComponent<CapsuleCollider>());
            CharacterController controller = player.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.5f;

            Health playerHealth = player.AddComponent<Health>();
            playerHealth.Configure(140f);
            player.AddComponent<HeroMotor>();
            player.AddComponent<HeroRespawn>();
            player.AddComponent<BasicAttack>();
            VeyrAbilities abilities = player.AddComponent<VeyrAbilities>();
            SetColor(player, new Color(0.07f, 0.055f, 0.11f));

            GameObject enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            enemy.name = "AV_DimensionZeroHunter";
            enemy.transform.position = new Vector3(5f, 1f, 4f);
            Health enemyHealth = enemy.AddComponent<Health>();
            enemyHealth.Configure(180f);
            PrototypeEnemyBot bot = enemy.AddComponent<PrototypeEnemyBot>();
            bot.SetTarget(playerHealth);
            SetColor(enemy, new Color(0.62f, 0.055f, 0.09f));

            AddEnemyBlade(enemy.transform);
            AddVeyrBlade(player.transform);

            UnityEngine.Camera cam = UnityEngine.Camera.main;
            if (cam == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                cam = cameraObject.AddComponent<UnityEngine.Camera>();
            }

            cam.backgroundColor = new Color(0.035f, 0.025f, 0.055f);
            MobaCamera mobaCamera = cam.GetComponent<MobaCamera>();
            if (mobaCamera == null) mobaCamera = cam.gameObject.AddComponent<MobaCamera>();
            mobaCamera.SetTarget(player.transform);

            GameObject hudObject = new GameObject("AV_HUD");
            PrototypeHUD hud = hudObject.AddComponent<PrototypeHUD>();
            hud.Initialize(playerHealth, abilities, enemyHealth);

            Debug.Log("ALTERVERSE combat slice ready. WASD move, SPACE basic attack, Q/E/R/F abilities.");
        }

        private static void BuildArena()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "AV_Arena";
            floor.transform.localScale = new Vector3(3f, 1f, 3f);
            SetColor(floor, new Color(0.10f, 0.105f, 0.14f));

            CreateLaneStrip(new Vector3(0f, 0.035f, 0f), new Vector3(1.1f, 0.05f, 27f));
            CreateLaneStrip(new Vector3(0f, 0.04f, 0f), new Vector3(27f, 0.05f, 1.1f));

            Vector3[] pillarPositions =
            {
                new Vector3(-11f, 1.25f, -11f),
                new Vector3(11f, 1.25f, -11f),
                new Vector3(-11f, 1.25f, 11f),
                new Vector3(11f, 1.25f, 11f)
            };

            foreach (Vector3 position in pillarPositions)
            {
                GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pillar.name = "AV_RiftPillar";
                pillar.transform.position = position;
                pillar.transform.localScale = new Vector3(0.7f, 1.25f, 0.7f);
                SetColor(pillar, new Color(0.18f, 0.08f, 0.28f));
            }

            GameObject rift = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rift.name = "AV_CentralRift";
            rift.transform.position = new Vector3(0f, 0.08f, 0f);
            rift.transform.localScale = new Vector3(2.2f, 0.08f, 2.2f);
            Collider riftCollider = rift.GetComponent<Collider>();
            if (riftCollider != null) riftCollider.enabled = false;
            SetColor(rift, new Color(0.36f, 0.08f, 0.62f));
        }

        private static void CreateLaneStrip(Vector3 position, Vector3 scale)
        {
            GameObject strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            strip.name = "AV_RiftLane";
            strip.transform.position = position;
            strip.transform.localScale = scale;
            Collider collider = strip.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
            SetColor(strip, new Color(0.18f, 0.12f, 0.24f));
        }

        private static void AddVeyrBlade(Transform parent)
        {
            GameObject blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blade.name = "Veyr_ShadowBlade";
            blade.transform.SetParent(parent, false);
            blade.transform.localPosition = new Vector3(0.55f, 0.15f, 0.15f);
            blade.transform.localRotation = Quaternion.Euler(8f, 0f, -18f);
            blade.transform.localScale = new Vector3(0.12f, 1.45f, 0.18f);
            Collider collider = blade.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
            SetColor(blade, new Color(0.025f, 0.015f, 0.04f));
        }

        private static void AddEnemyBlade(Transform parent)
        {
            GameObject blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blade.name = "Hunter_RiftBlade";
            blade.transform.SetParent(parent, false);
            blade.transform.localPosition = new Vector3(-0.55f, 0.1f, 0.15f);
            blade.transform.localRotation = Quaternion.Euler(-8f, 0f, 18f);
            blade.transform.localScale = new Vector3(0.12f, 1.15f, 0.18f);
            Collider collider = blade.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
            SetColor(blade, new Color(0.38f, 0.02f, 0.035f));
        }

        private static void SetColor(GameObject target, Color color)
        {
            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer != null) renderer.material.color = color;
        }
    }
}
