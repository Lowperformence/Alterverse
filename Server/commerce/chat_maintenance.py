"""Run with the existing account server configuration and server credentials.

Schedule every minute for SMTP retries, daily for deletion (or use Firestore TTL).
No public HTTP endpoint or additional service is required.
"""
import argparse
from clan_chat import ClanChat
from chat_delivery import ChatDelivery


def cleanup(social, limit=100):
    if hasattr(social.ledger, 'connect'):
        with social.ledger.connect() as db:
            db.execute('BEGIN IMMEDIATE');social.chat.cleanup(db)
        return
    # Bounded batches, with immutable creation times; unrelated rows are untouched.
    config=social.chat_config;now=ClanChat.now()
    for table,days in (('social_message',config.message_days),('chat_report',config.report_days),('chat_audit',config.report_days)):
        snapshots=list(social.collections[table].where('created_at','<',now-days*86400000).limit(limit).stream())
        if not snapshots:continue
        batch=social.client.batch()
        for snap in snapshots:
            batch.delete(snap.reference)
            if table=='chat_report':batch.delete(social.collections['chat_outbox'].document(snap.id))
        batch.commit()


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--cleanup',action='store_true');args=parser.parse_args()
    from wsgi import application
    if args.cleanup:cleanup(application.social)
    print(ChatDelivery(application.social).pump(20))
