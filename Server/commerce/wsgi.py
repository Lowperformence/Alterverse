"""Production entry point. Credentials stay on the server; storage must be durable."""
import os
from pathlib import Path
import google.auth
from google.auth.transport.requests import AuthorizedSession
from authentication import UnityPlayerAuthenticator
from application import CommerceApplication
from firestore_ledger import FirestoreLedger
from ledger import Ledger


def create_application():
    unity_project_id=os.environ['DF_UNITY_PROJECT_ID']
    enabled=os.environ.get('DF_ENABLE_GOOGLE_PLAY','0')=='1'
    package=os.environ['DF_ANDROID_PACKAGE_NAME'] if enabled else ''
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
    google=None
    if enabled:
        credentials,_=google.auth.default(scopes=['https://www.googleapis.com/auth/androidpublisher'])
        google=AuthorizedSession(credentials)
    return CommerceApplication(ledger,UnityPlayerAuthenticator(unity_project_id),google,package)

application=create_application()
