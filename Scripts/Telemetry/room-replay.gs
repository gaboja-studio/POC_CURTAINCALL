// Authenticated, read-only. The browser may request a run ID, never a Drive file ID.
function getRoomReplayPage(environment,version,runId,offset,stamp,auth){
  ccViewer_(auth);
  if(!['LIVE','TEST'].includes(environment)||typeof version!=='string'||version.length>64||
    !/^[a-f0-9-]{36}$/i.test(runId)||!Number.isSafeInteger(offset)||offset<0)throw Error('INVALID_RUN_REQUEST');
  var epoch=ccDataEpoch_(),state=ccSessionState_(),s=state&&state.sessions[JSON.stringify([environment,version,runId])];
  if(!s||ccDeleted_(ccSessionEnvelope_(s),ccRules_(ccBook_())))throw Error('RUN_UNAVAILABLE');
  if(s.archivedAt||!s.detailFile)return {events:[],archived:true};
  var revision=ccHash_(s.detailFile);if(offset&&stamp!==revision)throw Error('RUN_CHANGED');
  var rows=ccSessionDetail_(s),out=[],bytes=0,i=offset;
  for(;i<rows.length;i++){
    var e=rows[i];if(/^(INPUT_KEY|BUTTON_INPUT|MICROPHONE_INPUT)$/.test(e.eventType))continue;
    var size=JSON.stringify(e).length;if(bytes+size>1700000&&out.length)break;
    bytes+=size;out.push(e);
  }
  if(epoch!==ccDataEpoch_())throw Error('RUN_CHANGED');
  return {events:out,next:i<rows.length?i:null,stamp:revision,archived:false};
}
