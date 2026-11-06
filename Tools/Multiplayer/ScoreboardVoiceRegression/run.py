"""Compile the real scoreboard voice-button method with controlled UI and voice fixtures."""
import argparse,json,subprocess,tempfile
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--dotnet-root',type=Path,required=True);a=p.parse_args()
root=Path(__file__).resolve().parents[3]
score=(root/'Assets/Alterverse/Scripts/Match/MatchScoreboard.cs').read_text()
result=(root/'Assets/Alterverse/Scripts/UI/MatchResultScreen.cs').read_text()
start=score.index('private void DrawTeamSocialButtons(');end=score.index('{',start)+1;depth=1
while depth:
    depth+=(score[end]=='{')-(score[end]=='}');end+=1
method=score[start:end]
assert 'PlayerId = rSlot != null && rSlot.IsHuman ? rSlot.PlayerId : null' in score
assert 'TryGetVoicePlayer(row.PlayerId' in result and 'SetPlayerMuted(row.PlayerId' in result
assert 'ToggleMutePlayer(row.Name)' not in result and 'SyncVoiceMuteByName' not in score
fixture='''
using System;using System.Collections.Generic;
using UnityEngine;using Alterverse.Multiplayer;using Alterverse.Characters;using Alterverse.Profile;
namespace UnityEngine {public struct Rect {public float x,y,width,height;public Rect(float x,float y,float w,float h){this.x=x;this.y=y;width=w;height=h;}}public static class Mathf {public static int Max(int a,int b)=>Math.Max(a,b);}}
namespace Alterverse.Characters {public enum HeroId {Veyr}public class HeroIdentity {public HeroId Hero;}}
namespace Alterverse.Multiplayer {
 public class CoopHeroSlot {public bool IsHuman,IsLocalHuman;public string PlayerId;}
 public class PartyVoiceService {
  public static PartyVoiceService Instance;public struct VoicePlayer {public bool Muted,Pending;}
  public readonly Dictionary<string,VoicePlayer> Players=new();public string LastId;public bool LastValue;public int Calls;
  public bool TryGetVoicePlayer(string id,out VoicePlayer player){player=default;return id!=null&&Players.TryGetValue(id,out player);}
  public bool SetPlayerMuted(string id,bool value){LastId=id;LastValue=value;Calls++;return true;}
 }
}
namespace Alterverse.Profile {public static class PlayerExperience {public static int Level=1;}public static class FriendStickerService {
 public static bool IsFriendOrRequested(string s)=>false;public static void RequestFriendFromScoreboard(string n,HeroId h,int l){}
}}
public class Health {public CoopHeroSlot Slot;public T GetComponent<T>() where T:class=>typeof(T)==typeof(CoopHeroSlot)?Slot as T:null;}
public class Theme {
 public float ClickY=-1;public bool Enabled;public string Label;public int Buttons;
 public bool Action(Rect r,string label,bool primary=false,bool selected=false,bool enabled=true){if(label=="MUTE"||label=="MUTED"){Buttons++;if(r.y==ClickY){Enabled=enabled;Label=label;return enabled;}}return false;}
}
public class TestBoard {
 class Entry {public bool BoardOwn;public string Name;public Health Health;}
 Theme boardTheme=new();
 METHOD
 public static void Main(){
  var b=new TestBoard();var voice=PartyVoiceService.Instance=new();
  voice.Players["a"]=new();voice.Players["b"]=new();
  var a=new Entry{Name="Same",Health=new Health{Slot=new CoopHeroSlot{PlayerId="a",IsHuman=true}}};
  var c=new Entry{Name="Same",Health=new Health{Slot=new CoopHeroSlot{PlayerId="b",IsHuman=true}}};
  var bot=new Entry{Name="Bot",Health=new Health{Slot=new CoopHeroSlot{PlayerId="bot",IsHuman=false}}};
  var unknown=new Entry{Name="Other",Health=new Health{Slot=new CoopHeroSlot{PlayerId="c",IsHuman=true}}};
  var self=new Entry{Name="Self",BoardOwn=true,Health=new Health{Slot=new CoopHeroSlot{PlayerId="self",IsHuman=true,IsLocalHuman=true}}};
  var rows=new List<Entry>{a,c,bot,unknown,self};
  b.boardTheme.ClickY=81;b.DrawTeamSocialButtons(rows,0,0,400);
  if(voice.Calls!=1||voice.LastId!="b"||!voice.LastValue)throw new Exception("wrong same-name player muted");
  voice.Players["b"]=new(){Muted=true};b.DrawTeamSocialButtons(rows,0,0,400);
  if(voice.Calls!=2||voice.LastId!="b"||voice.LastValue||b.boardTheme.Label!="MUTED")throw new Exception("unmute target or confirmed label wrong");
  voice.Players["b"]=new(){Pending=true};b.DrawTeamSocialButtons(rows,0,0,400);
  if(voice.Calls!=2||b.boardTheme.Enabled)throw new Exception("pending operation clickable");
  b.boardTheme.ClickY=123;b.DrawTeamSocialButtons(rows,0,0,400);if(voice.Calls!=2||b.boardTheme.Enabled)throw new Exception("bot clickable");
  b.boardTheme.ClickY=165;b.DrawTeamSocialButtons(rows,0,0,400);if(voice.Calls!=2||b.boardTheme.Enabled)throw new Exception("offline player clickable");
  b.boardTheme.ClickY=207;b.boardTheme.Buttons=0;b.DrawTeamSocialButtons(rows,0,0,400);if(voice.Calls!=2||b.boardTheme.Buttons!=4)throw new Exception("self row has mute action");
  Console.WriteLine("PASS: scoreboard row actions use unique player ID, confirmed state, pending lock, and exclude self, bots and offline voice.");
 }
}
'''.replace('METHOD',method)
sdk=sorted((a.dotnet_root/'sdk').glob('*/Roslyn/bincore/csc.dll'))[-1]
runtime=sorted((a.dotnet_root/'shared/Microsoft.NETCore.App').iterdir())[-1]
with tempfile.TemporaryDirectory(prefix='df-score-voice-') as d:
 d=Path(d);src=d/'Test.cs';src.write_text(fixture);out=d/'Test.dll';rsp=d/'args.rsp'
 rsp.write_text('\n'.join(['-nologo','-langversion:9','-out:'+str(out),*['-r:'+str(p) for p in runtime.glob('*.dll')],str(src)]))
 subprocess.run([str(a.dotnet_root/'dotnet'),str(sdk),'@'+str(rsp)],check=True)
 out.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':runtime.name}}}))
 subprocess.run([str(a.dotnet_root/'dotnet'),str(out)],check=True)
