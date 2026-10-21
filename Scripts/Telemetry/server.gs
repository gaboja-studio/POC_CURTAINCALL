/* Deploy core.gs, server.gs, dashboard.html together in one Apps Script project. */
var CC_CONFIG = {
  spreadsheetId:'1KCLPn1tEW5qsK16jcw8swZ75IeN6p6lG_82zRUOg7I8',
  rawFolderId:'1MEeWD5UNdKcypZ6jAhXfaMkP6i84WAi5',
  ownerEmail:'ourhosu@gmail.com', maxEvents:50000, maxBatchesPerRefresh:100
};
function ccBook_(){return SpreadsheetApp.openById(CC_CONFIG.spreadsheetId);}
function ccJson_(v){return ContentService.createTextOutput(JSON.stringify(v)).setMimeType(ContentService.MimeType.JSON);}
function ccHash_(s){return Utilities.computeDigest(Utilities.DigestAlgorithm.SHA_256,s,Utilities.Charset.UTF_8).map(function(n){return ('0'+((n+256)%256).toString(16)).slice(-2);}).join('');}
function ccLock_(action){var lock=LockService.getScriptLock();if(!lock.tryLock(10000))throw new Error('BUSY_RETRY');try{return action();}finally{lock.releaseLock();}}
function ccOwner_(){
  // Never fall back to effective user: public execute-as-owner deployments would expose data.
  if(Session.getActiveUser().getEmail()!==CC_CONFIG.ownerEmail)throw new Error('DASHBOARD_ACCESS_DENIED');
}
function doGet(){ccOwner_();return HtmlService.createHtmlOutputFromFile('dashboard').setTitle('CURTAINCALL · 플레이 로그');}
function doPost(e){
  try{
    if(!e||!e.postData||Utilities.newBlob(e.postData.contents).getBytes().length>262144)throw new Error('BODY_LIMIT');
    var request=JSON.parse(e.postData.contents);
    var token=PropertiesService.getScriptProperties().getProperty('INGEST_TOKEN');
    if(!token||token.length<32||request.token!==token)throw new Error('UNAUTHORIZED');
    if(typeof request.batchJson!=='string')throw new Error('BODY_LIMIT');
    var raw=request.batchJson,batch=TelemetryCore.validate(JSON.parse(raw)),hash=ccHash_(raw);
    var result=ccLock_(function(){
      var folder=DriveApp.getFolderById(CC_CONFIG.rawFolderId);
      var files=folder.getFilesByName(batch.batchId+'.json');var file;
      if(files.hasNext()){
        file=files.next();if(ccHash_(file.getBlob().getDataAsString('UTF-8'))!==hash)throw new Error('BATCH_ID_CONFLICT');
        if(files.hasNext())throw new Error('DUPLICATE_RAW_FILES');
      }else file=folder.createFile(batch.batchId+'.json',raw,MimeType.PLAIN_TEXT);
      return {ok:true,batchId:batch.batchId,sha256:hash,acceptedEvents:batch.events.length,stored:'DRIVE'};
    });
    return ccJson_(result);
  }catch(error){
    // No payload, token, stack, player identity or Google credential returned/logged.
    var allowed=/^(BODY_LIMIT|UNAUTHORIZED|SCHEMA_VERSION|GAME_VERSION|FLUSH_REASON|BATCH_EVENT_LIMIT|EVENT_ID|SEQUENCE|EVENT_TIME|ENVIRONMENT|PLAYER_ID|PLAYER_NUMBER|NETWORK_CLIENT_ID|ROOM_ID|PERFORMANCE_ID|BREAK_ID|SCENE_TYPE|AUTHORITY|EVENT_TYPE|PAYLOAD_SIZE|PERFORMANCE_CONTEXT|MICROPHONE_TIME|BUSY_RETRY|BATCH_ID_CONFLICT|DUPLICATE_RAW_FILES|INVALID_\w+|PAYLOAD_\w+)$/;
    return ccJson_({ok:false,error:allowed.test(error.message)?error.message:'SERVER_ERROR',retryable:!allowed.test(error.message)||error.message==='BUSY_RETRY'});
  }
}
function ccRows_(sheet,width,limit){
  var count=sheet.getLastRow()-1;if(count>limit)throw new Error('CAPACITY_LIMIT_'+sheet.getName());
  return count>0?sheet.getRange(2,1,count,width).getValues():[];
}
function ccText_(v){return typeof v==='string'&&/^[=+\-@]/.test(v)?"'"+v:v;}
function ccAppend_(sheet,rows){
  if(!rows.length)return;
  var end=sheet.getLastRow()+rows.length;
  if(end>sheet.getMaxRows())sheet.insertRowsAfter(sheet.getMaxRows(),end-sheet.getMaxRows());
  sheet.getRange(end-rows.length+1,1,rows.length,rows[0].length).setValues(rows.map(function(r){return r.map(ccText_);}));
}
function ccReadEvents_(book){return ccRows_(book.getSheetByName('Events'),TelemetryCore.headers.length,CC_CONFIG.maxEvents).map(TelemetryCore.decode);}
function ccFingerprint_(row){return JSON.stringify(row.slice(0,19).concat([row[21]||'']));}
function ccImport_(book){
  var batchSheet=book.getSheetByName('Batches'),eventSheet=book.getSheetByName('Events');
  eventSheet.getRange(1,22,1,1).setValues([['gameVersion']]);
  var receipts=ccRows_(batchSheet,7,CC_CONFIG.maxEvents),done={};
  receipts.forEach(function(r){if(r[6]==='IMPORTED')done[r[0]]=r[1];});
  var existing=ccRows_(eventSheet,TelemetryCore.headers.length,CC_CONFIG.maxEvents),ids={};
  existing.forEach(function(r){ids[r[0]]=ccFingerprint_(r);});
  var files=DriveApp.getFolderById(CC_CONFIG.rawFolderId).getFiles();var imported=0;
  var start=Date.now();
  while(files.hasNext()&&imported<CC_CONFIG.maxBatchesPerRefresh&&Date.now()-start<180000){
    var file=files.next();if(!/^[0-9a-f-]{36}\.json$/i.test(file.getName()))continue;
    var id=file.getName().slice(0,-5);if(done[id])continue;
    var raw=file.getBlob().getDataAsString('UTF-8'),b=TelemetryCore.validate(JSON.parse(raw));
    if(b.batchId!==id)throw new Error('RAW_BATCH_ID_MISMATCH');
    var hash=ccHash_(raw),now=new Date().toISOString(),pending=[];
    b.events.forEach(function(e){
      var row=TelemetryCore.row(e,b,now),fingerprint=ccFingerprint_(row);
      if(ids[e.eventId]&&ids[e.eventId]!==fingerprint)throw new Error('EVENT_ID_CONFLICT');
      if(!ids[e.eventId]){pending.push(row);ids[e.eventId]=fingerprint;}
    });
    if(existing.length+pending.length>CC_CONFIG.maxEvents)throw new Error('CAPACITY_LIMIT_Events');
    // Crash after Events append is safe: next import sees eventIds before recording receipt.
    ccAppend_(eventSheet,pending);existing.push.apply(existing,pending);
    ccAppend_(batchSheet,[[b.batchId,hash,file.getId(),file.getDateCreated().toISOString(),now,b.events.length,'IMPORTED']]);
    done[id]=hash;imported++;
  }
  return imported;
}
function ccReplace_(book,name,rows){
  var sheet=book.getSheetByName(name),old=sheet.getLastRow();
  if(old>1)sheet.getRange(2,1,old-1,sheet.getLastColumn()).clearContent();
  ccAppend_(sheet,rows);
}
function ccPublish_(book,all){
  var definitions=[
    ['묘기 통계','tricks',11,function(t){return [t.trickType,t.difficulty,t.attempts,t.completed,t.wins,t.winRate,t.avgMaxDistance,t.distanceSamples,t.deaths,t.interactions];}],
    ['사망 원인','deaths',5,function(d){return d.label.split(' / ').concat([d.deaths]);}],
    ['공간 이용','zones',6,function(z){return [z.zoneType,z.visits,z.distinctPlayers,z.avgDwellSeconds,z.dwellSamples];}],
    ['기물 서비스 이용','usage',7,function(u){return [u.kind,u.itemOrServiceType,u.uses,u.users,u.eligibleUsers,u.usageRate];}],
    ['자금 일계','funds',7,function(f){return [f.runId,f.gameDay,f.rewards,f.spent,f.netChange,f.transactions];}]
  ];
  var versions=TelemetryCore.versionSelection(all,'LIVE').options.map(function(v){return {version:v,data:TelemetryCore.aggregate(all,'LIVE',null,v)};});
  definitions.forEach(function(d){
    var rows=[];versions.forEach(function(v){v.data[d[1]].forEach(function(r){rows.push(d[3](r).concat([v.version]));});});
    book.getSheetByName(d[0]).getRange(1,d[2],1,1).setValues([['gameVersion']]);
    ccReplace_(book,d[0],rows);
  });
  book.getSheetByName('운영 상태').getRange('B8').setValue(new Date().toISOString());
}
function ccDashboard_(all,filters){
  filters=Object.assign({},filters||{});
  var version=TelemetryCore.versionSelection(all,filters.environment,filters.gameVersion);
  filters.gameVersion=version.selected;
  var data=TelemetryCore.aggregate(all,filters.environment,filters.runId,filters.gameVersion);
  data.gameVersion=version.selected;data.versionOptions=version.options;
  data.view=TelemetryCore.dashboardView(all,filters);data.runOptions=data.view.runOptions;
  return data;
}
function ccRefresh_(filters){
  return ccLock_(function(){
    var book=ccBook_(),count=ccImport_(book),all=ccReadEvents_(book);
    ccPublish_(book,all);
    ccAppend_(book.getSheetByName('갱신 이력'),[[new Date().toISOString(),count,all.length,'OK']]);
    var data=ccDashboard_(all,filters);
    data.totalTimelineRows=data.timeline.length;
    // Explicit paged display; all events participate in aggregates.
    var offset=filters&&Number.isSafeInteger(filters.offset)?Math.max(0,filters.offset):0;
    data.timeline=data.timeline.slice(offset,offset+200);
    data.refreshedAt=new Date().toISOString();data.importedBatches=count;
    return data;
  });
}
function refreshDashboard(filters){ccOwner_();return ccRefresh_(filters);}
// Changing a tab/filter reads saved rows; importing stays on the explicit/30-minute refresh path.
function getDashboard(filters){ccOwner_();var data=ccDashboard_(ccReadEvents_(ccBook_()),filters);data.timeline=[];data.refreshedAt=new Date().toISOString();data.importedBatches=0;return data;}
function scheduledRefresh(){ccOwner_();return ccScheduledRefresh_();}
function ccScheduledRefresh_(){return ccRefresh_({environment:'LIVE'}).eventCount;}
function installRefreshTrigger(){
  ccOwner_();var triggers=ScriptApp.getProjectTriggers().filter(function(t){return t.getHandlerFunction()==='ccScheduledRefresh_';});
  if(!triggers.length)ScriptApp.newTrigger('ccScheduledRefresh_').timeBased().everyMinutes(30).create();
  return {intervalMinutes:30,existingTriggers:triggers.length};
}
function onOpen(){SpreadsheetApp.getUi().createMenu('CURTAINCALL 로그').addItem('로그 가져오기·통계 갱신','scheduledRefresh').addItem('30분 자동 갱신 설치','installRefreshTrigger').addToUi();}
