const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const vm=require('node:vm');
const {randomUUID}=require('node:crypto');
const ctx=vm.createContext({});vm.runInContext(fs.readFileSync(__dirname+'/core.gs','utf8'),ctx);
const core=ctx.TelemetryCore;
const app=randomUUID(),sender=randomUUID(),run=randomUUID(),attempt=randomUUID();
function event(type,payload={},extra={}){return {eventId:randomUUID(),occurredAt:'2026-10-21T08:00:00.000Z',sequence:1,monotonicMs:1,environment:'TEST',playerId:'p1',playerNumber:0,networkClientId:'0',roomId:null,runId:run,attemptId:attempt,performanceId:'D1P1',breakId:null,sceneType:'MAINSTAGE',eventType:type,authority:'HOST',payload,...extra,appSessionId:app,senderId:sender};}
function batch(events){return {schemaVersion:1,batchId:randomUUID(),senderId:sender,appSessionId:app,flushReason:'CHECKPOINT',events:events.map((e,i)=>({...e,sequence:i+1}))};}

test('host roster accepts unresolved slots but rejects duplicate clients and invalid slot identities',()=>{
 const people=Array.from({length:4},(_,i)=>({playerId:'player-'+i,networkClientId:String(i),playerNumber:i===3?null:i}));
 const roster=event('PERFORMANCE_ROSTER',{trickType:'CANNON',difficulty:1,playerCount:4,participants:people});
 assert.doesNotThrow(()=>core.validate(batch([roster])));
 people[3].networkClientId='0';assert.throws(()=>core.validate(batch([roster])));people[3].networkClientId='3';
 people[3].playerNumber=4;assert.throws(()=>core.validate(batch([roster])));
 const arrived=event('PLAYER_ARRIVED',{position:{x:0,y:1,z:50},sectionId:null,lostCount:0});assert.doesNotThrow(()=>core.validate(batch([arrived])));
 arrived.attemptId=null;assert.throws(()=>core.validate(batch([arrived])));
});

test('cannon contact without a cut is valid and never inflates deaths or limb damage',()=>{
 const p={sectionId:1,position:{x:1,y:2,z:3},obstacleId:'OB_contact',hazardType:'GroundSaw',cutApplied:false,part:'None',lostCount:0,reason:'FRAME_LIMIT'};
 const hit=event('CANNON_HAZARD_CONTACT',p);core.validate(batch([hit]));
 const d=core.aggregate([event('PERFORMANCE_STARTED',{trickType:'CANNON',difficulty:1}),hit],'TEST');
 assert.equal(d.tricks[0].deaths,0);assert.equal(d.deaths.length,0);assert.equal(d.tricks[0].interactions,0);
 assert.throws(()=>core.validate(batch([{...hit,payload:{...p,cutApplied:'false'}}])));
});
test('operation summary counts confirmed failures and local carry events once',()=>{
 const a=event('PURCHASE_FAILED',{operation:'PURCHASE',reason:'SHOP_NotEnoughFunds'},{sceneType:'WAITINGROOM'});
 const b=event('ITEM_DROPPED',{}, {sceneType:'WAITINGROOM',authority:'LOCAL'});
 const r=core.aggregate([a,a,b,{...a,eventId:randomUUID(),authority:'OBSERVED'},{...b,eventId:randomUUID(),authority:'HOST'}],'TEST').operations;
 assert.equal(r.length,2);assert.equal(r.reduce((s,x)=>s+x.count,0),2);
});
function obligation(type,payload={},extra={}){return event(type,{flowId:'order',itemType:'tool-a',quantity:1,...payload},{sceneType:'WAITINGROOM',breakId:'D1T3',gameVersion:'0.1.0',...extra});}
test('break close confirms only remaining quantities and unused medical reservations once',()=>{
 const rows=[obligation('PURCHASE_COMPLETED',{quantity:2}),obligation('ITEM_COLLECTED',{}, {authority:'LOCAL'}),
  obligation('SERVICE_RESERVED',{flowId:'medical',serviceType:'RESTORE_BODY'},{authority:'LOCAL'}),obligation('BREAK_ENDED'),obligation('BREAK_ENDED')];
 const r=core.aggregate(rows,'TEST',null,'0.1.0').outstanding;
 assert.equal(r.length,2);assert.equal(r.find(x=>x.kind==='ITEM').quantity,1);assert.equal(r.find(x=>x.kind==='MEDICAL').quantity,1);
 assert.ok(r.every(x=>x.reason==='BREAK_ENDED'));
});
test('collected orders, entered medical visits and facility reservations are not unused',()=>{
 const rows=[obligation('PURCHASE_COMPLETED'),obligation('ITEM_COLLECTED',{}, {authority:'LOCAL'}),
 obligation('SERVICE_RESERVED',{flowId:'medical',serviceType:'RESTORE_BODY'},{authority:'LOCAL'}),obligation('MEDICAL_ENTERED',{flowId:'medical'},{authority:'LOCAL'}),
 obligation('SERVICE_RESERVED',{flowId:'facility',serviceType:'FACILITY'}),obligation('BREAK_ENDED')];
 assert.equal(core.aggregate(rows,'TEST').outstanding.length,0);
});
test('missing closure remains unknown; player exit is distinct and another app/run/break/version cannot close it',()=>{
 const purchase=obligation('PURCHASE_COMPLETED');assert.equal(core.aggregate([purchase],'TEST').outstanding.length,0);
 for(const extra of [{appSessionId:'another'},{runId:randomUUID()},{breakId:'D2T1'},{gameVersion:'0.2.0'}])
  assert.equal(core.aggregate([purchase,{...obligation('BREAK_ENDED'),...extra}],'TEST',null,'0.1.0').outstanding.length,0);
 const r=core.aggregate([purchase,obligation('PLAYER_EXITED'),obligation('BREAK_ENDED')],'TEST').outstanding;
 assert.equal(r.length,1);assert.equal(r[0].reason,'PLAYER_EXITED');
});
test('host break snapshot distinguishes departed buyers, while unknown client identity stays unknown',()=>{
 const absent=obligation('PURCHASE_COMPLETED',{}, {networkClientId:'42'}),close=obligation('BREAK_ENDED',{presentNetworkClientIds:['0']});
 assert.equal(core.aggregate([absent,close],'TEST').outstanding[0].reason,'PLAYER_EXITED');
 assert.equal(core.aggregate([{...absent,networkClientId:null},close],'TEST').outstanding[0].reason,'BREAK_ENDED');
});
test('cancelled medical reservation is separate from timeout and counted once',()=>{
 const a=obligation('SERVICE_RESERVED',{flowId:'medical',serviceType:'RESTORE_BODY'},{authority:'LOCAL'});
 const rows=[a,obligation('SERVICE_ABANDONED',{flowId:'medical'},{authority:'LOCAL'}),obligation('BREAK_ENDED')];
 const r=core.aggregate(rows,'TEST').outstanding;assert.equal(r.length,1);assert.equal(r[0].reason,'SERVICE_ABANDONED');
});
test('valid batch preserves nullable unavailable identity and exact slots',()=>{core.validate(batch([event('GAME_STARTED',{}, {playerNumber:null,networkClientId:'18446744073709551615'})]));});
test('game version validates without breaking old batches and columns',()=>{
 for(const v of ['0.1.0','0.2.0-preview.1',null])core.validate(batch([event('GAME_STARTED',{}, {gameVersion:v})]));
 for(const v of ['',123,'=IMPORTXML()'])assert.throws(()=>core.validate(batch([event('GAME_STARTED',{}, {gameVersion:v})])),/GAME_VERSION/);
 const e=event('GAME_STARTED',{}, {gameVersion:'0.1.0'}),b=batch([e]),r=core.row(e,b,'now');assert.equal(r[21],'0.1.0');assert.equal(r[19],b.batchId);assert.equal(core.decode(r).gameVersion,'0.1.0');
 assert.equal(core.versionOf(core.decode(r.slice(0,21))),'UNVERSIONED');
});
test('version filter separates performance and stage timing samples even in the same run',()=>{
 const p={trickType:'TIGHTROPE',difficulty:1},a=event('PERFORMANCE_SUCCEEDED',p,{gameVersion:'0.1.0'}),b=event('PERFORMANCE_FAILED',p,{gameVersion:'0.2.0'});
 const time=v=>event('ITEM_STORED',{storageArrivalToStoreMs:v},{gameVersion:v===1000?'0.1.0':'0.2.0',sceneType:'WAITINGROOM',authority:'LOCAL'});
 const d=core.aggregate([a,b,time(1000),time(9000)],'TEST',null,'0.1.0');assert.equal(d.tricks[0].winRate,1);assert.equal(d.stageTimings.find(t=>t.key==='arrivalStore').avgSeconds,1);assert.equal(d.eventCount,2);
});
test('validation rejects schema, unsafe number, unknown event and invalid enum',()=>{
 assert.throws(()=>core.validate({...batch([event('GAME_STARTED')]),schemaVersion:2}));
 assert.throws(()=>core.validate(batch([event('PERFORMANCE_STARTED',{trickType:'TIGHTROPE',difficulty:0,retryIndex:1,playerCount:1,startingLostCount:0})])));
 assert.throws(()=>core.validate(batch([event('NOT_REGISTERED')])));
 assert.throws(()=>core.validate(batch([event('GAME_STARTED',{}, {playerNumber:4})])));
});
test('duplicate event IDs, missing attempt and negative durations rejected',()=>{
 const e=event('GAME_STARTED');assert.throws(()=>core.validate(batch([e,e])));
 assert.throws(()=>core.validate(batch([event('SECTION_ENTERED',{sectionId:1,lostCount:0},{attemptId:null})])));
 assert.throws(()=>core.validate(batch([event('SCENE_EXITED',{sceneName:'Start',durationMs:-1})])));
});
test('catalog covers user requested flows without audio blobs',()=>{
 for(const name of ['CANNON_FIRED','CANNON_FLIGHT_ENDED','MICROPHONE_INPUT','ITEM_HANDED_OVER','SERVICE_ABANDONED','RECALL_EXPIRED','FUNDS_CHANGED'])assert.ok(core.catalog[name]);
 assert.equal(Object.prototype.hasOwnProperty.call(core.catalog.MICROPHONE_INPUT,'audio'),false);
});
test('unavailable cannon metadata is explicit null, never zero or a missing key',()=>{
 const p={launchId:randomUUID(),sectionId:null,position:{x:0,y:0,z:0},reachedTarget:null,reason:'FLIGHT_STOPPED'};
 core.validate(batch([event('CANNON_FLIGHT_ENDED',p)]));
 assert.throws(()=>core.validate(batch([event('CANNON_FLIGHT_ENDED',{...p,sectionId:0})])));
 const missing={...p};delete missing.sectionId;assert.throws(()=>core.validate(batch([event('CANNON_FLIGHT_ENDED',missing)])));
});
test('winner denominator excludes unfinished and participant observations',()=>{
 const p={trickType:'TIGHTROPE',difficulty:1};
 const events=[event('PERFORMANCE_STARTED',p),event('PERFORMANCE_SUCCEEDED',p),event('PERFORMANCE_SUCCEEDED',p,{authority:'OBSERVED'}),event('PERFORMANCE_SUCCEEDED',p),event('PERFORMANCE_STARTED',p,{attemptId:randomUUID()})];
 const t=core.aggregate(events,'TEST').tricks[0];assert.equal(t.attempts,2);assert.equal(t.completed,1);assert.equal(t.wins,1);assert.equal(t.winRate,1);
});
test('test data never appears in LIVE and missing distance stays null',()=>{const p={trickType:'CANNON',difficulty:1};assert.equal(core.aggregate([event('PERFORMANCE_STARTED',p)],'LIVE').eventCount,0);assert.equal(core.aggregate([event('PERFORMANCE_STARTED',p)],'TEST').tricks[0].avgMaxDistance,null);});
test('first host terminal outcome is stable even when it was a failure',()=>{const p={trickType:'CANNON',difficulty:1};const t=core.aggregate([event('PERFORMANCE_FAILED',p),event('PERFORMANCE_SUCCEEDED',p)],'TEST').tricks[0];assert.equal(t.completed,1);assert.equal(t.wins,0);assert.equal(t.winRate,0);});
test('distance uses participant-attempt maxima, not sum of snapshots',()=>{
 const p={trickType:'TIGHTROPE',difficulty:1};const a=[event('PERFORMANCE_STARTED',p),event('PERFORMANCE_PROGRESS',{...p,maxDistance:10},{authority:'LOCAL'}),event('PERFORMANCE_PROGRESS',{...p,maxDistance:20},{authority:'LOCAL'}),event('PERFORMANCE_PROGRESS',{...p,maxDistance:0},{authority:'LOCAL',playerId:'p2',playerNumber:1,networkClientId:'1'})];
 const t=core.aggregate(a,'TEST').tricks[0];assert.equal(t.avgMaxDistance,10);assert.equal(t.distanceSamples,2);
});
test('funds count host ledger transaction once, excluding client replicas',()=>{
 const p={transactionId:'ledger:1',gameDay:1,balanceBefore:0,balanceAfter:100,category:'REWARD'};
 const r=core.aggregate([event('FUNDS_CHANGED',p),event('FUNDS_CHANGED',p),event('FUNDS_CHANGED',{...p,transactionId:'client'},{authority:'OBSERVED'})],'TEST');
 assert.equal(r.funds[0].transactions,1);assert.equal(r.funds[0].rewards,100);
});
test('usage denominator is observed waiting-room players, visits retain zero dwell',()=>{
 const a=[event('ZONE_ENTERED',{zoneType:'BEDROOM'},{sceneType:'WAITINGROOM',authority:'LOCAL'}),event('ZONE_ENTERED',{zoneType:'BEDROOM'},{sceneType:'WAITINGROOM',authority:'LOCAL',playerId:'p2',playerNumber:1,networkClientId:'1'}),event('ZONE_EXITED',{zoneType:'BEDROOM',durationMs:0},{sceneType:'WAITINGROOM',authority:'LOCAL'}),event('SERVICE_RESERVED',{serviceType:'RESTORE_BODY'},{sceneType:'WAITINGROOM',authority:'LOCAL'})];
 const r=core.aggregate(a,'TEST');assert.equal(r.usage[0].usageRate,.5);assert.equal(r.zones[0].avgDwellSeconds,0);
});
test('facility usage accepts host confirmation once, without local replica or medical host duplication',()=>{
 const flowId=randomUUID(),p={flowId,serviceType:'FACILITY',lostCount:0,targetCount:0,amount:20,adjustment:'Strengthen',appliesOnDay:3};
 const accepted=event('SERVICE_RESERVED',p,{sceneType:'WAITINGROOM'});
 core.validate(batch([accepted]));
 const rows=[accepted,accepted,event('SERVICE_RESERVED',p,{authority:'LOCAL'}),event('SERVICE_RESERVED',{...p,serviceType:'RESTORE_BODY'})];
 const usage=core.aggregate(rows,'TEST').usage;assert.equal(usage.length,1);assert.equal(usage[0].itemOrServiceType,'FACILITY');assert.equal(usage[0].uses,1);
});
test('host remote-collection timing and collector copy count as one item use',()=>{
 const p={flowId:randomUUID(),itemType:'tool-a',quantity:1,buyerId:'buyer'};
 const host=event('ITEM_COLLECTED',{...p,availableToCollectionMs:5000,source:'HOST_RECEIPT_SNAPSHOT'},{sceneType:'WAITINGROOM',authority:'HOST',playerId:'collector'});
 const local=event('ITEM_COLLECTED',{...p,availableToCollectionMs:null,source:'LOCAL_RECEIPT_SNAPSHOT'},{sceneType:'WAITINGROOM',authority:'LOCAL',playerId:'collector'});
 core.validate(batch([host]));core.validate(batch([local]));
 const r=core.aggregate([host,local],'TEST');assert.equal(r.usage.length,1);assert.equal(r.usage[0].uses,1);assert.equal(r.usage[0].users,1);
});
test('same client timestamp ties use sequence ordering and untrusted strings stay text',()=>{const a=event('GAME_STARTED',{}, {sequence:2}),b=event('GAME_STARTED',{}, {sequence:1});assert.equal(core.aggregate([a,b],'TEST').timeline[0].sequence,1);const r=core.row(event('GAME_STARTED',{}, {playerId:'=IMPORTXML("x")'}),batch([]),'now');assert.equal(r[7],'=IMPORTXML("x")');});
test('untrusted zone identifiers cannot collide with JavaScript prototypes',()=>{const r=core.aggregate([event('ZONE_ENTERED',{zoneType:'__proto__'},{sceneType:'WAITINGROOM',authority:'LOCAL'}),event('ZONE_ENTERED',{zoneType:'toString'},{sceneType:'WAITINGROOM',authority:'LOCAL'})],'TEST');assert.equal(r.zones.length,2);assert.equal(r.zones[0].visits,1);});

test('waiting metrics count measured intervals including zero, without inventing historical arrivals',()=>{
 const local={sceneType:'WAITINGROOM',authority:'LOCAL'};
 const a=event('ZONE_ENTERED',{zoneType:'BEDROOM',spawnToZoneMs:0},local);
 const rows=[a,a,event('ZONE_ENTERED',{zoneType:'BEDROOM',spawnToZoneMs:2000},local),event('ZONE_ENTERED',{zoneType:'BEDROOM'},local),event('ZONE_ENTERED',{zoneType:'HALLWAY',spawnToZoneMs:null},local)];
 const m=core.aggregate(rows,'TEST').waiting;assert.equal(m.length,1);assert.equal(m[0].samples,2);assert.equal(m[0].avgSeconds,1);assert.equal(m[0].minSeconds,0);
});
test('assembly durations use host authority, reject invalid samples and preserve environment filters',()=>{
 const payload={firstLeftToAssemblyMs:4500,alarmToAssemblyMs:2000},host={sceneType:'WAITINGROOM',authority:'HOST'};
 const rows=[event('ALL_PLAYERS_ASSEMBLED',payload,host),event('ALL_PLAYERS_ASSEMBLED',payload,{...host,authority:'OBSERVED'}),event('ALL_PLAYERS_ASSEMBLED',{firstLeftToAssemblyMs:-1,alarmToAssemblyMs:NaN},host)];
 const m=core.aggregate(rows,'TEST').waiting;assert.equal(m.length,2);assert.equal(m.find(x=>x.key==='firstLeftToAssembly').avgSeconds,4.5);assert.equal(m[0].samples,1);assert.equal(core.aggregate(rows,'LIVE').waiting.length,0);
});
test('stage averages preserve zero and exclude missing, invalid, duplicate or wrong-authority samples',()=>{
 const p={screenToConfirmMs:0},local={sceneType:'WAITINGROOM',authority:'LOCAL'};
 const zero=event('SCHEDULE_CONFIRMED',p,local),two=event('SCHEDULE_CONFIRMED',{screenToConfirmMs:2000},local);
 const rows=[zero,zero,two,...[null,undefined,-1,NaN,Infinity,'100'].map(v=>event('SCHEDULE_CONFIRMED',{screenToConfirmMs:v},local)),event('SCHEDULE_CONFIRMED',{screenToConfirmMs:9000},{...local,authority:'HOST'})];
 const m=core.aggregate(rows,'TEST').stageTimings.find(m=>m.key==='scheduleConfirm');
 assert.equal(m.samples,2);assert.equal(m.avgSeconds,1);assert.equal(m.minSeconds,0);assert.equal(m.maxSeconds,2);
 const missing=core.aggregate([],'TEST').stageTimings;assert.equal(missing.length,18);assert.ok(missing.every(m=>m.samples===0&&m.avgSeconds===null&&m.minSeconds===null));
});
test('all collected stage fields map to the proper group and service type',()=>{
 const definitions=core.aggregate([],'TEST').stageTimings;
 const rows=definitions.map(m=>event(m.eventType,{[m.field]:3000,ready:true,serviceType:m.serviceType},
  {sceneType:'WAITINGROOM',authority:m.group==='FACILITY'||m.group==='SHOP'&&!['STORAGE_REACHED','ITEM_STORED'].includes(m.eventType)?'HOST':'LOCAL'}));
 for(const m of core.aggregate(rows,'TEST').stageTimings){assert.equal(m.samples,1,m.key);assert.equal(m.avgSeconds,3,m.key);}
});
test('collection stage chooses one valid host order sample; local clock gaps are never reconstructed',()=>{
 const p={flowId:'order',availableToCollectionMs:5000},extra={sceneType:'WAITINGROOM',playerId:'collector'};
 const host=event('ITEM_COLLECTED',p,extra),local=event('ITEM_COLLECTED',{...p,availableToCollectionMs:9000},{...extra,authority:'LOCAL'});
 const rows=[local,host,host,event('ITEM_COLLECTED',{flowId:'missing',availableToCollectionMs:null},{...extra,authority:'LOCAL'})];
 const m=core.aggregate(rows,'TEST').stageTimings.find(m=>m.key==='availableCollection');assert.equal(m.samples,1);assert.equal(m.avgSeconds,5);
 const reverse=core.aggregate([host,local],'TEST').stageTimings.find(m=>m.key==='availableCollection');assert.equal(reverse.avgSeconds,5);
});
test('stage samples stay within environment/run and ready cancellation is not completion',()=>{
 const p={screenToReadyMs:1000,confirmationToReadyMs:500,ready:true},local={sceneType:'WAITINGROOM',authority:'LOCAL'};
 const rows=[event('READY_CHANGED',p,local),event('READY_CHANGED',{...p,screenToReadyMs:9000},{...local,runId:'other'}),event('READY_CHANGED',{...p,ready:false},local)];
 assert.equal(core.aggregate(rows,'TEST',run).stageTimings.find(m=>m.key==='scheduleReady').avgSeconds,1);
 assert.ok(core.aggregate(rows,'LIVE').stageTimings.every(m=>m.samples===0));
});
