#!/usr/bin/env python3
"""Parse changed arena-visual C# sources; this does not compile Unity APIs."""
from pathlib import Path
from tree_sitter import Language, Parser
import tree_sitter_c_sharp

root = Path(__file__).resolve().parents[3]
parser = Parser(Language(tree_sitter_c_sharp.language()))
paths = (
    'Assets/Alterverse/Scripts/Combat/CentralHealingPickup.cs',
    'Assets/Alterverse/Scripts/Performance/MobilePerformanceProfile.cs',
    'Assets/Alterverse/Scripts/Visuals/HealingZoneMarker.cs',
    'Assets/Alterverse/Scripts/Visuals/LunarSurfaceField.cs',
    'Assets/Alterverse/Scripts/Visuals/PlanetSceneryMaterials.cs',
    'Assets/Alterverse/Scripts/Visuals/PlanetSurfaceMaterials.cs',
    'Assets/Alterverse/Scripts/Visuals/ProductionLunarGround.Geology.cs',
    'Assets/Alterverse/Scripts/Visuals/ProductionLunarGround.cs',
    'Assets/Alterverse/Scripts/Visuals/StructureSurfaceMaterials.cs',
)
for path in paths:
    tree = parser.parse((root / path).read_bytes())
    if tree.root_node.has_error:
        raise SystemExit(f'C# syntax error: {path}\n{tree.root_node}')
print(f'C# syntax parsed: {len(paths)} arena visual files (not a Unity compile)')
