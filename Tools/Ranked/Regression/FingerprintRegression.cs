using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Alterverse.Multiplayer;
using Alterverse.Characters;
using Alterverse.AI;
using Alterverse.Match;
using Alterverse.Profile;
partial class Regression
{
    static uint fingerprintSink;
    static void FingerprintCases()
    {
        Check(CoopPlanHandshake.Fingerprint(null)==0,"Null fingerprint remains zero");
        Check(CoopPlanHandshake.Fingerprint(new CoopMatchPlan())==0,"Unstaged fingerprint remains zero");
        var random=new Random(177);
        string[] text={null,"","player","ä中🎮","x|y,z:null",new string('x',300),"\0\ud800"};
        int[] numbers={0,1,9,10,11,99,100,999,1000,int.MaxValue,-1,int.MinValue};
        var previousCulture=CultureInfo.CurrentCulture;
        try
        {
            var unusual=(CultureInfo)CultureInfo.InvariantCulture.Clone();unusual.NumberFormat.NegativeSign="minus🎮";
            foreach(var culture in new[]{CultureInfo.InvariantCulture,new CultureInfo("de-DE"),new CultureInfo("ar-SA"),unusual})
            {
                CultureInfo.CurrentCulture=culture;
                var plan=new CoopMatchPlan();
                for(int n=0;n<1000;n++)
                {
                    plan.MatchId=text[random.Next(text.Length)];plan.Arena=(ArenaVariant)numbers[random.Next(numbers.Length)];
                    plan.Difficulty=(BotDifficulty)numbers[random.Next(numbers.Length)];
                    plan.Session=new MatchSession("test",(SessionMode)(n%4));
                    for(int i=0;i<plan.Session.Slots.Length;i++)
                    {
                        if(random.Next(5)==0)continue;
                        plan.Session.Slots[i]=new MatchPlayerSlot{
                            PlayerId=text[random.Next(text.Length)],DisplayName=text[random.Next(text.Length)],
                            Team=numbers[random.Next(numbers.Length)],Hero=(HeroId)numbers[random.Next(numbers.Length)],
                            BattleItem=(BattleItemType)numbers[random.Next(numbers.Length)],IsBot=random.Next(2)==0,IsHost=random.Next(2)==0};
                    }
                    Check(CoopPlanHandshake.Fingerprint(plan)==LegacyFingerprint(plan),"Exact legacy UTF-16 fingerprint across cultures and mutated rosters");
                    plan.MatchId+="new-round";
                    Check(CoopPlanHandshake.Fingerprint(plan)==LegacyFingerprint(plan),"In-place plan mutations cannot reuse a stale round identity");
                }
            }
            CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
            var bench=new CoopMatchPlan{MatchId="performance-round",Session=new MatchSession("bench",SessionMode.OnlineCasual)};
            for(int i=0;i<bench.Session.Slots.Length;i++)bench.Session.Slots[i]=new MatchPlayerSlot{
                PlayerId="player-"+i,DisplayName="Player "+i,Team=i/4,Hero=(HeroId)i,IsBot=i>0};
            for(int i=0;i<2000;i++){fingerprintSink^=LegacyFingerprint(bench);fingerprintSink^=CoopPlanHandshake.Fingerprint(bench);}
#if !MONO_FIXTURE
            const int iterations=20000;
            long before=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<iterations;i++)fingerprintSink^=CoopPlanHandshake.Fingerprint(bench);
            long optimizedBytes=GC.GetAllocatedBytesForCurrentThread()-before;
            before=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<iterations;i++)fingerprintSink^=LegacyFingerprint(bench);
            long legacyBytes=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(optimizedBytes==0,"Normal eight-slot plan hashing allocates zero managed bytes after warmup");
            Check(legacyBytes>optimizedBytes,"Fingerprint allocation reduction is measured against the old implementation");
            var clock=Stopwatch.StartNew();
            for(int i=0;i<iterations;i++)fingerprintSink^=LegacyFingerprint(bench);
            double legacyMs=clock.Elapsed.TotalMilliseconds;clock.Restart();
            for(int i=0;i<iterations;i++)fingerprintSink^=CoopPlanHandshake.Fingerprint(bench);
            Console.WriteLine($"Fingerprint benchmark (.NET fixture, {iterations} calls): old={legacyBytes}B/{legacyMs:F2}ms new={optimizedBytes}B/{clock.Elapsed.TotalMilliseconds:F2}ms. Not a native server FPS measurement.");
#endif
        }
        finally{CultureInfo.CurrentCulture=previousCulture;}
    }
        private static uint LegacyFingerprint(CoopMatchPlan plan)
        {
            if(plan==null||plan.Session==null)return 0;

            StringBuilder builder=new(256);
            builder.Append(plan.MatchId??"").Append('|')
                .Append((int)plan.Arena).Append('|')
                .Append((int)plan.Difficulty);
            if (plan.Session.Mode == SessionMode.OnlineRanked) builder.Append("|ranked");

            for(int i=0;i<plan.Session.Slots.Length;i++)
            {
                MatchPlayerSlot slot=plan.Session.Slots[i];
                builder.Append('|').Append(i).Append(':');
                if(slot==null){builder.Append("null");continue;}
                builder.Append(slot.PlayerId??"").Append(',')
                    .Append(slot.DisplayName??"").Append(',')
                    .Append(slot.Team).Append(',')
                    .Append((int)slot.Hero).Append(',')
                    .Append((int)slot.BattleItem).Append(',')
                    .Append(slot.IsBot?'1':'0').Append(',')
                    .Append(slot.IsHost?'1':'0');
            }

            // Stable FNV-1a over UTF-16 code units; deterministic across both peers.
            const uint offset=2166136261u;
            const uint prime=16777619u;
            uint hash=offset;
            string text=builder.ToString();
            for(int i=0;i<text.Length;i++)
            {
                char ch=text[i];
                hash^=(byte)(ch&0xFF);hash*=prime;
                hash^=(byte)(ch>>8);hash*=prime;
            }
            return hash==0?1u:hash;
        }

}
