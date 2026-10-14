"""Security and durability scenarios run against both persistence adapters."""
import io
import json
import tempfile
import unittest
import uuid
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
from unittest.mock import Mock, patch
from application import CommerceApplication
from clan_chat import ChatConfig, ClanChat, CHAT_VERSION, LEADER_VERSION, REASONS
from chat_delivery import ChatDelivery
from ledger import Ledger
from social import Social, SocialForbidden


class ClanChatTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory();self.addCleanup(self.temp.cleanup)
        self.clock = 1800000000000
        self.timer = patch.object(ClanChat, 'now', staticmethod(lambda: self.clock));self.timer.start();self.addCleanup(self.timer.stop)
        self.config = ChatConfig(admins=frozenset({'admin'}), admin_email='admin@example.test')
        self.ledger = self.make_ledger()
        self.social = self.make_social()
        for who in ('a', 'b', 'c', 'admin'):
            self.ledger.save_profile(who, [{'key':'DF_Shards','kind':0,'number':2000}], self.ledger.wallet(who)['profileRevision'])
        self.ids = {who:self.call(who)['me']['playerId'] for who in ('a', 'b', 'c', 'admin')}
        self.clan = self.create('a')
        self.call('b', '/social/clan/join', clanId=self.clan)
        for who in ('a', 'b'): self.accept(who)

    def make_ledger(self): return Ledger(Path(self.temp.name) / 'chat.db')
    def make_social(self): return Social(self.ledger, chat_config=self.config)
    def call(self, who, route='/social/state', **data): return self.social.dispatch(who, route, data)
    def create(self, who, **extra):
        data=dict(operationId=uuid.uuid4().hex, expectedProfileRevision=self.ledger.wallet(who)['profileRevision'],
                  accepted=True, consentVersion=LEADER_VERSION)
        data.update(extra)
        return self.call(who, '/social/clan/create', **data)['clan']['clanId']
    def accept(self, who): return self.call(who, '/social/chat/consent', accepted=True, consentVersion=CHAT_VERSION, ageBand='13-17')
    def send(self, who='a', text='Hello team', **extra):
        self.clock += 2000
        data=dict(clanId=self.clan, text=text, operationId=uuid.uuid4().hex[:20]);data.update(extra)
        return self.call(who, '/social/chat/send', **data)
    def report(self, message):
        return self.call('b', '/social/chat/report', clanId=self.clan, messageId=message, reason=REASONS[0])['reportId']

    def test_creation_requires_active_current_consent_and_does_not_debit_on_rejection(self):
        before=self.ledger.wallet('c')
        for data in (dict(accepted=False), dict(consentVersion='old'), dict(accepted=1)):
            with self.assertRaises(ValueError): self.create('c', **data)
        self.assertEqual(self.ledger.wallet('c'), before)
        self.assertTrue(self.call('a')['leaderAccepted']);self.assertEqual(self.call('a')['leaderAcceptedAt'],1800000000000)

    def test_first_read_requires_consent_and_under13_cannot_accept(self):
        other=self.create('c')
        with self.assertRaises(SocialForbidden): self.call('c', '/social/chat/read', clanId=other)
        for data in (dict(accepted=False, ageBand='18+'), dict(accepted=True, ageBand='under13'),dict(accepted=True,ageBand='18+',consentVersion='old')):
            request=dict(accepted=True,ageBand='18+',consentVersion=CHAT_VERSION);request.update(data)
            with self.assertRaises(ValueError): self.call('c','/social/chat/consent',**request)
        accepted=self.accept('c');timestamp=accepted['chatAcceptedAt'];self.clock+=2000
        self.assertEqual(self.accept('c')['chatAcceptedAt'],timestamp)

    def test_private_clan_ids_message_ids_and_names_cannot_be_spoofed(self):
        other=self.create('c');self.accept('c')
        sent=self.send(name='Forged',account='c',createdAt=0,role='leader')
        message=sent['messages'][0]
        self.assertEqual(message['name'],self.call('a')['me']['name']);self.assertNotEqual(message['createdAt'],0)
        for route in ('read','send','report','moderate'):
            with self.subTest(route=route),self.assertRaises(SocialForbidden):
                self.call('c','/social/chat/'+route,clanId=self.clan,text='Leak',operationId=uuid.uuid4().hex[:20],messageId=message['messageId'],reason=REASONS[0],playerId=self.ids['b'],action='mute')
        with self.assertRaises(SocialForbidden):self.call('c','/social/chat/report',clanId=other,messageId=message['messageId'],reason=REASONS[0])
        self.assertNotIn('Hello team',json.dumps(self.call('c','/social/chat/read',clanId=other)))

    def test_leave_kick_close_and_ban_revoke_even_unchanged_room_polls(self):
        revision=self.send()['chatRevision']
        self.call('b','/social/clan/leave')
        with self.assertRaises(SocialForbidden):self.call('b','/social/chat/read',knownRevision=revision,clanId=self.clan)
        self.call('b','/social/clan/join',clanId=self.clan)
        self.call('a','/social/clan/kick',playerId=self.ids['b'])
        with self.assertRaises(SocialForbidden):self.call('b','/social/chat/read',knownRevision=revision)
        self.call('b','/social/clan/join',clanId=self.clan)
        self.call('a','/social/chat/moderate',action='ban',playerId=self.ids['b'])
        with self.assertRaises(SocialForbidden):self.call('b','/social/clan/join',clanId=self.clan)
        self.call('admin','/social/chat/admin/action',action='close',clanId=self.clan)
        with self.assertRaises(SocialForbidden):self.call('a','/social/chat/read',knownRevision=revision)

    def test_mute_warn_unmute_and_role_enforcement(self):
        with self.assertRaises(SocialForbidden):self.call('b','/social/chat/moderate',action='mute',playerId=self.ids['a'],role='leader')
        self.call('a','/social/chat/moderate',action='warn',playerId=self.ids['b'])
        self.call('a','/social/chat/moderate',action='mute',playerId=self.ids['b'],minutes=60)
        self.assertEqual(self.call('b')['warnings'],1)
        with self.assertRaises(SocialForbidden):self.send('b')
        self.call('a','/social/chat/moderate',action='unmute',playerId=self.ids['b'])
        self.assertEqual(len(self.send('b')['messages']),1)

    def test_leadership_transfer_requires_recipient_agreement(self):
        with self.assertRaises(ValueError):self.call('a','/social/clan/transfer',playerId=self.ids['b'])
        self.call('b','/social/chat/leader-consent',accepted=True,consentVersion=LEADER_VERSION)
        self.call('a','/social/clan/transfer',playerId=self.ids['b'])
        self.assertEqual(self.call('b')['role'],'leader')

    def test_blocks_hide_both_directions_and_can_be_reversed(self):
        revision=self.send()['chatRevision']
        self.call('b','/social/block',playerId=self.ids['a'])
        state=self.call('b','/social/chat/read',knownRevision=revision)
        self.assertEqual(state['messages'],[])
        self.assertEqual(len(state['blocked']),1)
        self.assertEqual(self.send('b')['messages'][0]['playerId'],self.ids['b'])
        self.assertTrue(all(m['playerId']!=self.ids['b'] for m in self.call('a','/social/chat/read')['messages']))
        self.call('b','/social/unblock',playerId=self.ids['a'])
        self.assertEqual(len(self.call('b','/social/chat/read')['messages']),2)

    def test_length_links_contact_details_controls_and_words_are_rejected(self):
        for text in ('', 'x'*501, 'https://example.test', 'example.com', 'a@example.test', '123456789', 'hello\u202e', 'a r s c h l o c h'):
            with self.subTest(text=text),self.assertRaises(ValueError):self.send(text=text)
        self.assertEqual(self.send(text='Ready 🚀')['messages'][0]['text'],'Ready 🚀')

    def test_send_retry_is_idempotent_and_flood_is_rejected(self):
        operation=uuid.uuid4().hex[:20]
        message=self.send(operationId=operation)['sentMessageId']
        retry=self.call('a','/social/chat/send',text='Hello team',operationId=operation)
        self.assertEqual(retry['sentMessageId'],message);self.assertEqual(len(retry['messages']),1)
        with self.assertRaises(ValueError):self.call('a','/social/chat/send',text='New message',operationId=uuid.uuid4().hex[:20])
        with self.assertRaises(ValueError):self.send(operationId=operation,text='Changed')
        with self.assertRaises(ValueError):self.send(text='Hello team')

    def test_concurrent_flood_commits_only_one_message(self):
        def attempt(i):
            try:self.call('a','/social/chat/send',text='message '+str(i),operationId=uuid.uuid4().hex[:20]);return 1
            except ValueError:return 0
        with ThreadPoolExecutor(max_workers=6) as pool:admitted=sum(pool.map(attempt,range(6)))
        self.assertEqual(admitted,1);self.assertEqual(len(self.call('b','/social/chat/read')['messages']),1)

    def test_report_is_private_durable_and_evidence_survives_message_removal(self):
        message=self.send()['sentMessageId'];report=self.report(message)
        self.assertEqual(self.report(message),report)
        with self.assertRaises(SocialForbidden):self.call('b','/social/chat/admin/reports',isAdmin=True)
        self.social=self.make_social()
        stored=self.call('admin','/social/chat/admin/reports')['reports'][0]
        self.assertEqual(stored['clanId'],self.clan);self.assertEqual(stored['messageId'],message)
        self.assertEqual(stored['reporterId'],self.ids['b']);self.assertEqual(stored['reason'],REASONS[0])
        self.assertNotIn('account',json.dumps(stored))
        self.call('admin','/social/chat/admin/action',action='remove',reportId=report)
        self.assertEqual(self.call('b','/social/chat/read')['messages'],[])
        self.assertEqual(self.call('admin','/social/chat/admin/reports')['reports'][0]['evidence']['text'],'Hello team')

    def test_player_reports_and_suspension_have_server_authority(self):
        report=self.call('b','/social/chat/report',playerId=self.ids['a'],reason=REASONS[1])['reportId']
        self.call('admin','/social/chat/admin/action',action='suspend',reportId=report)
        with self.assertRaises(SocialForbidden):self.social.check_account_allowed('a')
        with self.assertRaises(SocialForbidden):self.call('a')
        self.call('admin','/social/chat/admin/action',action='unsuspend',reportId=report)
        self.social.check_account_allowed('a')

    def test_unchanged_poll_and_expired_messages(self):
        revision=self.send()['chatRevision']
        unchanged=self.call('b','/social/chat/read',knownRevision=revision)
        self.assertTrue(unchanged['unchanged']);self.assertNotIn('messages',unchanged)
        self.clock+=31*86400000
        self.assertEqual(self.call('b','/social/chat/read')['messages'],[])

    def test_smtp_failure_is_durable_then_retry_delivers_once(self):
        report=self.report(self.send()['sentMessageId']);delivery=ChatDelivery(self.social)
        with patch.object(delivery,'send',side_effect=RuntimeError('offline')):
            self.assertEqual(delivery.pump()['retryPending'],1)
        self.assertEqual(len(self.call('admin','/social/chat/admin/reports')['reports']),1)
        with patch.object(delivery,'send') as send:
            delivery.pump();send.assert_not_called()
            self.clock+=300000;self.assertEqual(delivery.pump()['delivered'],1)
            delivery.pump();send.assert_called_once()
        self.assertEqual(send.call_args.args[0]['id'],report)

    def test_retention_cleanup_physically_deletes_expired_evidence_and_outbox(self):
        from chat_maintenance import cleanup
        report=self.report(self.send()['sentMessageId'])
        self.call('a','/social/chat/moderate',action='warn',playerId=self.ids['b'])
        self.clock+=181*86400000;cleanup(self.social)
        self.assertEqual(self.call('admin','/social/chat/admin/reports')['reports'],[])
        self.assertEqual(self.call('b','/social/chat/read')['messages'],[])
        self.assertIsNone(ChatDelivery(self.social).reserve())
        if hasattr(self.ledger,'connect'):
            with self.ledger.connect() as db:
                for table in ('social_message','chat_report','chat_audit','chat_outbox'):
                    self.assertEqual(db.execute('SELECT COUNT(*) FROM '+table).fetchone()[0],0)
        else:
            self.assertFalse(any(value.get('id')==report for value in self.client.data.values()))

    def test_outbox_lease_prevents_two_workers_and_stale_ack(self):
        self.report(self.send()['sentMessageId']);delivery=ChatDelivery(self.social)
        item=delivery.reserve();self.assertIsNotNone(item);self.assertIsNone(delivery.reserve())
        self.clock+=130000;new=delivery.reserve();self.assertIsNotNone(new)
        delivery.finish(item[0]['id'],item[1],True)
        delivery.finish(new[0]['id'],new[1],False)
        self.clock+=600000;self.assertIsNotNone(delivery.reserve())

    def test_smtp_uses_encryption_and_configured_recipient_without_evidence(self):
        delivery=ChatDelivery(self.social);smtp=Mock();smtp.__enter__=Mock(return_value=smtp);smtp.__exit__=Mock()
        report=dict(id='a'*20,clan=self.clan,message_id='b'*20,reason=REASONS[0],created_at=self.clock,evidence='PRIVATE')
        env=dict(DF_CHAT_SMTP_HOST='smtp.example.test',DF_CHAT_SMTP_FROM='game@example.test',DF_CHAT_SMTP_USER='server',DF_CHAT_SMTP_PASSWORD='test-secret')
        with patch.dict('os.environ',env),patch('chat_delivery.smtplib.SMTP',return_value=smtp):delivery.send(report)
        smtp.starttls.assert_called_once();smtp.login.assert_called_once_with('server','test-secret')
        message=smtp.send_message.call_args.args[0]
        self.assertEqual(message['To'],'admin@example.test');self.assertNotIn('PRIVATE',message.as_string())

    def test_http_cannot_spoof_admin_and_ban_blocks_wallet(self):
        app=CommerceApplication(self.ledger,lambda header:header[7:] if header.startswith('Bearer ') else None,None,'',chat_config=self.config)
        def request(who,path,data):
            raw=json.dumps(data).encode();reply={}
            def start(status,headers):reply.update(status=status,headers=dict(headers))
            body=b''.join(app(dict(REQUEST_METHOD='POST',PATH_INFO=path,CONTENT_LENGTH=str(len(raw)),**{'wsgi.input':io.BytesIO(raw),'HTTP_AUTHORIZATION':'Bearer '+who}),start))
            return reply,json.loads(body)
        reply,body=request('b','/social/chat/admin/reports',{'isAdmin':True})
        self.assertEqual(reply['status'],'403 Forbidden');self.assertEqual(reply['headers']['Cache-Control'],'no-store')
        report=self.report(self.send()['sentMessageId'])
        self.call('admin','/social/chat/admin/action',action='suspend',reportId=report)
        self.assertEqual(request('a','/wallet',{})[0]['status'],'403 Forbidden')


class FirestoreClanChatTests(ClanChatTests):
    def setUp(self):
        from test_firestore_social import Client, transactional
        self.client=Client();self.decorator=patch('google.cloud.firestore.transactional',transactional)
        self.decorator.start();self.addCleanup(self.decorator.stop);super().setUp()
    def make_ledger(self):
        from firestore_ledger import FirestoreLedger
        return FirestoreLedger(client=self.client)
    def make_social(self):
        from firestore_social import FirestoreSocial
        return FirestoreSocial(self.ledger,chat_config=self.config)


if __name__=='__main__':unittest.main()
