// Credentials are Script Properties; these generated deployment sources contain none.
const fs=require('node:fs'),path=require('node:path');
const destination=path.resolve(__dirname,'../../.harness/local');fs.mkdirSync(destination,{recursive:true});
const source=['core.gs','surveys-core.gs','management.gs','surveys-server.gs','server.gs','refresh-cache.gs','session-store.gs'].map(name=>fs.readFileSync(path.join(__dirname,name),'utf8')).join('\n');
const mapServer=fs.readFileSync(path.join(__dirname,'spatial-maps-server.gs'),'utf8').replace('/* SPATIAL_MAP_ASSETS */{}',JSON.stringify(require('./map-bundle.cjs').assets()));
fs.writeFileSync(path.join(destination,'telemetry-dashboard-code.txt'),source+'\n'+mapServer);
fs.writeFileSync(path.join(destination,'telemetry-ingest-code.txt'),source.replace('dashboardEnabled:true','dashboardEnabled:false'));
fs.copyFileSync(path.join(__dirname,'dashboard.html'),path.join(destination,'telemetry-dashboard.html'));
fs.copyFileSync(path.join(__dirname,'appsscript.json'),path.join(destination,'telemetry-appsscript.json'));
console.log('Prepared dashboard/ingest sources and manifest in .harness/local. No credentials exported.');
