"""Production entry point. Credentials stay on the server; storage must be durable."""
import os
from pathlib import Path
import google.auth
from google.auth.transport.requests import AuthorizedSession
from authentication import UnityPlayerAuthenticator
from application import CommerceApplication


def create_application():
    project_id=os.environ['DF_UNITY_PROJECT_ID']
    enabled=os.environ.get('DF_ENABLE_GOOGLE_PLAY','0')=='1'
    package=os.environ['DF_ANDROID_PACKAGE_NAME'] if enabled else ''
    database=Path(os.environ['DF_ACCOUNT_DB'])
    database.parent.mkdir(parents=True,exist_ok=True)
    google=None
    if enabled:
        credentials,_=google.auth.default(scopes=['https://www.googleapis.com/auth/androidpublisher'])
        google=AuthorizedSession(credentials)
    return CommerceApplication(database,UnityPlayerAuthenticator(project_id),google,package)

application=create_application()
