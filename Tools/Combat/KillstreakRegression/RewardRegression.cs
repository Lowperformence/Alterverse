using System;
using Alterverse.Combat;

static class RewardRegression
{
    static int checks;
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;}
    static void Main()
    {
        var r=new KillstreakRewardState();
        Check(!r.RecordKill(1)&&!r.RecordKill(2)&&!r.HasReward,"no early reward");
        Check(r.RecordKill(3)&&r.Standard==1&&r.Mobile==0&&r.UpgradeEligible,"three kills grant the existing turret");
        Check(!r.RecordKill(4)&&r.Standard==1,"four kills keep the unspent turret");
        Check(r.RecordKill(5)&&r.Standard==0&&r.Mobile==1&&!r.UpgradeEligible,"five kills upgrade rather than duplicate the reward");
        Check(r.NextIsMobile&&r.Consume()&&!r.HasReward,"mobile reward can be spent once");
        r.Reset();r.RecordKill(3);
        Check(r.Consume()&&!r.RecordKill(5)&&!r.HasReward,"spending at three prevents the five-kill upgrade");
        r.Reset();r.RecordKill(3);r.RecordDeath();
        Check(r.Standard==1&&!r.UpgradeEligible&&!r.RecordKill(5),"death preserves old reward but invalidates its upgrade");
        r.RecordKill(3);r.RecordKill(5);
        Check(r.Standard==1&&r.Mobile==1,"new life upgrades only its own unspent reward");
        Check(r.Consume()&&r.Mobile==0&&r.Standard==1&&r.Consume()&&!r.Consume(),"priority and spending counts");
        r.Reset();r.RecordKill(3);r.RecordKill(5);r.RecordKill(6);r.RecordKill(8);
        Check(r.Mobile==2&&r.Standard==0,"later consecutive rewards can also upgrade");
        r.ApplySnapshot(2,1,true);Check(r.Standard==2&&r.Mobile==1&&r.UpgradeEligible,"network snapshot");
        r.ApplySnapshot(-1,-2,true);Check(!r.HasReward&&!r.UpgradeEligible,"invalid negative snapshot is clamped");
        r.Reset();Check(!r.HasReward&&!r.UpgradeEligible,"reset clears all rewards");
        Console.WriteLine("PASS "+checks+" killstreak reward checks");
    }
}
