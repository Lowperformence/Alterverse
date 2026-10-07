"""WSGI adapter: deployment injects authentication and an authorized Google API session.
Never deploy with an authenticator that simply decodes an unsigned JWT or trusts a header ID.
"""
import json
from urllib.parse import quote
from ledger import Ledger, PACKS, COSMETICS, ProfileConflict
from social import Social, SocialForbidden

class CommerceApplication:
    def __init__(self, ledger_or_path, authenticate, google_session, package_name):
        self.ledger = ledger_or_path if hasattr(ledger_or_path, 'wallet') else Ledger(ledger_or_path)
        if hasattr(self.ledger, 'connect'):
            self.social = Social(self.ledger)
        else:
            from firestore_social import FirestoreSocial
            self.social = FirestoreSocial(self.ledger)
        self.authenticate = authenticate
        self.google = google_session  # google.auth.transport.requests.AuthorizedSession, server credentials only
        self.package = package_name
    def __call__(self, env, start_response):
        try:
            if env.get('REQUEST_METHOD') != 'POST': raise ValueError('POST required')
            size = int(env.get('CONTENT_LENGTH') or '0')
            if size < 0 or size > 262144: raise ValueError('Request too large')
            # Must validate signature/issuer/audience/expiry and return the stable obfuscated account hash.
            account = self.authenticate(env.get('HTTP_AUTHORIZATION', ''))
            if not account: raise PermissionError('Authentication required')
            data = json.loads(env['wsgi.input'].read(size) or b'{}')
            if not isinstance(data, dict): raise ValueError('JSON object required')
            path = env.get('PATH_INFO')
            if isinstance(path, str) and path.startswith('/social/'):
                social = self.social.dispatch(account, path, data)
                body = json.dumps({'fulfilled': True, 'social': social}).encode()
                start_response('200 OK', [('Content-Type', 'application/json'), ('Cache-Control', 'no-store')])
                return [body]
            if path == '/wallet': wallet = self.ledger.wallet(account)
            elif path == '/profile/save': wallet = self.ledger.save_profile(account, data.get('profile'), data.get('expectedProfileRevision'))
            elif path == '/wallet/claim': wallet = self.ledger.claim(account)
            elif path == '/wallet/claim/ack': wallet = self.ledger.acknowledge_claim(account, data.get('claimId'))
            elif path == '/wallet/spend':
                wallet = self.ledger.purchase(account, data.get('cosmeticId'), data.get('operationId'), data.get('expectedProfileRevision'))
            elif path == '/cosmetics/buy':
                sku = data.get('cosmeticId')
                if not isinstance(sku, str) or sku not in COSMETICS: raise ValueError('Unknown cosmetic')
                wallet = self.ledger.buy(account, sku)
            elif path == '/google-play/fulfill':
                if self.google is None or not self.package: raise ValueError('Billing unavailable')
                product, token = data.get('productId'), data.get('purchaseToken')
                if not isinstance(product, str) or product not in PACKS: raise ValueError('Unknown product')
                if not isinstance(token, str) or not token.strip() or len(token) > 8192: raise ValueError('Invalid purchase token')
                url = ('https://androidpublisher.googleapis.com/androidpublisher/v3/applications/' + quote(self.package, safe='')
                       + '/purchases/products/' + quote(product, safe='') + '/tokens/' + quote(token, safe=''))
                response = self.google.get(url, timeout=15)
                response.raise_for_status()
                wallet = self.ledger.fulfill(account, product, token, response.json())
            else: raise ValueError('Unknown route')
            if hasattr(self.social, 'sync_wallet'):
                self.social.sync_wallet(account, wallet)
            wallet['accountHash'] = account
            wallet['billingReady'] = self.google is not None and bool(self.package)
            body = json.dumps({'fulfilled': True, 'wallet': wallet}).encode()
            start_response('200 OK', [('Content-Type', 'application/json'), ('Cache-Control', 'no-store')])
        except ProfileConflict:
            body = b'{"fulfilled":false,"conflict":true}'
            start_response('409 Conflict', [('Content-Type', 'application/json'), ('Cache-Control','no-store')])
        except SocialForbidden:
            body = b'{"fulfilled":false}'; start_response('403 Forbidden', [('Content-Type', 'application/json'), ('Cache-Control','no-store')])
        except PermissionError:
            body = b'{"fulfilled":false}'; start_response('401 Unauthorized', [('Content-Type', 'application/json')])
        except (ValueError, KeyError) as error:
            if isinstance(env.get('PATH_INFO'), str) and env['PATH_INFO'].startswith('/social/'):
                body = json.dumps({'fulfilled': False, 'message': str(error)}).encode()
                start_response('400 Bad Request', [('Content-Type', 'application/json'), ('Cache-Control', 'no-store')])
                return [body]
            body = b'{"fulfilled":false}'; start_response('400 Bad Request', [('Content-Type', 'application/json')])
        except Exception:
            # Do not expose purchase tokens/credentials in response or logs. Upstream outage is retryable.
            body = b'{"fulfilled":false}'; start_response('503 Service Unavailable', [('Content-Type', 'application/json')])
        return [body]


