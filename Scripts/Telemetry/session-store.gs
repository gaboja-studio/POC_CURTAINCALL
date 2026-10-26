/* Session summaries are durable data, not disposable dashboard caches. */
var SessionCore=(function(){
  function key(e){return JSON.stringify([e.environment,TelemetryCore.versionOf(e),e.runId||('app:'+e.appSessionId)]);}
  function clone(x){return JSON.parse(JSON.stringify(x));}
  function summarize(events,context){
    events=events.filter(function(e){return !/^(INPUT_KEY|BUTTON_INPUT|MICROPHONE_INPUT)$/.test(e.eventType);});
    var first=events[0]||context,a=TelemetryCore.aggregate(events,first.environment,null,TelemetryCore.versionOf(first));
    var view=TelemetryCore.dashboardView(events,{environment:first.environment,gameVersion:TelemetryCore.versionOf(first)});
    var links=[],seen=new Set();
    events.slice().sort(function(a,b){return a.occurredAt.localeCompare(b.occurredAt);}).forEach(function(e){
      if(!Number.isInteger(e.playerNumber)||!TelemetryCore.analysisEvent(e))return;
      var k=String(e.playerNumber),important=e.authority==='HOST'&&/^(PLAYER_DIED|LIMB_SEVERED|COOP_ACTIVATED)$/.test(e.eventType);
      if(seen.has(k)&&!important)return;seen.add(k);
      links.push({environment:e.environment,gameVersion:e.gameVersion,runId:e.runId,playerNumber:e.playerNumber,occurredAt:e.occurredAt,eventType:e.eventType,authority:e.authority,payload:{cause:e.payload.cause}});
    });
    a.timeline=[];
    return {aggregate:a,labels:view.runLabels,funds:view.funds,links:links};
  }
  function combine(summaries){
    var empty=TelemetryCore.aggregate([]),out=clone(empty),maps={};
    out.timeline=[];out.eventCount=0;out.eligibleUsers=0;out.runIds=[];
    ['tricks','deaths','zones','usage','waiting','stageTimings','operations','outstanding','funds'].forEach(function(k){out[k]=[];maps[k]=new Map();});
    function add(kind,row,id,counts,average,samples){
      var m=maps[kind],r=m.get(id);if(!r){r=clone(row);counts.forEach(function(k){r[k]=0;});if(average)r[average]=null;m.set(id,r);}
      if(average){var n=r[samples]||0,total=(r[average]||0)*n+(row[average]||0)*(row[samples]||0);r[average]=n+(row[samples]||0)?total/(n+(row[samples]||0)):null;}
      counts.forEach(function(k){r[k]+=(row[k]||0);});
      if('minSeconds' in row&&row.minSeconds!=null)r.minSeconds=r.minSeconds==null?row.minSeconds:Math.min(r.minSeconds,row.minSeconds);
      if('maxSeconds' in row&&row.maxSeconds!=null)r.maxSeconds=r.maxSeconds==null?row.maxSeconds:Math.max(r.maxSeconds,row.maxSeconds);
    }
    summaries.forEach(function(s){var a=s.aggregate;out.eventCount+=a.eventCount;out.eligibleUsers+=a.eligibleUsers||0;out.runIds.push.apply(out.runIds,a.runIds);
      a.tricks.forEach(function(r){add('tricks',r,r.trickType+':'+r.difficulty,['attempts','completed','wins','distanceSamples','deaths','interactions'],'avgMaxDistance','distanceSamples');});
      a.deaths.forEach(function(r){add('deaths',r,r.label,['deaths']);});
      a.zones.forEach(function(r){add('zones',r,r.zoneType,['visits','distinctPlayers','dwellSamples'],'avgDwellSeconds','dwellSamples');});
      a.usage.forEach(function(r){add('usage',r,r.kind+':'+r.itemOrServiceType,['uses','users','observedUsers']);});
      ['waiting','stageTimings'].forEach(function(k){a[k].forEach(function(r){add(k,r,r.key,['samples'],'avgSeconds','samples');});});
      a.operations.forEach(function(r){add('operations',r,JSON.stringify([r.eventType,r.operation,r.reason]),['count']);});
      a.outstanding.forEach(function(r){add('outstanding',r,JSON.stringify([r.kind,r.itemType,r.reason]),['quantity']);});
      out.funds.push.apply(out.funds,a.funds);
    });
    Object.keys(maps).filter(function(k){return k!=='funds';}).forEach(function(k){out[k]=Array.from(maps[k].values());});
    out.tricks.forEach(function(r){r.winRate=r.completed?r.wins/r.completed:null;});
    out.usage.forEach(function(r){r.eligibleUsers=out.eligibleUsers;r.usageRate=out.eligibleUsers?r.observedUsers/out.eligibleUsers:null;});
    out.runIds=Array.from(new Set(out.runIds));return out;
  }
  function view(summaries,filters,detail){
    filters=filters||{};var out=TelemetryCore.dashboardView(detail||[],filters),labels=[],daily=[],breaks=[];
    summaries.forEach(function(s){labels.push.apply(labels,s.labels);daily.push.apply(daily,s.funds.runSeries);breaks.push.apply(breaks,s.funds.breakRunSeries);});
    labels.sort(function(a,b){return b.startedAt.localeCompare(a.startedAt)||a.id.localeCompare(b.id);});
    out.runLabels=labels;out.runOptions=labels.map(function(r){return r.id;});
    function onDate(id,date){return !date||labels.some(function(r){return r.id===id&&new Date(Date.parse(r.startedAt)+9*3600000).toISOString().slice(0,10)===date;});}
    var interval=filters.fundsInterval==='BREAK'?'BREAK':'DAY',source=(interval==='BREAK'?breaks:daily).filter(function(p){return onDate(p.runId,filters.fundsDate);});
    var run=out.runOptions.indexOf(filters.fundsRunId)>=0&&onDate(filters.fundsRunId,filters.fundsDate)?filters.fundsRunId:'',series=[];
    if(run)series=source.filter(function(p){return p.runId===run;}).map(function(p){return Object.assign({samples:1},p);});
    else{var groups=new Map();source.forEach(function(p){var k=interval==='BREAK'?p.order:p.day;if(!groups.has(k))groups.set(k,[]);groups.get(k).push(p);});groups.forEach(function(ps,k){var r={day:ps[0].day,rest:ps[0].rest,order:k,samples:ps.length};['balance','rewards','spent'].forEach(function(f){r[f]=ps.reduce(function(s,p){return s+p[f];},0)/ps.length;});series.push(r);});}
    series.sort(function(a,b){return (a.order||a.day)-(b.order||b.day);});out.funds={runId:run,mode:run?'RUN':'AVERAGE',interval:interval,series:series,runSeries:daily,breakRunSeries:breaks,runCount:new Set(source.filter(function(p){return !run||p.runId===run;}).map(function(p){return p.runId;})).size};
    var options=out.runOptions.filter(function(id){return onDate(id,filters.timelineDate);}),selected=options.indexOf(filters.timelineRunId)>=0?filters.timelineRunId:options[0]||null;
    if(!detail||!detail.length){out.timeline=TelemetryCore.dashboardView([],filters).timeline;out.timeline.runId=selected;out.timeline.archived=!!selected;}
    return out;
  }
  return {key:key,summarize:summarize,combine:combine,view:view};
})();
var CC_SESSION_RUNTIME=null;
function initializeSessionSummaries(){ccOwner_();return ccMaintenanceLock_(function(){try{ccSessionUpdate_(ccBook_());Logger.log(JSON.stringify({complete:true,processing:ccSessionStatus_()}));}catch(error){Logger.log(JSON.stringify({complete:false,code:ccRefreshErrorCode_(error),processing:ccSessionStatus_()}));}});}
function ccSessionState_(){var p=ccPropertyJson_('SESSION_STORE');return p?ccDriveJson_(p.fileId):null;}
function ccSessionSave_(state){
  var folder=ccSnapshotFolder_(),file=folder.createFile('cc-sessions-'+Utilities.getUuid()+'.json',JSON.stringify(state),MimeType.PLAIN_TEXT);
  // Read back the durable checkpoint before making it authoritative or deleting source rows.
  var saved=JSON.parse(file.getBlob().getDataAsString('UTF-8'));if(saved.cursor!==state.cursor||Object.keys(saved.sessions).length!==Object.keys(state.sessions).length)throw Error('SESSION_CHECKPOINT_INVALID');
  var previous=ccPropertyJson_('SESSION_STORE');PropertiesService.getScriptProperties().setProperty('SESSION_STORE',JSON.stringify({fileId:file.getId()}));
  if(previous&&previous.fileId!==file.getId())try{DriveApp.getFileById(previous.fileId).setTrashed(true);}catch(e){}
  CC_SESSION_RUNTIME=state;ccSessionSaveStatus_(state);return state;
}
function ccSessionEnvelope_(s){return {eventId:s.key,environment:s.environment,gameVersion:s.gameVersion,runId:s.runId,appSessionId:s.appSessionId,occurredAt:s.firstAt,eventType:'SESSION_SUMMARY',payload:{},sessionSummary:s};}
function ccSessionRows_(state,rules){return Object.values(state.sessions).filter(function(s){return !ccDeleted_(ccSessionEnvelope_(s),rules||[]);}).map(ccSessionEnvelope_);}
function ccSessionRead_(book){var s=CC_SESSION_RUNTIME||ccSessionState_();if(s&&s.migration)throw Error('SESSION_MIGRATION_PENDING');return s?ccSessionRows_(s,ccRules_(book)):null;}
function ccSessionDetail_(s){return s.detailFile?ccDriveJson_(s.detailFile).events:[];}
function ccSessionUpdate_(book){
  var state=ccSessionState_()||{version:1,cursor:1,sessions:{},pendingLate:0},sheet=book.getSheetByName('Events'),last=sheet.getLastRow(),rules=ccRules_(book),groups=new Map(),now=Date.now(),changed=0,obsolete=[];
  // A prior cleanup may have stopped between physical row removal and checkpoint publication.
  if(state.compacting||state.cursor>last)state.cursor=1;
  for(var start=state.cursor+1;start<=last;start+=5000){
    sheet.getRange(start,1,Math.min(5000,last-start+1),TelemetryCore.headers.length).getValues().forEach(function(row){
      var e=TelemetryCore.decode(row),key=SessionCore.key(e);if(ccDeleted_(e,rules))return;
      if(!groups.has(key))groups.set(key,[]);groups.get(key).push(e);
    });
  }
  var began=Date.now(),completed=0;
  if(!state.migration||state.migration.lastRow!==last)state.migration={lastRow:last,done:[]};
  groups.forEach(function(incoming,key){
    if(state.migration.done.indexOf(key)>=0)return;
    if(Date.now()-began>90000){ccSessionSave_(state);throw Error('SESSION_MIGRATION_PENDING');}
    var s=state.sessions[key],old=s?ccSessionDetail_(s):[],ids=new Set(old.map(function(e){return e.eventId;}));
    // Closed summary-only sessions must never be counted twice on retries. Late arrivals
    // remain in Events/raw for review; no fabricated merge of incomplete attempt results.
    if(s&&s.archivedAt){state.migration.done.push(key);return;}
    var meaningful=incoming.filter(function(e){return !ids.has(e.eventId);});
    var events=old.concat(meaningful),first=events[0]||incoming[0],times=incoming.map(function(e){return e.occurredAt;}).sort(),received=incoming.map(function(e){return e.receivedAt||e.occurredAt;}).sort();
    var summary=s&&!meaningful.length?s.summary:SessionCore.summarize(events,first),file=s&&!meaningful.length?null:ccSnapshotFolder_().createFile('cc-session-detail-'+Utilities.getUuid()+'.json',JSON.stringify({events:events}),MimeType.PLAIN_TEXT);
    if(s&&file&&s.detailFile)obsolete.push(s.detailFile);
    s={key:key,environment:first.environment,gameVersion:TelemetryCore.versionOf(first),runId:first.runId,appSessionId:first.appSessionId,firstAt:s&&s.firstAt<times[0]?s.firstAt:times[0],lastAt:s&&s.lastAt>times[times.length-1]?s.lastAt:times[times.length-1],lastReceivedAt:s&&s.lastReceivedAt>received[received.length-1]?s.lastReceivedAt:received[received.length-1],detailFile:file?file.getId():s.detailFile,summary:summary,archivedAt:null};
    state.sessions[key]=s;if(meaningful.length)changed++;
    state.migration.done.push(key);if(++completed%10===0)ccSessionSave_(state);
  });
  // Remove explicitly deleted sessions; retention never deletes surveys or summary statistics.
  Object.keys(state.sessions).forEach(function(k){if(ccDeleted_(ccSessionEnvelope_(state.sessions[k]),rules)){obsolete.push(state.sessions[k].detailFile);delete state.sessions[k];}});
  state.cursor=last;state.migration=null;state.compacting=false;state.updatedAt=new Date().toISOString();state.changedSessions=changed;
  var expired=[],importStatus=ccPropertyJson_('IMPORT_STATUS')||{},caughtUp=!(importStatus.pendingBatches>0||importStatus.pendingExact===false);
  Object.values(state.sessions).forEach(function(s){var days=s.environment==='LIVE'?30:7;
    if(caughtUp&&!s.archivedAt&&now-Date.parse(s.lastAt)>days*86400000&&now-Date.parse(s.lastReceivedAt)>2*86400000){expired.push(s.detailFile);s.detailFile=null;s.archivedAt=new Date().toISOString();}
  });
  ccSessionSave_(state);
  // Source detail is removed only after summaries and the checkpoint have been verified.
  ccSessionCompact_(book,state);
  ccRelocateVerified_(book,state);
  expired.concat(obsolete).forEach(function(id){if(id)try{DriveApp.getFileById(id).setTrashed(true);}catch(e){}});
  ccSessionPublishIndex_(book,state);ccSessionSaveStatus_(state);ccSessionGarbage_(state);return ccSessionRows_(state,rules);
}
function ccSessionGarbage_(state){
  var referenced=new Set(Object.values(state.sessions).map(function(s){return s.detailFile;}).filter(Boolean)),files=ccSnapshotFolder_().getFiles(),count=0,began=Date.now();
  while(files.hasNext()&&count<100&&Date.now()-began<10000){var file=files.next();if(/^cc-session-detail-[0-9a-f-]{36}\.json$/.test(file.getName())&&!referenced.has(file.getId())&&Date.now()-file.getDateCreated().getTime()>CC_REFRESH_LEASE_MS){file.setTrashed(true);count++;}}
}
function ccSessionCompact_(book,state){
  var rules=ccRules_(book),expired=new Set(Object.values(state.sessions).filter(function(s){return s.archivedAt;}).map(function(s){return s.key;}));if(!expired.size&&!rules.length)return;
  var sheet=book.getSheetByName('Events'),last=sheet.getLastRow(),blocks=[],begin=null,count=0,late=0;
  for(var start=2;start<=last;start+=5000){sheet.getRange(start,1,Math.min(5000,last-start+1),TelemetryCore.headers.length).getValues().forEach(function(row,j){
    var e=TelemetryCore.decode(row),s=state.sessions[SessionCore.key(e)],remove=ccDeleted_(e,rules)||(s&&expired.has(s.key)&&Date.parse(e.receivedAt||e.occurredAt)<=Date.parse(s.archivedAt));
    if(s&&s.archivedAt&&!remove)late++;
    if(remove){if(begin===null)begin=start+j;count++;}else if(count){blocks.push([begin,count]);begin=null;count=0;}
  });}if(count)blocks.push([begin,count]);state.pendingLate=late;
  if(!blocks.length)return;
  state.compacting=true;ccSessionSave_(state);
  blocks.slice(-100).reverse().forEach(function(b){sheet.deleteRows(b[0],b[1]);});
  state.cursor=sheet.getLastRow();state.compacting=false;ccSessionSave_(state);
}
function ccSessionPublishIndex_(book,state){
  ccEnsureSheet_(book,'세션 요약',['environment','gameVersion','runId','startedAt','lastEventAt','detailStatus','events','plays','wins','deaths','lastReceivedAt','detailFile']);
  ccReplace_(book,'세션 요약',Object.values(state.sessions).map(function(s){var a=s.summary.aggregate;return [s.environment,s.gameVersion,s.runId,s.firstAt,s.lastAt,s.archivedAt?'SUMMARY_ONLY':'DETAIL',a.eventCount,a.tricks.reduce(function(n,t){return n+t.completed;},0),a.tricks.reduce(function(n,t){return n+t.wins;},0),a.deaths.reduce(function(n,t){return n+t.deaths;},0),s.lastReceivedAt,s.detailFile?'https://drive.google.com/file/d/'+s.detailFile+'/view':''];}));
}
function ccRelocateVerified_(book,state){
  var sheet=book.getSheetByName('Events');if(sheet.getLastRow()<=50000)return 0;
  // Only inspect a bounded prefix included in this committed checkpoint. New rows
  // beyond its cursor, late arrivals and files that fail readback stay untouched.
  var count=Math.min(25000,state.cursor-1),rows=sheet.getRange(2,1,count,TelemetryCore.headers.length).getValues(),verified=new Map(),blocks=[],start=null,size=0,began=Date.now();
  rows.forEach(function(row,i){var e=TelemetryCore.decode(row),key=SessionCore.key(e),s=state.sessions[key];
    if(s&&!s.archivedAt&&s.detailFile&&!verified.has(key)&&Date.now()-began<30000){
      var ids=new Map();try{var file=DriveApp.getFileById(s.detailFile),saved=JSON.parse(file.getBlob().getDataAsString('UTF-8'));saved.events.forEach(function(x){ids.set(x.eventId,JSON.stringify(TelemetryCore.row(x,x,x.receivedAt)));});}catch(error){}
      verified.set(key,ids);
    }
    var ids=verified.get(key),safe=ids&&ids.get(e.eventId)===JSON.stringify(row);
    if(safe){if(start===null)start=i+2;size++;}else if(size){blocks.push([start,size]);start=null;size=0;}
  });if(size)blocks.push([start,size]);if(!blocks.length)return 0;
  state.compacting=true;ccSessionSave_(state);var removed=0;
  blocks.slice(-100).reverse().forEach(function(b){sheet.deleteRows(b[0],b[1]);removed+=b[1];});
  state.cursor=sheet.getLastRow();state.compacting=false;ccSessionSave_(state);return removed;
}
function ccSessionSaveStatus_(s){var values=Object.values(s.sessions),status={mode:s.migration?'MIGRATING':'INCREMENTAL',liveDays:30,testDays:7,updatedAt:s.updatedAt,changedSessions:s.changedSessions||0,detailSessions:values.filter(function(x){return !x.archivedAt;}).length,summarySessions:values.filter(function(x){return !!x.archivedAt;}).length,pendingLate:s.pendingLate||0};PropertiesService.getScriptProperties().setProperty('SESSION_STATUS',JSON.stringify(status));}
function ccSessionStatus_(){return Object.assign(ccPropertyJson_('SESSION_STATUS')||{mode:'MIGRATION_PENDING',liveDays:30,testDays:7},ccPropertyJson_('IMPORT_STATUS')||{});}
function ccAggregate_(all,environment,runId,version){
  if(!all.some(function(e){return e.sessionSummary;}))return TelemetryCore.aggregate(all,environment,runId,version);
  return SessionCore.combine(all.filter(function(e){return e.environment===(environment||'LIVE')&&(!version||TelemetryCore.versionOf(e)===version)&&(!runId||e.runId===runId);}).map(function(e){return e.sessionSummary.summary;}));
}
function ccView_(all,filters,loadDetail){
  if(!all.some(function(e){return e.sessionSummary;}))return TelemetryCore.dashboardView(all,filters);
  var sessions=all.filter(function(e){return e.environment===(filters.environment||'LIVE')&&(!filters.gameVersion||TelemetryCore.versionOf(e)===filters.gameVersion);}).map(function(e){return e.sessionSummary;});
  var view=SessionCore.view(sessions.map(function(s){return s.summary;}),filters),selected=sessions.find(function(s){return s.runId===view.timeline.runId;});
  if(loadDetail&&selected&&!selected.archivedAt)view=SessionCore.view(sessions.map(function(s){return s.summary;}),Object.assign({},filters,{timelineRunId:selected.runId}),ccSessionDetail_(selected));
  view.timeline.archived=!!(selected&&selected.archivedAt);return view;
}
function ccSurveyEvents_(all){var result=[];all.forEach(function(e){if(e.sessionSummary){result.push(e);result.push.apply(result,e.sessionSummary.summary.links);}else result.push(e);});return result;}
