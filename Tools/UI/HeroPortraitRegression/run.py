"""Test the actual portrait queue methods with controlled renderer/engine fixtures."""
import argparse
import json
from pathlib import Path
import subprocess
import tempfile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--dotnet-root', type=Path, required=True)
args = parser.parse_args()
root = Path(__file__).resolve().parents[3]
source = (root / 'Assets/Alterverse/Scripts/Visuals/StylizedHeroCard.cs').read_text()


def block(signature):
    start = source.index(signature)
    brace = source.index('{', start)
    depth = 1
    end = brace + 1
    while depth:
        if source[end] == '{':
            depth += 1
        elif source[end] == '}':
            depth -= 1
        end += 1
    return source[start:end]


fields = source[source.index('private static readonly Dictionary<HeroId, Texture2D> PortraitCache'):
                source.index('internal static void ProcessOnePortrait()')]
methods = '\n'.join(block(signature) for signature in [
    'internal static void ProcessOnePortrait()', 'private static void ResetCache()',
    'public static void PrepareRoster()', 'public static bool RosterReady',
    'private static Texture2D GetOrRenderPortrait(HeroId hero)'])
fixtures = r'''
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Alterverse.Characters;
namespace UnityEngine {
 public class Object {public static void Destroy(Object o){}public static void DontDestroyOnLoad(Object o){}}
 public class Texture2D:Object{} public class Shader:Object{}
 public class GameObject:Object {public GameObject(string name){}public T AddComponent<T>() where T:new()=>new T();}
 public static class Application {public static bool isBatchMode;}
 public static class Time {public static float unscaledTime;}
 public static class SystemInfo {public static GraphicsDeviceType graphicsDeviceType=GraphicsDeviceType.Normal;}
}
namespace UnityEngine.Rendering {public enum GraphicsDeviceType{Normal,Null}}
namespace Alterverse.Characters {public enum HeroId{One,Two,Three}}
namespace Alterverse.Visuals {
 public class HeroPortraitQueue{}
 public static class StylizedHeroCard {
'''
tail = r'''
  public static int renders,backdrops;public static bool failSecondOnce=true;
  private static Texture2D GetOriginBackdrop(HeroId h){backdrops++;return new Texture2D();}
  private static Texture2D RenderHeroBustPortrait(HeroId h){renders++;if(h==HeroId.Two&&failSecondOnce){failSecondOnce=false;return null;}return new Texture2D();}
  public static int PendingCount=>Pending.Count;
  public static void ResetForTest()=>ResetCache();
 }
}
static class Regression {
 static void Check(bool b,string message){if(!b)throw new System.Exception(message);}
 static void Main(){
  UnityEngine.Application.isBatchMode=true;Alterverse.Visuals.StylizedHeroCard.PrepareRoster();
  Check(Alterverse.Visuals.StylizedHeroCard.PendingCount==0,"headless mode has no graphics work");
  UnityEngine.Application.isBatchMode=false;Alterverse.Visuals.StylizedHeroCard.PrepareRoster();
  Alterverse.Visuals.StylizedHeroCard.PrepareRoster();
  Check(Alterverse.Visuals.StylizedHeroCard.PendingCount==3&&Alterverse.Visuals.StylizedHeroCard.renders==0,"each hero is queued once and preparation never renders on the caller");
  Alterverse.Visuals.StylizedHeroCard.ProcessOnePortrait();
  Check(Alterverse.Visuals.StylizedHeroCard.renders==1&&!Alterverse.Visuals.StylizedHeroCard.RosterReady,"one portrait per frame and incomplete roster stays hidden");
  Alterverse.Visuals.StylizedHeroCard.ProcessOnePortrait();Alterverse.Visuals.StylizedHeroCard.ProcessOnePortrait();
  Check(!Alterverse.Visuals.StylizedHeroCard.RosterReady,"failed render does not claim readiness");
  Alterverse.Visuals.StylizedHeroCard.PrepareRoster();
  Check(Alterverse.Visuals.StylizedHeroCard.PendingCount==0,"failed portrait observes retry delay");
  UnityEngine.Time.unscaledTime=1f;Alterverse.Visuals.StylizedHeroCard.PrepareRoster();
  Check(Alterverse.Visuals.StylizedHeroCard.PendingCount==1,"failed portrait is retried");
  Alterverse.Visuals.StylizedHeroCard.ProcessOnePortrait();
  Check(Alterverse.Visuals.StylizedHeroCard.RosterReady&&Alterverse.Visuals.StylizedHeroCard.renders==4&&Alterverse.Visuals.StylizedHeroCard.backdrops==4,"all portraits and backdrops prepared");
  Alterverse.Visuals.StylizedHeroCard.ResetForTest();
  Check(!Alterverse.Visuals.StylizedHeroCard.RosterReady&&Alterverse.Visuals.StylizedHeroCard.PendingCount==0,"reset clears readiness and queue");
  UnityEngine.SystemInfo.graphicsDeviceType=UnityEngine.Rendering.GraphicsDeviceType.Null;
  Alterverse.Visuals.StylizedHeroCard.PrepareRoster();
  Check(Alterverse.Visuals.StylizedHeroCard.PendingCount==0,"null renderer does not start a worker");
  System.Console.WriteLine("PASS portrait queue, failure and retry (controlled fixtures)");
 }
}
'''

runtime = sorted((args.dotnet_root / 'shared/Microsoft.NETCore.App').iterdir())[-1]
csc = sorted((args.dotnet_root / 'sdk').glob('*/Roslyn/bincore/csc.dll'))[-1]
with tempfile.TemporaryDirectory(prefix='df-portraits-') as temp:
    temp = Path(temp)
    test = temp / 'PortraitRegression.cs'
    test.write_text(fixtures + fields + methods + tail)
    output = temp / 'PortraitRegression.dll'
    command = [str(args.dotnet_root / 'dotnet'), str(csc), '-nologo', '-langversion:9',
               '-nowarn:0169,0414', '-out:' + str(output),
               *['-r:' + str(path) for path in runtime.glob('*.dll')], str(test)]
    subprocess.run(command, check=True)
    output.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {
        'tfm': 'net8.0', 'framework': {'name': 'Microsoft.NETCore.App', 'version': runtime.name}}}))
    subprocess.run([str(args.dotnet_root / 'dotnet'), str(output)], check=True)
