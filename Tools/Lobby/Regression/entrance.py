"""Exercise actual lobby opening/render guard with controlled GUI failures."""
from pathlib import Path
import json,subprocess,tempfile

def method(text,declaration):
    start=text.index(declaration);end=text.index('{',start)+1;depth=1
    while depth:
        depth+=(text[end]=='{')-(text[end]=='}');end+=1
    return text[start:end]

def run(dotnet,root):
    source=(root/'Assets/Alterverse/Scripts/UI/OnlinePartyOverlay.cs').read_text()
    header=source[source.index('namespace Alterverse.UI'):source.index('        [RuntimeInitializeOnLoadMethod')]
    body='\n'.join(method(source,d) for d in ['private static void AutoCreate()','public static void Show()',
        'public static void Hide()','private void Awake()','public static void DrawFromMenu()','private void Draw()'])
    boundary='''
public static bool FailDraw,ExitDraw;public static int Draws;
private void DrawOverlay(float width,OnlinePartyService online,EdgegapOnlineFlow edgegap)
{Draws++;GUI.color=new Color();GUI.matrix=new Matrix4x4();if(ExitDraw)throw new ExitGUIException();if(FailDraw)throw new Exception("fixture failure");}
private void DrawHeroPicker(float w,OnlinePartyService o,EdgegapOnlineFlow e){}
private void DrawInvitations(float w,OnlinePartyService o,EdgegapOnlineFlow e){}
private void DrawVoiceConsent(float w){}
}}
'''
    fixtures='''
using System;using System.Reflection;using Alterverse.UI;using UnityEngine;
namespace UnityEngine {
public class Object {public static void Destroy(object o){}public static void DontDestroyOnLoad(object o){}}
public class MonoBehaviour:Object {public GameObject gameObject;}
public class GameObject {public GameObject(string name){}public T AddComponent<T>()where T:MonoBehaviour,new(){var c=new T{gameObject=this};typeof(T).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic)?.Invoke(c,null);return c;}}
public struct Vector2{}public struct Vector3 {public Vector3(float x,float y,float z){}public static Vector3 one=>new();public static Vector3 operator*(Vector3 v,float x)=>v;}
public struct Quaternion {public static Quaternion identity=>new();}
public struct Matrix4x4 {public int Marker;public static Matrix4x4 TRS(Vector3 p,Quaternion q,Vector3 s)=>new();}
public struct Color {public int Marker;public Color(float r,float g,float b,float a){Marker=0;}public static Color white=>new();}
public struct Rect {public float x,y,width,height;public float yMax=>y+height;}
public static class Screen {public static Rect safeArea=new(){width=1500,height=720};public static int height=720;}
public static class Mathf {public static float Min(float a,float b)=>Math.Min(a,b);}
public class TouchScreenKeyboard{}public class ExitGUIException:Exception{}
public static class GUI {public static Matrix4x4 matrix;public static Color color;public static bool enabled;public static int depth;}
public static class Debug {public static int Errors;public static void LogException(Exception e){Errors++;}}
}
namespace Alterverse.Multiplayer {
public class OnlinePartyService {public static OnlinePartyService Instance;public bool InParty;public static void EnsureAvailable(){Instance??=new();}}
public class CoopLobbyService {public static CoopLobbyService Instance;public bool InLobby;}
public class EdgegapOnlineFlow {public static EdgegapOnlineFlow Instance;}
public class PartyVoiceService {public static PartyVoiceService Instance;public bool NeedsConsent;}
}
namespace Alterverse.UI {
public class FrontEndTheme{}
public static class MainMenuController {public static bool IsVisible=true,ReviewPromptVisible;}
public static class OnlineMatchLoadingScreen {public static bool IsVisible;}
}
static class EntranceRegression {
static void Check(bool value,string label){if(!value)throw new Exception(label);}
static void Draw()=>OnlinePartyOverlay.DrawFromMenu();
static void Main(){
Check(Alterverse.Multiplayer.OnlinePartyService.Instance==null,"service starts absent");
OnlinePartyOverlay.Show();Check(OnlinePartyOverlay.IsOpen&&Alterverse.Multiplayer.OnlinePartyService.Instance!=null,"opening creates missing service");
GUI.matrix=new(){Marker=7};GUI.color=new(){Marker=8};GUI.enabled=false;GUI.depth=19;
OnlinePartyOverlay.FailDraw=true;Draw();
Check(!OnlinePartyOverlay.IsOpen&&OnlinePartyOverlay.LastError.Length>0&&Debug.Errors==1,"draw failure returns control to menu and reports error");
Check(GUI.matrix.Marker==7&&GUI.color.Marker==8&&!GUI.enabled&&GUI.depth==19,"GUI state restored after failure");
OnlinePartyOverlay.FailDraw=false;OnlinePartyOverlay.Show();Draw();
Check(OnlinePartyOverlay.IsOpen&&OnlinePartyOverlay.LastError=="","retry restores lobby");
OnlinePartyOverlay.ExitDraw=true;bool exited=false;try{Draw();}catch(ExitGUIException){exited=true;}
Check(exited&&OnlinePartyOverlay.IsOpen,"Unity GUI control exit is propagated");OnlinePartyOverlay.ExitDraw=false;
Alterverse.Multiplayer.OnlinePartyService.Instance=null;Check(!OnlinePartyOverlay.IsOpen,"missing service cannot hide underlying menu");
Console.WriteLine("PASS 7 lobby-entry and GUI recovery checks (controlled engine boundary).");
}}
'''
    with tempfile.TemporaryDirectory(prefix='df-lobby-entry-') as folder:
        folder=Path(folder);(folder/'Overlay.cs').write_text('using System;using UnityEngine;using Alterverse.Multiplayer;\n'+header+body+boundary)
        (folder/'Fixtures.cs').write_text(fixtures)
        runtime=sorted((dotnet/'shared/Microsoft.NETCore.App').iterdir())[-1]
        csc=sorted((dotnet/'sdk').glob('*/Roslyn/bincore/csc.dll'))[-1]
        output=folder/'Entry.dll';rsp=folder/'compile.rsp'
        rsp.write_text('\n'.join(['-nologo','-nowarn:0169,0649','-langversion:9','-out:'+str(output),
            *['-r:'+str(x) for x in runtime.glob('*.dll')],str(folder/'Overlay.cs'),str(folder/'Fixtures.cs')]))
        subprocess.run([str(dotnet/'dotnet'),str(csc),'@'+str(rsp)],check=True)
        output.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':runtime.name}}}))
        subprocess.run([str(dotnet/'dotnet'),str(output)],check=True)
