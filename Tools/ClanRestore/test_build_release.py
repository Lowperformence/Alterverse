import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[1]
spec=importlib.util.spec_from_file_location('build_release',HERE/'build_release.py')
builder=importlib.util.module_from_spec(spec);spec.loader.exec_module(builder)


class ReleaseTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.addCleanup(self.temp.cleanup)
        self.directory=Path(self.temp.name)
        self.index=HERE/'fixtures/node-2026-10-22.2.js'
        self.package=HERE/'fixtures/package.json'
        self.output=self.directory/'release'

    def prepare(self,**options):
        return builder.prepare(options.get('index',self.index),options.get('package',self.package),
                               options.get('namespace','existing-clans'),self.output,ROOT)

    def test_complete_bundle_reuses_supplied_node_package_and_existing_rules(self):
        before=self.index.read_bytes();package=self.package.read_bytes()
        self.prepare()
        self.assertEqual(self.index.read_bytes(),before)
        self.assertEqual((self.output/'package.json').read_bytes(),package)
        for path in ('Server/commerce/social.py','Server/commerce/clan_chat.py','Server/commerce/node_social.py',
                     'Assets/Alterverse/Resources/Commerce/ShardCatalog.json',
                     'Assets/Alterverse/Resources/Social/SafeIdentityCatalog.json'):
            self.assertEqual((self.output/path).read_bytes(),(ROOT/path).read_bytes())
        docker=(self.output/'Dockerfile').read_text()
        self.assertIn('DF_SOCIAL_NAMESPACE=existing-clans',docker)
        self.assertIn('DF_SOCIAL_ALLOW_EMPTY_NAMESPACE=0',docker)
        self.assertNotIn('__UNITY_PROJECT__',docker)
        self.assertTrue((self.output/'Server/commerce/node/start-with-clans.sh').exists())
        self.assertFalse(any((self.output/'Server/commerce').glob('test_*.py')))
        manifest=json.loads((self.output/'RELEASE.json').read_text())
        self.assertEqual(manifest['deploymentStatus'],'not deployed')
        self.assertEqual(manifest['socialNamespace'],'existing-clans')

    def test_later_hats_survive_without_using_saved_catalog_as_a_replacement(self):
        fresh=self.directory/'fresh.js'
        fresh.write_text(self.index.read_text().replace('const ITEMS = {',"const ITEMS = {\n  future_hat: item(1, [w('DF_HeadwearOwned_FutureHat')]),"))
        self.prepare(index=fresh)
        self.assertIn("future_hat: item(1, [w('DF_HeadwearOwned_FutureHat')])",(self.output/'index.js').read_text())

    def test_existing_output_is_never_overwritten(self):
        self.output.mkdir();(self.output/'keep').write_text('current release')
        with self.assertRaises(ValueError):self.prepare()
        self.assertEqual((self.output/'keep').read_text(),'current release')

    def test_namespace_must_be_deliberately_selected(self):
        for namespace in ('','../players','players/other','WrongNamespace'):
            with self.subTest(namespace=namespace),self.assertRaises(ValueError):self.prepare(namespace=namespace)
        self.assertFalse(self.output.exists())

    def test_unexpected_source_or_package_leaves_no_partial_release(self):
        source=self.directory/'other.js';source.write_text('export default 1;')
        with self.assertRaises(ValueError):self.prepare(index=source)
        package=self.directory/'other.json';package.write_text('{}')
        with self.assertRaises(ValueError):self.prepare(package=package)
        self.assertFalse(self.output.exists())


if __name__=='__main__':unittest.main()
