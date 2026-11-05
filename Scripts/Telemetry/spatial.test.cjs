const test=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm'),{randomUUID}=require('node:crypto');
const ctx=vm.createContext({});vm.runInContext(fs.readFileSync(__dirname+'/core.gs','utf8'),ctx);const core=ctx.TelemetryCore;
const run=randomUUID(),attempt=randomUUID();let sequence=0;
test('a legacy stage trace without an attempt remains visible without inventing an outcome',()=>{
 const e=event('GIMMICK_STATE',500,{sceneType:'MAINSTAGE',attemptId:null,breakId:null},{sceneName:'Tightrope'});
 const t=core.dashboardView([e],{environment:'TEST'}).timeline;
 assert.equal(t.phases[0].kind,'STAGE_TRACE');assert.equal(t.phases[0].traceTrick,'TIGHTROPE');assert.equal(t.phases[0].attempts.length,0);
 assert.equal(t.replay.points[0].event.eventId,e.eventId);
});
function event(type,time,extra={},payload={}){return {eventId:randomUUID(),occurredAt:new Date(1793800000000+time).toISOString(),appSessionId:'host-app',sequence:++sequence,monotonicMs:time,environment:'TEST',gameVersion:'0.2.0',runId:run,attemptId:null,performanceId:'D1P1',breakId:'D1T2',roomId:'a'.repeat(32),sceneType:'WAITINGROOM',authority:'LOCAL',playerId:'owner',playerNumber:1,networkClientId:'1',eventType:type,payload:{spatialVersion:1,spatialScope:'room/D1T2',clockSource:'SERVER',clockId:'room',timelineMs:time,floorY:0,position:{x:time/1000,y:0,z:0},reason:'MOVE',...payload},...extra};}
test('server time orders multiple owners despite UTC skew; world actors remain independent',()=>{
 const a=event('SPATIAL_SAMPLE',1000,{occurredAt:'2026-11-05T10:00:10Z'}),b=event('SPATIAL_SAMPLE',2000,{occurredAt:'2026-11-05T09:59:10Z'});
 const pursuer=event('PURSUIT_STATE',1500,{authority:'HOST',playerNumber:0},{entityId:'PU_one',entityKind:'PURSUER',state:'Run'});
 const replay=core.spatialReplay([b,a,pursuer]);assert.equal(replay.clock,'SERVER');assert.deepEqual(Array.from(replay.points,p=>p.timeMs),[1000,1500,2000]);assert.notEqual(replay.points[0].entityId,replay.points[1].entityId);
});
test('replay preserves every footprint and critical event beyond old 400 marker limit',()=>{
 const rows=Array.from({length:720},(_,i)=>event('SPATIAL_SAMPLE',i*1000));rows.push(event('SPATIAL_DIED',721000,{}, {reason:'PURSUIT'}));
 const t=core.dashboardView(rows,{environment:'TEST'}).timeline;assert.equal(t.replay.points.length,721);assert.equal(t.replay.points.at(-1).event.eventType,'SPATIAL_DIED');
});
test('geometry is isolated from actors and distinct floors and scopes survive',()=>{
 const map=event('SPATIAL_GEOMETRY',0,{authority:'HOST'},{entityId:'MAP_a',entityKind:'STRUCTURE',size:{x:10,y:2,z:10},floorY:-6});
 const r=core.spatialReplay([map,event('SPATIAL_SAMPLE',1000),event('SPATIAL_SAMPLE',2000,{}, {floorY:4,spatialScope:'room/rehearsal'})]);
 assert.equal(r.geometry.length,1);assert.equal(r.points.length,2);assert.deepEqual(Array.from(r.floors),[-6,0,4]);assert.equal(r.scopes.length,2);
});
test('escape phase wins over stale performance attempt, legacy remains available',()=>{
 const rows=[event('ESCAPE_ARRIVED',3000,{sceneType:'EXITROUTE',attemptId:attempt}),event('PLAYER_DIED',4000,{sceneType:'MAINSTAGE',attemptId:attempt,breakId:null},{spatialVersion:undefined})];
 const t=core.dashboardView(rows,{environment:'TEST',timelinePhase:'D1/ESCAPE'}).timeline;assert.equal(t.total,1);assert.equal(t.replay.points[0].event.eventType,'ESCAPE_ARRIVED');
 const old=core.dashboardView(rows.slice(1),{environment:'TEST'}).timeline;assert.equal(old.markers.length,1);assert.equal(old.replay.points.length,0);
});
test('unsynchronized clocks are labelled and unknown positions are not fabricated',()=>{
 const r=core.spatialReplay([event('SPATIAL_SAMPLE',1000),event('SPATIAL_SAMPLE',2000,{}, {clockSource:'LOCAL'}),event('CLUE_INSPECTED',3000,{}, {position:null})]);
 assert.equal(r.clock,'UTC_UNALIGNED');assert.equal(r.points.length,2);
});
test('new payload contracts validate as a complete uploaded batch and reject malformed positions',()=>{
 const rows=[event('SPATIAL_SAMPLE',1000),event('PURSUIT_STATE',1100,{authority:'HOST'},{entityId:'PU_a',state:'Run'}),event('AUDIENCE_REACTION',1200,{authority:'HOST'},{reaction:'CHEER'}),event('ESCAPE_CUTTER',1300,{authority:'HOST',sceneType:'EXITROUTE'},{deviceId:'IN_a',shotId:'IN_a:1:1:1',state:'FIRED',remainingCables:3,objectPosition:{x:0,y:1,z:2}})];
 const batch={schemaVersion:1,batchId:randomUUID(),senderId:randomUUID(),appSessionId:randomUUID(),flushReason:'SCENE_CHANGED',events:rows};
 assert.doesNotThrow(()=>core.validate(batch));rows[0].payload.position.x='NaN';assert.throws(()=>core.validate(batch));
});
const html=fs.readFileSync(__dirname+'/dashboard.html','utf8'),poseSource=html.slice(html.indexOf('function replayPose('),html.indexOf('function renderSpatialReplay('));vm.runInContext(poseSource,ctx);
function point(time,x,floor=0,type='SPATIAL_SAMPLE'){return {timeMs:time,floorY:floor,position:{x,y:floor,z:0},event:{eventType:type}};}
test('tightrope base survives missing geometry; relay updates are lane-specific and reversible',()=>{
 const base=require('./tightrope-map.json'),lane=2,initial=base.lanes[lane].spans;
 assert.equal(ctx.tightropeBase('0.2.1','Tightrope'),base.id);
 assert.equal(ctx.tightropeBase('0.2.1','CANNON'),null);
 const connected=[[initial[0][0],initial[1][1]],...initial.slice(2)];
 const update=(timeMs,ropeSpans)=>({timeMs,event:{payload:{lane,ropeSpans}}});
 const changes=[update(100,connected),update(200,initial),update(300,[])];
 const at=t=>ctx.ropeStateAt(base,changes,t);
 assert.equal(at(0).get(lane),initial);assert.equal(at(200).get(lane),initial);
 assert.deepEqual(at(300).get(lane),[]);assert.equal(at(150).get(lane),connected);
 assert.equal(at(150).get(0),base.lanes[0].spans);
});
test('playback seeks backwards and interpolates only observed same-floor movement',()=>{
 const track=[point(0,0),point(5000,10)];assert.equal(ctx.replayPose(track,2500).position.x,5);assert.equal(ctx.replayPose(track,-1),null);assert.equal(ctx.replayPose(track,1000).position.x,2);
 assert.equal(ctx.replayPose([point(0,0),point(5000,10,4)],2500).position.x,0);
 assert.equal(ctx.replayPose([point(0,0),point(10000,10)],5000).position.x,0);
 assert.equal(ctx.replayPose([point(0,0,0,'SPATIAL_DIED'),point(5000,10)],2500).position.x,0);
 assert.equal(ctx.replayPose([point(0,0),point(5000,200)],2500).position.x,0);
});
test('complete dashboard script parses',()=>{for(const match of html.matchAll(/<script>([\s\S]*?)<\/script>/g))assert.doesNotThrow(()=>new vm.Script(match[1]));});
test('floor images require existing dashboard authorization and return only the requested scene',()=>{
 const mapsContext=vm.createContext({ccViewer_(auth){if(auth!=='member')throw new Error('DENIED');}});
 vm.runInContext(fs.readFileSync(__dirname+'/spatial-maps-server.gs','utf8'),mapsContext);
 mapsContext.CC_SPATIAL_MAPS={ground:{scene:'WaitingRoom',floorY:0},basement:{scene:'WaitingRoom',floorY:-5},other:{scene:'Escape'}};
 assert.throws(()=>mapsContext.getSpatialMaps(['ground'],null),/DENIED/);
 assert.deepEqual(Object.keys(mapsContext.getSpatialMaps(['ground'],'member')).sort(),['basement','ground']);
 assert.deepEqual(Object.keys(mapsContext.getSpatialMaps(['missing'],'member')),[]);
 assert.throws(()=>mapsContext.getSpatialMaps(Array(25).fill('ground'),'member'),/INVALID_MAP_REQUEST/);
});
test('same-room runs stay distinct and team settlement never adds observed copies',()=>{
 const roomSession='a'.repeat(32),r2=randomUUID(),s={sceneType:'MAINSTAGE',attemptId:attempt,breakId:null,authority:'HOST'};
 const rows=[event('RUN_STARTED',0,{}, {roomSession,runNumber:1}),event('RUN_STARTED',5,{runId:r2},{roomSession,runNumber:2}),event('PERFORMANCE_STARTED',100,s,{trickType:'CANNON',playerCount:2}),event('AUDIENCE_REACTION',200,s,{reaction:'CHEER'}),event('STAGE_SETTLED',300,s,{teamAudience:7,tokens:21,people:[{slot:0,audience:7,tokens:21}]}),event('STAGE_SETTLED',301,{...s,authority:'OBSERVED'},{tokens:0})];
 const v=core.dashboardView(rows,{environment:'TEST',timelineRunId:run,timelinePhase:'D1/PERFORMANCE'}),a=v.timeline.phases.find(p=>p.kind==='PERFORMANCE').attempts[0];
 assert.deepEqual(Array.from(v.runLabels,x=>x.runNumber).sort(),[1,2]);assert.ok(v.runLabels.every(x=>x.roomSession===roomSession));assert.equal(a.reactions.CHEER,1);assert.equal(a.settlement.tokens,21);assert.equal(a.settlement.people[0].audience,7);
});
test('player and object use separate world points even on different floors',()=>{
 const rows=core.spatialReplay([event('CLUE_INSPECTED',1000,{}, {objectPosition:{x:9,y:-5,z:10},objectFloorY:-5,entityId:'clue'})]).points;
 assert.equal(rows.length,2);assert.equal(rows[0].position.x,1);assert.equal(rows[1].position.x,9);assert.equal(rows[1].floorY,-5);assert.equal(rows[1].anchor,true);
});
