/* Shared Apps Script/Node logic. No Google services and no credentials. */
var TelemetryCore = (function () {
  'use strict';
  var uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
  var scenes = ['TITLE','LOBBY','WAITINGROOM','MAINSTAGE','BACKSTAGE','EXITROUTE','RESULTS','UNKNOWN'];
  // Required payload fields. Additional bounded JSON fields support later domain adapters.
  var catalog = {
    GAME_STARTED:{}, GAME_EXITED:{reason:'text'}, PLAYER_EXITED:{reason:'text'},
    SCENE_ENTERED:{sceneName:'text'}, SCENE_EXITED:{sceneName:'text',durationMs:'duration'},
    INPUT_KEY:{keyId:'text',inputMethod:'text'}, BUTTON_INPUT:{buttonId:'text'},
    INJURY_CHANGED:{beforeLostCount:'limbs',afterLostCount:'limbs'},
    MICROPHONE_INPUT:{startedAt:'utc',endedAt:'utc'}, TITLE_ENTERED:{},
    ROOM_CREATED:{roomType:'roomType'}, ROOM_ENTERED:{roomType:'roomType',isHost:'bool'},
    SIGNATURE_CHANGED:{signed:'bool'}, RUN_STARTED:{playerCount:'players'}, RUN_ENDED:{reason:'text'},
    PERFORMANCE_STARTED:{trickType:'trick',difficulty:'difficulty',retryIndex:'positive',playerCount:'players',startingLostCount:'limbs'},
    PERFORMANCE_SUCCEEDED:{trickType:'trick',difficulty:'difficulty',durationMs:'duration',survivors:'count',arrivals:'count'},
    PERFORMANCE_FAILED:{trickType:'trick',difficulty:'difficulty',durationMs:'duration',reason:'text'},
    PERFORMANCE_PROGRESS:{trickType:'trick',difficulty:'difficulty',maxDistance:'duration'},
    SECTION_ENTERED:{sectionId:'positive',lostCount:'limbs'},
    SECTION_EXITED:{sectionId:'positive',durationMs:'duration',reason:'text'},
    PLAYER_DIED:{sectionId:'optionalPositive',position:'position',cause:'text',lostCount:'limbs'},
    LIMB_SEVERED:{sectionId:'optionalPositive',position:'position',part:'text',afterLostCount:'limbs'},
    PERFORMANCE_LEFT:{trickType:'trick',sectionId:'optionalPositive',reason:'text'},
    TIGHTROPE_FELL:{sectionId:'positive',ropeNumber:'positive',distance:'duration',lostCount:'limbs',handhold:'optionalBool',piggyback:'bool'},
    JUMP_RESULT:{sectionId:'positive',jumpType:'text',originRope:'positive',targetRope:'positive',landed:'bool'},
    COOP_ACTIVATED:{trickType:'trick',gimmickType:'text',gimmickId:'text',participants:'ids'},
    CANNON_FIRED:{launchId:'uuid',sectionId:'optionalPositive',cannonId:'text',direction:'position',power:'duration',lostCount:'limbs'},
    CANNON_FLIGHT_ENDED:{launchId:'uuid',sectionId:'optionalPositive',position:'position',reachedTarget:'optionalBool',reason:'text'},
    CANNON_HAZARD_CONTACT:{sectionId:'optionalPositive',position:'position',obstacleId:'text',hazardType:'text',cutApplied:'bool',part:'text',lostCount:'limbs',reason:'text'},
    ZONE_ENTERED:{zoneType:'text'}, ZONE_EXITED:{zoneType:'text',durationMs:'duration',reason:'text'},
    SCHEDULE_OPENED:{flowId:'uuid'}, SCHEDULE_CONFIRMED:{flowId:'uuid'}, READY_CHANGED:{ready:'bool'},
    STAGE_ENTRANCE_REACHED:{}, FIRST_PLAYER_LEFT:{}, ALL_PLAYERS_ASSEMBLED:{}, RECALL_ALARM:{},
    RECALL_EXPIRED:{position:'position'}, ROUTE_BLOCKED:{reason:'text'}, ROUTE_DETOUR:{reason:'text'},
    BREAK_ENDED:{reason:'text'},
    INTERACTION_FAILED:{interactionId:'text',reason:'text'},
    SHOP_OPENED:{flowId:'uuid'}, PURCHASE_COMPLETED:{flowId:'uuid',itemType:'text',quantity:'positive',balanceBefore:'count',balanceAfter:'count'},
    DELIVERY_DISPATCHED:{flowId:'uuid'}, DELIVERY_AVAILABLE:{flowId:'uuid',quantity:'positive'},
    ITEM_COLLECTED:{flowId:'uuid',itemType:'text',quantity:'positive'}, STORAGE_REACHED:{flowId:'uuid'},
    ITEM_STORED:{flowId:'uuid',itemType:'text',quantity:'positive'},
    PURCHASE_FAILED:{flowId:'uuid',reason:'text'}, COLLECTION_FAILED:{flowId:'uuid',reason:'text'}, STORAGE_FAILED:{flowId:'uuid',reason:'text'},
    ITEM_DROPPED:{flowId:'uuid'}, ITEM_PICKED_UP:{flowId:'uuid'}, ITEM_HANDED_OVER:{flowId:'uuid',toPlayerId:'text'},
    SERVICE_OPENED:{flowId:'uuid',serviceType:'text'},
    SERVICE_RESERVED:{flowId:'uuid',serviceType:'text',lostCount:'limbs',targetCount:'limbs',amount:'count'},
    MEDICAL_ROOM_REACHED:{flowId:'uuid'}, MEDICAL_ENTERED:{flowId:'uuid'},
    RESTORATION_COMPLETED:{flowId:'uuid',beforeLostCount:'limbs',afterLostCount:'limbs'},
    MEDICAL_RETURNED:{flowId:'uuid'}, SERVICE_FAILED:{flowId:'uuid',reason:'text'}, SERVICE_ABANDONED:{flowId:'uuid',reason:'text'},
    RESTORATION_VERIFIED:{flowId:'uuid',lostCount:'limbs',movementRestored:'bool',jumpRestored:'bool',capacityRestored:'bool'},
    FUNDS_CHANGED:{transactionId:'text',gameDay:'positive',balanceBefore:'count',balanceAfter:'count',category:'fundCategory'},
    TELEMETRY_GAP:{reason:'text',droppedEvents:'positive'}
  };
  function check(ok, message) { if (!ok) throw new Error(message); }
  function object(v) { return v !== null && typeof v === 'object' && !Array.isArray(v); }
  function text(v) { return typeof v === 'string' && v.length > 0 && v.length <= 128; }
  function integer(v,min,max) { return Number.isSafeInteger(v) && v >= min && v <= max; }
  function value(v,type) {
    if(type==='optionalPositive') return v===null||integer(v,1,Number.MAX_SAFE_INTEGER);
    if(type==='optionalBool') return v===null||typeof v==='boolean';
    if(type==='text') return text(v);
    if(type==='uuid') return typeof v==='string' && uuid.test(v);
    if(type==='utc') return typeof v==='string' && /^\d{4}-\d\d-\d\dT.*Z$/.test(v) && Number.isFinite(Date.parse(v));
    if(type==='bool') return typeof v==='boolean';
    if(type==='position') return object(v) && ['x','y','z'].every(function(k){return typeof v[k]==='number' && Number.isFinite(v[k]);});
    if(type==='ids') return Array.isArray(v) && v.length>=1 && v.length<=4 && v.every(text) && new Set(v).size===v.length;
    if(type==='duration') return typeof v==='number' && Number.isFinite(v) && v>=0;
    if(type==='roomType') return v==='CODE'||v==='LAN';
    if(type==='trick') return v==='TIGHTROPE'||v==='CANNON';
    if(type==='fundCategory') return ['REWARD','PURCHASE','SERVICE','EXPENSE','OTHER','RESET'].indexOf(v)>=0;
    if(type==='limbs') return integer(v,0,4);
    if(type==='players') return integer(v,1,4);
    if(type==='difficulty') return integer(v,1,3);
    return integer(v,type==='positive'?1:0,Number.MAX_SAFE_INTEGER);
  }
  function validate(batch) {
    check(object(batch) && batch.schemaVersion===1,'SCHEMA_VERSION');
    ['batchId','senderId','appSessionId'].forEach(function(k){check(value(batch[k],'uuid'),'INVALID_'+k);});
    check(['SCENE_CHANGED','RUN_ENDED','PLAYER_EXITED','GAME_EXITED','CHECKPOINT','RECOVERY'].indexOf(batch.flushReason)>=0,'FLUSH_REASON');
    check(Array.isArray(batch.events)&&batch.events.length>0&&batch.events.length<=250,'BATCH_EVENT_LIMIT');
    var seen=new Set(); var previous=0;
    batch.events.forEach(function(e){
      check(object(e)&&value(e.eventId,'uuid')&&!seen.has(e.eventId),'EVENT_ID'); seen.add(e.eventId);
      check(integer(e.sequence,1,Number.MAX_SAFE_INTEGER)&&e.sequence>previous,'SEQUENCE');previous=e.sequence;
      check(value(e.occurredAt,'utc')&&value(e.monotonicMs,'duration'),'EVENT_TIME');
      check(e.environment==='LIVE'||e.environment==='TEST','ENVIRONMENT');
      check(e.gameVersion==null||typeof e.gameVersion==='string'&&/^[0-9]+\.[0-9]+\.[0-9]+(?:[-+][0-9A-Za-z.-]+)?$/.test(e.gameVersion)&&e.gameVersion.length<=64,'GAME_VERSION');
      check(text(e.playerId),'PLAYER_ID');
      check(e.playerNumber==null||integer(e.playerNumber,0,3),'PLAYER_NUMBER');
      check(e.networkClientId==null||/^\d{1,20}$/.test(e.networkClientId),'NETWORK_CLIENT_ID');
      check(e.roomId==null||/^[0-9a-f]{32}$/.test(e.roomId),'ROOM_ID');
      ['runId','attemptId'].forEach(function(k){check(e[k]==null||value(e[k],'uuid'),'INVALID_'+k);});
      check(e.performanceId==null||/^D[1-9]\d*P[1-9]\d*$/.test(e.performanceId),'PERFORMANCE_ID');
      check(e.breakId==null||/^D[1-9]\d*T[1-9]\d*$/.test(e.breakId),'BREAK_ID');
      check(scenes.indexOf(e.sceneType)>=0,'SCENE_TYPE');
      check(['LOCAL','HOST','OBSERVED'].indexOf(e.authority)>=0,'AUTHORITY');
      check(Object.prototype.hasOwnProperty.call(catalog,e.eventType)&&object(e.payload),'EVENT_TYPE');
      check(JSON.stringify(e.payload).length<=8192,'PAYLOAD_SIZE');
      Object.keys(catalog[e.eventType]).forEach(function(k){check(value(e.payload[k],catalog[e.eventType][k]),'PAYLOAD_'+e.eventType+'_'+k);});
      if (/^(PERFORMANCE_|SECTION_|PLAYER_DIED|LIMB_SEVERED|TIGHTROPE_|JUMP_|COOP_|CANNON_)/.test(e.eventType))
        check(value(e.runId,'uuid')&&value(e.attemptId,'uuid'),'PERFORMANCE_CONTEXT');
      if(e.eventType==='MICROPHONE_INPUT')check(Date.parse(e.payload.endedAt)>=Date.parse(e.payload.startedAt),'MICROPHONE_TIME');
    });
    return batch;
  }
  var headers=['eventId','occurredAt','appSessionId','senderId','sequence','monotonicMs','environment','playerId','playerNumber','networkClientId','roomId','runId','performanceId','attemptId','breakId','sceneType','eventType','authority','payloadJson','batchId','receivedAt','gameVersion'];
  function row(e,b,receivedAt){return headers.map(function(k){
    if(k==='payloadJson')return JSON.stringify(e.payload);
    if(k==='receivedAt')return receivedAt;
    if(k==='batchId'||k==='senderId'||k==='appSessionId')return b[k];
    return e[k]==null?'':e[k];
  });}
  function decode(r){var e={};headers.forEach(function(k,i){e[k]=r[i];});e.payload=JSON.parse(e.payloadJson);return e;}
  function ordered(events){return events.slice().sort(function(a,b){return a.occurredAt.localeCompare(b.occurredAt)||a.appSessionId.localeCompare(b.appSessionId)||a.sequence-b.sequence;});}
  // Missing versions remain distinct from new releases; never relabel historical/outbox data.
  function versionOf(e){return e.gameVersion||'UNVERSIONED';}
  function versionSelection(events,environment,requested){
    var options=Array.from(new Set(['0.1.0'].concat(events.filter(function(e){return e.environment===(environment||'LIVE');}).map(versionOf)))).sort(function(a,b){
      if(a==='UNVERSIONED')return 1;if(b==='UNVERSIONED')return -1;
      var x=a.split(/[.+-]/),y=b.split(/[.+-]/);for(var i=0;i<3;i++){var d=Number(y[i])-Number(x[i]);if(d)return d;}return b.localeCompare(a);
    });
    if(requested&&options.indexOf(requested)<0)options.push(requested);
    return {selected:requested||options[0],options:options};
  }
  function resolveParticipants(events){
    var slots=new Map();
    function scope(e){return JSON.stringify([e.appSessionId,e.roomId,e.runId]);}
    events.forEach(function(e){if(e.authority!=='LOCAL'||!Number.isInteger(e.playerNumber))return;var key=scope(e);if(!slots.has(key))slots.set(key,new Set());slots.get(key).add(e.playerNumber);});
    return events.map(function(e){var known=slots.get(scope(e));return !Number.isInteger(e.playerNumber)&&e.authority!=='HOST'&&known&&known.size===1?Object.assign({},e,{playerNumber:Array.from(known)[0],playerNumberSource:'SAME_APP_RUN_LOCAL_SLOT'}):e;});
  }
  function playerKey(e){var scope=e.runId||e.roomId||e.appSessionId||'';return scope+':'+(Number.isInteger(e.playerNumber)?'slot:'+e.playerNumber:e.networkClientId!=null&&e.networkClientId!==''?'client:'+e.networkClientId:'app:'+e.appSessionId+':id:'+e.playerId);}
  function analysisEvent(e){return !/^(INPUT_KEY|BUTTON_INPUT|MICROPHONE_INPUT|SCENE_EXITED|TELEMETRY_GAP|PERFORMANCE_PROGRESS)$/.test(e.eventType)&&e.authority!=='OBSERVED';}
  function aggregate(events,environment,runId,gameVersion){
    var list=ordered(resolveParticipants(events.filter(function(e){return e.environment===(environment||'LIVE')&&(!runId||e.runId===runId)&&(!gameVersion||versionOf(e)===gameVersion);})));
    var attempts=Object.create(null),tricks=Object.create(null),death=Object.create(null),zones=Object.create(null),usage=Object.create(null),funds=Object.create(null),transactions=new Set(),eligible=new Set(),facilityReservations=new Set();
    function group(e){var p=e.payload;var k=p.trickType+' / '+p.difficulty;return tricks[k]||(tricks[k]={trickType:p.trickType,difficulty:p.difficulty,attemptIds:new Set(),outcomes:Object.create(null),distances:Object.create(null),deaths:0,interactions:0});}
    list.forEach(function(e){
      var p=e.payload;var person=playerKey(e);
      if(e.sceneType==='WAITINGROOM')eligible.add(person);
      if(/^PERFORMANCE_(STARTED|SUCCEEDED|FAILED)$/.test(e.eventType)&&e.authority==='HOST'){
        var g=group(e);g.attemptIds.add(e.attemptId);
        attempts[e.attemptId]=g;
        if(e.eventType!=='PERFORMANCE_STARTED'&&!Object.prototype.hasOwnProperty.call(g.outcomes,e.attemptId))g.outcomes[e.attemptId]=e.eventType==='PERFORMANCE_SUCCEEDED';
      }
    });
    list.forEach(function(e){
      var p=e.payload;var person=playerKey(e);var g=attempts[e.attemptId];
      if(e.eventType==='PERFORMANCE_PROGRESS'&&e.authority==='LOCAL'){
        g=group(e);var dk=e.attemptId+':'+playerKey(e);g.distances[dk]=Math.max(g.distances[dk]||0,p.maxDistance);
      }
      if(e.eventType==='PLAYER_DIED'&&e.authority==='HOST'){
        if(g)g.deaths++;
        var dk=(g?g.trickType:'UNKNOWN')+' / '+(g?g.difficulty:'?')+' / '+p.cause;
        death[dk]=(death[dk]||0)+1;
      }
      if(e.eventType==='COOP_ACTIVATED'&&e.authority==='HOST'&&g)g.interactions++;
      if(e.eventType==='ZONE_ENTERED'||e.eventType==='ZONE_EXITED'){
        if(e.authority!=='LOCAL')return;
        var z=zones[p.zoneType]||(zones[p.zoneType]={zoneType:p.zoneType,visits:0,people:new Set(),durations:[]});
        z.people.add(person);if(e.eventType==='ZONE_ENTERED')z.visits++;else z.durations.push(p.durationMs/1000);
      }
      if(e.eventType==='ITEM_COLLECTED'||e.eventType==='SERVICE_RESERVED'){
        // Facility reservations are emitted once by the deciding host; medical and item use remain local.
        var facility=e.eventType==='SERVICE_RESERVED'&&p.serviceType==='FACILITY';
        if(e.authority!==(facility?'HOST':'LOCAL'))return;
        if(facility){var reservation=e.runId+':'+p.flowId;if(facilityReservations.has(reservation))return;facilityReservations.add(reservation);}
        var kind=e.eventType==='ITEM_COLLECTED'?'ITEM':'SERVICE';var type=p.itemType||p.serviceType;var uk=kind+':'+type;
        var u=usage[uk]||(usage[uk]={kind:kind,type:type,uses:0,people:new Set()});u.uses++;u.people.add(person);
      }
      if(e.eventType==='FUNDS_CHANGED'&&e.authority==='HOST'){
        var tk=e.runId+':'+p.transactionId;if(transactions.has(tk))return;transactions.add(tk);
        var fk=e.runId+':'+p.gameDay;var f=funds[fk]||(funds[fk]={runId:e.runId,gameDay:p.gameDay,rewards:0,spent:0,netChange:0,transactions:0});
        var delta=p.balanceAfter-p.balanceBefore;f.netChange+=delta;f.transactions++;
        if(p.category==='REWARD')f.rewards+=delta;
        if(p.category==='PURCHASE'||p.category==='SERVICE'||p.category==='EXPENSE')f.spent-=delta;
      }
    });
    function avg(a){return a.length?a.reduce(function(s,n){return s+n;},0)/a.length:null;}
    return {eventCount:list.length,runIds:Array.from(new Set(list.map(function(e){return e.runId;}).filter(Boolean))),
      timeline:list,
      tricks:Object.keys(tricks).sort().map(function(k){var g=tricks[k],outcomes=Object.values(g.outcomes),ds=Object.values(g.distances);return {trickType:g.trickType,difficulty:g.difficulty,attempts:g.attemptIds.size,completed:outcomes.length,wins:outcomes.filter(Boolean).length,winRate:outcomes.length?outcomes.filter(Boolean).length/outcomes.length:null,avgMaxDistance:avg(ds),distanceSamples:ds.length,deaths:g.deaths,interactions:g.interactions};}),
      deaths:Object.keys(death).sort().map(function(k){return {label:k,deaths:death[k]};}),
      zones:Object.values(zones).map(function(z){return {zoneType:z.zoneType,visits:z.visits,distinctPlayers:z.people.size,avgDwellSeconds:avg(z.durations),dwellSamples:z.durations.length};}),
      usage:Object.values(usage).map(function(u){var observed=Array.from(u.people).filter(function(p){return eligible.has(p);}).length;return {kind:u.kind,itemOrServiceType:u.type,uses:u.uses,users:u.people.size,eligibleUsers:eligible.size,usageRate:eligible.size?observed/eligible.size:null};}),
      waiting:waitingMetrics(list),stageTimings:stageMetrics(list),operations:operationMetrics(list),outstanding:outstandingMetrics(list),funds:Object.values(funds)};
  }
  function outstandingMetrics(events){
    var pending=new Map(),rows=new Map(),seen=new Set();
    function scope(e){return JSON.stringify([e.roomId||'',e.runId||'']);}
    function finish(key,item,reason){
      var group=JSON.stringify([item.kind,item.type,reason]);
      if(!rows.has(group))rows.set(group,{kind:item.kind,itemType:item.type,reason:reason,quantity:0});
      rows.get(group).quantity+=item.quantity;pending.delete(key);
    }
    events.forEach(function(e){
      if(seen.has(e.eventId))return;seen.add(e.eventId);var p=e.payload,base=scope(e),key=base+':'+p.flowId;
      if(e.breakId&&e.appSessionId&&(e.eventType==='PURCHASE_COMPLETED'&&e.authority==='HOST'
        ||e.eventType==='SERVICE_RESERVED'&&p.serviceType==='RESTORE_BODY'&&e.authority==='LOCAL')){
        if(!pending.has(key))pending.set(key,{kind:e.eventType==='PURCHASE_COMPLETED'?'ITEM':'MEDICAL',type:p.itemType||p.serviceType,
          quantity:p.quantity||1,player:e.playerId,client:e.networkClientId,app:e.appSessionId,scope:base,breakId:e.breakId});
      }
      var item=pending.get(key);
      if(item&&item.app===e.appSessionId&&(item.kind==='ITEM'&&e.eventType==='ITEM_COLLECTED'&&['HOST','LOCAL'].indexOf(e.authority)>=0
        ||item.kind==='MEDICAL'&&e.eventType==='MEDICAL_ENTERED'&&e.authority==='LOCAL')){
        item.quantity-=item.kind==='ITEM'?(p.quantity||1):1;if(item.quantity<=0)pending.delete(key);
      }
      if(item&&item.kind==='MEDICAL'&&item.app===e.appSessionId&&e.eventType==='SERVICE_ABANDONED'&&e.authority==='LOCAL'&&pending.has(key))finish(key,item,'SERVICE_ABANDONED');
      if(e.eventType==='BREAK_ENDED'&&['HOST','LOCAL'].indexOf(e.authority)>=0)
        pending.forEach(function(o,k){if(o.app===e.appSessionId&&o.scope===base&&o.breakId===e.breakId){
          var left=o.kind==='ITEM'&&o.client!=null&&e.authority==='HOST'&&Array.isArray(p.presentNetworkClientIds)&&p.presentNetworkClientIds.indexOf(o.client)<0;
          finish(k,o,left?'PLAYER_EXITED':'BREAK_ENDED');
        }});
      if(['PLAYER_EXITED','GAME_EXITED','RUN_ENDED'].indexOf(e.eventType)>=0&&['HOST','LOCAL'].indexOf(e.authority)>=0)
        pending.forEach(function(o,k){if(o.app===e.appSessionId&&o.scope===base&&(e.eventType!=='PLAYER_EXITED'||o.player===e.playerId))finish(k,o,e.eventType);});
    });
    return Array.from(rows.values());
  }
  function operationMetrics(events){
    var rows=new Map(),seen=new Set();
    events.forEach(function(e){
      var host=/^(PURCHASE_FAILED|COLLECTION_FAILED|STORAGE_FAILED|SERVICE_FAILED|INTERACTION_FAILED)$/.test(e.eventType);
      var local=/^(ITEM_DROPPED|ITEM_PICKED_UP|ITEM_HANDED_OVER|SERVICE_ABANDONED)$/.test(e.eventType);
      if(e.sceneType!=='WAITINGROOM'||!(host&&e.authority==='HOST'||local&&e.authority==='LOCAL')||seen.has(e.eventId))return;
      seen.add(e.eventId);var p=e.payload,key=JSON.stringify([e.eventType,p.operation||'',p.reason||'']);
      if(!rows.has(key))rows.set(key,{eventType:e.eventType,operation:p.operation||null,reason:p.reason||null,count:0});
      rows.get(key).count++;
    });
    return Array.from(rows.values()).sort(function(a,b){return b.count-a.count||a.eventType.localeCompare(b.eventType);});
  }
  // Only emitted durations are aggregated. Never reconstruct them from UTC timestamps.
  var timingStages=[
    ['shopScreen','SHOP','상점 열기 → 구매','PURCHASE_COMPLETED','screenToPurchaseMs','HOST'],
    ['purchaseDispatch','SHOP','구매 → 배송 배출','DELIVERY_DISPATCHED','purchaseToDispatchMs','HOST'],
    ['dispatchAvailable','SHOP','배송 배출 → 수령 가능','DELIVERY_AVAILABLE','dispatchToAvailableMs','HOST'],
    ['purchaseAvailable','SHOP','구매 → 수령 가능','DELIVERY_AVAILABLE','purchaseToAvailableMs','HOST'],
    ['availableCollection','SHOP','수령 가능 → 실제 수령','ITEM_COLLECTED','availableToCollectionMs','COLLECTION'],
    ['collectionStorage','SHOP','수령·획득 → 소품실','STORAGE_REACHED','durationMs','LOCAL'],
    ['arrivalStore','SHOP','소품실 도착 → 보관','ITEM_STORED','storageArrivalToStoreMs','LOCAL'],
    ['scheduleConfirm','SCHEDULE','스케줄 열기 → 편성 확정','SCHEDULE_CONFIRMED','screenToConfirmMs','LOCAL'],
    ['scheduleReady','SCHEDULE','스케줄 열기 → 준비 완료','READY_CHANGED','screenToReadyMs','LOCAL'],
    ['confirmReady','SCHEDULE','편성 확정 → 준비 완료','READY_CHANGED','confirmationToReadyMs','LOCAL'],
    ['medicalScreen','MEDICAL','의료 화면 → 예약','SERVICE_RESERVED','screenToReservationMs','LOCAL','RESTORE_BODY'],
    ['reserveRoom','MEDICAL','예약 → 의료실 도착','MEDICAL_ROOM_REACHED','reservationToRoomMs','LOCAL'],
    ['roomEntry','MEDICAL','의료실 도착 → 입장','MEDICAL_ENTERED','roomToEntryMs','LOCAL'],
    ['reserveEntry','MEDICAL','예약 → 입장','MEDICAL_ENTERED','reservationToEntryMs','LOCAL'],
    ['entryRestore','MEDICAL','입장 → 복구 완료','RESTORATION_COMPLETED','enteredToRestoreMs','LOCAL'],
    ['restoreReturn','MEDICAL','복구 완료 → 대기실 복귀','MEDICAL_RETURNED','restorationToReturnMs','LOCAL'],
    ['entryReturn','MEDICAL','입장 → 대기실 복귀','MEDICAL_RETURNED','enteredToReturnMs','LOCAL'],
    ['facilityScreen','FACILITY','시설 화면 → 예약','SERVICE_RESERVED','screenToReservationMs','HOST','FACILITY']
  ];
  function stageMetrics(events){
    return timingStages.map(function(d){
      var samples=new Map();
      events.forEach(function(e){
        var p=e.payload,v=p[d[4]],collection=d[5]==='COLLECTION';
        if(e.sceneType!=='WAITINGROOM'||e.eventType!==d[3]||d[6]&&p.serviceType!==d[6]
          ||e.eventType==='READY_CHANGED'&&p.ready!==true
          ||(collection?e.authority!=='LOCAL'&&e.authority!=='HOST':e.authority!==d[5])
          ||typeof v!=='number'||!Number.isFinite(v)||v<0)return;
        var key=collection&&p.flowId?e.roomId+':'+e.runId+':'+p.flowId:(e.appSessionId||'')+':'+e.eventId;
        var rank=e.authority==='HOST'?2:1,old=samples.get(key);
        if(!old||rank>old.rank)samples.set(key,{seconds:v/1000,rank:rank});
      });
      var values=Array.from(samples.values()).map(function(v){return v.seconds;}),sum=0,min=null,max=null;
      values.forEach(function(v){sum+=v;min=min==null?v:Math.min(min,v);max=max==null?v:Math.max(max,v);});
      return {key:d[0],group:d[1],label:d[2],eventType:d[3],field:d[4],serviceType:d[6]||null,samples:values.length,
        avgSeconds:values.length?sum/values.length:null,minSeconds:min,maxSeconds:max};
    });
  }
  // Use emitted monotonic durations, never subtract clocks from different clients.
  // Missing measurements stay null; a genuine zero is a valid sample.
  function waitingMetrics(events){
    var samples=Object.create(null),seen=new Set();
    function add(key,label,e,field){
      var v=e.payload[field];if(typeof v!=='number'||!Number.isFinite(v)||v<0)return;
      var id=e.appSessionId+':'+e.eventId+':'+key;if(seen.has(id))return;seen.add(id);
      var m=samples[key]||(samples[key]={key:key,label:label,values:[]});m.values.push(v/1000);
    }
    events.forEach(function(e){
      if(e.sceneType!=='WAITINGROOM')return;
      if(e.authority==='LOCAL'){
        if(e.eventType==='ZONE_ENTERED')add('spawn:'+e.payload.zoneType,e.payload.zoneType,e,'spawnToZoneMs');
        if(e.eventType==='STAGE_ENTRANCE_REACHED')add('readyToEntrance','READY_TO_ENTRANCE',e,'readyToEntranceMs');
        if(e.eventType==='MEDICAL_RETURNED')add('medicalReturn','MEDICAL_RETURN',e,'enteredToReturnMs');
      }
      if(e.eventType==='ALL_PLAYERS_ASSEMBLED'&&e.authority==='HOST'){
        add('firstLeftToAssembly','FIRST_LEFT_TO_ASSEMBLY',e,'firstLeftToAssemblyMs');
        add('alarmToAssembly','ALARM_TO_ASSEMBLY',e,'alarmToAssemblyMs');
      }
    });
    return Object.values(samples).map(function(m){return {key:m.key,label:m.label,samples:m.values.length,
      avgSeconds:m.values.reduce(function(s,v){return s+v;},0)/m.values.length,minSeconds:Math.min.apply(null,m.values),maxSeconds:Math.max.apply(null,m.values)};});
  }
  function trickId(value){return /^(CANNON|HUMANCANNON|HUMANCONNON|HUMMANCANNON)$/i.test(value||'')?'CANNON':/^TIGHTROPE$/i.test(value||'')?'TIGHTROPE':null;}
  function gameDay(e){var m=/^D([1-9]\d*)[PT]/.exec(e.breakId||e.performanceId||'');return m?Number(m[1]):Number.isSafeInteger(e.payload.gameDay)&&e.payload.gameDay>0?e.payload.gameDay:null;}
  function dashboardView(events,filters){
    filters=filters||{};
    var list=ordered(resolveParticipants(events.filter(function(e){return e.environment===(filters.environment==='TEST'?'TEST':'LIVE')&&(!filters.gameVersion||versionOf(e)===filters.gameVersion);})));
    var runs=new Map();
    list.forEach(function(e){if(!e.runId)return;if(!runs.has(e.runId))runs.set(e.runId,[]);runs.get(e.runId).push(e);});
    var options=Array.from(runs.keys()).sort(function(a,b){return runs.get(b).slice(-1)[0].occurredAt.localeCompare(runs.get(a).slice(-1)[0].occurredAt)||a.localeCompare(b);});
    var balances=[];
    runs.forEach(function(items,id){
      var seen=new Set(),ledger=items.filter(function(e){if(e.eventType!=='FUNDS_CHANGED'||e.authority!=='HOST'||seen.has(e.payload.transactionId))return false;seen.add(e.payload.transactionId);return true;});
      // Ledger numbers survive client restarts/clock skew. Never sum duplicate snapshots.
      ledger.sort(function(a,b){var x=Number(String(a.payload.transactionId).split(':').pop()),y=Number(String(b.payload.transactionId).split(':').pop());return Number.isFinite(x)&&Number.isFinite(y)?x-y:a.occurredAt.localeCompare(b.occurredAt);});
      var days=new Map(),last=null,first=Infinity,max=0;
      items.forEach(function(e){max=Math.max(max,gameDay(e)||0);});
      ledger.forEach(function(e){var p=e.payload,d=p.gameDay;first=Math.min(first,d);max=Math.max(max,d);var v=days.get(d)||{day:d,balance:null,rewards:0,spent:0,transactions:0};v.balance=p.balanceAfter;v.transactions++;var delta=p.balanceAfter-p.balanceBefore;if(p.category==='REWARD')v.rewards+=delta;if(['PURCHASE','SERVICE','EXPENSE'].indexOf(p.category)>=0)v.spent-=delta;days.set(d,v);});
      // Carry an observed balance across reached days, never infer an initial balance.
      for(var d=first;d<=Math.min(max,3660);d++){var v=days.get(d);if(v)last=v.balance;if(last!==null)balances.push({runId:id,day:d,balance:last,rewards:v?v.rewards:0,spent:v?v.spent:0,transactions:v?v.transactions:0,carried:!v});}
    });
    var fundsId=runs.has(filters.fundsRunId)?filters.fundsRunId:'',fundSeries=[];
    if(fundsId)fundSeries=balances.filter(function(p){return p.runId===fundsId;}).map(function(p){return Object.assign({samples:1},p);});
    else {
      var grouped=new Map();balances.forEach(function(p){if(!grouped.has(p.day))grouped.set(p.day,[]);grouped.get(p.day).push(p);});
      grouped.forEach(function(points,day){function mean(k){return points.reduce(function(s,p){return s+p[k];},0)/points.length;}fundSeries.push({day:day,balance:mean('balance'),rewards:mean('rewards'),spent:mean('spent'),samples:points.length});});
    }
    fundSeries.sort(function(a,b){return a.day-b.day;});
    var selectedRun=runs.has(filters.timelineRunId)?filters.timelineRunId:options[0]||null;
    var runEvents=(runs.get(selectedRun)||[]).filter(analysisEvent),phases=new Map();
    runEvents.forEach(function(e){
      var day=gameDay(e),rest=/^D\d+T(\d+)$/.exec(e.breakId||'');
      var kind=rest&&e.sceneType==='WAITINGROOM'?'BREAK':e.attemptId?'PERFORMANCE':'OTHER';
      var key='D'+(day||'?')+'/'+kind+(kind==='BREAK'?'/'+rest[1]:'');
      if(!phases.has(key))phases.set(key,{id:key,day:day,kind:kind,rest:kind==='BREAK'?Number(rest[1]):null,events:[],attempts:new Map()});
      var phase=phases.get(key);phase.events.push(e);
      if(kind==='PERFORMANCE'&&e.attemptId){
        if(!phase.attempts.has(e.attemptId))phase.attempts.set(e.attemptId,{id:e.attemptId,trickType:null,performanceId:e.performanceId,outcome:'OPEN',reason:null,deaths:[],startedAt:e.occurredAt});
        var attempt=phase.attempts.get(e.attemptId);attempt.trickType=trickId(e.payload.trickType)||attempt.trickType;
        if(e.authority==='HOST'&&/^PERFORMANCE_(SUCCEEDED|FAILED)$/.test(e.eventType)&&attempt.outcome==='OPEN'){attempt.outcome=e.eventType==='PERFORMANCE_SUCCEEDED'?'SUCCESS':'FAILURE';attempt.reason=e.payload.reason||null;}
        if(e.eventType==='PLAYER_DIED'&&e.authority==='HOST')attempt.deaths.push({playerId:e.playerId,playerNumber:e.playerNumber,cause:e.payload.cause||'UNKNOWN'});
      }
    });
    var phaseList=Array.from(phases.values()).sort(function(a,b){return (a.day||0)-(b.day||0)||a.events[0].occurredAt.localeCompare(b.events[0].occurredAt);});
    var phase=phases.get(filters.timelinePhase)||phaseList[0]||null;
    var attempts=phase?Array.from(phase.attempts.values()):[];
    var selectedAttempt=attempts.some(function(a){return a.id===filters.timelineAttemptId;})?filters.timelineAttemptId:attempts.length?attempts[0].id:null;
    var shown=phase?phase.events.filter(function(e){return !selectedAttempt||e.attemptId===selectedAttempt;}):[];
    var offset=Number.isSafeInteger(filters.timelineOffset)?Math.max(0,filters.timelineOffset):0;
    if(offset>=shown.length)offset=0;
    function slim(e){return {eventId:e.eventId,occurredAt:e.occurredAt,appSessionId:e.appSessionId,sequence:e.sequence,playerId:e.playerId,playerNumber:e.playerNumber,networkClientId:e.networkClientId,runId:e.runId,roomId:e.roomId,eventType:e.eventType,authority:e.authority,sceneType:e.sceneType,attemptId:e.attemptId,payload:e.payload};}
    var markers=shown.filter(function(e){return /^(SECTION_|PLAYER_DIED|LIMB_SEVERED|INJURY_CHANGED|TIGHTROPE_FELL|JUMP_RESULT|COOP_ACTIVATED|CANNON_|PERFORMANCE_STARTED|PERFORMANCE_LEFT|INTERACTION_FAILED)/.test(e.eventType);});
    return {runOptions:options,runLabels:options.map(function(id){var es=runs.get(id);return {id:id,startedAt:es[0].occurredAt,day:Math.max.apply(null,es.map(function(e){return gameDay(e)||0;}))};}),funds:{runId:fundsId,mode:fundsId?'RUN':'AVERAGE',series:fundSeries,runSeries:balances,runCount:new Set(balances.map(function(p){return p.runId;})).size},
      timeline:{runId:selectedRun,phases:phaseList.map(function(p){return {id:p.id,day:p.day,kind:p.kind,rest:p.rest,count:p.events.length,attempts:Array.from(p.attempts.values())};}),phaseId:phase?phase.id:null,attemptId:selectedAttempt,
        events:shown.slice(offset,offset+200).map(slim),offset:offset,total:shown.length,markers:markers.slice(0,400).map(slim),markerTotal:markers.length}};
  }
  return {catalog:catalog,validate:validate,headers:headers,row:row,decode:decode,aggregate:aggregate,trickId:trickId,dashboardView:dashboardView,versionOf:versionOf,versionSelection:versionSelection,playerKey:playerKey,analysisEvent:analysisEvent};
})();
