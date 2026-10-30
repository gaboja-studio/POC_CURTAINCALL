import bpy, json, numpy as np, struct, zlib
from mathutils import Vector
ROOT='D:/github/POC_CURTAINCALL/Assets/Resources/Models/Objects/HumanCannonProps'
source=bpy.data.scenes['CC_HumanCannonProps_v01']
source.view_layers[0].update()
for previous in list(bpy.data.scenes):
    if previous.name.startswith('CC_Props_UnityExport'):
        for obj in list(previous.objects): bpy.data.objects.remove(obj,do_unlink=True)
        bpy.data.scenes.remove(previous)
export=bpy.data.scenes.new('CC_Props_UnityExport')
bpy.context.window.scene=export
palette=[('Burgundy',0,.72),('Ivory',0,.84),('Navy',0,.91),('Brass',.72,.43),('Iron',.7,.60),('Wood',0,.77),('Leather',0,.8),('Thread',0,.88)]
pix=np.ones((512,1024,4),dtype=np.float32)
for k,(_,metal,rough) in enumerate(palette):
    x=k%4*256; y=k//4*256
    pix[y:y+256,x:x+256,:3]=metal; pix[y:y+256,x:x+256,1]=.12 if k==4 else 1; pix[y:y+256,x:x+256,3]=1-rough
mask=bpy.data.images.new('CC_PropMetalSmooth',width=1024,height=512)
mask.colorspace_settings.name='Non-Color'; mask.pixels.foreach_set(pix.ravel()); mask.filepath_raw=ROOT+'/Textures/CC_PropMetalSmooth.png'
# Raw RGBA data map: image.save inherits RGB output in some Blender setups,
# dropping smoothness and premultiplying metal values. No color management here.
def chunk(kind,data): return struct.pack('>I',len(data))+kind+data+struct.pack('>I',zlib.crc32(kind+data)&0xffffffff)
rgba=np.round(np.clip(pix[::-1],0,1)*255).astype(np.uint8)
raw=b''.join(b'\0'+row.tobytes() for row in rgba)
with open(mask.filepath_raw,'wb') as f:
    f.write(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',1024,512,8,6,0,0,0))+chunk(b'IDAT',zlib.compress(raw,9))+chunk(b'IEND',b''))
mat=bpy.data.materials.new('CC_PropSurface');mat.use_nodes=True
bs=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images['CC_PropAtlas'];mat.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
mt=mat.node_tree.nodes.new('ShaderNodeTexImage');mt.image=mask
channels=mat.node_tree.nodes.new('ShaderNodeSeparateColor');channels.mode='RGB'
mat.node_tree.links.new(mt.outputs['Color'],channels.inputs[0]);mat.node_tree.links.new(channels.outputs[0],bs.inputs['Metallic'])
inverse=mat.node_tree.nodes.new('ShaderNodeMath');inverse.operation='SUBTRACT';inverse.inputs[0].default_value=1
mat.node_tree.links.new(mt.outputs['Alpha'],inverse.inputs[1]);mat.node_tree.links.new(inverse.outputs[0],bs.inputs['Roughness'])
report=[]
for name in ['CC_Cannon','CC_Trampoline']:
    parent=bpy.data.objects[name]; generated=[]
    for category in sorted(set(o.get('export_group') for o in parent.children if o.type=='MESH')):
        clones=[]
        for o in parent.children:
            if o.type!='MESH' or o.get('export_group')!=category:continue
            c=o.copy();c.data=o.data.copy();c.parent=None;export.collection.objects.link(c);c.matrix_world=parent.matrix_world.inverted()@o.matrix_world
            # Primitive UV layer names are localized. Joining mismatched names otherwise
            # creates two channels and leaves the first channel empty on custom meshes.
            c.data.uv_layers.active.name='UVMap'
            c.data.materials.clear();c.data.materials.append(mat)
            for p in c.data.polygons:p.material_index=0
            clones.append(c)
        bpy.ops.object.select_all(action='DESELECT')
        for c in clones:c.select_set(True)
        bpy.context.view_layer.objects.active=clones[0];bpy.ops.object.join();c=bpy.context.object;c.name=category;c.data.name=name+'_'+category
        # Bake all vertex transforms so export group origins equal the functional root.
        bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
        triang=c.modifiers.new('Game triangles','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=triang.name)
        generated.append(c)
    bpy.ops.object.select_all(action='DESELECT')
    for c in generated:c.select_set(True)
    bpy.context.view_layer.objects.active=generated[0]
    bpy.ops.export_scene.fbx(filepath=ROOT+'/Models/'+name+'.fbx',use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,global_scale=1,bake_anim=False,add_leaf_bones=False,use_mesh_modifiers=True,mesh_smooth_type='FACE',path_mode='STRIP')
    for c in generated:
        verts=[c.matrix_world@v.co for v in c.data.vertices]
        report.append({'model':name,'mesh':c.name,'triangles':len(c.data.polygons),'vertices':len(verts),'min':[min(v[i] for v in verts) for i in range(3)],'max':[max(v[i] for v in verts) for i in range(3)]})
json.dump(report,open(ROOT+'/Source~/mesh-report.json','w'),indent=2)
bpy.data.libraries.write(ROOT+'/Source~/human-cannon-props-game.blend',{export},fake_user=True)
bpy.context.window.scene=source
print(json.dumps(report))
