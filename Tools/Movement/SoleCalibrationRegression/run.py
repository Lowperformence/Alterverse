#!/usr/bin/env python3
"""Compile unchanged calibration methods and reproduce the old unsafe height path."""
import os, pathlib, shutil, subprocess, tempfile, argparse
HERE=pathlib.Path(__file__).resolve().parent
ROOT=HERE.parents[2]
def extract(text,token):
 start=text.index(token); brace=text.index('{',start);depth=1;i=brace+1
 while depth:
  if text[i]=='{':depth+=1
  elif text[i]=='}':depth-=1
  i+=1
 return text[start:i]
p=argparse.ArgumentParser();p.add_argument('--before',default=str(HERE/'BeforeMethods.cs'));p.add_argument('--mono-root');args=p.parse_args()
source=(ROOT/'Assets/Alterverse/Scripts/Visuals/HeroGroundedLocomotion.cs').read_text();before=pathlib.Path(args.before).read_text()
imports='using System;using System.Collections.Generic;using UnityEngine;using Alterverse.Visuals;\n'
ground='private Vector3 Nominal(Leg leg){return leg.rest;} private Vector3 Ground(Vector3 point,Leg leg,out Vector3 normal){normal=Vector3.up;point.y=-.02f+leg.sole;return point;}\n'
methods=['private sealed class Leg','private void CaptureSoles','private static bool FootWeighted','private static Vector3 SkinVertex','private static bool CaptureSolePoint','private static float SoleExtent','private void PrepareFoot','private void PlanAuthored','private void StabilizeAuthoredTarget','private void FitAuthoredTarget']
current=imports+'class CurrentSoleHarness {\nprivate HeroVisualMotion motion; private Leg left,right; private string name="TestActor";\n'+'\n'.join(extract(source,m) for m in methods)+'\n'+ground+(HERE/'Regression.cs').read_text()+'\n}'
old=imports+'class OldSoleHarness {\nprivate HeroVisualMotion motion;\n'+'\n'.join(extract(before,m) for m in ['private sealed class Leg','private static float SoleExtent','private void PrepareFoot','private void PlanAuthored','private void StabilizeAuthoredTarget'])+ground+'''\npublic static float FloatingPlant(){var h=new OldSoleHarness();h.motion=new HeroVisualMotion{PlanarSpeed=2,RunBlend=1};var leg=new Leg{foot=new GameObject("oldPlant").transform,length=1.2f,sole=.08f,authoredSample=true,contact=true,initialized=true,plant=Vector3.up*.06f,target=Vector3.up*.06f};h.StabilizeAuthoredTarget(leg,new Vector3(0,2,0),1f/60);h.PlanAuthored(leg,Vector3.zero,Vector3.zero,true,Quaternion.identity,0);return leg.plant.y;}
public static float Reproduce(){var h=new OldSoleHarness();h.motion=new HeroVisualMotion{PresentedRotation=Quaternion.identity,LocomotionWeight=1};var foot=new GameObject("oldFoot").transform;var leg=new Leg{foot=foot,length=1.2f,sole=.08f,target=Vector3.up*.06f,normal=Vector3.up,authoredSample=true,authoredRotation=Quaternion.identity,solePoints=new[]{new Vector3(0,-2.4f,0)}};h.PrepareFoot(leg,.6f,false);return leg.target.y;}\n}'''
env=dict(os.environ)
if args.mono_root:
 mono=pathlib.Path(args.mono_root)
 runtime=str(mono/'usr/bin/mono-sgen')
 compiler=[runtime,str(mono/'usr/lib/mono/4.5/mcs.exe')]
 env.update(MONO_PATH=str(mono/'usr/lib/mono/4.5'),MONO_CFG_DIR=str(mono/'etc'))
else:
 runtime=shutil.which('mono'); mcs=shutil.which('mcs')
 if not runtime or not mcs: p.error('Mono runtime/compiler missing: install mono and mcs or provide --mono-root PATH')
 compiler=[mcs]
with tempfile.TemporaryDirectory(prefix='sole-regression-') as folder:
 tmp=pathlib.Path(folder);(tmp/'Current.cs').write_text(current);(tmp/'Old.cs').write_text(old)
 command=compiler+['-r:System.Numerics.dll','-nowarn:0649','-out:'+str(tmp/'Regression.exe'),str(HERE/'EngineStubs.cs'),str(tmp/'Current.cs'),str(tmp/'Old.cs')]
 subprocess.run(command,env=env,check=True)
 subprocess.run([runtime,str(tmp/'Regression.exe')],env=env,check=True)
