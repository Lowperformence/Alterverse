using Alterverse.Combat;
using Alterverse.Characters;
using UnityEngine;

namespace Alterverse.UI
{
    // Pure presentation of the exact pose owned by the shared gesture component.
    [DefaultExecutionOrder(200)]
    public sealed class AbilityRangePreviewOverlay : MonoBehaviour
    {
        private ManualAiming aiming;
        private int shown;
        private readonly GameObject[] surfaces = new GameObject[2];
        private readonly Material[] materials = new Material[2];
        private static readonly int ColorId = Shader.PropertyToID("_Color"), ModeId = Shader.PropertyToID("_Mode"),
            ConeId = Shader.PropertyToID("_ConeCos"), WidthId = Shader.PropertyToID("_Width"),
            OpacityId = Shader.PropertyToID("_Opacity");
        private void LateUpdate()
        {
            shown = 0;
            aiming = aiming != null ? aiming : GetComponent<ManualAiming>();
            if (aiming == null || !aiming.Active || aiming.Slot > 3 || aiming.Cancelled) { Hide(); return; }
            AimSpec spec = aiming.Spec; AimPose pose = aiming.Pose;
            // Self-only abilities have no target geometry. Area effects still show their radius.
            if (spec.aimType == AimType.Self) { Hide(); return; }
            Vector3 origin = transform.position;
            Color color = AimingSettings.Current.previewColor;
            if (spec.aimType == AimType.Area)
            {
                Color range = color; range.a *= .45f;
                Show(0, origin, pose.Direction, spec.castRange, 0, spec, range);
                Show(1, pose.Point, pose.Direction, spec.aoeRadius, 0, spec, color);
            }
            else if (spec.aimType == AimType.SelfArea)
                Show(0, origin, pose.Direction, Mathf.Max(.7f, spec.aoeRadius), 0, spec, color);
            else if (aiming.Slot == 0 && spec.aimType == AimType.Projectile &&
                GetComponent<HeroIdentity>()?.Hero == HeroId.Nerok)
            {
                // The cone chooses one point. The anchor itself only hits along this narrow
                // path, which may turn toward that point and stop at a body or real cover.
                Color selection = color; selection.a *= .5f;
                Show(0, origin, pose.Direction, spec.castRange, 1, spec, selection);
                Vector3 end = HeroAttackDelivery.For(gameObject).PreviewProjectileEnd(pose, spec);
                Color flight = color; flight.a = Mathf.Min(1f, color.a + .25f);
                Show(1, origin, pose.CastDirection(origin), HeroCombatRules.Distance(origin, end), 2, spec, flight);
            }
            else
            {
                Show(0, origin, pose.Direction, spec.castRange, spec.aimType == AimType.Dash && spec.targetingMode != TargetingMode.Cone ? 2 : 1, spec, color);
                if (spec.aimType == AimType.Ally && pose.Target != null)
                    Show(1, pose.Target.transform.position, pose.Direction, .65f, 0, spec, color);
            }
            for (int i = 0; i < surfaces.Length; i++)
                if (surfaces[i] != null && (shown & (1 << i)) == 0 && surfaces[i].activeSelf) surfaces[i].SetActive(false);
        }
        private void Show(int index, Vector3 position, Vector3 direction, float range, int mode, AimSpec spec, Color color)
        {
            if (range <= 0f) return;
            if (surfaces[index] == null)
            {
                Shader shader = Resources.Load<Shader>("CombatVfx/ManualAimPreview");
                if (shader == null) return;
                GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Quad);
                surface.name = "Manual aim preview";
                Collider collider = surface.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
                Material material = new Material(shader);
                MeshRenderer renderer = surface.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
                surfaces[index] = surface; materials[index] = material;
            }
            position.y += .08f + index * .012f;
            surfaces[index].transform.SetPositionAndRotation(position, Quaternion.LookRotation(Vector3.down, AimGeometry.Direction(direction)));
            surfaces[index].transform.localScale = Vector3.one * range * 2f;
            materials[index].SetColor(ColorId, color); materials[index].SetFloat(ModeId, mode);
            materials[index].SetFloat(ConeId, Mathf.Cos(spec.coneAngle * .5f * Mathf.Deg2Rad));
            materials[index].SetFloat(WidthId, spec.projectileWidth / (range * 2f));
            bool directional = mode == 1 || mode == 2 && spec.aimType != AimType.Dash;
            float opacity = Mathf.Clamp01(AimingSettings.Current.previewOpacity);
            if (directional) opacity *= Mathf.Clamp01(AimingSettings.Current.conePreviewOpacity);
            materials[index].SetFloat(OpacityId, opacity);
            shown |= 1 << index;
            if (!surfaces[index].activeSelf) surfaces[index].SetActive(true);
        }
        private void Hide() { for (int i = 0; i < surfaces.Length; i++) if (surfaces[i] != null) surfaces[i].SetActive(false); }
        private void OnDisable() => Hide();
        private void OnDestroy()
        {
            for (int i = 0; i < surfaces.Length; i++)
            { if (surfaces[i] != null) Destroy(surfaces[i]); if (materials[i] != null) Destroy(materials[i]); }
        }
    }
}
