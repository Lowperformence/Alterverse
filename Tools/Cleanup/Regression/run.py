#!/usr/bin/env python3
"""Run production lifecycle rules with engine/network/render construction substitutes.

The actual Bootstrap orchestration methods and MainMenu EnterMatch are extracted
verbatim by syntax node. Geometry/actor factories and unrelated front-end rendering
are replaced by fixtures. Explicit constructor spelling accommodates Mono's older
compiler; no control flow or algorithm is rewritten. This is not a Unity build.
"""
import argparse
import os
from pathlib import Path
import subprocess
import tempfile
from tree_sitter import Language, Parser
import tree_sitter_c_sharp

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
PARSER = Parser(Language(tree_sitter_c_sharp.language()))


def members(path, names, fields_before=None):
    raw = path.read_bytes()
    tree = PARSER.parse(raw)
    assert not tree.root_node.has_error, path
    stack = [tree.root_node]
    found = {}
    fields = []
    while stack:
        node = stack.pop()
        if node.type == "method_declaration":
            name = node.child_by_field_name("name").text.decode()
            if name in names:
                assert name not in found, name
                found[name] = node.text.decode()
        if node.type == "field_declaration" and fields_before is not None and node.start_byte < fields_before:
            fields.append((node.start_byte, node.text.decode()))
        stack.extend(node.children)
    assert set(found) == set(names), (path, set(names) - set(found))
    return "\n".join(text for _, text in sorted(fields)) + "\n" + "\n".join(found[n] for n in names)


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--mono-root", type=Path, required=True)
    ap.add_argument("--source-root", type=Path, default=ROOT, help="Optional original revision for negative-control tests")
    args = ap.parse_args()
    source = args.source_root / "Assets/Alterverse/Scripts"
    mono = args.mono_root / "usr/bin/mono-sgen"
    compiler = args.mono_root / "usr/lib/mono/4.5/mcs.exe"
    env = dict(os.environ, MONO_PATH=str(args.mono_root / "usr/lib/mono/4.5"), MONO_CFG_DIR=str(args.mono_root / "etc"))
    bootstrap_path = source / "Prototype/PrototypeBootstrap.cs"
    prefix_end = bootstrap_path.read_bytes().index(b"private System.Collections.IEnumerator Start")
    boot = members(bootstrap_path, ["PrepareTransitionFrame", "Start", "LateUpdate", "RestartPrototype", "BuildPrototype", "IsServerOnlyMatch"], prefix_end)
    if b"private static void RecoverFailedBuild" in bootstrap_path.read_bytes():
        boot += members(bootstrap_path, ["RecoverFailedBuild"])
    boot = boot.replace("GameplayVoidColor=new(", "GameplayVoidColor=new Color(").replace('GameObject cameraObject=new(', 'GameObject cameraObject=new GameObject(')
    boot = "using Alterverse.AI;using Alterverse.CameraSystem;using Alterverse.Characters;using Alterverse.Combat;using Alterverse.Match;using Alterverse.Multiplayer;using Alterverse.UI;using Alterverse.Visuals;using Unity.Netcode;using UnityEngine;using FrontHudComponent=Alterverse.Match.DimensionsFrontHUD;\nnamespace Alterverse.Prototype {public sealed partial class PrototypeBootstrap:MonoBehaviour {\n" + boot + "\n}}"
    menu = members(source / "UI/MainMenuController.cs", ["EnterMatch"])
    menu = "using Alterverse.Match;using Alterverse.Shop;using Alterverse.Multiplayer;using UnityEngine;namespace Alterverse.UI {public sealed partial class MainMenuController {\n" + menu + "\n}}"
    music = "using Alterverse.Combat;using UnityEngine;namespace Alterverse.Audio {public sealed partial class ProductionMusicDirector {\n" + members(source / "Audio/ProductionMusicDirector.cs", ["End"]) + "\n}}"
    hud = "using Alterverse.Match;using UnityEngine;namespace Alterverse.UI {public sealed partial class PlanetHUD {\n" + members(source / "UI/PlanetHUD.cs", ["Update"]) + "\n}}"
    plan = (source / "Multiplayer/CoopMatchPlan.cs").read_text()
    for old, new in [("ordered=new(", "ordered=new List<CoopLobbyService.MemberView>("), ("guests=new(", "guests=new List<CoopLobbyService.MemberView>("), ("humanHeroes=new();", "humanHeroes=new HashSet<HeroId>();"), ("MatchSession session=new(", "MatchSession session=new MatchSession("), ("member,bool host)=>new()", "member,bool host)=>new MatchPlayerSlot()"), ("hero,int team)=>new()", "hero,int team)=>new MatchPlayerSlot()")]:
        assert old in plan, old
        plan = plan.replace(old, new)
    with tempfile.TemporaryDirectory(prefix="alterverse-cleanup-") as directory:
        directory = Path(directory)
        generated = []
        for name, content in [("Bootstrap.cs", boot), ("Menu.cs", menu), ("Plan.cs", plan), ("Music.cs", music), ("PlanetHud.cs", hud)]:
            path = directory / name
            path.write_text(content)
            generated.append(path)
        binary = directory / "Regression.exe"
        sources = [HERE / "EngineStubs.cs", HERE / "Regression.cs", *generated,
                   source / "Characters/BaseRecall.cs", source / "Characters/HeroRespawn.cs",
                   source / "AI/MobaBotRespawn.cs", source / "Match/MatchClock.cs",
                   source / "Match/MatchExit.cs", source / "Match/DimensionsFront.cs",
                   source / "Profile/DailyCalendar.cs", source / "Profile/LoginStreakProgress.cs",
                   source / "Multiplayer/MatchSession.cs", source / "Multiplayer/OnlineMatchRuntime.cs",
                   ROOT / "Assets/Alterverse/Scripts/Characters/LocalHeroContext.cs",
                   source / "Multiplayer/CoopHeroSlot.cs", source / "Combat/TeamMember.cs",
                   source / "Audio/MatchAudioDirector.cs"]
        subprocess.run([str(mono), str(compiler), "-langversion:latest", "-r:System.Numerics.dll", "-out:" + str(binary), *map(str, sources)], env=env, check=True)
        subprocess.run([str(mono), str(binary)], env=env, check=True)


if __name__ == "__main__":
    main()
