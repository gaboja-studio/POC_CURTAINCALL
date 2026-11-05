// Rendered art stays local; production sends maps only through the authenticated server API.
const fs=require('node:fs'),path=require('node:path');
function assets(){const root=path.resolve(__dirname,'../../.harness/local/telemetry-maps');const manifest=JSON.parse(fs.readFileSync(path.resolve(__dirname,'../../Assets/Resources/TelemetrySpatialMaps.json'),'utf8'));return Object.fromEntries(manifest.maps.map(m=>[m.id,{...m,image:'data:image/jpeg;base64,'+fs.readFileSync(path.join(root,m.id+'.jpg')).toString('base64')}]));}
module.exports=html=>html.replace('/* SPATIAL_MAP_ASSETS */{}',JSON.stringify(module.exports.assets()));
const tightrope=require('./tightrope-map.json');
module.exports.assets=()=>({...assets(),[tightrope.id]:tightrope});
