/* Private immutable Drive snapshots. Readers never import or publish statistics. */
var CC_REFRESH_INTERVAL_MS=5*60*1000, CC_REFRESH_LEASE_MS=7*60*1000;
function ccPropertyJson_(key){try{return JSON.parse(PropertiesService.getScriptProperties().getProperty(key)||'null');}catch(e){return null;}}
function ccDataEpoch_(){return PropertiesService.getScriptProperties().getProperty('DASHBOARD_DATA_EPOCH')||'0';}
function ccSnapshotPointer_(){var p=ccPropertyJson_('DASHBOARD_SNAPSHOT');return p&&p.epoch===ccDataEpoch_()?p:null;}
function ccRefreshState_(pointer){
  var p=arguments.length?pointer:ccSnapshotPointer_(),c=ccPropertyJson_('DASHBOARD_REFRESH_CONTROL')||{},now=Date.now();
  var running=!!(c.id&&c.expiresAt>now),last=p?Date.parse(p.refreshedAt):0;
  var next=Math.max(last?last+CC_REFRESH_INTERVAL_MS:0,c.failed&&c.startedAt?c.startedAt+CC_REFRESH_INTERVAL_MS:0);
  return {refreshing:running,refreshedAt:p?p.refreshedAt:null,revision:p?p.revision:'',cacheAvailable:!!p,
    nextRefreshAt:next?new Date(next).toISOString():null,serverNow:new Date(now).toISOString(),stage:c.stage||'',processing:ccSessionStatus_(),lastError:c.failed?(c.code==='SESSION_MIGRATION_PENDING'?'기존 기록을 세션 요약으로 전환 중입니다. 이전 완료 결과를 유지하며 다음 갱신에서 이어갑니다.':c.stage==='IMPORT'&&p&&p.refreshedAt===c.publishedAt?'일부 로그 적재가 지연되고 있습니다. 설문과 적재 완료 기록은 최신 결과입니다.':'최근 갱신을 완료하지 못했습니다. 이전 완료 결과를 표시합니다.') :''};
}
function getDashboardRefreshState(auth){ccViewer_(auth);return ccRefreshState_();}
function ccRefreshErrorCode_(error){
  var message=String(error&&error.message||'');
  if(/^CAPACITY_LIMIT_[A-Za-z0-9_]+$/.test(message))return message;
  if(/^[A-Z][A-Z0-9_]{1,80}$/.test(message))return message;
  if(/quota|too many times|limit exceeded/i.test(message))return 'GOOGLE_QUOTA';
  if(/number of columns|columns in the data/i.test(message))return 'SHEET_COLUMN_MISMATCH';
  if(/maximum.*execution|timed out/i.test(message))return 'EXECUTION_TIMEOUT';
  if(/permission|access denied|authorization/i.test(message))return 'GOOGLE_ACCESS';
  if(/grid limits|out of bounds|outside.*sheet|범위.*시트/.test(message))return 'SHEET_GRID_LIMIT';
  return 'REFRESH_ERROR';
}
function diagnoseDashboardRefresh(){
  ccOwner_();ccRefresh_({environment:'LIVE'});
  var control=ccPropertyJson_('DASHBOARD_REFRESH_CONTROL')||{};
  Logger.log(JSON.stringify({failed:!!control.failed,stage:control.stage||'',code:control.code||'',startedAt:control.startedAt}));
}
function ccCacheJsonPut_(key,value){
  // CacheService is an optional accelerator, never the durable source of truth.
  try{var text=JSON.stringify(value);if(text.length>400000)return;var parts=[],start=0;
    while(start<text.length){var end=Math.min(start+20000,text.length);if(end<text.length&&/[\uD800-\uDBFF]/.test(text[end-1]))end--;parts.push(text.slice(start,end));start=end;}
    parts.forEach(function(part,i){ccCache_().put(key+':'+i,part,21600);});ccCache_().put(key+':parts',String(parts.length),21600);
  }catch(e){/* Drive fallback also works after cache eviction or a quota failure. */}
}
function ccCacheJsonGet_(key){try{var count=Number(ccCache_().get(key+':parts'));if(!count||count>20)return null;var parts=[];for(var i=0;i<count;i++){var part=ccCache_().get(key+':'+i);if(part===null)return null;parts.push(part);}return JSON.parse(parts.join(''));}catch(e){return null;}}
function ccDriveJson_(id){var key='dashboard-file:'+id,value=ccCacheJsonGet_(key);if(value)return value;value=JSON.parse(DriveApp.getFileById(id).getBlob().getDataAsString('UTF-8'));ccCacheJsonPut_(key,value);return value;}
function ccSnapshotFolder_(){
  var props=PropertiesService.getScriptProperties(),id=props.getProperty('DASHBOARD_CACHE_FOLDER_ID');
  if(id)return DriveApp.getFolderById(id);
  var folder=DriveApp.getFolderById(CC_CONFIG.rawFolderId).createFolder('대시보드 캐시');props.setProperty('DASHBOARD_CACHE_FOLDER_ID',folder.getId());return folder;
}
function ccWriteSnapshot_(book,all,refreshedAt,importedBatches){
  var folder=ccSnapshotFolder_(),revision=Utilities.getUuid(),epoch=ccDataEpoch_(),previous=ccSnapshotPointer_();
  var survey=ccSurveySnapshot_(book),index={revision:revision,epoch:epoch,groups:{}};
  function save(suffix,value){var file=folder.createFile('cc-dashboard-'+revision+'-'+suffix+'.json',JSON.stringify(value),MimeType.PLAIN_TEXT);return file.getId();}
  ['TEST','LIVE'].forEach(function(environment){
    var versions=TelemetryCore.versionSelection(all.concat(survey.rows),environment).options,group={versions:versions,versionsData:{}};index.groups[environment]=group;
    versions.forEach(function(version,i){
      var events=all.filter(function(e){return e.environment===environment&&TelemetryCore.versionOf(e)===version;});
      var s={rows:survey.rows.filter(function(r){return r.environment===environment&&TelemetryCore.versionOf(r)===version;}),status:survey.status,error:survey.error};
      group.versionsData[version]={records:save(environment+'-'+i+'-records',{events:events,surveys:s}),
        overview:save(environment+'-'+i+'-overview',ccDashboard_(events,{environment:environment,gameVersion:version},s))};
    });
  });
  var indexId=save('index',index),pointer={revision:revision,epoch:epoch,indexId:indexId,
    refreshedAt:refreshedAt||new Date().toISOString(),importedBatches:importedBatches||0,previousRevision:previous?previous.revision:''};
  // All files are complete before this small pointer is published. Old readers retain a consistent generation.
  ccLock_(function(){if(ccDataEpoch_()!==epoch)throw new Error('SNAPSHOT_INVALIDATED');PropertiesService.getScriptProperties().setProperty('DASHBOARD_SNAPSHOT',JSON.stringify(pointer));});
  try{var files=folder.getFiles();while(files.hasNext()){var f=files.next(),m=/^cc-dashboard-([0-9a-f-]{36})-/.exec(f.getName());if(m&&m[1]!==revision&&m[1]!==pointer.previousRevision)f.setTrashed(true);}}catch(e){/* Keep older private cache files if cleanup is unavailable. */}
  return pointer;
}
function ccDashboardCache_(filters,action){
  filters=Object.assign({},filters||{});var pointer=ccSnapshotPointer_(),state=ccRefreshState_(pointer),data;
  if(pointer){
    var index=ccDriveJson_(pointer.indexId),environment=filters.environment||'LIVE',group=Object.prototype.hasOwnProperty.call(index.groups,environment)?index.groups[environment]:null;
    var versions=group?group.versions.slice():['0.1.0'],version=filters.gameVersion||versions[0];
    if(versions.indexOf(version)<0)versions.push(version);filters.gameVersion=version;
    var entry=group&&Object.prototype.hasOwnProperty.call(group.versionsData,version)?group.versionsData[version]:null;
    var overview=entry?ccDriveJson_(entry.overview):null,defaults=overview?{timelineRunId:overview.view.timeline.runId,timelinePhase:overview.view.timeline.phaseId,timelineAttemptId:overview.view.timeline.attemptId,surveyKind:'NIGHT1',fundsInterval:'DAY'}:{};
    var customized=['runId','fundsRunId','fundsDate','timelineRunId','timelineDate','timelinePhase','timelineAttemptId','timelineOffset','offset','surveyKind','surveyCampaign','surveyDate','surveyRunId','surveyPlayer','surveyOffset','fundsInterval'].some(function(k){return filters[k]!==undefined&&filters[k]!==null&&filters[k]!==''&&(filters[k]!==0||k==='surveyPlayer')&&filters[k]!==defaults[k];});
    var key='dashboard-view:'+pointer.revision+':'+ccHash_(JSON.stringify(filters));
    data=ccCacheJsonGet_(key);
    if(!data){
      if(entry&&!customized)data=overview;
      else{var records=entry?ccDriveJson_(entry.records):{events:[],surveys:{rows:[],status:[],error:''}};data=ccDashboard_(records.events,filters,records.surveys);}
      data.versionOptions=versions;data.gameVersion=version;data.timeline=[];ccCacheJsonPut_(key,data);
    }
    // Deletion invalidates an in-flight old read as well as all later cache hits.
    if(pointer.epoch!==ccDataEpoch_())return ccDashboardCache_(filters,'INVALIDATED');
  }else data=ccDashboard_([],filters,{rows:[],status:[],error:''});
  data.refreshedAt=state.refreshedAt;data.importedBatches=action==='UPDATED'&&pointer?pointer.importedBatches:0;
  data.processing=ccSessionStatus_();data.refresh=Object.assign({},state,{processing:data.processing,action:action||'CACHED',refreshing:state.refreshing||action==='RUNNING'});data.timeline=[];return data;
}
function ccMaintenanceLock_(action){var lock=LockService.getUserLock();if(!lock.tryLock(1))throw new Error('통계 갱신·정리가 진행 중입니다. 완료 후 다시 시도해 주세요.');try{return action();}finally{lock.releaseLock();}}
function rebuildDashboardCache(){ccOwner_();return ccMaintenanceLock_(function(){var book=ccBook_(),previous=ccSnapshotPointer_();return ccWriteSnapshot_(book,ccReadEvents_(book),previous?previous.refreshedAt:null,0);});}
function checkDashboardCache(){
  ccOwner_();var state=ccRefreshState_(),summary={cacheAvailable:state.cacheAvailable,refreshing:state.refreshing,refreshedAt:state.refreshedAt,
    nextRefreshAt:state.nextRefreshAt,stage:state.stage,lastError:state.lastError,processing:state.processing,testEvents:getDashboard({environment:'TEST'}).eventCount,liveEvents:getDashboard({environment:'LIVE'}).eventCount};
  Logger.log(JSON.stringify(summary));return summary;
}
function ccRefresh_(filters){
  var state=ccRefreshState_();
  if(state.refreshing)return ccDashboardCache_(filters,'RUNNING');
  if(state.nextRefreshAt&&Date.parse(state.nextRefreshAt)>Date.now())return ccDashboardCache_(filters,'CACHED');
  var maintenance=LockService.getUserLock();if(!maintenance.tryLock(1))return ccDashboardCache_(filters,'RUNNING');
  var claimed=false,id=Utilities.getUuid(),gate=LockService.getScriptLock();
  try{
    // Only the admission decision holds the lock shared with ingest; statistics run outside it.
    if(!gate.tryLock(50))return ccDashboardCache_(filters,'CACHED');
    try{state=ccRefreshState_();if(!state.refreshing&&(!state.nextRefreshAt||Date.parse(state.nextRefreshAt)<=Date.now())){
      PropertiesService.getScriptProperties().setProperty('DASHBOARD_REFRESH_CONTROL',JSON.stringify({id:id,startedAt:Date.now(),expiresAt:Date.now()+CC_REFRESH_LEASE_MS}));claimed=true;
    }}finally{gate.releaseLock();}
    if(!claimed)return ccDashboardCache_(filters,state.refreshing?'RUNNING':'CACHED');
    var stage='IMPORT';function report(next){stage=next;var c=ccPropertyJson_('DASHBOARD_REFRESH_CONTROL')||{};c.stage=next;PropertiesService.getScriptProperties().setProperty('DASHBOARD_REFRESH_CONTROL',JSON.stringify(c));}try{report('IMPORT');
      var book=ccBook_(),count=0,importError=null;
      try{var stored=ccSessionState_();if(!stored||!stored.migration)count=ccImport_(book);}catch(error){importError=ccRefreshErrorCode_(error);}
      var rules=ccRules_(book);report('SESSIONS');var all=ccSessionUpdate_(book);
      report('SURVEYS');
      try{ccImportSurveys_(book,all);PropertiesService.getScriptProperties().setProperty('SURVEY_SYNC_ERROR','');}
      catch(error){PropertiesService.getScriptProperties().setProperty('SURVEY_SYNC_ERROR','설문 응답 동기화에 실패했습니다. 이전 응답을 유지합니다.');}
      report('STATISTICS');ccPublish_(book,all);
      report('RETENTION');ccPruneRaw_(book,rules);
      report('CACHE');var published=ccWriteSnapshot_(book,all,null,count);
      report('HISTORY');
      var history=book.getSheetByName('갱신 이력');if(history.getLastRow()>1000)history.deleteRows(2,history.getLastRow()-1000);
      ccAppend_(book.getSheetByName('갱신 이력'),[[new Date().toISOString(),count,all.reduce(function(n,e){return n+(e.sessionSummary?e.sessionSummary.summary.aggregate.eventCount:1);},0),importError?'PARTIAL':'OK']]);
      PropertiesService.getScriptProperties().setProperty('DASHBOARD_REFRESH_CONTROL',JSON.stringify({startedAt:Date.now(),failed:!!importError,stage:importError?'IMPORT':'',code:importError||'',publishedAt:published.refreshedAt}));
      return ccDashboardCache_(filters,'UPDATED');
    }catch(error){
      var control=ccPropertyJson_('DASHBOARD_REFRESH_CONTROL')||{};
      PropertiesService.getScriptProperties().setProperty('DASHBOARD_REFRESH_CONTROL',JSON.stringify({startedAt:control.startedAt||Date.now(),failed:true,stage:stage,code:ccRefreshErrorCode_(error)}));
      return ccDashboardCache_(filters,'FAILED');
    }
  }finally{maintenance.releaseLock();}
}
