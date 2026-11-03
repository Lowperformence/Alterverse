using System;
namespace Alterverse.Combat
{
    public static class CosmicNebulaLayout
    {
        public readonly struct Zone
        {
            public readonly float X,Z,Radius;
            public Zone(float x,float z,float radius){X=x;Z=z;Radius=radius;}
            public bool Contains(float x,float z)=> (x-X)*(x-X)+(z-Z)*(z-Z)<=Radius*Radius;
        }
        // Mirrored pairs keep the random draw equally accessible to both teams.
        public static Zone[] Create(string matchId)
        {
            uint state=2166136261u;
            unchecked{foreach(char c in matchId??""){state^=c;state*=16777619u;}}
            if(state==0)state=1;
            var result=new Zone[4];
            for(int pair=0;pair<2;pair++)
            {
                float x=14f+Next(ref state)*10f,z=(pair==0?1:-1)*(15f+Next(ref state)*9f);
                float radius=3.8f+Next(ref state)*.7f;
                result[pair*2]=new Zone(x,z,radius);
                result[pair*2+1]=new Zone(-x,-z,radius);
            }
            return result;
        }
        private static float Next(ref uint state)
        {unchecked{state^=state<<13;state^=state>>17;state^=state<<5;}return (state&0x00ffffffu)/16777216f;}
        public static bool Concealed(bool enemies,bool targetInCloud,bool sharedCloud,float revealRemaining)
            =>enemies&&targetInCloud&&!sharedCloud&&revealRemaining<=0f;
    }
}
