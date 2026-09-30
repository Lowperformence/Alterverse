using System.Reflection;
using Alterverse.Multiplayer;
using Alterverse.Profile;
using UnityEngine;

namespace Alterverse.UI
{
    /// <summary>
    /// Adds the voice-chat playback level to the existing main-menu Settings page without
    /// duplicating the menu controller. The setting is global and is applied to the active
    /// Vivox co-op channel immediately when one exists.
    /// </summary>
    [DefaultExecutionOrder(1100)]
    public sealed class MainMenuVoiceChatVolume : MonoBehaviour
    {
        private const float MenuHeight=720f;
        private static readonly FieldInfo PageField=typeof(MainMenuController).GetField("page",BindingFlags.Instance|BindingFlags.NonPublic);

        private FrontEndTheme theme;
        private MainMenuController menu;
        private float voice;
        private bool wasSettings;
        private bool dirty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Ensure()
        {
            if(FindAnyObjectByType<MainMenuVoiceChatVolume>()==null)
                new GameObject("DF_MainMenuVoiceVolume").AddComponent<MainMenuVoiceChatVolume>();
        }

        private void Awake()
        {
            voice=PlayerProfile.VoiceChatVolume;
        }

        private void Update()
        {
            bool settings=IsSettingsPage();
            if(settings&&!wasSettings)voice=PlayerProfile.VoiceChatVolume;
            if(!settings&&wasSettings)Flush();
            wasSettings=settings;
        }

        private bool IsSettingsPage()
        {
            if(!MainMenuController.IsVisible||PageField==null)return false;
            menu??=FindAnyObjectByType<MainMenuController>();
            if(menu==null)return false;
            object page=PageField.GetValue(menu);
            return page!=null&&page.ToString()=="Settings";
        }

        private void ApplyLive()
        {
            PlayerProfile.SetVoiceChatVolume(voice,false);
            CoopVoiceChatService.Instance?.RefreshOutputVolume();
            dirty=true;
        }

        private void Flush()
        {
            if(!dirty)return;
            PlayerProfile.SetVoiceChatVolume(voice,true);
            dirty=false;
        }

        private void OnDisable(){Flush();}
        private void OnDestroy(){Flush();}

        private void OnGUI()
        {
            if(!IsSettingsPage())return;

            Rect safe=Screen.safeArea;
            if(safe.width<=0||safe.height<=0)return;
            float scale=Mathf.Min(safe.height/MenuHeight,safe.width/1500f);
            if(scale<=0)return;
            float menuWidth=safe.width/scale;
            float contentWidth=Mathf.Min(1360f,menuWidth-288f);
            float left=(menuWidth-contentWidth+240f)*.5f;
            float audioWidth=contentWidth*.64f;
            float x=left+audioWidth+32f;
            float width=contentWidth-audioWidth-32f;

            Matrix4x4 oldMatrix=GUI.matrix;
            Color oldColor=GUI.color;
            int oldDepth=GUI.depth;
            try
            {
                float y=Screen.height-safe.yMax+(safe.height-MenuHeight*scale)*.5f;
                GUI.matrix=Matrix4x4.TRS(new Vector3(safe.x,y,0),Quaternion.identity,Vector3.one*scale);
                GUI.depth=-5500;
                theme??=new FrontEndTheme();

                theme.Label(new Rect(x,306,width-90,28),"Voice chat volume",theme.Body);
                theme.Label(new Rect(x+width-82,306,82,28),$"{Mathf.RoundToInt(voice*100)}%",theme.Small);

                float before=voice;
                if(theme.Action(new Rect(x,344,46,36),"−"))voice=Mathf.Clamp01(voice-.1f);
                if(theme.Action(new Rect(x+width-46,344,46,36),"+"))voice=Mathf.Clamp01(voice+.1f);

                Rect track=new Rect(x+60,357,width-120,10);
                FrontEndTheme.Fill(track,FrontEndTheme.Raised);
                FrontEndTheme.Fill(new Rect(track.x,track.y,track.width*voice,track.height),FrontEndTheme.Accent);
                Event e=Event.current;
                if((e.type==EventType.MouseDown||e.type==EventType.MouseDrag)&&track.Contains(e.mousePosition))
                {
                    voice=Mathf.Clamp01((e.mousePosition.x-track.x)/track.width);
                    e.Use();
                }
                FrontEndTheme.Fill(new Rect(track.x+track.width*voice-5,track.center.y-10,10,20),FrontEndTheme.Text);

                theme.Label(new Rect(x,390,width,22),"Controls incoming co-op voice only.",theme.Small,true);
                if(!Mathf.Approximately(before,voice))ApplyLive();
            }
            finally
            {
                GUI.depth=oldDepth;
                GUI.matrix=oldMatrix;
                GUI.color=oldColor;
            }
        }
    }
}

