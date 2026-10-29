// Preview only. These synthetic scenarios are never uploaded to Drive or shipped in dashboard.html.
function fixture(){let i=0;const events=[],runA='11111111-1111-4111-8111-111111111111',runB='22222222-2222-4222-8222-222222222222';
 const add=(eventType,payload={},extra={})=>{const e={eventId:'preview-'+(++i),occurredAt:new Date(Date.UTC(2026,9,21,10,0,i*8)).toISOString(),appSessionId:'preview-app',sequence:i,runId:runA,performanceId:'D1P1',attemptId:null,breakId:null,gameVersion:'0.1.0',environment:'TEST',sceneType:'MAINSTAGE',authority:'HOST',playerId:'player-one',playerNumber:0,eventType,payload,...extra};events.push(e);return e;};
 for(let day=1;day<=3;day++){
  const performanceId='D'+day+'P1',a='rope-'+day,b='cannon-'+day;
  add('PERFORMANCE_STARTED',{trickType:'TIGHTROPE',difficulty:day,retryIndex:1,playerCount:2,startingLostCount:0},{performanceId,attemptId:a});
  for(let sectionId=1;sectionId<=4;sectionId++){
   add('SECTION_ENTERED',{sectionId,lostCount:0,position:{x:-3,y:8,z:[0,25,55,110][sectionId-1]}},{performanceId,attemptId:a,authority:'LOCAL'});
   if(sectionId<4)add('SECTION_ENTERED',{sectionId,lostCount:0,position:{x:3,y:8,z:[0,25,55,110][sectionId-1]}},{performanceId,attemptId:a,playerId:'player-two',playerNumber:1,authority:'LOCAL'});
   if(sectionId===2)add('COOP_ACTIVATED',{trickType:'TIGHTROPE',gimmickType:'HANDHOLD',gimmickId:'IN_preview01',participants:['player-one','player-two'],sectionId,position:{x:3,y:8,z:25}},{performanceId,attemptId:a});
   if(sectionId===3){add('LIMB_SEVERED',{sectionId,part:'LeftArm',afterLostCount:1,obstacleId:'OB_preview01',position:{x:3,y:8,z:55}},{performanceId,attemptId:a,playerId:'player-two',playerNumber:1});add('PLAYER_DIED',{sectionId,cause:'FALL',lostCount:1,obstacleId:'OB_preview01',position:{x:3,y:1,z:58}},{performanceId,attemptId:a,playerId:'player-two',playerNumber:1});}
  }
  add('PERFORMANCE_PROGRESS',{trickType:'TIGHTROPE',difficulty:day,maxDistance:120.5},{performanceId,attemptId:a,authority:'LOCAL'});
  add('PERFORMANCE_SUCCEEDED',{trickType:'TIGHTROPE',difficulty:day,durationMs:145000,survivors:1,arrivals:1},{performanceId,attemptId:a});
  const cannonId='D'+day+'P2';add('PERFORMANCE_STARTED',{trickType:'CANNON',difficulty:day,retryIndex:1,playerCount:2,startingLostCount:0},{performanceId:cannonId,attemptId:b});
  add('CANNON_FIRED',{launchId:'preview-launch',sectionId:1,cannonId:'IN_cannon',direction:{x:0,y:1,z:1},power:30,lostCount:0,position:{x:0,y:1,z:0}},{performanceId:cannonId,attemptId:b});
  add('SECTION_ENTERED',{sectionId:2,lostCount:0,position:{x:0,y:14,z:24}},{performanceId:cannonId,attemptId:b});
  add('COOP_ACTIVATED',{sectionId:2,gimmickType:'TRAMPOLINE',gimmickId:'IN_trampoline',trickType:'CANNON',participants:['player-one','player-two'],position:{x:0,y:7,z:36}},{performanceId:cannonId,attemptId:b});
  add('SECTION_ENTERED',{sectionId:1,lostCount:0,position:{x:3,y:0,z:0}},{performanceId:cannonId,attemptId:b,playerId:'player-two',playerNumber:1});
  add('COOP_ACTIVATED',{sectionId:2,gimmickType:'LIFT',gimmickId:'IN_lift',participants:['player-two'],position:{x:3,y:7,z:60}},{performanceId:cannonId,attemptId:b,playerId:'player-two',playerNumber:1});
  add('PLAYER_DIED',{sectionId:3,cause:'FIRE',lostCount:0,position:{x:2,y:7,z:68},obstacleId:'OB_fire'},{performanceId:cannonId,attemptId:b});
  add('PERFORMANCE_FAILED',{trickType:'CANNON',difficulty:day,durationMs:95000,reason:'ALL_DEAD'},{performanceId:cannonId,attemptId:b});
  add('ZONE_ENTERED',{zoneType:'BEDROOM',spawnToZoneMs:day*1000},{sceneType:'WAITINGROOM',breakId:'D'+day+'T3',performanceId,authority:'LOCAL'});add('ZONE_EXITED',{zoneType:'BEDROOM',durationMs:56000},{sceneType:'WAITINGROOM',breakId:'D'+day+'T3',performanceId,authority:'LOCAL'});
  add('STAGE_ENTRANCE_REACHED',{readyToEntranceMs:35000},{sceneType:'WAITINGROOM',breakId:'D'+day+'T1',performanceId,authority:'LOCAL'});
  add('ALL_PLAYERS_ASSEMBLED',{firstLeftToAssemblyMs:45000,alarmToAssemblyMs:15000},{sceneType:'WAITINGROOM',breakId:'D'+day+'T1',performanceId});
  add('ITEM_COLLECTED',{itemType:'tool-a',quantity:1},{sceneType:'WAITINGROOM',breakId:'D'+day+'T3',performanceId,authority:'LOCAL'});add('SERVICE_RESERVED',{serviceType:'RESTORE_BODY',lostCount:1,targetCount:1,amount:20},{sceneType:'WAITINGROOM',breakId:'D'+day+'T3',performanceId,authority:'LOCAL'});
  add('FUNDS_CHANGED',{transactionId:'a:'+day,gameDay:day,balanceBefore:day===1?0:day*100,balanceAfter:day*160,category:'REWARD'},{performanceId,breakId:'D'+day+'T3',sceneType:'WAITINGROOM'});
  add('FUNDS_CHANGED',{transactionId:'b:'+day,gameDay:day,balanceBefore:day===1?0:day*90,balanceAfter:day*120,category:'REWARD'},{runId:runB,performanceId,breakId:'D'+day+'T3',sceneType:'WAITINGROOM'});
 }
 return events;
}
module.exports=fixture;
