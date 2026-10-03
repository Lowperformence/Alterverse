#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
python3 "$script_dir/build_cyber_ranger.py"
blender_bin="${BLENDER_BIN:-blender}"
"$blender_bin" --background --python "$script_dir/refine_cyber_ranger_blender.py"
