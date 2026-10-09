#!/usr/bin/env python3
"""Execute production pool scheduling/pose math under Mono with a substituted mesh factory."""
import argparse
import os
from pathlib import Path
import subprocess
import tempfile
from tree_sitter import Language, Parser
import tree_sitter_c_sharp

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[2]

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("--mono-root",type=Path,required=True)
    args=parser.parse_args()
    mono=args.mono_root/"usr/bin/mono-sgen"
    compiler=args.mono_root/"usr/lib/mono/4.5/mcs.exe"
    env=dict(os.environ,MONO_PATH=str(args.mono_root/"usr/lib/mono/4.5"),MONO_CFG_DIR=str(args.mono_root/"etc"))
    source=(ROOT/"Assets/Alterverse/Scripts/Visuals/HeroVfxPool.cs").read_bytes()
    tree=Parser(Language(tree_sitter_c_sharp.language())).parse(source)
    stack=[tree.root_node]
    factory=None
    while stack:
        node=stack.pop()
        if node.type=="method_declaration" and node.child_by_field_name("name").text==b"BuildShape":
            factory=node;break
        stack.extend(node.children)
    assert factory is not None and not tree.root_node.has_error
    replacement=b'private static Mesh BuildShape(HeroFxShape shape,bool polished=true) {return new Mesh{name="Fixture_"+shape};}'
    # Only the unchanged, local-function-based startup geometry factory is replaced.
    # Emit/Show/Draw/Update/cancellation/Trace are compiled without edits.
    source=source[:factory.start_byte]+replacement+source[factory.end_byte:]
    with tempfile.TemporaryDirectory() as temp:
        temp=Path(temp)
        pool=temp/"HeroVfxPool.cs";pool.write_bytes(source)
        binary=temp/"Regression.exe"
        sources=[pool,HERE/"EngineStubs.cs",HERE/"Regression.cs",
            ROOT/"Assets/Alterverse/Scripts/Performance/VfxQualityHysteresis.cs",
            ROOT/"Assets/Alterverse/Scripts/Performance/RuntimePerformanceMonitor.cs",
            ROOT/"Assets/Alterverse/Scripts/Performance/AdaptiveVfxBudget.cs",
            ROOT/"Assets/Alterverse/Scripts/Audio/AudioDuckEnvelope.cs"]
        subprocess.run([str(mono),str(compiler),"-langversion:latest","-r:System.Numerics.dll","-out:"+str(binary),*[str(s) for s in sources]],env=env,check=True)
        subprocess.run([str(mono),str(binary)],env=env,check=True)

if __name__=="__main__":main()
