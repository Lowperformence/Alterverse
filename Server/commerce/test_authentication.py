import hashlib
import time
import unittest
from types import SimpleNamespace
from unittest.mock import Mock
import jwt
from cryptography.hazmat.primitives.asymmetric import rsa
from authentication import UnityPlayerAuthenticator, ISSUER

class AuthenticationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.private=rsa.generate_private_key(public_exponent=65537,key_size=2048)
        cls.other=rsa.generate_private_key(public_exponent=65537,key_size=2048)
    def setUp(self):
        self.jwks=Mock();self.jwks.get_signing_key_from_jwt.return_value=SimpleNamespace(key=self.private.public_key())
        self.authenticate=UnityPlayerAuthenticator('project-a',self.jwks)
    def token(self,**override):
        claims={'iss':ISSUER,'sub':'player-a','project_id':'project-a','nbf':int(time.time())-1,'exp':int(time.time())+60}
        claims.update(override)
        return jwt.encode(claims,self.private,algorithm='RS256',headers={'kid':'test-key'})
    def test_valid_identity_matches_client_account_hash(self):
        self.assertEqual(self.authenticate('Bearer '+self.token()),hashlib.sha256(b'player-a').hexdigest())
    def test_new_session_for_same_player_recovers_same_account(self):
        original = self.token(jti='old-device-session')
        restored = self.token(jti='reinstalled-app-session', exp=int(time.time())+120)
        self.assertNotEqual(original, restored)
        self.assertEqual(self.authenticate('Bearer '+original), self.authenticate('Bearer '+restored))
        self.assertNotEqual(self.authenticate('Bearer '+restored), self.authenticate('Bearer '+self.token(sub='another-player')))
    def test_expired_wrong_issuer_project_and_not_yet_valid_rejected(self):
        for override in ({'exp':int(time.time())-10},{'nbf':int(time.time())+600},{'iss':'other'},{'project_id':'other'},{'sub':''}):
            self.assertIsNone(self.authenticate('Bearer '+self.token(**override)))
    def test_unsigned_wrong_signature_and_missing_claims_rejected(self):
        payload={'iss':ISSUER,'sub':'player-a','project_id':'project-a','nbf':0,'exp':int(time.time())+60}
        unsigned=jwt.encode(payload,key=None,algorithm='none')
        other=jwt.encode(payload,self.other,algorithm='RS256')
        missing=jwt.encode({'sub':'player-a'},self.private,algorithm='RS256')
        for token in (unsigned,other,missing,'malformed'): self.assertIsNone(self.authenticate('Bearer '+token))
    def test_untrusted_header_ids_are_not_identities(self):
        for header in ('','player-a','Basic player-a','Bearer ','Bearer '+('x'*20000)):
            self.assertIsNone(self.authenticate(header))
    def test_unknown_signing_key_rejected(self):
        self.jwks.get_signing_key_from_jwt.side_effect=jwt.PyJWKClientError('unknown key')
        self.assertIsNone(self.authenticate('Bearer '+self.token()))

if __name__=='__main__':unittest.main()
