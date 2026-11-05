// Generated deployment injects JPEGs here, never in the unauthenticated HTML response.
var CC_SPATIAL_MAPS=/* SPATIAL_MAP_ASSETS */{};
function getSpatialMaps(ids,auth){
  ccViewer_(auth);
  if(!Array.isArray(ids)||ids.length>24)throw new Error('INVALID_MAP_REQUEST');
  var scenes=new Set(ids.filter(function(id){return typeof id==='string'&&CC_SPATIAL_MAPS[id];}).map(function(id){return CC_SPATIAL_MAPS[id].scene;})),result={};
  Object.keys(CC_SPATIAL_MAPS).forEach(function(id){if(scenes.has(CC_SPATIAL_MAPS[id].scene))result[id]=CC_SPATIAL_MAPS[id];});
  return result;
}
