/* Google-independent survey parsing and aggregation. A prefilled code is a join key, not identity proof. */
var SurveyCore=(function(){
  var header='플레이 기록 연결 코드 (자동 입력)';
  function parse(code,kind){
    var p=String(code||'').split('.');
    var legacy=p.length===8&&p[0]==='CCS1'&&p[1]==='0_1_0',modern=p.length===10&&p[0]==='CCS2';
    var version;try{version=decodeURIComponent(p[1]||'').replace(/_/g,'.');}catch(_){return null;}
    if(!legacy&&(!modern||!/^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$/.test(version)||!(/^[A-Za-z0-9_-]{1,40}$/.test(p[8]))||!(/^[1-9][0-9]{0,8}$/.test(p[9]))))return null;
    if(!['TEST','LIVE'].includes(p[2])||p[3]!==kind||!['NIGHT1','EXIT'].includes(kind)
      ||!(/^[0-9a-f]{32}$/i.test(p[4]))||!(/^[0-3]$/.test(p[5]))||!(/^[0-9a-f]{32}$/i.test(p[6]))||!(/^[0-9]{10}$/.test(p[7])))return null;
    var run=p[4].toLowerCase(),at=new Date(Number(p[7])*1000);
    return {gameVersion:version,surveyId:legacy?'M1_'+kind:p[8],surveyRound:legacy?1:Number(p[9]),environment:p[2],kind:kind,runId:run.slice(0,8)+'-'+run.slice(8,12)+'-'+run.slice(12,16)+'-'+run.slice(16,20)+'-'+run.slice(20),playerNumber:Number(p[5]),invitationId:p[6].toLowerCase(),occurredAt:at.toISOString()};
  }
  function read(values,kind,source){
    var result={responses:[],unlinked:0,duplicates:0,missingHeader:false},headers=values[0]||[],col=headers.findIndex(function(h){return String(h).replace(/\s/g,'')===header.replace(/\s/g,'');}),seen=new Set();
    if(col<0){result.missingHeader=true;result.unlinked=values.slice(1).filter(function(r){return r.some(Boolean);}).length;return result;}
    values.slice(1).forEach(function(row,index){
      if(!row.some(Boolean))return;var context=parse(row[col],kind),date=row[0] instanceof Date?row[0]:new Date(row[0]);
      if(!context||!Number.isFinite(date.getTime())){result.unlinked++;return;}
      if(seen.has(context.invitationId)){result.duplicates++;return;}seen.add(context.invitationId);
      var answers=[],budget=36000;headers.forEach(function(question,i){if(/^\d+\./.test(question)&&row[i]!=null&&String(row[i]).trim()){
        var text=String(row[i]),limit=Math.min(18000,budget);answers.push({question:String(question),answer:text.slice(0,limit),truncated:text.length>limit});budget-=Math.min(text.length,limit);
      }});
      result.responses.push(Object.assign(context,{responseId:kind+':'+context.invitationId,submittedAt:date.toISOString(),answers:answers,source:source,sourceRow:index+2}));
    });return result;
  }
  var multiOptions={NIGHT1:{4:['특별히 없었다 (이 항목만 선택)','무엇을 해야 하는지 파악하기','캐릭터 조작하기','장애물이나 위험을 알아보기','실패하거나 다친 이유 이해하기','팀원과 행동 맞추기','쉬는 시간에 길 찾기','공연 복귀 시점이나 장소 파악하기','버그·끊김·접속 문제']},EXIT:{
    4:['없었다 (이 항목만 선택)','외줄타기','인간 대포','쉬는 시간의 이동·탐색','밤의 구매·정비','사망 후 관전','팀원 준비 대기','로딩'],
    6:['캐릭터 조작감','목표·조작 방법 안내','묘기 난이도','묘기의 다양성','팀원과 협동하는 방식','부상 후 할 수 있는 행동','사망 후 기다리는 시간','탐색·탈출 과정','구매·보상','카메라·화면 정보','소리·음성 채팅','버그·끊김·접속 문제','특별히 없다 (이 항목만 선택)'],
    7:['외줄타기','인간 대포','팀원과 협동해서 위기를 넘기는 경험','팀원의 실수나 예상치 못한 사고','부상 상태로 버티는 경험','관객의 반응과 공연 분위기','쉬는 시간의 탐색·탈출 준비','돈을 모아 구매·정비하는 과정','특별히 없다 (이 항목만 선택)']}};
  function summary(responses,events,filters){
    filters=filters||{};var scoped=responses.filter(function(r){return r.environment===(filters.environment||'LIVE')&&r.gameVersion===(filters.gameVersion||'0.1.0');});
    var kind=filters.surveyKind==='EXIT'?'EXIT':'NIGHT1',base=scoped.filter(function(r){return r.kind===kind;});
    function campaign(r){return (r.surveyId||'M1_'+r.kind)+':'+(r.surveyRound||1);}
    var campaigns=Array.from(new Set(base.map(campaign))).sort(function(a,b){return a.localeCompare(b,undefined,{numeric:true});}),selectedCampaign=campaigns.includes(filters.surveyCampaign)?filters.surveyCampaign:campaigns[campaigns.length-1]||'';
    base=base.filter(function(r){return campaign(r)===selectedCampaign;});
    var runIds=Array.from(new Set(base.map(function(r){return r.runId;}))).sort();
    var starts=new Map(),opened=new Map();events.forEach(function(e){if(e.runId&&e.environment===(filters.environment||'LIVE')&&TelemetryCore.versionOf(e)===(filters.gameVersion||'0.1.0')&&(!starts.has(e.runId)||e.occurredAt<starts.get(e.runId)))starts.set(e.runId,e.occurredAt);});base.forEach(function(r){if(!opened.has(r.runId)||r.occurredAt<opened.get(r.runId))opened.set(r.runId,r.occurredAt);});
    var runLabels=runIds.map(function(id){return {id:id,startedAt:starts.get(id)||opened.get(id),dateSource:starts.has(id)?'PLAY':'SURVEY'};}),labelIndex=new Map(runLabels.map(function(r){return [r.id,r];}));
    function onDate(id){if(!filters.surveyDate)return true;var r=labelIndex.get(id),t=Date.parse(r.startedAt);return Number.isFinite(t)&&new Date(t+9*3600000).toISOString().slice(0,10)===filters.surveyDate;}
    var run=runIds.includes(filters.surveyRunId)&&onDate(filters.surveyRunId)?filters.surveyRunId:'',slot=/^[0-3]$/.test(String(filters.surveyPlayer))?Number(filters.surveyPlayer):null;
    var selected=base.filter(function(r){return onDate(r.runId)&&(!run||r.runId===run)&&(slot===null||r.playerNumber===slot);}),questions=new Map();
    selected.forEach(function(r){r.answers.forEach(function(a){
      var m1=(r.surveyId||'M1_'+kind)==='M1_'+kind;
      if(!questions.has(a.question))questions.set(a.question,{question:a.question,multiple:/복수 선택/.test(a.question),text:m1?kind==='EXIT'&&/^(10|11)\./.test(a.question):/서술|자유.?응답|자유.?의견/.test(a.question),samples:0,options:new Map()});
      var q=questions.get(a.question);q.samples++;
      if(q.text)return;
      var known=multiOptions[kind][parseInt(a.question,10)]||[],choices=q.multiple?a.answer.split(/,\s*/).map(function(choice){return !m1||known.includes(choice)?choice:'기타 응답';}):[a.answer];
      Array.from(new Set(choices.filter(Boolean))).forEach(function(choice){q.options.set(choice,(q.options.get(choice)||0)+1);});
    });});
    var eventIndex=new Map();events.forEach(function(e){
      if(e.environment!==(filters.environment||'LIVE')||TelemetryCore.versionOf(e)!==(filters.gameVersion||'0.1.0')||!Number.isInteger(e.playerNumber)||!TelemetryCore.analysisEvent(e))return;
      var key=e.runId+':'+e.playerNumber;if(!eventIndex.has(key))eventIndex.set(key,[]);eventIndex.get(key).push(e);
    });
    function playerEvents(r){return (eventIndex.get(r.runId+':'+r.playerNumber)||[]).filter(function(e){return Date.parse(e.occurredAt)<=Date.parse(r.occurredAt)+1000;});}
    var rows=selected.map(function(r){var es=playerEvents(r),deaths=es.filter(function(e){return e.eventType==='PLAYER_DIED'&&e.authority==='HOST';});return Object.assign({},r,{matched:es.length>0,deaths:deaths.map(function(e){return e.payload.cause||'UNKNOWN';}),injuries:es.filter(function(e){return e.eventType==='LIMB_SEVERED'&&e.authority==='HOST';}).length,interactions:es.filter(function(e){return e.eventType==='COOP_ACTIVATED'&&e.authority==='HOST';}).length});});
    var offset=Number.isSafeInteger(filters.surveyOffset)?Math.max(0,filters.surveyOffset):0;if(offset>=rows.length)offset=0;
    return {kind:kind,campaign:selectedCampaign,campaignOptions:campaigns,runId:run,playerNumber:slot,runOptions:runIds,runLabels:runLabels,total:selected.length,matched:rows.filter(function(r){return r.matched;}).length,rows:rows.slice(offset,offset+100),offset:offset,
      allRunOptions:Array.from(new Set(scoped.map(function(r){return r.runId;}))),counts:{NIGHT1:scoped.filter(function(r){return r.kind==='NIGHT1';}).length,EXIT:scoped.filter(function(r){return r.kind==='EXIT';}).length},
      questions:Array.from(questions.values()).map(function(q){return {question:q.question,multiple:q.multiple,text:q.text,samples:q.samples,options:Array.from(q.options,function(pair){return {label:pair[0],count:pair[1],rate:pair[1]/q.samples};}).sort(function(a,b){return b.count-a.count||a.label.localeCompare(b.label);})};})};
  }
  var columns=['responseId','kind','gameVersion','environment','runId','playerNumber','invitationId','openedAt','submittedAt','answersJson','sourceSheet','sourceRow','surveyId','surveyRound'];
  function row(r){return [r.responseId,r.kind,r.gameVersion,r.environment,r.runId,r.playerNumber,r.invitationId,r.occurredAt,r.submittedAt,JSON.stringify(r.answers),r.source,r.sourceRow,r.surveyId||'M1_'+r.kind,r.surveyRound||1];}
  function decode(r){return {responseId:r[0],kind:r[1],gameVersion:r[2],environment:r[3],runId:r[4],playerNumber:Number(r[5]),invitationId:r[6],occurredAt:r[7],submittedAt:r[8],answers:JSON.parse(r[9]),source:r[10],sourceRow:r[11],surveyId:r[12]||'M1_'+r[1],surveyRound:Number(r[13])||1};}
  return {header:header,parse:parse,read:read,summary:summary,columns:columns,row:row,decode:decode};
})();
