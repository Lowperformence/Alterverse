import copy
import io
import json
import unittest
from contextlib import redirect_stdout
from unittest.mock import patch
import setup_firestore as setup


class FirestoreSetupTests(unittest.TestCase):
    def setUp(self):
        self.service={'spec':{'template':{'spec':{'containers':[{'env':[
            {'name':'DF_ACCOUNT_STORAGE','value':'firestore'},
            {'name':'DF_FIRESTORE_PROJECT_ID','value':'database-project'},
            {'name':'DF_FIRESTORE_NAMESPACE','value':'custom-accounts'},
            {'name':'SMTP_PASSWORD','valueFrom':{'secretKeyRef':{'name':'private-secret','key':'1'}}}
        ]}]}}}}
        self.config=setup.definitions('custom-accounts')
    def indexes(self,state='READY'):
        result=[]
        for index in self.config['indexes']:
            value=copy.deepcopy(index);group=value.pop('collectionGroup')
            value['name']='projects/database-project/databases/(default)/collectionGroups/'+group+'/indexes/test'
            value['fields'].append({'fieldPath':'__name__','order':value['fields'][-1]['order']})
            value['state']=state;result.append(value)
        return result
    def test_derives_actual_database_project_and_namespace_from_existing_service(self):
        self.assertEqual(setup.settings(self.service,'run-project'),('database-project','custom-accounts'))
        self.assertTrue(all(row['collectionGroup'].startswith('custom-accounts-') for row in self.config['indexes']))
    def test_project_is_resolved_from_configured_service_number(self):
        config=json.dumps({'httpsEndpoint':'https://dimensions-front-purchase-server-123456.europe-west1.run.app'})
        with patch.object(setup.Path,'read_text',return_value=config),patch.object(setup,'run_json',return_value={'projectId':'actual-project'}) as read:
            self.assertEqual(setup.resolve_project(),'actual-project')
        read.assert_called_once_with(['gcloud','projects','describe','123456','--format=json'])
    def test_refuses_storage_migration_and_secret_config_guessing(self):
        env=self.service['spec']['template']['spec']['containers'][0]['env']
        env[0]['value']='sqlite'
        with self.assertRaises(ValueError):setup.settings(self.service,'run-project')
        env[0]['value']='firestore';env[2]={'name':'DF_FIRESTORE_NAMESPACE','valueFrom':{'secretKeyRef':{'name':'secret','key':'1'}}}
        with self.assertRaisesRegex(ValueError,'secret reference'):setup.settings(self.service,'run-project')
    def test_existing_indexes_are_not_recreated_or_deleted(self):
        commands=setup.setup_commands(self.config,self.indexes(),'database-project')
        self.assertEqual(len(commands),4)
        self.assertTrue(all('ttls' in command and '--enable-ttl' in command for command in commands))
        self.assertTrue(setup.ready(self.config,self.indexes()))
        self.assertFalse(setup.ready(self.config,self.indexes('CREATING')))
    def test_missing_indexes_get_exact_query_fields(self):
        commands=setup.setup_commands(self.config,[],'database-project')
        self.assertEqual(len(commands),6)
        self.assertIn('--field-config=field-path=clan,order=ascending',commands[0])
        self.assertIn('--field-config=field-path=created_at,order=descending',commands[0])
        self.assertIn('--field-config=field-path=sent_at,order=ascending',commands[1])
        self.assertTrue(all('--project=database-project' in command for command in commands))
        self.assertFalse(any('delete' in command for command in commands))
    def test_dry_run_never_mutates_and_never_prints_secret_settings(self):
        with patch('sys.argv',['setup','--project','run-project']),patch.object(setup,'run_json',side_effect=[self.service,[]]),patch.object(setup.subprocess,'run') as run,redirect_stdout(io.StringIO()) as output:
            setup.main()
        run.assert_not_called();self.assertNotIn('private-secret',output.getvalue());self.assertNotIn('SMTP_PASSWORD',output.getvalue())
    def test_deployment_is_blocked_until_all_indexes_are_ready(self):
        with patch('sys.argv',['setup','--project','run-project','--execute','--deploy']),patch.object(setup,'run_json',side_effect=[self.service,[],self.indexes('CREATING')]),patch.object(setup.subprocess,'run') as run,redirect_stdout(io.StringIO()):
            with self.assertRaisesRegex(SystemExit,'still in progress'):setup.main()
        self.assertFalse(any(call.args[0][:3]==['gcloud','run','deploy'] for call in run.call_args_list))


if __name__=='__main__':unittest.main()
