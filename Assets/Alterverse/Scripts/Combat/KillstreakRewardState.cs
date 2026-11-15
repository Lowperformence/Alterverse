namespace Alterverse.Combat
{
    /// <summary>Unspent rewards survive death; only a reward earned this life can be upgraded.</summary>
    public sealed class KillstreakRewardState
    {
        public int Standard { get; private set; }
        public int Mobile { get; private set; }
        public bool UpgradeEligible { get; private set; }

        // The established system grants one standard turret for every three kills.
        // Two more kills upgrade that unspent turret to the mobile version.
        public bool RecordKill(int consecutiveKills)
        {
            if(consecutiveKills<=0)return false;
            if(consecutiveKills%3==0)
            {
                Standard++;UpgradeEligible=true;return true;
            }
            if(consecutiveKills>=5&&consecutiveKills%3==2&&UpgradeEligible&&Standard>0)
            {
                Standard--;Mobile++;UpgradeEligible=false;return true;
            }
            return false;
        }

        public void RecordDeath(){UpgradeEligible=false;}
        public bool HasReward=>Standard>0||Mobile>0;
        public bool NextIsMobile=>Mobile>0;
        public bool Consume()
        {
            if(Mobile>0){Mobile--;return true;}
            if(Standard<=0)return false;
            Standard--;UpgradeEligible=false;return true;
        }
        public void Reset(){Standard=Mobile=0;UpgradeEligible=false;}
        public void ApplySnapshot(int standard,int mobile,bool upgradeEligible)
        {
            Standard=System.Math.Max(0,standard);Mobile=System.Math.Max(0,mobile);
            UpgradeEligible=upgradeEligible&&Standard>0;
        }
    }
}
