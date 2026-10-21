const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm');
const ctx=vm.createContext({});vm.runInContext(fs.readFileSync(__dirname+'/core.gs','utf8'),ctx);const core=ctx.TelemetryCore;
let seq=0;function e(type,payload={},extra={}){return {eventId:'e'+(++seq),occurredAt:new Date(Date.UTC(2026,9,21,0,0,seq)).toISOString(),sequence:seq,appSessionId:'app',environment:'TEST',runId:'run-a',attemptId:null,breakId:null,performanceId:'D1P1',playerId:'p1',playerNumber:0,authority:'HOST',sceneType:'MAINSTAGE',eventType:type,payload,...extra};}
function fund(run,day,before,after,num){return e('FUNDS_CHANGED',{transactionId:'ledger:'+num,gameDay:day,balanceBefore:before,balanceAfter:after,category:after>=before?'REWARD':'EXPENSE'},{runId:run,performanceId:'D'+day+'P1'});}
const view=(rows,f={})=>core.dashboardView(rows,{environment:'TEST',...f});
test('average balance averages runs after deduplicating ledger, and carries only observed balances',()=>{
 const f=fund('run-a',1,0,100,1),rows=[f,{...f,eventId:'replica'},fund('run-b',1,0,200,1),e('ZONE_ENTERED',{zoneType:'BEDROOM'},{performanceId:'D2P1'})];
 const d=view(rows);assert.equal(d.funds.series[0].balance,150);assert.equal(d.funds.series[0].samples,2);assert.equal(d.funds.series[1].balance,100);assert.equal(d.funds.series[1].samples,1);
 assert.equal(view(rows,{fundsRunId:'run-b'}).funds.series.length,1);assert.equal(view(rows,{fundsRunId:'run-b'}).funds.series[0].balance,200);
 assert.equal(view([e('RUN_STARTED')]).funds.series.length,0);
});
test('balance uses ledger ordering rather than skewed client wall clocks',()=>{const a=fund('run-a',1,0,100,1),b=fund('run-a',1,100,70,2);b.occurredAt='2026-10-20T00:00:00Z';assert.equal(view([a,b]).funds.series[0].balance,70);});
test('same-day two tricks are grouped together and break metadata takes precedence over stale attempt',()=>{
 const a=e('PERFORMANCE_STARTED',{trickType:'TIGHTROPE'},{attemptId:'a'}),b=e('PERFORMANCE_STARTED',{trickType:'HUMANCONNON'},{attemptId:'b',performanceId:'D1P2'}),rest=e('SERVICE_RESERVED',{}, {attemptId:'b',breakId:'D1T3',sceneType:'WAITINGROOM'});
 const d=view([a,b,rest]);assert.equal(d.timeline.phases.length,2);assert.equal(d.timeline.phases[0].attempts.length,2);assert.equal(d.timeline.phases[0].attempts[1].trickType,'CANNON');assert.equal(d.timeline.phases[1].kind,'BREAK');
});
test('selected run, day phase and attempt cannot bleed events across sessions',()=>{const rows=[e('PERFORMANCE_STARTED',{trickType:'CANNON'},{attemptId:'a'}),e('SECTION_ENTERED',{sectionId:2},{attemptId:'b',performanceId:'D2P1'}),e('PLAYER_DIED',{sectionId:1,cause:'FALL'},{runId:'run-b',attemptId:'c'})];const d=view(rows,{timelineRunId:'run-a',timelinePhase:'D2/PERFORMANCE',timelineAttemptId:'b'});assert.equal(d.timeline.total,1);assert.equal(d.timeline.events[0].eventType,'SECTION_ENTERED');assert.equal(d.timeline.markers[0].payload.sectionId,2);});
test('missing position remains missing; unknown section markers retain the source event',()=>{const x=e('PLAYER_DIED',{sectionId:null,position:{x:2,y:3,z:4},cause:'UNKNOWN'},{attemptId:'a'});const d=view([x]);assert.equal(d.timeline.markers[0].payload.sectionId,null);assert.equal(d.timeline.phases[0].attempts[0].deaths[0].cause,'UNKNOWN');});
test('host outcome is final and observed copies do not inflate death summaries',()=>{const rows=[e('PERFORMANCE_FAILED',{trickType:'CANNON',reason:'TIMEOUT'},{attemptId:'a'}),e('PERFORMANCE_SUCCEEDED',{trickType:'CANNON'},{attemptId:'a'}),e('PLAYER_DIED',{cause:'FALL'},{attemptId:'a'}),e('PLAYER_DIED',{cause:'FALL'},{attemptId:'a',authority:'OBSERVED'})];const a=view(rows).timeline.phases[0].attempts[0];assert.equal(a.outcome,'FAILURE');assert.equal(a.reason,'TIMEOUT');assert.equal(a.deaths.length,1);});
test('pagination and marker caps are explicit and deterministic',()=>{const rows=Array.from({length:450},()=>e('SECTION_ENTERED',{sectionId:1},{attemptId:'a'})),d=view(rows,{timelineOffset:200});assert.equal(d.timeline.events.length,200);assert.equal(d.timeline.markers.length,400);assert.equal(d.timeline.markerTotal,450);assert.equal(d.timeline.total,450);assert.equal(view(rows,{timelineOffset:9999}).timeline.offset,0);});
test('LIVE excludes every TEST event including run options and balances',()=>{const d=core.dashboardView([fund('run-a',1,0,100,1)],{environment:'LIVE'});assert.equal(d.runOptions.length,0);assert.equal(d.funds.series.length,0);assert.equal(d.timeline.events.length,0);});
test('dashboard inline code parses and user data never uses HTML interpolation',()=>{const html=fs.readFileSync(__dirname+'/dashboard.html','utf8'),js=html.match(/<script>([\s\S]*?)<\/script>/)[1];new vm.Script(js);assert.equal(/innerHTML|insertAdjacentHTML/.test(js),false);for(const text of ['총 플레이','공연 플레이','공연 평균 성공률','묘기 통계','대기실 통계','런 통계','난이도 별 승률','상세 데이터'])assert.ok(html.includes(text));});
test('stage event details show measured zero, match facility/medical and hide missing/invalid times',()=>{
 const html=fs.readFileSync(__dirname+'/dashboard.html','utf8'),stage=vm.createContext({data:{stageTimings:core.aggregate([],'TEST').stageTimings},duration:v=>v+'초'});
 vm.runInContext(html.split('// STAGE_UI_BEGIN')[1].split('// STAGE_UI_END')[0],stage);
 assert.deepEqual([...stage.stageEventLines({eventType:'SERVICE_RESERVED',payload:{serviceType:'FACILITY',screenToReservationMs:0}})],['시설 화면 → 예약 0초']);
 assert.equal(stage.stageEventLines({eventType:'READY_CHANGED',payload:{ready:false,screenToReadyMs:1000}}).length,0);
 for(const value of [null,undefined,NaN,-1,'1000'])assert.equal(stage.stageEventLines({eventType:'MEDICAL_RETURNED',payload:{restorationToReturnMs:value}}).length,0);
 assert.ok(html.includes('평균 소요 시간'));assert.ok(html.includes('소요 시간 분류'));
});
const routeCtx=vm.createContext({});vm.runInContext(fs.readFileSync(__dirname+'/dashboard.html','utf8').split('// ROUTE_MODEL_BEGIN')[1].split('// ROUTE_MODEL_END')[0],routeCtx);
const point=(z,x=0,y=0,extra={})=>e('SECTION_ENTERED',{position:{x,y,z},sectionId:1},{...extra});
test('route uses actual finite positions; section-only and null positions remain unlocated',()=>{
 const valid=point(10),missing=e('SECTION_ENTERED',{sectionId:4}),bad=e('PLAYER_DIED',{position:{x:0,y:NaN,z:20}}),d=routeCtx.routeGeometry([valid,missing,bad],'TIGHTROPE');
 assert.equal(d.points.length,1);assert.equal(d.unknown.length,2);assert.equal(d.points[0].event,valid);assert.equal(d.points[0].u,10);
});
test('rope maps forward Z left-to-right and sideways X vertically; cannon uses height Y',()=>{
 const rows=[point(0,-2,3),point(100,2,3)],rope=routeCtx.routeGeometry(rows,'TIGHTROPE'),cannon=routeCtx.routeGeometry(rows,'CANNON');
 assert.ok(rope.points[0].x<rope.points[1].x);assert.ok(rope.points[0].y>rope.points[1].y);assert.equal(cannon.points[0].y,cannon.points[1].y);
 const flying=routeCtx.routeGeometry([point(0,0,0),point(100,0,20)],'CANNON');assert.ok(flying.points[1].y<flying.points[0].y);
});
test('player color follows slot across filters and event categories',()=>{
 const one=point(10,0,0,{playerNumber:2,playerId:'three'}),other=point(20),a=routeCtx.routeGeometry([one,other],'TIGHTROPE'),b=routeCtx.routeGeometry([one],'CANNON');
 assert.equal(a.points[0].color,b.points[0].color);assert.notEqual(a.points[0].color,a.points[1].color);
 assert.equal(routeCtx.routeKind(e('LIMB_SEVERED')),'INJURY');assert.equal(routeCtx.routeKind(e('TIGHTROPE_FELL')),'DEATH');
 assert.equal(routeCtx.routeKind(e('COOP_ACTIVATED',{gimmickType:'HANDHOLD'})),'TEAM');assert.equal(routeCtx.routeKind(e('COOP_ACTIVATED',{gimmickType:'TRAMPOLINE'})),'OBJECT');assert.equal(routeCtx.routeKind(e('CANNON_FIRED')),'OBJECT');
 assert.equal(routeCtx.routeKind(e('JUMP_RESULT',{landed:false})),'DEATH');assert.equal(routeCtx.routeKind(e('SECTION_ENTERED')),'MOVE');
});
test('per-player lines follow time order, break across missing positions and never cross players',()=>{
 const a=point(10),b=point(20),gap=e('JUMP_RESULT'),c=point(30),other=point(50,0,0,{playerId:'p2',playerNumber:1});
 const d=routeCtx.routeGeometry([c,other,b,gap,a],'TIGHTROPE'),actor=d.actors.find(x=>x.key===routeCtx.routePlayerKey(a));
 assert.equal(actor.segments.length,2);assert.deepEqual(Array.from(actor.segments[0],p=>p.event.eventId),[a.eventId,b.eventId]);assert.equal(actor.segments[1][0].event.eventId,c.eventId);
 assert.equal(d.actors.find(x=>x.key===routeCtx.routePlayerKey(other)).segments[0].length,1);
});
test('coincident event icons spread without modifying recorded coordinates or path anchors',()=>{
 const rows=Array.from({length:25},()=>point(12)),before=JSON.stringify(rows),d=routeCtx.routeGeometry(rows,'TIGHTROPE');
 assert.equal(new Set(d.points.map(p=>p.drawX+':'+p.drawY)).size,25);assert.equal(new Set(d.points.map(p=>p.x+':'+p.y)).size,1);assert.equal(JSON.stringify(rows),before);
 assert.ok(d.points.every(p=>Number.isFinite(p.drawX)&&Number.isFinite(p.drawY)));
});
test('long routes grow the canvas for horizontal scrolling with bounded size',()=>{
 const d=routeCtx.routeGeometry(Array.from({length:400},(_,i)=>point(i)),'CANNON');assert.equal(d.width,4800);assert.equal(routeCtx.routeGeometry([],'CANNON').width,1120);
});
test('new start, injury and interaction markers retain coordinate provenance',()=>{
 const rows=[e('PERFORMANCE_STARTED',{trickType:'CANNON',position:{x:0,y:0,z:1},positionSource:'EMIT_OBSERVATION'},{attemptId:'a'}),e('INJURY_CHANGED',{position:{x:1,y:2,z:3}},{attemptId:'a'}),e('INTERACTION_FAILED',{reason:'BLOCKED'},{attemptId:'a'})];
 const d=view(rows);assert.equal(d.timeline.markers.length,3);assert.equal(d.timeline.markers[0].payload.positionSource,'EMIT_OBSERVATION');
});

test('same login separates slots, and relayed copies retain player identity',()=>{const a=point(10,0,0,{playerId:'shared',playerNumber:0}),b=point(20,0,0,{playerId:'shared',playerNumber:1}),c=point(30,0,0,{playerId:'changed',playerNumber:1,appSessionId:'other'});const d=routeCtx.routeGeometry([a,b,c],'TIGHTROPE');assert.equal(d.actors.length,2);assert.equal(d.actors.find(x=>x.key===routeCtx.routePlayerKey(b)).segments[0].length,2);});
test('zoom changes spacing without changing source coordinates',()=>{const rows=[point(0),point(100)],a=routeCtx.routeGeometry(rows,'TIGHTROPE',1),b=routeCtx.routeGeometry(rows,'TIGHTROPE',3);assert.ok(b.points[1].x-b.points[0].x>a.points[1].x-a.points[0].x);assert.equal(a.points[1].u,b.points[1].u);});
test('input noise and observed replicas stay out of analysis; raw totals preserved',()=>{const rows=[e('INPUT_KEY'),e('BUTTON_INPUT'),e('MICROPHONE_INPUT'),e('PLAYER_DIED',{cause:'FALL'},{attemptId:'a'}),e('PLAYER_DIED',{cause:'FALL'},{attemptId:'a',authority:'OBSERVED'})];assert.equal(view(rows).timeline.total,1);assert.equal(core.aggregate(rows,'TEST').eventCount,5);});
test('shared login yields two observed waiting users',()=>{const a=e('ZONE_ENTERED',{zoneType:'BEDROOM'},{sceneType:'WAITINGROOM',authority:'LOCAL'}),b={...a,eventId:'other',playerNumber:1};assert.equal(core.aggregate([a,b],'TEST').zones[0].distinctPlayers,2);});
test('search and native selection are separate',()=>{const html=fs.readFileSync(__dirname+'/dashboard.html','utf8');assert.ok(html.includes('<select id="timeline-run"'));assert.ok(html.includes('<select id="fund-run"'));assert.ok(html.includes('id="timeline-search"'));assert.equal(html.includes('list="run-options"'),false);});

test('break balances carry observed income, keep periods separate and match selected-run averages',()=>{
 const rows=[],rest=(run,day,part)=>rows.push(e('ZONE_ENTERED',{zoneType:'WAITINGROOM'},{runId:run,performanceId:'D'+day+'P1',breakId:'D'+day+'T'+part,sceneType:'WAITINGROOM'}));
 rows.push(fund('run-a',1,0,100,1));rest('run-a',1,2);
 rows.push({...fund('run-a',1,100,70,2),breakId:'D1T2'});rest('run-a',1,3);
 rows.push({...fund('run-a',1,70,50,3),breakId:'D1T3'});rest('run-a',2,1);
 rows.push(fund('run-b',1,0,200,1));rest('run-b',1,2);
 const f=view(rows,{fundsInterval:'BREAK'}).funds;
 assert.deepEqual(Array.from(f.series,p=>[p.day,p.rest,p.balance,p.samples]),[[1,2,135,2],[1,3,50,1],[2,1,50,1]]);
 assert.equal(f.breakRunSeries.find(p=>p.day===2).carried,true);assert.equal(f.series.some(p=>p.day===1&&p.rest===1),false);
 const single=view(rows,{fundsInterval:'BREAK',fundsRunId:'run-a'}).funds;assert.equal(single.series[0].balance,70);assert.equal(single.series[0].rewards,100);assert.equal(single.series[0].spent,30);
 assert.equal(view([fund('run-a',1,0,100,1)],{fundsInterval:'BREAK'}).funds.series.length,0);
});
test('calendar uses Seoul start date, applies server-side before averages and allows clearing',()=>{
 const a=fund('run-a',1,0,100,1),b=fund('run-b',1,0,200,1);a.occurredAt='2026-10-21T15:01:00Z';b.occurredAt='2026-10-21T14:59:00Z';
 const d=view([a,b],{fundsDate:'2026-10-22',timelineDate:'2026-10-22',timelineRunId:'run-b'});assert.equal(d.funds.series[0].balance,100);assert.equal(d.timeline.runId,'run-a');
 assert.equal(view([a,b],{fundsDate:'2026-10-23'}).funds.series.length,0);assert.equal(view([a,b],{fundsDate:''}).funds.series[0].balance,150);
 const html=fs.readFileSync(__dirname+'/dashboard.html','utf8'),c=vm.createContext({});vm.runInContext(html.split('// SESSION_MODEL_BEGIN')[1].split('// SESSION_MODEL_END')[0],c);
 assert.equal(c.sessionDate(a.occurredAt),'2026-10-22');assert.equal(c.matchingSessions(d.runLabels,'2026-10-22','run-b').length,0);assert.equal(c.matchingSessions(d.runLabels,'','RUN-B')[0].id,'run-b');
});

test('local end records resolve only an unambiguous slot in the same app and run',()=>{const a=e('ZONE_ENTERED',{zoneType:'WAITINGROOM'},{sceneType:'WAITINGROOM',authority:'LOCAL'}),b={...a,eventId:'other',playerNumber:1,appSessionId:'app-b'},end={...a,eventId:'end',eventType:'ZONE_EXITED',playerNumber:'',networkClientId:'',payload:{zoneType:'WAITINGROOM',durationMs:10}},end2={...end,eventId:'end2',appSessionId:'app-b'};assert.equal(core.aggregate([a,b,end,end2],'TEST').zones[0].distinctPlayers,2);assert.equal(end.playerNumber,'');const ambiguous={...b,appSessionId:'app'};assert.equal(core.aggregate([a,ambiguous,end],'TEST').zones[0].distinctPlayers,3);});
