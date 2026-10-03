using Alterverse.Profile;
using Alterverse.Shop;
using UnityEngine;
namespace Alterverse.UI
{
    internal static class AccountLoginPanel
    {
        static bool open;
        static string username="",password="";
        static bool working;
        public static void Draw(FrontEndTheme theme,Rect area)
        {
            var service=AccountServerBootstrap.Instance;
            string status=service!=null?service.Status:"Account unavailable.";
            if(PaidShardCommerce.Instance!=null&&PaidShardCommerce.Instance.Ready)status=PaidShardCommerce.Instance.Status;
            theme.Label(new Rect(area.x,area.y,area.width-130,40),status,theme.Small,true);
            if(theme.Action(new Rect(area.xMax-124,area.y,124,40),"ACCOUNT"))open=!open;
            if(!open)return;
            Rect panel=new(area.x,area.y-270,area.width,260);
            theme.Panel(panel);
            theme.Label(new Rect(panel.x+16,panel.y+12,panel.width-32,28),"SAVE AND RESTORE YOUR ACCOUNT",theme.Heading);
            theme.Label(new Rect(panel.x+16,panel.y+44,panel.width-32,24),"Username",theme.Small,true);
            username=GUI.TextField(new Rect(panel.x+16,panel.y+70,panel.width-32,38),username,64,theme.Field);
            theme.Label(new Rect(panel.x+16,panel.y+111,panel.width-32,24),"Password",theme.Small,true);
            password=GUI.PasswordField(new Rect(panel.x+16,panel.y+137,panel.width-32,38),password,'•',128,theme.Field);
            bool enabled=!working&&service!=null&&!service.Busy;
            float width=(panel.width-40)*.5f;
            if(theme.Action(new Rect(panel.x+16,panel.y+186,width,40),"SIGN IN",true,enabled:enabled))Submit(false);
            if(theme.Action(new Rect(panel.x+24+width,panel.y+186,width,40),"SECURE ACCOUNT",true,enabled:enabled))Submit(true);
            theme.Label(new Rect(panel.x+16,panel.y+230,panel.width-32,24),"Secure Account keeps your current progress.",theme.Small,true);
        }
        static async void Submit(bool register)
        {
            working=true;
            string secret=password;password="";
            try{if(await AccountServerBootstrap.Instance.SignInAsync(username,secret,register))open=false;}
            finally{secret=null;working=false;}
        }
    }
}
