"""Compile production hit resolution, healing attribution, target decoding and MVP selection.
Native physics, rendering and transport are controlled boundaries, not a Unity/Android test.
"""
import argparse,json,re,subprocess,tempfile
from pathlib import Path

p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--dotnet-root',type=Path,required=True)
p.add_argument('--negative-control',choices=['healing','hotzone','targets','mvp'])
a=p.parse_args();here=Path(__file__).resolve().parent;src=here.parents[2]/'Assets/Alterverse/Scripts'
def block(text,decl):
    start=text.index(decl);opening=text.index('{',start);arrow=text.find('=>',start)
    if 0<=arrow<opening:return text[start:text.index(';',arrow)+1]
    end=opening+1;depth=1
    while depth:
        depth+=(text[end]=='{')-(text[end]=='}');end+=1
    return text[start:end]
health=(src/'Combat/Health.cs').read_text();commands=(src/'Multiplayer/CoopGameplayCommands.cs').read_text()
board=(src/'Match/MatchScoreboard.cs').read_text();hot=(src/'Combat/HotZoneTank.cs').read_text();soldier=(src/'Combat/TrenchSoldier.cs').read_text()
heal=block(health,'public void Heal(float amount, bool playAudio, GameObject source)')
hit=block(hot,'private void ResolveShot()')
decode=block(commands,'private static bool TryReadTarget(')
performance=block(board,'private float Performance(')
if a.negative_control=='healing':heal=heal.replace('RecordHealing(healer,','RecordHealing(this,')
if a.negative_control=='hotzone':hit=hit.replace('if(!TeamSupportGroup.IsEnemyCombatUnit(victim,team))return;','TeamMember victimTeam=victim.GetComponent<TeamMember>();if(victimTeam==null||victimTeam.Team==team)return;')
if a.negative_control=='targets':
    decode=decode.replace('var candidates=Health.Active;','float best=float.MaxValue;var candidates=Health.Active;')
    decode=decode.replace('if(key==null||key.Key!=requestedKey)continue;\n                target=candidate;return true;','float d=(candidate.transform.position-new Vector3(x,y,z)).sqrMagnitude;if(d<best){best=d;target=candidate;}')
    decode=decode.replace('return false;\n        }','return target!=null;\n        }')
if a.negative_control=='mvp':performance='private float Performance(Entry e){var s=MatchTelemetry.Instance.GetStats(e.Health);return s.MvpScore;}'
parts=['using System;using System.Collections.Generic;using UnityEngine;using Unity.Collections;using Unity.Netcode;using Alterverse.Characters;using Alterverse.Combat;using Alterverse.Match;using Alterverse.Multiplayer;using Alterverse.Visuals;']
parts+=['namespace Alterverse.Combat {public partial class Health {'+heal+'}}']
parts+=['namespace Alterverse.Multiplayer {public class CoopGameplayCommands:MonoBehaviour {'+''.join(re.findall(r'private const int Target[^;]+;',commands))+decode+''.join(block(commands,x) for x in ['private static int TargetKind(','private static int TargetTeam(','private static bool MatchesTargetKind('])+'public static Health Probe(object[] data){var r=new FastBufferReader(data);return TryReadTarget(null,ref r,out Health h)?h:null;}}}']
parts+=['namespace Alterverse.Match {public class MatchScoreboard {'+block(board,'private sealed class Entry')+'private readonly Dictionary<Health,Entry> entries=new();'+performance+block(board,'private static float PerformanceScore(')+block(board,'private Entry Mvp(')+'public void Add(Health h,string n,int k,int d,int a){entries[h]=new Entry{Health=h,Name=n,Kills=k,Deaths=d,Assists=a};}public string Winner()=>Mvp().Name;public float Score(Health h)=>Performance(entries[h]);}}']
parts+=['namespace Alterverse.Combat {public class HotZoneTank:MonoBehaviour {bool shotPending,marsSkeleton,noxaraSpider,kryonBear,xenoAlien;float skeletonAttackKick,spiderAttackKick,bearAttackKick,xenoAttackKick;Health shotTarget;public int team;const float Damage=10f,AttackRange=5.8f;Vector3 PresentationMuzzle=>transform.position;'+hit+'public void FireTest(Health h){shotTarget=h;ResolveShot();}}public static class TeamSupportGroup {'+block(soldier,'public static bool IsEnemyCombatUnit(')+'}}']
with tempfile.TemporaryDirectory(prefix='df-combat-audit-') as folder:
    folder=Path(folder);generated=folder/'Production.cs';generated.write_text('\n'.join(parts));dll=folder/'Regression.dll'
    sdk=sorted((a.dotnet_root/'sdk').iterdir())[-1];refs=sorted((a.dotnet_root/'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'))[-1];version=sorted((a.dotnet_root/'shared/Microsoft.NETCore.App').iterdir())[-1].name
    rsp=folder/'compile.rsp';rsp.write_text('\n'.join(['-nologo','-langversion:9','-nowarn:0649,0067,0414','-out:'+str(dll),*['-r:'+str(x) for x in refs.glob('*.dll')],str(here/'EngineStubs.cs'),str(generated),str(here/'Regression.cs'),str(src/'Match/MatchTelemetry.cs')]))
    subprocess.run([str(a.dotnet_root/'dotnet'),str(sdk/'Roslyn/bincore/csc.dll'),'@'+str(rsp)],check=True)
    dll.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':version}}}))
    subprocess.run([str(a.dotnet_root/'dotnet'),str(dll)],check=True)
