"""Inspect an AAB with Google's bundletool; this does not certify Play compliance."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET
import zipfile

ANDROID = '{http://schemas.android.com/apk/res/android}'
UNUSED_PERMISSIONS = {
    'android.permission.' + name for name in (
        'ACCESS_COARSE_LOCATION', 'ACCESS_FINE_LOCATION', 'ACCESS_BACKGROUND_LOCATION',
        'READ_CONTACTS', 'WRITE_CONTACTS', 'MANAGE_CONTACTS', 'MANAGE_EXTERNAL_STORAGE',
        'READ_CALL_LOG', 'WRITE_CALL_LOG', 'PROCESS_OUTGOING_CALLS', 'READ_SMS',
        'SEND_SMS', 'RECEIVE_SMS', 'RECEIVE_MMS', 'RECEIVE_WAP_PUSH', 'ACCESS_LOCAL_NETWORK')
}


def inspect_manifest(xml):
    root = ET.fromstring(xml)
    if root.tag != 'manifest':
        raise ValueError('Expected a decoded Android manifest')
    permissions = sorted({e.get(ANDROID + 'name') for e in root
                          if e.tag in ('uses-permission', 'uses-permission-sdk-23')
                          and e.get(ANDROID + 'name')})
    sdk = root.find('uses-sdk')
    return {
        'package': root.get('package'),
        'version_code': root.get(ANDROID + 'versionCode'),
        'target_sdk': sdk.get(ANDROID + 'targetSdkVersion') if sdk is not None else None,
        'permissions': permissions,
        'unexpected_sensitive_permissions': sorted(set(permissions) & UNUSED_PERMISSIONS),
    }


def audit_bundle(bundle, bundletool):
    with zipfile.ZipFile(bundle) as archive:
        modules = sorted({p.split('/')[0] for p in archive.namelist()
                          if p.endswith('/manifest/AndroidManifest.xml')})
        if 'base' not in modules:
            raise ValueError('AAB has no base module manifest')
        dex = {p.filename: p.file_size for p in archive.infolist()
               if '/dex/' in p.filename and p.filename.endswith('.dex')}
        native = sorted(p for p in archive.namelist() if '/lib/' in p and p.endswith('.so'))
    manifests = {}
    for module in modules:
        result = subprocess.run(['java', '-jar', str(bundletool), 'dump', 'manifest',
                                 '--bundle=' + str(bundle), '--module=' + module],
                                check=True, capture_output=True, text=True)
        manifests[module] = inspect_manifest(result.stdout)
    total = sum(dex.values())
    with bundle.open('rb') as stream:
        digest = hashlib.file_digest(stream, 'sha256').hexdigest()
    return {
        'bundle': bundle.name, 'sha256': digest, 'modules': manifests,
        'dex_uncompressed_bytes': total, 'dex_files': dex,
        'game_dex_above_50_mb_estimate': total > 50_000_000,
        'native_libraries': native,
        'permission_check_passed': not any(
            m['unexpected_sensitive_permissions'] for m in manifests.values()),
        'still_requires_verification': [
            'Play Console DEX size and each 25% optimization threshold (if game DEX >50 MB)',
            'Android vitals P90 Anon RSS + Swap and bitmap memory by RAM tier/app state',
            'Signing, native 16 KB alignment, installed-device tests and Play pre-launch report',
            'Trader status, legal documents, Data Safety and applicable Japan authorization',
        ],
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('bundle', type=Path)
    parser.add_argument('--bundletool', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    report = audit_bundle(args.bundle, args.bundletool)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print('Report:', args.output)
    if not report['permission_check_passed']:
        raise SystemExit('Unexpected sensitive permissions in final AAB; do not upload this build.')


if __name__ == '__main__':
    main()
