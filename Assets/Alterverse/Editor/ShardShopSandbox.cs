#if UNITY_EDITOR
using System.Collections.Generic;
using Alterverse.Shop;
using UnityEditor;
using UnityEngine;
namespace Alterverse.EditorTools
{
    // Isolated ephemeral state. Never reads/writes player wallet, profile or receipt storage.
    public sealed class ShardShopSandbox : EditorWindow
    {
        readonly HashSet<string> receipts = new();
        readonly HashSet<string> owned = new();
        int balance, sequence;
        string pendingId, pendingToken, lastId, lastToken, equipped;
        string status = "No real purchase. Closing this window discards all test state.";
        [MenuItem("DIMENSIONS FRONT/Development/Shard Shop Sandbox")]
        static void Open() => GetWindow<ShardShopSandbox>("TEST SHOP — NO PAYMENT");
        void OnGUI()
        {
            EditorGUILayout.HelpBox("EDITOR SIMULATION ONLY — no payment, no saved shards, no production unlocks. This does not test Google Play or server verification.", MessageType.Warning);
            EditorGUILayout.LabelField("Simulated shards", balance.ToString());
            EditorGUILayout.LabelField("Simulated equipment", equipped ?? "None");
            foreach (var pack in ShardPacks.All)
                if (GUILayout.Button("SIMULATE " + pack.Name + " · " + pack.Amount + " shards"))
                { pendingId=pack.Id; pendingToken="editor-"+(++sequence); status="Simulated pending payment: no shards granted."; }
            using (new EditorGUI.DisabledScope(pendingToken == null))
            {
                if(GUILayout.Button("SIMULATE CONFIRMED")){Grant(pendingId,pendingToken);lastId=pendingId;lastToken=pendingToken;pendingToken=null;}
                if(GUILayout.Button("SIMULATE CANCEL / FAILURE")){pendingToken=null;status="Cancelled: balance unchanged.";}
            }
            using(new EditorGUI.DisabledScope(lastToken==null))
                if(GUILayout.Button("REPLAY LAST CALLBACK"))Grant(lastId,lastToken);
            if(GUILayout.Button("BUY / EQUIP TEST VOIDFALL"))
            {
                if(owned.Contains("voidfall")){equipped="VOIDFALL";status="Already owned: equipped without another debit.";}
                else if(UniversalSkin.Price>0&&balance>=UniversalSkin.Price){balance-=UniversalSkin.Price;owned.Add("voidfall");equipped="VOIDFALL";status="Simulated cosmetic purchased and equipped.";}
                else status="Not enough simulated shards.";
            }
            if(GUILayout.Button("RESET TEST STATE")){receipts.Clear();owned.Clear();balance=0;pendingToken=lastToken=equipped=null;status="Reset. Real profile unchanged.";}
            EditorGUILayout.HelpBox(status,MessageType.Info);
        }
        void Grant(string id,string token)
        {
            if(!receipts.Add(token)){status="Duplicate callback ignored: no additional shards.";return;}
            foreach(var pack in ShardPacks.All)if(pack.Id==id){balance+=pack.Amount;status="Simulated confirmation credited once.";return;}
        }
    }
}
#endif
