"""Compile production friend/gift client and exercise transport/retry/account isolation."""
import argparse
import json
import os
from pathlib import Path
import subprocess
import tempfile

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--dotnet-root',type=Path,required=True)
args=parser.parse_args();here=Path(__file__).resolve().parent;root=here.parents[2]
sdk=sorted((args.dotnet_root/'sdk').iterdir())[-1]
runtime=sorted((args.dotnet_root/'shared/Microsoft.NETCore.App').iterdir())[-1]
engine=(root/'Tools/ClanChat/Regression/EngineStubs.cs').read_text()
engine=engine.replace('public void StartCoroutine(IEnumerator e){}','public static IEnumerator Pending;public void StartCoroutine(IEnumerator e){Pending=e;}')
engine=engine.replace('public static class Mathf {','public static class Mathf {public const float PI=(float)Math.PI;public static float Sqrt(float f)=>(float)Math.Sqrt(f);public static float Abs(float f)=>Math.Abs(f);public static float Atan2(float a,float b)=>(float)Math.Atan2(a,b);public static float Cos(float f)=>(float)Math.Cos(f);public static float Sin(float f)=>(float)Math.Sin(f);')
engine=engine.replace('public class Texture2D {public static Texture2D whiteTexture=new();}','public enum TextureFormat{RGBA32}public enum FilterMode{Bilinear}public enum TextureWrapMode{Clamp}public class Texture2D {public static Texture2D whiteTexture=new();public string name;public FilterMode filterMode;public TextureWrapMode wrapMode;public Texture2D(){}public Texture2D(int w,int h,TextureFormat f,bool m){}public void SetPixels(Color[] c){}public void Apply(bool a=false,bool b=false){}}public struct Color {public float a;public Color(float r,float g,float b,float a=1){this.a=a;}public static Color white=>new();public static Color Lerp(Color a,Color b,float t)=>a;public static Color operator*(Color c,float f)=>c;}')
engine=engine.replace('public string name;public FilterMode', 'public HideFlags hideFlags;public string name;public FilterMode')
engine=engine.replace('public void SetPixels(Color[] c){}', 'public void SetPixels(Color[] c){}public void SetPixels32(Color32[] c){}')
engine+='\nnamespace UnityEngine {public enum HideFlags{HideAndDontSave}public struct Color32 {public Color32(byte r,byte g,byte b,byte a){}public static implicit operator Color32(Color c)=>new();}}\n'
with tempfile.TemporaryDirectory(prefix='df-friend-gifts-') as directory:
    target=Path(directory);(target/'Engine.cs').write_text(engine)
    output=target/'FriendGifts.dll';response=target/'compile.rsp'
    names=['Profile/SocialService.cs','Profile/FriendStickerService.cs','Profile/SafeIdentityCatalog.cs']
    response.write_text('\n'.join(['-nologo','-nowarn:0649,0414','-langversion:9','-out:'+str(output),*['-r:'+str(path) for path in runtime.glob('*.dll')],str(target/'Engine.cs'),str(here/'Regression.cs'),*[str(root/'Assets/Alterverse/Scripts'/name) for name in names]]))
    subprocess.run([str(args.dotnet_root/'dotnet'),str(sdk/'Roslyn/bincore/csc.dll'),'@'+str(response)],check=True)
    output.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':runtime.name}}}))
    subprocess.run([str(args.dotnet_root/'dotnet'),str(output)],env=dict(os.environ,DF_TEST_IDENTITY_CATALOG=str(root/'Assets/Alterverse/Resources/Social/SafeIdentityCatalog.json')),check=True)
