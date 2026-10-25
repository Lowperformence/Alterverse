namespace Alterverse.Characters
{
    /// <summary>Season 2 starting values. The 100-point allocation is a design budget, not an automatic stat conversion.</summary>
    public static class TimmyBalance
    {
        public const int DamagePoints = 30, MobilityPoints = 28, ControlPoints = 24, DurabilityPoints = 10, UtilityPoints = 8;
        public const int TotalPoints = DamagePoints + MobilityPoints + ControlPoints + DurabilityPoints + UtilityPoints;
        public const float RawHealth = 185f, BasicDamage = 22f, BasicRange = 2f, BasicInterval = .78f;
        public const float RawMoveSpeed = 7.1f * 1.05f * 1.05f * 1.03f;
        public const float BaseShield = 80f;
        public const float ModelScale = .70f, CollisionRadius = .28f, CollisionHeight = 1.4f;
        public const float TailSwayDegrees = 30f, TailSwaySeconds = 8f;
        public const float PassiveSpeed = 1.12f, PassiveSeconds = 1.2f, PassiveCooldown = 4f;
        public const float ClawDamage = 40f, ClawRange = 2.2f, ClawCooldown = 6f;
        public const float DustDamage = 14f, DustRange = 4.5f, DustRadius = 2f, DustCooldown = 12f, DustWindup = .3f;
        public const float DustSlow = .8f, DustSlowSeconds = 2f;
        public const float DustVfxSeconds = 2f;
        public const float JetRange = 4.5f, JetCooldown = 13f, JetDashSeconds = .16f;
        public const float ScratchRange = 9f, ScratchCooldown = 52f, ScratchDamage = 54f, ScratchWindup = .2f;
        public const float ScratchLeapSeconds = .4f, ScratchHoldSeconds = 2f, ScratchContactRange = 1.35f;
        public const float ScratchStandOff = .75f;
        public const int ScratchTicks = 4;
        public const float ScratchTickSeconds = ScratchHoldSeconds / ScratchTicks;
    }
}

