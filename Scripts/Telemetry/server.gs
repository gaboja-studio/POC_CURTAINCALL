/* Deploy core.gs, server.gs, dashboard.html together in one Apps Script project. */
var CC_CONFIG = {
  spreadsheetId:'1KCLPn1tEW5qsK16jcw8swZ75IeN6p6lG_82zRUOg7I8',
  rawFolderId:'1MEeWD5UNdKcypZ6jAhXfaMkP6i84WAi5',
  ownerEmail:'ourhosu@gmail.com', dashboardEnabled:true, maxEvents:50000, maxBatchesPerRefresh:100
};
function ccBook_(){return SpreadsheetApp.openById(CC_CONFIG.spreadsheetId);}
function ccJson_(v){return ContentService.createTextOutput(JSON.stringify(v)).setMimeType(ContentService.MimeType.JSON);}
function ccHash_(s){return Utilities.computeDigest(Utilities.DigestAlgorithm.SHA_256,s,Utilities.Charset.UTF_8).map(function(n){return ('0'+((n+256)%256).toString(16)).slice(-2);}).join('');}
function ccLock_(action){var lock=LockService.getScriptLock();if(!lock.tryLock(10000))throw new Error('BUSY_RETRY');try{return action();}finally{lock.releaseLock();}}
function ccOwner_(){
  // Never fall back to effective user: public execute-as-owner deployments would expose data.
  if(Session.getActiveUser().getEmail()!==CC_CONFIG.ownerEmail)throw new Error('DASHBOARD_ACCESS_DENIED');
}
function doGet(){if(!CC_CONFIG.dashboardEnabled)throw new Error('INGEST_ONLY');return HtmlService.createHtmlOutputFromFile('dashboard').setTitle('CURTAINCALL · 플레이 로그');}
function doPost(e){
  try{
    if(!e||!e.postData||Utilities.newBlob(e.postData.contents).getBytes().length>262144)throw new Error('BODY_LIMIT');
    var request=JSON.parse(e.postData.contents);
    var token=PropertiesService.getScriptProperties().getProperty('INGEST_TOKEN');
    if(!token||token.length<32||request.token!==token)throw new Error('UNAUTHORIZED');
    if(typeof request.batchJson!=='string')throw new Error('BODY_LIMIT');
    var raw=request.batchJson,batch=TelemetryCore.validate(JSON.parse(raw)),hash=ccHash_(raw);
    var result=ccLock_(function(){
      var book=ccBook_(),receipt=ccReceipts_(book)[batch.batchId];
      if(receipt){if(receipt[1]!==hash||Number(receipt[5])!==batch.events.length)throw new Error('BATCH_ID_CONFLICT');return {ok:true,batchId:batch.batchId,sha256:hash,acceptedEvents:batch.events.length,stored:'SHEETS'};}
      if(batch.events.every(function(event){return ccDeleted_(event,ccRules_(book));}))return {ok:true,batchId:batch.batchId,sha256:hash,acceptedEvents:batch.events.length,stored:'DISCARDED_BY_ADMIN'};
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
function ccReadEvents_(book){var rules=ccRules_(book);return ccRows_(book.getSheetByName('Events'),TelemetryCore.headers.length,CC_CONFIG.maxEvents).map(TelemetryCore.decode).filter(function(e){return !ccDeleted_(e,rules);});}
function ccFingerprint_(row){return JSON.stringify(row.slice(0,19).concat([row[21]||'']));}
function ccImport_(book){
  var batchSheet=book.getSheetByName('Batches'),eventSheet=book.getSheetByName('Events');
  eventSheet.getRange(1,22,1,1).setValues([['gameVersion']]);
  var receipts=ccRows_(batchSheet,7,CC_CONFIG.maxEvents),done={};
  receipts.forEach(function(r){if(r[6]==='IMPORTED')done[r[0]]=r[1];});
  var existing=ccRows_(eventSheet,TelemetryCore.headers.length,CC_CONFIG.maxEvents),ids={};
  existing.forEach(function(r){ids[r[0]]=ccFingerprint_(r);});
  var files=DriveApp.getFolderById(CC_CONFIG.rawFolderId).getFiles();var imported=0;
  var rules=ccRules_(book),start=Date.now();
  while(files.hasNext()&&imported<CC_CONFIG.maxBatchesPerRefresh&&Date.now()-start<180000){
    var file=files.next();if(!/^[0-9a-f-]{36}\.json$/i.test(file.getName()))continue;
    var id=file.getName().slice(0,-5);if(done[id])continue;
    var raw=file.getBlob().getDataAsString('UTF-8'),b=TelemetryCore.validate(JSON.parse(raw));
    if(b.batchId!==id)throw new Error('RAW_BATCH_ID_MISMATCH');
    var hash=ccHash_(raw),now=new Date().toISOString(),pending=[];
    b.events.forEach(function(e){
      if(ccDeleted_(e,rules))return;
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
  var groups=[];
  ['TEST','LIVE'].forEach(function(environment){
    TelemetryCore.versionSelection(all,environment).options.forEach(function(version){
      var filters={environment:environment,gameVersion:version};
      groups.push({version:version,environment:environment,data:TelemetryCore.aggregate(all,environment,null,version),filters:filters});
    });
  });
  definitions.forEach(function(d){
    var rows=[];groups.forEach(function(g){
      if(d[1]!=='funds')g.data[d[1]].forEach(function(r){rows.push(d[3](r).concat([g.version,g.environment]));});
      else {
        // Use the exact chart projection: ledger-order balance and carry only across reached days.
        var funds=TelemetryCore.dashboardView(all,g.filters).funds,ledger={};
        g.data.funds.forEach(function(f){ledger[f.runId+':'+f.gameDay]=f;});
        funds.runSeries.forEach(function(p){
          var f=ledger[p.runId+':'+p.day];
          rows.push([p.runId,p.day,p.rewards,p.spent,f?f.netChange:0,p.transactions,g.version,g.environment,p.balance,1,p.carried,'RUN']);
        });
        funds.series.forEach(function(p){
          var points=funds.runSeries.filter(function(r){return r.day===p.day;}),net=0,transactions=0;
          points.forEach(function(r){var f=ledger[r.runId+':'+r.day];if(f)net+=f.netChange;transactions+=r.transactions;});
          rows.push(['',p.day,p.rewards,p.spent,net/p.samples,transactions/p.samples,g.version,g.environment,p.balance,p.samples,'','AVERAGE']);
        });
      }
    });
    var sheet=book.getSheetByName(d[0]),extra=['gameVersion','environment'];
    if(d[1]==='funds')extra=extra.concat(['balance','samples','carried','mode']);
    sheet.getRange(1,d[2],1,extra.length).setValues([extra]);
    sheet.getRange(1,d[2],1,extra.length).setNotes([extra.map(function(k){return {
      gameVersion:'게임 버전. UNVERSIONED는 버전 수집 전 기록이며 0.1.0으로 추정하지 않습니다.',
      environment:'TEST=Unity/MPPM/개발 빌드, LIVE=일반 빌드. 대시보드와 같은 환경·버전을 선택하세요.',
      balance:'대시보드 자금 그래프와 같은 일차 말 관측 잔액입니다.',samples:'해당 일차 잔액의 런 표본 수입니다.',
      carried:'true이면 도달한 일차에 이전 관측 잔액을 이월했습니다.',mode:'RUN=개별 런, AVERAGE=대시보드 기본 평균. 합산하지 말고 한 종류만 필터하세요.'
    }[k];})]);
    ccReplace_(book,d[0],rows);
    sheet.setFrozenRows(1);
    var previousFilter=sheet.getFilter(),criteria=[],width=d[2]+extra.length-1;
    if(previousFilter){
      for(var col=1;col<=Math.min(width,previousFilter.getRange().getNumColumns());col++)criteria[col]=previousFilter.getColumnFilterCriteria(col);
      previousFilter.remove();
    }
    var filter=sheet.getRange(1,1,Math.max(2,rows.length+1),width).createFilter();
    criteria.forEach(function(c,col){if(c)filter.setColumnFilterCriteria(col,c);});
    // Leave room for the filter icon as well as UNVERSIONED / AVERAGE text.
    sheet.setColumnWidths(d[2],extra.length,130);
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
    var book=ccBook_(),count=ccImport_(book),rules=ccRules_(book);ccCompactDeleted_(book,rules);var all=ccReadEvents_(book);
    ccPublish_(book,all);
    ccPruneRaw_(book,rules);
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
function refreshDashboard(filters,auth){var viewer=ccViewer_(auth),data=ccRefresh_(filters);data.viewer=viewer;return data;}
// Changing a tab/filter reads saved rows; importing stays on the explicit/30-minute refresh path.
function getDashboard(filters,auth){var viewer=ccViewer_(auth),data=ccDashboard_(ccReadEvents_(ccBook_()),filters);data.viewer=viewer;data.timeline=[];data.refreshedAt=new Date().toISOString();data.importedBatches=0;return data;}
function scheduledRefresh(){ccOwner_();return ccScheduledRefresh_();}
function ccScheduledRefresh_(){return ccRefresh_({environment:'LIVE'}).eventCount;}
function installRefreshTrigger(){
  ccOwner_();var triggers=ScriptApp.getProjectTriggers().filter(function(t){return t.getHandlerFunction()==='ccScheduledRefresh_';});
  if(!triggers.length)ScriptApp.newTrigger('ccScheduledRefresh_').timeBased().everyMinutes(30).create();
  return {intervalMinutes:30,existingTriggers:triggers.length};
}
function onOpen(){SpreadsheetApp.getUi().createMenu('CURTAINCALL 로그').addItem('로그 가져오기·통계 갱신','scheduledRefresh').addItem('30분 자동 갱신 설치','installRefreshTrigger').addToUi();}
