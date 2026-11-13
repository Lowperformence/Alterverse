"""Compile both complete offline-lobby UI files, preserving cross-file namespaces.
Engine/widgets/session boundaries are substitutes; this is not a native Unity build.
"""
from pathlib import Path
import json,subprocess,tempfile


def run(dotnet,root):
    fixtures='''
using System;using System.Threading.Tasks;using System.Collections.Generic;using System.Reflection;
namespace UnityEngine {
public class Texture2D{}
public struct Rect {public float x,y,width,height;public float yMax=>y+height;public Rect(float a,float b,float c,float d){x=a;y=b;width=c;height=d;}}
public static class Time {public static float unscaledTime;}
public static class Mathf {public static float PingPong(float v,float n)=>n-Math.Abs(v%(2*n)-n);}
public class GUIStyle {public static GUIStyle none=>new();}
public class GUIContent {public static GUIContent none=>new();}
public static class GUI {public static bool enabled=true;public static bool Button(Rect r,GUIContent c,GUIStyle s)=>false;}
}
namespace Alterverse.Characters {
public enum HeroId {Veyr,Kaelon,Nyra,Elyra,Mira,Rook,Traveler,Nerok,Timmy,VoidKnight,Bolt7}
public static class HeroIdentity {public static string NameFor(HeroId h)=>h.ToString();}
public static class PlayerHeroSelection {public static bool Select(HeroId h)=>true;}
}
namespace Alterverse.Match {
public enum ArenaVariant {Moon}
public static class ArenaVariantRuntime {public static ArenaVariant Selected=>ArenaVariant.Moon;}
}
namespace Alterverse.AI {
public enum BotDifficulty {Normal}
public static class BotDifficultySettings {public static BotDifficulty Selected=>BotDifficulty.Normal;public static string Label(BotDifficulty d)=>d.ToString();}
}
namespace Alterverse.Shop {
public class ArenaShopItem {public string Name;}
public static class ArenaShopCatalog {public static ArenaShopItem For(Alterverse.Match.ArenaVariant v)=>new();}
public static class HeroUnlockInventory {public static bool Owned(Alterverse.Characters.HeroId h)=>true;}
}
namespace Alterverse.Audio {public static class UiSoundRouter {public static void Confirm(){}}}
namespace Alterverse.Multiplayer {
public static class MatchSession {public const int TeamSize=4;}
public static class OfflineLobbyRoster {
public static bool Prepared=>true;public static void Begin(Alterverse.Characters.HeroId h){}
public static Alterverse.Characters.HeroId At(int i)=>(Alterverse.Characters.HeroId)(i%4);
public static bool Available(int i,Alterverse.Characters.HeroId h)=>true;
public static bool Select(int i,Alterverse.Characters.HeroId h)=>true;
}}
namespace Alterverse.UI {
internal class FrontEndTheme {
public static List<string> Labels=new();public static string Click;
public static UnityEngine.Texture2D Pixel=>null;public UnityEngine.GUIStyle Body=new(),Small=new(),Heading=new();
public void Panel(UnityEngine.Rect r){}public void Frame(UnityEngine.Rect r,bool b){}
public void Progress(UnityEngine.Rect r,float fraction){}
public void Label(UnityEngine.Rect r,string text,UnityEngine.GUIStyle style,bool secondary=false){Labels.Add(text);}
public void FitLabel(UnityEngine.Rect r,string text,UnityEngine.GUIStyle style,bool secondary=false){Labels.Add(text);}
public bool Action(UnityEngine.Rect r,string text,bool primary=false,bool enabled=true){if(enabled&&Click==text){Click=null;return true;}return false;}
}
public static class StylizedHeroCard {
public static bool RosterReady;public static List<bool> Draws=new();
public static void Draw(UnityEngine.Rect r,Alterverse.Characters.HeroId h,UnityEngine.Texture2D t,float time,bool showPortrait=true){Draws.Add(showPortrait);}}
public sealed partial class MainMenuController {
private static MainMenuController instance;private int selectedHero;private bool launching,choosingMatchMode;
private float ContentWidth=1200,Left=50;private string launchMessage="";private FrontEndTheme theme=new();
public static string LastPage;public static int Starts;
private enum Page{Play,Coop}private void SetPage(Page p){LastPage=p.ToString();}private void Heading(string h,string s){}
private Task BeginOfflineMatchAsync(){Starts++;return Task.CompletedTask;}
}}
static class OfflinePresentationRegression {
static void Check(bool b,string label){if(!b)throw new Exception(label);}
static void Call(object o,string name)=>o.GetType().GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(o,null);
static void Main(){
var menu=new Alterverse.UI.MainMenuController();
UnityEngine.Time.unscaledTime=0;Alterverse.UI.StylizedHeroCard.RosterReady=false;Call(menu,"OpenOfflineLobby");
Call(menu,"DrawOfflineLobby");
Check(Alterverse.UI.FrontEndTheme.Labels.Contains("PREPARING YOUR SQUAD"),"Early lobby gives preparation feedback");
Check(Alterverse.UI.StylizedHeroCard.Draws.Count==0,"No portraits appear one by one while waiting");
Alterverse.UI.FrontEndTheme.Click="BACK";Call(menu,"DrawOfflineLobby");
Check(Alterverse.UI.MainMenuController.LastPage=="Play","Preparation can be cancelled");
Call(menu,"OpenOfflineLobby");UnityEngine.Time.unscaledTime=8.1f;
Alterverse.UI.StylizedHeroCard.Draws.Clear();Call(menu,"DrawOfflineLobby");
Check(Alterverse.UI.StylizedHeroCard.Draws.Count==19&&!Alterverse.UI.StylizedHeroCard.Draws.Contains(true),"Timeout exposes all cards with consistent fallback");
Alterverse.UI.FrontEndTheme.Click="START OFFLINE MATCH";Call(menu,"DrawOfflineLobby");
Check(Alterverse.UI.MainMenuController.Starts==1,"Timeout fallback remains playable");
Alterverse.UI.StylizedHeroCard.RosterReady=true;Alterverse.UI.StylizedHeroCard.Draws.Clear();Call(menu,"DrawOfflineLobby");
Check(Alterverse.UI.StylizedHeroCard.Draws.Count==19&&!Alterverse.UI.StylizedHeroCard.Draws.Contains(false),"Ready portraits appear together");
Console.WriteLine("PASS offline lobby readiness, cancellation and fallback (controlled fixtures)");
}}
'''
    ui=root/'Assets/Alterverse/Scripts/UI'
    runtime=sorted((dotnet/'shared/Microsoft.NETCore.App').iterdir())[-1]
    csc=sorted((dotnet/'sdk').glob('*/Roslyn/bincore/csc.dll'))[-1]
    with tempfile.TemporaryDirectory(prefix='df-offline-ui-') as directory:
        directory=Path(directory);stub=directory/'Fixtures.cs';stub.write_text(fixtures)
        command=[str(dotnet/'dotnet'),str(csc),'-nologo','-target:exe','-langversion:9','-nowarn:0169,0649,0414',
            '-out:'+str(directory/'OfflineUI.dll'),*['-r:'+str(x) for x in runtime.glob('*.dll')],str(stub),
            str(ui/'MainMenuOfflineLobby.cs'),str(ui/'LobbyHeroPicker.cs')]
        subprocess.run(command,check=True)
        (directory/'OfflineUI.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{
            'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':runtime.name}}}))
        subprocess.run([str(dotnet/'dotnet'),str(directory/'OfflineUI.dll')],check=True)
        # Reproduce the reported compiler failure by removing only the missing import.
        broken=directory/'MissingImport.cs';broken.write_text((ui/'MainMenuOfflineLobby.cs').read_text().replace('using Alterverse.Match;\n','',1))
        negative=command.copy();negative[-2]=str(broken)
        result=subprocess.run(negative,text=True,capture_output=True)
        assert result.returncode!=0 and "CS0103" in result.stdout and "ArenaVariantRuntime" in result.stdout
    print('PASS complete offline lobby/picker compilation; missing-import control reproduces CS0103.')


if __name__ == '__main__':
    import argparse
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dotnet-root',type=Path,required=True)
    args=parser.parse_args()
    run(args.dotnet_root,Path(__file__).resolve().parents[3])
