using System;
using UnityEngine;

namespace Alterverse.Match
{
    /// <summary>
    /// One local section of the Dimensions Front. Each lane can move independently,
    /// allowing simultaneous fights to reshape different parts of the battlefield.
    /// </summary>
    public sealed class DimensionsFrontLane : MonoBehaviour
    {
        [SerializeField, Range(0f, 100f)] private float lumenControl = 50f;
        [SerializeField] private float laneZ;

        public float ZeroControl => 100f - lumenControl;
        public float LumenControl => lumenControl;
        public float LaneZ => laneZ;

        // Zero begins on negative X and Lumen on positive X. More Lumen control
        // therefore pushes the collision boundary left into Zero territory.
        public float FrontWorldX => Mathf.Lerp(22f, -22f, lumenControl / 100f);

        public event Action<DimensionsFrontLane> Changed;

        public void Initialize(float assignedLaneZ)
        {
            laneZ = Mathf.Clamp(assignedLaneZ, -12f, 12f);
            lumenControl = 50f;
            Changed?.Invoke(this);
        }

        public void Push(int scoringTeam, float influence)
        {
            if (influence <= 0f || (scoringTeam != 0 && scoringTeam != 1)) return;
            lumenControl = Mathf.Clamp(lumenControl + (scoringTeam == 1 ? influence : -influence), 0f, 100f);
            Changed?.Invoke(this);
        }
    }
}
