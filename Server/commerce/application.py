"""WSGI adapter: deployment injects authentication and an authorized Google API session.
Never deploy with an authenticator that simply decodes an unsigned JWT or trusts a header ID.
"""
import json
from urllib.parse import quote
from ledger import Ledger

class CommerceApplication:
    def __init__(self, ledger_path, authenticate, google_session, package_name):
        self.ledger = Ledger(ledger_path)
        self.authenticate = authenticate
        self.google = google_session  # google.auth.transport.requests.AuthorizedSession, server credentials only
        self.package = package_name
    def __call__(self, env, start_response):
        try:
            if env.get('REQUEST_METHOD') != 'POST': raise ValueError('POST required')
            size = int(env.get('CONTENT_LENGTH') or '0')
            if size < 0 or size > 32768: raise ValueError('Request too large')
            # Must validate signature/issuer/audience/expiry and return the stable obfuscated account hash.
            account = self.authenticate(env.get('HTTP_AUTHORIZATION', ''))
            if not account: raise PermissionError('Authentication required')
            data = json.loads(env['wsgi.input'].read(size) or b'{}')
            path = env.get('PATH_INFO')
            if path == '/wallet': wallet = self.ledger.wallet(account)
            elif path == '/cosmetics/buy': wallet = self.ledger.buy(account, data['cosmeticId'])
            elif path == '/google-play/fulfill':
                product, token = data['productId'], data['purchaseToken']
                url = ('https://androidpublisher.googleapis.com/androidpublisher/v3/applications/' + quote(self.package, safe='')
                       + '/purchases/products/' + quote(product, safe='') + '/tokens/' + quote(token, safe=''))
                response = self.google.get(url, timeout=15)
                response.raise_for_status()
                wallet = self.ledger.fulfill(account, product, token, response.json())
            else: raise ValueError('Unknown route')
            body = json.dumps({'fulfilled': True, 'wallet': wallet}).encode()
            start_response('200 OK', [('Content-Type', 'application/json'), ('Cache-Control', 'no-store')])
        except PermissionError:
            body = b'{"fulfilled":false}'; start_response('401 Unauthorized', [('Content-Type', 'application/json')])
        except (ValueError, KeyError):
            body = b'{"fulfilled":false}'; start_response('400 Bad Request', [('Content-Type', 'application/json')])
        except Exception:
            # Do not expose purchase tokens/credentials in response or logs. Upstream outage is retryable.
            body = b'{"fulfilled":false}'; start_response('503 Service Unavailable', [('Content-Type', 'application/json')])
        return [body]
