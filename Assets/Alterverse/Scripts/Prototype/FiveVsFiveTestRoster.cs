using UnityEngine;
using Alterverse.AI;
using Alterverse.Combat;

namespace Alterverse.Prototype
{
    public sealed class FiveVsFiveTestRoster:MonoBehaviour
    {
        private static readonly float[] Lanes={16f,0f,-16f,16f};
        private void Start(){GameObject player=GameObject.Find("AV_Player");if(player==null||GameObject.Find("AV_AllyBot_1")!=null)return;TeamMember playerTeam=player.GetComponent<TeamMember>()??player.AddComponent<TeamMember>();playerTeam.Initialize(0);for(int i=0;i<4;i++)CreateBot($"AV_AllyBot_{i+1}",0,Lanes[i],new Vector3(-31f,1f,Lanes[i]),i);for(int i=0;i<5;i++){float lane=i<4?Lanes[i]:-16f;CreateBot($"AV_EnemyBot_{i+1}",1,lane,new Vector3(31f,1f,lane),i+1);}}
        private static void CreateBot(string botName,int team,float laneZ,Vector3 spawn,int styleIndex){GameObject bot=GameObject.CreatePrimitive(PrimitiveType.Capsule);bot.name=botName;bot.transform.position=spawn;Renderer renderer=bot.GetComponent<Renderer>();if(renderer!=null)renderer.material.color=team==0?new Color(.30f,.12f,.48f):new Color(.08f,.58f,.64f);Health health=bot.AddComponent<Health>();health.Configure(125f);TeamMember member=bot.AddComponent<TeamMember>();member.Initialize(team);BotPersonality personality=bot.AddComponent<BotPersonality>();BotPersonality.Style style=(BotPersonality.Style)(styleIndex%5);personality.Configure(style);bot.AddComponent<BotTargetSensor>();MobaBotBrain brain=bot.AddComponent<MobaBotBrain>();brain.Initialize(team,laneZ,personality.Aggression,personality.Caution);brain.RefreshPersonality();BotLanePlan lanePlan=bot.AddComponent<BotLanePlan>();lanePlan.Initialize(team,laneZ);bot.AddComponent<MobaBotMover>();bot.AddComponent<MobaBotCombat>();bot.AddComponent<MobaBotRespawn>();bot.AddComponent<CombatFeedback>();}
    }
}
