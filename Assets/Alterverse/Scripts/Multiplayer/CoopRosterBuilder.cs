using Alterverse.AI;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Visuals;
using UnityEngine;
using CombatFeedbackComponent=Alterverse.Combat.CombatFeedback;

namespace Alterverse.Multiplayer
{
    /// <summary>
    /// Builds the exact same ten hero slots on every peer. Only the authoritative NGO server gives AI slots
    /// decision/movement/combat components. Human input remains local to its owning device.
    /// </summary>
    public static class CoopRosterBuilder
    {
        private static readonly float[] SpawnRows={8f,-8f,5f,-5f,0f};

        public static bool Build(GameObject localPlayer,CoopMatchPlan plan)
        {
            if(localPlayer==null||plan?.Session==null||!plan.Session.IsFull)return false;

            int localSlot=FindLocalHumanSlot(plan);
            if(localSlot<0)return false;

            ConfigureHuman(localPlayer,plan.Session.Slots[localSlot],localSlot,true,SafeSpawn(0,localSlot));

            for(int slot=0;slot<MatchSession.MaxPlayers;slot++)
            {
                if(slot==localSlot)continue;
                MatchPlayerSlot data=plan.Session.Slots[slot];
                if(data==null)continue;
                if(data.IsBot)CreateBot(data,slot,CoopAuthority.LocalHasServerAuthority);
                else CreateRemoteHuman(data,slot);
            }
            return true;
        }

        public static int FindLocalHumanSlot(CoopMatchPlan plan)
        {
            if(plan?.Session?.Slots==null)return -1;
            for(int i=0;i<plan.Session.Slots.Length;i++)
            {
                MatchPlayerSlot slot=plan.Session.Slots[i];
                if(slot!=null&&!slot.IsBot&&slot.IsLocal)return i;
            }
            return -1;
        }

        public static Vector3 SafeSpawn(int team,int index)
        {
            float side=team==0?-1f:1f;
            int row=Mathf.Clamp(team==0?index:index-MatchSession.TeamSize,0,SpawnRows.Length-1);
            float x=side*(row<2?30f:row<4?28.8f:30.8f);
            float z=SpawnRows[row];
            return new Vector3(x,1f,z);
        }

        private static void ConfigureHuman(GameObject hero,MatchPlayerSlot data,int slot,bool local,Vector3 spawn)
        {
            hero.name=local?"AV_Player":$"AV_CoopHuman_{slot}_{data.Hero}";
            hero.transform.position=spawn;

            CharacterController controller=hero.GetComponent<CharacterController>();
            if(local&&controller==null)
            {
                controller=hero.AddComponent<CharacterController>();
                controller.height=HeroIdentity.HeroCollisionHeight;controller.radius=HeroIdentity.HeroCollisionRadius;
            }
            if(!local&&controller!=null)Object.Destroy(controller);

            HeroIdentity identity=hero.GetComponent<HeroIdentity>()??hero.AddComponent<HeroIdentity>();
            identity.Configure(data.Hero);
            HeroCombatProfile profile=hero.GetComponent<HeroCombatProfile>()??hero.AddComponent<HeroCombatProfile>();
            profile.Configure(data.Hero);
            HeroAbilityKit kit=hero.GetComponent<HeroAbilityKit>()??hero.AddComponent<HeroAbilityKit>();
            kit.Configure(data.Hero);
            Health health=hero.GetComponent<Health>()??hero.AddComponent<Health>();
            health.Configure(profile.Health);
            TeamMember team=hero.GetComponent<TeamMember>()??hero.AddComponent<TeamMember>();
            team.Initialize(0);

            HeroLocomotion locomotion=hero.GetComponent<HeroLocomotion>()??hero.AddComponent<HeroLocomotion>();
            CoopHeroSlot netSlot=hero.GetComponent<CoopHeroSlot>()??hero.AddComponent<CoopHeroSlot>();
            netSlot.Configure(slot,data);
            if(hero.GetComponent<CoopRespawnReplica>()==null)hero.AddComponent<CoopRespawnReplica>();

            StylizedHeroVisual visual=hero.GetComponent<StylizedHeroVisual>();
            if(visual==null){visual=hero.AddComponent<StylizedHeroVisual>();visual.Build();}

            // Remote human actors need the same combat execution stack on the authoritative server so
            // network commands can be validated/executed there, but they never read server-side input.
            if(hero.GetComponent<BasicAttack>()==null)hero.AddComponent<BasicAttack>();
            if(hero.GetComponent<PlayerHeroAbilities>()==null)hero.AddComponent<PlayerHeroAbilities>();
            if(hero.GetComponent<CombatFeedbackComponent>()==null)hero.AddComponent<CombatFeedbackComponent>();

            if(local)
            {
                if(hero.GetComponent<HeroMotor>()==null)hero.AddComponent<HeroMotor>();
            }
            else
            {
                HeroMotor motor=hero.GetComponent<HeroMotor>();
                if(motor!=null)Object.Destroy(motor);
            }

            if(CoopAuthority.LocalHasServerAuthority)
            {
                HeroRespawn respawn=hero.GetComponent<HeroRespawn>()??hero.AddComponent<HeroRespawn>();
                if(!local&&hero.GetComponent<CoopRemoteAutoRespawn>()==null)hero.AddComponent<CoopRemoteAutoRespawn>();
                _=respawn;
            }
            else
            {
                HeroRespawn respawn=hero.GetComponent<HeroRespawn>();
                if(respawn!=null)Object.Destroy(respawn);
            }

            _=locomotion;
        }

        private static GameObject CreateRemoteHuman(MatchPlayerSlot data,int slot)
        {
            GameObject hero=new($"AV_CoopHuman_{slot}_{data.Hero}");
            hero.transform.position=SafeSpawn(0,slot);
            CapsuleCollider hitbox=hero.AddComponent<CapsuleCollider>();
            hitbox.height=HeroIdentity.HeroCollisionHeight;hitbox.radius=HeroIdentity.HeroCollisionRadius;
            ParentToWorld(hero);
            ConfigureHuman(hero,data,slot,false,hero.transform.position);
            return hero;
        }

        private static GameObject CreateBot(MatchPlayerSlot data,int slot,bool authoritative)
        {
            GameObject bot=new($"AV_{(data.Team==0?"Zero":"Lumen")}_{data.Hero}_Slot{slot}");
            bot.transform.position=SafeSpawn(data.Team,slot);
            CapsuleCollider hitbox=bot.AddComponent<CapsuleCollider>();
            hitbox.height=HeroIdentity.HeroCollisionHeight;hitbox.radius=HeroIdentity.HeroCollisionRadius;
            ParentToWorld(bot);

            HeroIdentity identity=bot.AddComponent<HeroIdentity>();identity.Configure(data.Hero);
            HeroCombatProfile profile=bot.AddComponent<HeroCombatProfile>();profile.Configure(data.Hero);
            HeroAbilityKit kit=bot.AddComponent<HeroAbilityKit>();kit.Configure(data.Hero);
            Health health=bot.AddComponent<Health>();health.Configure(profile.Health);
            TeamMember member=bot.AddComponent<TeamMember>();member.Initialize(data.Team);
            bot.AddComponent<HeroLocomotion>();
            bot.AddComponent<StylizedHeroVisual>().Build();
            bot.AddComponent<CoopHeroSlot>().Configure(slot,data);

            if(authoritative)
            {
                BotPersonality personality=bot.AddComponent<BotPersonality>();
                personality.Configure((BotPersonality.Style)(slot%6));
                bot.AddComponent<BotTargetSensor>();
                MobaBotBrain brain=bot.AddComponent<MobaBotBrain>();
                brain.Initialize(data.Team,0f,personality.Aggression,personality.Caution);
                brain.RefreshPersonality();
                BotLanePlan roamPlan=bot.AddComponent<BotLanePlan>();roamPlan.Initialize(data.Team,0f);
                bot.AddComponent<MobaBotMover>();
                bot.AddComponent<MobaBotCombat>();
                bot.AddComponent<HeroBotAbilities>();
                bot.AddComponent<MobaBotRespawn>();
                bot.AddComponent<CombatFeedbackComponent>();
            }
            return bot;
        }

        private static void ParentToWorld(GameObject actor)
        {
            GameObject world=GameObject.Find("DF_MatchWorld");
            if(world!=null)actor.transform.SetParent(world.transform,true);
        }
    }
}
