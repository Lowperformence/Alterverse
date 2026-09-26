using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Visuals;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace Alterverse.UI
{
    /// <summary>
    /// Presentation-only mobile range preview. PrototypeHUD remains the sole owner of combat input.
    /// This overlay observes the same held ability button and makes the real range/radius readable on
    /// a small screen with strong contrast, while never casting an ability or changing gameplay values.
    /// </summary>
    [DefaultExecutionOrder(-160)]
    public sealed class AbilityRangePreviewOverlay : MonoBehaviour
    {
        private PlayerHeroAbilities abilities;
        private HeroIdentity identity;
        private Health health;
        private SoftTargeting targeting;
        private Camera view;
        private CombatHudLayout layout;
        private GUIStyle tagStyle;
        private GUIStyle tagShadowStyle;
        private int heldSlot = -1;
        private int touchId = -1;
        private bool mouseHeld;
        private bool inside;
        private readonly GameObject[] rangeDiscs = new GameObject[2];
        private readonly Material[] rangeMaterials = new Material[2];

        private void LateUpdate()
        {
            HideDiscs();
            if (heldSlot < 0 || !inside || !CanPreview()) return;
            AbilitySpec spec = abilities.SpecFor(heldSlot);
            Vector3 origin = transform.position; origin.y = .18f;
            Color color = PreviewColor(heldSlot);
            float radius = spec.Range > .01f ? spec.Range : spec.Radius;
            if (radius > .01f) ShowDisc(0, origin, radius, color);
            if (UsesMovementDirection(heldSlot) || spec.Range <= .01f || spec.Radius <= .01f) return;
            targeting ??= GetComponent<SoftTargeting>();
            Health current = targeting != null ? targeting.Current : null;
            // Only display an impact area for an actual target in casting range.
            // A sample circle at an arbitrary position suggests an aim point that does not exist.
            if (current == null) return;
            Vector3 center = current.transform.position;
            Vector3 offset = center - origin; offset.y = 0f;
            if (offset.sqrMagnitude > spec.Range * spec.Range) return;
            center.y = .20f;
            ShowDisc(1, center, spec.Radius, new Color(color.r, color.g, color.b, .72f));
        }

        private void ShowDisc(int index, Vector3 center, float radius, Color color)
        {
            if (rangeDiscs[index] == null)
            {
                // Resources reference retains the mobile shader in Android builds.
                Shader shader = Resources.Load<Shader>("CombatVfx/AbilityRangeDisc");
                if (shader == null) return;
                GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Quad);
                disc.name = "Ability range disc";
                disc.layer = gameObject.layer;
                Collider collider = disc.GetComponent<Collider>();
                collider.enabled = false;
                Destroy(collider);
                Material material = new Material(shader);
                MeshRenderer renderer = disc.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                rangeDiscs[index] = disc;
                rangeMaterials[index] = material;
            }
            Transform discTransform = rangeDiscs[index].transform;
            discTransform.SetPositionAndRotation(center, Quaternion.Euler(90f, 0f, 0f));
            discTransform.localScale = Vector3.one * (radius * 2f);
            rangeMaterials[index].SetColor("_Color", color);
            rangeDiscs[index].SetActive(true);
        }

        private void HideDiscs()
        {
            for (int i = 0; i < rangeDiscs.Length; i++)
                if (rangeDiscs[i] != null) rangeDiscs[i].SetActive(false);
        }

        private void OnDestroy()
        {
            for (int i = 0; i < rangeDiscs.Length; i++)
            {
                if (rangeDiscs[i] != null) Destroy(rangeDiscs[i]);
                if (rangeMaterials[i] != null) Destroy(rangeMaterials[i]);
            }
        }

        private void Awake()
        {
            abilities = GetComponent<PlayerHeroAbilities>();
            identity = GetComponent<HeroIdentity>();
            health = GetComponent<Health>();
            targeting = GetComponent<SoftTargeting>();
        }

        private void OnDisable() { Clear(); HideDiscs(); }
        private void OnApplicationFocus(bool focused) { if (!focused) { Clear(); HideDiscs(); } }
        private void OnApplicationPause(bool paused) { if (paused) { Clear(); HideDiscs(); } }

        private bool CanPreview()
        {
            if (abilities == null || health == null || health.IsDead || Time.timeScale <= 0f) return false;
            Alterverse.Multiplayer.CoopHeroSlot coop = GetComponent<Alterverse.Multiplayer.CoopHeroSlot>();
            return coop == null || !coop.IsHuman || coop.IsLocalHuman;
        }

        private void Update()
        {
            if (!CanPreview()) { Clear(); return; }
            layout = new CombatHudLayout(Screen.safeArea);
            if (layout.Safe.width <= 0f || layout.Safe.height <= 0f) { Clear(); return; }

            HandleTouchObservation();
            // Touch exclusively owns the mobile gesture. Mouse exists only for editor/desktop testing.
            if (touchId < 0 && (!EnhancedTouchSupport.enabled || ETouch.activeTouches.Count == 0))
                HandleMouseObservation();
        }

        private void HandleTouchObservation()
        {
            if (!EnhancedTouchSupport.enabled) return;
            var touches = ETouch.activeTouches;

            if (touchId >= 0)
            {
                bool found = false;
                for (int i = 0; i < touches.Count; i++)
                {
                    var touch = touches[i];
                    if (touch.touchId != touchId) continue;
                    found = true;
                    Vector2 point = layout.Point(touch.screenPosition);
                    inside = heldSlot >= 0 && layout.ContainsWithMargin(heldSlot, point);
                    if (touch.phase == UnityEngine.InputSystem.TouchPhase.Ended ||
                        touch.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                        Clear();
                    break;
                }
                if (!found) Clear();
                return;
            }

            for (int i = 0; i < touches.Count; i++)
            {
                var touch = touches[i];
                if (touch.phase != UnityEngine.InputSystem.TouchPhase.Began) continue;
                int slot = layout.Hit(layout.Point(touch.screenPosition));
                if (slot < 0 || slot > 3) continue;
                heldSlot = slot;
                touchId = touch.touchId;
                inside = true;
                mouseHeld = false;
                break;
            }
        }

        private void HandleMouseObservation()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 point = layout.Point(mouse.position.ReadValue());

            if (!mouseHeld && mouse.leftButton.wasPressedThisFrame)
            {
                int slot = layout.Hit(point);
                if (slot >= 0 && slot <= 3)
                {
                    heldSlot = slot;
                    mouseHeld = true;
                    inside = true;
                }
            }

            if (!mouseHeld) return;
            inside = heldSlot >= 0 && layout.ContainsWithMargin(heldSlot, point);
            if (mouse.leftButton.wasReleasedThisFrame) Clear();
        }

        private void Clear()
        {
            heldSlot = -1;
            touchId = -1;
            mouseHeld = false;
            inside = false;
        }

        private void OnGUI()
        {
            if (heldSlot < 0 || !inside || !CanPreview()) return;
            if (Event.current.type != EventType.Repaint) return;
            if (view == null) view = Camera.main;
            if (view == null) return;

            layout = new CombatHudLayout(Screen.safeArea);
            if (layout.Safe.width <= 0f || layout.Safe.height <= 0f) return;

            Matrix4x4 before = GUI.matrix;
            Color beforeColor = GUI.color;
            try
            {
                GUI.matrix = layout.Matrix;
                DrawAbilityPreview(heldSlot);
            }
            finally
            {
                GUI.matrix = before;
                GUI.color = beforeColor;
            }
        }

        private void DrawAbilityPreview(int slot)
        {
            AbilitySpec spec = abilities.SpecFor(slot);
            Vector3 origin = transform.position; origin.y = .18f;
            Color core = PreviewColor(slot);
            Color glow = new Color(.60f, .82f, 1f, .24f);
            // The analytic world-space disc supplies the continuous boundary and subtle fill.
            // Avoid text, ticks and a second GUI ring on top of the reference-style silhouette.
            if (UsesMovementDirection(slot))
                DrawDashPreview(origin, abilities.PreviewDirection(slot), spec.Range, core, glow);
            else if (spec.Range <= .01f && spec.Radius <= .01f)
                DrawWorldTag(origin + FlatForward() * .76f, "SELF", core);
        }

        private Color PreviewColor(int slot)
        {
            // Shared pale blue makes held attack previews consistent across the hero roster.
            return new Color(.40f, .70f, 1f, .92f);
        }

        private bool UsesMovementDirection(int slot)
        {
            HeroId hero = identity != null ? identity.Hero : HeroId.Veyr;
            return (hero == HeroId.Veyr && slot == 2) ||
                   (hero == HeroId.Nyra && slot == 1) ||
                   (hero == HeroId.Elyra && slot == 2) ||
                   (hero == HeroId.Traveler && slot == 1);
        }

        private Vector3 FlatForward()
        {
            Vector3 direction = transform.forward; direction.y = 0f;
            if (direction.sqrMagnitude < .001f) direction = Vector3.forward;
            return direction.normalized;
        }

        private void DrawDashPreview(Vector3 origin, Vector3 direction, float range, Color core, Color glow)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < .001f) direction = FlatForward();
            direction.Normalize();
            Vector3 end = origin + direction * Mathf.Max(.01f, range);
            Vector3 side = Vector3.Cross(Vector3.up, direction);

            // A restrained directional cue inside the range disc for movement abilities.
            DrawProjectedLine(origin, end, glow, 4f);
            DrawProjectedLine(origin, end, core, 1.5f);

            float arrow = Mathf.Clamp(range * .13f, .48f, .80f);
            Vector3 left = end - direction * arrow + side * arrow * .62f;
            Vector3 right = end - direction * arrow - side * arrow * .62f;
            DrawProjectedLine(end, left, glow, 4f);
            DrawProjectedLine(end, right, glow, 4f);
            DrawProjectedLine(end, left, core, 1.5f);
            DrawProjectedLine(end, right, core, 1.5f);
        }

        private void DrawWorldTag(Vector3 world, string text, Color color)
        {
            Vector3 screen = view.WorldToScreenPoint(world);
            if (screen.z <= 0f) return;
            Vector2 point = layout.Point(new Vector2(screen.x, screen.y));
            EnsureTagStyles();
            Rect rect = new Rect(point.x - 68f, point.y - 13f, 136f, 26f);
            Rect shadowRect = new Rect(rect.x + 1.5f, rect.y + 1.5f, rect.width, rect.height);
            tagShadowStyle.normal.textColor = new Color(0f, 0f, 0f, .95f);
            tagStyle.normal.textColor = new Color(color.r, color.g, color.b, 1f);
            GUI.Label(shadowRect, text, tagShadowStyle);
            GUI.Label(rect, text, tagStyle);
        }

        private void EnsureTagStyles()
        {
            if (tagStyle != null) return;
            tagStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Clip
            };
            tagShadowStyle = new GUIStyle(tagStyle);
        }

        private void DrawProjectedLine(Vector3 from, Vector3 to, Color color, float width)
        {
            Vector3 a = view.WorldToScreenPoint(from);
            Vector3 b = view.WorldToScreenPoint(to);
            if (a.z <= 0f || b.z <= 0f) return;
            CombatGlyphs.Line(layout.Point(new Vector2(a.x, a.y)), layout.Point(new Vector2(b.x, b.y)), width, color);
        }
    }
}
