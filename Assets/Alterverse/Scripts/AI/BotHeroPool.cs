using System;
using System.Collections.Generic;
using Alterverse.Characters;

namespace Alterverse.AI
{
    // Bots use the complete catalog. Player ownership/release checks belong to human selection.
    public static class BotHeroPool
    {
        private static readonly HeroId[] catalog=(HeroId[])Enum.GetValues(typeof(HeroId));
        public static HeroId[] Shuffled(string matchId,int team)
        {
            uint seed=2166136261u;
            unchecked
            {
                foreach(char c in matchId??string.Empty){seed^=c;seed*=16777619u;}
                seed^=(uint)team+0x9e3779b9u;seed*=16777619u;
            }
            var random=new Random((int)(seed&0x7fffffffu));
            var order=(HeroId[])catalog.Clone();
            for(int i=order.Length-1;i>0;i--){int j=random.Next(i+1);var hero=order[i];order[i]=order[j];order[j]=hero;}
            return order;
        }
        public static HeroId Next(HeroId[] order,ISet<HeroId> used,ref int cursor)
        {
            if(order==null||order.Length==0)throw new ArgumentException("Bot hero catalog is empty.",nameof(order));
            for(int i=0;i<order.Length;i++)
            {
                var hero=order[cursor++%order.Length];
                if(used==null||!used.Contains(hero))return hero;
            }
            // A future team larger than the catalog still fills without restricting paid heroes.
            return order[cursor++%order.Length];
        }
    }
}
