const {test}=require('node:test'),assert=require('node:assert/strict'),crypto=require('node:crypto');
const {fixture}=require('./server.fixture.cjs');
const owner='ourhosu@gmail.com';
function admin(){const f=fixture();f.setEmail(owner);f.context.initializeDashboardManagement();return f;}
function code(f){return f.mail.at(-1).body.match(/\d{8}/)[0];}
function add(f,version='0.1.0',environment='TEST',runId=crypto.randomUUID(),time='2026-10-21T00:00:00.000Z'){
 const b=JSON.parse(JSON.stringify(f.batch));b.batchId=crypto.randomUUID();b.appSessionId=crypto.randomUUID();b.events[0].eventId=crypto.randomUUID();Object.assign(b.events[0],{gameVersion:version,environment,runId,occurredAt:time});assert.equal(f.send(b).ok,true);f.context.ccImport_(f.book);return b;
}
function challenge(f,scope){const p=f.context.previewDashboardDeletion({environment:'TEST',...scope});f.context.requestDashboardDeletionCode(p.previewId);const id=JSON.parse([...f.cache.entries()].find(([k,v])=>k.startsWith('code:')&&JSON.parse(v).purpose==='DELETE')[1]);return {p,verification:code(f),id:[...f.cache.keys()].find(k=>k.startsWith('code:')&&JSON.parse(f.cache.get(k)).purpose==='DELETE').slice(5)};}
test('registered mailbox login does not expose data before verification and revocation takes effect',()=>{
 const f=admin();f.setEmail('');assert.throws(()=>f.context.getDashboard({}),/ACCESS_DENIED/);
 f.context.requestDashboardLogin('unlisted@gmail.com');assert.equal(f.mail.length,0);
 const requested=f.context.requestDashboardLogin('imvagueness@gmail.com');assert.equal(f.mail.length,1);
 assert.throws(()=>f.context.completeDashboardLogin(requested.challengeId,'00000000'),/인증번호/);
 const session=f.context.completeDashboardLogin(requested.challengeId,code(f));assert.equal(session.viewer.name,'모호성');assert.equal(session.viewer.role,'VIEWER');
 assert.equal(f.context.getDashboardSession(session.auth).viewer.email,'imvagueness@gmail.com');
 assert.throws(()=>f.context.getDashboardManagement(session.auth),/ADMIN_ONLY/);
 assert.throws(()=>f.context.previewDashboardDeletion({kind:'VERSION',gameVersion:'0.1.0'},session.auth),/ADMIN_ONLY/);
 assert.throws(()=>f.context.completeDashboardLogin(requested.challengeId,code(f)),/만료/);
 f.setEmail(owner);f.context.saveDashboardMember('imvagueness@gmail.com','모호성',false);f.setEmail('');assert.throws(()=>f.context.getDashboard({},session.auth),/ACCESS_DENIED/);
});
test('codes have an expiry, attempt limit and are never returned from RPC',()=>{
 const f=admin();f.setEmail('');let r=f.context.requestDashboardLogin('gyumin9048@gmail.com');assert.equal(Object.hasOwn(r,'code'),false);
 const entry=JSON.parse(f.cache.get('code:'+r.challengeId));entry.expires=Date.now()-1;f.cache.set('code:'+r.challengeId,JSON.stringify(entry));assert.throws(()=>f.context.completeDashboardLogin(r.challengeId,code(f)),/만료/);
 r=f.context.requestDashboardLogin('lakuase2@gmail.com');const correct=code(f),wrong=correct==='00000000'?'00000001':'00000000';for(let i=0;i<5;i++)assert.throws(()=>f.context.completeDashboardLogin(r.challengeId,wrong));assert.throws(()=>f.context.completeDashboardLogin(r.challengeId,correct),/만료/);
});
test('retention prunes only imported hash-confirmed originals, preserves events and accepts replay without recreation',()=>{
 const f=admin(),a=add(f),b=add(f);const pending=JSON.parse(JSON.stringify(f.batch));pending.batchId=crypto.randomUUID();pending.events[0].eventId=crypto.randomUUID();f.send(pending);
 f.context.saveRawRetention(true,1);const rows=JSON.stringify(f.tables.Events),r=f.context.ccPruneRaw_(f.book,[]);assert.equal(r.trashed,2);assert.equal(f.files.size,1);assert.ok(f.files.has(pending.batchId+'.json'));assert.equal(JSON.stringify(f.tables.Events),rows);
 assert.equal(f.send(a).stored,'SHEETS');assert.equal(f.send(b).stored,'SHEETS');assert.equal(f.files.size,1);
});
test('version clear isolates the selected environment and protects other versions and blocks delayed/replayed old data',()=>{
 const f=admin(),a=add(f),b=add(f,'0.1.0','LIVE'),keep=add(f,'0.2.0');
 const c=challenge(f,{kind:'VERSION',gameVersion:'0.1.0'});assert.equal(c.p.events,1);assert.equal(c.p.groups.TEST,1);assert.equal(c.p.groups.LIVE,0);
 assert.throws(()=>f.context.executeDashboardDeletion(c.id,c.verification,'wrong'),/確認|삭제/);
 const r=f.context.executeDashboardDeletion(c.id,c.verification,'삭제');assert.equal(r.deletedEvents,1);assert.equal(f.context.ccReadEvents_(f.book).length,2);assert.equal(f.tables.Events.length,3);
 assert.equal(f.send(a).ok,true);assert.equal(f.files.has(a.batchId+'.json'),false);assert.ok(f.files.has(keep.batchId+'.json'));
 const delayed=JSON.parse(JSON.stringify(a));delayed.batchId=crypto.randomUUID();delayed.events[0].eventId=crypto.randomUUID();assert.equal(f.send(delayed).stored,'DISCARDED_BY_ADMIN');
 const fresh=add(f,'0.1.0','TEST',crypto.randomUUID(),new Date(Date.now()+1000).toISOString());assert.equal(f.context.ccReadEvents_(f.book).length,3);
 assert.throws(()=>f.context.executeDashboardDeletion(c.id,c.verification,'삭제'),/만료/);
});
test('selected multi-session deletion cannot touch another environment or a surviving session',()=>{
 const f=admin(),ra=crypto.randomUUID(),rb=crypto.randomUUID(),rc=crypto.randomUUID();add(f,'0.1.0','TEST',ra);add(f,'0.1.0','TEST',rb);add(f,'0.1.0','TEST',rc);add(f,'0.1.0','LIVE',ra);
 const c=challenge(f,{kind:'SESSIONS',gameVersion:'0.1.0',environment:'TEST',runIds:[ra,rb]});assert.equal(c.p.sessions,2);f.context.executeDashboardDeletion(c.id,c.verification,'삭제');
 const remaining=f.context.ccReadEvents_(f.book);assert.equal(remaining.length,2);assert.ok(remaining.some(e=>e.runId===rc));assert.ok(remaining.some(e=>e.environment==='LIVE'));
});
test('deletion preview changes, failed physical compaction and restored raw files never leak logically deleted data',()=>{
 const f=admin(),run=crypto.randomUUID(),a=add(f,'0.1.0','TEST',run),c=challenge(f,{kind:'SESSIONS',gameVersion:'0.1.0',environment:'TEST',runIds:[run]});add(f,'0.1.0','TEST',run);
 assert.throws(()=>f.context.executeDashboardDeletion(c.id,c.verification,'삭제'),/바뀌었/);assert.equal(f.context.ccRules_(f.book).length,0);
 const rule={kind:'SESSIONS',gameVersion:'0.1.0',environment:'TEST',runIds:[run],cutoff:new Date().toISOString()};f.context.ccAppend_(f.book.getSheetByName('삭제 관리'),[['rule',new Date().toISOString(),JSON.stringify(rule),owner,2]]);assert.equal(f.context.ccReadEvents_(f.book).length,0);
 // Rows can remain after a transient deleteRows failure; access is still filtered by the durable rule.
 assert.equal(f.tables.Events.length,3);f.context.ccCompactDeleted_(f.book,f.context.ccRules_(f.book));assert.equal(f.tables.Events.length,1);
});
test('mixed raw batch can be retired after deletion while unrelated version rows survive and retries stay idempotent',()=>{
 const f=admin(),b=JSON.parse(JSON.stringify(f.batch));b.events[0].gameVersion='0.1.0';b.events.push({...b.events[0],sequence:2,eventId:crypto.randomUUID(),gameVersion:'0.2.0'});f.send(b);f.context.ccImport_(f.book);
 const c=challenge(f,{kind:'VERSION',gameVersion:'0.1.0'});f.context.executeDashboardDeletion(c.id,c.verification,'삭제');
 assert.equal(f.context.ccReadEvents_(f.book).length,1);assert.equal(f.context.ccReadEvents_(f.book)[0].gameVersion,'0.2.0');assert.equal(f.files.size,0);
 assert.equal(f.send(b).stored,'SHEETS');assert.equal(f.files.size,0);assert.equal(f.context.ccImport_(f.book),0);assert.equal(f.tables.Events.length,2);
});
test('hash-mismatched originals are protected even when retention is exceeded',()=>{
 const f=admin(),b=add(f);add(f);const file=f.files.get(b.batchId+'.json');f.files.set(b.batchId+'.json',{...file,getBlob:()=>({getDataAsString:()=>'{"tampered":true}'})});f.context.saveRawRetention(true,1);
 f.context.ccPruneRaw_(f.book,[]);assert.ok(f.files.has(b.batchId+'.json'));assert.equal(f.tables.Events.length,3);
});
test('ingest-only deployment never serves the dashboard shell',()=>{const f=fixture();f.context.CC_CONFIG.dashboardEnabled=false;assert.throws(()=>f.context.doGet(),/INGEST_ONLY/);assert.equal(f.send().ok,true);});

test('interrupted physical deletion hides only the durable scope and can resume without another destructive request',()=>{const f=admin();add(f);add(f,'0.2.0');const c=challenge(f,{kind:'VERSION',gameVersion:'0.1.0'});f.failDelete();const result=f.context.executeDashboardDeletion(c.id,c.verification,'삭제');assert.equal(result.pendingCleanup,true);assert.equal(f.tables.Events.length,3);assert.equal(f.context.ccReadEvents_(f.book).length,1);f.context.ccCompactDeleted_(f.book,f.context.ccRules_(f.book));assert.equal(f.tables.Events.length,2);});
