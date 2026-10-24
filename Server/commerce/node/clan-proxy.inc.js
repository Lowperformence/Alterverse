// DF_CLAN_BRIDGE_BEGIN v1: loopback transport for the existing Python clan rules.
async function dfClanCall(req, path, body = req.body, timeout = 10000) {
  if (!/^\/social\/[a-z_/-]{1,80}$/.test(path)) throw new HttpError(400, 'invalid_social_route');
  const encoded = JSON.stringify(body && typeof body === 'object' && !Array.isArray(body) ? body : {});
  if (Buffer.byteLength(encoded, 'utf8') > 262144) throw new HttpError(400, 'social_request_too_large');
  let response, payload;
  try {
    response = await fetch('http://127.0.0.1:8081' + path, {
      method: 'POST', redirect: 'error', signal: AbortSignal.timeout(timeout),
      headers: { 'Content-Type': 'application/json', Authorization: String(req.get('authorization') || '') },
      body: encoded
    });
    const text = await response.text();
    if (Buffer.byteLength(text, 'utf8') > 1048576) throw new Error('response too large');
    payload = JSON.parse(text);
    if (!payload || typeof payload !== 'object' || Array.isArray(payload) || typeof payload.fulfilled !== 'boolean') {
      throw new Error('invalid response');
    }
  } catch {
    throw new HttpError(503, 'clan_service_unavailable');
  }
  return { status: response.status, payload };
}

async function dfClanAccess(req) {
  const { status, payload } = await dfClanCall(req, '/social/_access', {}, 3000);
  if (status !== 200 || !payload.fulfilled) {
    throw new HttpError(status === 401 || status === 403 ? status : 503, 'clan_account_access_unavailable');
  }
}

async function dfClanProfileSaved(req, saved) {
  const wallet = await saved;
  try {
    const {status,payload}=await dfClanCall(req, '/social/_sync', {}, 3000);
    if(status!==200||!payload.fulfilled)throw new Error('directory sync deferred');
  }
  catch { console.warn('Clan directory synchronization pending; committed profile preserved.'); }
  return wallet;
}

async function dfClanReply(req, res, accountHash) {
  const { status, payload } = await dfClanCall(req, req.path);
  if (status === 200 && (!payload.fulfilled || !payload.social || typeof payload.social !== 'object')) {
    throw new HttpError(503, 'clan_service_unavailable');
  }
  if (status === 200 && payload.social.wallet) {
    if(payload.social.wallet.accountHash!==accountHash)throw new HttpError(503,'clan_wallet_account_mismatch');
    // Reuse this exact Node version's billing/subscription decoration. It mutates
    // the snapshot; this response sink sends nothing to the actual HTTP client.
    const sink = { set() {}, status() { return this; }, json() {} };
    await success(sink, payload.social.wallet);
  }
  res.set('Cache-Control', 'no-store');
  return res.status(status).json(payload);
}
// DF_CLAN_BRIDGE_END v1
