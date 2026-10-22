using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Alterverse.Characters;
using Alterverse.Profile;
using Alterverse.Shop;
using Alterverse.Visuals;
using UnityEngine;

static class Regression
{
    static int checks;
    static void Check(bool value,string text){checks++;if(!value)throw new Exception(text);}
    static void Reset(int level,bool premium=true)
    {
        CosmeticProfile.TestData=null;AccountProfilePrefs.Ints.Clear();AccountProfilePrefs.Strings.Clear();ProfileTransaction.Commits.Clear();
        AccountProfilePrefs.SetInt("DF_BP_BETA01_XP",BattlePassProgress.TotalXpForLevel(level));
        AccountProfilePrefs.SetInt("DF_BP_BETA01_Premium",premium?1:0);
    }
    static GameObject Actor(bool authored=false)
    {
        var actor=new GameObject("fixture");actor.AddComponent<HeroIdentity>().Hero=HeroId.Rook;
        var root=new GameObject("DF_VisualRoot").transform;root.SetParent(actor.transform,false);
        var procedural=new GameObject("DF_Head");procedural.transform.SetParent(root,false);
        procedural.AddComponent<MeshFilter>().sharedMesh=new Mesh{bounds=new Bounds(new Vector3(0,1,0),new Vector3(.4f,.5f,.4f))};
        if(authored)
        {
            // Authored geometry must win over the separate procedural/pose control.
            procedural.GetComponent<MeshFilter>().sharedMesh.bounds=new Bounds(Vector3.zero,new Vector3(99,99,99));
            var head=new GameObject("Head").transform;head.SetParent(root,false);
            var surface=new GameObject("body");surface.transform.SetParent(root,false);
            var skin=surface.AddComponent<SkinnedMeshRenderer>();skin.bones=new[]{root,head};
            var points=new List<Vector3>();var weights=new List<BoneWeight>();
            for(int i=0;i<8;i++)
            {
                points.Add(new Vector3((i&1)==0?-.2f:.2f,(i&2)==0?.75f:1.25f,(i&4)==0?-.2f:.2f));
                weights.Add(new BoneWeight{boneIndex0=1,weight0=.8f,boneIndex1=0,weight1=.2f});
            }
            points.Add(new Vector3(999,999,999));weights.Add(new BoneWeight{boneIndex0=0,weight0=1});
            skin.sharedMesh=new Mesh{vertices=points.ToArray(),boneWeights=weights.ToArray()};
        }
        HeadwearPresentation.Configure(actor,root,HeroId.Rook,new SkinPreviewSelection());
        return actor;
    }
    static void AttachmentChecks()
    {
        int commits=ProfileTransaction.Commits.Count;
        foreach(bool authored in new[]{false,true})
        {
            var actor=Actor(authored);var view=actor.GetComponent<HeadwearPresentation>();
            Check(view.Apply(UniversalHeroCosmetic.BetaCap),"Cap did not fit synthetic head geometry: "+view.LastFitError);
            Check(view.Head.name==(authored?"Head":"DF_Head"),"Cap selected a pose control instead of the weighted head bone.");
            Check(Math.Abs(view.HeadBounds.size.x-.4f)<.001f&&Math.Abs(view.HeadBounds.size.y-.5f)<.001f,"Body/non-head vertices contaminated head bounds.");
            var cap=view.Head.Find("DF_Headwear_BetaCap");
            Check(cap!=null&&cap.parent==view.Head,"Cap was not attached to the selected head.");
            int count=actor.GetComponentsInChildren<MeshFilter>().Length;
            Check(view.Apply(UniversalHeroCosmetic.BetaCap)&&actor.GetComponentsInChildren<MeshFilter>().Length==count,"Repeated apply duplicated cap geometry.");
            Check(view.Apply(UniversalHeroCosmetic.Original)&&view.Head.Find("DF_Headwear_BetaCap")==null,"Unequip left cap geometry attached.");
        }
        var empty=new GameObject("empty");var root=new GameObject("DF_VisualRoot").transform;root.SetParent(empty.transform,false);
        HeadwearPresentation.Configure(empty,root,HeroId.Rook,new SkinPreviewSelection());
        var invalid=empty.GetComponent<HeadwearPresentation>();
        Check(!invalid.Apply(UniversalHeroCosmetic.BetaCap)&&invalid.LastFitError!=null,"Missing head geometry silently accepted.");
        var noShader=Actor();Shader.Available=false;
        Check(!noShader.GetComponent<HeadwearPresentation>().Apply(UniversalHeroCosmetic.BetaCap),"Unavailable shader silently accepted.");
        Shader.Available=true;
        Check(ProfileTransaction.Commits.Count==commits,"Rendering a cap changed the player profile.");
    }
    static object[] StatePacket(int value=0)
    {
        var values=new List<object>{Alterverse.Multiplayer.CoopPlanHandshake.Round,8};
        for(int i=0;i<8;i++){values.Add(i);values.Add(i==1?value:0);}return values.ToArray();
    }
    static void NetworkChecks()
    {
        Reset(50);AccountProfilePrefs.SetInt(HeadwearSelection.BetaCapOwnedKey,1);
        HeadwearSelection.EquipFor(HeroId.Rook,UniversalHeroCosmetic.BetaCap);
        int commits=ProfileTransaction.Commits.Count;
        var manager=new Unity.Netcode.NetworkManager{IsServer=true};Unity.Netcode.NetworkManager.Singleton=manager;
        Alterverse.Multiplayer.CoopAuthority.LocalHasServerAuthority=true;Time.unscaledTime=0;
        var server=new Alterverse.Multiplayer.CoopHeroStateSync();server.Initialize();server.Put(1,Actor());
        uint round=Alterverse.Multiplayer.CoopPlanHandshake.Round;
        object[] packet={round,1,new Unity.Collections.FixedString128Bytes("player"),7};
        server.Request(8,packet);Check(server.Messages.Sent.Count==0,"Unauthorized sender changed another player's headwear.");
        var wrong=(object[])packet.Clone();wrong[0]=round+1;server.Request(7,wrong);
        Check(server.Messages.Sent.Count==0,"Stale-round headwear request accepted.");
        wrong=(object[])packet.Clone();wrong[3]=999;server.Request(7,wrong);
        Check(server.Messages.Sent.Count==0,"Unsupported cosmetic request accepted.");
        wrong=(object[])packet.Clone();wrong[1]=0;server.Request(7,wrong);
        Check(server.Messages.Sent.Count==0,"Owner changed another roster slot.");
        server.Request(7,round);Check(server.Messages.Sent.Count==0,"Truncated request accepted.");
        server.Request(7,packet);
        Check(server.Value(1)==UniversalHeroCosmetic.BetaCap&&server.Messages.Sent.Count==1,"Valid owner cap request was not relayed.");
        server.Tick(manager);
        Check(server.Value(1)==UniversalHeroCosmetic.BetaCap,"First tick erased a cap request received before Update.");
        Check(server.Messages.Sent.All(p=>p.Peer==7),"Headwear state was sent to an older/unregistered peer.");
        server.Put(0,Actor(),true);Time.unscaledTime=2;server.Tick(manager);
        Check(server.Value(0)==UniversalHeroCosmetic.BetaCap,"Host headwear was not published.");
        Alterverse.Multiplayer.CoopPlanHandshake.Round++;Time.unscaledTime=4;server.Tick(manager);
        Check(server.Value(1)==UniversalHeroCosmetic.Original,"New round inherited old remote headwear.");
        server.Stop();Check(server.Messages.Handlers.Count==0,"Headwear handlers leaked on detach.");

        manager.IsServer=false;Alterverse.Multiplayer.CoopAuthority.LocalHasServerAuthority=false;
        var client=new Alterverse.Multiplayer.CoopHeroStateSync();client.Initialize();client.Put(0,Actor(),true);
        var remote=Actor();client.Put(1,remote);
        for(int i=0;i<6;i++){Time.unscaledTime=i*2;client.Tick(manager);}
        Check(client.Messages.Sent.Count==3,"Older server received unlimited headwear probes.");
        var state=StatePacket(7);client.State(7,state);
        Check(remote.GetComponent<HeadwearPresentation>().Current==UniversalHeroCosmetic.Original,"Non-server peer injected cosmetic state.");
        wrong=(object[])state.Clone();wrong[^2]=1;client.State(0,wrong);
        Check(remote.GetComponent<HeadwearPresentation>().Current==UniversalHeroCosmetic.Original,"Partially applied packet before rejecting duplicate slot.");
        wrong=(object[])state.Clone();wrong[^1]=999;client.State(0,wrong);
        Check(remote.GetComponent<HeadwearPresentation>().Current==UniversalHeroCosmetic.Original,"Invalid packet partially applied.");
        client.State(0,Alterverse.Multiplayer.CoopPlanHandshake.Round,8);
        client.State(0,state);
        Check(remote.GetComponent<HeadwearPresentation>().Current==UniversalHeroCosmetic.BetaCap,"Server cap state did not attach remote geometry.");
        Check(client.Messages.Sent.All(p=>p.Peer==0),"Client sent cosmetics directly to a non-server peer.");
        Time.unscaledTime=12;client.Tick(manager);
        Check(client.Messages.Sent.Count==4,"Acknowledged server did not resume state refresh.");
        client.State(0,StatePacket());
        Check(remote.GetComponent<HeadwearPresentation>().Current==UniversalHeroCosmetic.Original,"Remote unequip did not remove cap.");
        client.Stop();Check(ProfileTransaction.Commits.Count==commits,"Cosmetic network packets changed account ownership or progress.");
    }
    static void Main()
    {
        // Existing collection migration, wing purchases, bonuses and layout still behave exactly as before.
        Alterverse.UI.CosmeticCollectionChecks.Run();
        Reset(50);
        Check(BattlePassProgress.MaxLevel==50&&BattlePassProgress.PremiumShardTotal()==800,"Pass economy changed.");
        for(int level=1;level<=50;level++)
        {
            var r=BattlePassProgress.PremiumRewardFor(level);
            Check((r.AvatarId==16)==(level==25),"Crown must be at premium level 25 only.");
            Check((r.Headwear==UniversalHeroCosmetic.BetaCap)==(level==50),"Cap must be the premium level 50 reward only.");
            Check(BattlePassProgress.FreeRewardFor(level).Headwear==UniversalHeroCosmetic.Original,"Free track received premium cap.");
        }
        Reset(49);Check(!BattlePassProgress.ClaimPremium(50),"Cap claimed before level 50.");
        Reset(50,false);Check(!BattlePassProgress.ClaimPremium(50)&&!CosmeticCatalog.IsOwned(UniversalHeroCosmetic.BetaCap),"Free player received premium cap.");
        Reset(25);Check(BattlePassProgress.ClaimPremium(25)&&AvatarInventory.IsOwned(16),"Level 25 did not grant the crown.");
        Check(!CosmeticCatalog.IsOwned(UniversalHeroCosmetic.BetaCap),"Level 25 also granted the cap.");
        Reset(50);AccountProfilePrefs.SetInt("DF_AccountXP",12345);AccountProfilePrefs.SetInt("unrelatedSetting",77);
        Check(BattlePassProgress.ClaimPremium(50),"Level 50 cap claim failed.");
        Check(CosmeticCatalog.IsOwned(UniversalHeroCosmetic.BetaCap),"Cap ownership not persisted.");
        Check(!AvatarInventory.IsOwned(16),"Fresh level 50 claim still granted the old crown reward.");
        Check(PlayerWallet.EarnedBalance==200&&BattleItemInventory.Count(BattleItemType.Shield)==3,"Existing final currency/core rewards changed.");
        int balance=PlayerWallet.EarnedBalance,commits=ProfileTransaction.Commits.Count;
        Check(!BattlePassProgress.ClaimPremium(50)&&PlayerWallet.EarnedBalance==balance,"Final reward was duplicated.");
        Check(HeadwearSelection.EquippedFor(HeroId.Veyr)==UniversalHeroCosmetic.Original,"Claim silently equipped a hat.");
        AccountProfilePrefs.SetInt(UniversalSkin.OwnedKey,1);
        for(int hero=0;hero<HeroIdentity.HeroCount;hero++)
        {
            var id=(HeroId)hero;
            Check(UniversalCosmeticSelection.EquipFor(id,UniversalHeroCosmetic.Voidfall),"Existing wing equip failed.");
            Check(HeadwearSelection.EquipFor(id,UniversalHeroCosmetic.BetaCap),"Cap could not equip on catalog hero.");
            Check(HeadwearSelection.EquippedFor(id)==UniversalHeroCosmetic.BetaCap,"Per-hero head slot not read.");
            Check(UniversalCosmeticSelection.EquippedFor(id)==UniversalHeroCosmetic.Voidfall,"Cap replaced the wing slot.");
        }
        Check(HeadwearSelection.EquipFor(HeroId.Veyr,UniversalHeroCosmetic.Original),"Unequip failed.");
        Check(HeadwearSelection.EquippedFor(HeroId.Rook)==UniversalHeroCosmetic.BetaCap,"Unequip affected a different hero.");
        Check(UniversalCosmeticSelection.EquippedFor(HeroId.Veyr)==UniversalHeroCosmetic.Voidfall,"Unequip removed wings.");
        string persisted=JsonSerializer.Serialize(AccountProfilePrefs.Ints);
        AccountProfilePrefs.Ints=JsonSerializer.Deserialize<Dictionary<string,int>>(persisted);
        Check(HeadwearSelection.EquippedFor(HeroId.Rook)==UniversalHeroCosmetic.BetaCap,"Headwear did not survive profile reload.");
        Check(AccountProfilePrefs.GetInt("DF_AccountXP")==12345&&AccountProfilePrefs.GetInt("unrelatedSetting")==77,"Unrelated progress/settings changed.");
        Check(!HeadwearSelection.EquipFor((HeroId)999,UniversalHeroCosmetic.BetaCap)&&!HeadwearSelection.EquipFor(HeroId.Rook,(UniversalHeroCosmetic)999),"Invalid equip accepted.");
        Check(Alterverse.Match.WingMatchRewardBonus.Percent(UniversalHeroCosmetic.BetaCap)==0,"Hat added a gameplay bonus.");
        Reset(1);
        Check(!HeadwearSelection.EquipFor(HeroId.Rook,UniversalHeroCosmetic.BetaCap),"Unowned cap could be equipped.");
        Check(CosmeticCatalog.Purchase(UniversalHeroCosmetic.BetaCap)==AvatarPurchaseResult.InvalidItem,"Pass-exclusive cap can be purchased for free.");
        Check(!CosmeticCatalog.Find(UniversalHeroCosmetic.BetaCap).Purchasable,"Cap exposed as shard shop purchase.");
        foreach(int oldLevel in new[]{25,50})
        {
            Reset(oldLevel);ulong mask=1UL<<(oldLevel-1);
            AccountProfilePrefs.SetString("DF_BP_BETA01_PremiumClaims",mask.ToString());
            AccountProfilePrefs.SetInt("DF_Shards",4321);AccountProfilePrefs.SetInt("DF_BattleItem_2",9);
            PlayerProfile.Avatar=4;
            if(oldLevel==50)AccountProfilePrefs.SetInt("DF_AvatarOwned_16",1);
            BattlePassProgress.ReconcileCosmeticRewards();
            Check(AvatarInventory.IsOwned(16),"Existing eligible player lost/missed crown.");
            Check(CosmeticCatalog.IsOwned(UniversalHeroCosmetic.BetaCap)==(oldLevel==50),"Retroactive cap eligibility incorrect.");
            int first=ProfileTransaction.Commits.Count;BattlePassProgress.ReconcileCosmeticRewards();
            Check(ProfileTransaction.Commits.Count==first,"Cosmetic migration not idempotent.");
            Check(PlayerWallet.EarnedBalance==4321&&BattleItemInventory.Count(BattleItemType.Shield)==9,"Migration duplicated/reset currency or cores.");
            Check(AccountProfilePrefs.GetString("DF_BP_BETA01_PremiumClaims")==mask.ToString()&&PlayerProfile.Avatar==4,"Migration reset claims or selected avatar.");
            Check(!BattlePassProgress.ClaimPremium(oldLevel),"Old tier could be reclaimed.");
        }
        Reset(50,false);AccountProfilePrefs.SetString("DF_BP_BETA01_PremiumClaims",(1UL<<49).ToString());
        BattlePassProgress.ReconcileCosmeticRewards();
        Check(!CosmeticCatalog.IsOwned(UniversalHeroCosmetic.BetaCap),"Missing premium entitlement accepted during migration.");
        Mesh mesh=BetaCapMesh.Build(Matrix4x4.identity);
        Check(mesh.subMeshCount==3&&mesh.vertexCount>0&&mesh.vertexCount<2500,"Cap mesh exceeded budget or material layout.");
        foreach(var vertex in mesh.vertices)Check(float.IsFinite(vertex.x)&&float.IsFinite(vertex.y)&&float.IsFinite(vertex.z),"Nonfinite cap vertex.");
        foreach(var list in mesh.Indices.Values)
        {Check(list.Length%3==0,"Invalid triangles.");foreach(int index in list)Check(index>=0&&index<mesh.vertexCount,"Out-of-range triangle index.");}
        Check(mesh.bounds.max.z>1.5f&&mesh.bounds.max.y>.7f,"Cap has no visor or crown.");
        Check(mesh.Indices[2].Length>0,"BETA lettering missing.");
        AttachmentChecks();NetworkChecks();
        Console.WriteLine("PASS "+checks+" headwear, battle-pass, persistence, mesh and message checks; Unity fit/device/live multiplayer validation remains required.");
    }
}
