"""Compile production review scheduling, rewards, persistence and UI against controlled SDK/engine boundaries.

This does not run Unity, Gradle/R8, or a real Play Store review dialog.
"""
import argparse
import json
from pathlib import Path
import subprocess
import tempfile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--dotnet-root", type=Path, required=True)
args = parser.parse_args()
here = Path(__file__).resolve().parent
root = here.parents[2]
sdk = sorted((args.dotnet_root / "sdk").iterdir())[-1]
runtime = sorted((args.dotnet_root / "shared/Microsoft.NETCore.App").iterdir())[-1]
sources = [
    "Profile/AccountProfilePrefs.cs", "Profile/ProfileTransaction.cs",
    "Profile/MatchRewardService.cs", "Profile/ReviewPromptProgress.cs",
    "UI/MainMenuReviewPrompt.cs", "UI/NativeStoreReview.cs",
]
with tempfile.TemporaryDirectory(prefix="df-review-") as folder:
    folder = Path(folder)
    for platform in ("UNITY_ANDROID", "UNITY_EDITOR", "UNITY_SERVER"):
        output = folder / (platform + ".dll")
        response = folder / "compile.rsp"
        response.write_text("\n".join([
            "-nologo", "-nowarn:0649,0414", "-langversion:9", "-define:" + platform,
            "-out:" + str(output),
            *["-r:" + str(p) for p in runtime.glob("*.dll")],
            str(here / "Regression.cs"),
            *[str(root / "Assets/Alterverse/Scripts" / p) for p in sources],
        ]))
        subprocess.run([str(args.dotnet_root / "dotnet"), str(sdk / "Roslyn/bincore/csc.dll"), "@" + str(response)], check=True)
        output.with_suffix(".runtimeconfig.json").write_text(json.dumps({
            "runtimeOptions": {"tfm": "net8.0", "framework": {"name": "Microsoft.NETCore.App", "version": runtime.name}}
        }))
        subprocess.run([str(args.dotnet_root / "dotnet"), str(output)], check=True)

manifest = json.loads((root / "Packages/manifest.json").read_text())
lock = json.loads((root / "Packages/packages-lock.json").read_text())["dependencies"]
assert manifest["dependencies"]["com.google.play.review"] == lock["com.google.play.review"]["version"] == "1.8.4"
assert lock["com.google.play.review"]["dependencies"] == {"com.google.play.common": "1.9.2", "com.google.play.core": "1.8.6"}
scopes = next(r["scopes"] for r in manifest["scopedRegistries"] if r["url"] == "https://package.openupm.com")
assert all(p in scopes for p in ("com.google.play.common", "com.google.play.core", "com.google.play.review"))
assert "com.google.android.play:review:2.0.2" in (root / "Assets/Plugins/Android/mainTemplate.gradle").read_text()
assert "com.google.android.play.core.review.**" in (root / "Assets/Plugins/Android/proguard-user.txt").read_text()
print("Package manifest, lock, Gradle dependency and release JNI keep rules agree.")
