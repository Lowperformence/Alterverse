using System;
using System.Collections.Generic;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Multiplayer;
partial class Regression
{
    static void LobbyPreparationCases()
    {
        foreach(HeroId local in Enum.GetValues(typeof(HeroId)))
        {
            OfflineLobbyRoster.Begin(local);
            Check(OfflineLobbyRoster.Matches(local),"offline preparation retains local choice");
            for(int team=0;team<2;team++)
            {
                var seen=new HashSet<HeroId>();
                for(int row=0;row<MatchSession.TeamSize;row++)Check(seen.Add(OfflineLobbyRoster.At(team*MatchSession.TeamSize+row)),"offline heroes unique per team");
            }
            Check(!OfflineLobbyRoster.Select(1,local),"cannot select teammate hero");
            Check(!OfflineLobbyRoster.Select(-1,local)&&!OfflineLobbyRoster.Select(1,(HeroId)999),"invalid slot and hero rejected");
            HeroId opponent=OfflineLobbyRoster.At(MatchSession.TeamSize);
            Check(OfflineLobbyRoster.Available(MatchSession.TeamSize,opponent),"same hero on opposite team is allowed");
        }
        OfflineLobbyRoster.Clear();Check(!OfflineLobbyRoster.Prepared,"offline state can be cleared");
        for(int seed=0;seed<200;seed++)
        {
            var zones=CosmicNebulaLayout.Create("match-"+seed);var again=CosmicNebulaLayout.Create("match-"+seed);
            Check(zones.Length==4,"four bounded clouds");
            for(int i=0;i<4;i++)
            {
                var z=zones[i];
                Check(z.X==again[i].X&&z.Z==again[i].Z&&z.Radius==again[i].Radius,"deterministic for all peers");
                Check(Math.Abs(z.X)+z.Radius<30&&Math.Abs(z.Z)+z.Radius<30,"clouds stay away from bases and arena edge");
                Check(z.Contains(z.X,z.Z)&&!z.Contains(z.X+z.Radius+1,z.Z),"cloud boundary");
                if(i%2==0)Check(z.X==-zones[i+1].X&&z.Z==-zones[i+1].Z,"mirrored access for both teams");
            }
        }
        Check(CosmicNebulaLayout.Concealed(true,true,false,0),"outside enemy cannot see hidden hero");
        Check(!CosmicNebulaLayout.Concealed(false,true,false,0),"allies remain visible");
        Check(!CosmicNebulaLayout.Concealed(true,true,true,0),"shared cloud reveals opponents");
        Check(!CosmicNebulaLayout.Concealed(true,true,false,1),"attacking reveals hero");
        Check(!CosmicNebulaLayout.Concealed(true,false,false,0),"leaving cloud reveals hero");
    }
}
