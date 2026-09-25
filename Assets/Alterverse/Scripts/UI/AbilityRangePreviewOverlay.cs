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
    /// Read-only mobile range preview. It observes the same ability hit areas as PrototypeHUD but
    /// never consumes input and never casts anything. Holding Q/W/E/Ultimate therefore reveals the
    /// authoritative range/radius while PrototypeHUD remains the sole owner of combat input.
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
            // Touch owns the mobile gesture. Mouse is only an editor/desktop preview path.
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
            try
            {
                GUI.matrix = layout.Matrix;
                DrawAbilityPreview(heldSlot);
            }
            finally { GUI.matrix = before; }
        }

        private void DrawAbilityPreview(int slot)
        {
            AbilitySpec spec = abilities.SpecFor(slot);
            Vector3 origin = transform.position;
            origin.y = .18f;
            Color core = new Color(accent.r, accent.g, accent.b, .98f);
            Color glow = Color.Lerp(core, Color.white, .42f); glow.a = .92f;
            float pulse = .5f + .5f * Mathf.Sin(Time.unscaledTime * 6f);

            if (UsesMovementDirection(slot))
            {
                DrawDashPreview(origin, abilities.PreviewDirection(slot), spec.Range, core, glow, pulse);
                return;
            }

            if (spec.Range > .01f)
            {
                DrawWorldCircle(origin, spec.Range, core, glow, pulse, true);
                DrawRangeTicks(origin, spec.Range, core);

                // Targeted AoE abilities show both pieces of information at once: the outer circle is
                // maximum cast range, while the second circle is the real damage/control radius.
                if (spec.Radius > .01f && identity != null && identity.Hero != HeroId.Mira)
                {
                    targeting ??= GetComponent<SoftTargeting>();
                    Health target = targeting != null ? targeting.Acquire(spec.Range, transform.forward, true) : null;
                    if (target != null)
                    {
                        Vector3 center = target.transform.position; center.y = .18f;
                        DrawWorldCircle(center, spec.Radius, core, Color.white, pulse, false);
                        DrawProjectedLine(origin, center, new Color(core.r, core.g, core.b, .48f), 2f);
                    }
                }
                return;
            }

            if (spec.Radius > .01f)
            {
                // Self-centered AoE: radius itself is the authoritative reach.
                DrawWorldCircle(origin, spec.Radius, core, glow, pulse, true);
                DrawRangeTicks(origin, spec.Radius, core);
                return;
            }

            // Pure self buffs have no gameplay range. Show a compact self marker instead of inventing
            // a false metre value, so holding the button still gives immediate visual confirmation.
            DrawWorldCircle(origin, .72f, core, glow, pulse, false);
        }

        private bool UsesMovementDirection(int slot)
        {
            HeroId hero = identity != null ? identity.Hero : HeroId.Veyr;
            return (hero == HeroId.Veyr && slot == 2) ||
                   (hero == HeroId.Nyra && slot == 1) ||
                   (hero == HeroId.Elyra && slot == 2) ||
                   (hero == HeroId.Traveler && slot == 1);
        }

        private void DrawDashPreview(Vector3 origin, Vector3 direction, float range, Color core, Color glow, float pulse)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < .001f) direction = transform.forward;
            direction.Normalize();
            Vector3 end = origin + direction * Mathf.Max(.01f, range);
            Vector3 side = Vector3.Cross(Vector3.up, direction);

            DrawProjectedLine(origin, end, new Color(0f, 0f, 0f, .78f), 9f);
            DrawProjectedLine(origin, end, glow, 5f + pulse);
            DrawProjectedLine(origin, end, core, 3f);

            float arrow = Mathf.Clamp(range * .13f, .48f, .78f);
            Vector3 left = end - direction * arrow + side * arrow * .62f;
            Vector3 right = end - direction * arrow - side * arrow * .62f;
            DrawProjectedLine(end, left, new Color(0f, 0f, 0f, .78f), 9f);
            DrawProjectedLine(end, right, new Color(0f, 0f, 0f, .78f), 9f);
            DrawProjectedLine(end, left, glow, 5f);
            DrawProjectedLine(end, right, glow, 5f);
            DrawWorldCircle(end, .34f + pulse * .05f, core, glow, pulse, false);
        }

        private void DrawWorldCircle(Vector3 center, float radius, Color core, Color glow, float pulse, bool breathe)
        {
            if (radius <= .01f) return;
            const int Segments = 64;
            float displayedRadius = breathe ? radius * (1f - .006f + pulse * .012f) : radius;
            Color shadow = new Color(0f, 0f, 0f, .72f);
            Color soft = new Color(glow.r, glow.g, glow.b, .34f + pulse * .12f);

            for (int i = 0; i < Segments; i++)
            {
                float a = i * Mathf.PI * 2f / Segments;
                float b = (i + 1) * Mathf.PI * 2f / Segments;
                Vector3 p = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * displayedRadius;
                Vector3 q = center + new Vector3(Mathf.Cos(b), 0f, Mathf.Sin(b)) * displayedRadius;
                DrawProjectedLine(p, q, shadow, 8f);
                DrawProjectedLine(p, q, soft, 6f);
                DrawProjectedLine(p, q, core, 3.25f);
            }
        }

        private void DrawRangeTicks(Vector3 center, float radius, Color color)
        {
            if (radius < .5f) return;
            float tick = Mathf.Clamp(radius * .035f, .12f, .28f);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * .25f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 from = center + dir * Mathf.Max(.05f, radius - tick);
                Vector3 to = center + dir * (radius + tick);
                DrawProjectedLine(from, to, new Color(0f, 0f, 0f, .80f), 8f);
                DrawProjectedLine(from, to, color, 4f);
            }
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
