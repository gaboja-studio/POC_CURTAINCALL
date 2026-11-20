// Generated deployment injects JPEGs here, never in the unauthenticated HTML response.
var CC_SPATIAL_MAPS=/* SPATIAL_MAP_ASSETS */{};
function getSpatialMaps(ids,auth){
  ccViewer_(auth);
  if(!Array.isArray(ids)||ids.length>24)throw new Error('INVALID_MAP_REQUEST');
  var scenes=new Set(ids.filter(function(id){return typeof id==='string'&&CC_SPATIAL_MAPS[id];}).map(function(id){return CC_SPATIAL_MAPS[id].scene;})),result={};
  Object.keys(CC_SPATIAL_MAPS).forEach(function(id){if(scenes.has(CC_SPATIAL_MAPS[id].scene))result[id]=CC_SPATIAL_MAPS[id];});
  ccReadDriveMaps_(ids.filter(function(id){return typeof id==='string'&&!CC_SPATIAL_MAPS[id];}),result);
  return result;
}

function ccReadDriveMaps_(ids,result){
  var packs=[
    {prefix:'WaitingRoom_',file:'10ArZeyoY_aah0YrL7eMnqh_FPJ4VkNxH'},
    {prefix:'escape_route_',file:'1TX56c2mY3XPX0xUX_ZFOeK2gSDZg6hhx'},
    {prefix:'HumanCannon_',file:'1Yd1AJ8sWmc7B_dG2_cRF5BFI9dwz_A0f'}
  ];
  packs.forEach(function(pack){
    var requested=ids.filter(function(id){return id.indexOf(pack.prefix)===0;});
    if(!requested.length)return;
    var maps=JSON.parse(DriveApp.getFileById(pack.file).getBlob().getDataAsString('UTF-8'));
    if(!requested.some(function(id){return Object.prototype.hasOwnProperty.call(maps,id);}))return;
    Object.keys(maps).forEach(function(id){
      var map=maps[id];
      // Older dashboard clients group all cached floors by scene. Keep each survey build's floors together.
      result[id]=Object.assign({},map,{scene:map.scene+' ('+map.mapSet+')'});
    });
  });
}
