"""Compile the production combat layout and ping rectangles against engine fixtures."""
import argparse
import json
import re
from pathlib import Path
import subprocess
import tempfile

p=argparse.ArgumentParser()
p.add_argument('--dotnet-root',type=Path,required=True)
args=p.parse_args()
root=Path(__file__).resolve().parents[3]
scripts=root/'Assets/Alterverse/Scripts'
hud=(scripts/'UI/CombatFeedbackHUD.cs').read_text()
start=hud.index('private static Rect GetPingButtonRect(')
brace=hud.index('{',start);end=brace+1;depth=1
while depth:
    if hud[end]=='{': depth+=1
    elif hud[end]=='}': depth-=1
    end+=1
method=hud[start:end].replace('private static','public static',1)
score=(scripts/'Match/MatchScoreboard.cs').read_text()
settings=(scripts/'UI/InMatchSettingsMenu.cs').read_text()
score_rect=re.search(r'Rect scoreBtn\s*=\s*(new Rect\([^;]+\));',score).group(1).replace('HudWidth','width')
gear_rect=re.search(r'Rect settingsButton\s*=\s*(new Rect\([^;]+\));',settings).group(1)
catalog=json.loads((root/'Assets/Alterverse/Resources/Localization/InterfaceLanguages.json').read_text())
indices=[catalog['keys'].index(k) for k in ('ATTACK','RALLY','DEFEND')]
assert len(set(catalog['keys']))==len(catalog['keys'])
for lang in catalog['languages']:
    assert len(lang['words'])==len(catalog['keys']) and all(lang['words'][i].strip() for i in indices),lang['code']
assert [next(l for l in catalog['languages'] if l['code']==c)['words'][i] for c,i in [('de',indices[0]),('de',indices[1]),('de',indices[2])]]==['ANGRIFF','SAMMELN','SCHUTZ']
for key in ('ATTACK','RALLY','DEFEND'):
    assert hud.count('GameLanguage.Text("'+key+'")')>=2,key
assert '⚔ ANGRIFF [1]' not in hud

fixture=r'''
using System;using UnityEngine;using Alterverse.UI;
namespace UnityEngine {
 public struct Vector2 {public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}}
 public struct Vector3 {public Vector3(float x,float y,float z){}public static Vector3 one=>new();public static Vector3 operator*(Vector3 a,float b)=>a;}
 public struct Quaternion {public static Quaternion identity=>new();}
 public struct Matrix4x4 {public static Matrix4x4 TRS(Vector3 a,Quaternion b,Vector3 c)=>new();}
 public struct Rect {
  public float x,y,width,height; public Rect(float x,float y,float w,float h){this.x=x;this.y=y;width=w;height=h;}
  public float xMin {get=>x;set{width+=x-value;x=value;}} public float yMin {get=>y;set{height+=y-value;y=value;}}
  public float xMax {get=>x+width;set=>width=value-x;} public float yMax {get=>y+height;set=>height=value-y;}
  public bool Contains(Vector2 p)=>p.x>=x&&p.x<xMax&&p.y>=y&&p.y<yMax;
  public static Rect MinMaxRect(float x,float y,float xmax,float ymax)=>new(x,y,xmax-x,ymax-y);
 }
 public static class Mathf {
  public static float Max(float a,float b)=>Math.Max(a,b);public static float Min(float a,float b)=>Math.Min(a,b);
  public static float Clamp(float n,float min,float max)=>Math.Clamp(n,min,max);
 }
 public static class Screen {public static float width,height;}
}
static class Rects {
'''
tail=r'''
}
static class Test {
 static void Check(bool value,string label){if(!value)throw new Exception(label);}
 static bool Overlap(Rect a,Rect b)=>a.x<b.xMax&&a.xMax>b.x&&a.y<b.yMax&&a.yMax>b.y;
 static Rect Project(Rect r,float s)=>new(r.x*s,r.y*s,r.width*s,r.height*s);
 static void TestSize(int w,int h){
  Screen.width=w;Screen.height=h;
  var layout=new CombatHudLayout(new Rect(0,0,w,h));
  for(int i=0;i<3;i++){
   Rect ping=Rects.GetPingButtonRect(layout,i);
   Check(ping.x>=0&&ping.xMax<=layout.Width&&ping.y>=0&&ping.yMax<=layout.Height,"ping inside safe area");
   Check(ping.Contains(new Vector2(ping.x+ping.width/2,ping.y+ping.height/2)),"ping hit area");
   for(int j=0;j<5;j++)Check(!Overlap(ping,layout.Action(j)),"ping overlaps an ability");
   for(int j=0;j<i;j++)Check(!Overlap(ping,Rects.GetPingButtonRect(layout,j)),"ping controls overlap");
  }
  float scoreScale=Math.Min(h/720f,w/960f), hudWidth=w/scoreScale;
  var score=Project(Rects.Score(hudWidth),scoreScale);
  var gear=Project(Rects.Gear(layout),layout.Scale);
  var front=Project(new Rect((hudWidth-420f)/2,16,420,82),scoreScale);
  Check(!Overlap(score,gear)&&!Overlap(score,front),"score button must clear gear and front bar");
  Check(score.x>=0&&score.xMax<=w&&gear.x>=0&&gear.xMax<=w,"top controls inside safe area");
  Check(gear.width>=46*layout.Scale,"settings touch area enlarged");
 }
 static void Main(){
  TestSize(960,540);TestSize(1280,720);TestSize(1920,1080);
  TestSize(2220,1080);TestSize(1440,1080);Console.WriteLine("PASS HUD geometry and touch areas across landscape sizes");
 }
}
'''
methods='public static Rect Score(float width)=>'+score_rect+';\npublic static Rect Gear(CombatHudLayout layout)=>'+gear_rect+';\n'
runtime=sorted((args.dotnet_root/'shared/Microsoft.NETCore.App').iterdir())[-1]
csc=sorted((args.dotnet_root/'sdk').glob('*/Roslyn/bincore/csc.dll'))[-1]
with tempfile.TemporaryDirectory(prefix='df-hud-layout-') as tmp:
    tmp=Path(tmp);source=tmp/'Test.cs';source.write_text(fixture+method+methods+tail)
    out=tmp/'Test.dll'
    subprocess.run([str(args.dotnet_root/'dotnet'),str(csc),'-nologo','-langversion:9','-out:'+str(out),
        *['-r:'+str(x) for x in runtime.glob('*.dll')],str(source),str(scripts/'UI/CombatHudLayout.cs')],check=True)
    out.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':runtime.name}}}))
    subprocess.run([str(args.dotnet_root/'dotnet'),str(out)],check=True)
print('PASS tactical translations for all configured languages')
