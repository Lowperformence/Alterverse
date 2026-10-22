"""Exercise Nerok's production attacks and aim preview with deterministic Unity fixtures.

Requires .NET 8; no Unity rendering, native physics or live transport is simulated.
"""
import argparse
import json
from pathlib import Path
import subprocess
import tempfile

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('--dotnet-root', type=Path, required=True)
a = p.parse_args()
dotnet = a.dotnet_root.resolve()
here = Path(__file__).resolve().parent
root = here.parents[2]
src = root / 'Assets/Alterverse/Scripts'
names = [
    'Shop/WeaponSkinCatalog.cs', 'Shop/HeroUnlockCatalog.cs', 'Characters/PlayerHeroSelection.cs',
    'Characters/HeroIdentity.cs', 'Characters/HeroAbilityKit.cs', 'Characters/HeroCombatProfile.cs',
    'Characters/TimmyBalance.cs', 'Characters/VoidKnightBalance.cs', 'Characters/VoidKnightState.cs',
    'Characters/HeroAbilityExecution.cs', 'Characters/HeroAbilityExecution.CyberShark.cs',
    'Characters/HeroAbilityExecution.Timmy.cs', 'Characters/HeroAbilityExecution.VoidKnight.cs',
    'Characters/HeroAimPrediction.cs', 'Characters/PlayerHeroAbilities.cs',
    'Combat/AimingSettings.cs', 'Combat/AimGeometry.cs', 'Combat/HeroAttackDelivery.cs',
    'Combat/HeroCombatRules.cs', 'Combat/HeroCombatStatus.cs', 'Combat/BasicAttack.cs',
    'Combat/SoftTargeting.cs', 'Combat/ManualAiming.cs', 'Multiplayer/CoopGameplayCommands.cs',
    'Audio/HeroCombatAudio.cs', 'Audio/CombatSoundPalette.cs', 'UI/HeroAttackCatalog.cs',
    'UI/CombatHudLayout.cs', 'UI/AbilityRangePreviewOverlay.cs',
    'Visuals/HeroVfxEvents.cs', 'Visuals/HeroSkillVfx.VoidKnight.cs', 'Visuals/VoidKnightPresentation.cs',
]
# Reuse the existing combat fixtures; compile the complete current ability partials unchanged.
engine = (root / 'Tools/Aiming/Regression/EngineStubs.cs').read_text()
engine = engine.replace('public class SerializeField:Attribute{}', 'public class DisallowMultipleComponent:Attribute{} public class SerializeField:Attribute{}')
engine = engine.replace('public Vector3 position,forward=', 'public Vector3 localPosition;public Quaternion localRotation=Quaternion.identity;public Vector3 position,forward=')
# The legacy fixture ignores LookRotation's up vector. Ground preview quads need it:
# local forward points down and local up follows the supplied planar aim direction.
engine = engine.replace('f=f.normalized;return Euler(', 'f=f.normalized;if(f.y<-.99999f&&up.sqrMagnitude>.00001f)return AngleAxis((float)Math.Atan2(up.x,up.z)*Mathf.Rad2Deg,Vector3.up)*AngleAxis(90,Vector3.right);return Euler(')
engine = engine.replace('public static T Load<T>(string s)where T:class', 'public static T[] LoadAll<T>(string s)=>new T[0];public static T Load<T>(string s)where T:class')
engine = engine.replace('LogError(object x)=>', 'LogError(object x,Object context=null)=>')
engine = engine.replace('public static bool CanSimulateHostGameplay=>', 'public static bool CanMutateAuthoritativeState=>CanSimulateHostGameplay;public static bool CanSimulateHostGameplay=>')
engine = engine.replace('float shield=Math.Min(Shield,n);', 'n=GetComponent<Alterverse.Characters.VoidKnightState>()?.ReduceFrontalDamage(n,source)??n;float shield=Math.Min(Shield,n);')
engine = engine.replace('UnityEngine.GameObject actor=null)=>HeroVfxPool.Emit', 'UnityEngine.GameObject actor=null,bool warning=false)=>HeroVfxPool.Emit')
engine = engine.replace('public enum DimensionSound{Kill}', 'public enum DimensionSound{Kill,Purchase}')
engine = engine.replace('public static void BroadcastTimmyEffect(', 'public static void BroadcastVoidKnightEffect(UnityEngine.GameObject actor,int kind,UnityEngine.Vector3 from,UnityEngine.Vector3 to,float radius,float duration){}public static void BroadcastTimmyEffect(')
# The shared presentation source now exposes wider arm IK. This combat suite does
# not render or validate rig poses: provide only the omitted presentation API.
engine = engine.replace('public class Transform:Component {', 'public class Transform:Component {public Vector3 up=>rotation*Vector3.up;')
engine = engine.replace('public struct Vector3 {', 'public struct Vector3 {public static Vector3 ProjectOnPlane(Vector3 v,Vector3 n)=>v-n*Dot(v,n);')
engine = engine.replace('public static class CombatMotion {', 'public static class CombatMotion {public static float Ease(float t){t=UnityEngine.Mathf.Clamp01(t);return t*t*(3-2*t);}public static void Solve(UnityEngine.Transform a,UnityEngine.Transform b,UnityEngine.Transform c,UnityEngine.Vector3 target,UnityEngine.Vector3 hint,float extension=.975f){}')
extras = (root / 'Tools/Combat/VoidKnightRegression/ExtraStubs.cs').read_text()
extras = extras.replace('namespace Alterverse.UI {public class AbilityRangePreviewOverlay:MonoBehaviour{}}', '')
refs = next((dotnet / 'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'))
csc = next((dotnet / 'sdk').glob('*/Roslyn/bincore/csc.dll'))
with tempfile.TemporaryDirectory(prefix='nerok-regression-') as directory:
    tmp = Path(directory)
    (tmp / 'Engine.cs').write_text(engine)
    (tmp / 'Extras.cs').write_text(extras)
    out = tmp / 'Regression.dll'
    subprocess.run([str(dotnet / 'dotnet'), str(csc), '-nologo', '-noconfig', '-langversion:latest',
        '-out:' + str(out), *[f'-r:{x}' for x in refs.glob('*.dll')],
        str(tmp / 'Engine.cs'), str(tmp / 'Extras.cs'), str(here / 'Regression.cs'),
        *[str(src / n) for n in names]], check=True)
    (tmp / 'Regression.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {
        'tfm': 'net8.0', 'framework': {'name': 'Microsoft.NETCore.App', 'version': '8.0.0'}}}))
    subprocess.run([str(dotnet / 'dotnet'), str(out)], check=True)

