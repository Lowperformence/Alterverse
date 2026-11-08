"""Compile production shield transfer, Prism Shot contact and ability cards.

Engine/network/visual boundaries use controlled substitutes; this is not a Unity
or device transport test. Hit and ResolvePresentedHit are copied verbatim from
the production class so normal damage order and slot gating are exercised.
"""
import argparse
import ast
import json
from pathlib import Path
import re
import subprocess
import tempfile

p = argparse.ArgumentParser(description=__doc__)
p.add_argument("--dotnet-root", type=Path, required=True)
args = p.parse_args()
here = Path(__file__).resolve().parent
root = here.parents[2]
src = root / "Assets/Alterverse/Scripts"
sdk = args.dotnet_root.resolve()
shared = ast.parse((here.parent / "ShieldRegression/run.py").read_text())
fixture = next(ast.literal_eval(n.value) for n in shared.body if isinstance(n, ast.Assign)
               and any(isinstance(t, ast.Name) and t.id == "fixture" for t in n.targets))
fixture = fixture.split("class ShieldRegression")[0]
enum = re.search(r"public enum HeroId\s*\{[^}]+\}", (src / "Characters/HeroIdentity.cs").read_text()).group()
fixture = fixture.replace("HERO_ENUM", enum)
fixture = fixture.replace("public HeroId Hero;", "public static int CatalogHeroCount=>System.Enum.GetValues(typeof(HeroId)).Length;public HeroId Hero;")
fixture = fixture.replace("public float ReduceFrontalDamage", "public void RecordBasicHit(){} public void RecordSkillHit(){} public float ReduceFrontalDamage")
fixture = fixture.replace("public void Clear(){}", "public void Clear(){} public static float Slow,Seconds;public static void Apply(Health t,float slow,float seconds,float stun=0){Slow=slow;Seconds=seconds;}")
fixture = fixture.replace("public class RealityCore:UnityEngine.MonoBehaviour{}", "public class RealityCore:UnityEngine.MonoBehaviour{public int Team;public bool IsVulnerable=true;}")
fixture = fixture.replace("public class LaneMinion:UnityEngine.MonoBehaviour{}", "public class LaneMinion:UnityEngine.MonoBehaviour{public int Team;}public class LaneTower:UnityEngine.MonoBehaviour{public int Team;}")
fixture = fixture.replace("DamageTakenMultiplier=1;}", "DamageTakenMultiplier=1;public float CooldownMultiplier=1;}")
fixture = fixture.replace("public static bool CanMutateAuthoritativeState=true;", "public static bool CanMutateAuthoritativeState=true;}public static class CoopGameplayCommands{public static float Damage;public static void RecordAuthoritativeDamage(UnityEngine.GameObject actor,float value){Damage+=value;}")
fixture = fixture.replace("public static void ConfirmedKill", "public static void Play(UnityEngine.GameObject a,int s,bool hit,UnityEngine.Vector3 p){}public static void Impact(UnityEngine.GameObject a,int s,UnityEngine.Vector3 p){} public static void ConfirmedKill")
fixture += """
namespace Alterverse.Match {public class DimensionPlanet:UnityEngine.MonoBehaviour{public bool Available=true;}}
namespace Alterverse.Combat {
 public class HeroAttackDelivery{
  public static HeroAttackDelivery For(UnityEngine.GameObject a)=>new();
  public static float Windup(Alterverse.Characters.HeroId id)=>0;
  public static float AbilityWindup(Alterverse.Characters.HeroId id,int slot,Alterverse.Characters.AbilitySpec spec)=>0;
  public void Basic(Health target,float damage,float range){}
 }
}
namespace Alterverse.Visuals {
 public static class HeroSkillVfx {
  public static void Impact(UnityEngine.GameObject a,Alterverse.Combat.Health h,int slot){}
  public static void Spell(UnityEngine.GameObject a,UnityEngine.Vector3 p,int slot){}
  public static void Debuff(UnityEngine.GameObject a,Alterverse.Combat.Health h,int slot){}
  public static void Support(UnityEngine.GameObject a,Alterverse.Combat.Health h,bool shield,float seconds,bool shieldUsesTotal=false){}
 }
}
"""

def method(text, signature):
    start = text.index(signature)
    end = text.index("{", start) + 1
    depth = 1
    while depth:
        depth += (text[end] == "{") - (text[end] == "}")
        end += 1
    return text[start:end]

execution = (src / "Characters/HeroAbilityExecution.cs").read_text()
fixture += """
namespace Alterverse.Characters {
 public sealed class HeroAbilityExecution:UnityEngine.MonoBehaviour{
  private const int SightlineBreakShots=3;
  private Alterverse.Combat.Health self;private HeroIdentity identity;private HeroAbilityKit kit;
  private void Awake(){self=GetComponent<Alterverse.Combat.Health>();identity=GetComponent<HeroIdentity>();kit=GetComponent<HeroAbilityKit>();}
  private void TriggerTimmyPassive(){}
  public void Contact(int slot,Alterverse.Combat.Health target,bool volley=false)=>ResolvePresentedHit(slot,target,volley);
"""
fixture += method(execution, "        internal void ResolvePresentedHit(")
fixture += method(execution, "        private void Hit(") + "}}"
preamble = "using Alterverse.Characters;using Alterverse.Combat;using Alterverse.Visuals;using Alterverse.Audio;"
engine = (root / "Tools/Aiming/Regression/EngineStubs.cs").read_text().split("namespace UnityEngine.InputSystem")[0]
engine = engine.replace("SubsystemRegistration,BeforeSceneLoad", "SubsystemRegistration,BeforeSceneLoad,AfterSceneLoad")
engine = engine.replace("FindObjectsInactive i) where", "FindObjectsInactive i=FindObjectsInactive.Exclude) where")
refs = next((sdk / "packs/Microsoft.NETCore.App.Ref").glob("*/ref/net8.0"))
csc = next((sdk / "sdk").glob("*/Roslyn/bincore/csc.dll"))
names = ["Combat/Health.cs", "Combat/HeroCombatRules.cs", "Characters/HeroAbilityKit.cs",
         "Characters/HeroCombatProfile.cs", "Characters/TimmyBalance.cs",
         "Characters/VoidKnightBalance.cs", "Characters/Bolt7Balance.cs", "UI/HeroAttackCatalog.cs"]
with tempfile.TemporaryDirectory(prefix="prism-shield-") as directory:
    directory = Path(directory)
    (directory / "Fixture.cs").write_text(preamble + engine + fixture)
    output = directory / "Regression.dll"
    subprocess.run([str(sdk / "dotnet"), str(csc), "-nologo", "-langversion:9", "-out:" + str(output),
                    *["-r:" + str(f) for f in refs.glob("*.dll")], str(directory / "Fixture.cs"),
                    str(here / "Regression.cs"), *[str(src / n) for n in names]], check=True)
    output.with_suffix(".runtimeconfig.json").write_text(json.dumps({"runtimeOptions": {
        "tfm": "net8.0", "framework": {"name": "Microsoft.NETCore.App", "version": "8.0.0"},
        "rollForward": "LatestPatch"}}))
    subprocess.run([str(sdk / "dotnet"), str(output)], check=True)
