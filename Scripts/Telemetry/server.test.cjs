const {test}=require('node:test');const assert=require('node:assert/strict');const vm=require('node:vm');const fs=require('node:fs');const crypto=require('node:crypto');
function fixture(){
 const tables={Events:[[]],Batches:[[]]},files=new Map();let failReceipt=false;
 function sheet(name){return {getName:()=>name,getLastRow:()=>tables[name].length,getMaxRows:()=>100000,insertRowsAfter(){},getRange(row,col,count,width){return {getValues:()=>tables[name].slice(row-1,row-1+count).map(r=>r.slice(col-1,col-1+width)),setValues(rows){if(name==='Batches'&&failReceipt){failReceipt=false;throw new Error('injected failure');}rows.forEach((r,i)=>{const target=tables[name][row-1+i]||[];r.forEach((v,j)=>target[col-1+j]=v);tables[name][row-1+i]=target;});}};}};}
 function file(name,raw){return {getName:()=>name,getBlob:()=>({getDataAsString:()=>raw}),getId:()=>name,getDateCreated:()=>new Date('2026-10-21T00:00:00Z')};}
 function iterator(arr){let i=0;return {hasNext:()=>i<arr.length,next:()=>arr[i++]};}
 const folder={getFilesByName:name=>iterator(files.has(name)?[files.get(name)]:[]),getFiles:()=>iterator([...files.values()]),createFile(name,raw){const f=file(name,raw);files.set(name,f);return f;}};
 const context=vm.createContext({
  ContentService:{MimeType:{JSON:'json'},createTextOutput:text=>({text,setMimeType(){return this;}})},
  PropertiesService:{getScriptProperties:()=>({getProperty:()=> 'test-only-token-0000000000000000000000000000'})},
  Utilities:{newBlob:s=>({getBytes:()=>Buffer.from(s)}),DigestAlgorithm:{SHA_256:'sha256'},Charset:{UTF_8:'utf8'},computeDigest:(_,s)=>[...crypto.createHash('sha256').update(s).digest()]},
  DriveApp:{getFolderById:()=>folder},MimeType:{PLAIN_TEXT:'text/plain'},
  LockService:{getScriptLock:()=>({tryLock:()=>true,releaseLock(){}})},
  SpreadsheetApp:{openById:()=>({getSheetByName:sheet})},Session:{getActiveUser:()=>({getEmail:()=>''})}
 });
 vm.runInContext(fs.readFileSync(__dirname+'/core.gs','utf8')+'\n'+fs.readFileSync(__dirname+'/server.gs','utf8'),context);
 const batch={schemaVersion:1,batchId:crypto.randomUUID(),senderId:crypto.randomUUID(),appSessionId:crypto.randomUUID(),flushReason:'CHECKPOINT',events:[{eventId:crypto.randomUUID(),sequence:1,monotonicMs:1,occurredAt:'2026-10-21T00:00:00Z',environment:'TEST',playerId:'test',playerNumber:null,networkClientId:null,roomId:null,runId:null,attemptId:null,performanceId:null,breakId:null,sceneType:'TITLE',eventType:'GAME_STARTED',authority:'LOCAL',payload:{}}]};
 const send=(b=batch,token='test-only-token-0000000000000000000000000000')=>JSON.parse(context.doPost({postData:{contents:JSON.stringify({token,batchJson:JSON.stringify(b)})}}).text);
 return {context,tables,files,batch,send,failReceipt:()=>failReceipt=true,book:{getSheetByName:sheet}};
}
test('ingest ACK is durable and token is absent from raw Drive files',()=>{const f=fixture();assert.equal(f.send().ok,true);assert.equal(f.files.size,1);assert.equal(f.send().ok,true);assert.equal(f.files.size,1);const raw=[...f.files.values()][0].getBlob().getDataAsString();assert.equal(raw.includes('test-only-token'),false);});
test('bad token rejects without writes and same batch ID different bytes conflicts',()=>{const f=fixture();assert.equal(f.send(f.batch,'invalid').error,'UNAUTHORIZED');assert.equal(f.files.size,0);f.send();const changed=JSON.parse(JSON.stringify(f.batch));changed.events[0].payload={changed:true};assert.equal(f.send(changed).error,'BATCH_ID_CONFLICT');assert.equal(f.files.size,1);});
test('partial event append before receipt failure retries exactly once',()=>{const f=fixture();f.send();f.failReceipt();assert.throws(()=>f.context.ccImport_(f.book),/injected failure/);assert.equal(f.tables.Events.length,2);assert.equal(f.tables.Batches.length,1);assert.equal(f.context.ccImport_(f.book),1);assert.equal(f.tables.Events.length,2);assert.equal(f.tables.Batches.length,2);assert.equal(f.context.ccImport_(f.book),0);});
test('duplicate event in a different batch deduplicates and conflicting event is retained for review',()=>{const f=fixture();f.send();f.context.ccImport_(f.book);const b=JSON.parse(JSON.stringify(f.batch));b.batchId=crypto.randomUUID();f.send(b);f.context.ccImport_(f.book);assert.equal(f.tables.Events.length,2);b.batchId=crypto.randomUUID();b.events[0].playerId='changed';f.send(b);assert.throws(()=>f.context.ccImport_(f.book),/EVENT_ID_CONFLICT/);assert.equal(f.tables.Events.length,2);});
test('anonymous execute-as-owner deployment cannot access dashboard/RPC',()=>{const f=fixture();assert.throws(()=>f.context.doGet(),/DASHBOARD_ACCESS_DENIED/);assert.throws(()=>f.context.refreshDashboard({}),/DASHBOARD_ACCESS_DENIED/);assert.throws(()=>f.context.getDashboard({}),/DASHBOARD_ACCESS_DENIED/);assert.throws(()=>f.context.installRefreshTrigger(),/DASHBOARD_ACCESS_DENIED/);assert.throws(()=>f.context.scheduledRefresh(),/DASHBOARD_ACCESS_DENIED/);});
test('formula-like untrusted text is escaped for Sheets',()=>{const f=fixture();assert.equal(f.context.ccText_('=IMPORTXML("url")'),'\'=IMPORTXML("url")');assert.equal(f.context.ccText_(0),0);});

test('version column appends safely and a changed version conflicts on replay',()=>{
 const f=fixture();f.batch.events[0].gameVersion='0.1.0';assert.equal(f.send().ok,true);f.context.ccImport_(f.book);
 assert.equal(f.tables.Events[0][21],'gameVersion');assert.equal(f.tables.Events[1][21],'0.1.0');
 const b=JSON.parse(JSON.stringify(f.batch));b.batchId=crypto.randomUUID();b.events[0].gameVersion='0.2.0';assert.equal(f.send(b).ok,true);
 assert.throws(()=>f.context.ccImport_(f.book),/EVENT_ID_CONFLICT/);assert.equal(f.tables.Events.length,2);
});
test('legacy rows replay without conflict after appending version column',()=>{
 const f=fixture();f.send();f.context.ccImport_(f.book);f.tables.Events[1]=f.tables.Events[1].slice(0,21);
 const b=JSON.parse(JSON.stringify(f.batch));b.batchId=crypto.randomUUID();f.send(b);assert.equal(f.context.ccImport_(f.book),1);assert.equal(f.tables.Events.length,2);
});
test('dashboard defaults to current release, filters every view and never falls back to another version',()=>{
 const f=fixture(),old=f.batch.events[0],current={...old,eventId:crypto.randomUUID(),gameVersion:'0.1.0',runId:crypto.randomUUID()},next={...current,eventId:crypto.randomUUID(),gameVersion:'0.2.0',runId:crypto.randomUUID()};
 let d=f.context.ccDashboard_([old,current,next],{environment:'TEST'});assert.equal(d.gameVersion,'0.2.0');assert.equal(d.eventCount,1);assert.equal(d.view.runOptions[0],next.runId);
 d=f.context.ccDashboard_([old,current,next],{environment:'TEST',gameVersion:'0.1.0',timelineRunId:next.runId});assert.equal(d.eventCount,1);assert.equal(d.view.runOptions.length,1);assert.equal(d.view.timeline.events[0].eventId,current.eventId);
 assert.equal(f.context.ccDashboard_([old],{environment:'TEST',gameVersion:'0.1.0'}).eventCount,0);
 assert.equal(f.context.ccDashboard_([old,current],{environment:'TEST',gameVersion:'UNVERSIONED'}).eventCount,1);
});
