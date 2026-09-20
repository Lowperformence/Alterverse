#if UNITY_EDITOR
using Alterverse.Characters;
using UnityEditor;
using UnityEngine;

namespace Alterverse.UI
{
    // Draws the production grid with synthetic entries. No wallet, entitlement or inventory writes.
    public sealed class CosmeticCollectionStressWindow : EditorWindow
    {
        private CosmeticCollectionPanel panel;
        private FrontEndTheme theme;
        private bool bag;
        [MenuItem("DIMENSIONS FRONT/Checks/Collection layout preview (100 cosmetics, 30 items)")]
        private static void Open()
        {
            var window=GetWindow<CosmeticCollectionStressWindow>();
            window.titleContent=new GUIContent("Collection stress preview");window.minSize=new Vector2(720,520);window.Show();
        }
        private void OnEnable(){panel=new CosmeticCollectionPanel();panel.PopulateStressFixture(false);}
        private void OnDisable(){panel?.Dispose();panel=null;}
        private static void IgnoreHero(int hero){}
        private void OnGUI()
        {
            if(panel==null)return;theme??=new FrontEndTheme();
            FrontEndTheme.Fill(new Rect(0,0,position.width,position.height),FrontEndTheme.Background);
            if(theme.Action(new Rect(16,14,230,48),"100 COSMETICS",selected:!bag)){bag=false;panel.PopulateStressFixture(false);}
            if(theme.Action(new Rect(260,14,230,48),"30 BATTLE ITEMS",selected:bag)){bag=true;panel.PopulateStressFixture(true);}
            theme.Label(new Rect(16,70,position.width-32,24),"Read-only fixture • resize, scroll and select • purchases disabled",theme.Small,true);
            panel.Draw(theme,new Rect(16,108,position.width-32,410),HeroId.Veyr,bag,null,IgnoreHero);
        }
    }
}
#endif
