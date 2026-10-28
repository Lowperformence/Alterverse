// Controlled HTTP/Firestore fixture for the real Node server plus the catalog patch.
import {enableUfoTestHat,headwearItems} from '../../../Server/commerce/node/enable-ufo-test-hat.mjs';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import vm from 'node:vm';

if(!process.argv[2])throw new Error('Pass a copy of the current Node index.js; this test never deploys or accesses a live service.');
const sourcePath = process.argv[2];
const source = enableUfoTestHat(await readFile(sourcePath, 'utf8'));
const hash = value => createHash('sha256').update(value).digest('hex');
const account = hash('existing-player');
const playerPath = `players/${account}`;
const nameEntry = text => ({ key: 'DF_PlayerName', kind: 1, text });
const xpEntry = { key: 'DF_AccountXP', kind: 1, text: '98765' };
const aliasEntry = { key: 'DF_PlayGamesDisplayName', kind: 1, text: 'My Google Name' };
const copy = value => structuredClone(value);
const same = (a, b) => assert.deepEqual(copy(a), copy(b));

async function environment(seed = {}, options = {}) {
  const documents = new Map(Object.entries(copy(seed)));
  const writes = [];
  let handler, queue = Promise.resolve(), databaseReads = 0;
  const logs = [];
  const snapshot = path => ({ exists: documents.has(path), data: () => copy(documents.get(path)) });
  const ref = path => ({
    path,
    async get() {
      databaseReads++;
      if (options.failRead || options.failSubscriptions && path.startsWith('subscriptions/')) throw new Error('private database error');
      return snapshot(path);
    },
    async set(value, settings) {
      documents.set(path, settings?.merge ? { ...documents.get(path), ...copy(value) } : copy(value));
      writes.push(path);
    },
    collection: name => collection(path + '/' + name)
  });
  const collection = prefix => ({
    doc: id => ref(prefix + '/' + id),
    select() { return this; },
    limit() { return this; },
    async *stream() {
      for (const [path] of documents) if (path.startsWith(prefix + '/') && !path.slice(prefix.length + 1).includes('/')) {
        yield { id: path.slice(prefix.length + 1), ...snapshot(path) };
      }
    }
  });
  const db = {
    collection,
    runTransaction(work) {
      const result = queue.then(async () => {
        const staged = [];
        const tx = {
          async get(reference) {
            assert.equal(staged.length, 0, 'Firestore transactions must read before writing');
            databaseReads++;
            if (options.failRead) throw new Error('private database error');
            if (options.exhaustClaims && reference.path.startsWith('playerNameClaims/')) {
              return { exists: true, data: () => ({ accountHash: 'other-account' }) };
            }
            return snapshot(reference.path);
          },
          set(reference, value, settings) { staged.push({ reference, value: copy(value), settings }); },
          create(reference,value) { assert.ok(!documents.has(reference.path),'Create must be new');staged.push({reference,value:copy(value)}); }
        };
        const answer = await work(tx);
        if (options.failCommit) throw new Error('private commit error');
        for (const { reference, value, settings } of staged) await reference.set(value, settings);
        return answer;
      });
      queue = result.catch(() => {});
      return result;
    }
  };
  const context = vm.createContext({
    URL, Buffer, Date, Intl, setTimeout, clearTimeout,
    process: { env: options.subscription ? {
      DF_ENABLE_SHARD_SUBSCRIPTIONS: '1',
      DF_RTDN_AUDIENCE: 'https://example.test/google-play/rtdn',
      DF_RTDN_SERVICE_ACCOUNT: 'fixture@example.iam.gserviceaccount.com'
    } : {} },
    console: { error: (...args) => logs.push(args), warn: (...args) => logs.push(args), log() {} }
  });
  const dependencies = {
    '@google-cloud/functions-framework': { http: (name, fn) => { assert.equal(name, 'helloHttp'); handler = fn; } },
    '@google-cloud/firestore': { Firestore: class { constructor() { return db; } }, FieldValue: { serverTimestamp: () => 'SERVER_TIMESTAMP' } },
    'google-auth-library': { GoogleAuth: class {} },
    'node:crypto': { createHash },
    jose: {
      createRemoteJWKSet: () => ({}),
      jwtVerify: async () => ({ payload: options.payload || {
        sub: 'existing-player', project_id: '57bb61ab-1371-4b7a-a996-5a932e351c03',
        aud: ['upid:57bb61ab-1371-4b7a-a996-5a932e351c03', 'envName:production']
      } })
    }
  };
  const module = new vm.SourceTextModule(source, { context });
  await module.link(async name => {
    const values = dependencies[name];
    assert.ok(values, `Unexpected dependency ${name}`);
    return new vm.SyntheticModule(Object.keys(values), function () {
      for (const [key, value] of Object.entries(values)) this.setExport(key, value);
    }, { context });
  });
  await module.evaluate();
  return {
    documents, writes, logs,
    get reads() { return databaseReads; },
    async request(path = '/wallet', body = {}, authorized = true, method = 'POST') {
      let status = 200, value;
      const headers = {};
      const response = {
        status(code) { status = code; return this; },
        json(payload) { value = copy(payload); return this; },
        set(key, val) { headers[key] = val; return this; },
        send(payload) { value = payload; return this; }
      };
      await handler({ method, path, body, get: () => authorized ? 'Bearer test-fixture-token' : '' }, response);
      return { status, body: value, headers };
    }
  };
}

const tests = [];
const test = (name, run) => tests.push({ name, run });
const saved = profile => ({ balance: 5500, revision: 17, profileRevision: 4, profile, owned: ['voidfall', 'avatar_12'], supportFlag: 'keep-existing-metadata' });

const ufoKey='DF_HeadwearOwned_UfoTestHat';
const ufoValue=w=>w.profile.find(x=>x.key===ufoKey)?.number;
const earned=w=>w.profile.find(x=>x.key==='DF_Shards')?.number||0;
const ufoSeed=(shards,paid=0)=>({...saved([nameEntry('SolarFox1234567'),xpEntry,{key:'DF_Shards',kind:0,number:shards}]),balance:paid,owned:[]});
const buy=(e,revision,id='a'.repeat(32))=>e.request('/wallet/spend',{cosmeticId:'ufo_test_hat',operationId:id,expectedProfileRevision:revision});
test('UFO exact 100 earned shards grants ownership and leaves XP/loadout unchanged',async()=>{
 const e=await environment({[playerPath]:ufoSeed(100)});const r=await buy(e,4);
 assert.equal(r.status,200,JSON.stringify(e.logs));assert.equal(earned(r.body.wallet),0);assert.equal(ufoValue(r.body.wallet),1);
 assert.equal(r.body.wallet.profile.find(x=>x.key===xpEntry.key).text,'98765');
 assert.ok(!r.body.wallet.profile.some(x=>x.key.startsWith('DF_HeadwearEquipped_')));
});
test('UFO zero funds fails without changing the wallet',async()=>{
 const seed=ufoSeed(0);const e=await environment({[playerPath]:seed});const r=await buy(e,4);
 assert.equal(r.status,400);same(e.documents.get(playerPath),seed);
});
test('UFO 100 paid shards is accepted through the existing ledger',async()=>{
 const e=await environment({[playerPath]:ufoSeed(0,100)});const r=await buy(e,4);
 assert.equal(r.status,200);assert.equal(r.body.wallet.balance,0);assert.equal(ufoValue(r.body.wallet),1);
});
test('UFO simultaneous retries and later re-purchase charge exactly once',async()=>{
 const e=await environment({[playerPath]:ufoSeed(200)});const result=await Promise.all([buy(e,4),buy(e,4),buy(e,4)]);
 assert.ok(result.every(x=>x.status===200));const current=result.at(-1).body.wallet;
 const again=await buy(e,current.profileRevision,'b'.repeat(32));
 assert.equal(again.status,200);assert.equal(earned(again.body.wallet),100);assert.equal(ufoValue(again.body.wallet),1);
});
test('UFO ownership/equipment survives Node restart and is isolated from another account',async()=>{
 const e=await environment({[playerPath]:ufoSeed(100)});const r=await buy(e,4);assert.equal(r.status,200);
 const w=r.body.wallet;const saved=await e.request('/profile/save',{expectedProfileRevision:w.profileRevision,profile:[...w.profile,{key:'DF_HeadwearEquipped_5',kind:0,number:8}]});
 assert.equal(saved.status,200);const restarted=await environment(Object.fromEntries(e.documents));const loaded=await restarted.request();
 assert.equal(ufoValue(loaded.body.wallet),1);assert.equal(loaded.body.wallet.profile.find(x=>x.key==='DF_HeadwearEquipped_5').number,8);
 const other=await environment(Object.fromEntries(e.documents),{payload:{sub:'other-player',project_id:'57bb61ab-1371-4b7a-a996-5a932e351c03',aud:['upid:57bb61ab-1371-4b7a-a996-5a932e351c03','envName:production']}});
 assert.equal(ufoValue((await other.request()).body.wallet),undefined);
});

for(const item of headwearItems) test(item.id+' unlocks for 100 shards and retains ownership on retry',async()=>{
 const e=await environment({[playerPath]:ufoSeed(200)});
 const buyHat=(revision,operationId)=>e.request('/wallet/spend',{cosmeticId:item.id,operationId,expectedProfileRevision:revision});
 const r=await buyHat(4,'c'.repeat(32));
 assert.equal(r.status,200,JSON.stringify(e.logs));assert.equal(earned(r.body.wallet),100);
 assert.equal(r.body.wallet.profile.find(x=>x.key===item.writes[0].key)?.number,1);
 assert.ok(!r.body.wallet.profile.some(x=>x.key.startsWith('DF_HeadwearEquipped_')));
 const again=await buyHat(r.body.wallet.profileRevision,'d'.repeat(32));
 assert.equal(again.status,200);assert.equal(earned(again.body.wallet),100);
});

let passed = 0;
for (const { name, run } of tests) {
  try { await run(); passed++; console.log(`PASS ${name}`); }
  catch (error) { console.error(`FAIL ${name}`); throw error; }
}
console.log(`${passed}/${tests.length} regression scenarios passed`);
