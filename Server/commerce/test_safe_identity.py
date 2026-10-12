"""Approved catalog, collision ownership and legacy profile migration on both stores."""
import json
import tempfile
import unittest
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
from unittest.mock import patch
from ledger import Ledger, ProfileConflict
from firestore_ledger import FirestoreLedger
from safe_identity import (PLAYER_NAMES,CLAN_NAMES,DESCRIPTIONS,NAME_KEY,NAME_SPACE,
    valid_player_name,valid_clan_name,valid_clan_tag,profile_name,with_player_name)
from test_firestore_social import Client,transactional

class SafeIdentityTests(unittest.TestCase):
    def test_catalog_capacity_boundaries_and_approved_content(self):
        self.assertEqual(NAME_SPACE,1_280_000_000)
        self.assertEqual(len(set(PLAYER_NAMES)),128)
        self.assertEqual(len(set(CLAN_NAMES)),128)
        self.assertEqual(len(set(DESCRIPTIONS)),32)
        for root in PLAYER_NAMES:
            self.assertRegex(root,r'^[A-Z][a-z]+[A-Z][a-z]+$')
            for suffix in ('0000000','9999999'):
                self.assertTrue(valid_player_name(root+suffix))
                self.assertLessEqual(len(root+suffix),16)
        for root in CLAN_NAMES:
            for suffix in ('0000000','9999999'):
                self.assertTrue(valid_clan_name(root+' '+suffix))
                self.assertLessEqual(len(root+' '+suffix),24)
        for description in DESCRIPTIONS:
            self.assertTrue(description.isascii())
            self.assertLessEqual(len(description),160)
            self.assertNotIn('://',description)
        for bad in (None,True,[],"Custom Name","SolarFox123456","SolarFox12345678",
                    "SolarFox１２３４５６７","solarfox0000001","SolarFox0000001\n"):
            self.assertFalse(valid_player_name(bad))
        for bad in ('CUSTOM','c0000001','C00000012','C１２３４５６７'):
            self.assertFalse(valid_clan_tag(bad))

    def test_unique_claims_rerolls_restarts_and_untrusted_text(self):
        with tempfile.TemporaryDirectory() as folder,patch('google.cloud.firestore.transactional',transactional):
            client=Client()
            for factory in (lambda:Ledger(Path(folder)/'names.db'),lambda:FirestoreLedger(client=client)):
                with self.subTest(adapter=type(factory()).__name__):
                    ledger=factory()
                    first=ledger.save_profile('a',with_player_name([], 'SolarFox0000001'),0)
                    collided=ledger.save_profile('b',with_player_name([], 'SolarFox0000001'),0)
                    self.assertTrue(valid_player_name(profile_name(collided['profile'])))
                    self.assertNotEqual(profile_name(first['profile']),profile_name(collided['profile']))
                    rerolled=ledger.save_profile('a',with_player_name(first['profile'],'LunarOwl0000002'),first['profileRevision'])
                    third=ledger.save_profile('c',with_player_name([],'SolarFox0000001'),0)
                    self.assertEqual(profile_name(third['profile']),'SolarFox0000001')
                    unsafe=ledger.save_profile('a',with_player_name(rerolled['profile'],'https://discord.gg/test'),rerolled['profileRevision'])
                    self.assertEqual(profile_name(unsafe['profile']),'LunarOwl0000002')
                    self.assertEqual(factory().wallet('a'),unsafe)
                    with self.assertRaises(ProfileConflict):
                        ledger.save_profile('a',first['profile'],first['profileRevision'])

    def test_concurrent_requests_for_same_name_are_unique(self):
        with tempfile.TemporaryDirectory() as folder,patch('google.cloud.firestore.transactional',transactional):
            for ledger in (Ledger(Path(folder)/'concurrent.db'),FirestoreLedger(client=Client())):
                with self.subTest(adapter=type(ledger).__name__):
                    def save(account):
                        return profile_name(ledger.save_profile(account,with_player_name([],'SolarFox1111111'),0)['profile'])
                    with ThreadPoolExecutor(max_workers=8) as pool:
                        names=list(pool.map(save,[str(i) for i in range(8)]))
                    self.assertEqual(len(set(names)),8)
                    self.assertTrue(all(valid_player_name(n) for n in names))

    def test_legacy_profile_migration_retains_progress_and_bumps_revision_once(self):
        entries=with_player_name([{'key':'DF_Shards','kind':0,'number':777}], 'Legacy Name')
        with tempfile.TemporaryDirectory() as folder,patch('google.cloud.firestore.transactional',transactional):
            for ledger in (Ledger(Path(folder)/'legacy.db'),FirestoreLedger(client=Client())):
                with self.subTest(adapter=type(ledger).__name__):
                    if hasattr(ledger,'connect'):
                        with ledger.connect() as db:
                            ledger.ensure(db,'legacy')
                            db.execute('UPDATE profile SET revision=5,data=? WHERE account=?',(json.dumps(entries),'legacy'))
                    else:
                        ref=ledger.accounts.document('legacy')
                        ledger.client.data[ref.path]=dict(ledger._empty(),profileRevision=5,profile=entries)
                    migrated=ledger.wallet('legacy')
                    self.assertEqual(migrated['profileRevision'],6)
                    self.assertTrue(valid_player_name(profile_name(migrated['profile'])))
                    self.assertEqual(next(e['number'] for e in migrated['profile'] if e['key']=='DF_Shards'),777)
                    self.assertEqual(ledger.wallet('legacy'),migrated)
                    with self.assertRaises(ProfileConflict):ledger.save_profile('legacy',entries,5)
