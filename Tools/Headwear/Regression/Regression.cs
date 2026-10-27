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
        procedural.AddComponent<MeshRenderer>();
        if(authored)
        {
            // Authored geometry must win over the separate procedural/pose control.
            procedural.GetComponent<MeshFilter>().sharedMesh.bounds=new Bounds(Vector3.zero,new Vector3(99,99,99));
            procedural.GetComponent<MeshRenderer>().enabled=false;
            var head=new GameObject("Head").transform;head.SetParent(root,false);
            var surface=new GameObject("body");surface.transform.SetParent(root,false);
            var skin=surface.AddComponent<SkinnedMeshRenderer>();skin.bones=new[]{root,head};
            var points=new List<Vector3>();var weights=new List<BoneWeight>();
            for(int i=0;i<8;i++)
            {
                points.Add(new Vector3((i&1)==0?-.2f:.2f,(i&2)==0?.75f:1.25f,((i&4)==0?-.2f:.2f)+((i&2)==0?0:.08f)));
                weights.Add(new BoneWeight{boneIndex0=1,weight0=.8f,boneIndex1=0,weight1=.2f});
            }
            points.Add(new Vector3(999,999,999));weights.Add(new BoneWeight{boneIndex0=0,weight0=1});
            // Unreferenced, removed hair must not influence the visible head surface.
            points.Add(new Vector3(0,5,0));weights.Add(new BoneWeight{boneIndex0=1,weight0=1});
            skin.sharedMesh=new Mesh{vertices=points.ToArray(),boneWeights=weights.ToArray(),subMeshCount=1,bindposes=new[]{Matrix4x4.identity,Matrix4x4.identity}};
            skin.sharedMesh.SetTriangles(new List<int>{0,1,3,0,3,2,4,6,7,4,7,5,0,4,5,0,5,1,2,3,7,2,7,6,0,2,6,0,6,4,1,5,7,1,7,3,0,8,1},0);
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
            Check(view.SeatInHeadSpace.y>view.HeadBounds.center.y&&view.SeatInHeadSpace.y<view.HeadBounds.max.y,
                "The head does not enter the hat opening at the upper crown.");
            Check(Math.Abs(view.SeatInHeadSpace.z-(authored?.08f:0))<.0001f,"Hat used the face/chin center instead of the upper contour center.");
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
        var moving=Actor(true);var mounted=moving.GetComponent<HeadwearPresentation>();
        Check(mounted.Apply(UniversalHeroCosmetic.WizardHat),"Initial bind-frame hat fit failed.");
        var before=mounted.Head.Find("DF_Headwear_WizardHat").GetComponent<MeshFilter>().sharedMesh.vertices;
        mounted.Apply(UniversalHeroCosmetic.Original);
        mounted.Head.localToWorldMatrix=Matrix4x4.TRS(new Vector3(.25f,.12f,0),Quaternion.identity,Vector3.one);
        mounted.Head.worldToLocalMatrix=mounted.Head.localToWorldMatrix.inverse;
        Check(mounted.Apply(UniversalHeroCosmetic.WizardHat),"Late head-pose equip failed.");
        var after=mounted.Head.Find("DF_Headwear_WizardHat").GetComponent<MeshFilter>().sharedMesh.vertices;
        Check(before.SequenceEqual(after),"Equipping during a head pose baked an inverse pose into the hat.");
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
        Check(client.Messages.Sent.Count(p=>p.Name=="DF_HEADWEAR_REQUEST_V1")==3&&client.Messages.Sent.Count(p=>p.Name=="DF_HEADWEAR_REQUEST_V2")==3,"Older server received unlimited headwear probes.");
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
        Check(client.Messages.Sent.Count(p=>p.Name=="DF_HEADWEAR_REQUEST_V1")==4&&client.Messages.Sent.Count(p=>p.Name=="DF_HEADWEAR_REQUEST_V2")==3,"Acknowledged legacy server did not resume bounded state refresh.");
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
        Check(mesh.subMeshCount==3&&mesh.vertexCount>0&&mesh.vertexCount<6000,"Cap mesh exceeded budget or material layout.");
        foreach(var vertex in mesh.vertices)Check(float.IsFinite(vertex.x)&&float.IsFinite(vertex.y)&&float.IsFinite(vertex.z),"Nonfinite cap vertex.");
        foreach(var list in mesh.Indices.Values)
        {Check(list.Length%3==0,"Invalid triangles.");foreach(int index in list)Check(index>=0&&index<mesh.vertexCount,"Out-of-range triangle index.");}
        Check(mesh.bounds.max.z>1.5f&&mesh.bounds.max.y>.7f,"Cap has no visor or crown.");
        Check(mesh.Indices[2].Length>0,"BETA lettering missing.");
        foreach(var list in mesh.Indices.Values)
            for(int i=0;i<list.Length;i+=3)
            {
                var a=mesh.vertices[list[i]];var b=mesh.vertices[list[i+1]];var c=mesh.vertices[list[i+2]];
                var u=b-a;var v=c-a;
                float x=u.y*v.z-u.z*v.y,y=u.z*v.x-u.x*v.z,z=u.x*v.y-u.y*v.x;
                Check(x*x+y*y+z*z>1e-14f,"Trucker cap contains a zero-area face.");
                if(a.z>.7f&&b.z>.7f&&c.z>.7f&&a.y>.2f&&b.y>.2f&&c.y>.2f)
                    Check(z>0,"Front panel or BETA embroidery faces inward.");
            }
        CollectionCategoryChecks();AttachmentChecks();NetworkChecks();UfoChecks();ShopHatChecks();HatVisibilityChecks();MergedHaloChecks();HatSizeChecks();HatIconChecks();ModernNetworkChecks();NerokSkullChecks();BoltHeadBindChecks();
        Console.WriteLine("PASS "+checks+" headwear, battle-pass, persistence, mesh and message checks; Unity fit/device/live multiplayer validation remains required.");
    }
    static void CollectionCategoryChecks()
    {
        int commits=ProfileTransaction.Commits.Count;
        Check(Alterverse.UI.CosmeticCollectionCategories.Count==2&&
            Alterverse.UI.CosmeticCollectionCategories.Label(0)=="WINGS"&&Alterverse.UI.CosmeticCollectionCategories.Label(1)=="HATS",
            "Collection still exposes All/Skins or lacks separate Wings/Hats tabs.");
        foreach(var definition in CosmeticCatalog.All)
        {
            bool wings=Alterverse.UI.CosmeticCollectionCategories.Matches(definition,0);
            bool hats=Alterverse.UI.CosmeticCollectionCategories.Matches(definition,1);
            Check(!(wings&&hats)&&wings==(definition.Category==CosmeticCategory.Wings&&!definition.Headwear)&&hats==definition.Headwear,
                "Wings and hats leak into each other's collection.");
            Check(!Alterverse.UI.CosmeticCollectionCategories.Matches(definition,-1)&&!Alterverse.UI.CosmeticCollectionCategories.Matches(definition,2),
                "Invalid category exposes an All view.");
        }
        var shop=CosmeticCatalog.All.Where(x=>x.Purchasable).ToArray();
        Check(Alterverse.UI.CosmeticCollectionCategories.Choose(shop,1,UniversalHeroCosmetic.Voidfall)==UniversalHeroCosmetic.UfoTestHat,
            "Switching to Hats retained a wing preview instead of selecting the first available hat.");
        Check(Alterverse.UI.CosmeticCollectionCategories.Choose(shop,0,UniversalHeroCosmetic.WizardHat)==UniversalHeroCosmetic.Voidfall,
            "Switching to Wings retained a hat preview.");
        Check(Alterverse.UI.CosmeticCollectionCategories.Choose(shop,1,UniversalHeroCosmetic.MushroomHat)==UniversalHeroCosmetic.MushroomHat,
            "A valid selection was lost on refresh.");
        var owned=new[]{CosmeticCatalog.Find(UniversalHeroCosmetic.PropellerCap)};
        Check(Alterverse.UI.CosmeticCollectionCategories.Choose(owned,1,UniversalHeroCosmetic.WizardHat)==UniversalHeroCosmetic.PropellerCap&&
            Alterverse.UI.CosmeticCollectionCategories.Choose(owned,0,UniversalHeroCosmetic.PropellerCap)==UniversalHeroCosmetic.Original,
            "Inventory selected an unavailable item or the empty category retained a preview from the other tab.");
        Check(Alterverse.UI.CosmeticCollectionCategories.Choose(System.Array.Empty<CosmeticDefinition>(),1,UniversalHeroCosmetic.WizardHat)==UniversalHeroCosmetic.Original,
            "Empty hat collection did not clear its selection.");
        var future=new List<CosmeticDefinition>();
        for(int i=0;i<150;i++)future.Add(new CosmeticDefinition((UniversalHeroCosmetic)(1000+i),"FUTURE HAT "+i,100,"",CosmeticCategory.Skins,"",default,headwear:true));
        Check(future.All(x=>Alterverse.UI.CosmeticCollectionCategories.Matches(x,1)&&!Alterverse.UI.CosmeticCollectionCategories.Matches(x,0))&&
            Alterverse.UI.CosmeticCollectionCategories.Choose(future,1,future[149].Id)==future[149].Id,
            "Future hats depend on a fixed item count or hardcoded current IDs.");
        Check(ProfileTransaction.Commits.Count==commits,"Changing category modified ownership, equipment or currency.");
    }
    static void UfoChecks()
    {
        Reset(1,false);
        var item=CosmeticCatalog.Find(UniversalHeroCosmetic.UfoTestHat);
        Check(item.Headwear&&item.Purchasable&&item.Price==100&&item.PriceLabel=="100 SHARDS","UFO is not the requested 100-shard shop item.");
        Check(CosmeticCatalog.Sku(item.Id)=="ufo_test_hat","UFO client/server SKU mismatch.");
        Check(CosmeticCatalog.Purchase(item.Id)==AvatarPurchaseResult.InsufficientShards&&!CosmeticCatalog.IsOwned(item.Id),"Zero-shard account received the UFO.");
        AccountProfilePrefs.SetInt("DF_Shards",100);AccountProfilePrefs.SetInt("DF_AccountXP",777);
        Check(CosmeticCatalog.Purchase(item.Id)==AvatarPurchaseResult.Purchased,"Exact 100-shard purchase failed.");
        Check(PlayerWallet.Balance==0&&CosmeticCatalog.IsOwned(item.Id),"Purchase did not charge exactly 100 shards and grant ownership.");
        Check(HeadwearSelection.EquippedFor(HeroId.Rook)==UniversalHeroCosmetic.Original,"Purchase auto-equipped the UFO.");
        Check(CosmeticCatalog.Purchase(item.Id)==AvatarPurchaseResult.AlreadyOwned&&PlayerWallet.Balance==0,"Repeated purchase charged twice.");
        AccountProfilePrefs.SetInt(UniversalSkin.OwnedKey,1);
        for(int i=0;i<HeroIdentity.HeroCount;i++)
        {
            var hero=(HeroId)i;UniversalCosmeticSelection.EquipFor(hero,UniversalHeroCosmetic.Voidfall);
            Check(UniversalCosmeticSelection.EquipFor(hero,item.Id)&&HeadwearSelection.EquippedFor(hero)==item.Id,"UFO cannot equip on a catalog hero.");
            Check(UniversalCosmeticSelection.EquippedFor(hero)==UniversalHeroCosmetic.Voidfall,"UFO displaced wings.");
        }
        string snapshot=JsonSerializer.Serialize(AccountProfilePrefs.Ints);
        AccountProfilePrefs.Ints=JsonSerializer.Deserialize<Dictionary<string,int>>(snapshot);
        Check(HeadwearSelection.EquippedFor(HeroId.Rook)==item.Id&&AccountProfilePrefs.GetInt("DF_AccountXP")==777,"UFO loadout/profile reload failed.");
        Check(Alterverse.Match.WingMatchRewardBonus.Percent(item.Id)==0,"UFO introduced a progression bonus.");
        int commits=ProfileTransaction.Commits.Count;
        Check(UfoHatMesh.TryBuild(Matrix4x4.identity,Shader.Find("fixture"),out Mesh mesh,out Material[] materials,out string error),"Blender mesh did not load: "+error);
        Check(mesh.vertexCount==2579&&mesh.Indices.Values.Sum(x=>x.Length)==4984*3&&mesh.subMeshCount==6&&materials.Length==6,"Runtime mesh differs from the real Blender export.");
        Check(UfoHatMesh.TryGetBounds("UfoTestHat",out Bounds referenceBounds,out _)&&referenceBounds.size==mesh.bounds.size,
            "Cached UFO reference dimensions differ from the production mesh.");
        Check(!UfoHatMesh.TryGetBounds("../OtherAsset",out _,out _),"Unknown reference mesh path accepted.");
        foreach(bool authored in new[]{false,true})
        {
            var actor=Actor(authored);var view=actor.GetComponent<HeadwearPresentation>();
            Check(view.Apply(item.Id)&&view.Head.Find("DF_Headwear_UfoTestHat")!=null,"UFO failed real/procedural head-bound attachment.");
            Check(view.Apply(UniversalHeroCosmetic.BetaCap)&&view.Head.Find("DF_Headwear_UfoTestHat")==null,"Switching hats retained UFO geometry.");
            Check(view.Apply(item.Id)&&view.Head.Find("DF_Headwear_BetaCap")==null,"Switching back retained cap geometry.");
            Check(view.Apply(UniversalHeroCosmetic.Original),"UFO unequip failed.");
        }
        Check(ProfileTransaction.Commits.Count==commits,"UFO rendering changed account progress.");
        var manager=new Unity.Netcode.NetworkManager{IsServer=true};Unity.Netcode.NetworkManager.Singleton=manager;
        Alterverse.Multiplayer.CoopAuthority.LocalHasServerAuthority=true;
        var sync=new Alterverse.Multiplayer.CoopHeroStateSync();sync.Initialize();sync.Put(1,Actor());
        sync.Request(7,Alterverse.Multiplayer.CoopPlanHandshake.Round,1,new Unity.Collections.FixedString128Bytes("player"),8);
        Check(sync.Value(1)==item.Id&&sync.Messages.Sent.Count==1,"Server did not accept/relay the new UFO ID.");sync.Stop();
        manager.IsServer=false;Alterverse.Multiplayer.CoopAuthority.LocalHasServerAuthority=false;
        var remote=Actor();var client=new Alterverse.Multiplayer.CoopHeroStateSync();client.Initialize();client.Put(1,remote);client.State(0,StatePacket(8));
        Check(remote.GetComponent<HeadwearPresentation>().Current==item.Id,"Client did not apply server UFO state.");client.Stop();
        Reset(1,false);Check(!CosmeticCatalog.IsOwned(item.Id)&&!HeadwearSelection.EquipFor(HeroId.Rook,item.Id),"Another profile inherited the UFO.");
    }
    static Mesh Cube(Vector3 center,Vector3 size)
    {
        var mesh=new Mesh{subMeshCount=1};
        var points=new List<Vector3>();
        for(int i=0;i<8;i++)points.Add(center+Vector3.Scale(size*.5f,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
        mesh.SetVertices(points);
        mesh.SetTriangles(new List<int>{0,1,3,0,3,2,4,6,7,4,7,5,0,4,5,0,5,1,2,3,7,2,7,6,0,2,6,0,6,4,1,5,7,1,7,3},0);
        mesh.RecalculateBounds();return mesh;
    }
    static GameObject HeadFixture(bool fin,HeroId hero=HeroId.Nerok)
    {
        var actor=Actor(true);actor.GetComponent<HeroIdentity>().Hero=hero;
        var skin=actor.GetComponentsInChildren<SkinnedMeshRenderer>()[0];
        var vertices=new List<Vector3>();var triangles=new List<int>();
        void Surface(Vector3 center,Vector3 radius,int segments,int rings)
        {
            var points=new Vector3[(rings+1)*segments];
            for(int ring=0;ring<=rings;ring++)for(int side=0;side<segments;side++)
            {
                float a=ring*Mathf.PI/rings,b=side*2*Mathf.PI/segments;
                // Exact common pole positions also exercise duplicate normal seams.
                points[ring*segments+side]=center+new Vector3(
                    ring==0||ring==rings?0:radius.x*Mathf.Sin(a)*Mathf.Cos(b),
                    ring==0?radius.y:ring==rings?-radius.y:radius.y*Mathf.Cos(a),
                    ring==0||ring==rings?0:radius.z*Mathf.Sin(a)*Mathf.Sin(b));
            }
            void Triangle(int a,int b,int c)
            {
                // Runtime flat shading duplicates every triangle corner. Index-only
                // component detection would select a single face instead of the skull.
                foreach(int index in new[]{a,b,c}){triangles.Add(vertices.Count);vertices.Add(points[index]);}
            }
            for(int ring=0;ring<rings;ring++)for(int side=0;side<segments;side++)
            {
                int a=ring*segments+side,b=ring*segments+(side+1)%segments,c=a+segments,d=b+segments;
                Triangle(a,b,c);Triangle(b,d,c);
            }
        }
        Surface(new Vector3(0,1.757f,.08975f),new Vector3(.2156875f,.193f,.23275f),24,16);
        // More vertices than the skull, less surface area; the tallest 20% is all fin.
        if(fin)Surface(new Vector3(0,2.0005f,-.0915f),new Vector3(.014f,.0745f,.0485f),40,40);
        var source=new Mesh{name=hero==HeroId.Nerok?"DF_Nerok_Siege_0_SkinOrnaments_HeadAdornment":"BOLT7_AuthoredRuntimeMesh",
            vertices=vertices.ToArray(),boneWeights=vertices.Select(p=>new BoneWeight{boneIndex0=1,weight0=1}).ToArray(),
            bindposes=new[]{Matrix4x4.identity,Matrix4x4.identity},subMeshCount=1};
        source.SetTriangles(triangles,0);source.RecalculateBounds();skin.sharedMesh=source;
        var root=actor.transform.Find("DF_VisualRoot");
        HeadwearPresentation.Configure(actor,root,hero,new SkinPreviewSelection());
        return actor;
    }
    static readonly UniversalHeroCosmetic[] AllHats={UniversalHeroCosmetic.BetaCap,UniversalHeroCosmetic.UfoTestHat,
        UniversalHeroCosmetic.WizardHat,UniversalHeroCosmetic.PropellerCap,UniversalHeroCosmetic.MushroomHat};
    static void NerokSkullChecks()
    {
        int commits=ProfileTransaction.Commits.Count;
        var plain=HeadFixture(false).GetComponent<HeadwearPresentation>();
        var finActor=HeadFixture(true);var fitted=finActor.GetComponent<HeadwearPresentation>();
        Mesh anatomy=finActor.GetComponentsInChildren<SkinnedMeshRenderer>()[0].sharedMesh;
        var vertices=(Vector3[])anatomy.vertices.Clone();var triangles=anatomy.GetTriangles(0).ToArray();
        Check(anatomy.bounds.max.y>2.07f,"Fin fixture does not reproduce the high original contact surface.");
        foreach(var hat in AllHats)
        {
            Check(plain.Apply(hat)&&fitted.Apply(hat),"Nerok hat failed to fit: "+fitted.LastFitError);
            Check(Math.Abs(fitted.HeadBounds.max.y-1.95f)<.00001f,"The fin is still measured as Nerok's skull apex.");
            Check(fitted.HeadBounds.size==plain.HeadBounds.size&&fitted.CrownSection.center==plain.CrownSection.center,
                "A detached fin/flat-shading seam changed Nerok's true head measurements.");
            string name="DF_Headwear_"+HeadwearSelection.ResourceFor(hat);
            var expected=plain.Head.Find(name).GetComponent<MeshFilter>().sharedMesh;
            var actual=fitted.Head.Find(name).GetComponent<MeshFilter>().sharedMesh;
            Check(actual.vertices.SequenceEqual(expected.vertices)&&fitted.SeatInHeadSpace==plain.SeatInHeadSpace,
                "Nerok's hat width/seat is still determined by the narrow fin.");
            fitted.Apply(UniversalHeroCosmetic.Original);
            fitted.Head.localToWorldMatrix=Matrix4x4.TRS(new Vector3(.3f,.2f,-.1f),Quaternion.identity,new Vector3(.9f,1.1f,.8f));
            fitted.Head.worldToLocalMatrix=fitted.Head.localToWorldMatrix.inverse;
            Check(fitted.Apply(hat)&&fitted.Head.Find(name).GetComponent<MeshFilter>().sharedMesh.vertices.SequenceEqual(expected.vertices),
                "A late Nerok equip baked the currently animated head pose into the hat.");
            Check(anatomy.vertices.SequenceEqual(vertices)&&anatomy.GetTriangles(0).SequenceEqual(triangles),
                "Measuring the skull modified Nerok's visible head/fin geometry.");
        }
        Check(ProfileTransaction.Commits.Count==commits,"Nerok's fit correction changed ownership or progression.");
        // The same detached geometry on a different model must retain the old fit.
        var unrelated=HeadFixture(true,HeroId.Bolt7).GetComponent<HeadwearPresentation>();
        Check(unrelated.Apply(UniversalHeroCosmetic.MushroomHat)&&unrelated.HeadBounds.max.y>2.07f,
            "The Nerok-only contact correction changed an unrelated hero.");
    }
    static void BoltHeadBindChecks()
    {
        var actor=HeadFixture(false,HeroId.Bolt7);var view=actor.GetComponent<HeadwearPresentation>();
        var skin=actor.GetComponentsInChildren<SkinnedMeshRenderer>()[0];
        // BOLT's authored rest geometry is measured in the visual-root frame and
        // attached through its actual Head bind matrix, including the model scale.
        skin.transform.localToWorldMatrix=Matrix4x4.TRS(new Vector3(.4f,-.98f,0),Quaternion.identity,new Vector3(.85f,.85f,.85f));
        skin.transform.worldToLocalMatrix=skin.transform.localToWorldMatrix.inverse;
        skin.sharedMesh.bindposes[1]=Matrix4x4.TRS(new Vector3(0,-1.757f,0),Quaternion.identity,Vector3.one);
        HeadwearPresentation.Configure(actor,actor.transform.Find("DF_VisualRoot"),HeroId.Bolt7,new SkinPreviewSelection{Headwear=UniversalHeroCosmetic.MushroomHat});
        Check(view.Current==UniversalHeroCosmetic.MushroomHat,"BOLT's selected preview hat did not use its authored Head bind frame.");
        foreach(var hat in AllHats)
        {
            Check(view.Apply(hat)&&view.Head==skin.bones[1],"BOLT hat did not attach to the actual animated Head bone.");
            string name="DF_Headwear_"+HeadwearSelection.ResourceFor(hat);
            var expected=view.Head.Find(name).GetComponent<MeshFilter>().sharedMesh.vertices.ToArray();
            view.Apply(UniversalHeroCosmetic.Original);
            view.Head.localToWorldMatrix=Matrix4x4.TRS(new Vector3(2,1.5f,-3),Quaternion.identity,new Vector3(.7f,1.2f,.9f));
            view.Head.worldToLocalMatrix=view.Head.localToWorldMatrix.inverse;
            Check(view.Apply(hat)&&view.Head.Find(name).GetComponent<MeshFilter>().sharedMesh.vertices.SequenceEqual(expected),
                "Equipping BOLT's hat after animation/network delivery changed its bind-space fit.");
            Check(view.Apply(UniversalHeroCosmetic.Original)&&view.Head.Find(name)==null,"BOLT unequip left an old hat on its animated head.");
        }
    }
    static void HatSizeChecks()
    {
        // Recorded before the size corrections. The UFO must grow exactly 25%
        // from its one-third version. Relative to that shared width, the wizard
        // is 25% larger and the propeller/mushroom hats are 25% smaller.
        var originalSizes=new Dictionary<UniversalHeroCosmetic,Vector3>
        {
            {UniversalHeroCosmetic.BetaCap,new Vector3(.6413307f,.25305986f,.9218707f)},
            {UniversalHeroCosmetic.UfoTestHat,new Vector3(.97954357f,.3726163f,.97954357f)},
            {UniversalHeroCosmetic.WizardHat,new Vector3(1.2146885f,.6547725f,1.2146884f)},
            {UniversalHeroCosmetic.PropellerCap,new Vector3(.75265193f,.34898663f,.9395902f)},
            {UniversalHeroCosmetic.MushroomHat,new Vector3(.7415408f,.19938469f,.7415408f)}
        };
        foreach(bool authored in new[]{false,true})
        foreach(var entry in originalSizes)
        {
            var view=Actor(authored).GetComponent<HeadwearPresentation>();
            Check(view.Apply(entry.Key),"Hat size check failed to attach: "+view.LastFitError);
            var model=view.Head.Find("DF_Headwear_"+HeadwearSelection.ResourceFor(entry.Key));
            Bounds actual=model.GetComponent<MeshFilter>().sharedMesh.bounds;
            bool ufo=entry.Key==UniversalHeroCosmetic.UfoTestHat;
            float referenceWidth=originalSizes[UniversalHeroCosmetic.UfoTestHat].x/3f*1.25f*1.15f;
            if(entry.Key==UniversalHeroCosmetic.WizardHat)referenceWidth*=1.25f;
            if(entry.Key==UniversalHeroCosmetic.PropellerCap||entry.Key==UniversalHeroCosmetic.MushroomHat)referenceWidth*=.75f;
            Vector3 expected=entry.Value*(referenceWidth/entry.Value.x);
            Check(Math.Abs(actual.size.x-expected.x)<.00001f&&
                Math.Abs(actual.size.y-expected.y)<.00001f&&Math.Abs(actual.size.z-expected.z)<.00001f,
                ufo?"UFO reference size changed.":"Hat does not match its requested size adjustment or its previous proportions changed.");
            float expectedSeat=1.25f-.1f*(referenceWidth/entry.Value.x);
            Check(Math.Abs(view.SeatInHeadSpace.y-expectedSeat)<.00001f,
                "Hat size correction lost the crown contact position.");
            Check(actual.min.y<view.HeadBounds.max.y&&actual.max.y>view.HeadBounds.max.y,
                "Resized hat sits entirely above or below the crown.");
        }
    }
    static void MergedHaloChecks()
    {
        var actor=Actor(true);var view=actor.GetComponent<HeadwearPresentation>();
        var skin=actor.GetComponentsInChildren<SkinnedMeshRenderer>()[0];
        Mesh source=skin.sharedMesh;
        var halo=Cube(new Vector3(0,1.70f,0),new Vector3(.70f,.06f,.70f));
        var points=new List<Vector3>(source.vertices);var weights=new List<BoneWeight>(source.boneWeights);
        int offset=points.Count;
        points.AddRange(halo.vertices);
        foreach(var point in halo.vertices)weights.Add(new BoneWeight{boneIndex0=1,weight0=1});
        source.vertices=points.ToArray();source.boneWeights=weights.ToArray();source.subMeshCount=2;
        source.SetTriangles(halo.triangles.Select(i=>i+offset).ToList(),1);
        // TahlaHalo uses HeadAdornmentSkin.Merge: the halo shares DF_Anatomy and
        // the real head weights. Skin_FineOrnaments is real skin, not floating decor.
        skin.sharedMaterials=new[]{new Material(Shader.Find("fixture")){name="Skin_FineOrnaments"},
            new Material(Shader.Find("fixture")){name="DF_TahlaHaloGlow"}};
        foreach(var id in new[]{UniversalHeroCosmetic.BetaCap,UniversalHeroCosmetic.UfoTestHat,UniversalHeroCosmetic.WizardHat,UniversalHeroCosmetic.PropellerCap,UniversalHeroCosmetic.MushroomHat})
        {
            var baseline=Actor(true).GetComponent<HeadwearPresentation>();
            Check(baseline.Apply(id)&&view.Apply(id),"Hat failed on the merged-halo fixture.");
            Check(Math.Abs(view.HeadBounds.max.y-1.25f)<.00001f&&view.HeadBounds.size==baseline.HeadBounds.size,
                "Merged halo was measured as skull geometry, or real ornament-shaded skin was discarded.");
            Check(view.SeatInHeadSpace==baseline.SeatInHeadSpace,"Merged halo lifted the hat off the actual crown.");
            var a=view.Head.Find("DF_Headwear_"+HeadwearSelection.ResourceFor(id)).GetComponent<MeshFilter>().sharedMesh;
            var b=baseline.Head.Find("DF_Headwear_"+HeadwearSelection.ResourceFor(id)).GetComponent<MeshFilter>().sharedMesh;
            Check(a.vertices.SequenceEqual(b.vertices),"Merged halo changed the hat size or attachment.");
            view.Apply(UniversalHeroCosmetic.Original);
        }
        // A real rigid helmet must still define the top surface beneath every hat.
        var helmet=new GameObject("DF_Tahla_GoldenHelmet");helmet.transform.SetParent(view.Head,false);
        helmet.AddComponent<MeshFilter>().sharedMesh=Cube(new Vector3(0,1.35f,0),new Vector3(.42f,.20f,.44f));
        helmet.AddComponent<MeshRenderer>();
        Check(view.Apply(UniversalHeroCosmetic.UfoTestHat)&&Math.Abs(view.HeadBounds.max.y-1.45f)<.00001f,
            "The halo exclusion also discarded Tahla's real helmet.");
        float helmetWidth=view.Head.Find("DF_Headwear_UfoTestHat").GetComponent<MeshFilter>().sharedMesh.bounds.size.x;
        foreach(var id in new[]{UniversalHeroCosmetic.BetaCap,UniversalHeroCosmetic.WizardHat,UniversalHeroCosmetic.PropellerCap,UniversalHeroCosmetic.MushroomHat})
        {
            Check(view.Apply(id),"Hat failed on the helmet plus merged-halo fixture.");
            float width=view.Head.Find("DF_Headwear_"+HeadwearSelection.ResourceFor(id)).GetComponent<MeshFilter>().sharedMesh.bounds.size.x;
            float target=helmetWidth;
            if(id==UniversalHeroCosmetic.WizardHat)target*=1.25f;
            if(id==UniversalHeroCosmetic.PropellerCap||id==UniversalHeroCosmetic.MushroomHat)target*=.75f;
            Check(Math.Abs(width-target)<.00001f,"Hat size adjustment failed on a differently shaped, helmeted head.");
        }
        Check(skin.sharedMesh==source&&source.GetTriangles(1).Length>0&&skin.enabled,
            "Headwear fitting removed or hid Tahla's halo instead of excluding it from measurement.");
    }
    static void HatVisibilityChecks()
    {
        var actor=Actor(true);var view=actor.GetComponent<HeadwearPresentation>();
        Check(view.Apply(UniversalHeroCosmetic.WizardHat)&&Math.Abs(view.HeadBounds.max.y-1.25f)<.0001f,"Removed hair vertices influenced head top.");
        view.Apply(UniversalHeroCosmetic.Original);
        var helmet=new GameObject("DF_Tahla_GoldenHelmet");helmet.transform.SetParent(view.Head,false);
        helmet.AddComponent<MeshFilter>().sharedMesh=Cube(new Vector3(0,1.35f,0),new Vector3(.42f,.20f,.44f));
        helmet.AddComponent<MeshRenderer>();
        var halo=new GameObject("DF_TahlaHalo");halo.transform.SetParent(view.Head,false);
        halo.AddComponent<MeshFilter>().sharedMesh=Cube(new Vector3(0,9,0),new Vector3(1,1,1));halo.AddComponent<MeshRenderer>();
        Check(view.Apply(UniversalHeroCosmetic.WizardHat)&&Math.Abs(view.HeadBounds.max.y-1.45f)<.0001f,"A rigid helmet was ignored or a floating halo set the hat seat.");
        view.Apply(UniversalHeroCosmetic.Original);helmet.GetComponent<MeshRenderer>().enabled=false;
        Check(view.Apply(UniversalHeroCosmetic.MushroomHat)&&Math.Abs(view.HeadBounds.max.y-1.25f)<.0001f,"A hidden helmet influenced the hat seat.");
        view.Apply(UniversalHeroCosmetic.Original);helmet.GetComponent<MeshRenderer>().enabled=true;helmet.SetActive(false);
        Check(view.Apply(UniversalHeroCosmetic.PropellerCap)&&Math.Abs(view.HeadBounds.max.y-1.25f)<.0001f,"An inactive helmet influenced the hat seat.");
        view.Apply(UniversalHeroCosmetic.Original);
        actor.GetComponentsInChildren<SkinnedMeshRenderer>()[0].enabled=false;
        Check(!view.Apply(UniversalHeroCosmetic.BetaCap),"Hidden anatomy or invisible pose controls were used for attachment.");
    }
    static void HatIconChecks()
    {
        var rect=new Rect(0,0,240,120);
        string mushroom="Headwear/MushroomHatIcon";Time.unscaledTime=0;Resources.MissingTextures.Add(mushroom);
        int before=Resources.Loads.GetValueOrDefault(mushroom);
        Alterverse.UI.HeadwearIconRenderer.Draw(rect,UniversalHeroCosmetic.MushroomHat);
        Alterverse.UI.HeadwearIconRenderer.Draw(rect,UniversalHeroCosmetic.MushroomHat);
        Time.unscaledTime=.5f;Alterverse.UI.HeadwearIconRenderer.Draw(rect,UniversalHeroCosmetic.MushroomHat);
        Check(Resources.Loads.GetValueOrDefault(mushroom)==before+1,"Missing hat art retried every GUI event.");
        Resources.MissingTextures.Remove(mushroom);Time.unscaledTime=1.1f;
        Alterverse.UI.HeadwearIconRenderer.Draw(rect,UniversalHeroCosmetic.MushroomHat);
        Check(Resources.Loads.GetValueOrDefault(mushroom)==before+2&&GUI.Draws.Last().Texture.Resource==mushroom,"A late thumbnail import never recovered.");
        foreach(var id in new[]{UniversalHeroCosmetic.BetaCap,UniversalHeroCosmetic.UfoTestHat,UniversalHeroCosmetic.WizardHat,UniversalHeroCosmetic.PropellerCap,UniversalHeroCosmetic.MushroomHat})
        {
            string path="Headwear/"+HeadwearSelection.ResourceFor(id)+"Icon";
            Alterverse.UI.HeadwearIconRenderer.Draw(rect,id);
            Check(GUI.Draws.Last().Texture.Resource==path&&GUI.Draws.Last().Scale==ScaleMode.ScaleToFit&&GUI.Draws.Last().Alpha,"Hat art was incorrect, stretched or lost transparency.");
            int loads=Resources.Loads[path];
            Alterverse.UI.HeadwearIconRenderer.Draw(rect,id);
            Check(Resources.Loads[path]==loads,"Successful hat art was reloaded.");
        }
        int draws=GUI.Draws.Count;Alterverse.UI.HeadwearIconRenderer.Draw(rect,UniversalHeroCosmetic.Voidfall);
        Check(GUI.Draws.Count==draws,"A wing was incorrectly treated as a hat.");
    }
    static void ModernNetworkChecks()
    {
        Reset(1,false);
        AccountProfilePrefs.SetInt(CosmeticCatalog.Find(UniversalHeroCosmetic.MushroomHat).OwnedKey,1);
        AccountProfilePrefs.SetInt(CosmeticCatalog.Find(UniversalHeroCosmetic.PropellerCap).OwnedKey,1);
        HeadwearSelection.EquipFor(HeroId.Rook,UniversalHeroCosmetic.MushroomHat);
        int commits=ProfileTransaction.Commits.Count;
        var manager=new Unity.Netcode.NetworkManager{IsServer=true};Unity.Netcode.NetworkManager.Singleton=manager;
        Alterverse.Multiplayer.CoopAuthority.LocalHasServerAuthority=true;Time.unscaledTime=0;
        var server=new Alterverse.Multiplayer.CoopHeroStateSync();server.Initialize();server.Put(0,Actor(),true);server.Put(1,Actor());
        uint round=Alterverse.Multiplayer.CoopPlanHandshake.Round;
        object[] Packet(int id)=>new object[]{round,1,new Unity.Collections.FixedString128Bytes("player"),id};
        server.RequestV2(8,Packet(9));server.RequestV2(7,Packet(999));
        Check(server.Messages.Sent.Count==0,"V2 bypassed owner/ID validation.");
        foreach(int id in new[]{9,10,11})
        {
            server.RequestV2(7,Packet(id));
            Check(server.Value(1)==(UniversalHeroCosmetic)id&&server.Messages.Sent.Last().Name=="DF_HEADWEAR_STATE_V2","New hat was not relayed through V2.");
            Check((int)server.Messages.Sent.Last().Values[5]==id,"Modern state lost a new hat ID.");
        }
        int sent=server.Messages.Sent.Count;server.Request(7,Packet(0));
        Check(server.Value(1)==UniversalHeroCosmetic.MushroomHat&&server.Messages.Sent.Count==sent,"Delayed V1 fallback erased a negotiated V2 hat.");
        // A legacy peer must keep seeing its BETA cap while a new host wears a newer hat.
        Alterverse.Multiplayer.CoopPlanHandshake.Round++;round=Alterverse.Multiplayer.CoopPlanHandshake.Round;
        Time.unscaledTime=20;server.Tick(manager);server.Request(7,Packet(7));
        var legacy=server.Messages.Sent.Last();
        Check(legacy.Name=="DF_HEADWEAR_STATE_V1"&&(int)legacy.Values[3]==0&&(int)legacy.Values[5]==7,"Legacy peer received unsupported new IDs or lost a known cap.");
        server.RequestV2(7,Packet(10));
        Check(server.Messages.Sent.Last().Name=="DF_HEADWEAR_STATE_V2"&&(int)server.Messages.Sent.Last().Values[3]==11,"Capability upgrade did not restore the host's new hat.");
        server.Stop();Check(server.Messages.Handlers.Count==0&&ProfileTransaction.Commits.Count==commits,"V2 leaked handlers or changed account ownership.");
        manager.IsServer=false;Alterverse.Multiplayer.CoopAuthority.LocalHasServerAuthority=false;
        HeadwearSelection.EquipFor(HeroId.Rook,UniversalHeroCosmetic.PropellerCap);commits=ProfileTransaction.Commits.Count;
        var local=Actor();local.GetComponent<HeadwearPresentation>().Apply(UniversalHeroCosmetic.PropellerCap);
        var remote=Actor();var client=new Alterverse.Multiplayer.CoopHeroStateSync();client.Initialize();client.Put(0,local,true);client.Put(1,remote);
        Time.unscaledTime=0;client.Tick(manager);
        Check(client.Messages.Sent.Any(x=>x.Name=="DF_HEADWEAR_REQUEST_V1"&&(int)x.Values[3]==0)&&client.Messages.Sent.Any(x=>x.Name=="DF_HEADWEAR_REQUEST_V2"&&(int)x.Values[3]==10),"New client did not safely negotiate with old/new hosts.");
        client.StateV2(7,StatePacket(9));client.StateV2(0,StatePacket(999));
        Check(remote.GetComponent<HeadwearPresentation>().Current==UniversalHeroCosmetic.Original,"Invalid V2 packet partially applied.");
        client.StateV2(0,StatePacket(9));client.State(0,StatePacket());
        Check(remote.GetComponent<HeadwearPresentation>().Current==UniversalHeroCosmetic.WizardHat,"Modern hat was not rendered or a stale V1 reply erased it.");
        Check(local.GetComponent<HeadwearPresentation>().Current==UniversalHeroCosmetic.PropellerCap,"Remote snapshot replaced the local equipped hat.");
        int legacyRequests=client.Messages.Sent.Count(x=>x.Name=="DF_HEADWEAR_REQUEST_V1");
        Time.unscaledTime=2;client.Tick(manager);
        Check(client.Messages.Sent.Last().Name=="DF_HEADWEAR_REQUEST_V2"&&client.Messages.Sent.Count(x=>x.Name=="DF_HEADWEAR_REQUEST_V1")==legacyRequests,"Negotiated modern client kept duplicating legacy requests.");
        var duplicate=StatePacket(11);duplicate[^2]=1;client.StateV2(0,duplicate);
        Check(remote.GetComponent<HeadwearPresentation>().Current==UniversalHeroCosmetic.WizardHat,"Duplicate-slot V2 packet partially applied.");
        Alterverse.Multiplayer.CoopPlanHandshake.Round++;Time.unscaledTime=4;client.Tick(manager);
        client.State(0,StatePacket(7));
        Check(remote.GetComponent<HeadwearPresentation>().Current==UniversalHeroCosmetic.BetaCap,"New round retained old protocol capability state.");
        client.Stop();Check(client.Messages.Handlers.Count==0&&ProfileTransaction.Commits.Count==commits,"V2 renderer wrote to the account or leaked handlers.");
    }
    static void ShopHatChecks()
    {
        using(var shared=JsonDocument.Parse(System.IO.File.ReadAllText("Assets/Alterverse/Resources/Commerce/ShardCatalog.json")))
        {
            var items=shared.RootElement.GetProperty("cosmetics").EnumerateArray().ToArray();
            var skus=new HashSet<string>();int hats=0;
            foreach(var definition in CosmeticCatalog.All.Where(x=>x.Headwear&&x.Purchasable))
            {
                hats++;string sku=CosmeticCatalog.Sku(definition.Id);
                Check(!string.IsNullOrEmpty(sku)&&skus.Add(sku),"Shop hats share an empty or duplicate purchase SKU.");
                Check(definition.Name.IndexOf("test",StringComparison.OrdinalIgnoreCase)<0&&definition.Description.IndexOf("test",StringComparison.OrdinalIgnoreCase)<0,
                    "Prototype name or description is exposed in the shop.");
                var matches=items.Where(x=>x.GetProperty("id").GetString()==sku).ToArray();
                Check(matches.Length==1&&matches[0].GetProperty("price").GetInt32()==definition.Price,"Client/server hat prices or SKUs differ.");
                var writes=matches[0].GetProperty("writes").EnumerateArray().ToArray();
                Check(writes.Length==1&&writes[0].GetProperty("key").GetString()==definition.OwnedKey&&
                    writes[0].GetProperty("number").GetInt32()==1,"Client/server hat ownership keys differ.");
            }
            Check(hats==4,"Not all four hats are in the Skins shop.");
            Check(!items.Any(x=>x.TryGetProperty("writes",out var w)&&w.EnumerateArray().Any(e=>e.GetProperty("key").GetString()==HeadwearSelection.BetaCapOwnedKey)),"BETA cap leaked into the shard shop.");
        }
        Reset(1,false);int commits=ProfileTransaction.Commits.Count;
        foreach(var id in new[]{UniversalHeroCosmetic.WizardHat,UniversalHeroCosmetic.PropellerCap,UniversalHeroCosmetic.MushroomHat})
        {
            Reset(1,false);
            string resource=HeadwearSelection.ResourceFor(id);
            var definition=CosmeticCatalog.Find(id);
            Check(definition!=null&&definition.Headwear&&definition.Purchasable&&definition.Category==CosmeticCategory.Skins&&
                definition.Price==100&&definition.PriceLabel=="100 SHARDS"&&!CosmeticCatalog.IsOwned(id),"Shop hat has a wrong price/category or is still automatically free.");
            Check(!string.IsNullOrEmpty(CosmeticCatalog.Sku(id))&&definition.HeadwearResource==resource,"Shop hat has no purchase SKU or art mapping.");
            Check(!HeadwearSelection.EquipFor(HeroId.Rook,id),"Unpurchased shop hat can be equipped.");
            AccountProfilePrefs.SetInt("DF_Shards",99);
            Check(CosmeticCatalog.Purchase(id)==AvatarPurchaseResult.InsufficientShards&&PlayerWallet.Balance==99&&!CosmeticCatalog.IsOwned(id),"Underfunded purchase debited or granted a shop hat.");
            AccountProfilePrefs.SetInt("DF_Shards",100);
            Check(CosmeticCatalog.Purchase(id)==AvatarPurchaseResult.Purchased&&CosmeticCatalog.IsOwned(id)&&PlayerWallet.Balance==0,"100-shard hat purchase failed.");
            Check(CosmeticCatalog.Purchase(id)==AvatarPurchaseResult.AlreadyOwned&&PlayerWallet.Balance==0,"Shop hat charged twice.");
            Check(HeadwearSelection.EquippedFor(HeroId.Rook)==UniversalHeroCosmetic.Original,"Shop purchase silently equipped the hat.");
            commits=ProfileTransaction.Commits.Count;
            Check(UfoHatMesh.TryBuildResource(resource,Matrix4x4.identity,Shader.Find("fixture"),out Mesh mesh,out Material[] materials,out string error),resource+": "+error);
            Check(mesh.vertexCount>0&&mesh.vertexCount<6000&&mesh.subMeshCount==6&&materials.Length==6,"Prototype mesh/material budget invalid.");
            foreach(bool authored in new[]{false,true})
            {
                var view=Actor(authored).GetComponent<HeadwearPresentation>();
                Check(view.Apply(id)&&view.Current==id&&view.Head.Find("DF_Headwear_"+resource)!=null,"Test hat failed production head attachment.");
                Check(view.SeatInHeadSpace.y>view.HeadBounds.center.y&&view.SeatInHeadSpace.y<view.HeadBounds.max.y,
                    "The head does not enter the test hat opening.");
                var fitted=view.Head.Find("DF_Headwear_"+resource).GetComponent<MeshFilter>().sharedMesh;
                Check(fitted.bounds.min.y<view.HeadBounds.max.y&&fitted.bounds.max.y>view.HeadBounds.max.y,"Hat geometry sits entirely above or below the crown.");
                int count=view.gameObject.GetComponentsInChildren<MeshFilter>().Length;
                Check(view.Apply(id)&&view.gameObject.GetComponentsInChildren<MeshFilter>().Length==count,"Repeated test hat equip duplicated geometry.");
                Check(view.Apply(UniversalHeroCosmetic.Original)&&view.Head.Find("DF_Headwear_"+resource)==null,"Unequip retained test hat geometry.");
            }
            Check(Alterverse.Match.WingMatchRewardBonus.Percent(id)==0,"A test hat grants a progression bonus.");
            Check(ProfileTransaction.Commits.Count==commits,"Previewing the test hat changed the profile.");
            AccountProfilePrefs.SetInt(UniversalSkin.OwnedKey,1);
            for(int i=0;i<HeroIdentity.HeroCount;i++)
            {
                var hero=(HeroId)i;UniversalCosmeticSelection.EquipFor(hero,UniversalHeroCosmetic.Voidfall);
                Check(HeadwearSelection.EquipFor(hero,id)&&HeadwearSelection.EquippedFor(hero)==id,"Purchased hat failed per-hero equip.");
                Check(UniversalCosmeticSelection.EquippedFor(hero)==UniversalHeroCosmetic.Voidfall,"Purchased hat displaced the wing slot.");
            }
            Check(HeadwearSelection.EquipFor(HeroId.Veyr,UniversalHeroCosmetic.Original)&&HeadwearSelection.EquippedFor(HeroId.Rook)==id&&
                UniversalCosmeticSelection.EquippedFor(HeroId.Veyr)==UniversalHeroCosmetic.Voidfall,"Unequipping a test hat affected another hero or removed wings.");
            string saved=JsonSerializer.Serialize(AccountProfilePrefs.Ints);
            AccountProfilePrefs.Ints=JsonSerializer.Deserialize<Dictionary<string,int>>(saved);
            Check(HeadwearSelection.EquippedFor(HeroId.Rook)==id,"New test hat did not survive save/reload.");
            Check(PlayerWallet.Balance==0,"Equipping a purchased hat changed shards.");
            commits=ProfileTransaction.Commits.Count;
        }
        Check(ProfileTransaction.Commits.Count==commits,"Prototype preview changed the account.");
        Check(!Actor().GetComponent<HeadwearPresentation>().ApplyPrototype("../OtherAsset"),"Unknown prototype path accepted.");
        Check(!UfoHatMesh.TryBuildResource("../OtherAsset",Matrix4x4.identity,Shader.Find("fixture"),out _,out _,out _),"Unknown serialized mesh path accepted.");
        Reset(1,false);
        Check(!CosmeticCatalog.IsOwned(UniversalHeroCosmetic.BetaCap)&&!HeadwearSelection.EquipFor(HeroId.Rook,UniversalHeroCosmetic.BetaCap),"Shop hats bypassed the premium Battle Pass reward.");
        Check(!CosmeticCatalog.IsOwned(UniversalHeroCosmetic.UfoTestHat),"Another profile inherited UFO ownership.");
        foreach(var id in new[]{UniversalHeroCosmetic.WizardHat,UniversalHeroCosmetic.PropellerCap,UniversalHeroCosmetic.MushroomHat})
            Check(!CosmeticCatalog.IsOwned(id)&&HeadwearSelection.EquippedFor(HeroId.Rook)==UniversalHeroCosmetic.Original,"Another profile inherited a purchased hat.");
    }
}


