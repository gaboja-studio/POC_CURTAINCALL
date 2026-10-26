const {test}=require('node:test'),assert=require('node:assert/strict'),crypto=require('node:crypto');
const {fixture}=require('./server.fixture.cjs');
function setup(){const f=fixture();f.setEmail('ourhosu@gmail.com');f.context.ccImportSurveys_=()=>{};return f;}
function send(f,version='0.1.0',environment='TEST'){
 const batch=JSON.parse(JSON.stringify(f.batch));batch.batchId=crypto.randomUUID();batch.events[0].eventId=crypto.randomUUID();Object.assign(batch.events[0],{gameVersion:version,environment,runId:crypto.randomUUID(),playerNumber:1});assert.equal(f.send(batch).ok,true);return batch;
}
const view={environment:'TEST',gameVersion:'0.1.0'};
test('survey-only new versions survive snapshot reload and round filters do not reuse the default view',()=>{
 const f=setup(),s=f.context.SurveyCore,code=['CCS2','0_2_3','TEST','NIGHT1','a'.repeat(32),0,'b'.repeat(32),'1792627200','M2_NIGHT',1].join('.');
 const r={...s.parse(code,'NIGHT1'),responseId:'a',submittedAt:'2026-10-22T00:00:00Z',answers:[{question:'1. 재미',answer:'좋음'}],source:'source',sourceRow:2};
 f.tables['설문 응답']=[Array.from(s.columns),Array.from(s.row(r)),Array.from(s.row({...r,responseId:'b',surveyRound:2}))];
 f.context.refreshDashboard({environment:'TEST'});f.cache.clear();
 const d=f.context.getDashboard({environment:'TEST'});assert.equal(d.gameVersion,'0.2.3');assert.equal(d.eventCount,0);assert.equal(d.surveys.total,1);assert.equal(d.surveys.campaign,'M2_NIGHT:2');
 const older=f.context.getDashboard({environment:'TEST',gameVersion:'0.2.3',surveyCampaign:'M2_NIGHT:1'});assert.equal(older.surveys.campaign,'M2_NIGHT:1');assert.equal(older.surveys.total,1);
});
test('import failure cannot prevent survey sync or publishing already imported gameplay',()=>{
 const f=setup();send(f);f.context.refreshDashboard(view);f.advance(301000);let synced=0;
 f.context.ccImport_=()=>{throw Error('CAPACITY_LIMIT_Events');};f.context.ccImportSurveys_=()=>{synced++;};
 const d=f.context.refreshDashboard(view);assert.equal(synced,1);assert.equal(d.eventCount,1);assert.equal(d.refresh.action,'UPDATED');
 assert.match(d.refresh.lastError,/설문과 적재 완료/);assert.equal(JSON.parse(f.properties.get('DASHBOARD_REFRESH_CONTROL')).code,'CAPACITY_LIMIT_Events');
});
test('opening without a completed snapshot never imports or creates a cache',()=>{
 const f=setup();send(f);f.context.ccImport_=()=>{throw Error('must not import');};f.context.ccReadEvents_=()=>{throw Error('must not scan Sheets');};
 const d=f.context.getDashboard(view);assert.equal(d.eventCount,0);assert.equal(d.refresh.cacheAvailable,false);assert.equal(d.refreshedAt,null);assert.equal(f.cacheFiles.size,0);
});
test('global five-minute cooldown applies to every environment, version, filter and user',()=>{
 const f=setup();send(f);const a=f.context.refreshDashboard(view);assert.equal(a.refresh.action,'UPDATED');assert.equal(a.eventCount,1);const stamp=a.refreshedAt;
 send(f);let imports=0;const original=f.context.ccImport_;f.context.ccImport_=book=>{imports++;return original(book);};
 f.advance(299000);const b=f.context.refreshDashboard({environment:'LIVE',gameVersion:'0.2.0'});assert.equal(b.refresh.action,'CACHED');assert.equal(imports,0);assert.equal(b.refreshedAt,stamp);
 f.advance(2000);const c=f.context.refreshDashboard(view);assert.equal(c.refresh.action,'UPDATED');assert.equal(c.eventCount,2);assert.equal(imports,1);assert.notEqual(c.refreshedAt,stamp);
});
test('read and filter requests use Drive/memory snapshots after cache eviction without reading event Sheets',()=>{
 const f=setup(),a=send(f);send(f,'0.2.0');send(f,'0.1.0','LIVE');f.context.refreshDashboard(view);f.cache.clear();
 f.context.ccReadEvents_=()=>{throw Error('must not scan Sheets');};f.context.ccImport_=()=>{throw Error('must not import');};
 const d=f.context.getDashboard({...view,timelineRunId:a.events[0].runId});assert.equal(d.eventCount,1);assert.equal(d.view.timeline.runId,a.events[0].runId);assert.equal(d.versionOptions.length,2);
 const reads=f.metrics.driveReads;assert.equal(f.context.getDashboard({...view,timelineRunId:a.events[0].runId}).eventCount,1);assert.equal(f.metrics.driveReads,reads);
 assert.equal(f.context.getDashboard({environment:'LIVE',gameVersion:'0.1.0'}).eventCount,1);assert.equal(f.context.getDashboard({...view,gameVersion:'0.3.0'}).eventCount,0);
 assert.equal(f.context.getDashboard({...view,gameVersion:'__proto__'}).eventCount,0);
});
test('another refresh gets the previous snapshot and ingest stays unblocked while statistics run',()=>{
 const f=setup();send(f);f.context.refreshDashboard(view);f.advance(301000);send(f);const publish=f.context.ccPublish_;let observed=false;
 f.context.ccPublish_=(book,all)=>{
  const other=f.context.refreshDashboard(view);assert.equal(other.refresh.action,'RUNNING');assert.equal(other.refresh.refreshing,true);assert.equal(other.eventCount,1);
  assert.equal(f.context.getDashboardRefreshState().refreshing,true);assert.equal(f.locks.script,false);send(f);observed=true;return publish(book,all);
 };
 const d=f.context.refreshDashboard(view);assert.equal(observed,true);assert.equal(d.eventCount,2);assert.equal(d.refresh.refreshing,false);assert.equal(f.locks.user,false);
});
test('concurrent first refresh shows waiting state without manufacturing an initial import',()=>{
 const f=setup();send(f);const publish=f.context.ccPublish_;f.context.ccPublish_=(book,all)=>{const d=f.context.refreshDashboard(view);assert.equal(d.refresh.refreshing,true);assert.equal(d.refresh.cacheAvailable,false);assert.equal(d.eventCount,0);return publish(book,all);};
 assert.equal(f.context.refreshDashboard(view).eventCount,1);
});
test('a failed refresh keeps the last completed snapshot and retries are throttled',()=>{
 const f=setup();send(f);const a=f.context.refreshDashboard(view);f.advance(301000);send(f);let attempts=0;f.context.ccPublish_=()=>{attempts++;throw Error('injected error with private details');};
 const d=f.context.refreshDashboard(view);assert.equal(d.eventCount,1);assert.equal(d.refreshedAt,a.refreshedAt);assert.equal(d.refresh.action,'FAILED');assert.ok(d.refresh.lastError);assert.equal(JSON.stringify(d).includes('private details'),false);assert.equal(d.refresh.refreshing,false);
 f.context.refreshDashboard(view);assert.equal(attempts,1);assert.equal(f.context.getDashboard(view).eventCount,1);
});
test('failed refresh stores a safe diagnostic stage and code without private exception text',()=>{
 const f=setup();f.context.ccImport_=()=>{throw Error('EVENT_ID_CONFLICT');};assert.equal(f.context.refreshDashboard(view).refresh.action,'UPDATED');
 const control=JSON.parse(f.properties.get('DASHBOARD_REFRESH_CONTROL'));assert.equal(control.stage,'IMPORT');assert.equal(control.code,'EVENT_ID_CONFLICT');
 assert.equal(f.context.ccRefreshErrorCode_(Error('private payload and identifiers')),'REFRESH_ERROR');
});
test('an interrupted worker lease expires so the next eligible request can recover',()=>{
 const f=setup();send(f);f.properties.set('DASHBOARD_REFRESH_CONTROL',JSON.stringify({id:'crashed',startedAt:Date.now()-600000,expiresAt:Date.now()-1}));assert.equal(f.context.refreshDashboard(view).refresh.action,'UPDATED');
});
test('script-lock contention serves a cache instead of throwing BUSY_RETRY',()=>{
 const f=setup();send(f);f.context.refreshDashboard(view);f.advance(301000);f.locks.script=true;const d=f.context.refreshDashboard(view);assert.equal(d.eventCount,1);assert.equal(d.refresh.action,'CACHED');assert.equal(f.locks.user,false);
});
test('partial snapshot creation never replaces the published pointer',()=>{
 const f=setup();send(f);f.context.refreshDashboard(view);const pointer=f.properties.get('DASHBOARD_SNAPSHOT');f.advance(301000);send(f);
 const folder=f.context.ccSnapshotFolder_();folder.createFile=()=>{throw Error('injected Drive failure');};const d=f.context.refreshDashboard(view);assert.equal(d.refresh.action,'FAILED');assert.equal(d.eventCount,1);assert.equal(f.properties.get('DASHBOARD_SNAPSHOT'),pointer);
});
test('authenticated deletion rebuilds snapshots immediately and cannot resurrect warm cached events',()=>{
 const f=setup(),a=send(f);f.context.initializeDashboardManagement();const old=f.context.refreshDashboard(view);f.context.getDashboard({...view,timelineRunId:a.events[0].runId});
 const p=f.context.previewDashboardDeletion({kind:'SESSIONS',...view,runIds:[a.events[0].runId]});const c=f.context.requestDashboardDeletionCode(p.previewId),otp=f.mail.at(-1).body.match(/\d{8}/)[0];
 const result=f.context.executeDashboardDeletion(c.challengeId,otp,'삭제');assert.equal(result.deletedEvents,1);const d=f.context.getDashboard(view);assert.equal(d.eventCount,0);assert.notEqual(d.refresh.revision,old.refresh.revision);assert.equal(d.refreshedAt,old.refreshedAt);assert.equal(f.context.getDashboard({...view,timelineRunId:a.events[0].runId}).eventCount,0);
});
test('invalidated cache fails closed while deletion cleanup or cache rebuilding is incomplete',()=>{
 const f=setup();send(f);f.context.refreshDashboard(view);f.properties.set('DASHBOARD_DATA_EPOCH','deleted-generation');const d=f.context.getDashboard(view);assert.equal(d.eventCount,0);assert.equal(d.refresh.cacheAvailable,false);
});
test('cache and refresh status remain inaccessible to unauthenticated callers',()=>{
 const f=setup();send(f);f.context.refreshDashboard(view);f.setEmail('');assert.throws(()=>f.context.getDashboard(view),/ACCESS_DENIED/);assert.throws(()=>f.context.getDashboardRefreshState(),/ACCESS_DENIED/);assert.throws(()=>f.context.refreshDashboard(view),/ACCESS_DENIED/);
});

test('overview snapshots omit the full raw timeline and normalized default selections stay on the fast path',()=>{
 const f=setup();send(f);const d=f.context.refreshDashboard(view);for(const file of f.cacheFiles.values())if(file.getName().includes('-overview.json'))assert.equal(JSON.parse(file.getBlob().getDataAsString()).timeline.length,0);
 f.cache.clear();const read=f.context.ccDriveJson_;f.context.ccDriveJson_=id=>{assert.equal(id.includes('-records.json'),false,'default screen must not load raw records');return read(id);};
 const result=f.context.getDashboard({...view,timelineRunId:d.view.timeline.runId,timelinePhase:d.view.timeline.phaseId,timelineAttemptId:d.view.timeline.attemptId,timelineOffset:0,surveyKind:'NIGHT1',fundsInterval:'DAY'});assert.equal(result.eventCount,1);
});
