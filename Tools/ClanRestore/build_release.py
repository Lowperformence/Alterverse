#!/usr/bin/env python3
"""Build a local release directory from an actual current Node source export.

No cloud API, deployment, credential change or database write is performed.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil
import subprocess
import tempfile


def prepare(index, package, namespace, output, root=None):
    root=Path(root) if root else Path(__file__).resolve().parents[2]
    index,package,output=map(Path,(index,package,output))
    if output.exists(): raise ValueError('Output must be a new directory')
    if not re.fullmatch(r'[a-z0-9][a-z0-9-]{0,62}',namespace): raise ValueError('Explicit existing social namespace required')
    source=index.read_text()
    project=re.findall(r"\bconst\s+UNITY_PROJECT_ID\s*=\s*(['\"])([a-f0-9-]{36})\1\s*;",source)
    if len(project)!=1: raise ValueError('The current Node Unity project must be unambiguous')
    project=[project[0][1]]
    manifest=json.loads(package.read_text())
    if manifest.get('type')!='module' or '@google-cloud/functions-framework' not in manifest.get('dependencies',{}):
        raise ValueError('Expected current Functions Framework ES module package')
    with tempfile.TemporaryDirectory(prefix='df-clan-release-',dir=output.parent) as temporary:
        stage=Path(temporary)/'release';stage.mkdir()
        (stage/'index.js').parent.mkdir(parents=True,exist_ok=True)
        subprocess.run(['node',str(root/'Server/commerce/node/attach-clans.mjs'),str(index.resolve()),str(stage/'index.js')],check=True)
        subprocess.run(['node','--check',str(stage/'index.js')],check=True)
        shutil.copy2(package,stage/'package.json')
        for path in (root/'Server/commerce').glob('*.py'):
            if not path.name.startswith('test_'):
                target=stage/path.relative_to(root);target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(path,target)
        for relative in ('Server/commerce/requirements.txt','Server/commerce/node/start-with-clans.sh',
                         'Assets/Alterverse/Resources/Commerce/ShardCatalog.json',
                         'Assets/Alterverse/Resources/Social/SafeIdentityCatalog.json'):
            target=stage/relative;target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(root/relative,target)
        docker=(root/'Server/commerce/node/Dockerfile.clans').read_text().replace('__UNITY_PROJECT__',project[0]).replace('__SOCIAL_NAMESPACE__',namespace)
        (stage/'Dockerfile').write_text(docker)
        (stage/'RELEASE.json').write_text(json.dumps({
            'release':'2026-10-24.clans.1','sourceIndexSha256':hashlib.sha256(index.read_bytes()).hexdigest(),
            'sourcePackageSha256':hashlib.sha256(package.read_bytes()).hexdigest(),
            'socialNamespace':namespace,'unityProjectId':project[0],
            'deploymentStatus':'not deployed','emptyNamespaceAllowed':False},indent=2)+'\n')
        shutil.move(stage,output)
    return output


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--node-index',type=Path,required=True)
    parser.add_argument('--node-package',type=Path,required=True)
    parser.add_argument('--social-namespace',required=True)
    parser.add_argument('--output',type=Path,required=True)
    args=parser.parse_args()
    print(prepare(args.node_index,args.node_package,args.social_namespace,args.output))


if __name__=='__main__': main()
