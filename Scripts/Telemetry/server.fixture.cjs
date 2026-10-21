const {test}=require('node:test');const assert=require('node:assert/strict');const vm=require('node:vm');const fs=require('node:fs');const crypto=require('node:crypto');
function fixture(){
 const tables={Events:[[]],Batches:[[]]},files=new Map(),properties=new Map([['INGEST_TOKEN','test-only-token-0000000000000000000000000000']]),cache=new Map(),mail=[];let failReceipt=false,failDelete=false,activeEmail='';
 for(const name of ['운영 상태','묘기 통계','사망 원인','공간 이용','기물 서비스 이용','자금 일계','갱신 이력'])tables[name]=[[]];
 function sheet(name){tables[name]??=[[]];return {getName:()=>name,getLastRow:()=>tables[name].length,getLastColumn:()=>Math.max(...tables[name].map(r=>r.length)),getMaxRows:()=>100000,insertRowsAfter(){},deleteRows(start,count){if(failDelete){failDelete=false;throw new Error('injected deletion failure');}tables[name].splice(start-1,count);},setFrozenRows(){},getFilter:()=>null,setColumnWidths(){},getRange(row,col,count,width){return {getValues:()=>tables[name].slice(row-1,row-1+count).map(r=>r.slice(col-1,col-1+width)),clearContent(){for(let i=row-1;i<row-1+count;i++)tables[name][i]=[];while(tables[name].length>1&&!tables[name].at(-1).length)tables[name].pop();},setNotes(){},createFilter(){},setValue(value){this.setValues([[value]]);},setValues(rows){if(name==='Batches'&&failReceipt){failReceipt=false;throw new Error('injected failure');}rows.forEach((r,i)=>{const target=tables[name][row-1+i]||[];r.forEach((v,j)=>target[col-1+j]=v);tables[name][row-1+i]=target;});}};}};}
 function file(name,raw){return {getName:()=>name,getBlob:()=>({getDataAsString:()=>raw}),getId:()=>name,getDateCreated:()=>new Date('2026-10-21T00:00:00Z'),setTrashed(value){if(value)files.delete(name);}};}
 function iterator(arr){let i=0;return {hasNext:()=>i<arr.length,next:()=>arr[i++]};}
 const folder={getFilesByName:name=>iterator(files.has(name)?[files.get(name)]:[]),getFiles:()=>iterator([...files.values()]),createFile(name,raw){const f=file(name,raw);files.set(name,f);return f;}};
 const context=vm.createContext({
  ContentService:{MimeType:{JSON:'json'},createTextOutput:text=>({text,setMimeType(){return this;}})},
  PropertiesService:{getScriptProperties:()=>({getProperty:k=>properties.get(k)||null,setProperty:(k,v)=>properties.set(k,v)})},CacheService:{getScriptCache:()=>({get:k=>cache.get(k)||null,put:(k,v)=>cache.set(k,v),remove:k=>cache.delete(k)})},MailApp:{getRemainingDailyQuota:()=>100,sendEmail:m=>mail.push(m)},
  Utilities:{getUuid:()=>crypto.randomUUID(),newBlob:s=>({getBytes:()=>Buffer.from(s)}),DigestAlgorithm:{SHA_256:'sha256'},Charset:{UTF_8:'utf8'},computeDigest:(_,s)=>[...crypto.createHash('sha256').update(s).digest()]},
  DriveApp:{getFolderById:()=>folder},MimeType:{PLAIN_TEXT:'text/plain'},
  LockService:{getScriptLock:()=>({tryLock:()=>true,releaseLock(){}})},
  SpreadsheetApp:{openById:()=>({getSheetByName:name=>tables[name]?sheet(name):null,insertSheet:sheet})},Session:{getActiveUser:()=>({getEmail:()=>activeEmail})},HtmlService:{createHtmlOutputFromFile:()=>({setTitle(){return this;}})}
 });
 vm.runInContext(['core.gs','surveys-core.gs','management.gs','surveys-server.gs','server.gs'].map(name=>fs.readFileSync(__dirname+'/'+name,'utf8')).join('\n'),context);
 const batch={schemaVersion:1,batchId:crypto.randomUUID(),senderId:crypto.randomUUID(),appSessionId:crypto.randomUUID(),flushReason:'CHECKPOINT',events:[{eventId:crypto.randomUUID(),sequence:1,monotonicMs:1,occurredAt:'2026-10-21T00:00:00Z',environment:'TEST',playerId:'test',playerNumber:null,networkClientId:null,roomId:null,runId:null,attemptId:null,performanceId:null,breakId:null,sceneType:'TITLE',eventType:'GAME_STARTED',authority:'LOCAL',payload:{}}]};
 const send=(b=batch,token='test-only-token-0000000000000000000000000000')=>JSON.parse(context.doPost({postData:{contents:JSON.stringify({token,batchJson:JSON.stringify(b)})}}).text);
 return {context,tables,files,batch,send,failReceipt:()=>failReceipt=true,failDelete:()=>failDelete=true,properties,cache,mail,setEmail:value=>activeEmail=value,book:{getSheetByName:name=>tables[name]?sheet(name):null,insertSheet:sheet}};
}

module.exports={fixture};
