import assert from 'node:assert/strict';
import {EventEmitter} from 'node:events';
import {readFile} from 'node:fs/promises';
const source=await readFile(new URL('../../Server/commerce/node/standalone-delivery.inc.js',import.meta.url),'utf8');
let checks=0;
const check=(value,message)=>{assert.ok(value,message);checks++;};
function fixture(options={}){
  const packets=[],connections=[];let inData=false;
  class Socket extends EventEmitter{
    constructor(secure,greeting,old=null){super();this.secure=secure;this.old=old;queueMicrotask(()=>{this.emit(secure?'secureConnect':'connect');if(greeting)this.emit('data',Buffer.from('220 test SMTP\r\n'));});}
    setTimeout(){return this;}
    write(packet){
      packets.push({secure:this.secure,packet});let reply;
      if(packet.startsWith('EHLO'))reply='250-test\r\n250-AUTH PLAIN LOGIN\r\n250 STARTTLS\r\n';
      else if(packet==='STARTTLS\r\n')reply='220 Upgrade\r\n';
      else if(packet.startsWith('AUTH PLAIN')){check(this.secure,'credentials require TLS');reply='235 Authenticated\r\n';}
      else if(packet.startsWith('MAIL FROM')||packet.startsWith('RCPT TO'))reply='250 OK\r\n';
      else if(packet==='DATA\r\n'){inData=true;reply='354 Send\r\n';}
      else if(packet==='QUIT\r\n')reply='221 Bye\r\n';
      else if(inData){inData=false;reply='250 Queued\r\n';}
      else throw new Error('Unexpected fixture command');
      queueMicrotask(()=>options.closeEarly&&packet.startsWith('EHLO')?this.emit('close'):this.emit('data',Buffer.from(reply)));
    }
    destroy(){this.emit('close');if(this.old)this.old.destroy();}
  }
  const providers={net:{connect(opts){connections.push({kind:'plain',opts});return new Socket(false,true);}},tls:{connect(opts){connections.push({kind:'tls',opts});check(opts.rejectUnauthorized===true&&opts.servername==='smtp.example.test','certificate and hostname verification retained');return new Socket(true,!opts.socket,opts.socket);}}};
  const env={DF_CHAT_SMTP_HOST:'smtp.example.test',DF_CHAT_SMTP_FROM:'Game <sender@example.test>',DF_CHAT_SMTP_USER:'fixture-user',DF_CHAT_SMTP_PASSWORD:'fixture-secret',...options.env};
  const api=new Function('process','console',source+'\nreturn {send:dfSendReportEmail};')({env},{warn(){}});
  return {send:api.send,providers,packets,connections};
}
const report={id:'a'.repeat(20),clan:'b'.repeat(20),message_id:'c'.repeat(20),reason:'Threats',created_at:1800000000000,evidence:'PRIVATE CHAT EVIDENCE'};
for(const mode of ['starttls','tls']){
  const f=fixture({env:{DF_CHAT_SMTP_MODE:mode}});
  await f.send(report,{adminEmail:'admin@example.test'},f.providers);
  check(f.connections.filter(c=>c.kind==='tls').length===1,'one encrypted SMTP session');
  check(f.packets.some(p=>p.packet==='RCPT TO:<admin@example.test>\r\n'),'configured administrator recipient');
  const mail=f.packets.find(p=>p.packet.startsWith('From:')).packet;
  check(mail.includes('Message-ID: <clan-report-'+report.id+'@dimensions-front>'),'stable report message ID');
  const body=Buffer.from(mail.split('\r\n\r\n')[1].replace(/\s|\./g,''),'base64').toString('utf8');
  check(body.includes(report.id)&&body.includes('Threats'),'report metadata delivered');
  check(!body.includes('PRIVATE')&&!body.includes('fixture-secret'),'private evidence and credentials excluded from email');
  check(!f.packets.filter(p=>!p.secure).some(p=>p.packet.startsWith('AUTH')),'no plaintext authentication');
  check(mode==='starttls'?f.packets.some(p=>p.packet==='STARTTLS\r\n'):!f.packets.some(p=>p.packet==='STARTTLS\r\n'),'configured TLS mode retained');
}
for(const env of [{DF_CHAT_SMTP_MODE:'plain'},{DF_CHAT_SMTP_FROM:'sender@example.test\r\nBcc: other@example.test'}]){
  const f=fixture({env});await assert.rejects(f.send(report,{adminEmail:'admin@example.test'},f.providers));checks++;check(f.connections.length===0,'invalid mail configuration opens no connection');
}
const closing=fixture({closeEarly:true});await assert.rejects(closing.send(report,{adminEmail:'admin@example.test'},closing.providers),/closed/);checks++;
console.log(`Standalone encrypted SMTP: ${checks} protocol, recipient, privacy and failure checks passed.`);
