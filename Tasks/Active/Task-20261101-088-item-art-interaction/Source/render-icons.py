import bpy
from mathutils import Vector
scene=bpy.data.scenes['CC_FieldKit_088'];bpy.context.window.scene=scene
camera=scene.camera
ids=['rescue-gloves','sticky-gloves','balloon-pants','spring-shoes','magician-hairpin','stake-puller','uv-lamp','route-key']
def render_icon(id,slot):
 for key in ids:
  if key in scene.objects:scene.objects[key].hide_render=key!=id;scene.objects[key].hide_set(key!=id)
 o=scene.objects[id];bpy.context.view_layer.update();center=sum([o.matrix_world@Vector(v) for v in o.bound_box],Vector())/8
 camera.location=center+Vector((.7 if not slot else .3,-1.4,.55));camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler()
 camera.data.ortho_scale=max(o.dimensions)*1.38
 scene.render.filepath='D:/github/POC_CURTAINCALL/Assets/Resources/ItemArt/Icons/'+id+('-slot' if slot else '-shop')+'.png'
 bpy.ops.render.render(write_still=True)
 print('ICON',id,slot)
for id in selected_ids:
 for slot in [False,True]:render_icon(id,slot)
