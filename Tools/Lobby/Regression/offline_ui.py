"""Compile both complete offline-lobby UI files, preserving cross-file namespaces.
Engine/widgets/session boundaries are substitutes; this is not a native Unity build.
"""
from pathlib import Path
import subprocess,tempfile


def run(dotnet,root):
    fixtures='''
using System;using System.Threading.Tasks;
namespace UnityEngine {
public class Texture2D{}
public struct Rect {public float x,y,width,height;public float yMax=>y+height;public Rect(float a,float b,float c,float d){x=a;y=b;width=c;height=d;}}
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
public static UnityEngine.Texture2D Pixel=>null;public UnityEngine.GUIStyle Body=new(),Small=new(),Heading=new();
public void Panel(UnityEngine.Rect r){}public void Frame(UnityEngine.Rect r,bool b){}
public void Label(UnityEngine.Rect r,string text,UnityEngine.GUIStyle style,bool secondary=false){}
public void FitLabel(UnityEngine.Rect r,string text,UnityEngine.GUIStyle style,bool secondary=false){}
public bool Action(UnityEngine.Rect r,string text,bool primary=false,bool enabled=true)=>false;
}
public static class StylizedHeroCard {public static void Draw(UnityEngine.Rect r,Alterverse.Characters.HeroId h,UnityEngine.Texture2D t,float time){}}
public sealed partial class MainMenuController {
private static MainMenuController instance;private int selectedHero;private bool launching,choosingMatchMode;
private float ContentWidth=1200,Left=50;private string launchMessage="";private FrontEndTheme theme=new();
private enum Page{Play,Coop}private void SetPage(Page p){}private void Heading(string h,string s){}
private Task BeginOfflineMatchAsync()=>Task.CompletedTask;
}}
'''
    ui=root/'Assets/Alterverse/Scripts/UI'
    runtime=sorted((dotnet/'shared/Microsoft.NETCore.App').iterdir())[-1]
    csc=sorted((dotnet/'sdk').glob('*/Roslyn/bincore/csc.dll'))[-1]
    with tempfile.TemporaryDirectory(prefix='df-offline-ui-') as directory:
        directory=Path(directory);stub=directory/'Fixtures.cs';stub.write_text(fixtures)
        command=[str(dotnet/'dotnet'),str(csc),'-nologo','-target:library','-langversion:9','-nowarn:0169,0649,0414',
            '-out:'+str(directory/'OfflineUI.dll'),*['-r:'+str(x) for x in runtime.glob('*.dll')],str(stub),
            str(ui/'MainMenuOfflineLobby.cs'),str(ui/'LobbyHeroPicker.cs')]
        subprocess.run(command,check=True)
        # Reproduce the reported compiler failure by removing only the missing import.
        broken=directory/'MissingImport.cs';broken.write_text((ui/'MainMenuOfflineLobby.cs').read_text().replace('using Alterverse.Match;\n','',1))
        negative=command.copy();negative[-2]=str(broken)
        result=subprocess.run(negative,text=True,capture_output=True)
        assert result.returncode!=0 and "CS0103" in result.stdout and "ArenaVariantRuntime" in result.stdout
    print('PASS complete offline lobby/picker compilation; missing-import control reproduces CS0103.')
