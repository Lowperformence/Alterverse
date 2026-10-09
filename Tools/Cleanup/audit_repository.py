#!/usr/bin/env python3
"""Read-only C#/Unity-asset inventory. Syntax checks do not replace Unity compilation.

--snapshot accepts {head, tree: Git-tree entries, guids: GUID-to-asset-path} from
an authenticated repository inventory; --serialized-root supplies its text assets.
Generated warnings about package GUIDs or import metadata are not declared runtime
failures. No assets, importers, dependencies, saves or selected recordings are edited.
"""
import argparse
import collections
import json
from pathlib import Path
import re
from tree_sitter import Language, Parser
import tree_sitter_c_sharp

ROOT = Path(__file__).resolve().parents[2]


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--snapshot", type=Path, required=True)
    ap.add_argument("--serialized-root", type=Path, required=True)
    ap.add_argument("--output", type=Path, required=True)
    args = ap.parse_args()
    snapshot = json.loads(args.snapshot.read_text())
    entries = {e["path"]: e for e in snapshot["tree"]}
    guids = snapshot["guids"]
    for meta in ROOT.glob("Assets/**/*.meta"):
        match = re.search(r"^guid:\s*([0-9a-f]{32})\s*$", meta.read_text(), re.M)
        if match:
            guids[match[1]] = meta.relative_to(ROOT).as_posix()[:-5]
    sources = sorted([*ROOT.glob("Assets/**/*.cs"), *ROOT.glob("Tools/**/*.cs")])
    parser = Parser(Language(tree_sitter_c_sharp.language()))
    syntax_errors = []
    resources = collections.defaultdict(list)
    for path, entry in entries.items():
        if entry["type"] == "blob" and "/Resources/" in path and not path.endswith(".meta"):
            resources[str(Path(path.split("/Resources/", 1)[1]).with_suffix(""))].append(path)
    literals = []
    unknown = []
    expected_extensions = {"AudioClip": {".mp3", ".wav", ".ogg"}, "Material": {".mat"}, "Shader": {".shader"}, "TextAsset": {".json", ".txt", ".bytes", ".csv"}, "GameObject": {".prefab", ".fbx"}, "Texture2D": {".png", ".jpg", ".jpeg", ".tga"}}
    for path in sources:
        text = path.read_text()
        parse_text = text
        if path.name == "AndroidDeviceCompatibilityPostprocessor.cs":
            # This grammar misreads a valid contextual-keyword local, `required`.
            # Escape it only in parser input; the actual source is never rewritten.
            parse_text = re.sub(r"\brequired\b", "@required", parse_text)
        if parser.parse(parse_text.encode()).root_node.has_error:
            syntax_errors.append(path.relative_to(ROOT).as_posix())
        for kind, key in re.findall(r'Resources\.Load<([\w.]+)>\(\s*"([^"\n]+)"\s*\)', text):
            candidates = resources.get(key, [])
            extensions = expected_extensions.get(kind.split(".")[-1])
            if extensions:
                candidates = [p for p in candidates if Path(p).suffix.lower() in extensions]
            record = {"source": path.relative_to(ROOT).as_posix(), "type": kind, "key": key, "candidates": candidates}
            literals.append(record)
            if len(candidates) != 1:
                unknown.append(record)
    missing_meta = []
    for path, entry in entries.items():
        if entry["type"] == "blob" and path.startswith("Assets/") and not path.endswith(".meta"):
            if path + ".meta" not in entries and not (ROOT / (path + ".meta")).is_file():
                missing_meta.append(path)
    unresolved = collections.defaultdict(list)
    serialized = []
    for path, entry in entries.items():
        if entry["type"] != "blob" or Path(path).suffix not in {".unity", ".prefab", ".mat", ".asset"}:
            continue
        local = args.serialized_root / path
        assert local.is_file(), path
        serialized.append(path)
        for guid in re.findall(r"guid:\s*([0-9a-f]{32})", local.read_text()):
            if guid not in guids and not guid.startswith("0000000000000000"):
                unresolved[guid].append(path)
    result = {"base_commit": snapshot["head"], "csharp_files_parsed": len(sources), "syntax_errors": syntax_errors,
              "tracked_resources": len(resources), "literal_resource_loads_checked": len(literals),
              "literal_loads_needing_review": unknown, "remaining_missing_import_metadata": missing_meta,
              "serialized_assets_read": len(serialized), "package_or_unresolved_guid_references": dict(unresolved),
              "limits": ["C# syntax, not Unity semantic compilation", "Only constant Resources.Load addresses; dynamic loads require Unity",
                         "Unresolved GUIDs may belong to installed packages", "FBX import and all real device flows remain untested"]}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2) + "\n")
    print(json.dumps({k: result[k] for k in ["csharp_files_parsed", "syntax_errors", "tracked_resources", "literal_resource_loads_checked", "serialized_assets_read"]}))
    print("Resource review entries:", len(unknown), "; missing import metadata:", len(missing_meta), "; package/unresolved GUIDs:", len(unresolved))
    if syntax_errors:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
