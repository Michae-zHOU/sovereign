"""Sovereign 0.11: reproducible full-figure historical character authoring.

Blender --background --python build_leaders_11.py -- william_iv --render
Blender --background --python build_leaders_11.py -- all
Uses only the CC0 source anatomy shipped beside this file. Artistic likenesses.
"""
import sys, math, random, json, hashlib, struct
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
import bpy
from mathutils import Vector, Quaternion
from mathutils.bvhtree import BVHTree
import build_leaders as b
import period_wardrobe

def profile(sex, age, wardrobe, coat, **kw):
    return dict(sex=sex, age=age, wardrobe=wardrobe, coat=coat, eth='caucasian',
                width=1., jaw=1., nose=1., length=1., cheek=0., brow=0.,
                hair='swept', hair_color=(.065,.045,.029), beard='none',
                sash=(.30,.055,.04), **kw)

# Dimensions are small independent sculpt controls, not just tint replacements.
PROFILES = {
 'william_iv': profile('male',70,'naval',(.022,.030,.052)),
 'victoria': profile('female',18,'gown',(.53,.43,.31)),
 'frederick_william_iii': profile('male',65,'prussian',(.025,.035,.05)),
 'frederick_william_iv': profile('male',44,'prussian',(.029,.04,.055)),
 'tokugawa_ienari': profile('male',62,'shogun',(.023,.048,.041)),
 'tokugawa_ieyoshi': profile('male',44,'shogun',(.061,.069,.029)),
 'louis_philippe': profile('male',62,'royal',(.019,.038,.068)),
 'ferdinand_i': profile('male',42,'austrian',(.64,.61,.52)),
 'nicholas_i': profile('male',39,'russian',(.027,.052,.038)),
 'andrew_jackson': profile('male',68,'statesman',(.021,.025,.029)),
 'martin_van_buren': profile('male',54,'statesman',(.029,.023,.018)),
 'william_henry_harrison': profile('male',68,'statesman',(.028,.029,.032)),
 'john_tyler': profile('male',51,'statesman',(.021,.028,.033)),
 'james_polk': profile('male',49,'statesman',(.023,.025,.029)),
 'daoguang': profile('male',53,'qing',(.44,.29,.07)),
 'mahmud_ii': profile('male',50,'ottoman',(.025,.042,.064)),
 'abdulmejid_i': profile('male',16,'ottoman',(.022,.037,.058)),
 'isabella_ii': profile('female',5,'gown',(.38,.12,.13)),
 'isabella_ii_adolescent': profile('female',13,'gown',(.42,.16,.18)),
 'maria_ii': profile('female',16,'gown',(.50,.40,.23)),
 'leopold_i': profile('male',45,'royal',(.023,.042,.039)),
}
DETAILS = {
 'william_iv':dict(width=1.03,jaw=1.08,nose=1.08,cheek=.12,hair='receding',hair_color=(.25,.24,.205),beard='short_sideburns',sash=(.17,.29,.36)),
 'victoria':dict(width=.99,jaw=.97,nose=.90,cheek=.13,hair='centre_part',hair_color=(.058,.027,.013),sash=(.14,.26,.39)),
 'frederick_william_iii':dict(width=.94,length=1.04,jaw=.94,nose=1.06,hair='receding',hair_color=(.105,.084,.059),beard='short_sideburns'),
 'frederick_william_iv':dict(width=1.10,jaw=1.09,cheek=.22,hair='side_part',beard='sideburns'),
 'tokugawa_ienari':dict(eth='asian',width=1.055,jaw=1.03,hair='chonmage'),
 'tokugawa_ieyoshi':dict(eth='asian',width=.96,jaw=.95,length=1.02,hair='chonmage'),
 'louis_philippe':dict(width=1.085,jaw=1.06,cheek=.16,nose=1.12,hair='side_part',hair_color=(.11,.089,.059),beard='sideburns'),
 'ferdinand_i':dict(width=.94,length=1.055,jaw=.91,nose=1.16,cheek=-.12,hair='receding',hair_color=(.044,.028,.018)),
 'nicholas_i':dict(width=.985,jaw=1.035,nose=1.12,brow=.08,hair='receding',beard='moustache',sash=(.15,.30,.43)),
 'andrew_jackson':dict(width=.92,length=1.04,jaw=.94,nose=1.18,cheek=-.24,hair='high_swept',hair_color=(.29,.275,.23)),
 'martin_van_buren':dict(width=1.095,jaw=1.08,cheek=.17,hair='balding',hair_color=(.15,.095,.055),beard='large_sideburns'),
 'william_henry_harrison':dict(width=.95,length=1.035,jaw=.96,nose=1.07,cheek=-.13,hair='balding',hair_color=(.18,.155,.115)),
 'john_tyler':dict(width=.93,length=1.055,jaw=.92,nose=1.17,cheek=-.18,hair='receding',hair_color=(.049,.030,.019)),
 'james_polk':dict(width=.965,length=1.03,jaw=.96,nose=1.09,cheek=-.10,hair='long_swept',hair_color=(.037,.026,.018)),
 'daoguang':dict(eth='asian',width=.99,hair='qing'),
 'mahmud_ii':dict(width=1.045,jaw=1.055,nose=1.08,hair='fez',beard='full',hair_color=(.025,.020,.016)),
 'abdulmejid_i':dict(width=.955,jaw=.93,nose=1.03,hair='fez',beard='none',hair_color=(.028,.020,.014)),
 'isabella_ii':dict(width=1.02,jaw=.94,nose=.90,hair='child_curls',hair_color=(.074,.041,.022)),
 'isabella_ii_adolescent':dict(width=1.02,jaw=.98,nose=.95,hair='centre_part',hair_color=(.056,.031,.017)),
 'maria_ii':dict(width=1.06,jaw=1.02,cheek=.19,hair='side_curls',hair_color=(.032,.020,.012)),
 'leopold_i':dict(width=.98,length=1.03,jaw=.96,nose=1.11,hair='side_part',beard='sideburns'),
}
for key, val in PROFILES.items():val.update(DETAILS[key]);val['identity']=key

def skin_geometry(p):
    v,uv,f,g=b.readobj(b.MH/'3dobjs/base.obj')
    old=max(0,min(1,(p['age']-25)/65))
    child=max(0,min(1,(18-p['age'])/10))
    weights={'young':(1-old)*(1-child),'old':old,'child':child}
    for stage,w in weights.items():
        if w==0:continue
        for name in [f"{p['eth']}-{p['sex']}-{stage}",f"universal-{p['sex']}-{stage}-averagemuscle-averageweight"]:
            for idx,d in b.target('macrodetails/'+name+'.target').items():
                for a in range(3):v[idx][a]+=d[a]*w
    eye_ids=set(a[0] for q,group in zip(f,g) if group=='joint-l-eye' for a in q)
    b.S=.166 if p['age']<11 else (.149 if p['sex']=='female' else .136)
    b.OFFSET=2.10-sum(v[i][1] for i in eye_ids)/len(eye_ids)*b.S
    for co in v:
        x,y,z=co[0]*b.S,co[1]*b.S+b.OFFSET,co[2]*b.S
        if 1.80<y<1.985:
            blend=max(0,min(1,(1.985-y)/.074))
            # Remove the retained template shoulder stubs within the standing collar.
            co[0]=(x*(1-blend)+max(-.064,min(.064,x))*blend)/b.S
            co[2]=(z*(1-blend)+max(-.010,min(.108,z))*blend)/b.S
        if y<1.96:continue
        co[0]*=p['width']
        co[1]+=(y-2.10)*(p['length']-1)/b.S
        lower=math.exp(-((y-2.005)/.039)**2)
        co[0]*=1+(p['jaw']-1)*lower
        if z>.04:
            nose=math.exp(-(x/.024)**2-((y-2.063)/.043)**2)
            co[2]+=.033*(p['nose']-1)*nose/b.S
            cheek=math.exp(-((abs(x)-.06)/.027)**2-((y-2.047)/.028)**2)
            co[2]+=.016*p['cheek']*cheek/b.S
    if p['identity']=='daoguang':b.sculpt_daoguang(v)
    elif p['age']>35:
        for name in ['eye-left-closure.target','eye-right-closure.target']:
            for idx,d in b.target('expression/units/asian/'+name).items():
                for a in range(3):v[idx][a]+=.045*d[a]
    return v,uv,f,g

def surface_of(face):
    bpy.context.view_layer.update();ev=face.evaluated_get(bpy.context.evaluated_depsgraph_get());me=ev.to_mesh()
    tree=BVHTree.FromPolygons([v.co.copy() for v in me.vertices],[list(p.vertices) for p in me.polygons]);ev.to_mesh_clear()
    return tree

def hair_groom(face,p):
    if p['hair']=='qing':return
    tree=surface_of(face)
    col=p['hair_color'];mat=b.material('Individually groomed '+p['hair'],col,.80)
    glint=b.material('Fine hair strand highlights',tuple(c*1.22 for c in col),.76)
    center=Vector(b.cv((0,2.135,.065)))
    def scalp(phi,theta,lift=.002):
        direction=Vector(b.cv((math.sin(phi)*math.sin(theta),math.cos(phi),math.sin(phi)*math.cos(theta))))
        hit,normal,_,_=tree.ray_cast(center+direction*.4,-direction,1)
        if hit is None:return None
        hit+=normal*lift
        return (hit.x,hit.z,-hit.y)
    def limit(theta):
        front=max(0,math.cos(theta))
        lim=2.00-.66*front**2
        if p['hair'] in ['receding','balding']:lim-=.15*front
        if p['hair']=='balding':lim-=.24*max(0,math.cos(theta))**.25
        if p['hair']=='chonmage':lim=1.95
        return lim
    # A contiguous fitted scalp under many fine swept strands prevents visible card gaps.
    verts=[];faces=[];n=72;rows=14
    for j in range(rows+1):
        for i in range(n):
            theta=2*math.pi*i/n;phi=.01+(limit(theta)-.01)*j/rows
            pt=scalp(phi,theta,.0055)
            verts.append(b.cv(pt or (0,2.24,.02)))
    for j in range(rows):
        for i in range(n):
            t=2*math.pi*(i+.5)/n
            if p['hair']=='chonmage' and (math.cos(t)>.15 or j<6):continue
            if p['hair']=='balding' and j<6:continue
            a=j*n+i;faces.append([a,a+n,(j+1)*n+(i+1)%n,j*n+(i+1)%n])
    ob=b.meshobj('Fitted continuous hair roots',verts,faces,mat);b.subd(ob,1);b.group_object(ob,'Head')
    for i in range(250):
        theta=(i+.2)*2*math.pi/250
        if p['hair']=='chonmage' and math.cos(theta)>.15:continue
        lo=.20 if p['hair']!='balding' else .91
        pts=[]
        for j in range(8):
            t=j/7;phi=lo+(limit(theta)-lo)*t
            # The centre part is kept small; high forelocks are limited to Jackson's silhouette.
            sweep=.22*math.sin(phi) if p['hair'] in ['swept','high_swept','side_part','long_swept'] else 0
            lift=.0065+.0018*math.sin(math.pi*t)
            if p['hair']=='high_swept':lift+=.011*(1-t)*max(0,math.cos(theta))
            pt=scalp(phi,theta+sweep,lift)
            if pt:pts.append(pt)
        if len(pts)>3:b.curve('Fine directional hair fibre',pts,.00032 if p['sex']=='female' else .00038,glint if i%7==0 else mat,'Head',[.2]+[1]*(len(pts)-2)+[.06])
    if p['sex']=='female':
        # Back bun and side rolls follow a centre part instead of the old helmet-shaped braid.
        b.sphere('Gathered hair bun',(0,2.10,-.105),(.055,.050,.034),mat)
        for side in [-1,1]:
            for i in range(18):
                y=2.145-i*.004;xx=side*(.081+.007*math.sin(i*.4))
                pts=[(xx,y,.012),(xx+side*.008,y-.016,.025),(xx+side*.002,y-.028,.012)]
                b.curve('Soft court side curl',pts,.0032 if p['hair'] in ['side_curls','child_curls'] else .0018,mat)
        pearl=b.material('Small pearl hair comb',(.58,.53,.41),.33,.08)
        gold=b.material('Restrained gold hair comb',(.38,.255,.087),.4,.55)
        for i in range(9):
            x=(i-4)*.011;y=2.208+.021*(1-(x/.055)**2)
            b.sphere('Small court diadem pearl',(x,y,.069),(.0035,.004,.0035),pearl)
        b.curve('Fine court diadem band',[(-.051,2.21,.065),(0,2.233,.07),(.051,2.21,.065)],.0017,gold)
    elif p['hair']=='chonmage':
        b.curve('Folded chonmage',[(0,2.205,-.065),(0,2.265,-.015),(0,2.265,.045),(0,2.245,.053)],.017,mat,'Head',[.7,1,.85,.5])
    elif p['hair']=='fez':
        red=b.material('Wine red felt fez',(.255,.025,.022),.93)
        b.lathe('Ottoman felt fez',[(2.19,.105,.09,.007),(2.20,.106,.09,.007),(2.32,.091,.078,.007),(2.326,.085,.073,.007),(2.328,0,0,.007)],red,'Head',64)
        b.curve('Silk fez tassel',[(0,2.335,0),(.07,2.33,-.045),(.104,2.27,-.05),(.108,2.20,-.05)],.0038,mat)
    elif p['hair']=='long_swept':
        for side in [-1,1]:
            for i in range(20):
                z=-.04-i*.003
                b.curve('Swept hair at nape',[(side*.081,2.14,z),(side*.092,2.07,z-.008),(side*.080,2.015,z-.016)],.0018,mat)
    if p['beard']=='none':return
    def point(x,y,lift=.0007):
        hit,n,_,_=tree.ray_cast(Vector(b.cv((x,y,.5))),Vector(b.cv((0,0,-1))),1)
        if hit is None:return None
        hit+=n*lift;return (hit.x,hit.z,-hit.y)
    rng=random.Random(p['identity'])
    for side in [-1,1]:
        count=230 if p['beard'] in ['full','large_sideburns'] else 125
        for i in range(count):
            if p['beard']=='moustache':
                x=side*rng.uniform(.003,.031);y=2.045-.12*abs(x)+rng.uniform(-.0018,.0018)
            elif p['beard']=='full':
                x=side*rng.uniform(.005,.068);y=rng.uniform(1.992,2.055)
                if abs(x)<.028 and y>2.008 and y<2.035:continue
            else:
                x=side*rng.uniform(.074,.095);y=rng.uniform(2.041 if p['beard']=='short_sideburns' else 2.008,2.122)
            root=point(x,y)
            if root is None:continue
            length=.006 if p['beard']=='short_sideburns' else (.026 if p['beard'] in ['large_sideburns','full'] else .013)
            mid=point(x*.99,y-length*.45,.0016)
            end=point(x*.96,y-length,.003)
            if not end:end=(root[0]*.94,root[1]-length,root[2]+.001)
            if mid:b.curve('Rooted '+p['beard'],[root,mid,end],rng.uniform(.00023,.00045),mat,'Head',[.5,1,.03])

def hands(v,uv,faces,groups,skin):
    def landmark(name):
        ids=set(a[0] for f,g in zip(faces,groups) if g==name for a in f)
        return Vector(tuple(sum(b.vec(v[i])[j] for i in ids)/len(ids) for j in range(3)))
    for side,label in [(1,'Left'),(-1,'Right')]:
        wrist=landmark('joint-l-hand' if side==1 else 'joint-r-hand')
        finger=landmark('joint-l-finger-3-4' if side==1 else 'joint-r-finger-3-4')
        target_wrist=Vector(b.cv((side*.379,1.075,.067)))
        direction=Vector(b.cv((side*.011,-.215,.023))).normalized()
        rotation=Quaternion(direction,side*.75)@(finger-wrist).normalized().rotation_difference(direction)
        selected=[f for f,g in zip(faces,groups) if g=='body' and min(side*v[a[0]][0]*b.S for a in f)>abs(wrist.x)-.042]
        ids=sorted(set(a[0] for f in selected for a in f));idx={x:i for i,x in enumerate(ids)};coords=[]
        for i in ids:
            local=rotation@(Vector(b.vec(v[i]))-wrist);along=local.dot(direction);cross=local-direction*along
            relax=max(0,min(1,(along-.06)/.13));local=direction*along+cross*(1-.38*relax)
            local+=Vector(b.cv((0,0,-.011*relax*relax)))
            coords.append(target_wrist+local)
        ob=b.meshobj(label+' relaxed anatomical fingers',coords,[[idx[a[0]] for a in f] for f in selected],skin,[[uv[a[1]] for a in f] for f in selected]);b.subd(ob,1);b.group_object(ob,label+'Forearm')

def facial_hair(face,p):
    if p['beard']=='none':return
    tree=surface_of(face);mat=b.material('Individually rooted '+p['beard'],p['hair_color'],.87)
    def point(x,y,lift=.0007):
        hit,n,_,_=tree.ray_cast(Vector(b.cv((x,y,.5))),Vector(b.cv((0,0,-1))),1)
        if hit is None:return None
        hit+=n*lift;return (hit.x,hit.z,-hit.y)
    rng=random.Random(p['identity'])
    groom=bpy.data.curves.new('Coherent facial hair fibres','CURVE');groom.dimensions='3D';groom.resolution_u=3;groom.bevel_depth=.00027;groom.bevel_resolution=1
    for side in [-1,1]:
        count=1700 if p['beard']=='full' else 750 if p['beard']=='large_sideburns' else 320
        for i in range(count):
            fade=1
            if p['beard']=='moustache':
                x=side*rng.uniform(.001,.034);u=abs(x)/.034
                y=2.046-.011*u**1.3+rng.uniform(-.0018,.0018)
                fade=min(1,(1-u)*6)
            elif p['beard']=='full':
                x=side*rng.uniform(.001,.083);u=abs(x)/.083
                low=1.989+.036*u*u;high=2.044+.047*u**1.5
                y=rng.uniform(low,high)
                if (x/.029)**2+((y-2.024)/.014)**2<1:continue
                fade=min(1,(y-low)/.005,(high-y)/.006,(1-u)*9)
            else:
                x=side*rng.uniform(.071,.095);y=rng.uniform(2.042 if p['beard']=='short_sideburns' else 2.005,2.12)
            root=point(x,y)
            if root is None:continue
            length=(.004+.009*(abs(x)/.034)) if p['beard']=='moustache' else .005 if p['beard']=='short_sideburns' else .023 if p['beard']=='large_sideburns' else .008
            length*=rng.uniform(.65,1.15)
            mid=point(x*.99,y-length*.45,.0012);end=point(x*.96,y-length,.0025)
            if not end:end=(root[0]*.94,root[1]-length,root[2]+.001)
            if mid:
                spline=groom.splines.new('BEZIER');spline.bezier_points.add(2);width=rng.uniform(.67,1.33)*max(.05,fade)
                for bp,co,radius in zip(spline.bezier_points,[root,mid,end],[.4,1,.03]):
                    bp.co=b.cv(co);bp.handle_left_type='AUTO';bp.handle_right_type='AUTO';bp.radius=radius*width
    ob=bpy.data.objects.new('Directional '+p['beard'],groom);bpy.context.collection.objects.link(ob);ob.data.materials.append(mat);b.group_object(ob,'Head')

def boot_fit(p):
    if p['wardrobe'] not in ['prussian','russian','austrian']:return
    for ob in bpy.context.scene.objects:
        if ob.type!='MESH' or not any('wool trousers' in m.name.lower() for m in ob.data.materials):continue
        for vertex in ob.data.vertices:
            if vertex.co.z>.63:continue
            blend=max(0,min(1,(.63-vertex.co.z)/.07));center=.108 if vertex.co.x>0 else -.108
            x=vertex.co.x-center;z=-vertex.co.y
            # Cloth tucks inside the boot opening, with an overlapping 7cm transition.
            radius=math.sqrt((x/.054)**2+((z-.012)/.054)**2)
            if radius>1:
                vertex.co.x=center+x*((1-blend)+blend/radius)
                vertex.co.y=-(.012+(z-.012)*((1-blend)+blend/radius))

def child_proportions(p):
    if p['age']>=18 and p['sex']!='female':return
    amount=max(0,min(1,(18-p['age'])/13))
    # Piecewise body/head transform changes skeletal rest and all morphs identically.
    # The young child keeps a proportionately larger head and shorter limbs.
    body_scale=1-.40*amount;head_scale=1-.15*amount;width_scale=1-.24*amount;neck=1.94
    def transform(co):
        shoulder_taper=.91 if p['sex']=='female' and co.z<1.94 else 1
        co.x*=width_scale*shoulder_taper;co.y*=width_scale
        co.z=co.z*body_scale if co.z<neck else neck*body_scale+(co.z-neck)*head_scale
    for ob in bpy.context.scene.objects:
        if ob.type!='MESH':continue
        if ob.data.shape_keys:
            for key in ob.data.shape_keys.key_blocks:
                for v in key.data:transform(v.co)
            for vertex,basis in zip(ob.data.vertices,ob.data.shape_keys.key_blocks[0].data):vertex.co=basis.co
        else:
            for v in ob.data.vertices:transform(v.co)
    bpy.context.view_layer.objects.active=b.rig;bpy.ops.object.mode_set(mode='EDIT')
    for bone in b.rig.data.edit_bones:transform(bone.head);transform(bone.tail)
    bpy.ops.object.mode_set(mode='OBJECT')

def cloth_surface():
    """Portable, original tangent-space woven relief; shared 128px tile."""
    path=b.ROOT/'period_weave_normal.png'
    if path.exists():image=bpy.data.images.load(str(path))
    else:
        image=bpy.data.images.new('Original period weave normal',width=128,height=128,alpha=False)
        pixels=[]
        for y in range(128):
            for x in range(128):
                nx=.13*math.sin(math.tau*x/8)*(1+.22*math.cos(math.tau*y/16))
                ny=.13*math.sin(math.tau*y/8)*(1+.22*math.cos(math.tau*x/16))
                pixels.extend((.5+nx,.5+ny,math.sqrt(1-nx*nx-ny*ny)*.5+.5,1))
        image.pixels=pixels;image.filepath_raw=str(path);image.file_format='PNG';image.save()
    image.colorspace_settings.name='Non-Color'
    for mat in bpy.data.materials:
        if not mat.use_nodes or not any(key in mat.name.lower() for key in ['cloth','linen','woven','waistcoat','trouser','court skirt','silk ribbon']):continue
        nodes=mat.node_tree.nodes;links=mat.node_tree.links;shader=nodes.get('Principled BSDF')
        tex=nodes.new('ShaderNodeTexImage');tex.image=image
        uv=nodes.new('ShaderNodeTexCoord');mapping=nodes.new('ShaderNodeMapping');mapping.inputs['Scale'].default_value=(14,14,1)
        links.new(uv.outputs['UV'],mapping.inputs['Vector']);links.new(mapping.outputs['Vector'],tex.inputs['Vector'])
        normal=nodes.new('ShaderNodeNormalMap');normal.inputs['Strength'].default_value=.22
        links.new(tex.outputs['Color'],normal.inputs['Color']);links.new(normal.outputs['Normal'],shader.inputs['Normal'])

def build(identity, render=False):
    p=PROFILES[identity];random.seed(1836+sum(map(ord,identity)))
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.world=bpy.data.worlds.new('Portrait studio world')
    bpy.context.preferences.filepaths.save_version=0
    v,uv,f,g=skin_geometry(p)
    # All new characters use the nine-bone full-figure rig, not the old bust rig.
    b.rig=b.create_rig(dict(p,wardrobe='qing'));b.rig.name=identity+'Rig'
    if p['sex']=='female':folder='young_caucasian_female'
    elif p['eth']=='asian':folder='old_asian_male' if p['age']>45 else 'young_asian_male'
    else:folder='old_caucasian_male' if p['age']>55 else ('young_caucasian_male' if p['age']<25 else 'middleage_caucasian_male')
    skinpath=next((b.SYS/'skins'/folder).glob('*.png'))
    skin=b.material('Anatomical skin',(.58,.42,.3),.62,0,skinpath)
    bsdf=skin.node_tree.nodes.get('Principled BSDF');bsdf.inputs['Subsurface Weight'].default_value=.045;bsdf.inputs['Subsurface Radius'].default_value=(1,.38,.2);bsdf.inputs['Specular IOR Level'].default_value=.27
    face=b.create_skin(v,uv,f,g,skin,neck_crop=1.907)
    if identity=='daoguang':b.facial_root_tint(face)
    # Detail normal is an existing authored tangent-space skin map on the shared CC0 UV.
    normal_path=b.ROOT/'daoguang_skin_detail_normal.png'
    if normal_path.exists():
        nodes=skin.node_tree.nodes;links=skin.node_tree.links
        tex=nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(normal_path));tex.image.colorspace_settings.name='Non-Color'
        norm=nodes.new('ShaderNodeNormalMap');norm.inputs['Strength'].default_value=.26 if p['age']<25 else .45
        links.new(tex.outputs['Color'],norm.inputs['Color']);links.new(norm.outputs['Normal'],bsdf.inputs['Normal'])
    ev,eu,ef=b.fit_proxy(b.SYS/'eyes/high-poly/high-poly',v)
    ef=[q for q in ef if not all(eu[a[1]][0]>.86 and eu[a[1]][1]<.15 for a in q)]
    em=b.material('Natural iris and sclera',(.7,.68,.60),.34,0,b.SYS/'eyes/materials/brown_eye.png')
    em.node_tree.nodes.get('Principled BSDF').inputs['Specular IOR Level'].default_value=.23
    eyes=b.meshobj('Anatomical fitted eyes',[b.vec(x) for x in ev],[[a[0] for a in q] for q in ef],em,[[eu[a[1]] for a in q] for q in ef]);b.subd(eyes,1);b.group_object(eyes,'Head')
    bv,bu,bf=b.fit_proxy(b.SYS/'eyebrows/eyebrow001/eyebrow001',v)
    bm=b.material('Natural eyebrow fibres',(.04,.035,.027),.9,0,b.SYS/'eyebrows/eyebrow001/eyebrow001.png')
    bp=bm.node_tree.nodes.get('Principled BSDF');bt=next(n for n in bm.node_tree.nodes if n.type=='TEX_IMAGE');bm.node_tree.links.new(bt.outputs['Alpha'],bp.inputs['Alpha']);bm.surface_render_method='DITHERED'
    brows=b.meshobj('Anatomical fitted eyebrows',[b.vec(x) for x in bv],[[a[0] for a in q] for q in bf],bm,[[bu[a[1]] for a in q] for q in bf]);b.group_object(brows,'Head')
    if identity=='daoguang':
        b.robe();b.authored_facial_hair(face);b.add_sleeves_and_hands(v,uv,f,g,skin,p)
    else:
        period_wardrobe.build(b,p,face)
        if p['hair']=='chonmage':hair_groom(face,p)
        else:
            import period_hair
            period_hair.build(b,v,dict(p,hair='receding') if p['hair']=='fez' else p,face);facial_hair(face,p)
            if p['hair']=='fez':
                red=b.material('Period red felt fez',(.255,.025,.022),.94)
                b.lathe('Fitted Ottoman fez',[(2.185,.105,.142,.063),(2.195,.108,.145,.063),(2.32,.092,.120,.060),(2.328,.086,.115,.060),(2.330,0,0,.060)],red,'Head',64)
                silk=b.material('Black silk fez tassel',(.016,.012,.009),.80)
                b.curve('Hanging silk fez tassel',[(0,2.334,.060),(.07,2.33,.016),(.112,2.27,.014),(.116,2.18,.014)],.0025,silk)
        hands(v,uv,f,g,skin)
    cloth_surface();b.bake_face_subdivision(face);b.consolidate_meshes(face);boot_fit(p)
    child_proportions(p);b.animate(face)
    for ob in bpy.context.scene.objects:ob.select_set(ob.type in {'ARMATURE','MESH'})
    bpy.context.view_layer.objects.active=b.rig
    bpy.ops.export_scene.gltf(filepath=str(b.OUT/(identity+'.glb')),export_format='GLB',use_selection=True,export_apply=False,export_animations=True,export_animation_mode='NLA_TRACKS',export_nla_strips_merged_animation_name='idle',export_morph=True,export_skins=True,export_force_sampling=True,export_frame_range=True)
    b.setup_render(True)
    bpy.ops.wm.save_as_mainfile(filepath=str(b.ROOT/(identity+'.blend')))
    bpy.ops.file.make_paths_relative();bpy.ops.wm.save_as_mainfile(filepath=str(b.ROOT/(identity+'.blend')))
    if render:
        scene=bpy.context.scene;scene.render.resolution_x=720;scene.render.resolution_y=960;scene.cycles.samples=24
        if p['age']<18:
            center=1.15*(1-.30*max(0,(18-p['age'])/13));scene.camera.location=b.cv((.30,center+.1,3.6));scene.camera.rotation_euler=(Vector(b.cv((0,center,0)))-scene.camera.location).to_track_quat('-Z','Y').to_euler()
        scene.render.filepath=str(b.ROOT/(identity+'-qa.png'));bpy.ops.render.render(write_still=True)
        top=max(v.co.z for v in face.data.vertices);bottom=min(v.co.z for v in face.data.vertices);center=(top+bottom)/2
        scene.camera.location=b.cv((.12,center+.035,1.04));scene.camera.rotation_euler=(Vector(b.cv((0,center,.02)))-scene.camera.location).to_track_quat('-Z','Y').to_euler();scene.camera.data.lens=68
        scene.render.resolution_x=720;scene.render.resolution_y=720;scene.render.filepath=str(b.ROOT/(identity+'-face-qa.png'));bpy.ops.render.render(write_still=True)
    print('SOVEREIGN_011_CHARACTER_COMPLETE',identity,flush=True)

if __name__=='__main__':
    args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else ['william_iv','--render']
    names=list(PROFILES) if 'all' in args else [x for x in args if not x.startswith('--')]
    for identity in names:build(identity,'--render' in args)
