/* Dashboard membership, one-time email verification and retention/deletion policy. */
function ccEnsureSheet_(book,name,headers){var s=book.getSheetByName(name);if(!s){s=book.insertSheet(name);s.getRange(1,1,1,headers.length).setValues([headers]);s.setFrozenRows(1);}return s;}
function ccManagementSheets_(book){
  ccEnsureSheet_(book,'대시보드 멤버',['email','displayName','enabled']);
  ccEnsureSheet_(book,'삭제 관리',['ruleId','createdAt','scopeJson','actor','events']);
}
function initializeDashboardManagement(){ccOwner_();return ccLock_(function(){var book=ccBook_();ccManagementSheets_(book);var sheet=book.getSheetByName('대시보드 멤버');if(sheet.getLastRow()===1)ccAppend_(sheet,[['ourhosu@gmail.com','모호성',true],['imvagueness@gmail.com','모호성',true],['gyumin9048@gmail.com','서민규',true],['lakuase2@gmail.com','이현수',true]]);return {members:ccMembers_(),retention:ccRetentionSettings_()};});}
function ccMembers_(){
  var result=[{email:CC_CONFIG.ownerEmail,name:'관리자',enabled:true,role:'ADMIN'}],sheet=ccBook_().getSheetByName('대시보드 멤버');
  if(sheet)ccRows_(sheet,3,500).forEach(function(r){var email=String(r[0]||'').trim().toLowerCase();if(!email)return;var m={email:email,name:String(r[1]||email),enabled:r[2]===true,role:email===CC_CONFIG.ownerEmail?'ADMIN':'VIEWER'};if(email===CC_CONFIG.ownerEmail)result[0]=Object.assign(m,{enabled:true});else result.push(m);});
  return result;
}
function ccMember_(email){return ccMembers_().find(function(m){return m.email===String(email||'').toLowerCase()&&m.enabled;});}
function ccCache_(){return CacheService.getScriptCache();}
function ccCached_(key){var text=ccCache_().get(key);return text?JSON.parse(text):null;}
function ccPut_(key,value,seconds){ccCache_().put(key,JSON.stringify(value),seconds);}
function ccRandom_(){return Utilities.getUuid()+Utilities.getUuid();}
function ccViewer_(auth){
  var email=Session.getActiveUser().getEmail().toLowerCase();
  if(email===CC_CONFIG.ownerEmail)return ccMember_(email);
  if(typeof auth!=='string'||auth.length>160)throw new Error('DASHBOARD_ACCESS_DENIED');
  var session=ccCached_('session:'+ccHash_(auth)),member=session&&session.expires>Date.now()&&ccMember_(session.email);
  if(!member)throw new Error('DASHBOARD_ACCESS_DENIED');return member;
}
function ccAdmin_(auth){var m=ccViewer_(auth);if(m.email!==CC_CONFIG.ownerEmail)throw new Error('ADMIN_ONLY');return m;}
function getDashboardSession(auth){try{return {viewer:ccViewer_(auth)};}catch(e){return {viewer:null};}}
function ccMailCode_(email,purpose,extra){
  var cache=ccCache_(),rate='mail:'+ccHash_(email),global=ccCached_('mail-global')||{count:0,until:Date.now()+60000};
  if(cache.get(rate)||global.until>Date.now()&&global.count>=10)throw new Error('인증 메일은 잠시 후 다시 요청해 주세요.');
  if(MailApp.getRemainingDailyQuota()<1)throw new Error('오늘의 무료 메일 발송 한도에 도달했습니다.');
  var id=ccRandom_(),code=String(parseInt(Utilities.getUuid().replace(/-/g,'').slice(0,12),16)%100000000).padStart(8,'0');
  var challenge={email:email,purpose:purpose,hash:ccHash_(id+':'+code),attempts:0,expires:Date.now()+600000,extra:extra};
  ccPut_('code:'+id,challenge,600);cache.put(rate,'1',60);
  if(global.until<=Date.now())global={count:0,until:Date.now()+60000};global.count++;ccPut_('mail-global',global,60);
  try{MailApp.sendEmail({to:email,subject:'[CURTAINCALL] '+(purpose==='LOGIN'?'대시보드 로그인':'로그 삭제 확인')+' 인증번호',body:'인증번호: '+code+'\n10분 이내에 입력하세요. 다른 사람에게 전달하지 마세요.\n'+(extra&&extra.description||'등록된 계정의 대시보드 조회용 인증입니다.')});}
  catch(e){cache.remove('code:'+id);throw new Error('메일 전송에 실패했습니다. 잠시 후 다시 시도해 주세요.');}
  return id;
}
function ccVerifyCode_(id,code,purpose,email){
  if(typeof id!=='string'||id.length>160||typeof code!=='string'||!/^\d{8}$/.test(code))throw new Error('인증번호를 확인해 주세요.');
  var key='code:'+id,c=ccCached_(key);
  if(!c||c.expires<=Date.now()||c.attempts>=5||c.purpose!==purpose||email&&c.email!==email)throw new Error('인증이 만료되었거나 사용할 수 없습니다.');
  c.attempts++;ccPut_(key,c,Math.max(1,Math.ceil((c.expires-Date.now())/1000)));
  if(c.hash!==ccHash_(id+':'+code))throw new Error('인증번호를 확인해 주세요.');
  ccCache_().remove(key);return c;
}
function requestDashboardLogin(email){return ccLock_(function(){email=String(email||'').trim().toLowerCase();var member=email.length<=254&&ccMember_(email);return {challengeId:member?ccMailCode_(email,'LOGIN',null):ccRandom_(),message:'등록된 계정이면 인증 메일이 발송됩니다.'};});}
function completeDashboardLogin(id,code){return ccLock_(function(){var c=ccVerifyCode_(id,code,'LOGIN'),member=ccMember_(c.email);if(!member)throw new Error('DASHBOARD_ACCESS_DENIED');var token=ccRandom_();ccPut_('session:'+ccHash_(token),{email:c.email,expires:Date.now()+21600000},21600);return {auth:token,viewer:member};});}
function logoutDashboard(auth){if(typeof auth==='string'&&auth.length<160)ccCache_().remove('session:'+ccHash_(auth));return true;}
function ccRetentionSettings_(){var raw=PropertiesService.getScriptProperties().getProperty('RAW_RETENTION');try{var v=JSON.parse(raw);if(v&&typeof v.enabled==='boolean'&&Number.isInteger(v.maxFiles)&&v.maxFiles>=1&&v.maxFiles<=10000)return v;}catch(e){}return {enabled:true,maxFiles:1000};}
function getDashboardManagement(auth){ccAdmin_(auth);return {members:ccMembers_(),retention:ccRetentionSettings_()};}
function saveDashboardMember(email,name,enabled,auth){ccAdmin_(auth);email=String(email||'').trim().toLowerCase();name=String(name||'').trim();if(!/^[a-z0-9.!#$%&'*+/=?^_`{|}~-]+@[a-z0-9.-]+\.[a-z]{2,}$/.test(email)||email.length>254||!name||name.length>40||typeof enabled!=='boolean'||email===CC_CONFIG.ownerEmail&&!enabled)throw new Error('멤버 정보를 확인해 주세요.');return ccLock_(function(){var book=ccBook_();ccManagementSheets_(book);var sheet=book.getSheetByName('대시보드 멤버'),rows=ccRows_(sheet,3,500),index=rows.findIndex(function(r){return String(r[0]).toLowerCase()===email;});if(index<0){if(rows.length>=500)throw new Error('멤버 한도에 도달했습니다.');ccAppend_(sheet,[[email,name,enabled]]);}else sheet.getRange(index+2,1,1,3).setValues([[email,name,enabled]]);return {members:ccMembers_(),retention:ccRetentionSettings_()};});}
function saveRawRetention(enabled,maxFiles,auth){ccAdmin_(auth);if(typeof enabled!=='boolean'||!Number.isInteger(maxFiles)||maxFiles<1||maxFiles>10000)throw new Error('보관 개수는 1~10000 사이의 정수로 입력해 주세요.');PropertiesService.getScriptProperties().setProperty('RAW_RETENTION',JSON.stringify({enabled:enabled,maxFiles:maxFiles}));return {members:ccMembers_(),retention:ccRetentionSettings_()};}
function ccRules_(book){var s=book.getSheetByName('삭제 관리');return s?ccRows_(s,5,5000).map(function(r){return JSON.parse(r[2]);}):[];}
function ccDeleted_(e,rules){return rules.some(function(r){return TelemetryCore.versionOf(e)===r.gameVersion&&(r.environment==='ALL'||e.environment===r.environment)&&(r.kind==='SESSIONS'?r.runIds.indexOf(e.runId)>=0:Date.parse(e.occurredAt)<=Date.parse(r.cutoff));});}
function ccDeletionScope_(scope){
  if(!scope||!['VERSION','SESSIONS'].includes(scope.kind)||typeof scope.gameVersion!=='string'||!(/^[0-9]+\.[0-9]+\.[0-9]+(?:[-+][0-9A-Za-z.-]+)?$/.test(scope.gameVersion)||scope.gameVersion==='UNVERSIONED'))throw new Error('삭제 범위를 확인해 주세요.');
  var r={kind:scope.kind,gameVersion:scope.gameVersion,environment:scope.environment,cutoff:new Date().toISOString(),runIds:[]};
  if(!['TEST','LIVE'].includes(r.environment))throw new Error('삭제 환경을 확인해 주세요.');
  if(scope.kind==='SESSIONS'){if(!Array.isArray(scope.runIds)||scope.runIds.length<1||scope.runIds.length>500||!scope.runIds.every(function(id){return typeof id==='string'&&/^[0-9a-f-]{36}$/i.test(id);}))throw new Error('삭제할 세션을 선택해 주세요.');r.runIds=Array.from(new Set(scope.runIds)).sort();}return r;
}
function ccDeletionPreview_(scope,all,surveys){var events=all.filter(function(e){return ccDeleted_(e,[scope]);}),answers=(surveys||[]).filter(function(r){return ccDeleted_(r,[scope]);}),rows=events.concat(answers),groups={TEST:0,LIVE:0};rows.forEach(function(e){groups[e.environment]++;});return {events:events.reduce(function(n,e){return n+(e.sessionSummary?e.sessionSummary.summary.aggregate.eventCount:1);},0),surveys:answers.length,sessions:new Set(rows.map(function(e){return e.runId;}).filter(Boolean)).size,groups:groups,fingerprint:ccHash_(rows.map(function(e){return e.sessionSummary?e.eventId+':'+e.sessionSummary.lastReceivedAt+':'+e.sessionSummary.summary.aggregate.eventCount:e.eventId||e.responseId;}).sort().join('\n'))};}
function previewDashboardDeletion(scope,auth){var member=ccAdmin_(auth);return ccLock_(function(){var s=ccDeletionScope_(scope),p=ccDeletionPreview_(s,ccReadEvents_(ccBook_()),ccSurveyRows_(ccBook_()));if(!p.events&&!p.surveys)throw new Error('삭제할 기록이 없습니다.');var id=ccRandom_();ccPut_('delete-preview:'+id,{scope:s,preview:p,email:member.email,expires:Date.now()+600000},600);return {previewId:id,scope:s,events:p.events,surveys:p.surveys,sessions:p.sessions,groups:p.groups};});}
function requestDashboardDeletionCode(previewId,auth){var member=ccAdmin_(auth);return ccLock_(function(){var p=ccCached_('delete-preview:'+previewId);if(!p||p.email!==member.email||p.expires<=Date.now())throw new Error('삭제 미리보기를 다시 확인해 주세요.');var description='버전 '+p.scope.gameVersion+' / '+p.scope.environment+' / '+p.preview.events+'개 사건 / 설문 '+p.preview.surveys+'개 / '+p.preview.sessions+'개 세션\n'+(p.scope.kind==='SESSIONS'?p.scope.runIds.join('\n'):'선택 환경·버전의 기존 기록을 삭제합니다.');return {challengeId:ccMailCode_(member.email,'DELETE',{previewId:previewId,description:description})};});}
function ccCompactDeleted_(book,rules){if(!rules.length)return 0;if(ccSessionState_()){ccSessionUpdate_(book);return 0;}var sheet=book.getSheetByName('Events'),rows=ccRows_(sheet,TelemetryCore.headers.length,CC_CONFIG.maxEvents),removed=0,start=null,count=0,blocks=0;for(var i=rows.length-1;i>=-1;i--){if(i>=0&&ccDeleted_(TelemetryCore.decode(rows[i]),rules)){start=i+2;count++;}else if(count){sheet.deleteRows(start,count);removed+=count;count=0;if(++blocks>=100)break;}}return removed;}
function executeDashboardDeletion(challengeId,code,confirmation,auth){var member=ccAdmin_(auth);if(confirmation!=='삭제')throw new Error('확인란에 삭제를 입력해 주세요.');return ccMaintenanceLock_(function(){var c=ccVerifyCode_(challengeId,code,'DELETE',member.email),p=ccCached_('delete-preview:'+c.extra.previewId);if(!p||p.email!==member.email||p.expires<=Date.now())throw new Error('삭제 미리보기가 만료되었습니다.');var book=ccBook_(),current=ccDeletionPreview_(p.scope,ccReadEvents_(book),ccSurveyRows_(book));if(current.fingerprint!==p.preview.fingerprint)throw new Error('대상 기록이 바뀌었습니다. 미리보기부터 다시 확인해 주세요.');ccManagementSheets_(book);if(ccRows_(book.getSheetByName('삭제 관리'),5,5000).length>=4999)throw new Error('삭제 이력 보관 한도에 도달했습니다.');var previousSnapshot=ccSnapshotPointer_();PropertiesService.getScriptProperties().setProperty('DASHBOARD_DATA_EPOCH',Utilities.getUuid());ccAppend_(book.getSheetByName('삭제 관리'),[[Utilities.getUuid(),new Date().toISOString(),JSON.stringify(p.scope),member.email,current.events]]);ccCache_().remove('delete-preview:'+c.extra.previewId);var rules=ccRules_(book),retention=null,pending=false;try{ccCompactDeleted_(book,rules);ccPublish_(book,ccReadEvents_(book));ccCompactSurveys_(book,ccReadEvents_(book));retention=ccPruneRaw_(book,rules);}catch(error){pending=true;}try{ccWriteSnapshot_(book,ccReadEvents_(book),previousSnapshot?previousSnapshot.refreshedAt:null,0);}catch(error){pending=true;}return {deletedEvents:current.events,deletedSurveys:current.surveys,pendingCleanup:pending,retention:retention,message:pending?'삭제 요청을 적용해 조회에서 제외했습니다. 물리 정리·통계 게시를 다음 갱신에서 이어갑니다.':'삭제 완료. 미정리 원본은 적재 확인 후 자동 정리됩니다.'};});}
function ccReceipts_(book){var map=Object.create(null);ccRows_(book.getSheetByName('Batches'),7,CC_CONFIG.maxEvents).forEach(function(r){if(r[6]==='IMPORTED')map[r[0]]=r;});return map;}
function ccPruneRaw_(book,rules){
  var sessionState=ccSessionState_(),settings=ccRetentionSettings_(),receipts=ccReceipts_(book),it=DriveApp.getFolderById(CC_CONFIG.rawFolderId).getFiles(),files=[],trashed=0,started=Date.now();
  while(it.hasNext()){var f=it.next();if(/^[0-9a-f-]{36}\.json$/i.test(f.getName()))files.push(f);}
  files.sort(function(a,b){return a.getDateCreated()-b.getDateCreated()||a.getName().localeCompare(b.getName());});var remaining=files.length;
  files.forEach(function(file){try{if(trashed>=100||Date.now()-started>15000)return;var receipt=receipts[file.getName().slice(0,-5)];if(!receipt)return;var over=settings.enabled&&remaining>settings.maxFiles;if(!over&&!rules.length&&!(sessionState&&Object.values(sessionState.sessions).some(function(s){return s.archivedAt;})))return;var raw=file.getBlob().getDataAsString('UTF-8');if(ccHash_(raw)!==receipt[1])return;var batch=TelemetryCore.validate(JSON.parse(raw));if(batch.events.length!==Number(receipt[5]))return;var deleted=batch.events.some(function(e){return ccDeleted_(e,rules);});var expired=sessionState&&batch.events.every(function(e){var saved=sessionState.sessions[SessionCore.key(Object.assign({appSessionId:batch.appSessionId},e))];return saved&&saved.archivedAt;});if(over||deleted||expired){file.setTrashed(true);trashed++;remaining--;}}catch(error){/* Keep unverifiable or inaccessible originals; retry next refresh. */}});
  return {trashed:trashed,remaining:remaining,maxFiles:settings.maxFiles};
}
