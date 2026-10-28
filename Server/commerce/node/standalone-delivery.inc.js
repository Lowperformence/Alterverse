// Same durable report outbox and encrypted SMTP policy as the existing backend.
// SMTP runs only from the authenticated administrator delivery endpoint.
async function dfSendReportEmail(report,config,providers=null){
  const env=process.env;
  const host=(env.DF_CHAT_SMTP_HOST??'').trim(),mode=env.DF_CHAT_SMTP_MODE??'starttls';
  const address=value=>{
    if(typeof value!=='string'||/[\r\n]/.test(value))throw new Error('Invalid configured report address');
    const match=/^(?:[^<>]*<)?([^\s<>@,]+@[^\s<>@,]+)>?$/.exec(value.trim());
    if(!match||/[^\x21-\x7e]/.test(match[1]))throw new Error('Invalid configured report address');return match[1];
  };
  const sender=address(env.DF_CHAT_SMTP_FROM??''),recipient=address(config.adminEmail);
  if(!host||/[\r\n]/.test(host)||!['starttls','tls'].includes(mode))throw new Error('Encrypted SMTP configuration required');
  const port=Number(env.DF_CHAT_SMTP_PORT??(mode==='tls'?'465':'587'));
  if(!Number.isInteger(port)||port<1||port>65535)throw new Error('Invalid configured SMTP port');
  if(typeof report.id!=='string'||!/^[a-f0-9]{20}$/.test(report.id))throw new Error('Invalid stored report');
  const net=providers?.net??await import('node:net'),tls=providers?.tls??await import('node:tls');
  const connected=(socket,event)=>new Promise((resolve,reject)=>{socket.once(event,()=>{socket.off('error',reject);resolve();});socket.once('error',reject);});
  function session(socket){
    let buffer='',lines=[],failure=null;const ready=[],waiting=[];
    function deliver(value,error){const waiter=waiting.shift();if(waiter)error?waiter.reject(error):waiter.resolve(value);else if(error)failure=error;else ready.push(value);}
    const onError=error=>{failure=error;while(waiting.length)waiting.shift().reject(error);};
    const onClose=()=>onError(new Error('SMTP connection closed'));
    const onData=chunk=>{
      buffer+=chunk.toString('utf8');if(buffer.length>32768){socket.destroy(new Error('SMTP reply too large'));return;}
      let end;while((end=buffer.indexOf('\n'))>=0){const line=buffer.slice(0,end).replace(/\r$/,'');buffer=buffer.slice(end+1);const m=/^(\d{3})([- ])(.*)$/.exec(line);if(!m){socket.destroy(new Error('Invalid SMTP reply'));return;}lines.push(line);if(m[2]===' '){deliver({code:Number(m[1]),text:lines.join('\n')});lines=[];}}
    };
    socket.on('data',onData);socket.on('error',onError);socket.on('close',onClose);socket.setTimeout(5000,()=>socket.destroy(new Error('SMTP timeout')));
    const read=()=>failure?Promise.reject(failure):ready.length?Promise.resolve(ready.shift()):new Promise((resolve,reject)=>waiting.push({resolve,reject}));
    const expect=async(code)=>{const reply=await read();if(reply.code!==code)throw new Error('SMTP command rejected');return reply;};
    return {expect,command:async(text,code)=>{socket.write(text+'\r\n');return expect(code);},detach(){socket.off('data',onData);socket.off('error',onError);socket.off('close',onClose);socket.setTimeout(0);}};
  }
  let socket=mode==='tls'?tls.connect({host,port,servername:host,rejectUnauthorized:true}):net.connect({host,port});
  let channel=session(socket);
  try{
    await connected(socket,mode==='tls'?'secureConnect':'connect');await channel.expect(220);
    let ehlo=await channel.command('EHLO dimensions-front',250);
    if(mode==='starttls'){
      await channel.command('STARTTLS',220);channel.detach();
      socket=tls.connect({socket,servername:host,rejectUnauthorized:true});channel=session(socket);
      await connected(socket,'secureConnect');ehlo=await channel.command('EHLO dimensions-front',250);
    }
    const username=env.DF_CHAT_SMTP_USER??'';
    if(username){
      const password=env.DF_CHAT_SMTP_PASSWORD;
      if(typeof password!=='string')throw new Error('SMTP authentication configuration required');
      if(/\bAUTH\b[^\n]*\bPLAIN\b/i.test(ehlo.text))await channel.command('AUTH PLAIN '+Buffer.from('\0'+username+'\0'+password,'utf8').toString('base64'),235);
      else{await channel.command('AUTH LOGIN',334);await channel.command(Buffer.from(username,'utf8').toString('base64'),334);await channel.command(Buffer.from(password,'utf8').toString('base64'),235);}
    }
    await channel.command('MAIL FROM:<'+sender+'>',250);await channel.command('RCPT TO:<'+recipient+'>',250);await channel.command('DATA',354);
    const body='A clan report is available in the in-game administration view.\n\nReport ID: '+report.id+'\nClan ID: '+report.clan+'\nMessage ID: '+(report.message_id||'(player report)')+'\nReason: '+report.reason+'\nReported at (UTC): '+new Date(report.created_at).toISOString()+'\n';
    const encoded=Buffer.from(body,'utf8').toString('base64').match(/.{1,76}/g).join('\r\n');
    const message=['From: '+sender,'To: '+recipient,'Subject: Dimensions Front: clan report '+report.id,'Message-ID: <clan-report-'+report.id+'@dimensions-front>','MIME-Version: 1.0','Content-Type: text/plain; charset=utf-8','Content-Transfer-Encoding: base64','',encoded].join('\r\n');
    await channel.command(message+'\r\n.',250);await channel.command('QUIT',221);
  }finally{channel.detach();socket.destroy();}
}
async function dfReserveReport(config){
  const now=Date.now();
  return db.runTransaction(async tx=>{
    const pending=await tx.get(dfSocialCollection('chat_outbox').where('sent_at','==',0).orderBy('next_at').limit(20));
    for(const snap of pending.docs){
      const job=snap.data();if(job.next_at>now||job.lease_until>now)continue;
      const report=await tx.get(dfSocialCollection('chat_report').doc(job.id));
      if(!report.exists||report.data().created_at<now-config.reportDays*86400000){tx.delete(snap.ref);return null;}
      const lease=now+120000;tx.set(snap.ref,{...job,attempts:job.attempts+1,lease_until:lease});return {report:report.data(),lease};
    }return null;
  });
}
async function dfFinishReport(id,lease,delivered){
  const now=Date.now();return db.runTransaction(async tx=>{
    const ref=dfSocialCollection('chat_outbox').doc(id),snap=await tx.get(ref);
    if(snap.exists&&snap.data().lease_until===lease){const job=snap.data();tx.set(ref,{...job,lease_until:0,sent_at:delivered?now:0,next_at:now+Math.min(3600000,60000*2**Math.min(job.attempts,6))});}
  });
}
async function dfPumpReports(config,send=dfSendReportEmail){
  const result={delivered:0,retryPending:0};if(!config.adminEmail)return result;
  for(let i=0;i<3;i++){
    const item=await dfReserveReport(config);if(!item)break;let delivered=false;
    try{await send(item.report,config);delivered=true;result.delivered++;}catch{console.warn('Clan report email pending retry');result.retryPending++;}
    finally{await dfFinishReport(item.report.id,item.lease,delivered);}
  }return result;
}
