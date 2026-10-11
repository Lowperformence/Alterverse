"""Validate ranked deployment settings without opening storage or exposing secrets."""
import os
import re
from urllib.parse import urlsplit


def validate_ranked_environment(env):
    if env.get('DF_ENABLE_RANKED', '0') != '1':
        return
    storage = env.get('DF_ACCOUNT_STORAGE', 'sqlite').strip().lower()
    if storage not in ('sqlite', 'firestore'):
        raise ValueError('DF_ACCOUNT_STORAGE must be sqlite or firestore')
    if env.get('K_SERVICE') and storage != 'firestore':
        raise ValueError('Ranked on Cloud Run requires DF_ACCOUNT_STORAGE=firestore')
    if storage == 'sqlite' and not env.get('DF_ACCOUNT_DB'):
        raise ValueError('DF_ACCOUNT_DB is required for SQLite ranked storage')
    url = urlsplit(env.get('DF_RANKED_MATCHMAKER_URL', ''))
    if url.scheme != 'https' or not url.hostname or url.username or url.password or url.query or url.fragment:
        raise ValueError('DF_RANKED_MATCHMAKER_URL must be an HTTPS base URL without credentials, query or fragment')
    if not env.get('DF_RANKED_MATCHMAKER_TOKEN', '').strip():
        raise ValueError('DF_RANKED_MATCHMAKER_TOKEN is required')
    for key in ('DF_RANKED_ADMISSION_KEY', 'DF_RANKED_SERVER_KEY'):
        if len(env.get(key, '')) < 32:
            raise ValueError(key + ' must contain at least 32 characters')
    if env['DF_RANKED_ADMISSION_KEY'] == env['DF_RANKED_SERVER_KEY']:
        raise ValueError('Admission and dedicated-server keys must be different')
    if not re.fullmatch(r'[A-Za-z0-9_.:-]{1,128}', env.get('DF_RANKED_SEASON', 'S1')):
        raise ValueError('DF_RANKED_SEASON must be a nonempty identifier')


if __name__ == '__main__':
    try:
        if os.environ.get('DF_ENABLE_RANKED') != '1':
            raise ValueError('DF_ENABLE_RANKED must be 1 for this deployment check')
        validate_ranked_environment(os.environ)
    except ValueError as error:
        raise SystemExit(str(error)) from None
    print('Ranked deployment environment passed; no network or database calls performed.')
