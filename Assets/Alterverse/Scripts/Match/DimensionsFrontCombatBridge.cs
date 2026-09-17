using System.Collections.Generic;
using Alterverse.Combat;
using UnityEngine;

namespace Alterverse.Match
{
    public sealed class DimensionsFrontCombatBridge : MonoBehaviour
    {
        [SerializeField,Min(.1f)]private float refreshInterval=.5f;
        private readonly HashSet<Health> tracked=new();private DimensionsFront front;private DimensionsFrontVisual visual;private float nextRefresh;
        private void Start(){front=Object.FindAnyObjectByType<DimensionsFront>();visual=Object.FindAnyObjectByType<DimensionsFrontVisual>();RefreshHeroes();}
        private void Update(){if(Time.unscaledTime<nextRefresh)return;nextRefresh=Time.unscaledTime+refreshInterval;if(front==null)front=Object.FindAnyObjectByType<DimensionsFront>();if(visual==null)visual=Object.FindAnyObjectByType<DimensionsFrontVisual>();RefreshHeroes();}
        private void RefreshHeroes(){foreach(TeamMember member in Object.FindObjectsByType<TeamMember>(FindObjectsSortMode.None)){if(member==null)continue;Health health=member.GetComponent<Health>();if(health==null||!tracked.Add(health))continue;health.DiedWithSource+=OnHeroDied;}}
        private void OnHeroDied(Health victim,GameObject source){if(front==null||victim==null||source==null)return;TeamMember victimTeam=victim.GetComponent<TeamMember>();TeamMember killerTeam=source.GetComponentInParent<TeamMember>();if(victimTeam==null||killerTeam==null||victimTeam.Team==killerTeam.Team)return;float worldZ=victim.transform.position.z;front.RegisterHeroKill(killerTeam.Team,worldZ);if(visual!=null)visual.PulseImpact(worldZ,killerTeam.Team);}
        private void OnDestroy(){foreach(Health health in tracked)if(health!=null)health.DiedWithSource-=OnHeroDied;tracked.Clear();}
    }
}
