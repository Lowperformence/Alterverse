using Alterverse.Characters;
using Alterverse.Combat;
using UnityEngine;

namespace Alterverse.UI
{
    public sealed class PrototypeHUD : MonoBehaviour
    {
        private Health playerHealth;
        private VeyrAbilities abilities;
        private Health enemyHealth;

        private GUIStyle titleStyle;
        private GUIStyle textStyle;
        private GUIStyle centerStyle;

        public void Initialize(Health player, VeyrAbilities heroAbilities, Health enemy)
        {
            playerHealth = player;
            abilities = heroAbilities;
            enemyHealth = enemy;
        }

        private void EnsureStyles()
        {
            titleStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft
            };

            textStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold
            };

            centerStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
        }

        private void OnGUI()
        {
            EnsureStyles();
            float scale = Mathf.Clamp(Screen.height / 900f, 0.8f, 1.4f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            GUI.Box(new Rect(18, 18, 410, 170), GUIContent.none);
            GUI.Label(new Rect(34, 30, 370, 34), "ALTERVERSE // VEYR", titleStyle);

            if (playerHealth != null)
            {
                GUI.Label(new Rect(34, 72, 350, 28), $"HP  {playerHealth.Current:0} / {playerHealth.Max:0}", textStyle);
                DrawBar(new Rect(34, 102, 350, 18), playerHealth.Normalized);
            }

            if (abilities != null)
            {
                string q = Cooldown("Q Schattenstich", abilities.QRemaining);
                string e = Cooldown("E Seelenriss", abilities.ERemaining);
                string r = Cooldown("R Nullschritt", abilities.RRemaining);
                string f = Cooldown("F Ende der Zukunft", abilities.FRemaining);
                GUI.Label(new Rect(34, 130, 375, 26), q, textStyle);
                GUI.Label(new Rect(34, 154, 380, 26), $"{e}    {r}    {f}", textStyle);
            }

            if (enemyHealth != null && !enemyHealth.IsDead)
            {
                float width = 360f;
                float x = (Screen.width / scale - width) * 0.5f;
                GUI.Box(new Rect(x, 20, width, 65), GUIContent.none);
                GUI.Label(new Rect(x + 12, 28, width - 24, 24), "DIMENSION ZERO HUNTER", centerStyle);
                DrawBar(new Rect(x + 18, 57, width - 36, 14), enemyHealth.Normalized);
            }

            float screenW = Screen.width / scale;
            float screenH = Screen.height / scale;
            GUI.Box(new Rect(screenW - 350, screenH - 112, 330, 92), GUIContent.none);
            GUI.Label(new Rect(screenW - 335, screenH - 100, 300, 24), "WASD  Move   SPACE  Attack", textStyle);
            GUI.Label(new Rect(screenW - 335, screenH - 72, 300, 24), "Q / E / R / F  Abilities", textStyle);
            GUI.Label(new Rect(screenW - 335, screenH - 44, 300, 24), "Defeat the hunter. Survive.", textStyle);
        }

        private static string Cooldown(string label, float remaining)
        {
            return remaining <= 0.05f ? $"{label}: READY" : $"{label}: {remaining:0.0}s";
        }

        private static void DrawBar(Rect rect, float normalized)
        {
            GUI.Box(rect, GUIContent.none);
            float fill = Mathf.Clamp01(normalized) * (rect.width - 4f);
            GUI.Box(new Rect(rect.x + 2f, rect.y + 2f, fill, rect.height - 4f), GUIContent.none);
        }
    }
}
