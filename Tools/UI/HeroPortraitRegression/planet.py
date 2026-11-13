"""Exercise the real loading-planet drawing method against a small GUI fixture."""
import argparse
import json
from pathlib import Path
import subprocess
import tempfile

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--dotnet-root',type=Path,required=True)
args=parser.parse_args()
root=Path(__file__).resolve().parents[3]
source=(root/'Assets/Alterverse/Scripts/UI/DimensionUiMotif.cs').read_text()
start=source.index('public static void DrawLoadingPlanet(')
brace=source.index('{',start);end=brace+1;depth=1
while depth:
    if source[end]=='{': depth+=1
    elif source[end]=='}': depth-=1
    end+=1
method=source[start:end]
fixtures=r'''
using UnityEngine;
namespace UnityEngine {
 public enum TextureFormat{RGBA32} public enum HideFlags{HideAndDontSave}
 public enum TextureWrapMode{Clamp} public enum FilterMode{Bilinear}
 public class Texture2D {
  public static int Created;public string name;public HideFlags hideFlags;public TextureWrapMode wrapMode;public FilterMode filterMode;
  public Color32[] Pixels;public Texture2D(int w,int h,TextureFormat f,bool m){Created++;}
  public void SetPixels32(Color32[] p){Pixels=p;}public void Apply(bool a,bool b){}
 }
 public struct Color {public float r,g,b,a;public Color(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;}}
 public struct Color32 {public byte a;public Color32(byte r,byte g,byte b,byte a){this.a=a;}}
 public struct Vector2 {public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}}
 public struct Rect {public float x,y,width,height;public Rect(float x,float y,float w,float h){this.x=x;this.y=y;width=w;height=h;}public Vector2 center=>new(x+width/2,y+height/2);}
 public struct Matrix4x4 {public int value;}
 public static class GUI {public static Color color;public static Matrix4x4 matrix;public static int Draws;public static float LastAlpha;public static void DrawTexture(Rect r,Texture2D t){Draws++;LastAlpha=color.a;}}
 public static class GUIUtility {public static float LastRotation;public static void RotateAroundPivot(float r,Vector2 p){LastRotation=r;GUI.matrix=new(){value=999};}}
 public static class Mathf {
  public static float Sqrt(float v)=>(float)System.Math.Sqrt(v);public static float Sin(float v)=>(float)System.Math.Sin(v);
  public static float Clamp01(float v)=>System.Math.Clamp(v,0,1);public static int RoundToInt(float v)=>(int)System.Math.Round(v);
 }
}
namespace Alterverse.UI {
 public static class DimensionUiMotif {
  private static Texture2D loadingPlanetTex,softGlowTex;
  private static void EnsureMotifTextures(){softGlowTex??=new Texture2D(1,1,TextureFormat.RGBA32,false);}
  public static Texture2D Planet=>loadingPlanetTex;
'''
tail=r'''
 }
}
static class Regression {
 static void Check(bool b,string message){if(!b)throw new System.Exception(message);}
 static void Main(){
  GUI.color=new Color(1,1,1,.4f);GUI.matrix=new Matrix4x4{value=42};
  Alterverse.UI.DimensionUiMotif.DrawLoadingPlanet(new Rect(0,0,0,20),new Color(1,1,1,1),2);
  Check(Texture2D.Created==0&&GUI.Draws==0,"invalid dimensions draw and allocate nothing");
  Alterverse.UI.DimensionUiMotif.DrawLoadingPlanet(new Rect(10,20,150,150),new Color(.5f,.8f,1,1),12);
  var planet=Alterverse.UI.DimensionUiMotif.Planet;
  Check(Texture2D.Created==2&&planet.Pixels.Length==256*256,"smooth planet is generated once");
  Check(planet.Pixels[0].a==0&&planet.Pixels[128*256+128].a==255,"planet has transparent corners and an opaque center");
  Check(GUI.Draws==2&&GUIUtility.LastRotation==12&&System.Math.Abs(GUI.LastAlpha-.38f)<.001f,"planet and glow respect rotation and transition opacity");
  Check(GUI.color.a==.4f&&GUI.matrix.value==42,"planet restores GUI state");
  Alterverse.UI.DimensionUiMotif.DrawLoadingPlanet(new Rect(0,0,100,100),new Color(1,1,1,1),-5);
  Check(Texture2D.Created==2&&GUIUtility.LastRotation==-5,"second planet reuses its texture and rotates independently");
  System.Console.WriteLine("PASS loading planets: smooth texture, rotation, opacity and reuse (controlled fixtures)");
 }
}
'''
runtime=sorted((args.dotnet_root/'shared/Microsoft.NETCore.App').iterdir())[-1]
csc=sorted((args.dotnet_root/'sdk').glob('*/Roslyn/bincore/csc.dll'))[-1]
with tempfile.TemporaryDirectory(prefix='df-loading-planets-') as temp:
    temp=Path(temp);fixture=temp/'PlanetRegression.cs';fixture.write_text(fixtures+method+tail)
    output=temp/'PlanetRegression.dll'
    subprocess.run([str(args.dotnet_root/'dotnet'),str(csc),'-nologo','-langversion:9','-out:'+str(output),
        *['-r:'+str(p) for p in runtime.glob('*.dll')],str(fixture)],check=True)
    output.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{
        'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':runtime.name}}}))
    subprocess.run([str(args.dotnet_root/'dotnet'),str(output)],check=True)
