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
            Check(Math.Abs(view.SeatInHeadSpace.y-1.15f)<.0001f,"The head does not enter the hat opening at the upper crown.");
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
        AttachmentChecks();NetworkChecks();UfoChecks();FreeHatChecks();HatVisibilityChecks();HatSizeChecks();HatIconChecks();ModernNetworkChecks();
        Console.WriteLine("PASS "+checks+" headwear, battle-pass, persistence, mesh and message checks; Unity fit/device/live multiplayer validation remains required.");
    }
    static void UfoChecks()
    {
        Reset(1,false);
        var item=CosmeticCatalog.Find(UniversalHeroCosmetic.UfoTestHat);
        Check(item.Headwear&&item.Purchasable&&item.Price==1&&item.PriceLabel=="1 SHARD","UFO is not the requested 1-shard shop item.");
        Check(CosmeticCatalog.Sku(item.Id)=="ufo_test_hat","UFO client/server SKU mismatch.");
        Check(CosmeticCatalog.Purchase(item.Id)==AvatarPurchaseResult.InsufficientShards&&!CosmeticCatalog.IsOwned(item.Id),"Zero-shard account received the UFO.");
        AccountProfilePrefs.SetInt("DF_Shards",1);AccountProfilePrefs.SetInt("DF_AccountXP",777);
        Check(CosmeticCatalog.Purchase(item.Id)==AvatarPurchaseResult.Purchased,"Exact 1-shard purchase failed.");
        Check(PlayerWallet.Balance==0&&CosmeticCatalog.IsOwned(item.Id),"Purchase did not charge exactly one shard and grant ownership.");
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
    static void HatSizeChecks()
    {
        // Recorded on the same synthetic head before the UFO size correction. Check
        // the produced mesh dimensions, including its rim, antennae and visor, so an
        // automatic fit cannot silently undo the requested one-third reduction.
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
            Vector3 expected=entry.Value*(ufo?1f/3f:1f);
            Check(Math.Abs(actual.size.x-expected.x)<.00001f&&
                Math.Abs(actual.size.y-expected.y)<.00001f&&Math.Abs(actual.size.z-expected.z)<.00001f,
                ufo?"UFO is not one third of its previous size on every axis.":"UFO correction changed another hat's size.");
            float expectedSeat=ufo?1.2166667f:1.15f;
            Check(Math.Abs(view.SeatInHeadSpace.y-expectedSeat)<.00001f,
                "Hat size correction lost the crown contact position.");
            Check(actual.min.y<view.HeadBounds.max.y&&actual.max.y>view.HeadBounds.max.y,
                "Resized hat sits entirely above or below the crown.");
        }
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
        Check(view.Apply(UniversalHeroCosmetic.WizardHat)&&Math.Abs(view.SeatInHeadSpace.y-1.31f)<.0001f,"A rigid helmet was ignored or a floating halo set the hat seat.");
        view.Apply(UniversalHeroCosmetic.Original);helmet.GetComponent<MeshRenderer>().enabled=false;
        Check(view.Apply(UniversalHeroCosmetic.MushroomHat)&&Math.Abs(view.SeatInHeadSpace.y-1.15f)<.0001f,"A hidden helmet influenced the hat seat.");
        view.Apply(UniversalHeroCosmetic.Original);helmet.GetComponent<MeshRenderer>().enabled=true;helmet.SetActive(false);
        Check(view.Apply(UniversalHeroCosmetic.PropellerCap)&&Math.Abs(view.SeatInHeadSpace.y-1.15f)<.0001f,"An inactive helmet influenced the hat seat.");
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
        Reset(1,false);HeadwearSelection.EquipFor(HeroId.Rook,UniversalHeroCosmetic.MushroomHat);
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
    static void FreeHatChecks()
    {
        Reset(1,false);int commits=ProfileTransaction.Commits.Count;
        foreach(var id in new[]{UniversalHeroCosmetic.WizardHat,UniversalHeroCosmetic.PropellerCap,UniversalHeroCosmetic.MushroomHat})
        {
            string resource=HeadwearSelection.ResourceFor(id);
            var definition=CosmeticCatalog.Find(id);
            Check(definition!=null&&definition.Headwear&&!definition.Purchasable&&definition.Price==0&&CosmeticCatalog.IsOwned(id),"Free test hat is missing from inventory.");
            Check(CosmeticCatalog.Purchase(id)==AvatarPurchaseResult.AlreadyOwned,"Free test hat attempted a shard purchase.");
            Check(UfoHatMesh.TryBuildResource(resource,Matrix4x4.identity,Shader.Find("fixture"),out Mesh mesh,out Material[] materials,out string error),resource+": "+error);
            Check(mesh.vertexCount>0&&mesh.vertexCount<6000&&mesh.subMeshCount==6&&materials.Length==6,"Prototype mesh/material budget invalid.");
            foreach(bool authored in new[]{false,true})
            {
                var view=Actor(authored).GetComponent<HeadwearPresentation>();
                Check(view.Apply(id)&&view.Current==id&&view.Head.Find("DF_Headwear_"+resource)!=null,"Test hat failed production head attachment.");
                Check(Math.Abs(view.SeatInHeadSpace.y-1.15f)<.0001f,"The head does not enter the test hat opening.");
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
                Check(HeadwearSelection.EquipFor(hero,id)&&HeadwearSelection.EquippedFor(hero)==id,"Free hat failed per-hero equip.");
                Check(UniversalCosmeticSelection.EquippedFor(hero)==UniversalHeroCosmetic.Voidfall,"Free hat displaced the wing slot.");
            }
            Check(HeadwearSelection.EquipFor(HeroId.Veyr,UniversalHeroCosmetic.Original)&&HeadwearSelection.EquippedFor(HeroId.Rook)==id&&
                UniversalCosmeticSelection.EquippedFor(HeroId.Veyr)==UniversalHeroCosmetic.Voidfall,"Unequipping a test hat affected another hero or removed wings.");
            string saved=JsonSerializer.Serialize(AccountProfilePrefs.Ints);
            AccountProfilePrefs.Ints=JsonSerializer.Deserialize<Dictionary<string,int>>(saved);
            Check(HeadwearSelection.EquippedFor(HeroId.Rook)==id,"New test hat did not survive save/reload.");
            Check(PlayerWallet.Balance==0,"Equipping a free test hat changed shards.");
            commits=ProfileTransaction.Commits.Count;
        }
        Check(ProfileTransaction.Commits.Count==commits,"Prototype preview changed the account.");
        Check(!Actor().GetComponent<HeadwearPresentation>().ApplyPrototype("../OtherAsset"),"Unknown prototype path accepted.");
        Check(!UfoHatMesh.TryBuildResource("../OtherAsset",Matrix4x4.identity,Shader.Find("fixture"),out _,out _,out _),"Unknown serialized mesh path accepted.");
        Reset(1,false);
        Check(!CosmeticCatalog.IsOwned(UniversalHeroCosmetic.BetaCap)&&!HeadwearSelection.EquipFor(HeroId.Rook,UniversalHeroCosmetic.BetaCap),"Free test hats bypassed the premium Battle Pass reward.");
        Check(!CosmeticCatalog.IsOwned(UniversalHeroCosmetic.UfoTestHat),"Free test hats granted the paid UFO.");
        foreach(var id in new[]{UniversalHeroCosmetic.WizardHat,UniversalHeroCosmetic.PropellerCap,UniversalHeroCosmetic.MushroomHat})
            Check(CosmeticCatalog.IsOwned(id)&&HeadwearSelection.EquippedFor(HeroId.Rook)==UniversalHeroCosmetic.Original,"Another profile inherited an equipped hat.");
    }
}


