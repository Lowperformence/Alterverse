"""Durable SMTP outbox. Invoke from the authenticated admin UI or scheduled worker.

Only server configuration selects the recipient; messages contain report metadata,
not chat evidence. A failed SMTP delivery never loses or rolls back a stored report.
"""
from datetime import datetime, timezone
from email.message import EmailMessage
from email.utils import parseaddr
import logging
import os
import smtplib
import ssl
from clan_chat import ClanChat


class ChatDelivery:
    def __init__(self, social):
        self.social = social
        self.config = social.chat.config if hasattr(social, 'chat') else social.chat_config

    def reserve(self):
        now = ClanChat.now()
        if hasattr(self.social.ledger, 'connect'):
            with self.social.ledger.connect() as db:
                db.execute('BEGIN IMMEDIATE')
                row = db.execute('SELECT id FROM chat_outbox WHERE sent_at=0 AND next_at<=? AND lease_until<=? ORDER BY next_at,id LIMIT 1', (now, now)).fetchone()
                if not row: return None
                report = db.execute('SELECT id,clan,message_id,reason,created_at FROM chat_report WHERE id=?', row).fetchone()
                if not report or report[4] < now - self.config.report_days * 86400000:
                    db.execute('DELETE FROM chat_outbox WHERE id=?', row);return None
                lease = now + 120000
                db.execute('UPDATE chat_outbox SET attempts=attempts+1,lease_until=? WHERE id=?', (lease, row[0]))
                return dict(zip(('id', 'clan', 'message_id', 'reason', 'created_at'), report)), lease
        from google.cloud import firestore
        @firestore.transactional
        def take(tx):
            source = self.social.collections['chat_outbox'].where('sent_at', '==', 0).order_by('next_at').limit(20)
            for snapshot in source.stream(transaction=tx):
                job = snapshot.to_dict()
                if job['next_at'] > now or job['lease_until'] > now: continue
                report = self.social.collections['chat_report'].document(job['id']).get(transaction=tx)
                if not report.exists or report.to_dict()['created_at'] < now - self.config.report_days * 86400000:
                    tx.delete(snapshot.reference);return None
                lease = now + 120000
                tx.set(snapshot.reference, dict(job, attempts=job['attempts'] + 1, lease_until=lease))
                return report.to_dict(), lease
            return None
        return take(self.social.client.transaction())

    def finish(self, identifier, lease, delivered):
        now = ClanChat.now()
        def updated(job):
            delay = min(3600000, 60000 * (2 ** min(job['attempts'], 6)))
            return dict(job, lease_until=0, sent_at=now if delivered else 0,
                        next_at=now + delay)
        if hasattr(self.social.ledger, 'connect'):
            with self.social.ledger.connect() as db:
                db.execute('BEGIN IMMEDIATE')
                row = db.execute('SELECT id,attempts,next_at,lease_until,sent_at FROM chat_outbox WHERE id=?', (identifier,)).fetchone()
                if row and row[3] == lease:
                    job = updated(dict(zip(('id', 'attempts', 'next_at', 'lease_until', 'sent_at'), row)))
                    db.execute('UPDATE chat_outbox SET next_at=?,lease_until=0,sent_at=? WHERE id=? AND lease_until=?',
                               (job['next_at'], job['sent_at'], identifier, lease))
            return
        from google.cloud import firestore
        @firestore.transactional
        def finish(tx):
            ref = self.social.collections['chat_outbox'].document(identifier)
            snap = ref.get(transaction=tx)
            if snap.exists and snap.to_dict()['lease_until'] == lease:
                tx.set(ref, updated(snap.to_dict()))
        finish(self.social.client.transaction())

    def send(self, report):
        host = os.environ.get('DF_CHAT_SMTP_HOST', '').strip()
        sender = os.environ.get('DF_CHAT_SMTP_FROM', '').strip()
        recipient = self.config.admin_email
        for address in (sender, recipient):
            if '\n' in address or '\r' in address or '@' not in parseaddr(address)[1]:
                raise ValueError('A valid configured sender and administrator email are required')
        if not host: raise ValueError('SMTP host required')
        mode = os.environ.get('DF_CHAT_SMTP_MODE', 'starttls')
        if mode not in ('starttls', 'tls'): raise ValueError('Encrypted SMTP is required')
        port = int(os.environ.get('DF_CHAT_SMTP_PORT', '465' if mode == 'tls' else '587'))
        message = EmailMessage()
        message['From'] = sender;message['To'] = recipient
        message['Subject'] = 'Dimensions Front: clan report ' + report['id']
        message['Message-ID'] = '<clan-report-' + report['id'] + '@dimensions-front>'
        timestamp = datetime.fromtimestamp(report['created_at'] / 1000, timezone.utc).isoformat()
        message.set_content('A clan report is available in the in-game administration view.\n\n'
                            f"Report ID: {report['id']}\nClan ID: {report['clan']}\n"
                            f"Message ID: {report['message_id'] or '(player report)'}\n"
                            f"Reason: {report['reason']}\nReported at (UTC): {timestamp}\n")
        context = ssl.create_default_context()
        client = smtplib.SMTP_SSL(host, port, timeout=5, context=context) if mode == 'tls' else smtplib.SMTP(host, port, timeout=5)
        with client as smtp:
            if mode == 'starttls': smtp.starttls(context=context)
            username = os.environ.get('DF_CHAT_SMTP_USER', '')
            if username: smtp.login(username, os.environ['DF_CHAT_SMTP_PASSWORD'])
            smtp.send_message(message)

    def pump(self, limit=3):
        result = dict(delivered=0, retryPending=0)
        if not self.config.admin_email: return result
        for _ in range(limit):
            item = self.reserve()
            if not item: break
            report, lease = item
            delivered = False
            try:
                self.send(report);delivered = True;result['delivered'] += 1
            except Exception:
                # Never log SMTP credentials, addresses, evidence or exception strings.
                logging.warning('Clan report email pending retry');result['retryPending'] += 1
            finally: self.finish(report['id'], lease, delivered)
        return result
