namespace Alterverse.Shop
{
    public readonly struct AvatarShopItem
    {
        public readonly int AvatarId;
        public readonly string Name, Theme;
        public readonly int Price;

        public AvatarShopItem(int avatarId, string name, string theme, int price)
        {
            AvatarId = avatarId;
            Name = name;
            Theme = theme;
            Price = price;
        }
    }

    public static class AvatarShopCatalog
    {
        public const string CurrencyName = "SHARDS";

        public static readonly AvatarShopItem[] Items =
        {
            new AvatarShopItem(10, "SHATTERED MOON", "ZERO", ShardPacks.CosmeticPrice("avatar_10")),
            new AvatarShopItem(11, "LUMEN LENS", "LUMEN", ShardPacks.CosmeticPrice("avatar_11")),
            new AvatarShopItem(12, "REALITY COLLISION", "COLLISION FRONT", ShardPacks.CosmeticPrice("avatar_12")),
            new AvatarShopItem(13, "VOID MONOLITH", "ZERO", ShardPacks.CosmeticPrice("avatar_13")),
            new AvatarShopItem(14, "QUELL PRISM", "LUMEN", ShardPacks.CosmeticPrice("avatar_14")),
            new AvatarShopItem(15, "ECLIPSE RELIC", "FRONT ELITE", ShardPacks.CosmeticPrice("avatar_15")),
            new AvatarShopItem(17, "COSMIC CAT", "COMIC FRONT", ShardPacks.CosmeticPrice("avatar_17")),
            new AvatarShopItem(18, "BATTLE DUCK", "COMIC FRONT", ShardPacks.CosmeticPrice("avatar_18")),
            new AvatarShopItem(19, "COFFEE BOT", "COMIC FRONT", ShardPacks.CosmeticPrice("avatar_19")),
            new AvatarShopItem(20, "DISCO ALIEN", "COMIC FRONT", ShardPacks.CosmeticPrice("avatar_20")),
            new AvatarShopItem(21, "VOID KNIGHT", "ZERO", ShardPacks.CosmeticPrice("avatar_21")),
            new AvatarShopItem(22, "FROST WOLF", "FROZEN FRONT", ShardPacks.CosmeticPrice("avatar_22")),
            new AvatarShopItem(23, "PHOENIX", "REBIRTH", ShardPacks.CosmeticPrice("avatar_23")),
            new AvatarShopItem(24, "NEVER GIVE UP", "RESOLVE", ShardPacks.CosmeticPrice("avatar_24")),
            new AvatarShopItem(25, "TEAM FIRST", "SOLIDARITY", ShardPacks.CosmeticPrice("avatar_25")),
            new AvatarShopItem(26, "GOOD GAME", "SPORTSMANSHIP", ShardPacks.CosmeticPrice("avatar_26")),
            new AvatarShopItem(32, "NEROK ANCHOR", "NEROK ABYSS", ShardPacks.CosmeticPrice("avatar_32")),
            new AvatarShopItem(33, "AUREN AEGIS", "AUREN KEST", ShardPacks.CosmeticPrice("avatar_33")),
            new AvatarShopItem(34, "TRAVELER RIFT", "THE TRAVELER", ShardPacks.CosmeticPrice("avatar_34")),
            new AvatarShopItem(35, "TILT PROOF", "COMIC FRONT", ShardPacks.CosmeticPrice("avatar_35")),
            new AvatarShopItem(36, "SHARK SNACK", "COMIC FRONT", ShardPacks.CosmeticPrice("avatar_36")),
            new AvatarShopItem(38, "BRAKK IMPACT", "BRAKK ORUN", ShardPacks.CosmeticPrice("avatar_38")),
            new AvatarShopItem(39, "DIMENSION HEART", "COLLISION FRONT", ShardPacks.CosmeticPrice("avatar_39")),
            new AvatarShopItem(40, "LOOT GOBLIN", "COMIC FRONT", ShardPacks.CosmeticPrice("avatar_40")),
            new AvatarShopItem(41, "ONE MORE MATCH", "COMIC FRONT", ShardPacks.CosmeticPrice("avatar_41")),
            new AvatarShopItem(42, "COMEBACK SPARK", "RESOLVE", ShardPacks.CosmeticPrice("avatar_42")),
            new AvatarShopItem(43, "GOOD GAME ROYAL", "SPORTSMANSHIP", ShardPacks.CosmeticPrice("avatar_43")),
            new AvatarShopItem(44, "GANGSTER SKULL", "STREET LEGEND", ShardPacks.CosmeticPrice("avatar_44")),
            new AvatarShopItem(45, "STREET RABBIT", "STREET LEGEND", ShardPacks.CosmeticPrice("avatar_45")),
            new AvatarShopItem(46, "NEON CAT", "MIDNIGHT", ShardPacks.CosmeticPrice("avatar_46"))
        };

        public static bool TryGet(int avatarId, out AvatarShopItem item)
        {
            for (int i = 0; i < Items.Length; i++)
            {
                if (Items[i].AvatarId != avatarId) continue;
                item = Items[i];
                return true;
            }

            item = default;
            return false;
        }
    }
}
