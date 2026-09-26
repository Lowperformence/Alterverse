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
        private Color accent;
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
            Vector3 center = current != null ? current.transform.position :
                origin + FlatForward() * Mathf.Clamp(spec.Range * .64f, Mathf.Max(.9f, spec.Radius + .25f), spec.Range);
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
            accent = identity != null ? StylizedArtDirection.HeroAccent(identity.Hero) : FrontEndTheme.Accent;
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
            Color glow = Color.Lerp(core, Color.white, .42f); glow.a = .92f;
            float pulse = .5f + .5f * Mathf.Sin(Time.unscaledTime * 6f);

            if (UsesMovementDirection(slot))
            {
                DrawDashPreview(origin, abilities.PreviewDirection(slot), spec.Range, core, glow, pulse);
                return;
            }

            if (spec.Range > .01f)
            {
                // Outer ring: authoritative maximum cast/auto-target range.

                DrawWorldTag(origin + FlatForward() * spec.Range, $"RANGE {spec.Range:0.#}m", core);

                if (spec.Radius > .01f)
                {
                    // Targeted AoE needs both values to be understandable. If SoftTargeting already has a
                    // valid sticky target, show the area on that exact target without mutating targeting.
                    // Otherwise show an explicitly labelled AREA SIZE sample inside the cast range.
                    targeting ??= GetComponent<SoftTargeting>();
                    Health current = targeting != null ? targeting.Current : null;
                    Vector3 center;
                    bool exactTarget = current != null;
                    if (exactTarget)
                    {
                        center = current.transform.position;
                        center.y = .18f;
                    }
                    else
                    {
                        float offset = Mathf.Clamp(spec.Range * .64f, Mathf.Max(.9f, spec.Radius + .25f), spec.Range);
                        center = origin + FlatForward() * offset;
                    }

                    DrawProjectedLine(origin, center, new Color(0f, 0f, 0f, .62f), 5f);
                    DrawProjectedLine(origin, center, new Color(core.r, core.g, core.b, .46f), 2f);

                    DrawWorldTag(center + Vector3.right * spec.Radius,
                        exactTarget ? $"AREA {spec.Radius:0.#}m" : $"AREA SIZE {spec.Radius:0.#}m", core);
                }
                return;
            }

            if (spec.Radius > .01f)
            {
                // Self-centered AoE: the radius itself is the authoritative reach.

                DrawWorldTag(origin + FlatForward() * spec.Radius, $"AREA {spec.Radius:0.#}m", core);
                return;
            }

            // Pure self buffs have no gameplay range. Never invent a metre value.

            DrawWorldTag(origin + FlatForward() * .76f, "SELF", core);
        }

        private Color PreviewColor(int slot)
        {
            HeroId hero = identity != null ? identity.Hero : HeroId.Veyr;
            // Support/protection ranges read as friendly at a glance; everything else keeps hero identity.
            if (hero == HeroId.Mira && slot > 0) return new Color(.42f, .96f, .68f, .98f);
            if ((hero == HeroId.Kaelon || hero == HeroId.Rook) && slot == 1)
                return Color.Lerp(accent, new Color(.46f, .84f, 1f), .55f);
            return new Color(1f, .08f, .10f, .98f);
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

        private void DrawDashPreview(Vector3 origin, Vector3 direction, float range, Color core, Color glow, float pulse)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < .001f) direction = FlatForward();
            direction.Normalize();
            Vector3 end = origin + direction * Mathf.Max(.01f, range);
            Vector3 side = Vector3.Cross(Vector3.up, direction);

            // Black underlay + bright glow + core keeps the direction legible over every planet surface.
            DrawProjectedLine(origin, end, new Color(0f, 0f, 0f, .82f), 10f);
            DrawProjectedLine(origin, end, glow, 6f + pulse);
            DrawProjectedLine(origin, end, core, 3.25f);

            float arrow = Mathf.Clamp(range * .13f, .48f, .80f);
            Vector3 left = end - direction * arrow + side * arrow * .62f;
            Vector3 right = end - direction * arrow - side * arrow * .62f;
            DrawProjectedLine(end, left, new Color(0f, 0f, 0f, .82f), 10f);
            DrawProjectedLine(end, right, new Color(0f, 0f, 0f, .82f), 10f);
            DrawProjectedLine(end, left, glow, 5.5f);
            DrawProjectedLine(end, right, glow, 5.5f);

            DrawWorldTag(end + side * .46f, $"MOVE {range:0.#}m", core);
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
