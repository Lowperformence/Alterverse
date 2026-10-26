#!/usr/bin/env python3
"""Compile unchanged production aiming/attack/input/network code with deterministic engine fixtures.
This is a C# regression harness, not a Unity build or a device test.
"""
import argparse, os, subprocess, tempfile
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--mono-root',type=Path,required=True)
p.add_argument('--roslyn',type=Path,required=True,help='Path to Roslyn csc.exe (C# 9 or newer)')
p.add_argument('--connection-timing',action='store_true',help='Print release-to-first-effect CSV for all attacks at 30/60/120 FPS')
p.add_argument('--movement-dashes',action='store_true',help='Run movement-tap, manual-aim, multitouch, leap and network checks')
a=p.parse_args(); here=Path(__file__).resolve().parent; root=here.parents[2]; src=root/'Assets/Alterverse/Scripts'
names=['Visuals/HeroPresentationAnchor.cs','Visuals/HeroVfxEvents.cs','Visuals/HeroVfxPool.cs','Visuals/HeroVfxPool.Textures.cs','Visuals/HeroSkillVfx.Timmy.cs','Audio/HeroCombatAudio.cs','Audio/CombatSoundPalette.cs','Combat/AimingSettings.cs','Combat/AimGeometry.cs','Combat/ManualAiming.cs','Combat/HeroAttackDelivery.cs','Combat/HeroCombatRules.cs','Combat/HeroCombatStatus.cs','Combat/SoftTargeting.cs','Combat/BasicAttack.cs','Characters/HeroAbilityKit.cs','Characters/HeroIdentity.cs','Characters/HeroCombatProfile.cs','Characters/TimmyBalance.cs', 'Characters/Bolt7Balance.cs','Characters/HeroAimPrediction.cs','Characters/HeroAbilityExecution.cs','Characters/HeroAbilityExecution.CyberShark.cs','Characters/HeroAbilityExecution.Timmy.cs','Characters/PlayerHeroAbilities.cs','Multiplayer/CoopGameplayCommands.cs','UI/CombatHudLayout.cs','UI/PrototypeHUD.cs','UI/AbilityRangePreviewOverlay.cs','UI/HeroAttackCatalog.cs']
env=dict(os.environ,MONO_PATH=str(a.mono_root/'usr/lib/mono/4.5')+':'+str(a.mono_root/'usr/lib/mono/4.5/Facades'),MONO_CFG_DIR=str(a.mono_root/'etc'),LD_LIBRARY_PATH=str(a.mono_root/'usr/lib')+':'+str(a.mono_root/'usr/lib/x86_64-linux-gnu'))
mono=a.mono_root/'usr/bin/mono-sgen'
with tempfile.TemporaryDirectory(prefix='df-aim-regression-') as d:
 binary=Path(d)/'Regression.exe'
 subprocess.run([str(mono),str(a.roslyn),'-nologo','-noconfig','-langversion:9','-define:UNITY_EDITOR','-r:'+str(a.mono_root/'usr/lib/mono/4.5/System.dll'),'-r:'+str(a.mono_root/'usr/lib/mono/4.5/System.Core.dll'),'-out:'+str(binary),str(here/'EngineStubs.cs'),str(here/'Regression.cs'),str(here/'TimmyRegression.cs'),str(here/'TimmyVfxRegression.cs'),str(here/'TemporalBeamRegression.cs'),str(here/'ConnectionTiming.cs'),str(here/'ConeCaptureRegression.cs'),str(here/'MovingConnectionRegression.cs'),str(root/'Assets/Alterverse/Editor/AttackDeliveryRegression.cs'),str(root/'Assets/Alterverse/Editor/BasicAttackDeliveryRegression.cs'),*[str(src/n) for n in names]],env=env,check=True)
 subprocess.run([str(mono),str(binary),*(['--connection-timing'] if a.connection_timing else []),*(['--movement-dashes'] if a.movement_dashes else [])],env=env,check=True)


