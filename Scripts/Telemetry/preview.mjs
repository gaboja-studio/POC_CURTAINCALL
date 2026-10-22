// Local-only source viewer and dashboard preview for deployment/QA. No telemetry uploads.
import http from 'node:http';
import fs from 'node:fs/promises';
const files={
 '/core.txt':['core.gs','text/plain; charset=utf-8'],
 '/server.txt':['server.gs','text/plain; charset=utf-8'],
 '/dashboard.txt':['dashboard.html','text/plain; charset=utf-8'],
 '/':['dashboard.html','text/html; charset=utf-8']
};
http.createServer(async(req,res)=>{
 const file=files[new URL(req.url,'http://127.0.0.1').pathname];
 if(!file){res.writeHead(404).end();return;}
 try{res.setHeader('Content-Type',file[1]);res.setHeader('Cache-Control','no-store');res.end(await fs.readFile(new URL(file[0],import.meta.url)));}
 catch{res.writeHead(500).end('Source unavailable');}
}).listen(8766,'127.0.0.1',()=>console.log('Local telemetry preview: http://127.0.0.1:8766'));
