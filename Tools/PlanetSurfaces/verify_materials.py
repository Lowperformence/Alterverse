#!/usr/bin/env python3
"""Run real new C# profiles/bindings with Unity material/resource test doubles.

Validate packed texture orientation, import contracts and material-specific data.
No scene simulation, renderer/Unity compilation or device-performance claims.
"""
import argparse,json,os,re,subprocess,tempfile
from pathlib import Path
import numpy as np
from PIL import Image
from tree_sitter import Language,Parser
import tree_sitter_c_sharp
ROOT=Path(__file__).resolve().parents[2]
PARSER=Parser(Language(tree_sitter_c_sharp.language()))
STUB=r'''
using System;using System.Collections.Generic;using Alterverse.Visuals;using Alterverse.Match;
namespace Alterverse.Match {public enum ArenaVariant {Moon,Mars,GreenPlanet,Noxara,Kryon,Earth,Pyraxis,Neptune,Dark00,DiamondCore,GJ504b}}
namespace UnityEngine {
 public class Texture2D {public static readonly Texture2D grayTexture=new Texture2D();}
 public class Shader {public bool isSupported=true;}
 public class MeshRenderer {public bool enabled=true;}
 public class GameObject {public static Dictionary<string,GameObject> all=new Dictionary<string,GameObject>();public MeshRenderer renderer;public static GameObject Find(string name){GameObject value;return all.TryGetValue(name,out value)?value:null;}public T GetComponent<T>() where T:class{return renderer as T;}}
 public struct Color {public float r,g,b,a;public Color(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;}}
 public class Material {
  public Shader shader=new Shader();public bool enableInstancing;public Color color;
  public Dictionary<string,object> data=new Dictionary<string,object>();public HashSet<string> keywords=new HashSet<string>();
  public bool HasProperty(string key){return data.ContainsKey(key);}
  public Color GetColor(string key){return (Color)data[key];}
  public void SetColor(string key,Color value){data[key]=value;}
  public void SetFloat(string key,float value){data[key]=value;}
  public void SetTexture(string key,Texture2D value){data[key]=value;}
  public void DisableKeyword(string key){keywords.Remove(key);}
 }
 public static class Resources {public static int reads;public static Dictionary<string,object> assets=new Dictionary<string,object>();public static T Load<T>(string key) where T:class {reads++;object value;return assets.TryGetValue(key,out value)?value as T:null;}}
}
class Regression {
 static int checks;
 static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
 static void Emit(string group,string key,PlanetSurfaceMicroProfile p){Console.WriteLine("PROFILE|"+group+"|"+key+"|"+(p.TextureName??"")+"|"+p.Scale+"|"+p.Relief+"|"+p.Contrast);}
 static void Bind(PlanetSurfaceMicroProfile p) {
  var m=new UnityEngine.Material();var original=m.shader;var color=new UnityEngine.Color(.13f,.24f,.37f,1);m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",.72f);m.SetFloat("_Smoothness",.64f);
  var expected=new UnityEngine.Texture2D();if(p.TextureName!=null)UnityEngine.Resources.assets["SurfaceMicro/"+p.TextureName]=expected;
  PlanetSceneryMaterials.ApplyMicro(m,p);
  Check(m.shader==original,"micro binding changes shader");Check(m.GetColor("_BaseColor").g==color.g,"micro binding changes palette");Check((float)m.data["_Metallic"]==.72f && (float)m.data["_Smoothness"]==.64f,"micro binding changes authored finish");
  Check(m.data["_FineData"]==(p.TextureName!=null?expected:UnityEngine.Texture2D.grayTexture),"micro texture mismatch");
  Check((float)m.data["_FineRelief"]==(p.TextureName!=null?p.Relief:0),"micro relief mismatch");
  UnityEngine.Resources.assets.Clear();PlanetSceneryMaterials.ApplyMicro(m,p);Check((float)m.data["_FineRelief"]==0,"missing texture needs safe disabled fallback");Check(m.data["_FineData"]==UnityEngine.Texture2D.grayTexture,"missing texture fallback not neutral");
 }
 static void Main(){
  string[] expected={"Sand","Sand","Soil","Stone",null,"Soil","Stone","Stone","Stone",null,"Sand"};
  foreach(ArenaVariant planet in Enum.GetValues(typeof(ArenaVariant))){var p=PlanetSurfaceMicroProfile.Ground(planet);Check(p.TextureName==expected[(int)planet],"incorrect planet material: "+planet);Check(p.Relief>=0&&p.Relief<=.2f&&p.Contrast>=0&&p.Contrast<=.5f,"invalid bounds");Bind(p);Emit("ground",planet.ToString(),p);
   for(int layer=0;layer<4;layer++){SceneryFinish f=PlanetSceneryMaterials.LandscapeFinish(planet,layer);Check(Enum.IsDefined(typeof(SceneryFinish),f),"landscape layer mapping missing");}}
  foreach(SceneryFinish finish in Enum.GetValues(typeof(SceneryFinish))){var p=PlanetSurfaceMicroProfile.Scenery(finish);bool quiet=finish==SceneryFinish.Metal||finish==SceneryFinish.Bark||finish==SceneryFinish.Leaf||finish==SceneryFinish.Ice;Check((p.TextureName==null)==quiet,"unexpected organic/metal/ice micro grain");Bind(p);Emit("scenery",finish.ToString(),p);}
  try{PlanetSurfaceMicroProfile.Ground((ArenaVariant)999);Check(false,"unknown planet accepted");}catch(ArgumentOutOfRangeException){Check(true,"unknown planet rejected");}
  try{PlanetSurfaceMicroProfile.Scenery((SceneryFinish)999);Check(false,"unknown finish accepted");}catch(ArgumentOutOfRangeException){Check(true,"unknown finish rejected");}
  PlanetWaterMaterials.Apply(null);
  for(int shaderCase=0;shaderCase<3;shaderCase++)for(int hasBase=0;hasBase<2;hasBase++){
   var m=new UnityEngine.Material();var original=m.shader;var color=new UnityEngine.Color(.025f,.22f,.52f,1);m.color=color;if(hasBase==1)m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.88f);m.keywords.Add("_SPECULARHIGHLIGHTS_OFF");
   var water=new UnityEngine.Shader();water.isSupported=shaderCase!=1;UnityEngine.Resources.assets.Clear();if(shaderCase>0)UnityEngine.Resources.assets["DF_MapWater"]=water;
   PlanetWaterMaterials.Apply(m);
   if(shaderCase==2){Check(m.shader==water,"water shader not applied");Check(m.GetColor("_BaseColor").g==color.g,"water palette changed");Check(m.enableInstancing,"water not instancing compatible");Check(!m.keywords.Contains("_SPECULARHIGHLIGHTS_OFF"),"water specular disabled");Check((float)m.data["_WaveStrength"]==.14f&&(float)m.data["_ShoreStrength"]==.065f,"water strengths drift");}
   else {Check(m.shader==original,"invalid water shader destroys valid material");Check((float)m.data["_Smoothness"]==.88f,"fallback changes material");Check(m.keywords.Contains("_SPECULARHIGHLIGHTS_OFF"),"unsupported shader changes keywords");}
  }
  var floor=new UnityEngine.Material();var floorShader=floor.shader;var tint=new UnityEngine.Color(.64f,.91f,1,.32f);floor.SetColor("_BaseColor",tint);floor.SetFloat("_CoreAlpha",.1088f);floor.SetFloat("_SurfaceRelief",.22f);floor.SetFloat("_ProbeReflection",1f);floor.SetTexture("_SurfaceData",new UnityEngine.Texture2D());floor.SetTexture("_MacroData",new UnityEngine.Texture2D());
  DiamondCoreArenaRuntime.ConfigureClearFloor(null);DiamondCoreArenaRuntime.ConfigureClearFloor(floor);
  Check(floor.shader==floorShader,"clear floor shader changed");Check(floor.GetColor("_BaseColor").a==.32f&&floor.GetColor("_BaseColor").g==.91f,"original crystal tint/alpha changed");Check((float)floor.data["_CoreAlpha"]==.1088f,"original crystal core alpha changed");
  Check((float)floor.data["_SurfaceRelief"]==0&&(float)floor.data["_FacetDetail"]==0&&(float)floor.data["_ProbeReflection"]==0,"floor remains frosted/reflected");Check(floor.data["_SurfaceData"]==null&&floor.data["_MacroData"]==null,"floor retains geological textures");
  foreach(string name in new[]{"FacetedCrystalGroundOverlay","DF_PSRJ1719_SeamlessPrismFloor"}){var go=new UnityEngine.GameObject();go.renderer=new UnityEngine.MeshRenderer();UnityEngine.GameObject.all[name]=go;DiamondCoreFloorSeamFix.DisableOverlay(name);Check(!go.renderer.enabled,"legacy opaque overlay still visible");DiamondCoreFloorSeamFix.DisableOverlay(name);Check(!go.renderer.enabled,"overlay removal not idempotent");}
  DiamondCoreFloorSeamFix.DisableOverlay("missing");UnityEngine.GameObject.all["empty"]=new UnityEngine.GameObject();DiamondCoreFloorSeamFix.DisableOverlay("empty");
  Console.WriteLine("CHECKS|"+checks);
 }
}
'''

def method(path,name):
    tree=PARSER.parse(path.read_bytes());assert not tree.root_node.has_error,path
    stack=[tree.root_node];result=[]
    while stack:
        node=stack.pop()
        if node.type=='method_declaration' and node.child_by_field_name('name').text.decode()==name:result.append(node.text.decode())
        stack.extend(node.children)
    assert len(result)==1,(path,name);return result[0]

def main():
    p=argparse.ArgumentParser();p.add_argument('--mono-root',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args();a.output.mkdir(parents=True,exist_ok=True)
    source=ROOT/'Assets/Alterverse/Scripts/Visuals';scenery=(source/'PlanetSceneryMaterials.cs').read_text();enum=re.search(r'public enum SceneryFinish \{[^}]+\}',scenery).group()
    subset='using System;using UnityEngine;using Alterverse.Match;namespace Alterverse.Visuals {'+enum+'public static class PlanetSceneryMaterials {'+method(source/'PlanetSceneryMaterials.cs','LandscapeFinish')+method(source/'PlanetSceneryMaterials.cs','ApplyMicro')+'}}'
    subset+='namespace Alterverse.Visuals {public class DiamondCoreArenaRuntime {'+method(source/'DiamondCoreArenaRuntime.cs','ConfigureClearFloor')+'}public class DiamondCoreFloorSeamFix {'+method(source/'DiamondCoreFloorSeamFix.cs','DisableOverlay')+'}}'
    env=dict(os.environ,MONO_PATH=str(a.mono_root/'usr/lib/mono/4.5'),MONO_CFG_DIR=str(a.mono_root/'etc'))
    with tempfile.TemporaryDirectory() as d:
        d=Path(d);(d/'Stubs.cs').write_text(STUB);(d/'SceneryBindings.cs').write_text(subset)
        cmd=[str(a.mono_root/'usr/bin/mono-sgen'),str(a.mono_root/'usr/lib/mono/4.5/mcs.exe'),'-langversion:7.2','-out:'+str(d/'test.exe'),str(d/'Stubs.cs'),str(d/'SceneryBindings.cs'),str(source/'PlanetSurfaceMicroProfile.cs'),str(source/'PlanetWaterMaterials.cs')]
        subprocess.run(cmd,env=env,check=True,capture_output=True)
        result=subprocess.check_output([str(a.mono_root/'usr/bin/mono-sgen'),str(d/'test.exe')],env=env,text=True)
    profiles={'ground':{},'scenery':{}};checks=0
    for line in result.splitlines():
        parts=line.split('|')
        if parts[0]=='PROFILE':profiles[parts[1]][parts[2]]={'texture':parts[3] or None,'scale':float(parts[4]),'relief':float(parts[5]),'contrast':float(parts[6])}
        elif parts[0]=='CHECKS':checks=int(parts[1])
    assert len(profiles['ground'])==11 and len(profiles['scenery'])==8 and checks>200
    (a.output/'material-profiles.json').write_text(json.dumps(profiles,indent=2)+'\n')
    textures=[]
    for path in sorted((ROOT/'Assets/Alterverse/Resources/SurfaceMicro').glob('*.png')):
        im=Image.open(path);assert im.mode=='RGBA' and im.size==(256,256)
        raw=np.asarray(im)[::-1].astype(float)/255;h=raw[:,:,3]
        correlations=[]
        for i,axis in enumerate((1,0)):
            slope=np.roll(h,-1,axis)-np.roll(h,1,axis);corr=float(np.corrcoef(slope.ravel(),raw[:,:,i].ravel())[0,1]);assert corr>.90,(path,corr);correlations.append(corr)
        meta=Path(str(path)+'.meta').read_text()
        for text in ('sRGBTexture: 0','enableMipMap: 1','streamingMipmaps: 1','filterMode: 2','wrapU: 0','wrapV: 0','alphaIsTransparency: 0','textureFormat: 4','buildTarget: Android','overridden: 1'):assert text in meta,(path,text)
        textures.append({'material':path.stem,'slopeHeightCorrelations':correlations})
    assert len(textures)==4
    # Confirm that no late forest pass still flattens/overrides the actual arena water material.
    forest=(source/'EarthForestPolish.cs').read_text();assert 'WaterPool' not in forest and '_SPECULARHIGHLIGHTS_OFF' not in forest
    theme=(source/'PlanetArenaThemeRuntime.cs').read_text();assert 'if(name=="DF_Earth_Water")PlanetWaterMaterials.Apply(material);' in theme
    polish=(source/'DiamondCoreMapPolish.cs').read_text();assert 'BuildFacetGroundMesh' not in polish and 'MakeFloorFacetMaterial' not in polish
    seam=(source/'DiamondCoreFloorSeamFix.cs').read_text();assert 'BuildMesh' not in seam and 'new Material' not in seam and 'new Mesh' not in seam
    runtime=(source/'DiamondCoreArenaRuntime.cs').read_text();assert 'ConfigureClearFloor(floorMaterial);' in runtime
    report={'productionCSharpAssertions':checks,'groundProfiles':11,'sceneryProfiles':8,'textures':textures,'scope':__doc__.strip()}
    (a.output/'material-validation.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report,indent=2))

if __name__=='__main__':main()
