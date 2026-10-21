const {test}=require('node:test'),assert=require('node:assert/strict'),crypto=require('node:crypto');
const {fixture}=require('./server.fixture.cjs');
test('ingest ACK is durable and token is absent from raw Drive files',()=>{const f=fixture();assert.equal(f.send().ok,true);assert.equal(f.files.size,1);assert.equal(f.send().ok,true);assert.equal(f.files.size,1);const raw=[...f.files.values()][0].getBlob().getDataAsString();assert.equal(raw.includes('test-only-token'),false);});
test('bad token rejects without writes and same batch ID different bytes conflicts',()=>{const f=fixture();assert.equal(f.send(f.batch,'invalid').error,'UNAUTHORIZED');assert.equal(f.files.size,0);f.send();const changed=JSON.parse(JSON.stringify(f.batch));changed.events[0].payload={changed:true};assert.equal(f.send(changed).error,'BATCH_ID_CONFLICT');assert.equal(f.files.size,1);});
test('partial event append before receipt failure retries exactly once',()=>{const f=fixture();f.send();f.failReceipt();assert.throws(()=>f.context.ccImport_(f.book),/injected failure/);assert.equal(f.tables.Events.length,2);assert.equal(f.tables.Batches.length,1);assert.equal(f.context.ccImport_(f.book),1);assert.equal(f.tables.Events.length,2);assert.equal(f.tables.Batches.length,2);assert.equal(f.context.ccImport_(f.book),0);});
test('duplicate event in a different batch deduplicates and conflicting event is retained for review',()=>{const f=fixture();f.send();f.context.ccImport_(f.book);const b=JSON.parse(JSON.stringify(f.batch));b.batchId=crypto.randomUUID();f.send(b);f.context.ccImport_(f.book);assert.equal(f.tables.Events.length,2);b.batchId=crypto.randomUUID();b.events[0].playerId='changed';f.send(b);assert.throws(()=>f.context.ccImport_(f.book),/EVENT_ID_CONFLICT/);assert.equal(f.tables.Events.length,2);});
test('anonymous execute-as-owner deployment cannot access dashboard/RPC',()=>{const f=fixture();assert.ok(f.context.doGet());assert.throws(()=>f.context.refreshDashboard({}),/DASHBOARD_ACCESS_DENIED/);assert.throws(()=>f.context.getDashboard({}),/DASHBOARD_ACCESS_DENIED/);assert.throws(()=>f.context.installRefreshTrigger(),/DASHBOARD_ACCESS_DENIED/);assert.throws(()=>f.context.scheduledRefresh(),/DASHBOARD_ACCESS_DENIED/);});
test('formula-like untrusted text is escaped for Sheets',()=>{const f=fixture();assert.equal(f.context.ccText_('=IMPORTXML("url")'),'\'=IMPORTXML("url")');assert.equal(f.context.ccText_(0),0);});

test('version column appends safely and a changed version conflicts on replay',()=>{
 const f=fixture();f.batch.events[0].gameVersion='0.1.0';assert.equal(f.send().ok,true);f.context.ccImport_(f.book);
 assert.equal(f.tables.Events[0][21],'gameVersion');assert.equal(f.tables.Events[1][21],'0.1.0');
 const b=JSON.parse(JSON.stringify(f.batch));b.batchId=crypto.randomUUID();b.events[0].gameVersion='0.2.0';assert.equal(f.send(b).ok,true);
 assert.throws(()=>f.context.ccImport_(f.book),/EVENT_ID_CONFLICT/);assert.equal(f.tables.Events.length,2);
});
test('legacy rows replay without conflict after appending version column',()=>{
 const f=fixture();f.send();f.context.ccImport_(f.book);f.tables.Events[1]=f.tables.Events[1].slice(0,21);
 const b=JSON.parse(JSON.stringify(f.batch));b.batchId=crypto.randomUUID();f.send(b);assert.equal(f.context.ccImport_(f.book),1);assert.equal(f.tables.Events.length,2);
});
test('dashboard defaults to current release, filters every view and never falls back to another version',()=>{
 const f=fixture(),old=f.batch.events[0],current={...old,eventId:crypto.randomUUID(),gameVersion:'0.1.0',runId:crypto.randomUUID()},next={...current,eventId:crypto.randomUUID(),gameVersion:'0.2.0',runId:crypto.randomUUID()};
 let d=f.context.ccDashboard_([old,current,next],{environment:'TEST'});assert.equal(d.gameVersion,'0.2.0');assert.equal(d.eventCount,1);assert.equal(d.view.runOptions[0],next.runId);
 d=f.context.ccDashboard_([old,current,next],{environment:'TEST',gameVersion:'0.1.0',timelineRunId:next.runId});assert.equal(d.eventCount,1);assert.equal(d.view.runOptions.length,1);assert.equal(d.view.timeline.events[0].eventId,current.eventId);
 assert.equal(f.context.ccDashboard_([old],{environment:'TEST',gameVersion:'0.1.0'}).eventCount,0);
 assert.equal(f.context.ccDashboard_([old,current],{environment:'TEST',gameVersion:'UNVERSIONED'}).eventCount,1);
});

test('published summaries match dashboard aggregates for TEST/LIVE and each recorded or missing version',()=>{
 const f=fixture(),all=[];
 for(const environment of ['TEST','LIVE'])for(const gameVersion of ['0.1.0','0.2.0',null]){
  const runId=crypto.randomUUID(),attemptId=crypto.randomUUID();let sequence=0;
  const add=(eventType,payload,extra={})=>all.push({...f.batch.events[0],eventId:crypto.randomUUID(),appSessionId:f.batch.appSessionId,environment,gameVersion,runId,attemptId,sequence:++sequence,sceneType:'WAITINGROOM',performanceId:'D1P1',eventType,payload,...extra});
  add('PERFORMANCE_STARTED',{trickType:'TIGHTROPE',difficulty:1,retryIndex:1,playerCount:2,startingLostCount:0},{authority:'HOST'});
  add('PERFORMANCE_SUCCEEDED',{trickType:'TIGHTROPE',difficulty:1,durationMs:10,survivors:1,arrivals:1},{authority:'HOST'});
  add('PLAYER_DIED',{cause:'FALL'},{authority:'HOST'});add('ZONE_ENTERED',{zoneType:'BEDROOM'});add('ZONE_EXITED',{zoneType:'BEDROOM',durationMs:2500});
  add('ITEM_COLLECTED',{itemType:'GLOVES'});add('FUNDS_CHANGED',{transactionId:runId+':1',gameDay:1,balanceBefore:0,balanceAfter:100,category:'REWARD'},{authority:'HOST'});
  add('ZONE_ENTERED',{zoneType:'BEDROOM'},{performanceId:'D2P1'});
 }
 f.context.ccPublish_(f.book,all);
 const value=v=>JSON.parse(JSON.stringify(v)),definitions={
  '묘기 통계':['tricks',t=>[t.trickType,t.difficulty,t.attempts,t.completed,t.wins,t.winRate,t.avgMaxDistance,t.distanceSamples,t.deaths,t.interactions]],
  '사망 원인':['deaths',d=>d.label.split(' / ').concat(d.deaths)],
  '공간 이용':['zones',z=>[z.zoneType,z.visits,z.distinctPlayers,z.avgDwellSeconds,z.dwellSamples]],
  '기물 서비스 이용':['usage',u=>[u.kind,u.itemOrServiceType,u.uses,u.users,u.eligibleUsers,u.usageRate]]};
 for(const environment of ['TEST','LIVE'])for(const gameVersion of ['0.1.0','0.2.0','UNVERSIONED']){
  const d=f.context.ccDashboard_(all,{environment,gameVersion});
  for(const [name,[key,project]]of Object.entries(definitions)){
   const rows=f.tables[name].slice(1).filter(r=>r.at(-1)===environment&&r.at(-2)===gameVersion).map(r=>r.slice(0,-2));
   assert.deepEqual(value(rows),value(d[key].map(project)),name+' '+environment+' '+gameVersion);
  }
  const rows=f.tables['자금 일계'].slice(1).filter(r=>r[6]===gameVersion&&r[7]===environment);
  assert.deepEqual(value(rows.filter(r=>r[11]==='AVERAGE').map(r=>[r[1],r[8],r[2],r[3],r[9]])),value(d.view.funds.series.map(p=>[p.day,p.balance,p.rewards,p.spent,p.samples])));
  const run=d.runIds[0],view=f.context.ccDashboard_(all,{environment,gameVersion,fundsRunId:run}).view.funds;
  assert.deepEqual(value(rows.filter(r=>r[0]===run&&r[11]==='RUN').map(r=>[r[1],r[8],r[2],r[3],r[10]])),value(view.series.map(p=>[p.day,p.balance,p.rewards,p.spent,p.carried])));
 }
 const counts=Object.fromEntries(Object.keys(definitions).concat('자금 일계').map(n=>[n,f.tables[n].length]));
 f.context.ccPublish_(f.book,all);for(const[n,count]of Object.entries(counts))assert.equal(f.tables[n].length,count,'refresh replaces instead of appending '+n);
});
test('published break balances equal chart averages and individual runs without modifying daily rows',()=>{
 const f=fixture(),base={...f.batch.events[0],appSessionId:f.batch.appSessionId,gameVersion:'0.1.0',authority:'HOST',sceneType:'WAITINGROOM',breakId:'D1T3',performanceId:'D1P1'},all=[];
 for(const [i,balance]of [100,200].entries()){const runId=crypto.randomUUID();all.push({...base,eventId:crypto.randomUUID(),runId,eventType:'FUNDS_CHANGED',payload:{transactionId:runId+':1',gameDay:1,balanceBefore:0,balanceAfter:balance,category:'REWARD'}});}
 f.context.ccPublish_(f.book,all);const chart=f.context.ccDashboard_(all,{environment:'TEST',gameVersion:'0.1.0',fundsInterval:'BREAK'}).view.funds;
 const rows=f.tables['자금 구간'].slice(1).filter(r=>r[6]==='0.1.0'&&r[7]==='TEST');assert.equal(rows.filter(r=>r[11]==='RUN').length,2);const average=rows.find(r=>r[11]==='AVERAGE');assert.equal(average[8],chart.series[0].balance);assert.equal(average[8],150);assert.equal(average[9],2);assert.equal(average[2],3);
 f.context.ccPublish_(f.book,all);assert.equal(f.tables['자금 구간'].length,4);
});
