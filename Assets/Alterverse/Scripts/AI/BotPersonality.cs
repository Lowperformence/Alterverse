using UnityEngine;

namespace Alterverse.AI
{
    /// <summary>
    /// Small data component used to make offline bots feel less identical.
    /// It contains no movement or combat logic and is safe to attach later.
    /// </summary>
    public sealed class BotPersonality : MonoBehaviour
    {
        public enum Style { Balanced, Aggressive, Defensive, Supportive, ObjectiveFocused, Roamer }

        [SerializeField] private Style style = Style.Balanced;
        [SerializeField, Range(0.1f, 1f)] private float aggression = 0.55f;
        [SerializeField, Range(0.1f, 1f)] private float caution = 0.55f;
        [SerializeField, Range(0.1f, 1f)] private float teamFocus = 0.60f;
        [SerializeField, Range(0.1f, 1f)] private float objectiveFocus = 0.60f;
        [SerializeField, Range(0.15f, 0.8f)] private float reactionDelay = 0.35f;

        public Style PlayStyle => style;
        public float Aggression => aggression;
        public float Caution => caution;
        public float TeamFocus => teamFocus;
        public float ObjectiveFocus => objectiveFocus;
        public float ReactionDelay => reactionDelay;

        public void Configure(Style newStyle)
        {
            style = newStyle;
            switch (style)
            {
                case Style.Aggressive:
                    Set(0.78f, 0.38f, 0.55f, 0.48f, 0.27f);
                    break;
                case Style.Defensive:
                    Set(0.38f, 0.80f, 0.62f, 0.62f, 0.40f);
                    break;
                case Style.Supportive:
                    Set(0.46f, 0.66f, 0.86f, 0.58f, 0.36f);
                    break;
                case Style.ObjectiveFocused:
                    Set(0.50f, 0.60f, 0.66f, 0.88f, 0.34f);
                    break;
                case Style.Roamer:
                    Set(0.64f, 0.50f, 0.52f, 0.74f, 0.31f);
                    break;
                default:
                    Set(0.55f, 0.55f, 0.60f, 0.60f, 0.35f);
                    break;
            }
        }

        private void Set(float a, float c, float team, float objective, float reaction)
        {
            aggression = a;
            caution = c;
            teamFocus = team;
            objectiveFocus = objective;
            reactionDelay = reaction;
        }
    }
}
