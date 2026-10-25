import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import vm from 'node:vm';
import {DatabaseSync} from 'node:sqlite';
import {createHash,randomBytes,randomInt} from 'node:crypto';
import {patchOwnedClanAvatars,previousOwnership,ownedAvatars} from './enable-owned-clan-avatars.mjs';

const input=process.argv[2];if(!input)throw new Error('Supply the current standalone index.js');
const original=readFileSync(input,'utf8'),updated=patchOwnedClanAvatars(original);
assert.equal(updated.replace(ownedAvatars,previousOwnership).replace('2026-10-25.clans.node.2','2026-10-24.clans.node.1'),original);
assert.throws(()=>patchOwnedClanAvatars(updated));
assert.throws(()=>patchOwnedClanAvatars(original.replace('// DF_STANDALONE_CLANS_BEGIN v1','')));
const start=updated.indexOf('// DF_STANDALONE_CLANS_BEGIN v1');
const end=updated.indexOf('// Existing Firestore collection layout;',start);
assert.ok(start>=0&&end>start);
const context={Buffer,process,dfRandomBytes:randomBytes,dfRandomInt:randomInt,
  sha256:value=>createHash('sha256').update(value).digest('hex'),ITEMS:{avatar_10:{}}};
vm.runInNewContext(updated.slice(start,end)+'\nglobalThis.Rules=DfClanRules;globalThis.Schema=DF_SOCIAL_SQL;',context);
const sql=new DatabaseSync(':memory:');
try{
  sql.exec(context.Schema);
  sql.prepare('INSERT INTO wallet VALUES(?,?,?)').run('leader',777,4);
  sql.prepare('INSERT INTO profile VALUES(?,?,?)').run('leader',9,'[]');
  sql.prepare('INSERT INTO social_clan(id,name,name_search,tag,description,open) VALUES(?,?,?,?,?,?)').run('1'.repeat(20),'Solar Crew 1234567','solar crew 1234567','C1234567','Crew',1);
  sql.prepare('INSERT INTO social_member VALUES(?,?,?)').run('leader','1'.repeat(20),'leader');
  const rules=new context.Rules(sql,'leader',{enabled:true});
  const choose=avatar=>rules.clanAction('/social/clan/icon',{avatar});
  const selected=()=>sql.prepare('SELECT avatar FROM social_clan').get().avatar;
  for(let id=0;id<6;id++){choose(id);assert.equal(selected(),id);}
  for(const id of [6,16,10,7,-1,1001,'0',true,1.5])assert.throws(()=>choose(id));
  const profile=[{key:'DF_FoundersPack_Owned',kind:0,number:1},{key:'DF_AvatarOwned_16',kind:0,number:1}];
  sql.prepare('UPDATE profile SET data=? WHERE account=?').run(JSON.stringify(profile),'leader');
  for(const id of [6,16]){choose(id);assert.equal(selected(),id);}
  sql.prepare('INSERT INTO cosmetic VALUES(?,?)').run('leader','avatar_10');choose(10);assert.equal(selected(),10);
  sql.prepare("UPDATE social_member SET role='member' WHERE account='leader'").run();assert.throws(()=>choose(0));
  assert.deepEqual({...sql.prepare('SELECT balance,revision FROM wallet').get()},{balance:777,revision:4});
  assert.equal(sql.prepare('SELECT revision FROM profile').get().revision,9);
  assert.equal(sql.prepare('SELECT COUNT(*) AS n FROM purchase').get().n,0);
  console.log('Standalone source preservation, starter/reward/shop ownership, leader-only and unchanged-wallet checks passed.');
}finally{sql.close();}
