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
    nextRefreshAt:next?new Date(next).toISOString():null,serverNow:new Date(now).toISOString(),stage:c.stage||'',processing:ccSessionStatus_(),lastError:c.failed?(c.code==='SESSION_MIGRATION_PENDING'?'기존 기록을 세션 요약으로 전환 중입니다. 이전 완료 결과를 유지하며 다음 갱신에서 이어갑니다.':c.stage==='IMPORT'&&p&&p.refreshedAt===c.publishedAt?'일부 로그 적재가 지연되고 있습니다. 이미 적재된 기록을 표시합니다.':'최근 갱신을 완료하지 못했습니다. 이전 완료 결과를 표시합니다.') :''};
}
function ccScopeRefreshState_(state,index,environment,version){
  var g=index&&index.groups[environment],entry=g&&Object.prototype.hasOwnProperty.call(g.versionsData,version)&&g.versionsData[version];
  state.revision=entry?[entry.records,entry.overview].join(':'):'';
  var props=PropertiesService.getScriptProperties(),time=Math.max(Number(props.getProperty('DASHBOARD_REFRESH_FULL')||0),Number(props.getProperty('DASHBOARD_REFRESH_'+environment+'_'+version)||0));
  if(time){state.refreshedAt=new Date(time).toISOString();state.nextRefreshAt=new Date(time+CC_REFRESH_INTERVAL_MS).toISOString();}
  return state;
}
function getDashboardRefreshState(auth,environment,version){ccViewer_(auth);var p=ccSnapshotPointer_(),state=ccRefreshState_(p);
  return environment&&version?ccScopeRefreshState_(state,p&&ccDriveJson_(p.indexId),environment,version):state;}
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
function ccGameSignature_(events){
  return ccHash_('activity2:'+JSON.stringify(events.map(function(e){var s=e.sessionSummary;
    return s?[s.key,s.detailFile,s.archivedAt,s.lastReceivedAt,s.summary.aggregate.eventCount,!!s.summary.aggregate.activities]:[e.eventId,e.receivedAt];
  }).sort(function(a,b){return String(a[0]).localeCompare(String(b[0]));})));
}
function ccWriteSnapshot_(book,all,refreshedAt,importedBatches,publishSheets,scope){
  var folder=ccSnapshotFolder_(),revision=Utilities.getUuid(),epoch=ccDataEpoch_(),previous=ccSnapshotPointer_();
  var survey=ccSurveySnapshot_(book),index={revision:revision,epoch:epoch,groups:{}};
  var old=previous?ccDriveJson_(previous.indexId):null,dirty=new Set();
  if(old&&!scope)Object.keys(old.groups).forEach(function(env){Object.keys(old.groups[env].versionsData).forEach(function(v){dirty.add(env+'/'+v);});});
  function save(suffix,value){var file=folder.createFile('cc-dashboard-'+revision+'-'+suffix+'.json',JSON.stringify(value),MimeType.PLAIN_TEXT);return file.getId();}
  ['TEST','LIVE'].forEach(function(environment){
    if(scope&&scope.environment!==environment){index.groups[environment]=old&&old.groups[environment]||{versions:[],versionsData:{}};return;}
    var versions=TelemetryCore.versionSelection(all.concat(survey.rows),environment).options,group={versions:versions,versionsData:{}};index.groups[environment]=group;
    if(scope){var saved=old&&old.groups[environment];group.versionsData=Object.assign({},saved&&saved.versionsData);group.versions=Array.from(new Set([scope.gameVersion].concat(saved?saved.versions:[])));versions=[scope.gameVersion];}
    versions.forEach(function(version,i){
      var events=all.filter(function(e){return e.environment===environment&&TelemetryCore.versionOf(e)===version;});
      var s={rows:survey.rows.filter(function(r){return r.environment===environment&&TelemetryCore.versionOf(r)===version;}),status:survey.status,error:survey.error};
      var game=ccGameSignature_(events),prior=old&&old.groups[environment]&&old.groups[environment].versionsData[version],key=environment+'/'+version;
      var answers=ccHash_(JSON.stringify(s.rows));
      if(prior&&prior.game===game){
        dirty.delete(key);
        if(prior.answers===answers){group.versionsData[version]=prior;return;}
      }else dirty.add(key);
      group.versionsData[version]={game:game,answers:answers,records:save(environment+'-'+i+'-records',{events:events,surveys:s}),
        overview:save(environment+'-'+i+'-overview',ccDashboard_(events,{environment:environment,gameVersion:version},s))};
    });
  });
  if(publishSheets&&(!old||dirty.size))ccPublish_(book,all,old||scope?dirty:null);
  index.surveyStatus=survey.status;index.surveyError=survey.error;
  var indexId=save('index',index),pointer={revision:revision,epoch:epoch,indexId:indexId,previousIndexId:previous?previous.indexId:null,
    refreshedAt:refreshedAt||new Date().toISOString(),importedBatches:importedBatches||0,previousRevision:previous?previous.revision:''};
  // All files are complete before this small pointer is published. Old readers retain a consistent generation.
  // Refresh, rebuild and deletion already hold the deploying owner's maintenance lock.
  // The upload lock protects raw-file receipts, never the dashboard pointer.
  if(ccDataEpoch_()!==epoch)throw new Error('SNAPSHOT_INVALIDATED');PropertiesService.getScriptProperties().setProperty('DASHBOARD_SNAPSHOT',JSON.stringify(pointer));
  return pointer;
}
function getDashboardVersions(environment,auth){
  ccViewer_(auth);if(!['LIVE','TEST'].includes(environment))throw Error('INVALID_ENVIRONMENT');
  var p=ccSnapshotPointer_(),g=p&&ccDriveJson_(p.indexId).groups[environment];
  return Array.from(new Set(['0.2.1'].concat(g?g.versions:['0.2.0','0.1.0'])));
}
function ccDashboardCache_(filters,action){
  filters=Object.assign({},filters||{});var pointer=ccSnapshotPointer_(),state=ccRefreshState_(pointer),data;
  if(pointer){
    var index=ccDriveJson_(pointer.indexId),environment=filters.environment||'LIVE',group=Object.prototype.hasOwnProperty.call(index.groups,environment)?index.groups[environment]:null;
    var versions=group?group.versions.slice():['0.1.0'],version=filters.gameVersion||versions[0];
    if(filters.sharedScope&&versions.indexOf('0.2.1')<0)versions.unshift('0.2.1');if(versions.indexOf(version)<0)versions.push(version);filters.gameVersion=version;
    var entry=group&&Object.prototype.hasOwnProperty.call(group.versionsData,version)?group.versionsData[version]:null;
    var overview=entry&&!filters.sharedScope?ccDriveJson_(entry.overview):null,defaults=overview?{timelineRunId:overview.view.timeline.runId,timelinePhase:overview.view.timeline.phaseId,timelineAttemptId:overview.view.timeline.attemptId,surveyKind:'NIGHT1',fundsInterval:'DAY'}:{};
    var customized=['sharedScope','runId','fundsRunId','fundsDate','timelineRunId','timelineDate','timelinePhase','timelineAttemptId','timelineOffset','offset','surveyKind','surveyCampaign','surveyDate','surveyRunId','surveyPlayer','surveyOffset','fundsInterval'].some(function(k){return filters[k]!==undefined&&filters[k]!==null&&filters[k]!==''&&(filters[k]!==0||k==='surveyPlayer')&&filters[k]!==defaults[k];});
    var key='dashboard-view:activity2:'+pointer.revision+':'+ccHash_(JSON.stringify(filters));
    data=ccCacheJsonGet_(key);
    if(!data){
      if(entry&&!customized)data=overview;
      else{var records=entry?ccDriveJson_(entry.records):{events:[],surveys:{rows:[],status:[],error:''}};data=ccDashboard_(records.events,filters,records.surveys);}
      data.versionOptions=versions;data.gameVersion=version;data.timeline=[];ccCacheJsonPut_(key,data);
    }
    // Deletion invalidates an in-flight old read as well as all later cache hits.
    if(pointer.epoch!==ccDataEpoch_())return ccDashboardCache_(filters,'INVALIDATED');
    if(filters.sharedScope)ccScopeRefreshState_(state,index,environment,version);if(data.surveys&&index.surveyStatus){data.surveys.status=index.surveyStatus;data.surveys.error=index.surveyError||'';}
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
  var scope=filters&&filters.sharedScope?{environment:filters.environment,gameVersion:filters.gameVersion}:null;
  if(scope&&(!['TEST','LIVE'].includes(scope.environment)||! /^(?:UNVERSIONED|[0-9]+\.[0-9]+\.[0-9]+(?:[-+][0-9A-Za-z.-]+)?)$/.test(scope.gameVersion)||scope.gameVersion.length>64))throw Error('INVALID_VERSION_SCOPE');
  var state=ccRefreshState_(),scopeKey=scope?'DASHBOARD_REFRESH_'+scope.environment+'_'+scope.gameVersion:'DASHBOARD_REFRESH_FULL';
  function cooling(st){var props=PropertiesService.getScriptProperties(),at=Math.max(Number(props.getProperty(scopeKey)||0),scope?Number(props.getProperty('DASHBOARD_REFRESH_FULL')||0):0);return at&&Date.now()-at<CC_REFRESH_INTERVAL_MS||st.lastError&&st.nextRefreshAt&&Date.parse(st.nextRefreshAt)>Date.now();}
  if(state.refreshing)return ccDashboardCache_(filters,'RUNNING');
  if(cooling(state))return ccDashboardCache_(filters,'CACHED');
  var maintenance=LockService.getUserLock();if(!maintenance.tryLock(1))return ccDashboardCache_(filters,'RUNNING');
  var claimed=false,id=Utilities.getUuid();
  try{
    state=ccRefreshState_();if(!state.refreshing&&!cooling(state)){
      PropertiesService.getScriptProperties().setProperty('DASHBOARD_REFRESH_CONTROL',JSON.stringify({id:id,startedAt:Date.now(),expiresAt:Date.now()+CC_REFRESH_LEASE_MS}));claimed=true;
    }
    if(!claimed)return ccDashboardCache_(filters,state.refreshing?'RUNNING':'CACHED');
    var stage='IMPORT';function report(next){stage=next;var c=ccPropertyJson_('DASHBOARD_REFRESH_CONTROL')||{};c.stage=next;PropertiesService.getScriptProperties().setProperty('DASHBOARD_REFRESH_CONTROL',JSON.stringify(c));}try{report('IMPORT');
      var book=ccBook_(),count=0,importError=null;
      try{var stored=ccSessionState_();if(!stored||!stored.migration)count=ccImport_(book,filters&&filters.priorityRuns,scope);}catch(error){importError=ccRefreshErrorCode_(error);}
      report('SESSIONS');var all=ccSessionUpdate_(book,true);
      report('CACHE');var published=ccWriteSnapshot_(book,all,null,count,true,scope);PropertiesService.getScriptProperties().setProperty(scopeKey,String(Date.now()));
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
