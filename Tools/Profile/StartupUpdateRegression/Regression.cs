using System;
using System.Reflection;
using Alterverse.Profile;
using Alterverse.Shop;
using Alterverse.UI;
using UnityEngine;
public static class StartupUpdateTests{
 static int checks;static readonly BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
 static void Check(bool value,string why){checks++;if(!value)throw new Exception(why);}
 static StartupAppUpdateManager Fresh(){if(StartupAppUpdateManager.Instance!=null)typeof(StartupAppUpdateManager).GetMethod("OnDestroy",Flags).Invoke(StartupAppUpdateManager.Instance,null);PlayFixture.InfoRequests=PlayFixture.DialogRequests=PlayFixture.UiRequests=PlayFixture.WrongSignatures=0;PlayFixture.Missing=false;PlayFixture.Successful=PlayFixture.Complete=PlayFixture.Allowed=PlayFixture.StartResult=true;PlayFixture.Availability=0;Time.unscaledTime=Time.Clock=Time.ClockStep=0;PaidShardCommerce.Instance=new();NativeStoreReview.Calls=0;typeof(StartupAppUpdateManager).GetMethod("Ensure",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);return StartupAppUpdateManager.Instance;}
 public static void Main(){
#if UNITY_SERVER
  Check(Fresh()==null,"Dedicated server must never create a Play Store update manager");
#else
  var manager=Fresh();Check(manager!=null,"Scene startup creates update manager");
  var menu=new MainMenuController();Check(!menu.PrepareForTest(),"Main menu must wait for initial version check");FrontEndTheme.Labels.Clear();menu.DrawStartupForTest();Check(FrontEndTheme.Labels.Contains("CHECKING FOR UPDATES"),"Waiting screen explains version check");
  manager.CheckAndTriggerMandatoryUpdateAsync(true).GetAwaiter().GetResult();Check(manager.CheckCompleted&&!manager.Checking,"Initial check completes and releases spinner");Check(menu.PrepareForTest(),"No update allows main menu");
#if UNITY_ANDROID && !UNITY_EDITOR
  Check(PlayFixture.InfoRequests==1,"Startup asks Google Play for app update information");Check(manager.InstalledVersionCode==5,"Installed version comes from Android package manager");
  typeof(StartupAppUpdateManager).GetMethod("OnApplicationFocus",Flags).Invoke(manager,new object[]{true});Check(PlayFixture.InfoRequests==1,"Immediate refocus does not issue duplicate requests");Time.unscaledTime=16;typeof(StartupAppUpdateManager).GetMethod("OnApplicationFocus",Flags).Invoke(manager,new object[]{true});Check(PlayFixture.InfoRequests==2,"Later foreground entry checks for newly available updates");
  manager=Fresh();PlayFixture.Availability=2;manager.CheckAndTriggerMandatoryUpdateAsync(true).GetAwaiter().GetResult();Check(manager.UpdateRequired&&manager.AvailableVersionCode==10,"New Play build is recognized");Check(PlayFixture.DialogRequests==1&&PlayFixture.UiRequests==1&&PlayFixture.WrongSignatures==0,"Immediate dialog uses the boolean Java API on Android UI thread");Check(!menu.PrepareForTest(),"Required update prevents entering old main menu");
  manager=Fresh();PlayFixture.Availability=3;PlayFixture.Allowed=false;manager.CheckAndTriggerMandatoryUpdateAsync(true).GetAwaiter().GetResult();Check(PlayFixture.DialogRequests==1,"An interrupted immediate update resumes even when a new immediate update is not offered");
  manager=Fresh();PlayFixture.Availability=2;PlayFixture.StartResult=false;manager.CheckAndTriggerMandatoryUpdateAsync(true,true).GetAwaiter().GetResult();Check(manager.UpdateRequired&&NativeStoreReview.Calls==1,"False dialog result uses explicit Play Store fallback");
  manager=Fresh();PlayFixture.Successful=false;manager.CheckAndTriggerMandatoryUpdateAsync(true).GetAwaiter().GetResult();Check(manager.CheckCompleted&&!manager.UpdateRequired&&!manager.Checking,"Failed availability query does not leave endless startup spinner");
  manager=Fresh();PlayFixture.Complete=false;Time.ClockStep=13;manager.CheckAndTriggerMandatoryUpdateAsync(true).GetAwaiter().GetResult();Check(manager.CheckCompleted&&!manager.Checking&&PlayFixture.DialogRequests==0,"Play query timeout releases initial startup gate");
  manager=Fresh();PlayFixture.Missing=true;PaidShardCommerce.Instance.MinAndroidVersionCode=6;manager.CheckAndTriggerMandatoryUpdateAsync(true,true).GetAwaiter().GetResult();Check(manager.UpdateRequired&&NativeStoreReview.Calls==1,"Existing server minimum version still enforces store update when Play API is unavailable");
#endif
#endif
  Console.WriteLine($"Startup update: {checks} production-manager/menu checks passed for this target; Java/Unity substitutes, no real Play installation.");
 }
}
