using System;
using System.Linq;
using UnityEngine;
using Alterverse.Characters;
using Alterverse.Profile;
using Alterverse.Shop;
using Alterverse.Visuals;
static class Regression{
static int checks;static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;}
static void Reset(int funds){AccountProfilePrefs.Values.Clear();PlayerWallet.EarnedBalance=funds;PlayerWallet.Requests=ProfileTransaction.Commits=0;PaidShardCommerce.Instance=null;}
static void Main(){
WeaponColorwayChecks.Run();
Check(WeaponSkinCatalog.VariantCount(HeroId.Bolt7)==2,"two BOLT-7 variants");
foreach(int variant in new[]{0,1}){var item=WeaponSkinCatalog.For(HeroId.Bolt7,variant);Check(item.Hero==HeroId.Bolt7&&item.Variant==variant,"catalog does not fall back to Zhaev");Check(item.Price==500&&item.WeaponName.Contains("Gatling")&&item.WeaponName.Contains("Launcher"),"correct weapon label and established price");
Reset(499);Check(WeaponSkinInventory.Purchase(HeroId.Bolt7,variant)==AvatarPurchaseResult.InsufficientShards&&PlayerWallet.EarnedBalance==499&&ProfileTransaction.Commits==0,"insufficient balance preserves wallet");
Reset(500);Check(WeaponSkinInventory.Purchase(HeroId.Bolt7,variant)==AvatarPurchaseResult.Purchased,"exact price buys weapon");Check(PlayerWallet.EarnedBalance==0&&WeaponSkinInventory.Owned(HeroId.Bolt7,variant)&&WeaponSkinInventory.EquippedVariant(HeroId.Bolt7)==variant,"ownership and selection persist on BOLT-7 keys");
int commits=ProfileTransaction.Commits;Check(WeaponSkinInventory.Purchase(HeroId.Bolt7,variant)==AvatarPurchaseResult.AlreadyOwned&&ProfileTransaction.Commits==commits,"no duplicate debit");
Check(!WeaponSkinInventory.Owned(HeroId.Veyr),"purchase cannot grant Zhaev skin");Check(WeaponSkinInventory.EquipOriginal(HeroId.Bolt7)&&WeaponSkinInventory.EquippedVariant(HeroId.Bolt7)==-1,"original can be restored");Check(WeaponSkinInventory.Equip(HeroId.Bolt7,variant)&&WeaponSkinInventory.EquippedVariant(HeroId.Bolt7)==variant,"owned variant can be restored");
Reset(500);PaidShardCommerce.Instance=new(){Ready=true};Check(WeaponSkinInventory.Purchase(HeroId.Bolt7,variant)==AvatarPurchaseResult.Pending&&PlayerWallet.LastSku=="weapon_10_"+variant&&ProfileTransaction.Commits==0&&!WeaponSkinInventory.Owned(HeroId.Bolt7),"server flow uses correct SKU without local grant");}
Reset(1000);WeaponSkinInventory.Purchase(HeroId.Bolt7,0);WeaponSkinInventory.Purchase(HeroId.Bolt7,1);Check(PlayerWallet.EarnedBalance==0&&WeaponSkinInventory.Owned(HeroId.Bolt7,0)&&WeaponSkinInventory.Owned(HeroId.Bolt7,1),"two independent purchases");AccountProfilePrefs.Values.Clear();Check(!WeaponSkinInventory.Owned(HeroId.Bolt7),"account separation");
var chest=new Transform{name="Chest"};var upper=new Transform{name="UpperArm.L",parent=chest};var left=new Transform{name="Weapon.L",parent=upper};var right=new Transform{name="Weapon.R",parent=chest};var barrel=new Transform{name="GatlingSpin",parent=right};var shoulder=new Transform{name="WeaponShoulder",parent=chest};
var bones=new[]{chest,left,right,barrel,shoulder};Check(!Bolt7WeaponColorwayMask.IsWeapon(chest)&&!Bolt7WeaponColorwayMask.IsWeapon(upper)&&!Bolt7WeaponColorwayMask.IsWeapon(shoulder),"body and shoulders excluded");Check(Bolt7WeaponColorwayMask.IsWeapon(left)&&Bolt7WeaponColorwayMask.IsWeapon(right)&&Bolt7WeaponColorwayMask.IsWeapon(barrel)&&!Bolt7WeaponColorwayMask.IsWeapon(null),"both weapons and animated descendants included");
var weights=new BoneWeight[12];var vertices=new Vector3[12];for(int i=0;i<12;i++){weights[i]=new(){boneIndex0=i/3,weight0=1};vertices[i]=new Vector3(i<3?100:i,0,0);}
var source=new Mesh{vertices=vertices,boneWeights=weights,Triangles=new[]{new[]{0,1,2,3,4,5},new[]{6,7,8,9,10,11,0,4,5}},bounds=new Bounds(new Vector3(100,0,0),Vector3.zero)};
var original=source.Triangles.Select(t=>(int[])t.Clone()).ToArray();var overlay=source.Clone();Bolt7WeaponColorwayMask.KeepWeapons(overlay,bones);
Check(overlay.Triangles[0].SequenceEqual(new[]{3,4,5})&&overlay.Triangles[1].SequenceEqual(new[]{6,7,8,9,10,11}),"only complete weapon triangles retained across material slots");Check(source.Triangles.Zip(original,(a,b)=>a.SequenceEqual(b)).All(x=>x),"source body mesh unchanged");Check(overlay.vertices.Length==source.vertices.Length&&overlay.boneWeights.Select(w=>w.boneIndex0).SequenceEqual(source.boneWeights.Select(w=>w.boneIndex0)),"skinning and vertices unchanged");Check(overlay.bounds.min.x==3&&overlay.bounds.max.x==11,"patterns use weapon bounds rather than body bounds");
weights[3].weight0=.5f;Check(Bolt7WeaponColorwayMask.FilterTriangles(new[]{3,4,5},weights,new[]{false,true,true,true,false}).Length==0,"mixed body weights fail closed");Check(Bolt7WeaponColorwayMask.FilterTriangles(new[]{-1,4,5},weights,new[]{false,true,true,true,false}).Length==0,"invalid triangle fails closed");
Console.WriteLine("PASS "+checks+" BOLT-7 weapon catalog, purchase, account and skinned-mask checks. Native materials/rendering require Unity acceptance.");
}}
