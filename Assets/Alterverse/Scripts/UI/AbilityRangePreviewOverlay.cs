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

        private void Awake()
        {
            abilities = GetComponent<PlayerHeroAbilities>();
            identity = GetComponent<HeroIdentity>();
            health = GetComponent<Health>();
            targeting = GetComponent<SoftTargeting>();
            accent = identity != null ? StylizedArtDirection.HeroAccent(identity.Hero) : FrontEndTheme.Accent;
        }

        private void OnDisable() => Clear();

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
                // Clean, Hot-Zone-like outer ring: authoritative maximum cast/auto-target range.
                DrawWorldCircle(origin, spec.Range, core, glow, pulse, true, 1f);
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

                    DrawProjectedLine(origin, center, new Color(0f, 0f, 0f, .42f), 4f);
                    DrawProjectedLine(origin, center, new Color(core.r, core.g, core.b, .32f), 1.75f);
                    DrawWorldCircle(center, spec.Radius, core, Color.white, pulse, false, exactTarget ? .90f : .72f);
                    DrawWorldTag(center + Vector3.right * spec.Radius,
                        exactTarget ? $"AREA {spec.Radius:0.#}m" : $"AREA SIZE {spec.Radius:0.#}m", core);
                }
                return;
            }

            if (spec.Radius > .01f)
            {
                // Self-centered AoE: the radius itself is the authoritative reach.
                DrawWorldCircle(origin, spec.Radius, core, glow, pulse, true, 1f);
                DrawWorldTag(origin + FlatForward() * spec.Radius, $"AREA {spec.Radius:0.#}m", core);
                return;
            }

            // Pure self buffs have no gameplay range. Never invent a metre value.
            DrawWorldCircle(origin, .72f, core, glow, pulse, false, .90f);
            DrawWorldTag(origin + FlatForward() * .76f, "SELF", core);
        }

        private Color PreviewColor(int slot)
        {
            HeroId hero = identity != null ? identity.Hero : HeroId.Veyr;
            // Support/protection ranges read as friendly at a glance; everything else keeps hero identity.
            if (hero == HeroId.Mira && slot > 0) return new Color(.42f, .96f, .68f, .98f);
            if ((hero == HeroId.Kaelon || hero == HeroId.Rook) && slot == 1)
                return Color.Lerp(accent, new Color(.46f, .84f, 1f), .55f);
            return new Color(accent.r, accent.g, accent.b, .98f);
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
            DrawWorldCircle(end, .34f + pulse * .05f, core, glow, pulse, false, 1f);
            DrawWorldTag(end + side * .46f, $"MOVE {range:0.#}m", core);
        }

        private void DrawWorldCircle(Vector3 center, float radius, Color core, Color glow, float pulse, bool breathe, float alpha)
        {
            if (radius <= .01f) return;

            // Match the Hot Zone's clean circular language, but keep the held-attack preview lighter.
            // A fixed radius prevents the edge from wobbling while aiming; 96 segments keep it smooth
            // even on large phone screens without introducing a texture or an extra world-space object.
            const int Segments = 96;
            float displayedRadius = radius;
            float opacityPulse = breathe ? .94f + pulse * .06f : 1f;
            float opacity = Mathf.Clamp01(alpha) * opacityPulse;
            Color shadow = new Color(0f, 0f, 0f, .26f * opacity);
            Color soft = new Color(glow.r, glow.g, glow.b, .10f * opacity);
            Color line = new Color(core.r, core.g, core.b, .58f * opacity);

            for (int i = 0; i < Segments; i++)
            {
                float a = i * Mathf.PI * 2f / Segments;
                float b = (i + 1) * Mathf.PI * 2f / Segments;
                Vector3 p = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * displayedRadius;
                Vector3 q = center + new Vector3(Mathf.Cos(b), 0f, Mathf.Sin(b)) * displayedRadius;
                DrawProjectedLine(p, q, shadow, 5f);
                DrawProjectedLine(p, q, soft, 3.5f);
                DrawProjectedLine(p, q, line, 2.25f);
            }
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
