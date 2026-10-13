"""Compile and run the real component against CPU-only Unity API fixtures.
python Tools/TahlaEquipment/run_regression.py [--mono-prefix /path/to/extracted/mono]
These checks do not run the Unity editor, renderer or Android player.
"""
import argparse,os,shutil,subprocess,tempfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
parser=argparse.ArgumentParser();parser.add_argument('--mono-prefix',type=Path)
args=parser.parse_args();env=os.environ.copy()
if args.mono_prefix:
    prefix=args.mono_prefix.resolve();mono=prefix/'usr/bin/mono'
    config=prefix/'etc/mono/config';base=[str(mono),'--config',str(config)]
    env['MONO_CFG_DIR']=str(prefix/'etc');env['MONO_PATH']=str(prefix/'usr/lib/mono/4.5')
    env['LD_LIBRARY_PATH']=str(prefix/'usr/lib')
    compiler=base+[str(prefix/'usr/lib/mono/4.5/mcs.exe')]
else:
    assert shutil.which('mono') and shutil.which('mcs'),'Install Mono or provide --mono-prefix'
    base=[shutil.which('mono')];compiler=[shutil.which('mcs')]
with tempfile.TemporaryDirectory() as directory:
    executable=Path(directory)/'tahla-regression.exe'
    subprocess.run(compiler+['-langversion:latest','-r:System.Numerics.dll','-out:'+str(executable),
        str(ROOT/'Tools/TahlaEquipment/Regression/EngineStubs.cs'),
        str(ROOT/'Assets/Alterverse/Scripts/Visuals/TahlaEquipmentAppearance.cs'),
        str(ROOT/'Tools/TahlaEquipment/Regression/Regression.cs')],env=env,check=True)
    subprocess.run(base+[str(executable),str(ROOT/'Assets/Alterverse/Resources/TahlaEquipment/Model.bytes'),
        str(ROOT/'ArtSource/Heroes/TahlaEquipment/reference-rig.csv')],env=env,check=True)
