using UnityEngine;using Alterverse.AI;using Alterverse.Characters;using Alterverse.Combat;using Alterverse.Visuals;using CombatFeedbackComponent=Alterverse.Combat.CombatFeedback;
namespace Alterverse.Prototype
{
public sealed class FiveVsFiveTestRoster:MonoBehaviour
{
private static readonly HeroId[] Pool={HeroId.Veyr,HeroId.Kaelon,HeroId.Nyra,HeroId.Elyra,HeroId.Mira,HeroId.Rook};private static readonly HeroId[] LumenHeroes={HeroId.Veyr,HeroId.Kaelon,HeroId.Nyra,HeroId.Elyra,HeroId.Rook};
private static readonly float[] SpawnRows={8f,-8f,5f,-5f,0f};
private void Start(){GameObject player=GameObject.Find("AV_Player");if(player==null||GameObject.Find("AV_Lumen_Kaelon")!=null)return;HeroId selected=PlayerHeroSelection.Selected;TeamMember playerTeam=player.GetComponent<TeamMember>()??player.AddComponent<TeamMember>();playerTeam.Initialize(0);HeroIdentity identity=player.GetComponent<HeroIdentity>()??player.AddComponent<HeroIdentity>();identity.Configure(selected);HeroCombatProfile profile=player.GetComponent<HeroCombatProfile>()??player.AddComponent<HeroCombatProfile>();profile.Configure(selected);HeroAbilityKit kit=player.GetComponent<HeroAbilityKit>()??player.AddComponent<HeroAbilityKit>();kit.Configure(selected);Health health=player.GetComponent<Health>();if(health!=null)health.Configure(profile.Health);if(player.GetComponent<StylizedHeroVisual>()==null)player.AddComponent<StylizedHeroVisual>();HeroId[] allies=BuildAllies(selected);for(int i=0;i<4;i++)CreateHero(allies[i],0,SafeSpawn(0,i),i);for(int i=0;i<5;i++)CreateHero(LumenHeroes[i],1,SafeSpawn(1,i),i+1);}
private static HeroId[] BuildAllies(HeroId selected){HeroId[] result=new HeroId[4];int index=0;for(int i=0;i<Pool.Length&&index<4;i++){if(Pool[i]==selected)continue;result[index++]=Pool[i];}return result;}
private static Vector3 SafeSpawn(int team,int index)
{
    // Free-roam formation: spread heroes around the base opening rather than assigning lanes.
    // This also keeps the roster clear of the new flank defense bunkers and the healing guards.
    float side=team==0?-1f:1f;
    float x=side*(index<2?30f:index<4?28.8f:30.8f);
    float z=SpawnRows[Mathf.Clamp(index,0,SpawnRows.Length-1)];
    return new Vector3(x,1f,z);
}
private static void CreateHero(HeroId hero,int team,Vector3 spawn,int styleIndex){GameObject bot=new GameObject();bot.name=$"AV_{(team==0?"Zero":"Lumen")}_{hero}";bot.transform.position=spawn;CapsuleCollider hitbox=bot.AddComponent<CapsuleCollider>();hitbox.height=2f;hitbox.radius=.5f;GameObject world=GameObject.Find("DF_MatchWorld");if(world!=null)bot.transform.SetParent(world.transform,true);/* Hero rendering is owned entirely by StylizedHeroVisual. */HeroIdentity identity=bot.AddComponent<HeroIdentity>();identity.Configure(hero);HeroCombatProfile profile=bot.AddComponent<HeroCombatProfile>();profile.Configure(hero);HeroAbilityKit kit=bot.AddComponent<HeroAbilityKit>();kit.Configure(hero);Health health=bot.AddComponent<Health>();health.Configure(profile.Health);TeamMember member=bot.AddComponent<TeamMember>();member.Initialize(team);bot.AddComponent<StylizedHeroVisual>().Build();BotPersonality personality=bot.AddComponent<BotPersonality>();personality.Configure((BotPersonality.Style)(styleIndex%6));bot.AddComponent<BotTargetSensor>();MobaBotBrain brain=bot.AddComponent<MobaBotBrain>();brain.Initialize(team,0f,personality.Aggression,personality.Caution);brain.RefreshPersonality();BotLanePlan roamPlan=bot.AddComponent<BotLanePlan>();roamPlan.Initialize(team,0f);bot.AddComponent<MobaBotMover>();bot.AddComponent<MobaBotCombat>();bot.AddComponent<HeroBotAbilities>();bot.AddComponent<MobaBotRespawn>();bot.AddComponent<CombatFeedbackComponent>();}
}
}
