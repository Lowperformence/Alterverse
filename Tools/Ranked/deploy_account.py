"""Deploy ranked to the existing Cloud Run account service using Secret Manager refs.

Default: inspect the existing service and print the command. --execute deploys it.
Requires an authenticated gcloud installation; never reads or prints secret values.
"""
import argparse
import json
from pathlib import Path
import re
import shlex
import subprocess
from urllib.parse import urlsplit

SERVICE = 'dimensions-front-purchase-server'
REGION = 'europe-west1'


def deployment_command(args, service, root):
    containers = service['spec']['template']['spec']['containers']
    if len(containers) != 1:
        raise ValueError('Expected the existing single-container account service')
    env = {entry['name']: entry for entry in containers[0].get('env', [])}
    if env.get('DF_ACCOUNT_STORAGE', {}).get('value') != 'firestore':
        raise ValueError('Existing account service must already use Firestore; storage is not migrated automatically')
    if 'DF_UNITY_PROJECT_ID' not in env:
        raise ValueError('Existing Unity authentication configuration is missing')
    url = urlsplit(args.matchmaker_url)
    if url.scheme != 'https' or not url.hostname or url.username or url.password or url.query or url.fragment or ',' in args.matchmaker_url:
        raise ValueError('Matchmaker URL must be an HTTPS base URL')
    refs = [args.matchmaker_secret, args.admission_secret, args.server_secret]
    if any(not re.fullmatch(r'[A-Za-z0-9_-]+:[1-9][0-9]*', ref) for ref in refs):
        raise ValueError('Secrets require existing Secret Manager references NAME:NUMERIC_VERSION')
    if len({ref.split(':')[0] for ref in refs}) != 3:
        raise ValueError('Use three separate secrets for matchmaker, admission and server access')
    if not re.fullmatch(r'[A-Za-z0-9_.:-]{1,128}', args.season):
        raise ValueError('Invalid season identifier')
    settings = f'DF_ENABLE_RANKED=1,DF_RANKED_MATCHMAKER_URL={args.matchmaker_url},DF_RANKED_SEASON={args.season}'
    secrets = ','.join(f'{name}={ref}' for name, ref in zip(
        ('DF_RANKED_MATCHMAKER_TOKEN', 'DF_RANKED_ADMISSION_KEY', 'DF_RANKED_SERVER_KEY'), refs))
    return ['gcloud', 'run', 'deploy', SERVICE, '--project', args.project, '--region', REGION,
            '--source', str(root), '--update-env-vars', settings, '--update-secrets', secrets]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', required=True, help='Existing Google Cloud project ID')
    parser.add_argument('--matchmaker-url', required=True)
    for name in ('matchmaker', 'admission', 'server'):
        parser.add_argument(f'--{name}-secret', required=True, help='Existing secret NAME:VERSION; not its value')
    parser.add_argument('--season', default='S1')
    parser.add_argument('--execute', action='store_true')
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[2]
    try:
        response = subprocess.run(['gcloud', 'run', 'services', 'describe', SERVICE,
                                   '--project', args.project, '--region', REGION, '--format=json'],
                                  check=True, capture_output=True, text=True)
        command = deployment_command(args, json.loads(response.stdout), root)
        print(shlex.join(command))
        if args.execute:
            subprocess.run(command, check=True)
        else:
            print('Checked existing service. Run again with --execute to build and deploy.')
    except (OSError, subprocess.CalledProcessError, ValueError, KeyError) as error:
        # Captured describe output can contain account settings; never echo it.
        if isinstance(error, subprocess.CalledProcessError):
            raise SystemExit('gcloud failed. Check login, project permissions, and service/build logs.') from None
        raise SystemExit(str(error)) from None


if __name__ == '__main__':
    main()
