#!/usr/bin/env python3
"""Exercise the production guard hierarchy and pose methods with transform doubles.

No Unity renderer, physics, Android runtime or live gameplay is simulated.
"""
import argparse
import importlib.util
import os
from pathlib import Path
import subprocess
import tempfile

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
spec = importlib.util.spec_from_file_location("cleanup_regression", ROOT / "Tools/Cleanup/Regression/run.py")
extract = importlib.util.module_from_spec(spec)
spec.loader.exec_module(extract)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--mono-root", type=Path, required=True)
    parser.add_argument("--observe", action="store_true", help="Measure armor and wrist drift without enforcing their stability checks")
    args = parser.parse_args()
    scripts = ROOT / "Assets/Alterverse/Scripts"
    lane = scripts / "Visuals/LaneUnitVisualMotion.cs"
    methods = ["BindRobotParts", "FindDeep", "Group", "BuildGuardPivots", "CaptureRestPose",
               "BuildAttackChain", "PulseAttack", "Seek", "AttackArm", "LateUpdate",
               "ResetPoseAfterRelocation", "Rest", "StepInfantryFoot", "AimInfantryLeg", "Pose"]
    raw = lane.read_bytes()
    fields = raw.index(b"public void ConfigureInfantry")
    motion = extract.members(lane, methods, fields)
    motion = "using UnityEngine;namespace Alterverse.Visuals {public sealed partial class LaneUnitVisualMotion:MonoBehaviour {\n" + motion + "\n}}"
    arm = extract.members(scripts / "Combat/HealingGuardMech.cs", ["BuildArm"])
    arm = "using UnityEngine;using Alterverse.Match;namespace Alterverse.Combat {public partial class HealingGuardMech {\n" + arm + "\n}}"
    combat = extract.members(scripts / "Visuals/CombatMotion.cs", ["Ease", "Curve", "Path", "Solve"])
    combat = "using UnityEngine;namespace Alterverse.Visuals {public static class CombatMotion {\n" + combat + "\n}}"
    planets = "using UnityEngine;namespace Alterverse.Visuals {\n"
    for name in ("GreenAlienMechVisual", "MarsStoneGolemMechVisual"):
        planets += "public partial class " + name + ":GuardGeometry {\n" + extract.members(scripts / ("Visuals/"+name+".cs"), ["BuildArm"]) + "\n}\n"
    planets += "}"
    stubs = (ROOT / "Tools/Movement/UpperBodyRegression/EngineStubs.cs").read_text().split("namespace Alterverse.Characters")[0]
    stubs = stubs.replace("public static float time, deltaTime=1f/60f;", "public static float time, deltaTime=1f/60f,timeScale=1f;")
    stubs = stubs.replace("public static float Min(float a,float b)", "public static float Exp(float v)=>(float)Math.Exp(v);public static float Repeat(float v,float n)=>v-(float)Math.Floor(v/n)*n;public static float InverseLerp(float a,float b,float v)=>Clamp01((v-a)/(b-a));public static float SmoothStep(float a,float b,float v){v=Clamp01(v);return a+(b-a)*v*v*(3-2*v);}public static float Min(float a,float b)")
    stubs = stubs.replace("public static Vector3 zero=>", "public static Vector3 down=>new Vector3(0,-1,0);public static Vector3 ClampMagnitude(Vector3 v,float m)=>v.magnitude>m?v.normalized*m:v;public static Vector3 zero=>")
    stubs = stubs.replace("public bool isActiveAndEnabled=true;", "public bool isActiveAndEnabled=true;protected void Destroy(object o){}")
    stubs = stubs.replace("public T AddComponent<T>()", "public static GameObject CreatePrimitive(PrimitiveType type){var go=new GameObject(type.ToString());go.AddComponent<Renderer>();go.AddComponent<Collider>();return go;}public T AddComponent<T>()")
    stubs = stubs.replace("public Transform Find(string n)", "public int childCount=>children.Count;public Transform GetChild(int i)=>children[i];public bool IsChildOf(Transform t){for(var a=parent;a!=null;a=a.parent)if(a==t)return true;return false;}public void SetParent(Transform t,bool world){Vector3 pos=position,scale=localScale;Quaternion rot=rotation;parent=t;if(world){position=pos;rotation=rot;localScale=scale;}}public Transform Find(string n)")
    stubs += "\nnamespace Alterverse.Visuals {public class MechMachineGunVisual:UnityEngine.MonoBehaviour {public bool StableHoldReady;public UnityEngine.Vector3 HoldingHandPosition;}}\n"
    with tempfile.TemporaryDirectory(prefix="alterverse-guard-arm-") as directory:
        directory = Path(directory)
        generated = []
        for name, content in [("Engine.cs", stubs), ("Motion.cs", motion), ("Guard.cs", arm), ("Combat.cs", combat), ("Planets.cs", planets)]:
            path = directory / name
            path.write_text(content)
            generated.append(path)
        binary = directory / "Regression.exe"
        mono = args.mono_root / "usr/bin/mono-sgen"
        compiler = args.mono_root / "usr/lib/mono/4.5/mcs.exe"
        env = os.environ.copy()
        env["MONO_PATH"] = str(args.mono_root / "usr/lib/mono/4.5")
        env["MONO_CFG_DIR"] = str(args.mono_root / "etc")
        subprocess.run([str(mono), str(compiler), "-langversion:latest", "-r:System.Numerics.dll", "-out:" + str(binary), str(HERE / "Regression.cs"), *map(str, generated)], env=env, check=True)
        subprocess.run([str(mono), str(binary), *( ["--observe"] if args.observe else [])], env=env, check=True)


if __name__ == "__main__":
    main()
