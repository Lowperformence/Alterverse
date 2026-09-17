using UnityEngine;
namespace Alterverse.Characters
{
public sealed class HeroCombatProfile:MonoBehaviour
{
public HeroId Hero{get;private set;}public float Health{get;private set;}public float Attack{get;private set;}public float AttackRange{get;private set;}public float MoveSpeed{get;private set;}public float AbilityPower{get;private set;}public float Control{get;private set;}public float Sustain{get;private set;}
public void Configure(HeroId id){Hero=id;Health=HeroIdentity.BaseHealth(id);Attack=HeroIdentity.AttackDamage(id);switch(id){case HeroId.Veyr:AttackRange=2.3f;MoveSpeed=7.2f;AbilityPower=1.15f;Control=.25f;Sustain=.55f;break;case HeroId.Kaelon:AttackRange=2.1f;MoveSpeed=5.7f;AbilityPower=.72f;Control=1f;Sustain=.82f;break;case HeroId.Nyra:AttackRange=8.5f;MoveSpeed=6.5f;AbilityPower=1.05f;Control=.38f;Sustain=.22f;break;case HeroId.Elyra:AttackRange=7.4f;MoveSpeed=6.2f;AbilityPower=1.25f;Control=.78f;Sustain=.20f;break;case HeroId.Mira:AttackRange=6.7f;MoveSpeed=6.35f;AbilityPower=.68f;Control=.52f;Sustain=1f;break;default:AttackRange=2.5f;MoveSpeed=6.4f;AbilityPower=.88f;Control=.48f;Sustain=.72f;break;}}
}
}
