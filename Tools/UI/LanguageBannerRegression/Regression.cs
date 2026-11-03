using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using Alterverse.Profile;
using Alterverse.Shop;
using Alterverse.UI;
using GoogleMobileAds.Api;
using UnityEngine;

static class Regression
{
    static int checks;
    static void Check(bool value,string message) {checks++;if(!value)throw new Exception(message);}
    static void ResetLanguage()=>typeof(GameLanguage).GetMethod("Reset",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
    static void Call(MenuBannerAds ads,string method,params object[] args)=>typeof(MenuBannerAds).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ads,args);
    public static void Main(string[] args)
    {
        Resources.Catalog=File.ReadAllText(args[0]);
        PlayerPrefs.SetString("DF_Shards","170");
        Application.systemLanguage=SystemLanguage.German;
        Check(GameLanguage.Current.Code=="de","First launch follows device language without location access");
        Check(GameLanguage.Languages.Count==40,"All forty language options are available offline");
        foreach(var language in GameLanguage.Languages)
        {
            Check(GameLanguage.Select(language.Code),"Can select "+language.Code);
            Check(GameLanguage.Current.Code==language.Code,"Selection applies immediately: "+language.Code);
            string expected=GameLanguage.Text("Settings");
            Check(!string.IsNullOrWhiteSpace(expected),"Settings label exists: "+language.Code);
            Check(GameLanguage.Text("SETTINGS")==expected,"Uppercase menu label resolves to the same translation");
            ResetLanguage();
            Check(GameLanguage.Current.Code==language.Code && GameLanguage.Text("Settings")==expected,"Selection survives a new app session: "+language.Code);
        }
        Check(PlayerPrefs.GetString("DF_Shards","")=="170","Language changes do not change currency or profile data");
        GameLanguage.Select("de");
        Check(GameLanguage.Text("CHOOSE ONE BONUS")=="WÄHLE EINEN BONUS"&&GameLanguage.Text("MOVEMENT SPEED")=="BEWEGUNGSTEMPO"&&GameLanguage.Text("RECOMMENDED")=="EMPFOHLEN","Buff choices are translated into German");
        GameLanguage.Select("es");Check(GameLanguage.Text("MAX HEALTH")=="SALUD MÁXIMA","Buff choices are translated into Spanish");
        GameLanguage.Select("fr");Check(GameLanguage.Text("OUTGOING DAMAGE")=="DÉGÂTS INFLIGÉS","Buff choices are translated into French");
        GameLanguage.Select("de");
        Check(!GameLanguage.Select("unconfigured-language")&&GameLanguage.Current.Code=="de","Invalid input preserves the existing selection");
        Check(GameLanguage.Normalize("ES_mx")=="es-MX"&&GameLanguage.Normalize("pt-BR")=="pt-BR","Regional variants remain distinct");
        Check(GameLanguage.Normalize("fr-CA")=="fr"&&GameLanguage.Normalize("no-NO")=="nb","Regional and legacy aliases resolve");
        Check(GameLanguage.Normalize("zh-TW")=="zh-Hant"&&GameLanguage.Normalize("zh-CN")=="zh-Hans","Chinese writing systems remain distinct");
        Check(GameLanguage.Text("Timmy")=="Timmy"&&GameLanguage.Text("An untranslated message.")=="An untranslated message.","Names and missing translations retain the source text");
        var culture=CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture=new CultureInfo("es-MX");Application.systemLanguage=SystemLanguage.Spanish;
        GameLanguage.Select(GameLanguage.DeviceDefault);
        Check(GameLanguage.Current.Code=="es-MX","Automatic language detection preserves the Mexican locale");
        CultureInfo.CurrentUICulture=culture;Application.systemLanguage=SystemLanguage.German;
        Check(GameLanguage.Current.Code=="de","Device-default follows a changed device language");

        var picker=new LanguagePicker();var theme=new FrontEndTheme();
        picker.Open();GUI.NextText="russian";FrontEndTheme.Click="Русский  /  Russian";
        picker.Draw(theme,new Rect(0,0,1500,720));
        Check(!picker.IsOpen&&GameLanguage.Current.Code=="ru","Search and selecting a language change the real preference");
        picker.Open();GUI.NextText="cestina";FrontEndTheme.Click="Čeština  /  Czech";
        picker.Draw(theme,new Rect(0,0,1500,720));
        Check(!picker.IsOpen&&GameLanguage.Current.Code=="cs","Search ignores accents in native names");
        picker.Open();FrontEndTheme.Click=GameLanguage.Text("Close");picker.Draw(theme,new Rect(0,0,1500,720));
        Check(!picker.IsOpen&&GameLanguage.Current.Code=="cs","Closing does not change the selection");
        picker.Open();FrontEndTheme.Actions=0;picker.Draw(theme,new Rect(0,0,1500,720));
        Check(FrontEndTheme.Actions<16,"Offscreen language rows do not allocate/render text textures");

        foreach(float density in new[]{1f,2f,3f,4f})
        foreach(var safe in new[]{new Rect(0,0,1920,1080),new Rect(80,40,1760,1000),new Rect(0,0,1280,720),new Rect(0,0,1560,1020)})
        {
            var layout=MenuBannerLayout.Calculate(safe,1080,density);
            if(!layout.Valid)continue;
            Check(layout.AdPixels.width==320*density&&layout.AdPixels.height==(layout.LargeBanner?100:50)*density,"Never shrink or crop a native banner");
            Check(layout.AdPixels.x>=layout.BoardPixels.x&&layout.AdPixels.xMax<=layout.BoardPixels.xMax&&layout.AdPixels.yMax<=layout.BoardPixels.yMax,"The native ad stays completely inside its frame");
            var menuRect=layout.InMenuCoordinates(safe,1080);
            Check(menuRect.x>=0&&menuRect.y>=0,"Convert the lower-right board into the menu coordinate system");
            float scale=Math.Min(safe.height/720f,safe.width/1500f);
            float menuWidth=safe.width/scale,contentWidth=Math.Min(1360,menuWidth-64),left=(menuWidth-contentWidth)*.5f;
            float footerWidth=Math.Min(contentWidth*.66f,menuRect.x-left-16),cardWidth=Math.Min(232,(footerWidth-24)/3);
            Check(cardWidth>=120&&left+cardWidth*3+24<=menuRect.x-16,"All three footer cards/actions fit to the left of the advertisement");
            Check(Math.Max(110,menuRect.y-268)+256<=menuRect.y-12,"The event card remains above the advertising board");
            Check(layout.BoardPixels.x>=safe.x&&layout.BoardPixels.xMax<=safe.xMax&&layout.BoardPixels.yMax<=1080-safe.y,"Frame respects display cutouts and safe insets");
        }
        Check(!MenuBannerLayout.Calculate(new Rect(0,0,300,200),200,1).Valid,"Do not request a banner that cannot fit");
        Check(MenuBannerLayout.Calculate(new Rect(0,0,1500,720),720,float.NaN).Valid,"Invalid density safely falls back");

        var ads=MenuBannerAds.Ensure();
        ads.Request(new Rect(300,600,960,150));Call(ads,"LateUpdate");
        Check(BannerView.All.Count==0,"Never request ads before the shared consent and initialization gate");
#if UNITY_ANDROID && !UNITY_EDITOR && !UNITY_SERVER
        Check(ads.Status==RewardedShardAds.Instance.Status,"An unready banner exposes its shared privacy/SDK blocker");
#endif
#if UNITY_ANDROID && !UNITY_EDITOR && !UNITY_SERVER
        RewardedShardAds.Instance.ReadyForRequests=true;
        Call(ads,"LateUpdate");
        Check(BannerView.All.Count==1,"One banner instance is created");
        var banner=BannerView.All[0];
#if DEVELOPMENT_BUILD
        Check(banner.Unit=="ca-app-pub-3940256099942544/6300978111","Development uses the pinned SDK's real banner test ID");
#else
        Check(banner.Unit=="ca-app-pub-1716027263297702/9930233354","Release uses the supplied banner placement");
#endif
        Check(banner.X==100&&banner.Y==200,"Convert Unity pixels to the pinned Android plugin's dp offsets");
        banner.Fail();Call(ads,"LateUpdate");
        Check(!ads.Loaded&&!banner.Visible&&ads.Status.Contains("Network unavailable")&&ads.Status.Contains("(2)"),
            "An initial failure exposes the SDK reason and hides the empty native view");
        for(int i=0;i<100;i++)Call(ads,"LateUpdate");
        Time.unscaledTime+=29;Call(ads,"LateUpdate");
        Check(BannerView.All.Count==1&&banner.Loads==1,"A failed initial load cannot retry in a frame loop or before thirty seconds");
        MainMenuController.MockVisible=false;Time.unscaledTime+=1;Call(ads,"LateUpdate");
        Check(BannerView.All.Count==1,"A due retry remains suspended outside the menu");
        MainMenuController.MockVisible=true;Call(ads,"LateUpdate");
        Check(banner.Destroyed&&BannerView.All.Count==2,"An eligible menu retries the empty initial load after its delay");
        banner=BannerView.All[1];
        banner.Fail();Call(ads,"LateUpdate");
        Time.unscaledTime+=59;Call(ads,"LateUpdate");
        Check(BannerView.All.Count==2,"Consecutive failures increase the delay to sixty seconds");
        Time.unscaledTime+=1;Call(ads,"LateUpdate");banner=BannerView.All[2];
        banner.Complete();Call(ads,"LateUpdate");Check(ads.Loaded&&banner.Visible,"A recovered banner becomes available");
        banner.Fail();Call(ads,"LateUpdate");Time.unscaledTime+=200;Call(ads,"LateUpdate");
        Check(ads.Loaded&&banner.Visible&&BannerView.All.Count==3,"SDK refresh errors preserve an existing creative and never trigger app refreshes");
        for(int i=0;i<100;i++)Call(ads,"LateUpdate");
        Check(banner.Loads==1&&BannerView.All.Count==3,"No application refresh loop or duplicate views");
        MainMenuController.MockVisible=false;Call(ads,"LateUpdate");
        Check(!banner.Visible,"Hide advertisements outside the home page and behind dialogs");
        MainMenuController.MockVisible=true;Call(ads,"LateUpdate");
        Check(banner.Visible&&banner.Loads==1,"Returning to the menu reuses the banner");
        MainMenuController.ReviewPromptVisible=true;Call(ads,"LateUpdate");
        Check(!banner.Visible,"The real menu eligibility property hides banners behind the static review prompt");
        MainMenuController.ReviewPromptVisible=false;Call(ads,"LateUpdate");
        RewardedShardAds.Instance.ReadyForRequests=false;Call(ads,"LateUpdate");
        Check(!banner.Visible,"Fullscreen rewarded ads and privacy dialogs suppress the banner");
        RewardedShardAds.Instance.PrivacyRevision++;Call(ads,"LateUpdate");
        Check(banner.Destroyed&&BannerView.All.Count==3,"A privacy change destroys the old ad without requesting before consent");
        RewardedShardAds.Instance.ReadyForRequests=true;Call(ads,"LateUpdate");
        var second=BannerView.All[3];
        Call(ads,"OnApplicationPause",true);Call(ads,"LateUpdate");Check(!second.Visible,"Backgrounded apps hide the banner");
        Call(ads,"OnApplicationPause",false);Call(ads,"LateUpdate");Check(second.Visible,"Resume restores an eligible menu banner");
        Time.frameCount+=4;Call(ads,"LateUpdate");Check(!second.Visible,"A stale menu draw cannot leave a native overlay visible");
        ads.Request(new Rect(300,600,960,300),true);Call(ads,"LateUpdate");
        var large=BannerView.All[4];
        Check(second.Destroyed&&large.Size.Width==320&&large.Size.Height==100,"A size transition replaces the view with a full 320x100 dp large banner");
        large.Fail();Call(ads,"LateUpdate");Time.unscaledTime+=29;Call(ads,"LateUpdate");
        Check(BannerView.All.Count==5,"A successful recovery resets the next initial-failure delay to thirty seconds");
        large.Complete();Call(ads,"LateUpdate");Time.unscaledTime+=200;Call(ads,"LateUpdate");
        Check(ads.Loaded&&large.Visible&&BannerView.All.Count==5,
            "An SDK recovery cancels the pending app retry without destroying its creative");
        int before=AndroidJavaClass.Calls;
        LocalizedText.Draw(new Rect(0,0,300,48),"العربية",new GUIStyle());
        LocalizedText.Draw(new Rect(0,0,300,48),"العربية",new GUIStyle());
        Check(AndroidJavaClass.Calls==before+1,"Script-shaped text is rendered once and cached");
        Call(ads,"OnDestroy");Check(large.Destroyed,"Destroying the host releases its native ad");
#else
        RewardedShardAds.Instance.ReadyForRequests=true;Call(ads,"LateUpdate");
        Check(BannerView.All.Count==0,"Editor and dedicated servers never request real mobile advertisements");
#endif
        Console.WriteLine("PASS: "+checks+" language persistence, dialog, layout, banner lifecycle and platform checks.");
    }
}
