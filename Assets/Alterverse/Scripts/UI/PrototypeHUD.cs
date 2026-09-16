using Alterverse.Characters;
using Alterverse.Combat;
using UnityEngine;

namespace Alterverse.UI
{
    public sealed class PrototypeHUD : MonoBehaviour
    {
        private Health playerHealth;
        private VeyrAbilities abilities;
        private BasicAttack basicAttack;
        private HeroMotor motor;
        private Health enemyHealth;

        private GUIStyle titleStyle;
        private GUIStyle textStyle;
        private GUIStyle centerStyle;
        private GUIStyle buttonStyle;

        private int joystickFinger = -1;
        private Vector2 joystickCenter;
        private Vector2 joystickKnob;
        private const float JoystickRadius = 72f;

        public void Initialize(Health player, VeyrAbilities heroAbilities, Health enemy)
        {
            playerHealth = player;
            abilities = heroAbilities;
            enemyHealth = enemy;
            if (player != null)
            {
                basicAttack = player.GetComponent<BasicAttack>();
                motor = player.GetComponent<HeroMotor>();
            }
        }

        private void EnsureStyles()
        {
            titleStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            textStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
            centerStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            buttonStyle ??= new GUIStyle(GUI.skin.button) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        }

        private void Update()
        {
            HandleTouchJoystick();
        }

        private void HandleTouchJoystick()
        {
            if (motor == null || Input.touchCount == 0)
            {
                if (joystickFinger >= 0 && motor != null) motor.SetMoveInput(Vector2.zero);
                joystickFinger = -1;
                return;
            }

            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                Vector2 guiPos = new Vector2(touch.position.x, Screen.height - touch.position.y);

                if (touch.phase == TouchPhase.Began && joystickFinger < 0 && touch.position.x < Screen.width * 0.45f)
                {
                    joystickFinger = touch.fingerId;
                    joystickCenter = guiPos;
                    joystickKnob = guiPos;
                }

                if (touch.fingerId != joystickFinger) continue;

                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    joystickFinger = -1;
                    motor.SetMoveInput(Vector2.zero);
                    return;
                }

                Vector2 delta = guiPos - joystickCenter;
                delta = Vector2.ClampMagnitude(delta, JoystickRadius);
                joystickKnob = joystickCenter + delta;
                motor.SetMoveInput(new Vector2(delta.x, -delta.y) / JoystickRadius);
            }
        }

        private void OnGUI()
        {
            EnsureStyles();
            float scale = Mathf.Clamp(Screen.height / 900f, 0.72f, 1.25f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float sw = Screen.width / scale;
            float sh = Screen.height / scale;

            GUI.Box(new Rect(16, 16, 335, 105), GUIContent.none);
            GUI.Label(new Rect(28, 24, 300, 30), "ALTERVERSE // VEYR", titleStyle);
            if (playerHealth != null)
            {
                GUI.Label(new Rect(28, 56, 280, 24), $"HP {playerHealth.Current:0} / {playerHealth.Max:0}", textStyle);
                DrawBar(new Rect(28, 84, 295, 16), playerHealth.Normalized);
            }

            if (enemyHealth != null && !enemyHealth.IsDead)
            {
                float width = 320f;
                float x = (sw - width) * 0.5f;
                GUI.Box(new Rect(x, 18, width, 58), GUIContent.none);
                GUI.Label(new Rect(x + 10, 23, width - 20, 24), "DIMENSION ZERO HUNTER", centerStyle);
                DrawBar(new Rect(x + 18, 51, width - 36, 13), enemyHealth.Normalized);
            }

            DrawCombatButtons(sw, sh);
            DrawJoystick(sh);
        }

        private void DrawCombatButtons(float sw, float sh)
        {
            float attackSize = 92f;
            float attackX = sw - 120f;
            float attackY = sh - 125f;
            if (GUI.Button(new Rect(attackX, attackY, attackSize, attackSize), "ATTACK", buttonStyle) && basicAttack != null)
                basicAttack.TryAttack();

            DrawAbilityButton(new Rect(sw - 220f, sh - 110f, 76f, 76f), "Q\nSHADOW", abilities != null ? abilities.QRemaining : 0f, () => abilities?.ShadowStrike());
            DrawAbilityButton(new Rect(sw - 195f, sh - 200f, 70f, 70f), "E\nSOUL", abilities != null ? abilities.ERemaining : 0f, () => abilities?.SoulRend());
            DrawAbilityButton(new Rect(sw - 112f, sh - 220f, 70f, 70f), "R\nSTEP", abilities != null ? abilities.RRemaining : 0f, () => abilities?.NullStep());
            DrawAbilityButton(new Rect(sw - 292f, sh - 188f, 72f, 72f), "F\nULT", abilities != null ? abilities.FRemaining : 0f, () => abilities?.EndOfFuture());
        }

        private void DrawAbilityButton(Rect rect, string label, float cooldown, System.Action action)
        {
            string text = cooldown <= 0.05f ? label : $"{label}\n{cooldown:0.0}";
            bool old = GUI.enabled;
            GUI.enabled = cooldown <= 0.05f;
            if (GUI.Button(rect, text, buttonStyle)) action?.Invoke();
            GUI.enabled = old;
        }

        private void DrawJoystick(float sh)
        {
            Vector2 center = joystickFinger >= 0 ? joystickCenter / GUI.matrix.m00 : new Vector2(115f, sh - 115f);
            Vector2 knob = joystickFinger >= 0 ? joystickKnob / GUI.matrix.m00 : center;
            GUI.Box(new Rect(center.x - 72f, center.y - 72f, 144f, 144f), GUIContent.none);
            GUI.Box(new Rect(knob.x - 30f, knob.y - 30f, 60f, 60f), "MOVE");
        }

        private static void DrawBar(Rect rect, float normalized)
        {
            GUI.Box(rect, GUIContent.none);
            float fill = Mathf.Clamp01(normalized) * (rect.width - 4f);
            GUI.Box(new Rect(rect.x + 2f, rect.y + 2f, fill, rect.height - 4f), GUIContent.none);
        }
    }
}
