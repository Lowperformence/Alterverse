"""Production entry point. Credentials stay on the server; storage must be durable."""
import logging
import os
from pathlib import Path
import google.auth
from google.auth.exceptions import DefaultCredentialsError
from google.auth.transport.requests import AuthorizedSession
from authentication import UnityPlayerAuthenticator
from application import CommerceApplication
from firestore_ledger import FirestoreLedger
from ledger import Ledger
from ranked import Ranked, EdgegapRankedGateway


def create_application():
    if os.environ.get('DF_ENABLE_RANKED', '0') == '1':
        from ranked_configuration import validate_ranked_environment
        validate_ranked_environment(os.environ)
    unity_project_id=os.environ['DF_UNITY_PROJECT_ID']
    enabled=os.environ.get('DF_ENABLE_GOOGLE_PLAY','0')=='1'
    package=os.environ.get('DF_ANDROID_PACKAGE_NAME','').strip() if enabled else ''
    storage=os.environ.get('DF_ACCOUNT_STORAGE','sqlite').strip().lower()
    if storage == 'firestore':
        ledger=FirestoreLedger(
            project_id=os.environ.get('DF_FIRESTORE_PROJECT_ID') or None,
            namespace=os.environ.get('DF_FIRESTORE_NAMESPACE','dimensions-front'))
    elif storage == 'sqlite':
        database=Path(os.environ['DF_ACCOUNT_DB'])
        database.parent.mkdir(parents=True,exist_ok=True)
        ledger=Ledger(database)
    else:
        raise ValueError('DF_ACCOUNT_STORAGE must be sqlite or firestore')
    # Do not shadow the imported google module: the old local 'google=None'
    # crashed service startup as soon as DF_ENABLE_GOOGLE_PLAY was enabled.
    # Account/profile functionality must remain available if billing is misconfigured.
    publisher_session=None
    if enabled:
        if not package:
            logging.error('Google Play billing disabled: DF_ANDROID_PACKAGE_NAME is missing')
        else:
            try:
                credentials,_=google.auth.default(scopes=['https://www.googleapis.com/auth/androidpublisher'])
                publisher_session=AuthorizedSession(credentials)
            except DefaultCredentialsError:
                logging.error('Google Play billing disabled: application credentials unavailable')
    ranked = None
    server_key = os.environ.get('DF_RANKED_SERVER_KEY', '')
    if os.environ.get('DF_ENABLE_RANKED', '0') == '1':
        if len(server_key) < 32: raise ValueError('DF_RANKED_SERVER_KEY must contain at least 32 characters')
        ranked = Ranked(ledger, EdgegapRankedGateway(os.environ['DF_RANKED_MATCHMAKER_URL'],
                        os.environ['DF_RANKED_MATCHMAKER_TOKEN']), os.environ['DF_RANKED_ADMISSION_KEY'],
                        os.environ.get('DF_RANKED_SEASON', 'S1'))
    subscription_options = {}
    if os.environ.get('DF_ENABLE_SHARD_SUBSCRIPTIONS', '0') == '1':
        from subscriptions import SubscriptionService, GooglePushAuthenticator
        if publisher_session is None or not package:
            raise ValueError('Subscriptions require Google Play publisher access')
        push_auth = GooglePushAuthenticator(os.environ['DF_RTDN_AUDIENCE'], os.environ['DF_RTDN_SERVICE_ACCOUNT'])
        subscription_options['subscriptions'] = SubscriptionService(ledger, publisher_session, package, push_auth)
    return CommerceApplication(ledger,UnityPlayerAuthenticator(unity_project_id),publisher_session,package,
                               **subscription_options,
                               **({'ranked': ranked, 'ranked_server_key': server_key} if ranked is not None else {}))

application=create_application()
