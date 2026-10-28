using UnityEngine;using Alterverse.AI;using Alterverse.Characters;using Alterverse.Combat;using Alterverse.Visuals;using Alterverse.Multiplayer;using CombatFeedbackComponent=Alterverse.Combat.CombatFeedback;
namespace Alterverse.Prototype
{
// Legacy component name retained for serialized scene references; roster size comes from MatchSession.
public sealed class FiveVsFiveTestRoster:MonoBehaviour
{
private void Start(){GameObject player=GameObject.Find("AV_Player");if(player==null||GameObject.Find("AV_OfflineBotRoster")!=null)return;var marker=new GameObject("AV_OfflineBotRoster");var matchWorld=GameObject.Find("DF_MatchWorld");if(matchWorld!=null)marker.transform.SetParent(matchWorld.transform,true);string draw=System.Guid.NewGuid().ToString("N");HeroId selected=PlayerHeroSelection.Selected;TeamMember playerTeam=player.GetComponent<TeamMember>()??player.AddComponent<TeamMember>();playerTeam.Initialize(0);HeroIdentity identity=player.GetComponent<HeroIdentity>()??player.AddComponent<HeroIdentity>();identity.Configure(selected);HeroCombatProfile profile=player.GetComponent<HeroCombatProfile>()??player.AddComponent<HeroCombatProfile>();profile.Configure(selected);HeroAbilityKit kit=player.GetComponent<HeroAbilityKit>()??player.AddComponent<HeroAbilityKit>();kit.Configure(selected);Health health=player.GetComponent<Health>();if(health!=null)health.Configure(profile.Health);if(player.GetComponent<StylizedHeroVisual>()==null)player.AddComponent<StylizedHeroVisual>();HeroId[] allies=BuildAllies(selected,draw);for(int i=0;i<allies.Length;i++)CreateHero(allies[i],0,SafeSpawn(0,i+1),i);var enemyOrder=BotHeroPool.Shuffled(draw,1);var enemyUsed=new System.Collections.Generic.HashSet<HeroId>();int cursor=0;for(int i=0;i<MatchSession.TeamSize;i++){var hero=BotHeroPool.Next(enemyOrder,enemyUsed,ref cursor);enemyUsed.Add(hero);CreateHero(hero,1,SafeSpawn(1,i),i+1);}}
private static HeroId[] BuildAllies(HeroId selected,string matchId)
{
var result=new HeroId[MatchSession.TeamSize-1];var used=new System.Collections.Generic.HashSet<HeroId>{selected};var order=BotHeroPool.Shuffled(matchId,0);int cursor=0;
for(int i=0;i<result.Length;i++){result[i]=BotHeroPool.Next(order,used,ref cursor);used.Add(result[i]);}return result;
}
private static Vector3 SafeSpawn(int team,int index)
{
    return ArenaLayout.HeroSpawn(team,index,MatchSession.TeamSize);
}
private static void CreateHero(HeroId hero,int team,Vector3 spawn,int styleIndex){GameObject bot=new GameObject();bot.name=$"AV_{(team==0?"Zero":"Lumen")}_{hero}";bot.transform.position=spawn;CapsuleCollider hitbox=bot.AddComponent<CapsuleCollider>();hitbox.height=HeroIdentity.HeroCollisionHeight;hitbox.radius=HeroIdentity.HeroCollisionRadius;GameObject world=GameObject.Find("AV_OfflineBotRoster")??GameObject.Find("DF_MatchWorld");if(world!=null)bot.transform.SetParent(world.transform,true);/* Hero rendering is owned entirely by StylizedHeroVisual. */HeroIdentity identity=bot.AddComponent<HeroIdentity>();identity.Configure(hero);HeroCombatProfile profile=bot.AddComponent<HeroCombatProfile>();profile.Configure(hero);HeroAbilityKit kit=bot.AddComponent<HeroAbilityKit>();kit.Configure(hero);Health health=bot.AddComponent<Health>();health.Configure(profile.Health);TeamMember member=bot.AddComponent<TeamMember>();member.Initialize(team);bot.AddComponent<StylizedHeroVisual>().Build();BotPersonality personality=bot.AddComponent<BotPersonality>();personality.Configure((BotPersonality.Style)(styleIndex%6));bot.AddComponent<BotTargetSensor>();MobaBotBrain brain=bot.AddComponent<MobaBotBrain>();brain.Initialize(team,0f,personality.Aggression,personality.Caution);brain.RefreshPersonality();BotLanePlan roamPlan=bot.AddComponent<BotLanePlan>();roamPlan.Initialize(team,0f);bot.AddComponent<MobaBotMover>();bot.AddComponent<MobaBotCombat>();bot.AddComponent<HeroBotAbilities>();bot.AddComponent<MobaBotRespawn>();bot.AddComponent<CombatFeedbackComponent>();}
}
}

