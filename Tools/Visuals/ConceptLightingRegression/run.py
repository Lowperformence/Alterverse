#!/usr/bin/env python3
"""Compile the production lighting component and exercise its state ownership.

Unity types are substituted; this is not a Unity renderer or Android benchmark.
"""
import argparse
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dotnet", type=Path, default=os.environ.get("DOTNET_HOST_PATH") or shutil.which("dotnet"))
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    if not args.dotnet:
        parser.error("supply --dotnet /path/to/dotnet (requires .NET SDK 8)")
    enum_source = (ROOT / "Assets/Alterverse/Scripts/Match/ArenaVariantRuntime.cs").read_text()
    match = re.search(r"public enum ArenaVariant\s*\{[^}]+\}", enum_source)
    if not match:
        raise RuntimeError("actual map enum not found")
    env = dict(os.environ, DOTNET_CLI_TELEMETRY_OPTOUT="1", DOTNET_SKIP_FIRST_TIME_EXPERIENCE="1")
    with tempfile.TemporaryDirectory(prefix="df-concept-lighting-") as folder:
        folder = Path(folder)
        (folder / "ArenaVariant.cs").write_text("namespace Alterverse.Match {" + match.group() + "}\n")
        (folder / "NuGet.Config").write_text("<configuration><packageSources><clear/></packageSources></configuration>")
        includes = [HERE / "EngineStubs.cs", HERE / "Regression.cs", ROOT / "Assets/Alterverse/Scripts/Visuals/LunarLighting.cs"]
        (folder / "Lighting.csproj").write_text(
            '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
            '<TargetFramework>net8.0</TargetFramework><ImplicitUsings>disable</ImplicitUsings>'
            '<Nullable>disable</Nullable></PropertyGroup><ItemGroup>'
            + "".join('<Compile Include="' + escape(str(path)) + '"/>' for path in includes)
            + '</ItemGroup></Project>'
        )
        build = subprocess.run([str(args.dotnet), "build", str(folder / "Lighting.csproj"), "--nologo", "-v:q"], env=env, text=True, capture_output=True)
        if build.returncode:
            raise RuntimeError(build.stdout + build.stderr)
        result = subprocess.run([str(args.dotnet), str(folder / "bin/Debug/net8.0/Lighting.dll")], env=env, text=True, capture_output=True)
        if result.returncode:
            raise RuntimeError(result.stdout + result.stderr)
        if args.output:
            args.output.parent.mkdir(parents=True, exist_ok=True)
            args.output.write_text(result.stdout)
        print(result.stdout, end="")


if __name__ == "__main__":
    main()
