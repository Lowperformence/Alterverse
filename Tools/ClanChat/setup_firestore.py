"""Check/create clan-chat indexes and TTL using the existing Cloud Run settings.

Default: read configuration and print the planned commands. --execute applies them;
--deploy additionally rebuilds the existing account service once indexes are ready.
Requires gcloud authenticated as an operator with Firestore index/TTL permissions.
Secret values are neither requested nor printed. Existing service settings stay intact.
"""
import argparse
import json
from pathlib import Path
import re
import shlex
import subprocess
from urllib.parse import urlsplit

SERVICE='dimensions-front-purchase-server'
REGION='europe-west1'
ROOT=Path(__file__).resolve().parents[2]


def run_json(command):
    result=subprocess.run(command,check=True,capture_output=True,text=True)
    return json.loads(result.stdout)


def resolve_project(explicit=None):
    if explicit:return explicit
    config=json.loads((ROOT/'Assets/Alterverse/Resources/Commerce/AccountServerConfig.json').read_text())
    url=urlsplit(config['httpsEndpoint'])
    match=re.fullmatch(re.escape(SERVICE)+r'-([0-9]+)\.'+re.escape(REGION)+r'\.run\.app',url.hostname or '')
    if url.scheme!='https' or not match:
        raise ValueError('Cannot derive the existing Cloud Run project; supply --project explicitly')
    metadata=run_json(['gcloud','projects','describe',match[1],'--format=json'])
    return metadata['projectId']


def settings(service,project,social_namespace=None):
    containers=service['spec']['template']['spec']['containers']
    if len(containers)!=1:raise ValueError('Expected the existing single-container account service')
    env={entry['name']:entry for entry in containers[0].get('env',[])}
    def value(name,default):
        entry=env.get(name,{})
        if entry.get('valueFrom'):raise ValueError(name+' must be readable configuration, not a secret reference')
        return entry.get('value',default)
    bridge_namespace=value('DF_SOCIAL_NAMESPACE','')
    if bridge_namespace and social_namespace and bridge_namespace!=social_namespace:
        raise ValueError('The selected namespace differs from the existing clan runtime')
    namespace=social_namespace or bridge_namespace
    if not namespace:
        if value('DF_ACCOUNT_STORAGE','sqlite')!='firestore':raise ValueError('This service does not use Firestore; no storage migration is performed')
        namespace=value('DF_FIRESTORE_NAMESPACE','dimensions-front')
    database_project=value('DF_FIRESTORE_PROJECT_ID',project) or project
    for name,item in (('namespace',namespace),('project',database_project)):
        if not isinstance(item,str) or not re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9_-]{0,100}',item):raise ValueError('Invalid '+name)
    return database_project,namespace


def definitions(namespace):
    config=json.loads((ROOT/'Server/commerce/firestore-chat.indexes.json').read_text())
    for section in ('indexes','fieldOverrides'):
        for entry in config[section]:entry['collectionGroup']=entry['collectionGroup'].replace('dimensions-front-',namespace+'-',1)
    return config


def signature(index):
    group=index.get('collectionGroup')
    if not group:
        pieces=index.get('name','').split('/')
        if 'collectionGroups' not in pieces:return None
        group=pieces[pieces.index('collectionGroups')+1]
    fields=tuple((field.get('fieldPath'),field.get('order')) for field in index.get('fields',[]) if field.get('fieldPath')!='__name__')
    return group,index.get('queryScope','COLLECTION'),fields


def setup_commands(config,existing,project,database='(default)'):
    found={signature(index):index for index in existing}
    commands=[]
    for index in config['indexes']:
        if signature(index) in found:continue
        command=['gcloud','firestore','indexes','composite','create','--project='+project,'--database='+database,
                 '--collection-group='+index['collectionGroup'],'--query-scope=collection','--quiet']
        for field in index['fields']:
            command.append('--field-config=field-path='+field['fieldPath']+',order='+field['order'].lower())
        commands.append(command)
    for field in config['fieldOverrides']:
        if field.get('ttl'):
            commands.append(['gcloud','firestore','fields','ttls','update',field['fieldPath'],
                             '--project='+project,'--database='+database,'--collection-group='+field['collectionGroup'],
                             '--enable-ttl','--quiet'])
    return commands


def ready(config,existing):
    found={signature(index):index for index in existing}
    return all(found.get(signature(index),{}).get('state')=='READY' for index in config['indexes'])


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project',help='Existing Cloud Run project ID; default: resolve from the game account endpoint')
    parser.add_argument('--execute',action='store_true')
    parser.add_argument('--social-namespace',help='Verified existing clan namespace for the paired Node/Python account runtime')
    parser.add_argument('--deploy',action='store_true',help='Also deploy the existing account backend; requires --execute')
    args=parser.parse_args()
    if args.deploy and not args.execute:parser.error('--deploy requires --execute')
    try:
        args.project=resolve_project(args.project)
        if not re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9_-]{0,100}',args.project):raise ValueError('Invalid project ID')
        service=run_json(['gcloud','run','services','describe',SERVICE,'--project='+args.project,'--region='+REGION,'--format=json'])
        project,namespace=settings(service,args.project,args.social_namespace);config=definitions(namespace)
        env=service['spec']['template']['spec']['containers'][0].get('env',[])
        if args.deploy and (args.social_namespace or any(e['name']=='DF_SOCIAL_NAMESPACE' for e in env)):
            raise ValueError('The paired Node clan runtime must be deployed from its prepared release directory; --deploy here would replace the purchase service with another runtime')
        listing=['gcloud','firestore','indexes','composite','list','--project='+project,'--database=(default)','--format=json']
        existing=run_json(listing)
        for command in setup_commands(config,existing,project):
            print(shlex.join(command),flush=True)
            if args.execute:subprocess.run(command,check=True,capture_output=True,text=True)
        if not args.execute:
            print('Inspection only. Add --execute to apply indexes and TTL; add --deploy to rebuild the backend.')
            return
        if not ready(config,run_json(listing)):
            raise ValueError('Index creation is still in progress or failed. Re-run this command once the indexes are READY; the backend has not been redeployed.')
        print('Required clan-chat indexes are READY.',flush=True)
        if args.deploy:
            # No env/secret/IAM flags: preserve the existing service configuration.
            subprocess.run(['gcloud','run','deploy',SERVICE,'--project='+args.project,'--region='+REGION,'--source='+str(ROOT)],check=True)
    except subprocess.CalledProcessError:
        raise SystemExit('gcloud failed. Check operator login, index/TTL permissions and Cloud Run/build logs. No secret values were printed.') from None
    except (OSError,ValueError,KeyError) as error:
        raise SystemExit(str(error)) from None


if __name__=='__main__':main()
