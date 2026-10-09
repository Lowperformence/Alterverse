using Alterverse.Profile;
using Alterverse.Shop;
using UnityEngine;

namespace Alterverse.UI
{
    internal static class AccountLoginPanel
    {
        static bool open,working;
        public static void Draw(FrontEndTheme theme,Rect area)
        {
            var service=AccountServerBootstrap.Instance;
            bool linked=service?.GooglePlayAccountReady==true;
            string status=service==null?"Account service unavailable.":
                linked&&!open&&PaidShardCommerce.Instance!=null?PaidShardCommerce.Instance.Status:service.GoogleStatus;
            if(!GooglePlayAccountSession.Supported)status="Google account connection is available in the Android build.";
            theme.Label(new Rect(area.x,area.y,area.width-174,40),status,theme.Small,true);
            if(theme.Action(new Rect(area.xMax-168,area.y,168,40),"GOOGLE ACCOUNT"))open=!open;
            if(!open)return;
            Rect panel=new Rect(area.x,area.y-188,area.width,178);
            theme.Panel(panel);
            theme.Label(new Rect(panel.x+16,panel.y+12,panel.width-32,28),"GOOGLE PLAY ACCOUNT",theme.Heading);
            theme.Label(new Rect(panel.x+16,panel.y+48,panel.width-32,50),
                service?.GoogleRestoreAvailable==true
                    ?"Restore the selected Google account. The current save stays separate; accounts are not merged."
                    :"Save and restore your progress and purchases with Google Play Games. No extra username or password.",theme.Small,true);
            bool enabled=!working&&service!=null&&!service.Busy&&GooglePlayAccountSession.Supported;
            bool restore=service?.GoogleRestoreAvailable==true;
            if(theme.Action(new Rect(panel.x+16,panel.y+112,panel.width-32,44),
                linked?"GOOGLE CONNECTED":restore?"RESTORE GOOGLE":"CONNECT GOOGLE",true,enabled:enabled&&!linked))Submit(restore);
        }
        static async void Submit(bool restore)
        {
            working=true;
            try
            {
                var service=AccountServerBootstrap.Instance;
                if(service!=null&&await service.ConnectGoogleAsync(restore))open=false;
            }
            finally{working=false;}
        }
    }
}
