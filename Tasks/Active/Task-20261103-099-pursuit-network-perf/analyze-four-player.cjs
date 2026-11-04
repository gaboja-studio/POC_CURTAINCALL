// Run from the Unity project root: node Tasks/Active/Task-20261103-099-pursuit-network-perf/analyze-four-player.cjs
// Local-machine clocks only. Four independent PCs require clock alignment before this analysis.
const fs = require('fs'), path = require('path');
const vp = 'Library/VP';
const dirs = ['Logs/FourPlayer', ...(fs.existsSync(vp) ? fs.readdirSync(vp, {withFileTypes:true})
  .filter(x => x.isDirectory()).map(x => path.join(vp, x.name, 'Logs/FourPlayer')) : [])];
const runs = dirs.filter(fs.existsSync).flatMap(d => fs.readdirSync(d).filter(f => f.endsWith('.json'))
  .map(f => ({...JSON.parse(fs.readFileSync(path.join(d, f), 'utf8')), file:path.join(d, f)})));
const quantile = (a, p) => { a = [...a].sort((x,y) => x-y); return a.length ? a[Math.floor((a.length-1)*p)] : null; };
const distance = (a,b) => Math.hypot(a.x-b.x,a.y-b.y,a.z-b.z);
const report = [];
for (const label of ['reliable-a3','unreliable-b1','unreliable-b2','reliable-a4']) {
  const peers = runs.filter(r => r.label === label);
  if (peers.length !== 4 || new Set(peers.map(r => r.localId)).size !== 4) throw Error(`${label}: expected four unique owners`);
  const begin = Math.max(...peers.map(r=>r.frames[0].utcTicks)) + 3e7;
  const end = Math.min(...peers.map(r=>r.frames.at(-1).utcTicks)) - .5e7;
  if (end <= begin) throw Error(`${label}: no common capture window`);
  const owners = new Map(peers.map(r=>[r.localId,r.poses.filter(p=>p.local)]));
  const errors=[], frames=[], rtts=[];
  const results = peers.map(r=>{
    const fs = r.frames.filter(f=>f.utcTicks>=begin&&f.utcTicks<=end);
    frames.push(...fs.map(f=>f.ms)); if(!r.host) rtts.push(...fs.map(f=>f.rtt));
    const es=[];
    for (const p of r.poses) {
      if(p.local||p.utcTicks<begin||p.utcTicks>end) continue;
      const os=owners.get(p.owner); let lo=0,hi=os.length-1;
      while(lo<hi){const m=Math.floor((lo+hi)/2);if(os[m].utcTicks<p.utcTicks)lo=m+1;else hi=m;}
      if(lo===0)continue;
      const a=os[lo-1],b=os[lo],t=(p.utcTicks-a.utcTicks)/(b.utcTicks-a.utcTicks);
      if(!Number.isFinite(t))continue;
      es.push(distance(p.position,{x:a.position.x+(b.position.x-a.position.x)*t,
        y:a.position.y+(b.position.y-a.position.y)*t,z:a.position.z+(b.position.z-a.position.z)*t}));
    }
    errors.push(...es);
    const own=owners.get(r.localId).filter(p=>p.utcTicks>=begin&&p.utcTicks<=end);
    let pathM=0; for(let i=1;i<own.length;i++)pathM+=distance(own[i-1].position,own[i].position);
    const valid=fs.every(f=>f.players===4)&&own.every(p=>p.position.y>=-.2&&p.position.y<.5)&&pathM>10;
    if(!valid)throw Error(`${label} owner ${r.localId}: disconnected, falling, or insufficient movement`);
    return {pid:r.pid,owner:r.localId,host:r.host,samples:fs.length,allFourConnected:true,pathM,
      frameP50:quantile(fs.map(f=>f.ms),.5),frameP95:quantile(fs.map(f=>f.ms),.95),
      errorP50:quantile(es,.5),errorP95:quantile(es,.95),cpuSeconds:r.cpuSeconds};
  });
  report.push({label,seconds:(end-begin)/1e7,samples:errors.length,oneWayDelayMs:peers[0].oneWayDelayMs,
    jitterMs:peers[0].jitterMs,sendLossPercent:peers[0].sendLossPercent,
    frameP50:quantile(frames,.5),frameP95:quantile(frames,.95),rttP50:quantile(rtts,.5),rttP95:quantile(rtts,.95),
    positionErrorP50:quantile(errors,.5),positionErrorP95:quantile(errors,.95),peers:results});
}
fs.writeFileSync(path.join(__dirname,'four-player-lan-ab-20261104.json'),JSON.stringify(report,null,2)+'\n');
console.log(JSON.stringify(report.map(({peers,...summary})=>summary),null,2));
