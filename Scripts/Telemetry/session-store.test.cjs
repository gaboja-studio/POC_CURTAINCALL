const {test}=require('node:test'),assert=require('node:assert/strict'),{randomUUID}=require('node:crypto');
const {fixture}=require('./server.fixture.cjs');
function rows(f,days,environment='TEST',runId=randomUUID()){
 const at=new Date(Date.now()-days*86400000).toISOString();
 const events=['PERFORMANCE_STARTED','PERFORMANCE_SUCCEEDED','PLAYER_DIED','ZONE_ENTERED','ZONE_EXITED','FUNDS_CHANGED'].map((type,i)=>({...f.batch.events[0],eventId:randomUUID(),appSessionId:f.batch.appSessionId,sequence:i+1,occurredAt:at,receivedAt:at,gameVersion:'0.1.0',environment,runId,attemptId:runId,playerNumber:i%2,performanceId:'D1P1',sceneType:i>2?'WAITINGROOM':'MAINSTAGE',authority:i===3||i===4?'LOCAL':'HOST',eventType:type,payload:{trickType:'TIGHTROPE',difficulty:1,cause:'FALL',zoneType:'BEDROOM',durationMs:5000,transactionId:'t1',gameDay:1,balanceBefore:10,balanceAfter:20,category:'REWARD'}}));
 f.context.ccAppend_(f.book.getSheetByName('Events'),events.map(e=>f.context.TelemetryCore.row(e,f.batch,at)));return events;
}
const view={environment:'TEST',gameVersion:'0.1.0'};
function plain(x){return JSON.parse(JSON.stringify(x));}
test('TEST 7d and LIVE 30d retain exact summaries, funds and survey evidence while deleting expired detail',()=>{
 const f=fixture();rows(f,8);rows(f,29,'LIVE');const expired=rows(f,31,'LIVE');
 const before=f.context.ccReadEvents_(f.book),old=f.context.ccAggregate_(before,'LIVE',null,'0.1.0');
 const all=f.context.ccSessionUpdate_(f.book),state=f.context.ccSessionState_();
 assert.equal(Object.values(state.sessions).filter(s=>s.archivedAt).length,2);assert.equal(f.tables.Events.length,7);
 const after=f.context.ccAggregate_(all,'LIVE',null,'0.1.0');assert.deepEqual(plain(after.tricks),plain(old.tricks));assert.deepEqual(plain(after.funds).sort((a,b)=>a.runId.localeCompare(b.runId)),plain(old.funds).sort((a,b)=>a.runId.localeCompare(b.runId)));
 const archived=all.find(e=>e.runId===expired[0].runId);assert.equal(archived.sessionSummary.detailFile,null);assert.equal(archived.sessionSummary.summary.links.some(e=>e.eventType==='PLAYER_DIED'),true);
 const v=f.context.ccView_(all,{environment:'LIVE',gameVersion:'0.1.0',timelineRunId:expired[0].runId},true);assert.equal(v.timeline.archived,true);assert.equal(v.timeline.markers.length,0);
});
test('unchanged sessions never reaggregate and a changed session alone is recalculated',()=>{
 const f=fixture(),a=rows(f,1),b=rows(f,1);f.context.ccSessionUpdate_(f.book);const before=f.context.ccSessionState_();let calls=0;const original=f.context.SessionCore.summarize;f.context.SessionCore.summarize=(...args)=>{calls++;return original(...args);};
 f.context.ccSessionUpdate_(f.book);assert.equal(calls,0);const e={...a[0],eventId:randomUUID(),eventType:'PLAYER_DIED'};f.context.ccAppend_(f.book.getSheetByName('Events'),[f.context.TelemetryCore.row(e,f.batch,new Date().toISOString())]);f.context.ccSessionUpdate_(f.book);assert.equal(calls,1);
 const after=f.context.ccSessionState_(),k=f.context.SessionCore.key(b[0]);assert.equal(after.sessions[k].detailFile,before.sessions[k].detailFile);
});
test('interrupted compaction retries without losing or doubling retained statistics',()=>{
 const f=fixture();rows(f,8);rows(f,1);f.failDelete();assert.throws(()=>f.context.ccSessionUpdate_(f.book),/deletion failure/);assert.equal(f.context.ccSessionState_().compacting,true);
 const all=f.context.ccSessionUpdate_(f.book);assert.equal(f.context.ccAggregate_(all,'TEST',null,'0.1.0').eventCount,12);assert.equal(f.tables.Events.length,7);assert.equal(f.context.ccSessionState_().compacting,false);
});
test('late events for an archived session remain visible as pending and cannot overwrite a complete summary',()=>{
 const f=fixture(),old=rows(f,8);f.context.ccSessionUpdate_(f.book);const e={...old[0],eventId:randomUUID()};f.advance(10000);f.context.ccAppend_(f.book.getSheetByName('Events'),[f.context.TelemetryCore.row(e,f.batch,new Date(Date.now()+10000).toISOString())]);const all=f.context.ccSessionUpdate_(f.book);assert.equal(f.context.ccAggregate_(all,'TEST',null,'0.1.0').eventCount,6);assert.equal(f.tables.Events.length,2);assert.equal(f.context.ccSessionStatus_().pendingLate,1);
});
test('weighted metrics keep the original denominator including sessions with no item use',()=>{
 const f=fixture(),a=rows(f,1),b=rows(f,1);const c={...a[3],eventType:'ITEM_COLLECTED',payload:{itemType:'GLOVES'}};a.push(c);
 const merged=f.context.SessionCore.combine([f.context.SessionCore.summarize(a),f.context.SessionCore.summarize(b)]),direct=f.context.TelemetryCore.aggregate(a.concat(b),'TEST');
 assert.deepEqual(plain(merged.usage),plain(direct.usage));assert.deepEqual(plain(merged.stageTimings),plain(direct.stageTimings));
});
test('busy first player cannot hide players 3 and 4; roster includes progress-only participants',()=>{
 const f=fixture(),es=rows(f,1);es.length=1;es[0].payload.playerCount=4;
 for(let i=0;i<450;i++)es.push({...es[0],eventId:randomUUID(),sequence:i+2,eventType:'SECTION_ENTERED',playerNumber:0});
 for(let slot=1;slot<4;slot++)es.push({...es[0],eventId:randomUUID(),eventType:slot===3?'PERFORMANCE_PROGRESS':'PLAYER_DIED',playerNumber:slot});
 const t=f.context.TelemetryCore.dashboardView(es,view).timeline;assert.equal(t.participants.length,4);assert.equal(t.expectedPlayers,4);assert.ok(t.markers.some(e=>e.playerNumber===2));assert.ok(t.markers.length<=400);
});
test('relocation removes only rows whose complete contents were read back from committed session files',()=>{
 const f=fixture(),es=rows(f,1);f.context.ccSessionUpdate_(f.book);const state=f.context.ccSessionState_();
 // Fill above the threshold with records that have NOT been checkpointed.
 const unknown=f.tables.Events[1].slice();unknown[0]=randomUUID();while(f.tables.Events.length<=50000)f.tables.Events.push(unknown.slice());
 state.cursor=f.tables.Events.length;const before=f.tables.Events.length;
 // A mismatching row, despite having the same event ID, must remain too.
 f.tables.Events[2][18]='{"changed":true}';
 assert.equal(f.context.ccRelocateVerified_(f.book,state),5);assert.equal(f.tables.Events.length,before-5);assert.equal(f.tables.Events[1][18],'{"changed":true}');assert.equal(f.tables.Events[2][0],unknown[0]);
});
test('unreadable detail never authorizes relocation and pending intake prevents expiry',()=>{
 const f=fixture();rows(f,8);f.properties.set('IMPORT_STATUS',JSON.stringify({pendingBatches:1,pendingExact:true}));f.context.ccSessionUpdate_(f.book);assert.equal(Object.values(f.context.ccSessionState_().sessions)[0].archivedAt,null);
 const state=f.context.ccSessionState_(),unknown=f.tables.Events[1].slice();while(f.tables.Events.length<=50000)f.tables.Events.push(unknown.slice());state.cursor=f.tables.Events.length;
 f.context.DriveApp.getFileById=()=>{throw Error('unavailable');};assert.equal(f.context.ccRelocateVerified_(f.book,state),0);assert.equal(f.tables.Events.length,50001);
});
test('initial conversion checkpoints and resumes instead of recalculating completed sessions',()=>{
 const f=fixture();rows(f,1);rows(f,1);rows(f,1);const original=f.context.SessionCore.summarize;let calls=0;
 f.context.SessionCore.summarize=(...args)=>{calls++;if(calls===1)f.advance(91000);return original(...args);};
 assert.throws(()=>f.context.ccSessionUpdate_(f.book),/SESSION_MIGRATION_PENDING/);assert.equal(f.context.ccSessionState_().migration.done.length,1);
 f.context.ccSessionUpdate_(f.book);assert.equal(calls,3);assert.equal(f.context.ccSessionState_().migration,null);assert.equal(f.context.ccAggregate_(f.context.ccReadEvents_(f.book),'TEST',null,'0.1.0').eventCount,18);
});
