import bpy, math, os, json
from mathutils import Vector
from math import sin,cos,pi
ROOT='D:/github/POC_CURTAINCALL'
OUT=ROOT+'/Assets/Resources/ItemArt'
SOURCE=ROOT+'/Tasks/Active/Task-20261101-088-item-art-interaction/Source'
scene=bpy.data.scenes.new('CC_FieldKit_088');bpy.context.window.scene=scene
palette={'Red':(.48,.035,.025),'Ivory':(.87,.73,.49),'Orange':(.65,.21,.04),'Plum':(.22,.045,.15),'Rubber':(.63,.43,.07),'Steel':(.045,.055,.06),'Brass':(.5,.29,.075),'Rope':(.5,.36,.19),'Violet':(.37,.025,.8),'Velvet':(.22,.015,.025)}
mats={}
for name,col in palette.items():
 m=bpy.data.materials.new('Kit_'+name);m.diffuse_color=(*col,1);m.use_nodes=True
 p=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');p.inputs['Base Color'].default_value=(*col,1);p.inputs['Roughness'].default_value=.46 if name in ('Steel','Brass') else .72;p.inputs['Metallic'].default_value=.7 if name in ('Steel','Brass') else 0
 if name=='Violet':p.inputs['Emission Color'].default_value=(*col,1);p.inputs['Emission Strength'].default_value=2
 mats[name]=m
objects=[];assets={}
def finish(o,name,mat):
 o.name=name;o.data.materials.append(mats[mat]);objects.append(o)
 return o
def ball(name,pos,scale,mat):
 bpy.ops.mesh.primitive_uv_sphere_add(segments=16,ring_count=10,location=pos);o=bpy.context.object;o.scale=scale
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 for p in o.data.polygons:p.use_smooth=True
 return finish(o,name,mat)
def box(name,pos,scale,mat,bevel=.008):
 bpy.ops.mesh.primitive_cube_add(size=1,location=pos);o=bpy.context.object;o.scale=scale;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 if bevel:
  m=o.modifiers.new('Soft handmade edges','BEVEL');m.width=bevel;m.segments=2;bpy.ops.object.modifier_apply(modifier=m.name)
 return finish(o,name,mat)
def tube(name,points,radius,mat):
 c=bpy.data.curves.new(name,'CURVE');c.dimensions='3D';c.resolution_u=2;c.bevel_depth=radius;c.bevel_resolution=2
 sp=c.splines.new('POLY');sp.points.add(len(points)-1)
 for p,co in zip(sp.points,points):p.co=(*co,1)
 o=bpy.data.objects.new(name,c);scene.collection.objects.link(o);bpy.context.view_layer.objects.active=o;o.select_set(True);bpy.ops.object.convert(target='MESH');o=bpy.context.object;o.select_set(False);return finish(o,name,mat)
def ring(name,center,radius,thick,mat,axis='Z'):
 pts=[]
 for i in range(49):
  a=2*pi*i/48;v=(radius*cos(a),radius*sin(a),0) if axis=='Z' else (radius*cos(a),0,radius*sin(a));pts.append(tuple(center[j]+v[j] for j in range(3)))
 return tube(name,pts,thick,mat)
def star(name,pos,size,mat):
 verts=[]
 for y in [-.003,.003]:
  for i in range(10):
   a=pi/2+i*pi/5;r=size*(1 if i%2==0 else .43);verts.append((pos[0]+r*cos(a),pos[1]+y,pos[2]+r*sin(a)))
 faces=[tuple(range(9,-1,-1)),tuple(range(10,20))]+[(i,(i+1)%10,(i+1)%10+10,i+10) for i in range(10)]
 me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.update();o=bpy.data.objects.new(name,me);scene.collection.objects.link(o);return finish(o,name,mat)
def glove(sticky=False):
 for side in [-1,1]:
  x=side*.105; base='Plum' if sticky else 'Orange';patch='Rubber' if sticky else 'Ivory'
  ball('Leather palm',(x,0,.16),(.072,.032,.085),base);ball('Palm patch',(x,-.028,.16),(.052,.009,.06),patch)
  box('Wide stitched cuff',(x,0,.055),(.144,.068,.055),base);box('Wrist band',(x,-.038,.058),(.145,.012,.023),'Brass')
  for j in range(4):
   fx=x+(j-1.5)*.032;h=[.078,.102,.094,.07][j]
   ball('Finger',(fx,0,.225+h/2),(.018,.027,h/2+.018),base);ball('Finger pad',(fx,-.025,.23+h/2),(.012,.007,h/2),patch)
  o=ball('Thumb',(x+side*.081,0,.17),(.027,.029,.062),base);o.rotation_euler.y=side*.6
  for j in range(7):box('Stitch',(x-.05+j*.017,-.042,.06),(.006,.003,.012),'Ivory',.001)
  if sticky:
   for j in range(3):tube('Sticky trail',[(x-.04+j*.04,-.038,.14),(x-.04+j*.04,-.043,.09-j*.008)],.004,'Rubber')
def pants():
 for side in [-1,1]:
  o=ball('Inflated striped leg',(side*.115,0,.23),(.165,.13,.215),'Red')
  o.data.materials.append(mats['Ivory'])
  for p in o.data.polygons:
   a=math.atan2(p.center.y,p.center.x);p.material_index=int((a+pi)/(pi/4))%2
  box('Gathered ankle',(side*.11,0,.035),(.12,.10,.035),'Ivory')
 box('Waist band',(0,0,.40),(.30,.17,.045),'Red')
 for side in [-1,1]:tube('Suspenders',[(side*.10,-.08,.40),(side*.12,-.07,.62),(side*.12,.07,.62),(side*.10,.08,.40)],.014,'Rope');box('Brass buckle',(side*.1,-.09,.43),(.034,.014,.04),'Brass')
 star('Star repair',(.14,-.132,.25),.045,'Rubber')
def shoes():
 for side in [-1,1]:
  x=side*.13
  box('Base plate',(x,0,.018),(.205,.29,.025),'Steel');ring('Brass base',(x,0,.035),.067,.012,'Brass')
  tube('Helical spring',[(x+.059*cos(2*pi*i/20),.059*sin(2*pi*i/20),.045+.13*i/100) for i in range(101)],.011,'Brass')
  box('Leather sole',(x,-.035,.19),(.195,.29,.032),'Steel');ball('Red shoe',(x,-.064,.24),(.095,.15,.055),'Red');ball('Upper shoe',(x,.025,.29),(.08,.072,.105),'Red')
  ring('Cuff',(x,.025,.37),.065,.012,'Ivory');star('Shoe star',(x,-.19,.25),.035,'Ivory')
  for j in range(3):tube('Lacing',[(x-.04,-.08+j*.025,.29),(x+.04,-.07+j*.025,.30)],.004,'Ivory')
def pins():
 for j in range(3):
  x=(j-1)*.055;z=.24-abs(j-1)*.025
  tube('Hairpin prong',[(x-.008,0,0),(x-.008,0,z),(x+.008,0,z),(x+.008,0,.015)],.0035,'Steel')
  if j!=1:star('Star finial',(x,0,z+.02),.028,'Brass')
  else:tube('Crescent',[(x+.023*cos(a),0,z+.025+.023*sin(a)) for a in [pi*.3+i*pi*1.4/24 for i in range(25)]],.006,'Brass')
 tube('Red ribbon',[(-.08,-.01,.075),(0,-.01,.06),(.08,-.01,.075)],.007,'Red')
def puller():
 tube('Forged red shaft',[(0,0,-.07),(0,0,.27),(0,-.025,.35),(0,-.085,.38)],.022,'Red')
 for x in [-.018,.018]:tube('Forked claw',[(x,-.06,.37),(x,-.12,.36),(x,-.145,.325)],.014,'Steel')
 for i in range(25):ring('Rope grip',(0,0,-.06+i*.005),.025,.0035,'Rope')
 ring('End eye',(0,0,-.09),.018,.006,'Brass','Y')
 for z in [.17,.23,.28]:box('Chipped paint',(.021,0,z),(.002,.018,.024),'Steel',.001)
def lamp():
 box('Red base',(0,0,.02),(.14,.13,.04),'Red');box('Red cap',(0,0,.255),(.14,.13,.04),'Red')
 for x in [-.045,0,.045]:tube('UV tube',[(x,0,.055),(x,0,.23)],.011,'Violet')
 for x in [-.065,.065]:
  for y in [-.06,.06]:tube('Cage rib',[(x,y,.035),(x,y,.25)],.005,'Steel')
 for z in [.06,.14,.23]:ring('Cage hoop',(0,0,z),.082,.005,'Steel')
 tube('Side handle',[(.07,0,.045),(.125,0,.045),(.125,0,.23),(.07,0,.23)],.012,'Brass')
 for i in range(19):ring('Tape on handle',(.125,0,.085+i*.006),.014,.003,'Ivory')
 tube('Top hook',[(.04*cos(a),0,.30+.04*sin(a)) for a in [i*pi*1.65/24 for i in range(25)]],.008,'Brass')
 star('Maker mark',(0,-.067,.255),.016,'Ivory')
builders=[('rescue-gloves',lambda:glove()),('sticky-gloves',lambda:glove(True)),('balloon-pants',pants),('spring-shoes',shoes),('magician-hairpin',pins),('stake-puller',puller),('uv-lamp',lamp)]
for id,fn in builders:
 objects=[];bpy.ops.object.select_all(action='DESELECT');fn();bpy.ops.object.select_all(action='DESELECT')
 for o in objects:o.select_set(True)
 bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join();o=bpy.context.object;o.name=id
 scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR');assets[id]=o
 if id=='stake-puller':
  for v in o.data.vertices:v.co.x*=.7;v.co.y*=.7
  o['grip_refined']=True
 bpy.ops.export_scene.fbx(filepath=OUT+'/Models/'+id+'.fbx',use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_UNITS',bake_anim=False)
 o.hide_render=True;o.hide_set(True)
# Transparent, consistent studio icons from the actual production models.
scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True
scene.render.resolution_x=512;scene.render.resolution_y=512;scene.render.resolution_percentage=100
scene.render.film_transparent=True;scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGBA'
scene.world=bpy.data.worlds.new('Field kit studio');scene.world.use_nodes=True
next(n for n in scene.world.node_tree.nodes if n.type=='BACKGROUND').inputs[0].default_value=(.32,.32,.32,1)
for name,loc,power,size in [('Key',(-2,-3,4),320,3),('Fill',(3,-1,2),180,2),('Rim',(1,3,3),350,2)]:
 d=bpy.data.lights.new(name,'AREA');d.energy=power;d.shape='DISK';d.size=size;o=bpy.data.objects.new(name,d);scene.collection.objects.link(o);o.location=loc;o.rotation_euler=(Vector((0,0,.2))-o.location).to_track_quat('-Z','Y').to_euler()
d=bpy.data.cameras.new('Icon camera');camera=bpy.data.objects.new('Icon camera',d);scene.collection.objects.link(camera);scene.camera=camera;d.type='ORTHO'
def render_icon(id,slot=False):
 for key,o in assets.items():o.hide_render=key!=id;o.hide_set(key!=id)
 o=assets[id];bpy.context.view_layer.update();bounds=[o.matrix_world@Vector(v) for v in o.bound_box];center=sum(bounds,Vector())/8
 camera.location=center+Vector((.7 if not slot else .3,-1.4,.55));camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler()
 d.ortho_scale=max(o.dimensions)*1.38;scene.render.filepath=OUT+'/Icons/'+id+('-slot' if slot else '-shop')+'.png';bpy.ops.render.render(write_still=True)
 print('ICON',id,slot)
bpy.ops.wm.save_as_mainfile(filepath=SOURCE+'/field-kit.blend')
print('MODELS_READY',list(assets))
