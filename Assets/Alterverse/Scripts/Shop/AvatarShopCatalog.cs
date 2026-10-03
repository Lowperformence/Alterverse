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
            new AvatarShopItem(27, "NEROK BITE", "NEROK ABYSS", ShardPacks.CosmeticPrice("avatar_27")),
            new AvatarShopItem(28, "ANCHOR RECALL", "NEROK ABYSS", ShardPacks.CosmeticPrice("avatar_28")),
            new AvatarShopItem(29, "ANCHOR SLAM", "NEROK ABYSS", ShardPacks.CosmeticPrice("avatar_29")),
            new AvatarShopItem(30, "TIDAL CHARGE", "NEROK ABYSS", ShardPacks.CosmeticPrice("avatar_30")),
            new AvatarShopItem(31, "ABYSSAL MAELSTROM", "NEROK ABYSS", ShardPacks.CosmeticPrice("avatar_31"))
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
