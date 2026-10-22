/* Read response Sheets only. The owner edits/publishes both Google Forms. */
var CC_SURVEYS=[{kind:'NIGHT1',id:'1mPuy_fgd1zIQJ-V5NWf5GmVbG7_N7cOLu9n8XR_5K60',sheet:'설문지 응답 시트1'},
  {kind:'EXIT',id:'1m7ovUScxvyCJVJlWaAiRNtWlqEIK-zrgnAjOCpGpV7o',sheet:'설문지 응답 시트1'}];
function ccSurveyRows_(book){var s=book.getSheetByName('설문 응답'),rules=ccRules_(book);return s?ccRows_(s,SurveyCore.columns.length,10000).map(SurveyCore.decode).filter(function(r){return !ccDeleted_(r,rules);}):[];}
function ccImportSurveys_(book,all){
  var responses=[],status=[],rules=ccRules_(book);
  CC_SURVEYS.forEach(function(source){
    var sheet=SpreadsheetApp.openById(source.id).getSheetByName(source.sheet);
    if(!sheet)throw new Error('설문 응답 탭을 찾을 수 없습니다: '+source.kind);
    if(sheet.getLastRow()>5001||sheet.getLastColumn()>100)throw new Error('CAPACITY_LIMIT_Survey_'+source.kind);
    var values=sheet.getLastRow()?sheet.getRange(1,1,sheet.getLastRow(),sheet.getLastColumn()).getValues():[];
    var result=SurveyCore.read(values,source.kind,source.id);
    responses.push.apply(responses,result.responses.filter(function(r){return !ccDeleted_(r,rules);}));
    status.push([new Date().toISOString(),source.kind,result.responses.length,result.unlinked,result.duplicates,result.missingHeader?'연결 질문 없음':'OK']);
  });
  // Gather and validate both sources before replacing any stored snapshot.
  ccEnsureSheet_(book,'설문 응답',SurveyCore.columns);ccReplace_(book,'설문 응답',responses.map(SurveyCore.row));
  ccEnsureSheet_(book,'설문 동기화',['updatedAt','kind','linked','unlinked','duplicates','status']);ccReplace_(book,'설문 동기화',status);
  ccPublishSurveys_(book,responses,all);
}
function ccPublishSurveys_(book,responses,all){
  ccEnsureSheet_(book,'설문 통계',['gameVersion','environment','kind','question','answer','count','samples','rate']);var rows=[];
  ['TEST','LIVE'].forEach(function(environment){['NIGHT1','EXIT'].forEach(function(kind){
    SurveyCore.summary(responses,all,{gameVersion:'0.1.0',environment:environment,surveyKind:kind}).questions.forEach(function(q){
      q.options.forEach(function(o){rows.push(['0.1.0',environment,kind,q.question,o.label,o.count,q.samples,o.rate]);});
    });
  });});
  // Prefix formula-like user text when writing native cells; the response JSON remains unchanged.
  ccReplace_(book,'설문 통계',rows.map(function(row){return row.map(function(v){return typeof v==='string'&&/^[=+@-]/.test(v)?"'"+v:v;});}));
}
function ccSurveyView_(book,all,filters){
  var view=SurveyCore.summary(ccSurveyRows_(book),all,filters),s=book.getSheetByName('설문 동기화');
  view.status=s?ccRows_(s,6,2).map(function(r){return {kind:r[1],updatedAt:r[0],unlinked:Number(r[3]),duplicates:Number(r[4]),message:r[5]};}):[];
  view.error=PropertiesService.getScriptProperties().getProperty('SURVEY_SYNC_ERROR')||'';
  return view;
}
function ccCompactSurveys_(book,all){
  if(!book.getSheetByName('설문 응답'))return;var rows=ccSurveyRows_(book);
  ccReplace_(book,'설문 응답',rows.map(SurveyCore.row));ccPublishSurveys_(book,rows,all);
}
