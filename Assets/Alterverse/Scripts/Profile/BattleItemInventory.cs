using UnityEngine;

namespace Alterverse.Profile
{
    public enum BattleItemType { None=0, Heal=1, Shield=2, Speed=3, Meteor=4 }

    public static class BattleItemInventory
    {
        public static event System.Action Changed;
        public const float VictoryDropChance=.30f;
        public const int ShieldShardPrice=100;

        private const string EquipKey="DF_BattleItemEquipped";
        private const string ReceiptKey="DF_LastBattleItemRewardReceipt";
        private const string ReceiptDropKey="DF_LastBattleItemRewardDrop";

        public static readonly BattleItemType[] All =
        {
            BattleItemType.Heal,BattleItemType.Shield,BattleItemType.Speed,BattleItemType.Meteor
        };

        public static BattleItemType Equipped
        {
            get
            {
                BattleItemType item=(BattleItemType)Mathf.Clamp(PlayerPrefs.GetInt(EquipKey,0),0,4);
                return item!=BattleItemType.None&&Count(item)>0?item:BattleItemType.None;
            }
        }

        public static int Count(BattleItemType item)
        {
            if(item==BattleItemType.None)return 0;
            return Mathf.Max(0,PlayerPrefs.GetInt(Key(item),0));
        }

        public static string Name(BattleItemType item)=>item switch
        {
            BattleItemType.Heal=>"REGEN CORE",
            BattleItemType.Shield=>"AEGIS CORE",
            BattleItemType.Speed=>"VELOCITY CORE",
            BattleItemType.Meteor=>"METEOR CORE",
            _=>"NONE"
        };

        public static string ShortDescription(BattleItemType item)=>item switch
        {
            BattleItemType.Heal=>"Team pickup: restores 20% max HP.",
            BattleItemType.Shield=>"Team pickup: grants a 20% max-HP shield.",
            BattleItemType.Speed=>"Team pickup: +10% move speed for 5 seconds.",
            BattleItemType.Meteor=>"Calls a telegraphed meteor on your focused enemy for 40 damage.",
            _=>"No battle item equipped."
        };

        public static bool Equip(BattleItemType item)
        {
            if(item==BattleItemType.None){PlayerPrefs.SetInt(EquipKey,0);PlayerPrefs.Save();Changed?.Invoke();return true;}
            if(Count(item)<=0)return false;
            PlayerPrefs.SetInt(EquipKey,(int)item);PlayerPrefs.Save();Changed?.Invoke();return true;
        }

        public static void Grant(BattleItemType item,int amount=1)
        {
            if(item==BattleItemType.None||amount<=0)return;
            PlayerPrefs.SetInt(Key(item),ProfileTransaction.Add(Count(item),amount));
            PlayerPrefs.Save();Changed?.Invoke();
        }

        public static bool TryConsumeEquipped(BattleItemType expected)
        {
            if(expected==BattleItemType.None||Equipped!=expected)return false;
            int count=Count(expected);if(count<=0)return false;
            PlayerPrefs.SetInt(Key(expected),count-1);
            PlayerPrefs.SetInt(EquipKey,0);
            PlayerPrefs.Save();Changed?.Invoke();
            return true;
        }

        public static BattleItemType RollVictoryDrop(string matchReceipt)
        {
            if(string.IsNullOrEmpty(matchReceipt))return BattleItemType.None;
            if(PlayerPrefs.GetString(ReceiptKey,"")==matchReceipt)
                return (BattleItemType)Mathf.Clamp(PlayerPrefs.GetInt(ReceiptDropKey,0),0,4);

            BattleItemType drop=BattleItemType.None;
            if(Random.value<VictoryDropChance)drop=All[Random.Range(0,All.Length)];

            if(drop==BattleItemType.None)
            {
                ProfileTransaction.Commit(
                    new ProfileTransaction.Write(ReceiptKey,matchReceipt),
                    new ProfileTransaction.Write(ReceiptDropKey,0));
            }
            else
            {
                ProfileTransaction.Commit(
                    new ProfileTransaction.Write(Key(drop),ProfileTransaction.Add(Count(drop),1)),
                    new ProfileTransaction.Write(ReceiptKey,matchReceipt),
                    new ProfileTransaction.Write(ReceiptDropKey,(int)drop));
            }
            return drop;
        }

        public static bool TryBuyShield()
        {
            if(!PlayerWallet.CanAfford(ShieldShardPrice))return false;
            ProfileTransaction.Commit(
                new ProfileTransaction.Write("DF_Shards",PlayerWallet.Balance-ShieldShardPrice),
                new ProfileTransaction.Write(Key(BattleItemType.Shield),ProfileTransaction.Add(Count(BattleItemType.Shield),1)));
            return true;
        }

        internal static string StorageKey(BattleItemType item)=>Key(item);
        private static string Key(BattleItemType item)=>"DF_BattleItem_"+(int)item;
    }
}

