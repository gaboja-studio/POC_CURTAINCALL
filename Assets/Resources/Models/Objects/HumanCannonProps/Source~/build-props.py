"""Curtain Call original static props. Run in Blender; preserves existing scenes.
Meters, Z up / +Y forward; FBX exports -Z forward / Y up for Unity.
Authoring geometry is retained in a separate scene and library blend.
"""
import bpy, math, random, json, os
import numpy as np
from mathutils import Vector
from math import sin, cos, pi
ROOT = 'D:/github/POC_CURTAINCALL/Assets/Resources/Models/Objects/HumanCannonProps'
random.seed(82)
scene = bpy.data.scenes.new('CC_HumanCannonProps_v01')
bpy.context.window.scene = scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1
palette = [('Burgundy',(0.42,.065,.075),0,.72),('Ivory',(.79,.68,.47),0,.84),('Navy',(.048,.075,.115),0,.91),('Brass',(.52,.32,.115),.72,.43),('Iron',(.07,.075,.073),.7,.60),('Wood',(.22,.105,.042),0,.77),('Leather',(.16,.063,.027),0,.8),('Thread',(.68,.48,.23),0,.88)]
# One original shared atlas, 8 padded swatches. Fine weave + broad uneven wear.
N=1024; tile=256; pix=np.ones((512,1024,4),dtype=np.float32)
rng=np.random.default_rng(82)
yy,xx=np.mgrid[0:tile,0:tile]
for k,(name,col,metal,rough) in enumerate(palette):
    grain=rng.normal(0,.025,(tile,tile))
    broad=.05*np.sin(xx*.061+np.sin(yy*.027)*2)*np.cos(yy*.049)
    weave=.016*((xx%4==0)+(yy%4==0))
    if name=='Wood': broad=.14*np.sin(xx*.15+2*np.sin(yy*.025))+.04*np.sin(xx*.7)
    if name=='Brass': broad=.045*np.sin(yy*.7)+.04*np.sin(xx*.07)*np.cos(yy*.04)
    shade=np.clip(1+grain+broad-weave,.65,1.3)
    # Input color is linear; texture is stored in sRGB by Blender.
    x=(k%4)*tile; y=(k//4)*tile
    pix[y:y+tile,x:x+tile,:3]=np.clip(np.array(col)[None,None,:]*shade[:,:,None],0,1)
im=bpy.data.images.new('CC_PropAtlas',width=1024,height=512)
im.pixels.foreach_set(pix.ravel()); im.filepath_raw=ROOT+'/Textures/CC_PropAtlas.png'; im.file_format='PNG'; im.save()
mats=[]
for k,(name,col,metal,rough) in enumerate(palette):
    m=bpy.data.materials.new('CC_'+name); m.diffuse_color=(*col,1); m.use_nodes=True
    bs=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    bs.inputs['Metallic'].default_value=metal; bs.inputs['Roughness'].default_value=rough
    tex=m.node_tree.nodes.new('ShaderNodeTexImage'); tex.image=im
    m.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color']); mats.append(m)
objects=[]; active_root=None; group='Body'
def finish(o,name,mat=0,smooth=False):
    o.name=name; o.data.materials.clear(); o.data.materials.append(mats[mat]); o.parent=active_root
    o['export_group']=group
    if o.type=='MESH':
        uv=o.data.uv_layers.new(name='UVMap') if not o.data.uv_layers else o.data.uv_layers.active
        uv.name='UVMap'
        coords=np.array([v.co[:] for v in o.data.vertices]); lo=coords.min(axis=0); span=np.maximum(coords.max(axis=0)-lo,.0001)
        for p in o.data.polygons:
            p.use_smooth=smooth
            axes=sorted(range(3),key=lambda a:abs(p.normal[a]))[:2]
            for li in p.loop_indices:
                co=o.data.vertices[o.data.loops[li].vertex_index].co
                u=(co[axes[0]]-lo[axes[0]])/span[axes[0]]; v=(co[axes[1]]-lo[axes[1]])/span[axes[1]]
                uv.data[li].uv=((mat%4+.06+.88*u)/4,(mat//4+.06+.88*v)/2)
    objects.append(o); return o
def mesh(name,vs,fs,mat=0,smooth=False):
    me=bpy.data.meshes.new(name); me.from_pydata(vs,[],fs); me.update()
    o=bpy.data.objects.new(name,me); scene.collection.objects.link(o); return finish(o,name,mat,smooth)
def cube(name,pos,size,mat=0,bevel=.02):
    bpy.ops.mesh.primitive_cube_add(size=1,location=pos); o=bpy.context.object; o.scale=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        b=o.modifiers.new('Soft handmade edges','BEVEL'); b.width=bevel; b.segments=2
        bpy.ops.object.modifier_apply(modifier=b.name)
    return finish(o,name,mat)
def rod(name,a,b,r,mat=4,n=10,r2=None):
    a=Vector(a); b=Vector(b); delta=b-a
    bpy.ops.mesh.primitive_cone_add(vertices=n,radius1=r,radius2=r if r2 is None else r2,depth=delta.length,location=(a+b)*.5)
    o=bpy.context.object; o.rotation_quaternion=delta.to_track_quat('Z','Y'); o.rotation_mode='QUATERNION'; o.rotation_quaternion=delta.to_track_quat('Z','Y')
    return finish(o,name,mat,True)
def ring(name,center,radius,tube,mat=3,normal=(0,0,1),n=48,m=8):
    center=Vector(center); normal=Vector(normal).normalized(); q=Vector((0,0,1)).rotation_difference(normal)
    vs=[]; fs=[]
    for i in range(n):
        a=2*pi*i/n
        for j in range(m):
            b=2*pi*j/m; vs.append(center+q@Vector(((radius+tube*cos(b))*cos(a),(radius+tube*cos(b))*sin(a),tube*sin(b))))
    for i in range(n):
        for j in range(m):
            fs.append((i*m+j,((i+1)%n)*m+j,((i+1)%n)*m+(j+1)%m,i*m+(j+1)%m))
    return mesh(name,vs,fs,mat,True)
def star(name,center,radius,normal=(0,0,1),mat=1,angle=pi/2):
    c=Vector(center); q=Vector((0,0,1)).rotation_difference(Vector(normal)); vs=[c]
    for i in range(10):
        a=angle+i*pi/5; r=radius if i%2==0 else radius*.43
        vs.append(c+q@Vector((r*cos(a),r*sin(a),0)))
    return mesh(name,vs,[(0,i+1,(i+1)%10+1) for i in range(10)],mat)
def root(name):
    o=bpy.data.objects.new(name,None); scene.collection.objects.link(o); return o

# TRAMPOLINE: exact contact envelope, static woven bed.
active_root=root('CC_Trampoline'); tramp=active_root
group='Fabric'
rod('BounceFabric',(0,0,.47),(0,0,.5),2,2,n=96)
star('IvoryCenterStar',(0,0,.501),.93,mat=1)
ring('BedSeam',(0,0,.501),1.96,.008,6,n=96,m=4)
for i in range(96):
    a=2*pi*i/96; b=a+.018
    rod('BedStitch',(1.935*cos(a),1.935*sin(a),.504),(1.935*cos(b),1.935*sin(b),.504),.004,7,n=4)
group='Body'
ring('StructuralFrame',(0,0,.39),2.285,.055,4,n=64)
ring('LowerBrace',(0,0,.19),2.22,.025,4,n=64,m=6)
# Rounded rectangular cross-section pad sectors, retaining gaps for seam shadows.
section=[(2.14,.43),(2.13,.51),(2.17,.61),(2.23,.65),(2.39,.65),(2.455,.61),(2.4753408,.53),(2.46,.45),(2.4,.42),(2.2,.42)]
for k in range(24):
    vs=[]; fs=[]; steps=5
    for i in range(steps+1):
        a=2*pi*(k+(i/steps)*.965)/24
        for r,z in section: vs.append((r*cos(a),r*sin(a),z))
    m=len(section)
    for i in range(steps):
        for j in range(m): fs.append((i*m+j,(i+1)*m+j,(i+1)*m+(j+1)%m,i*m+(j+1)%m))
    fs+=[tuple(reversed(range(m))),tuple(steps*m+j for j in range(m))]
    mesh('RedPaddedSegment_%02d'%k,vs,fs,0,True)
    a=2*pi*(k+.02)/24
    # narrow bound leather panel edge and brass stud
    rod('PadBinding',(2.17*cos(a),2.17*sin(a),.62),(2.43*cos(a),2.43*sin(a),.62),.014,6,6)
    a=2*pi*(k+.5)/24
    rod('RimStud',(2.43*cos(a),2.43*sin(a),.615),(2.43*cos(a),2.43*sin(a),.64),.018,3,8)
for k in range(32):
    a=2*pi*k/32; radial=Vector((cos(a),sin(a),0)); tangent=Vector((-sin(a),cos(a),0))
    pts=[]
    for i in range(25):
        t=i/24; p=radial*(2.005+.13*t)+tangent*(.023*cos(t*8*pi))+Vector((0,0,.5+.023*sin(t*8*pi))); pts.append(p)
    for i in range(24): rod('Spring',pts[i],pts[i+1],.007,4,5)
for k in range(8):
    a=2*pi*(k+.5)/8; x=2.24*cos(a); y=2.24*sin(a)
    rod('BracedLeg',(x,y,.045),(x,y,.43),.047,4,10)
    rod('BrassLegCuff',(x,y,.08),(x,y,.13),.063,3,12)
    rod('RubberFoot',(x,y,0),(x,y,.055),.085,4,12)
    rod('RadialBrace',(x,y,.12),(1.98*cos(a),1.98*sin(a),.39),.022,4,8)
    # Red pennants on four cardinal-adjacent legs.
    if k%2==0:
        c=Vector((2.46*cos(a),2.46*sin(a),.40)); tang=Vector((-sin(a),cos(a),0)); normal=Vector((cos(a),sin(a),0))
        vs=[c+tang*.14+Vector((0,0,.14)),c-tang*.14+Vector((0,0,.14)),c-tang*.13-Vector((0,0,.17)),c-Vector((0,0,.13)),c+tang*.13-Vector((0,0,.17))]
        mesh('HangingLeatherPennant',vs,[(0,1,2,3,4)],0)
        star('PennantStar',c+normal*.006,.085,normal,3)
# Real grab handle stays at x=-2.8 (Unity); Blender y maps to forward.
group='Handle'
for y in (-.55,.55): rod('HandleConnector',(-2.22,y,.47),(-2.8,y,.6),.035,4,10)
rod('CarryGrip',(-2.8,-.68,.6),(-2.8,.68,.6),.055,6,12)
for y in (-.64,.64): rod('GripBrassCollar',(-2.8,y-.035,.6),(-2.8,y+.035,.6),.068,3,12)
for k in range(18): ring('GripWrap',(-2.8,-.5+k/17,.6),.055,.006,7,(0,1,0),12,4)

# CANNON: forward barrel axis fixed to existing muzzle center.
active_root=root('CC_Cannon'); cannon=active_root; group='Body'
d=Vector((0,cos(math.radians(25)),sin(math.radians(25)))); tip=Vector((0,1.5,2)); base=tip-d*2.65
u=Vector((1,0,0)); v=d.cross(u)
def P(t,r,a): return base+d*t+r*(u*cos(a)+v*sin(a))
# striped outer shell with soft barrel bulge; deep visible open bore.
profile=[(0,.43),(.13,.49),(.5,.53),(1.3,.59),(2.3,.65),(2.65,.67)]
for k in range(12):
    vs=[]; fs=[]; n=5
    for t,r in profile:
        for j in range(n+1): vs.append(P(t,r,2*pi*(k+j/n)/12))
    for i in range(len(profile)-1):
        for j in range(n): fs.append((i*(n+1)+j,i*(n+1)+j+1,(i+1)*(n+1)+j+1,(i+1)*(n+1)+j))
    mesh('EnamelStripe_%02d'%k,vs,fs,0 if k%2==0 else 1,True)
vs=[]; fs=[]
for t,r in [(2.65,.53),(2.35,.52),(.4,.32)]:
    for i in range(60): vs.append(P(t,r,2*pi*i/60))
for j in range(2):
    for i in range(60): fs.append((j*60+i,(j+1)*60+i,(j+1)*60+(i+1)%60,j*60+(i+1)%60))
fs.append(tuple(reversed(range(120,180))))
mesh('DeepBoreLining',vs,fs,4,True)
rod('RearCap',base-d*.015,base+d*.04,.435,4,48)
ring('RearBrassBand',base+d*.18,.505,.055,3,d,60)
ring('MuzzleBrassBand',base+d*2.37,.655,.065,3,d,60)
ring('IvoryPaddedMuzzle',tip,.60,.12,1,d,60,12)
for k in range(20):
    a=2*pi*k/20
    rod('BandRivet',P(2.37,.704,a),P(2.37,.735,a),.026,3,8)
    # radial seam around padded lip
    points=[tip+d*(.122*sin(b))+(.60+.122*cos(b))*(u*cos(a)+v*sin(a)) for b in [i*pi/8 for i in range(17)]]
    for i in range(16): rod('LipStitchSeam',points[i],points[i+1],.0045,6,4)
for side in (-1,1):
    # applique stars follow outer barrel panel plane, offset to avoid z fighting.
    for t,r in [(.78,.56),(1.62,.62)]:
        star('BarrelStar',P(t,r,0 if side==1 else pi),.18,(side,0,0),3,angle=pi/2)
    x=side*.82
    # wooden spoked wheels, 1.92 m total including hub endcaps; base at z=0.
    center=(x,-.38,.62)
    ring('WoodenWheel',center,.52,.10,5,(1,0,0),40,8)
    ring('IronTyre',center,.60,.020,4,(1,0,0),48,6)
    ring('WheelGoldInlay',(x+side*.09,-.38,.62),.52,.014,3,(1,0,0),40,5)
    rod('WheelHub',(x-side*.12,-.38,.62),(side*.96,-.38,.62),.13,3,16)
    for k in range(10):
        a=2*pi*k/10
        rod('WoodSpoke',(x,-.38+.10*cos(a),.62+.10*sin(a)),(x,-.38+.49*cos(a),.62+.49*sin(a)),.040,5,8,r2=.055)
        rod('TyreRivet',(x+side*.088,-.38+.52*cos(a),.62+.52*sin(a)),(x+side*.115,-.38+.52*cos(a),.62+.52*sin(a)),.018,3,8)
    cube('RedCarriageRail',(side*.58,-.1,.48),(.16,1.95,.24),0,.035)
    cube('TrunnionStand',(side*.59,.12,.81),(.16,.56,.58),5,.035)
    cube('CarriageBanner',(side*.69,.22,.64),(.035,.60,.42),0,.02)
    for y in (-.06,.50): cube('BannerBorder',(side*.713,y,.64),(.008,.018,.37),3,.002)
    star('CarriageStar',(side*.714,.22,.65),.15,(side,0,0),1)
    rod('TrunnionPin',(side*.5,.12,1.04),(side*.73,.12,1.04),.12,3,16)
    # chock under front rail
    cube('FrontFoot',(side*.58,.73,.23),(.22,.28,.46),5,.025)
rod('Axle',(-.83,-.38,.62),(.83,-.38,.62),.065,4,12)
for y in (-.82,.55): cube('CrossBeam',(0,y,.45),(1.35,.18,.20),5,.025)
for j in range(2):
    y=-1.58+j*.31; z=.10+j*.12
    cube('BoardingStep',(0,y,z),(.84,.36,.16),0,.025)
    for x in (-.37,.37): cube('StepBrassCorner',(x,y,z+.085),(.06,.32,.012),3,.003)
    for k in range(3): cube('StepGrip',(0,y-.1+k*.1,z+.084),(.64,.018,.015),4,.002)

# Preserve the legacy whole-device bounce envelope, including the grip.
scene.view_layers[0].update()
for o in objects:
    if o.get('export_group')!='Handle': continue
    matrix=o.parent.matrix_world.inverted()@o.matrix_world; inverse=matrix.inverted()
    for vert in o.data.vertices:
        p=matrix@vert.co; p.x=-2.8+(p.x+2.8)*(.075/.068); p.z=.6+(p.z-.6)*(.05/.068); vert.co=inverse@p

# Save standalone editable construction; do not overwrite the user's character file.
scene.world=bpy.data.worlds.new('CC_PropsWorld'); scene.world.use_nodes=True
next(n for n in scene.world.node_tree.nodes if n.type == 'BACKGROUND').inputs[0].default_value=(.055,.065,.08,1)
next(n for n in scene.world.node_tree.nodes if n.type == 'BACKGROUND').inputs[1].default_value=.35
tramp.location.x=-3.2; cannon.location.x=3.0
def area(name,loc,power,size):
    l=bpy.data.lights.new(name,'AREA'); l.energy=power; l.shape='DISK'; l.size=size
    o=bpy.data.objects.new(name,l); scene.collection.objects.link(o); o.location=loc; o.rotation_euler=(Vector((0,0,.6))-o.location).to_track_quat('-Z','Y').to_euler()
area('CC_Key',(-3,-5,8),1700,7); area('CC_Fill',(5,3,6),1300,6); area('CC_Rim',(-3,4,4),1000,5)
camdata=bpy.data.cameras.new('CC_PropsCamera'); cam=bpy.data.objects.new('CC_PropsCamera',camdata); scene.collection.objects.link(cam)
cam.location=(9,12,9); cam.rotation_euler=(Vector((0,0,.7))-cam.location).to_track_quat('-Z','Y').to_euler(); camdata.type='ORTHO'; camdata.ortho_scale=12.5; scene.camera=cam
scene.render.engine='CYCLES'; scene.cycles.samples=32
scene.render.resolution_x=1500; scene.render.resolution_y=900; scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'; scene.render.filepath=ROOT+'/Source~/props-preview.png'
bpy.data.libraries.write(ROOT+'/Source~/human-cannon-props-construction.blend',{scene},fake_user=True)
print('BUILT',len(objects),'objects',sum(len(o.data.polygons) for o in objects),'polygons')

