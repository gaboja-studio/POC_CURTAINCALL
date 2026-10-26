/* Read response Sheets only. The owner edits/publishes both Google Forms. */
var CC_SURVEYS=[{kind:'NIGHT1',id:'1mPuy_fgd1zIQJ-V5NWf5GmVbG7_N7cOLu9n8XR_5K60',sheet:'설문지 응답 시트1'},
  {kind:'EXIT',id:'1m7ovUScxvyCJVJlWaAiRNtWlqEIK-zrgnAjOCpGpV7o',sheet:'설문지 응답 시트1'}];
function ccSurveySources_(){
  var raw=PropertiesService.getScriptProperties().getProperty('SURVEY_EXTRA_SOURCES'),extra=raw?JSON.parse(raw):[];
  if(!Array.isArray(extra)||extra.length>18||extra.some(function(s){return !s||!['NIGHT1','EXIT'].includes(s.kind)||!(/^[A-Za-z0-9_-]{10,100}$/.test(s.id))||typeof s.sheet!=='string'||!s.sheet.length||s.sheet.length>100;}))throw new Error('SURVEY_SOURCE_CONFIGURATION');
  var seen=new Set();return CC_SURVEYS.concat(extra).filter(function(s){var key=s.id+':'+s.sheet+':'+s.kind;if(seen.has(key))return false;seen.add(key);return true;});
}
function ccSurveyRows_(book){var s=book.getSheetByName('설문 응답'),rules=ccRules_(book);return s?ccRows_(s,SurveyCore.columns.length,10000).map(SurveyCore.decode).filter(function(r){return !ccDeleted_(r,rules);}):[];}
function ccImportSurveys_(book,all){
  var responses=[],status=[],rules=ccRules_(book);
  ccSurveySources_().forEach(function(source){
    var sheet=SpreadsheetApp.openById(source.id).getSheetByName(source.sheet);
    if(!sheet)throw new Error('설문 응답 탭을 찾을 수 없습니다: '+source.kind);
    if(sheet.getLastRow()>5001||sheet.getLastColumn()>100)throw new Error('CAPACITY_LIMIT_Survey_'+source.kind);
    var values=sheet.getLastRow()?sheet.getRange(1,1,sheet.getLastRow(),sheet.getLastColumn()).getValues():[];
    var result=SurveyCore.read(values,source.kind,source.id);
    responses.push.apply(responses,result.responses.filter(function(r){return !ccDeleted_(r,rules);}));
    status.push([new Date().toISOString(),source.kind,result.responses.length,result.unlinked,result.duplicates,result.missingHeader?'연결 질문 없음':'OK']);
  });
  // Gather and validate both sources before replacing any stored snapshot.
  ccEnsureSheet_(book,'설문 응답',SurveyCore.columns).getRange(1,1,1,SurveyCore.columns.length).setValues([SurveyCore.columns]);ccReplace_(book,'설문 응답',responses.map(SurveyCore.row));
  ccEnsureSheet_(book,'설문 동기화',['updatedAt','kind','linked','unlinked','duplicates','status']);ccReplace_(book,'설문 동기화',status);
  ccPublishSurveys_(book,responses,all);
}
function ccPublishSurveys_(book,responses,all){
  var headers=['gameVersion','environment','kind','question','answer','count','samples','rate','surveyId','surveyRound'];
  ccEnsureSheet_(book,'설문 통계',headers).getRange(1,1,1,headers.length).setValues([headers]);var rows=[],groups=new Map();
  responses.forEach(function(r){var id=r.surveyId||'M1_'+r.kind,round=r.surveyRound||1,key=JSON.stringify([r.gameVersion,r.environment,r.kind,id,round]);groups.set(key,{gameVersion:r.gameVersion,environment:r.environment,surveyKind:r.kind,surveyCampaign:id+':'+round,id:id,round:round});});
  groups.forEach(function(f){SurveyCore.summary(responses,ccSurveyEvents_(all),f).questions.forEach(function(q){q.options.forEach(function(o){rows.push([f.gameVersion,f.environment,f.surveyKind,q.question,o.label,o.count,q.samples,o.rate,f.id,f.round]);});});});
  // Prefix formula-like user text when writing native cells; the response JSON remains unchanged.
  ccReplace_(book,'설문 통계',rows.map(function(row){return row.map(function(v){return typeof v==='string'&&/^[=+@-]/.test(v)?"'"+v:v;});}));
}
function ccSurveyView_(book,all,filters){
  return ccSurveyFromSnapshot_(ccSurveySnapshot_(book),all,filters);
}
function ccSurveySnapshot_(book){var s=book.getSheetByName('설문 동기화');return {rows:ccSurveyRows_(book),status:s?ccRows_(s,6,20).map(function(r){return {kind:r[1],updatedAt:r[0],unlinked:Number(r[3]),duplicates:Number(r[4]),message:r[5]};}):[],error:PropertiesService.getScriptProperties().getProperty('SURVEY_SYNC_ERROR')||''};}
function ccSurveyFromSnapshot_(snapshot,all,filters){var view=SurveyCore.summary(snapshot.rows,ccSurveyEvents_(all),filters);view.status=snapshot.status;view.error=snapshot.error;return view;}
function ccCompactSurveys_(book,all){
  if(!book.getSheetByName('설문 응답'))return;var rows=ccSurveyRows_(book);
  ccReplace_(book,'설문 응답',rows.map(SurveyCore.row));ccPublishSurveys_(book,rows,all);
}
