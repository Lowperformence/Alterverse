using System;
using System.Collections.Generic;
using Alterverse.AI;
using Alterverse.Characters;

namespace Alterverse.Multiplayer
{
    // Local preparation only. Opening this lobby never creates a Relay/Unity session.
    public static class OfflineLobbyRoster
    {
        private static HeroId[] slots;
        public static bool Prepared => slots != null;
        public static string MatchId { get; private set; }
        public static HeroId At(int slot) => slots[slot];
        public static void Begin(HeroId localHero)
        {
            MatchId = Guid.NewGuid().ToString("N");
            slots = new HeroId[MatchSession.MaxPlayers];
            slots[0] = localHero;
            for (int team=0; team<2; team++)
            {
                var used = new HashSet<HeroId>();
                int cursor=0, start=team*MatchSession.TeamSize;
                if(team==0)used.Add(localHero);
                var order=BotHeroPool.Shuffled(MatchId,team);
                for(int i=start+(team==0?1:0);i<start+MatchSession.TeamSize;i++)
                { slots[i]=BotHeroPool.Next(order,used,ref cursor);used.Add(slots[i]); }
            }
        }
        public static bool Available(int slot,HeroId hero)
        {
            if(slots==null||slot<0||slot>=slots.Length||!Enum.IsDefined(typeof(HeroId),hero))return false;
            int start=slot/MatchSession.TeamSize*MatchSession.TeamSize;
            for(int i=start;i<start+MatchSession.TeamSize;i++)if(i!=slot&&slots[i]==hero)return false;
            return true;
        }
        public static bool Select(int slot,HeroId hero)
        { if(!Available(slot,hero))return false;slots[slot]=hero;return true; }
        public static bool Matches(HeroId localHero)=>Prepared&&slots[0]==localHero;
        public static void Clear(){slots=null;MatchId=null;}
    }
}
