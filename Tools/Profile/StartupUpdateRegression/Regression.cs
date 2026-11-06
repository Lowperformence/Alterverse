using System;
using System.Reflection;
using Alterverse.Profile;
using Alterverse.Shop;
using Alterverse.UI;
using UnityEngine;
public static class StartupUpdateTests{
 static int checks;static readonly BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
 static void Check(bool value,string why){checks++;if(!value)throw new Exception(why);}
 static StartupAppUpdateManager Fresh(){if(StartupAppUpdateManager.Instance!=null)typeof(StartupAppUpdateManager).GetMethod("OnDestroy",Flags).Invoke(StartupAppUpdateManager.Instance,null);PlayFixture.InfoRequests=PlayFixture.DialogRequests=PlayFixture.UiRequests=PlayFixture.WrongSignatures=PlayFixture.CompletionRequests=0;PlayFixture.Missing=false;PlayFixture.Successful=PlayFixture.Complete=PlayFixture.Allowed=PlayFixture.StartResult=PlayFixture.CompletionSuccessful=PlayFixture.CompletionComplete=true;PlayFixture.Availability=PlayFixture.InstallStatus=0;PlayFixture.Installed=5;Time.unscaledTime=Time.Clock=Time.ClockStep=0;PaidShardCommerce.Instance=new();NativeStoreReview.Calls=0;typeof(StartupAppUpdateManager).GetMethod("Ensure",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);return StartupAppUpdateManager.Instance;}
 static void Invoke(StartupAppUpdateManager manager,string name,params object[] args)=>typeof(StartupAppUpdateManager).GetMethod(name,Flags).Invoke(manager,args);
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
  manager=Fresh();PlayFixture.Availability=2;manager.CheckAndTriggerMandatoryUpdateAsync(true).GetAwaiter().GetResult();
  Check(manager.UpdateFlowActive,"A started native update remains active after dialog launch returns");
  PlayFixture.Availability=3;Time.unscaledTime=2;Invoke(manager,"Update");
  Check(PlayFixture.InfoRequests==2&&PlayFixture.DialogRequests==1,"Background progress polling never opens a duplicate update dialog");
  Invoke(manager,"OnApplicationPause",true);PlayFixture.Availability=2;PlayFixture.InstallStatus=11;Invoke(manager,"OnApplicationPause",false);
  Check(PlayFixture.CompletionRequests==1&&manager.UpdateFlowActive&&NativeStoreReview.Calls==0,"Returning to a downloaded update completes installation/relaunch automatically without a store visit");
  Time.unscaledTime=4;Invoke(manager,"Update");
  Check(PlayFixture.CompletionRequests==1,"A pending native install/relaunch is requested only once");
  PlayFixture.InstallStatus=0;PlayFixture.Availability=1;PlayFixture.Installed=10;Time.unscaledTime=6;Invoke(manager,"Update");
  Check(!manager.UpdateRequired&&!manager.UpdateFlowActive&&manager.InstalledVersionCode==10&&menu.PrepareForTest(),"Installed update clears stale version gate and continues startup without another launch");
  manager=Fresh();PlayFixture.Availability=2;manager.CheckAndTriggerMandatoryUpdateAsync(true).GetAwaiter().GetResult();
  PlayFixture.InstallStatus=11;Time.unscaledTime=2;Invoke(manager,"Update");
  Check(PlayFixture.CompletionRequests==1,"A foreground download finishes automatically even without a focus callback");
  manager=Fresh();PlayFixture.Availability=2;manager.CheckAndTriggerMandatoryUpdateAsync(true).GetAwaiter().GetResult();
  Invoke(manager,"OnApplicationFocus",false);PlayFixture.Availability=3;Invoke(manager,"OnApplicationFocus",true);Invoke(manager,"OnApplicationPause",false);
  Check(PlayFixture.DialogRequests==2,"Focus and pause resume callbacks resume an interrupted immediate update once");
  manager=Fresh();PlayFixture.Availability=2;manager.CheckAndTriggerMandatoryUpdateAsync(true).GetAwaiter().GetResult();
  Invoke(manager,"OnApplicationFocus",false);PlayFixture.InstallStatus=6;Invoke(manager,"OnApplicationFocus",true);
  Check(!manager.UpdateFlowActive&&PlayFixture.DialogRequests==1&&manager.UpdateRequired,"Cancelled consent leaves explicit retry instead of opening the dialog again");
  manager=Fresh();PlayFixture.Availability=2;manager.CheckAndTriggerMandatoryUpdateAsync(true).GetAwaiter().GetResult();
  PlayFixture.InstallStatus=6;Time.unscaledTime=2;Invoke(manager,"Update");
  Check(!manager.UpdateFlowActive&&PlayFixture.DialogRequests==1,"Polling clears cancelled flow even without a focus event");
  manager=Fresh();PlayFixture.Availability=2;manager.CheckAndTriggerMandatoryUpdateAsync(true).GetAwaiter().GetResult();
  PlayFixture.InstallStatus=5;Time.unscaledTime=2;Invoke(manager,"Update");
  Check(!manager.UpdateFlowActive&&manager.UpdateRequired,"Native install failure releases retry while retaining mandatory update");
  manager=Fresh();PlayFixture.Availability=2;PlayFixture.InstallStatus=11;PlayFixture.CompletionSuccessful=false;
  manager.CheckAndTriggerMandatoryUpdateAsync(true).GetAwaiter().GetResult();
  Check(manager.UpdateRequired&&!manager.Checking&&!manager.UpdateFlowActive&&manager.StatusMessage.Contains("erneut"),"Failed completion releases spinner and offers an update retry");
  manager=Fresh();PlayFixture.Availability=3;PlayFixture.InstallStatus=11;
  manager.CheckAndTriggerMandatoryUpdateAsync(true).GetAwaiter().GetResult();
  Check(PlayFixture.CompletionRequests==0&&PlayFixture.DialogRequests==1&&manager.UpdateFlowActive,"downloaded immediate update stays in Play's immediate flow and never calls flexible completion");
  Time.unscaledTime=2;Invoke(manager,"Update");
  Check(PlayFixture.CompletionRequests==0&&PlayFixture.DialogRequests==1,"polling immediate installation does not interrupt or relaunch it repeatedly");
  manager=Fresh();PlayFixture.InstallStatus=11;PlayFixture.CompletionComplete=false;Time.ClockStep=13;
  manager.CheckAndTriggerMandatoryUpdateAsync(true).GetAwaiter().GetResult();
  Check(manager.UpdateRequired&&!manager.Checking&&!manager.UpdateFlowActive,"Completion timeout retains update gate without an endless spinner");
  manager=Fresh();PlayFixture.Availability=3;manager.CheckAndTriggerMandatoryUpdateAsync(true).GetAwaiter().GetResult();
  PaidShardCommerce.Instance.MinAndroidVersionCode=12;PlayFixture.Availability=1;PlayFixture.Installed=10;
  manager.CheckAndTriggerMandatoryUpdateAsync(false).GetAwaiter().GetResult();
  Check(manager.UpdateRequired&&manager.AvailableVersionCode==12,"Post-install reset cannot bypass a higher account-server minimum");
  manager=Fresh();PlayFixture.Availability=2;PlayFixture.StartResult=false;manager.CheckAndTriggerMandatoryUpdateAsync(true,true).GetAwaiter().GetResult();Check(manager.UpdateRequired&&NativeStoreReview.Calls==1,"False dialog result uses explicit Play Store fallback");
  manager=Fresh();PlayFixture.Successful=false;manager.CheckAndTriggerMandatoryUpdateAsync(true).GetAwaiter().GetResult();Check(manager.CheckCompleted&&!manager.UpdateRequired&&!manager.Checking,"Failed availability query does not leave endless startup spinner");
  manager=Fresh();PlayFixture.Complete=false;Time.ClockStep=13;manager.CheckAndTriggerMandatoryUpdateAsync(true).GetAwaiter().GetResult();Check(manager.CheckCompleted&&!manager.Checking&&PlayFixture.DialogRequests==0,"Play query timeout releases initial startup gate");
  manager=Fresh();PlayFixture.Missing=true;PaidShardCommerce.Instance.MinAndroidVersionCode=6;manager.CheckAndTriggerMandatoryUpdateAsync(true,true).GetAwaiter().GetResult();Check(manager.UpdateRequired&&NativeStoreReview.Calls==1,"Existing server minimum version still enforces store update when Play API is unavailable");
#endif
#endif
  Console.WriteLine($"Startup update: {checks} production-manager/menu checks passed for this target; Java/Unity substitutes, no real Play installation.");
 }
}
