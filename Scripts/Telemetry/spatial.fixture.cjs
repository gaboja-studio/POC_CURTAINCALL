// Synthetic local preview only. Never uploaded to the live ingestion endpoint.
module.exports=()=>{
 const events=[],runId='33333333-3333-4333-8333-333333333333';let sequence=0;
 function add(type,time,slot,payload={},scene='WAITINGROOM'){
  events.push({eventId:'spatial-preview-'+(++sequence),occurredAt:new Date(Date.UTC(2026,10,5)+time).toISOString(),appSessionId:'spatial-preview-'+slot,sequence,monotonicMs:time,environment:'TEST',gameVersion:'0.2.0',runId,roomId:'c'.repeat(32),playerId:'spatial-owner-'+slot,playerNumber:slot,networkClientId:String(slot),sceneType:scene,attemptId:scene==='MAINSTAGE'?'44444444-4444-4444-8444-444444444444':null,performanceId:'D1P1',breakId:scene==='WAITINGROOM'?'D1T2':null,authority:slot===0?'HOST':'LOCAL',eventType:type,payload:{spatialVersion:1,spatialScope:scene,clockSource:'SERVER',clockId:'preview-room',timelineMs:time,floorY:0,entityKind:'PLAYER',...payload}});
 }
 add('RUN_STARTED',0,0,{playerCount:4,roomSession:'c'.repeat(32),runNumber:2});
 for(const scene of ['WAITINGROOM','MAINSTAGE','EXITROUTE']){
  const offset=scene==='WAITINGROOM'?0:scene==='MAINSTAGE'?120000:240000;
  for(const floor of (scene==='WAITINGROOM'?[0,4]:[0])){
   add('SPATIAL_GEOMETRY',offset,0,{entityId:scene+'-floor-'+floor,entityKind:'STRUCTURE',position:{x:10,y:floor,z:8},size:{x:30,y:.2,z:24},floorY:floor},scene);
   for(let i=0;i<3;i++)add('SPATIAL_GEOMETRY',offset,0,{entityId:scene+'-wall-'+floor+'-'+i,entityKind:'STRUCTURE',position:{x:i*10,y:floor,z:6},size:{x:.3,y:3,z:14},floorY:floor},scene);
  }
  if(scene==='MAINSTAGE')add('PERFORMANCE_STARTED',offset,0,{trickType:'CANNON',difficulty:1,playerCount:4,startingLostCount:0,retryIndex:1},scene);
  for(let t=0;t<=60000;t+=scene==='MAINSTAGE'?5000:1000){
   for(let slot=0;slot<4;slot++){
    if(slot===2&&t>42000)continue;
    const floor=scene==='WAITINGROOM'&&slot===3&&t>=30000?4:0;
    add('SPATIAL_SAMPLE',offset+t,slot,{position:{x:slot*4+Math.sin(t/14000)*2,y:floor,z:t/3000-4},floorY:floor,reason:'MOVE'},scene);
   }
   if(scene==='WAITINGROOM')add(t===20000?'PURSUIT_STATE':'PURSUIT_SAMPLE',offset+t,0,{entityId:'PU_preview',entityKind:'PURSUER',position:{x:8,y:0,z:t/3000-8},state:t>=20000?'Run':'Walk',behaviour:t>=20000?'Chase':'Patrol',label:'추격자 A'},scene);
   if(t%5000===0)add('WORLD_SAMPLE',offset+t,0,{entityId:'OB_preview',entityKind:'OBSTACLE',position:{x:12+Math.sin(t/5000),y:0,z:7},state:'ACTIVE',label:'이동 장애물'},scene);
  }
  add(scene==='WAITINGROOM'?'PURSUIT_CAPTURED':'SPATIAL_DIED',offset+42000,2,{position:{x:8,y:0,z:10},reason:'CONFIRMED'},scene);
  add(scene==='MAINSTAGE'?'AUDIENCE_REACTION':'CLUE_INSPECTED',offset+10000,1,{position:{x:4,y:0,z:0},reaction:'CHEER',entityId:'IN_preview',targetId:'clue'},scene);
  if(scene==='MAINSTAGE')add('STAGE_SETTLED',offset+60000,0,{teamAudience:10,tokens:25,people:[{slot:0,audience:4,tokens:13},{slot:1,audience:6,tokens:12}]},scene);
  add(scene==='MAINSTAGE'?'AUDIENCE_REACTION':'TRICK_UNLOCKED',offset+25000,0,{position:{x:0,y:0,z:4},reaction:'JEER',entityId:'IN_prop',trickId:'HumanCannon'},scene);
 }
 for(const [state,ms] of [['FOCUSED',8000],['SECRET_FOUND',11500],['CLOSED',12000]])add('CLUE_INSPECTED',ms,1,{targetId:'clue',state,...(state==='CLOSED'?{durationMs:2000}:{})});
 add('CLUE_INSPECTED',100,1,{entityId:'preview-clue',targetId:'clue',state:'AVAILABLE'});
 add('EXPLORATION_INTERACTION',9000,0,{targetId:'clue',accepted:false,opened:false,reason:'REQUIRED_TOOL_MISSING'});
 add('REHEARSAL_STATE',23000,0,{state:'GUIDE_SHOWN',recordKind:'ACTION',trickId:'CANNON',round:1,stage:1,guideId:'preview-guide'});
 const maps=require('../../Assets/Resources/TelemetrySpatialMaps.json').maps;
 const names={WAITINGROOM:'WaitingRoom',MAINSTAGE:'HumanCannon',EXITROUTE:'escape_route'};
 // Actual exported scene coordinates; movement remains clearly labelled synthetic preview data.
 for(const e of events){
   const available=maps.filter(m=>m.scene===names[e.sceneType]);
   const desired=e.sceneType==='WAITINGROOM'&&e.playerNumber===3?(e.payload.timelineMs>=45000?-10:e.payload.timelineMs>=30000?-5:0):0;
   const m=available.find(m=>m.floorY===desired)||available[0];if(!m)continue;
   e.payload.mapId=m.id;e.payload.sceneName=m.scene;e.payload.floorY=m.floorY;
   const room=m.rooms[(e.payload.entityKind==='PURSUER'?0:e.playerNumber)%Math.max(1,m.rooms.length)];
   if(room){if(e.sceneType==='WAITINGROOM'&&e.eventType==='SPATIAL_SAMPLE')e.payload.roomName=room.name;const route=room.route.length>1?room.route:[{x:room.x-room.width*.2,y:m.floorY,z:room.z},{x:room.x+room.width*.2,y:m.floorY,z:room.z}];
     const index=(e.payload.timelineMs%60000)/60000*(route.length-1),a=route[Math.floor(index)],b=route[Math.min(route.length-1,Math.floor(index)+1)],u=index%1;
     e.payload.position={x:a.x+(b.x-a.x)*u,y:m.floorY,z:a.z+(b.z-a.z)*u};
   }else if(e.payload.position){e.payload.position={x:(m.minX+m.maxX)/2+e.playerNumber*.5,y:0,z:m.minZ+(e.payload.timelineMs%60000)/60000*(m.maxZ-m.minZ)};}
 }
 return events.filter(e=>e.eventType!=='SPATIAL_GEOMETRY'&&e.eventType!=='WORLD_SAMPLE');
};
