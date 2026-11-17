import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
const dir=path.dirname(fileURLToPath(import.meta.url));
const rate=22050,duration=3.6,n=Math.round(rate*duration),tau=2*Math.PI;
let seed=30116;
function rand(){seed=(Math.imul(seed,1664525)+1013904223)>>>0;return seed/4294967296;}
const out={};
function envelope(t,a,b,c){return t<a?0:t<b?Math.sin((t-a)/(b-a)*Math.PI/2):t<c?Math.pow(1-(t-b)/(c-b),1.2):0;}
function make(positive){
 const dry=[new Float64Array(n),new Float64Array(n)];
 function add(i,x,pan=0){if(i<0||i>=n)return;dry[0][i]+=x*Math.sqrt((1-pan)/2);dry[1][i]+=x*Math.sqrt((1+pan)/2);}
 // Nonverbal formant synthesis: intentionally a timing mock, not a human crowd recording.
 for(let v=0;v<30;v++){
  const start=(positive?.18:.38)+rand()*.28,f0=95+rand()*135,pan=(rand()*2-1)*.88,phase=rand()*tau;
  const formants=positive?[690,1170,2450]:[340,790,2250];
  const harmonics=Array.from({length:18},(_,k)=>{const hz=(k+1)*f0;return .06/(k+1)+formants.reduce((s,f,j)=>s+Math.exp(-.5*((hz-f)/(j===2?260:140))**2)*(j===2?.25:1),0)/(k+1)**.6;});
  let phi=phase;
  for(let i=Math.floor(start*rate);i<n;i++){
   const t=i/rate,u=t-start,env=envelope(t,start,start+.2,positive?2.05:2.2);
   if(!env)continue;
   const shift=positive?1+.1*Math.sin(Math.min(1,u)*Math.PI):1-.035*Math.min(u,1);
   phi+=tau*f0*shift*(1+.012*Math.sin(u*25+phase)+.005*Math.sin(u*73))/rate;
   let x=0;for(let k=0;k<18;k++)x+=Math.sin(phi*(k+1)+phase*.13*k)*harmonics[k];
   const trem=.72+.18*Math.sin(u*(positive?18:9)+phase)+.1*Math.sin(u*37+phase);
   add(i,x*env*trem*.0068,pan);
  }
 }
 if(positive){
  for(let clap=0;clap<115;clap++){
   const start=.2+rand()*1.65,pan=(rand()*2-1)*.9,gain=.008+rand()*.014;
   let prev=0;
   for(let j=0;j<rate*.075;j++){
    const t=j/rate,z=rand()*2-1,h=z-prev*.75;prev=z;
    add(Math.floor(start*rate)+j,h*Math.exp(-t*65)*gain,pan);
   }
  }
 }else{
  for(const start of [.35,.76,1.13]){
   for(let j=0;j<rate*.24;j++){
    const t=j/rate,x=(Math.sin(tau*(112*t-28*t*t))*.72+(rand()*2-1)*.28*Math.exp(-t*30))*Math.exp(-t*20)*.085;
    add(Math.floor(start*rate)+j,x,.2);
   }
  }
 }
 // A compact shared identity with opposite pitch direction.
 const pitches=positive?[233.08,277.18]:[277.18,233.08];
 pitches.forEach((freq,k)=>{for(let j=0;j<rate*.23;j++){
  const t=j/rate,env=Math.min(1,t/.012)*Math.exp(-t*19),p=tau*freq*t;
  const x=(Math.sin(p)+.22*Math.sin(2.006*p)+.10*Math.sin(3*p))*.09*env;
  add(Math.floor((.018+k*.115)*rate)+j,x,0);
 }});
 const wet=[new Float64Array(n),new Float64Array(n)];
 for(let c=0;c<2;c++){
  let low=0;for(let i=0;i<n;i++){low+=.52*(dry[c][i]-low);wet[c][i]=low*.70;}
  for(let tap=0;tap<29;tap++){
   const delay=.044+tap*.036+rand()*.024,offset=Math.floor(delay*rate),gain=.12*Math.exp(-delay*2.7),source=dry[(c+tap)%2];
   let lp=0;for(let i=offset;i<n;i++){lp+=.22*(source[i-offset]-lp);wet[c][i]+=lp*gain;}
  }
 }
 for(let i=0;i<n;i++){const fade=Math.min(1,i/(rate*.008),(n-1-i)/(rate*.15));wet[0][i]*=fade;wet[1][i]*=fade;}
 return wet;
}
out.cheer=make(true);out.boo=make(false);
// Loudness match the prototypes; never normalize the quiet tail separately.
function stats(buf){let peak=0,energy=0;for(const ch of buf)for(const x of ch){peak=Math.max(peak,Math.abs(x));energy+=x*x;}return {peak,rms:Math.sqrt(energy/(n*2))};}
const statsBefore=Object.fromEntries(Object.entries(out).map(([k,v])=>[k,stats(v)]));
const scale=Object.fromEntries(Object.entries(statsBefore).map(([k,s])=>[k,.055/s.rms]));
const safety=Math.min(1,.76/Math.max(...Object.keys(out).map(k=>statsBefore[k].peak*scale[k])));
for(const [key,buf] of Object.entries(out)){
 const bytes=Buffer.alloc(44+n*4);bytes.write('RIFF');bytes.writeUInt32LE(36+n*4,4);bytes.write('WAVEfmt ',8);bytes.writeUInt32LE(16,16);bytes.writeUInt16LE(1,20);bytes.writeUInt16LE(2,22);bytes.writeUInt32LE(rate,24);bytes.writeUInt32LE(rate*4,28);bytes.writeUInt16LE(4,32);bytes.writeUInt16LE(16,34);bytes.write('data',36);bytes.writeUInt32LE(n*4,40);
 for(let i=0;i<n;i++)for(let c=0;c<2;c++){buf[c][i]*=scale[key]*safety;bytes.writeInt16LE(Math.round(Math.max(-1,Math.min(1,buf[c][i]))*32767),44+(i*2+c)*2);}
 fs.writeFileSync(path.join(dir,`${key}-synthetic-preview.wav`),bytes);
 const samples=Array.from({length:90},(_,b)=>{let max=0;for(let i=Math.floor(b*n/90);i<Math.floor((b+1)*n/90);i++)max=Math.max(max,Math.abs(buf[0][i]),Math.abs(buf[1][i]));return Number(max.toFixed(4));});
 fs.writeFileSync(path.join(dir,`${key}-waveform.json`),JSON.stringify(samples));
 console.log(JSON.stringify({key,seconds:duration,rate,channels:2,...stats(buf)}));
}
