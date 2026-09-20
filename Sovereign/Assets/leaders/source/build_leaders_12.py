"""Sovereign 0.12: focused Daoguang sculpt, grooming and eye articulation.

Blender --background --python build_leaders_12.py -- --out-dir <directory> --render
Omit --out-dir to update the shipped daoguang.glb and editable source blend.
All geometry remains skinned 3D; no portrait image substitutes the face.
"""
import sys, math, random, argparse
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree
import build_leaders as b
import build_leaders_11 as previous


def refine_anatomy(v):
    """Subtle middle-aged volume changes on the same CC0 facial topology."""
    for co in v:
        x,y,z=co[0]*b.S,co[1]*b.S+b.OFFSET,co[2]*b.S
        if y<1.970 or y>2.215 or z<.060:continue
        front=max(0,min(1,(z-.06)/.042))
        cheek=math.exp(-((abs(x)-.057)/.026)**2-((y-2.044)/.022)**2)*front
        lower=math.exp(-((y-2.000)/.025)**2)*front
        co[0]*=1-.038*cheek-.032*lower
        co[2]-=(.0028*cheek+.0008*lower)/b.S
        cheekbone=math.exp(-((abs(x)-.061)/.023)**2-((y-2.070)/.013)**2)*front
        co[2]+=.0013*cheekbone/b.S
        # A quieter lip volume and straighter, less bulbous nose.
        lips=math.exp(-(x/.031)**4-((y-2.022)/.009)**2)*front
        co[2]-=.0013*lips/b.S
        co[1]-=(y-2.022)*.13*lips/b.S
        lower_lip_edge=math.exp(-(x/.024)**4-((y-2.018)/.004)**2)*front
        co[1]+=.0006*lower_lip_edge/b.S
        tip=math.exp(-(x/.022)**2-((y-2.055)/.013)**2)*front
        co[0]*=1-.07*tip
        bridge=math.exp(-(x/.012)**2-((y-2.087)/.022)**2)*front
        co[2]+=.0008*bridge/b.S
        # Small left/right differences prevent a mirrored mannequin expression.
        asym=math.exp(-((x-.045)/.026)**2-((y-2.067)/.028)**2)*front
        co[2]+=.00055*asym/b.S
    for name,amount in [('eye-left-closure.target',.04),('eye-right-closure.target',.065)]:
        for idx,delta in b.target('expression/units/asian/'+name).items():
            for axis in range(3):v[idx][axis]+=delta[axis]*amount
    # Relax the template's slightly open lower lip in the neutral pose. Keep the
    # complete speech delta available, rather than welding away the mouth rim.
    for idx,delta in b.target('expression/units/asian/mouth-parling.target').items():
        x,y,z=v[idx][0]*b.S,v[idx][1]*b.S+b.OFFSET,v[idx][2]*b.S
        if 1.99<y<2.026 and z>.13:
            weight=.035*math.exp(-(x/.040)**4-((y-2.016)/.018)**4)
            for axis in range(3):v[idx][axis]-=delta[axis]*weight


def skin_material():
    path=next((b.SYS/'skins/old_asian_male').glob('*.png'))
    skin=b.material('Daoguang skin 0.12 anatomical detail',(.60,.43,.33),.53,0,path)
    nodes=skin.node_tree.nodes;links=skin.node_tree.links;shader=nodes.get('Principled BSDF')
    shader.inputs['Subsurface Weight'].default_value=.055
    shader.inputs['Subsurface Radius'].default_value=(1,.43,.28)
    shader.inputs['Specular IOR Level'].default_value=.29
    # Connect detail BEFORE copying for face-specific vertex tone. In 0.11 the
    # normal was attached only to the obsolete original material after the copy.
    normal=nodes.new('ShaderNodeTexImage');normal.image=bpy.data.images.load(str(b.ROOT/'daoguang_skin_detail_normal.png'))
    normal.image.colorspace_settings.name='Non-Color'
    mapping=nodes.new('ShaderNodeNormalMap');mapping.inputs['Strength'].default_value=.16
    links.new(normal.outputs['Color'],mapping.inputs['Color']);links.new(mapping.outputs['Normal'],shader.inputs['Normal'])
    return skin


def refine_skin_tone(face):
    b.facial_root_tint(face)
    face.data.materials[0].name='Daoguang aged skin 0.12 with connected microdetail'
    colors=face.data.color_attributes['FacialTone']
    for vertex,color in zip(face.data.vertices,colors.data):
        x,y,z=vertex.co.x,vertex.co.z,-vertex.co.y
        if z<.06:continue
        front=max(0,min(1,(z-.06)/.05))
        lip=math.exp(-(x/.033)**4-((y-2.022)/.010)**2)*front
        eye=math.exp(-((abs(x)-.039)/.024)**2-((y-2.081)/.010)**2)*front
        cheek=math.exp(-((abs(x)-.066)/.029)**2-((y-2.043)/.029)**2)*front
        old=color.color
        color.color=(old[0]*(1-.05*lip-.025*eye),old[1]*(1-.055*lip-.020*cheek),old[2]*(1-.025*lip-.009*cheek),1)


def age_surface(face):
    """Fine anatomical creases baked into all morphs, with unchanged deltas."""
    keys=face.data.shape_keys.key_blocks
    for i,v in enumerate(face.data.vertices):
        x,y,z=v.co.x,v.co.z,-v.co.y
        if not (1.974<y<2.20 and z>.070):continue
        front=max(0,min(1,(z-.070)/.040));d=0
        ax=abs(x)
        # Curved infraorbital fold below the eye and a broad, soft lid bag.
        eye_envelope=math.exp(-((ax-.041)/.024)**4)
        line=2.079+.005*((ax-.037)/.033)**2
        d-=.00042*math.exp(-((y-line)/.0013)**2)*eye_envelope
        d+=.00070*math.exp(-((y-(line+.003))/ .0032)**2)*eye_envelope
        # Nasolabial valley and adjacent cheek volume, fading towards the lip.
        fold=.019+(2.055-y)*.53
        envelope=math.exp(-((y-2.031)/.024)**4)
        d-=.0008*math.exp(-((ax-fold)/.0018)**2)*envelope
        d+=.0004*math.exp(-((ax-fold-.0038)/.0030)**2)*envelope
        # Three restrained outer-eye creases, not a uniform noise texture.
        for offset,slope in [(-.006,-.17),(-.001,.03),(.004,.22)]:
            line=2.098+offset+slope*(ax-.068)
            d-=.00024*math.exp(-((y-line)/.0011)**2-((ax-.073)/.012)**4)
        for h in [2.142,2.154]:
            line=h+.003*math.cos(x*27)
            d-=.00024*math.exp(-((y-line)/.0012)**2-(x/.074)**6)
        displacement=Vector((0,-d*front,0))
        for key in keys:key.data[i].co+=displacement
        v.co=keys[0].data[i].co


def groom_beard(face):
    """Rooted, curved clumps plus fine tapered fibres instead of wire whiskers."""
    bpy.context.view_layer.update();ev=face.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=ev.to_mesh()
    tree=BVHTree.FromPolygons([v.co.copy() for v in mesh.vertices],[list(p.vertices) for p in mesh.polygons]);ev.to_mesh_clear()
    rng=random.Random(183612)
    dark=b.material('Daoguang fine charcoal beard',(.018,.014,.012),.80)
    mid=b.material('Daoguang warm beard variation',(.033,.026,.020),.82)
    grey=b.material('Daoguang occasional grey fibres',(.12,.109,.092),.84)
    groups=[]
    for name,mat,radius in [('Dense directional beard',dark,.000070),('Soft brown beard fibres',mid,.000057),('Individual aged beard fibres',grey,.000047)]:
        curve=bpy.data.curves.new(name,'CURVE');curve.dimensions='3D';curve.resolution_u=3;curve.bevel_depth=radius;curve.bevel_resolution=1
        obj=bpy.data.objects.new(name,curve);bpy.context.collection.objects.link(obj);obj.data.materials.append(mat);groups.append(obj)
    def point(x,y,lift=.00015):
        hit,n,_,_=tree.ray_cast(Vector(b.cv((x,y,.5))),Vector(b.cv((0,0,-1))),1)
        if hit is None:return None
        hit+=n*lift;return Vector((hit.x,hit.z,-hit.y))
    def strand(points):
        chance=rng.random();obj=groups[2 if chance<.06 else 1 if chance<.26 else 0]
        spline=obj.data.splines.new('BEZIER');spline.bezier_points.add(len(points)-1)
        taper=[.36,.88,.73,.36,.025]
        for i,(bp,co) in enumerate(zip(spline.bezier_points,points)):
            bp.co=b.cv(co);bp.radius=taper[i]*rng.uniform(.78,1.16);bp.handle_left_type='AUTO';bp.handle_right_type='AUTO'
    for side in [-1,1]:
        for i in range(280):
            u=rng.random();x=side*(.0018+.031*u)
            y=2.0435-.0095*u**.72+rng.uniform(-.0022,.0019)
            root=point(x,y)
            if root is None:continue
            length=(.005+.012*u**1.5)*rng.uniform(.70,1.08)
            points=[]
            for j in range(5):
                t=j/4;xx=x+side*(.002+.004*u)*t;yy=y-length*t
                onskin=point(xx,yy,.0003+.0010*math.sin(t*math.pi))
                if onskin is None:onskin=root+Vector((side*.004*t,-length*t,.001))
                onskin.z+=.0005*math.sin(t*math.pi)*math.sin(i*.43);points.append(onskin)
            strand(points)
    # Chin roots form an organic oval rather than the former rectangular brush.
    for i in range(780):
        angle=rng.random()*math.tau;r=math.sqrt(rng.random())
        x=.020*r*math.cos(angle);y=1.990+.010*r*math.sin(angle)
        root=point(x,y)
        if root is None:continue
        length=(.024-.004*abs(x)/.02)*rng.uniform(.65,1.12)
        points=[];wave=rng.uniform(-.0016,.0016);curl=rng.uniform(.001,.003)
        for j in range(5):
            t=j/4
            clump=int((x+.020)/.006)
            xx=x*(1-.15*t)+wave*math.sin(t*math.pi)+.0014*math.sin(clump*2.1)*t
            yy=y-length*t
            zz=root.z+.0032*math.sin(t*math.pi)-.003*t+curl*math.sin(t*math.pi*1.5)
            points.append(Vector((xx,yy,zz)))
        strand(points)
    for obj in groups:b.group_object(obj,'Head')


def create_eyes(v,uv,faces,groups):
    centers={}
    for sign,label in [(1,'Left'),(-1,'Right')]:
        ids={p[0] for f,g in zip(faces,groups) if g==('joint-l-eye' if sign>0 else 'joint-r-eye') for p in f}
        centers[label]=Vector(b.vec([sum(v[i][axis] for i in ids)/len(ids) for axis in range(3)]))
    bpy.context.view_layer.objects.active=b.rig;bpy.ops.object.mode_set(mode='EDIT')
    for label,center in centers.items():
        bone=b.rig.data.edit_bones.new(label+'Eye');bone.head=center;bone.tail=center+Vector((0,-.020,0));bone.parent=b.rig.data.edit_bones['Head']
    bpy.ops.object.mode_set(mode='OBJECT')
    ev,eu,ef=b.fit_proxy(b.SYS/'eyes/high-poly/high-poly',v)
    ef=[q for q in ef if not all(eu[a[1]][0]>.86 and eu[a[1]][1]<.15 for a in q)]
    mat=b.material('Natural iris and sclera',(.7,.68,.60),.27,0,b.SYS/'eyes/materials/brown_eye.png')
    mat.node_tree.nodes.get('Principled BSDF').inputs['Specular IOR Level'].default_value=.26
    eyes=b.meshobj('Fitted anatomical eyes with independent gaze',[b.vec(x) for x in ev],[[a[0] for a in q] for q in ef],mat,[[eu[a[1]] for a in q] for q in ef])
    # The shared CC0 brown iris is very red under the portrait key. Darken only
    # its UV islands to an appropriate deep brown, preserving the sclera and
    # original iris detail; this is exported vertex color, not a painted portrait.
    tones=eyes.data.color_attributes.new(name='IrisTone',type='FLOAT_COLOR',domain='CORNER')
    for loop,uv in zip(tones.data,eyes.data.uv_layers.active.data):
        u,w=uv.uv
        distance=min(math.hypot(u-.291,w-.294),math.hypot(u-.706,w-.703))
        mask=max(0,min(1,(.128-distance)/.029));mask=mask*mask*(3-2*mask)
        loop.color=(1-.64*mask,1-.50*mask,1-.35*mask,1)
    nodes=mat.node_tree.nodes;links=mat.node_tree.links
    color=nodes.new('ShaderNodeVertexColor');color.layer_name='IrisTone'
    tex=next(n for n in nodes if n.type=='TEX_IMAGE');mix=nodes.new('ShaderNodeMix');mix.data_type='RGBA';mix.blend_type='MULTIPLY';mix.inputs[0].default_value=1
    links.new(tex.outputs['Color'],mix.inputs[6]);links.new(color.outputs['Color'],mix.inputs[7]);links.new(mix.outputs[2],nodes.get('Principled BSDF').inputs['Base Color'])
    for label in centers:eyes.vertex_groups.new(name=label+'Eye')
    for i,vert in enumerate(eyes.data.vertices):eyes.vertex_groups['LeftEye' if vert.co.x>0 else 'RightEye'].add([i],1,'REPLACE')
    b.subd(eyes,1);mod=eyes.modifiers.new('Independent eye gaze rig','ARMATURE');mod.object=b.rig;eyes.parent=b.rig


def animate_gaze():
    action=b.rig.animation_data.nla_tracks[0].strips[0].action;b.rig.animation_data.action=action
    for frame,yaw,pitch in [(1,0,0),(55,0,0),(58,.013,-.004),(100,.013,-.004),(104,-.008,.003),(154,-.008,.003),(158,.002,0),(205,.002,0),(210,0,0),(241,0,0)]:
        for label in ['Left','Right']:
            bone=b.rig.pose.bones[label+'Eye'];bone.rotation_mode='XYZ';bone.rotation_euler=(pitch,0,yaw);bone.keyframe_insert('rotation_euler',frame=frame)
    b.rig.animation_data.action=None;bpy.context.scene.frame_set(1)


def build(directory=None,render=False):
    bpy.ops.wm.read_factory_settings(use_empty=True);bpy.context.scene.world=bpy.data.worlds.new('Portrait world')
    bpy.context.preferences.filepaths.save_version=0;random.seed(183612)
    p=dict(previous.PROFILES['daoguang']);v,uv,f,g=previous.skin_geometry(p);refine_anatomy(v)
    b.rig=b.create_rig(p);skin=skin_material();face=b.create_skin(v,uv,f,g,skin,neck_crop=1.907)
    refine_skin_tone(face)
    next(m for m in face.modifiers if m.type=='SUBSURF').levels=2
    next(m for m in face.modifiers if m.type=='SUBSURF').render_levels=2
    b.bake_face_subdivision(face);age_surface(face)
    create_eyes(v,uv,f,g)
    bv,bu,bf=b.fit_proxy(b.SYS/'eyebrows/eyebrow001/eyebrow001',v)
    bm=b.material('Natural eyebrow fibres',(.04,.035,.027),.87,0,b.SYS/'eyebrows/eyebrow001/eyebrow001.png')
    bp=bm.node_tree.nodes.get('Principled BSDF');bt=next(n for n in bm.node_tree.nodes if n.type=='TEX_IMAGE');bm.node_tree.links.new(bt.outputs['Alpha'],bp.inputs['Alpha']);bm.surface_render_method='DITHERED'
    brows=b.meshobj('Anatomical fitted eyebrows',[b.vec(x) for x in bv],[[a[0] for a in q] for q in bf],bm,[[bu[a[1]] for a in q] for q in bf]);b.group_object(brows,'Head')
    groom_beard(face);b.robe();b.add_sleeves_and_hands(v,uv,f,g,skin,p)
    import qing_tailoring_12
    qing_tailoring_12.refine_scene(b)
    previous.cloth_surface();b.consolidate_meshes(face);b.animate(face);animate_gaze()
    out=Path(directory).resolve() if directory else b.OUT;source=out if directory else b.ROOT
    out.mkdir(parents=True,exist_ok=True)
    for obj in bpy.context.scene.objects:obj.select_set(obj.type in {'ARMATURE','MESH'})
    bpy.context.view_layer.objects.active=b.rig
    bpy.ops.export_scene.gltf(filepath=str(out/'daoguang.glb'),export_format='GLB',use_selection=True,export_apply=False,export_animations=True,export_animation_mode='NLA_TRACKS',export_nla_strips_merged_animation_name='idle',export_morph=True,export_skins=True,export_force_sampling=True,export_frame_range=True)
    b.setup_render(True);bpy.ops.wm.save_as_mainfile(filepath=str(source/'daoguang.blend'))
    bpy.ops.file.make_paths_relative();bpy.ops.wm.save_as_mainfile(filepath=str(source/'daoguang.blend'))
    if render:
        scene=bpy.context.scene;scene.cycles.samples=24;scene.render.resolution_x=800;scene.render.resolution_y=1100
        scene.render.filepath=str(source/'daoguang-full-qa.png');bpy.ops.render.render(write_still=True)
        scene.camera.location=b.cv((.09,2.075,.90));scene.camera.rotation_euler=(Vector(b.cv((0,2.085,.04)))-scene.camera.location).to_track_quat('-Z','Y').to_euler();scene.camera.data.lens=68
        scene.render.resolution_x=900;scene.render.resolution_y=900;scene.render.filepath=str(source/'daoguang-face-qa.png');bpy.ops.render.render(write_still=True)
    print('SOVEREIGN_012_CHARACTER_COMPLETE daoguang',flush=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--out-dir');parser.add_argument('--render',action='store_true')
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    build(args.out_dir,args.render)
