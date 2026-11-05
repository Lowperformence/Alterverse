import importlib.util
from pathlib import Path
import re
import subprocess
import tempfile
import unittest
from unittest.mock import patch
import zipfile

HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location('audit', HERE / 'audit_bundle.py')
audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)
MANIFEST = '''<manifest xmlns:android="http://schemas.android.com/apk/res/android"
package="com.lowperformence.dimensionsfront" android:versionCode="123">
<uses-sdk android:minSdkVersion="26" android:targetSdkVersion="36"/>
<uses-permission android:name="android.permission.INTERNET"/>
<uses-permission android:name="android.permission.RECORD_AUDIO"/>
{extra}</manifest>'''


class AuditTests(unittest.TestCase):
    def test_core_network_and_optional_voice_allowed(self):
        report = audit.inspect_manifest(MANIFEST.format(extra=''))
        self.assertEqual([], report['unexpected_sensitive_permissions'])
        self.assertEqual('36', report['target_sdk'])

    def test_restricted_permissions_in_both_declaration_forms(self):
        for permission in audit.UNUSED_PERMISSIONS:
            for tag in ('uses-permission', 'uses-permission-sdk-23'):
                xml = MANIFEST.format(extra=f'<{tag} android:name="{permission}"/>')
                self.assertEqual([permission], audit.inspect_manifest(xml)['unexpected_sensitive_permissions'])

    def test_final_bundle_checks_feature_modules_and_dex(self):
        with tempfile.TemporaryDirectory() as tmp:
            bundle = Path(tmp) / 'fixture.aab'
            with zipfile.ZipFile(bundle, 'w') as z:
                for module in ('base', 'chat'):
                    z.writestr(module + '/manifest/AndroidManifest.xml', b'protobuf fixture')
                    z.writestr(module + '/dex/classes.dex', b'fixture')
            def dump(command, **kwargs):
                extra = ('<uses-permission android:name="android.permission.READ_CONTACTS"/>'
                         if '--module=chat' in command else '')
                return subprocess.CompletedProcess(command, 0, MANIFEST.format(extra=extra))
            with patch.object(audit.subprocess, 'run', side_effect=dump):
                report = audit.audit_bundle(bundle, Path('bundletool.jar'))
            self.assertFalse(report['permission_check_passed'])
            self.assertEqual(14, report['dex_uncompressed_bytes'])
            self.assertEqual(64, len(report['sha256']))

    def test_editor_and_final_bundle_policy_match(self):
        source = (HERE.parents[1] / 'Assets/Alterverse/Editor/AndroidPermissionPolicy.cs').read_text()
        self.assertEqual(audit.UNUSED_PERMISSIONS,
                         set(re.findall(r'"(android\.permission\.[A-Z_]+)"', source)))

    def test_malformed_and_non_manifest_input_fails(self):
        with self.assertRaises(ValueError):
            audit.inspect_manifest('<application/>')
        with self.assertRaises(audit.ET.ParseError):
            audit.inspect_manifest('not XML')


if __name__ == '__main__':
    unittest.main()
