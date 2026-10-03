"""Verify Unity player JWTs before deriving the shared opaque account identity."""
import hashlib
import jwt

ISSUER = 'https://player-auth.services.api.unity.com'
JWKS_URL = ISSUER + '/.well-known/jwks.json'

class UnityPlayerAuthenticator:
    def __init__(self, project_id, jwks=None):
        if not isinstance(project_id,str) or not project_id.strip():
            raise ValueError('Unity project ID required')
        self.project_id=project_id
        self.jwks=jwks or jwt.PyJWKClient(JWKS_URL,cache_jwk_set=True,lifespan=28800,timeout=10)

    def __call__(self, authorization):
        if not isinstance(authorization,str) or not authorization.startswith('Bearer '):return None
        token=authorization[7:]
        if not token or len(token)>16384:return None
        try:
            key=self.jwks.get_signing_key_from_jwt(token)
            claims=jwt.decode(token,key.key,algorithms=['RS256'],issuer=ISSUER,
                options={'require':['exp','nbf','sub','iss','project_id'],'verify_aud':False})
            # Unity player tokens scope their audience through project_id. This is checked
            # explicitly rather than accepting an otherwise valid token from another game.
            if claims['project_id']!=self.project_id:return None
            player=claims['sub']
            if not isinstance(player,str) or not player or len(player)>256:return None
            return hashlib.sha256(player.encode('utf-8')).hexdigest()
        except jwt.PyJWTError:
            return None
