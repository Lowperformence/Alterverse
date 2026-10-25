namespace Alterverse.Characters
{
    public static class Bolt7Balance
    {
        public const float Health=360f,Shield=60f,BasicDamage=5f,BasicRange=5.5f;
        public static float BasicInterval=>HeroCombatProfile.StatsFor(HeroId.Nyra).AttackInterval/3f;
        public const float MoveSpeed=4.5f,BasicWindup=.02f;
        public const float GrenadeDamage=35f,GrenadeEdgeDamage=15f,GrenadeCore=.6f,
            GrenadeRadius=1.4f,GrenadeRange=5f,GrenadeWindup=.45f,GrenadeCooldown=5f;
        public const float RamDamage=40f,RamRange=3f,RamDash=2f,RamReach=1f,
            RamAngle=70f,RamKnockback=.8f,RamWindup=.3f,RamCooldown=8f;
        public const float UltimateRange=6f,UltimateRadius=3f,UltimateWindup=.9f,UltimateCooldown=75f;
        public const float FxScale=2.5f;
        public static bool HasAbility(int slot)=>slot==0||slot==1||slot==3;
    }
}
