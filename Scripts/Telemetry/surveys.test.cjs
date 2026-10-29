const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm'),crypto=require('node:crypto');
const ctx=vm.createContext({});for(const n of ['core.gs','surveys-core.gs'])vm.runInContext(fs.readFileSync(__dirname+'/'+n,'utf8'),ctx);const survey=ctx.SurveyCore;
const run='7a9e2d31-8f5b-4cde-9201-1bfa8294a732';
const code=(kind='NIGHT1',slot=0,env='TEST',id='a'.repeat(32))=>['CCS1','0_1_0',env,kind,run.replaceAll('-',''),slot,id,'1792627200'].join('.');
function response(kind='NIGHT1',slot=0,env='TEST',id='a'.repeat(32)){return {...survey.parse(code(kind,slot,env,id),kind),responseId:kind+':'+id,submittedAt:'2026-10-22T00:10:00Z',answers:[{question:'1. 재미',answer:'재미있었다'}],source:'source',sourceRow:2};}
test('code rejects wrong version, form, environment, player and malformed context',()=>{
 assert.equal(survey.parse(code(),'NIGHT1').runId,run);assert.equal(survey.parse(code('EXIT'),'NIGHT1'),null);
 for(const value of [code().replace('0_1_0','0_1_1'),code().replace('.TEST.','.ALL.'),code('NIGHT1',4),'CC_LINK_SAMPLE',''])assert.equal(survey.parse(value,'NIGHT1'),null);
});
test('new survey codes preserve exact patch, campaign and round while old codes remain supported',()=>{
 const modern=code().replace('CCS1.0_1_0','CCS2.0_2_3')+'.M2_NIGHT.2',r=survey.parse(modern,'NIGHT1');
 assert.equal(r.gameVersion,'0.2.3');assert.equal(r.surveyId,'M2_NIGHT');assert.equal(r.surveyRound,2);
 assert.equal(survey.parse(code(),'NIGHT1').surveyId,'M1_NIGHT1');
 for(const bad of [modern.replace('.M2_NIGHT.2','.M2_NIGHT.0'),modern.replace('.M2_NIGHT.2','.../bad.2'),modern.replace('0_2_3','no-version')])assert.equal(survey.parse(bad,'NIGHT1'),null);
 assert.equal(survey.decode(survey.row({...response(),...r})).surveyRound,2);
 const a={...response(),...r},b={...response('NIGHT1',1),...r,surveyRound:3};
 assert.equal(survey.summary([a,b],[],{environment:'TEST',gameVersion:'0.2.3',surveyCampaign:'M2_NIGHT:2'}).total,1);
 assert.equal(survey.summary([a,b],[],{environment:'TEST',gameVersion:'0.2.0'}).total,0);
 assert.equal(survey.summary([a,b],[],{environment:'TEST',gameVersion:'0.2.3'}).campaignOptions.length,2);
});
test('survey sheet publication separates patch versions and rounds and preserves legacy defaults',()=>{
 const {fixture}=require('./server.fixture.cjs'),f=fixture(),a=response(),b={...response(),gameVersion:'0.2.3',surveyId:'M2_NIGHT',surveyRound:2};
 f.context.ccPublishSurveys_(f.book,[a,b],[]);
 const rows=f.tables['설문 통계'].slice(1);assert.equal(rows.length,2);assert.equal(rows[0][0],'0.1.0');assert.equal(rows[1][0],'0.2.3');assert.equal(rows[1][8],'M2_NIGHT');assert.equal(rows[1][9],2);
 assert.equal(f.context.ccSurveySources_().length,2);
});
test('response reader tolerates owner spacing, excludes unknown link and deduplicates the same invitation',()=>{
 const header=['타임스탬프','방코드를 입력해주세요','1. 재미','플레이 기록 연결 코드 (자동입력)'];
 const row=['2026-10-22T00:01:00Z','WRONG','재미있었다',code()],d=survey.read([header,row,row,['2026-10-22T00:01:00Z','','보통이었다','']],'NIGHT1','sheet');
 assert.equal(d.responses.length,1);assert.equal(d.duplicates,1);assert.equal(d.unlinked,1);assert.equal(d.responses[0].answers.length,1);
 assert.equal(d.responses[0].runId,run);assert.equal(survey.read([['타임스탬프'],['date']],'NIGHT1','sheet').missingHeader,true);
});
test('survey-only counts work without logs and filters cannot mix environment, player, version or form',()=>{
 const rows=[response(),response('NIGHT1',1,'TEST','b'.repeat(32)),response('EXIT'),response('NIGHT1',0,'LIVE')];
 assert.equal(survey.summary(rows,[],{environment:'TEST',gameVersion:'0.1.0'}).total,2);
 assert.equal(survey.summary(rows,[],{environment:'TEST',gameVersion:'0.1.0',surveyPlayer:'0'}).total,1);
 assert.equal(survey.summary(rows,[],{environment:'TEST',gameVersion:'0.1.1'}).total,0);
 assert.equal(survey.summary(rows,[],{environment:'TEST',surveyKind:'EXIT'}).total,1);
 assert.equal(survey.summary(rows,[],{environment:'TEST'}).matched,0);
});
test('joins use run and slot rather than login or color, excluding events after opening',()=>{
 const r=response('NIGHT1',1),event={environment:'TEST',gameVersion:'0.1.0',runId:run,playerNumber:0,eventType:'PLAYER_DIED',authority:'HOST',occurredAt:'2026-10-21T23:59:00Z',payload:{cause:'FALL'}};
 assert.equal(survey.summary([r],[event],{environment:'TEST'}).matched,0);
 const d=survey.summary([r],[{...event,playerNumber:1},{...event,playerNumber:1,occurredAt:'2026-10-22T00:20:00Z'}],{environment:'TEST'});
 assert.equal(d.matched,1);assert.equal(d.rows[0].deaths.length,1);
});
test('checkbox percent is per answering person and text is kept separate from ranked choices',()=>{
 const a=response('EXIT'),b=response('EXIT',1,'TEST','b'.repeat(32));
 a.answers=[{question:'4. 지루한 구간 (복수 선택 가능)',answer:'외줄타기, 인간 대포'},{question:'10. 경험',answer:'=HYPERLINK("bad")'}];
 b.answers=[{question:'4. 지루한 구간 (복수 선택 가능)',answer:'인간 대포, 자유의견, 쉼표'}];
 const d=survey.summary([a,b],[],{environment:'TEST',surveyKind:'EXIT'}),q=d.questions[0];
 assert.equal(q.samples,2);assert.equal(q.options.find(o=>o.label==='인간 대포').rate,1);assert.equal(q.options.find(o=>o.label==='기타 응답').count,1);assert.equal(d.questions[1].text,true);
 assert.equal(d.questions[1].options.length,0);assert.equal(d.rows[0].answers[1].answer,a.answers[1].answer);
});
test('snapshot row roundtrip preserves context and pagination retains full aggregate',()=>{
 const a=response();assert.equal(survey.decode(survey.row(a)).occurredAt,a.occurredAt);
 const rows=Array.from({length:105},(_,i)=>response('NIGHT1',i%4,'TEST',crypto.randomBytes(16).toString('hex'))),d=survey.summary(rows,[],{environment:'TEST',surveyOffset:100});
 assert.equal(d.total,105);assert.equal(d.rows.length,5);assert.equal(d.questions[0].samples,105);
});
test('stored survey deletion applies rules before read, compact and re-import',()=>{
 const {fixture}=require('./server.fixture.cjs'),f=fixture(),r=response();f.tables['설문 응답']=[Array.from(survey.columns),Array.from(survey.row(r))];
 const scope={kind:'SESSIONS',gameVersion:'0.1.0',environment:'TEST',runIds:[run],cutoff:new Date().toISOString()};
 assert.equal(f.context.ccDeletionPreview_(scope,[],f.context.ccSurveyRows_(f.book)).surveys,1);
 f.tables['삭제 관리']=[['ruleId','createdAt','scopeJson','actor','events'],['rule','now',JSON.stringify(scope),'owner',0]];
 assert.equal(f.context.ccSurveyRows_(f.book).length,0);f.context.ccCompactSurveys_(f.book,[]);assert.equal(f.tables['설문 응답'].length,1);
 assert.equal(f.context.ccDeleted_(r,[scope]),true);assert.equal(f.context.ccDeleted_({...r,environment:'LIVE'},[scope]),false);
});
test('survey calendar prefers play start, falls back to invitation time and filters every statistic',()=>{
 const a=response(),b={...response('NIGHT1',1,'TEST','b'.repeat(32)),runId:'11111111-1111-4111-8111-111111111111',occurredAt:'2026-10-23T00:00:00Z'};
 const play={runId:run,environment:'TEST',gameVersion:'0.1.0',occurredAt:'2026-10-20T15:01:00Z',playerNumber:0,eventType:'RUN_STARTED',payload:{}};
 let d=survey.summary([a,b],[play],{environment:'TEST',surveyDate:'2026-10-21'});assert.equal(d.total,1);assert.equal(d.questions[0].samples,1);assert.equal(d.runLabels.find(r=>r.id===run).dateSource,'PLAY');assert.equal(d.runLabels.find(r=>r.id===b.runId).dateSource,'SURVEY');
 d=survey.summary([a,b],[play],{environment:'TEST',surveyDate:'2026-10-23',surveyRunId:run});assert.equal(d.total,1);assert.equal(d.runId,'');assert.equal(d.rows[0].runId,b.runId);
 assert.equal(survey.summary([a,b],[play],{environment:'TEST',surveyDate:''}).total,2);
});
