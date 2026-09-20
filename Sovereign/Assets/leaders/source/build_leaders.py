"""Original Sovereign character authoring. Run with Blender 4.5 --background --python this.py.
MakeHuman CC0 topology/targets/skin provide anatomical geometry; tailoring, rig and animation are original.
The output is an artistic reconstruction, not a facial scan or verified historical likeness.
"""
import bpy, math, random, json, sys
from pathlib import Path
from mathutils import Vector, Matrix, Quaternion
from mathutils.bvhtree import BVHTree
ROOT=Path(__file__).resolve().parent
MH=ROOT/'makehuman/makehuman/data'
SYS=ROOT/'makehuman/system-assets'
OUT=ROOT.parent
random.seed(1836)
S=.22
OFFSET=.58617

def vec(p): return (float(p[0])*S, -float(p[2])*S, float(p[1])*S+OFFSET)
def cv(p): return (p[0],-p[2],p[1])
def readobj(p):
    v=[];uv=[];faces=[];groups=[];group='body'
    for l in p.read_text().splitlines():
        a=l.split()
        if not a:continue
        if a[0]=='v':v.append([float(x) for x in a[1:4]])
        elif a[0]=='vt':uv.append(tuple(float(x) for x in a[1:3]))
        elif a[0]=='g':group=' '.join(a[1:])
        elif a[0]=='f':
            faces.append([(int(x.split('/')[0])-1,int(x.split('/')[1])-1 if '/' in x else 0) for x in a[1:]])
            groups.append(group)
    return v,uv,faces,groups

def target(name):
    p=MH/'targets'/name
    out={}
    for l in p.read_text().splitlines():
        if not l.strip() or l.startswith('#'):continue
        a=l.split();out[int(a[0])]=[float(x) for x in a[1:4]]
    return out

def material(name,color,roughness=.55,metallic=0,image=None):
    m=bpy.data.materials.new(name);m.use_nodes=True
    p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*color,1)
    p.inputs['Roughness'].default_value=roughness;p.inputs['Metallic'].default_value=metallic
    if image:
        t=m.node_tree.nodes.new('ShaderNodeTexImage');t.image=bpy.data.images.load(str(image),check_existing=True)
        m.node_tree.links.new(t.outputs['Color'],p.inputs['Base Color'])
    return m

def meshobj(name,verts,faces,mat,uvs=None):
    mesh=bpy.data.meshes.new(name+'Mesh');mesh.from_pydata(verts,[],faces);mesh.update()
    ob=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(ob)
    if mat:mesh.materials.append(mat)
    for p in mesh.polygons:p.use_smooth=True
    if uvs:
        layer=mesh.uv_layers.new(name='UVMap')
        for poly,face_uv in zip(mesh.polygons,uvs):
            for li,uv in zip(poly.loop_indices,face_uv):layer.data[li].uv=uv
    return ob

def group_object(ob,bone):
    if ob.type=='CURVE':
        bpy.ops.object.select_all(action='DESELECT');bpy.context.view_layer.objects.active=ob;ob.select_set(True);bpy.ops.object.convert(target='MESH');ob.select_set(False)
    g=ob.vertex_groups.new(name=bone);g.add(list(range(len(ob.data.vertices))),1,'REPLACE')
    mod=ob.modifiers.new('Sovereign deformation rig','ARMATURE');mod.object=rig
    ob.parent=rig
    return ob

def subd(ob,levels=1):
    m=ob.modifiers.new('Anatomical smoothing','SUBSURF');m.levels=levels;m.render_levels=levels
    return m

def curve(name,points,radius,mat,bone='Head',radii=None):
    c=bpy.data.curves.new(name,'CURVE');c.dimensions='3D';c.resolution_u=3;c.bevel_depth=radius;c.bevel_resolution=1
    s=c.splines.new('BEZIER');s.bezier_points.add(len(points)-1)
    for i,(bp,co) in enumerate(zip(s.bezier_points,points)):
        bp.co=cv(co);bp.handle_left_type='AUTO';bp.handle_right_type='AUTO'
        if radii:bp.radius=radii[i]
    ob=bpy.data.objects.new(name,c);bpy.context.collection.objects.link(ob);ob.data.materials.append(mat)
    return group_object(ob,bone)

def lathe(name,levels,mat,bone='Chest',segments=96,flutes=0):
    # Levels are gallery Y, horizontal radius, depth radius, front/back center.
    vs=[];fs=[];us=[]
    for j,(y,rx,rz,zc) in enumerate(levels):
        for i in range(segments):
            a=i*2*math.pi/segments
            wave=1+flutes*math.cos(12*a)*math.sin(math.pi*j/max(1,len(levels)-1))
            vs.append(cv((math.sin(a)*rx*wave,y,math.cos(a)*rz*wave+zc)))
    for j in range(len(levels)-1):
        for i in range(segments):
            k=j*segments+i;n=j*segments+(i+1)%segments
            fs.append([k,n,n+segments,k+segments]);u=i/segments;v=j/(len(levels)-1)
            us.append([(u,v),(u+1/segments,v),(u+1/segments,(j+1)/(len(levels)-1)),(u,(j+1)/(len(levels)-1))])
    ob=meshobj(name,vs,fs,mat,us);subd(ob,1);return group_object(ob,bone)

def sphere(name,at,size,mat,bone='Head'):
    small=max(size)<.018
    bpy.ops.mesh.primitive_uv_sphere_add(segments=12 if small else 20,ring_count=8 if small else 12,location=cv(at));o=bpy.context.object;o.name=name
    o.scale=(size[0],size[2],size[1]);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    o.data.materials.append(mat)
    for p in o.data.polygons:p.use_smooth=True
    # Bake object transform into vertex positions so weighted deformation uses gallery-space bind coordinates.
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    return group_object(o,bone)

def fit_proxy(path,base):
    verts,uv,faces,groups=readobj(path.with_suffix('.obj'));sc=[1,1,1];mapping=[];started=False
    for l in path.with_suffix('.mhclo').read_text().splitlines():
        a=l.split()
        if not a or a[0].startswith('#'):continue
        if a[0] in ['x_scale','y_scale','z_scale']:
            axis={'x_scale':0,'y_scale':1,'z_scale':2}[a[0]]
            sc[axis]=abs(base[int(a[1])][axis]-base[int(a[2])][axis])/float(a[3])
        elif a[0]=='verts':started=True
        elif started and a[0].isdigit():
            if len(a)==1:mapping.append(base[int(a[0])])
            elif len(a)>=9:
                ids=[int(x) for x in a[:3]];ws=[float(x) for x in a[3:6]];ofs=[float(x) for x in a[6:9]]
                mapping.append([sum(base[idx][d]*w for idx,w in zip(ids,ws))+ofs[d]*sc[d] for d in range(3)])
        elif started and a[0] not in ['material','weight']:break
    assert len(mapping)==len(verts),(str(path),len(mapping),len(verts))
    return mapping,uv,faces

def create_skin(vs,uv,faces,groups,skinmat,neck_crop=None):
    # Neck/head only; no concealed full body is shipped or rendered.
    selected=[f for f,g in zip(faces,groups) if g=='body' and min(vs[x[0]][1]*S+OFFSET for x in f)>(neck_crop if neck_crop is not None else (1.82 if S<.2 else 1.68)) and max(abs(vs[x[0]][0]*S) for x in f)<(.14 if S<.2 else .213)]
    indices=sorted(set(x[0] for f in selected for x in f));remap={v:i for i,v in enumerate(indices)}
    ob=meshobj('Anatomical face and neck',[vec(vs[i]) for i in indices],[[remap[x[0]] for x in f] for f in selected],skinmat,[[uv[x[1]] for x in f] for f in selected])
    ob.shape_key_add(name='Basis')
    for name,files in [('Blink',['eye-left-closure.target','eye-right-closure.target']),('Speech',['mouth-parling.target'])]:
        key=ob.shape_key_add(name=name)
        for f in files:
            morph=target('expression/units/asian/'+f)
            for old,d in morph.items():
                if old in remap:
                    amount=.93 if name=='Blink' and S<.2 else 1
                    co=key.data[remap[old]].co;co.x+=d[0]*S*amount;co.y-=d[2]*S*amount;co.z+=d[1]*S*amount
    if S<.2:
        expression=ob.shape_key_add(name='Expression')
        for i,old in enumerate(indices):
            x,y,z=vs[old][0]*S,vs[old][1]*S+OFFSET,vs[old][2]*S
            brow=math.exp(-((abs(x)-.039)/.026)**2-((y-2.119)/.012)**2)
            mouth=math.exp(-((abs(x)-.027)/.012)**2-((y-2.024)/.010)**2)
            if z>.085:expression.data[i].co.z+=(.0012*brow*(1 if x>0 else -.3)+.0006*mouth)
    for bone in ['Chest','Neck','Head']:ob.vertex_groups.new(name=bone)
    for i,old in enumerate(indices):
        y=vs[old][1]*S+OFFSET
        h=max(0,min(1,(y-(1.94 if S<.2 else 1.80))/(.075 if S<.2 else .18)));n=1-h
        if h:ob.vertex_groups['Head'].add([i],h,'REPLACE')
        if n:ob.vertex_groups['Neck'].add([i],n,'REPLACE')
    subd(ob,1)
    mod=ob.modifiers.new('Sovereign deformation rig','ARMATURE');mod.object=rig;ob.parent=rig
    return ob


def sculpt_daoguang(vs):
    # Restrained age detail sculpted on the original anatomical topology; no extra face subdivision.
    for co in vs:
        x,y,z=co[0]*S,co[1]*S+OFFSET,co[2]*S
        if not (1.955<y<2.23 and z>.060):continue
        front=max(0,min(1,(z-.060)/.04))
        cheek=math.exp(-((abs(x)-.057)/.026)**2-((y-2.043)/.027)**2)*front
        jaw=math.exp(-((y-1.998)/.023)**2)*front
        co[0]*=1-.035*cheek-.040*jaw
        depth=-.0038*cheek
        # Shallow infraorbital bags and nasolabial grooves establish the older expression.
        eyes=math.exp(-((abs(x)-.038)/.024)**2)
        depth+=.0015*eyes*math.exp(-((y-2.082)/.0055)**2)
        depth-=.0008*eyes*math.exp(-((y-2.077)/.0028)**2)
        fold_x=.019+(2.052-y)*.52
        depth-=.0008*math.exp(-((abs(x)-fold_x)/.0030)**2-((y-2.029)/.024)**2)
        co[2]+=depth/S
    # A relaxed eyelid opening avoids the rigid wide-eyed base expression.
    for name in ['eye-left-closure.target','eye-right-closure.target']:
        for idx,d in target('expression/units/asian/'+name).items():
            for axis in range(3):vs[idx][axis]+=.07*d[axis]


def facial_root_tint(face):
    # A vertex-color transition belongs to the actual skin, so beard roots cannot float over it.
    mat=face.data.materials[0].copy();mat.name='Daoguang aged skin with beard root transition';face.data.materials[0]=mat
    nodes=mat.node_tree.nodes;links=mat.node_tree.links
    tex=next(n for n in nodes if n.type=='TEX_IMAGE');bsdf=nodes.get('Principled BSDF')
    color=nodes.new('ShaderNodeVertexColor');color.layer_name='FacialTone'
    mix=nodes.new('ShaderNodeMix');mix.data_type='RGBA';mix.blend_type='MULTIPLY';mix.inputs[0].default_value=1
    links.new(tex.outputs['Color'],mix.inputs[6]);links.new(color.outputs['Color'],mix.inputs[7]);links.new(mix.outputs[2],bsdf.inputs['Base Color'])
    attr=face.data.color_attributes.new(name='FacialTone',type='FLOAT_COLOR',domain='POINT')
    for i,v in enumerate(face.data.vertices):
        x,y,z=v.co.x,v.co.z,-v.co.y
        upper_y=2.043-.010*(min(1,abs(x)/.034)**.7)
        upper=math.exp(-((y-upper_y)/.0045)**2)*(max(0,1-(abs(x)/.037)**6))
        chin=math.exp(-(x/.021)**4-((y-1.986)/.0105)**4)
        beard=max(upper*.43,chin*.54) if z>.125 else 0
        # Gentle cool shadow beneath the eyes complements the color detail in the CC0 skin texture.
        eye=.035*math.exp(-((abs(x)-.039)/.025)**2-((y-2.079)/.010)**2) if z>.10 else 0
        attr.data[i].color=(1-beard-eye,1-beard*1.05-eye*1.1,1-beard*1.02-eye*.6,1)


def authored_facial_hair(face):
    # Fit every root to the smoothed skin. The previous fixed-coordinate hairs were partly buried.
    graph=bpy.context.evaluated_depsgraph_get();ev=face.evaluated_get(graph);me=ev.to_mesh()
    surface=BVHTree.FromPolygons([v.co.copy() for v in me.vertices],[list(p.vertices) for p in me.polygons])
    ev.to_mesh_clear()
    dark=material('Dense warm charcoal facial hair',(.038,.029,.023),.88)
    silver=material('Scattered aged grey facial hair',(.19,.175,.15),.91)
    def surface_point(x,y,lift=.0002):
        hit,normal,_,_=surface.ray_cast(Vector(cv((x,y,.50))),Vector(cv((0,0,-1))),1)
        if hit is None:return None
        hit+=normal*lift
        return (hit.x,hit.z,-hit.y)
    for side in [-1,1]:
        for i in range(122):
            u=random.random();x=side*(.0015+.032*u);y=2.043-.010*u**.7+random.uniform(-.0022,.0022)
            xx=x+side*(.0015+.004*u);yy=y-(.004+.008*u)*random.uniform(.75,1.18)
            root=surface_point(x,y);mid=surface_point((x+xx)/2,(y+yy)/2,.00055);tip=surface_point(xx,yy,.00085)
            if root and mid and tip:
                curve('Rooted upper lip moustache',[root,mid,tip],random.uniform(.00010,.00019),silver if random.random()<.13 else dark,'Head',[.66,1,.04])
    for i in range(145):
        x=random.uniform(-.019,.019);y=1.984+random.random()*.012
        root=surface_point(x,y)
        if not root:continue
        end_y=1.956-random.random()*.010+abs(x)*.25
        mid=(x*.88,y-(y-end_y)*.50,root[2]+.0025)
        tip=(x*.52+random.uniform(-.0015,.0015),end_y,root[2]-.0005)
        curve('Rooted short tapered chin beard',[root,mid,tip],random.uniform(.00011,.00020),silver if random.random()<.19 else dark,'Head',[.60,1,.025])

def create_rig(profile):
    bpy.ops.object.armature_add(enter_editmode=True,location=(0,0,0));ob=bpy.context.object;ob.name='DaoguangRig'
    arm=ob.data;arm.name='SovereignPortraitSkeleton';b=arm.edit_bones[0];b.name='Root';b.head=cv((0,.83,0));b.tail=cv((0,1.05,0))
    c=arm.edit_bones.new('Chest');c.head=cv((0,1.10,0));c.tail=cv((0,1.74,0));c.parent=b
    n=arm.edit_bones.new('Neck');n.head=cv((0,1.74,0));n.tail=cv((0,1.93,.02));n.parent=c
    h=arm.edit_bones.new('Head');h.head=cv((0,1.93,.04));h.tail=cv((0,2.27,.04));h.parent=n
    if profile['wardrobe']=='qing':
        b.head=cv((0,0,0));b.tail=cv((0,.50,0))
        pelvis=arm.edit_bones.new('Pelvis');pelvis.head=cv((0,1.00,0));pelvis.tail=cv((0,1.30,0));pelvis.parent=b
        c.head=cv((0,1.28,0));c.tail=cv((0,1.80,0));c.parent=pelvis
        n.head=cv((0,1.81,.025));n.tail=cv((0,1.985,.04));h.head=cv((0,1.985,.04));h.tail=cv((0,2.26,.04))
        for side,label in [(1,'Left'),(-1,'Right')]:
            upper=arm.edit_bones.new(label+'UpperArm');upper.head=cv((side*.292,1.785,.005));upper.tail=cv((side*.363,1.405,.025));upper.parent=c
            a=arm.edit_bones.new(label+'Forearm');a.head=upper.tail;a.tail=cv((side*.378,1.072,.070));a.parent=upper
    else:
        for side,label in [(1,'Left'),(-1,'Right')]:
            a=arm.edit_bones.new(label+'Forearm');a.head=cv((side*.52,1.12,.05));a.tail=cv((side*.265,1.07,.285));a.parent=c
    bpy.ops.object.mode_set(mode='OBJECT');ob.show_in_front=True;return ob

def rigged_mesh(name,verts,faces,mat,uvs,weights):
    ob=meshobj(name,verts,faces,mat,uvs)
    names=sorted(set(n for row in weights for n in row))
    for n in names:ob.vertex_groups.new(name=n)
    for i,row in enumerate(weights):
        for n,w in row.items():
            if w>0:ob.vertex_groups[n].add([i],w,'REPLACE')
    mod=ob.modifiers.new('Sovereign deformation rig','ARMATURE');mod.object=rig;ob.parent=rig
    return ob

QING_ROWS=[(.12,.324,.172,.025),(.18,.324,.172,.025),(.35,.315,.171,.025),(.60,.303,.166,.024),(.82,.282,.158,.024),(1.03,.268,.151,.025),(1.17,.239,.133,.028),(1.25,.226,.128,.033),(1.32,.237,.141,.033),(1.49,.265,.159,.033),(1.66,.276,.159,.028),(1.77,.289,.142,.018),(1.82,.254,.12,.024),(1.902,.092,.076,.043)]

def qing_profile(y):
    for lo,hi in zip(QING_ROWS,QING_ROWS[1:]):
        if lo[0]<=y<=hi[0]:
            t=(y-lo[0])/(hi[0]-lo[0]);return tuple(lo[k]+(hi[k]-lo[k])*t for k in range(1,4))
    return QING_ROWS[0][1:] if y<QING_ROWS[0][0] else QING_ROWS[-1][1:]

def front_depth(x,y,extra=0):
    rx,rz,zc=qing_profile(y);u=max(-1,min(1,x/rx));return zc+rz*max(0,1-u*u)**.44+extra

def tailored_long_robe(silk):
    # Front and back pattern pieces are lofted separately through anatomical chest/waist/hip rows.
    # Vertical darts and asymmetric hanging folds preserve a lean waist rather than inflating a capsule.
    rows=[]
    for lo,hi in zip(QING_ROWS,QING_ROWS[1:]):
        for j in range(3):
            t=j/3;rows.append(tuple(lo[k]+(hi[k]-lo[k])*t for k in range(4)))
    rows.append(QING_ROWS[-1]);n=48
    for sign,label in [(1,'Front'),(-1,'Back')]:
        vs=[];faces=[];uv=[];weights=[]
        for j,(y,rx,rz,zc) in enumerate(rows):
            for i in range(n+1):
                u=i/n*2-1;x=rx*u
                depth=rz*max(0,1-u*u)**.44
                # Long narrow folds radiate from the waist; their relief grows towards the hem.
                hanging=max(0,min(1,(1.18-y)/.85))
                fold=(math.sin(u*math.pi*7+.25*sign)+.34*math.sin(u*math.pi*15))*hanging*.0062*max(0,1-u*u)**.5
                waistdart=-.007*math.exp(-((y-1.34)/.18)**2)*(math.exp(-((u-.52)/.12)**2)+math.exp(-((u+.52)/.12)**2))
                z=zc+sign*(depth+fold+waistdart)
                hemwave=.005*math.cos(u*math.pi*8)*max(0,(.22-y)/.1)
                vs.append(cv((x,y+hemwave,z)))
                chest=max(0,min(1,(y-1.28)/.26));weights.append({'Pelvis':1-chest,'Chest':chest})
        # Distribute the cloth pattern by actual surface distance, not its front-view X projection.
        # Near each side seam the panel turns through depth, requiring more U space per column.
        row_us=[]
        for j in range(len(rows)):
            arc=[0.0];start=j*(n+1)
            for i in range(n):arc.append(arc[-1]+math.dist(vs[start+i],vs[start+i+1]))
            row_us.append([distance/arc[-1] for distance in arc])
        for j in range(len(rows)-1):
            for i in range(n):
                a=j*(n+1)+i;q=[a,a+1,a+n+2,a+n+1]
                faces.append(q if sign==1 else list(reversed(q)))
                # Blender's V grows upward; the embedded PNG is drawn neck to hem.
                y0=rows[j][0];y1=rows[j+1][0]
                # The existing 1.25 m row lies inside the belt. Assign the entire face to one
                # UV island, including its boundary corners, so no polygon bridges two motifs.
                chest_island=y0>=1.25-1e-6
                def robe_v(y):
                    if chest_island:return min(.995,.51+(y-1.25)/.48*.47)
                    return .04+(y-.28)/.94*.94
                if y1<=.28:
                    v0=(y0-.12)/.16*.11;v1=(y1-.12)/.16*.11;u_offset=0;u_scale=1
                else:
                    v0=robe_v(y0);v1=robe_v(y1);u_offset=.02;u_scale=.47
                corners=[(u_offset+row_us[j][i]*u_scale,v0),
                         (u_offset+row_us[j][i+1]*u_scale,v0),
                         (u_offset+row_us[j+1][i+1]*u_scale,v1),
                         (u_offset+row_us[j+1][i]*u_scale,v1)]
                uv.append(corners if sign==1 else list(reversed(corners)))
        ob=rigged_mesh(label+' cut imperial robe panel',vs,faces,silk,uv,weights)
        hem=material(label+' woven sea-wave hem',(.52,.34,.065),.68,.02,ROOT/'qing_robe_embroidery_07.png');ob.data.materials.append(hem)
        for poly in ob.data.polygons:
            if sum(ob.data.vertices[i].co.z for i in poly.vertices)/len(poly.vertices)<=.28:poly.material_index=1
        # Front and back already enclose the robe. Hidden doubled inner surfaces add no visible detail.
        subd(ob,1)

def shoulder_cape(navy,red,gold):
    # Four separate overlapping shoulder pieces, with a visible vermilion lining at the hem.
    def at(a,t):
        outerx=.430;outerz=.151 if math.cos(a)>0 else .177
        spread=math.sin(t*math.pi/2)
        rx=.105+(outerx-.105)*spread;rz=.083+(outerz-.083)*spread
        petal=.009*math.cos(4*a)
        # The shoulder takes the cloth's weight; its outer side falls alongside the upper arm.
        # A shallow front bib follows the chest instead of projecting forward like a brim.
        outer_y=1.735-.115*abs(math.sin(a))**3+petal
        y=1.910-(1.910-outer_y)*t**1.7-.010*t**6
        # Uneven shoulder support and radial cloth folds break the old rigid shell silhouette.
        sag=(-.020*max(0,math.sin(a))+.006*max(0,-math.sin(a)))*t**1.8
        fold=(.010*math.sin(11*a+2.3*t)+.0045*math.sin(19*a-1.2*t))*math.sin(math.pi*t)**.75
        edge=.0055*math.sin(9*a+.65)*t**5
        return (math.sin(a)*(rx+fold*.30),y+sag+fold+edge,math.cos(a)*(rz+fold*.32)+.043)
    for panel in range(4):
        lo=-math.pi/4+panel*math.pi/2;hi=lo+math.pi/2
        vs=[];faces=[];uv=[]
        for j in range(13):
            for i in range(29):
                a=lo+(hi-lo)*i/28;p=at(a,j/12);vs.append(cv(p))
        for j in range(12):
            for i in range(28):
                a=j*29+i;faces.append([a+29,a+30,a+1,a])
                corners=[]
                for ii,jj in [(i,j+1),(i+1,j+1),(i+1,j),(i,j)]:
                    aa=lo+(hi-lo)*ii/28;rr=.10+.36*jj/12
                    corners.append((.5+math.sin(aa)*rr,.5+math.cos(aa)*rr))
                uv.append(corners)
        ob=meshobj('Embroidered shoulder cape panel '+str(panel),vs,faces,navy,uv)
        sol=ob.modifiers.new('Cape red-backed cloth','SOLIDIFY');sol.thickness=.0038;sol.offset=-.6;ob.data.materials.append(red);sol.material_offset=1;sol.material_offset_rim=1;subd(ob,1);group_object(ob,'Chest')
        # Fine dragon/cloud embroidery comes from the silk texture; only silhouette bindings need geometry.
        for t in [.955,.995]:curve('Cape gold cord',[at(lo+(hi-lo)*i/24,t) for i in range(25)],.0010,gold,'Chest')

def robe():
    silk=material('Imperial golden woven silk',(.52,.34,.065),.68,.02,ROOT/'imperial_dragon_brocade_v2.png')
    navy=material('Ink blue shoulder cape and cuffs',(.008,.019,.026),.78,.0,ROOT/'imperial_navy_brocade_v1.png')
    gold=material('Antique gold silk embroidery',(.66,.45,.14),.55,.18)
    red=material('Vermilion silk lining',(.30,.027,.017),.77)
    black=material('Dark court hat felt',(.011,.013,.014),.89)
    hatred=material('Crimson court hat tassels',(.27,.020,.024),.76)
    tailored_long_robe(silk)
    shoulder_cape(navy,red,gold)
    # A narrow raised collar and fitted belt establish the neck and waist rather than a barrel silhouette.
    lathe('Close fitting dark collar',[(1.895,.093,.078,.043),(1.926,.086,.075,.047),(1.953,.084,.072,.051)],gold,'Neck',64)
    curve('Fine collar binding',[(math.sin(i*math.pi/16)*.085,1.954,math.cos(i*math.pi/16)*.073+.051) for i in range(33)],.0017,gold,'Neck')
    belt=lathe('Narrow woven court belt',[(1.222,.235,.141,.033),(1.228,.236,.142,.033),(1.276,.235,.143,.033),(1.282,.233,.14,.033)],navy,'Pelvis',96)
    for y in [1.227,1.277]:curve('Belt embroidered edge',[(math.sin(i*math.pi/24)*.237,y,math.cos(i*math.pi/24)*.144+.033) for i in range(49)],.002,gold,'Pelvis')
    sphere('Carved belt plaque',(0,1.25,.185),(.035,.021,.005),gold,'Pelvis')
    sphere('Belt plaque green stone',(0,1.25,.192),(.015,.011,.004),material('Court jade',(.17,.26,.19),.32),'Pelvis')
    # A long right-fastening seam and small bindings follow the lean front pattern piece.
    seam=[(.032,1.76),(.17,1.68),(.257,1.58),(.251,1.44),(.218,1.29),(.233,1.12),(.26,.86),(.292,.51),(.309,.19)]
    pts=[(x,y,front_depth(x,y,.004)) for x,y in seam]
    curve('Right-lapped robe binding',pts,.0024,navy,'Chest')
    for x,y in seam[:5]:sphere('Small robe fastening',(x,y,front_depth(x,y,.008)),(.0037,.0037,.003),gold,'Chest')
    # Individually spaced small court beads, hanging naturally from the shoulder cape.
    amber=material('Dark amber court beads',(.34,.17,.046),.30)
    turquoise=material('Court turquoise dividers',(.055,.17,.145),.42)
    for i in range(94):
        a=math.pi*i/93;x=-.139*math.cos(a);y=1.837-.57*math.sin(a);z=front_depth(x,y,.016)
        if y>1.59:z=max(z,.043+.23*math.sin(a)+.015)
        sphere('Court necklace amber',(x,y,z),(.0055,.0055,.0055),amber,'Chest')
    for a in [math.pi*.25,math.pi*.5,math.pi*.75]:
        x=-.139*math.cos(a);y=1.837-.57*math.sin(a);z=front_depth(x,y,.022)
        sphere('Court necklace divider',(x,y,z),(.009,.009,.009),turquoise,'Chest')
    # Waist-hung purse, pendant cords and tassels break the otherwise continuous long robe silhouette.
    for side,yoff in [(-1,0),(1,.08)]:
        x=side*.19
        curve('Waist hanging silk cord',[(x,1.245,.135),(x+side*.018,1.05,.185),(x+side*.025,.87+yoff,.191)],.0027,gold,'Pelvis')
        sphere('Court embroidered purse',(x+side*.025,.81+yoff,.18),(.037,.056,.012),navy,'Pelvis')
        curve('Purse gold trim',[(x+side*.025+math.sin(i*math.pi/12)*.035,.81+yoff+math.cos(i*math.pi/12)*.053,.194) for i in range(25)],.0015,gold,'Pelvis')
        for k in range(9):
            xx=x+(k-4)*.003
            curve('Fine hanging tassel',[(xx,.765+yoff,.193),(xx+side*.004,.68+yoff,.19)],.0008,gold,'Pelvis')
    # Low court crown: broad gold-bound black rim, shallow red crown and slender stacked finial.
    lathe('Low court hat black brim',[(2.155,.119,.151,.051),(2.16,.128,.158,.051),(2.197,.13,.158,.051),(2.216,.124,.150,.051)],black,'Head',96)
    lathe('Wide embroidered gold hat band',[(2.165,.130,.159,.051),(2.169,.131,.160,.051),(2.185,.131,.160,.051),(2.189,.129,.159,.051)],gold,'Head',96)
    lathe('Shallow crimson court crown',[(2.208,.123,.149,.051),(2.234,.122,.141,.051),(2.269,.077,.095,.051),(2.285,.017,.025,.051)],hatred,'Head',96)
    for i in range(78):
        a=i*2*math.pi/78
        curve('Fine radial crimson tassel',[(math.sin(a)*.017,2.286,math.cos(a)*.025+.051),(math.sin(a)*.078,2.268,math.cos(a)*.096+.051),(math.sin(a)*.124,2.230,math.cos(a)*.145+.051)],.00057,hatred)
    lathe('Slender gold finial stem',[(2.280,.020,.020,.051),(2.301,.020,.020,.051),(2.306,.011,.011,.051),(2.384,.009,.009,.051),(2.397,.016,.016,.051)],gold,'Head',48)
    coral=material('Finial coral',(.37,.034,.021),.29)
    for y,rr in [(2.321,.015),(2.373,.012),(2.419,.014)]:sphere('Stacked court finial jewel',(0,y,.051),(rr,rr*1.2,rr),coral)
    leather=material('Black court boots',(.008,.010,.011),.63)
    for side in [-1,1]:
        sphere('Soft black court boot',(side*.108,.081,.065),(.080,.069,.148),leather,'Root')

def add_legacy_sleeves_and_hands(v,uv,faces,groups,skin,profile):
    silk=next((m for m in bpy.data.materials if m.name.startswith('Imperial ochre woven')),None) if profile['wardrobe']=='qing' else None
    if not silk:silk=material('Sleeve fabric',profile['coat'],.78)
    cuff=material('Tailored cuff silk',(.018,.03,.034) if profile['wardrobe']=='qing' else profile['coat'],.69)
    thread=material('Cuff embroidery',(.42,.31,.13),.55,.22)
    def landmark(name):
        ids=set(a[0] for f,g in zip(faces,groups) if g==name for a in f)
        return Vector(tuple(sum(vec(v[i])[j] for i in ids)/len(ids) for j in range(3)))
    for side,label in [(1,'Left'),(-1,'Right')]:
        points=[(side*.26,1.69,-.004),(side*.455,1.62,.0),(side*.54,1.33,.01),(side*.52,1.12,.05),(side*.395,1.09,.16),(side*.265,1.07,.285)]
        radii=[.075,.132,.135,.130,.11,.08];vs=[];fs=[];us=[];n=48
        pts=[Vector(cv(x)) for x in points]
        for j,p in enumerate(pts):
            tangent=(pts[min(j+1,len(pts)-1)]-pts[max(0,j-1)]).normalized();across=tangent.cross(Vector((0,1,0))).normalized();up=tangent.cross(across).normalized()
            for i in range(n):
                a=2*math.pi*i/n;rr=radii[j]*(1+.017*math.cos(7*a));vs.append(p+rr*(math.cos(a)*across+math.sin(a)*up))
        for j in range(len(pts)-1):
            for i in range(n):
                fs.append([j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i]);us.append([(i/n,j/5),((i+1)/n,j/5),((i+1)/n,(j+1)/5),(i/n,(j+1)/5)])
        sleeve=meshobj(label+' bent cloth sleeve',vs,fs,silk,us);subd(sleeve,2)
        for name in ['Chest',label+'Forearm']:sleeve.vertex_groups.new(name=name)
        for i in range(len(vs)):
            w=max(0,min(1,(i//n-2)/3));sleeve.vertex_groups['Chest'].add([i],1-w,'REPLACE');sleeve.vertex_groups[label+'Forearm'].add([i],w,'REPLACE')
        mod=sleeve.modifiers.new('Sovereign deformation rig','ARMATURE');mod.object=rig;sleeve.parent=rig
        # Actual MakeHuman hand topology and skin UVs, rigidly posed from its anatomical wrist landmark.
        wrist=landmark('joint-l-hand' if side==1 else 'joint-r-hand')
        finger=landmark('joint-l-finger-3-4' if side==1 else 'joint-r-finger-3-4')
        target_wrist=Vector(cv((side*.255,1.063,.30)))
        target_direction=Vector(cv((-side*.15,-.03,.26)))
        rotation=(finger-wrist).normalized().rotation_difference(target_direction.normalized())
        selected=[f for f,g in zip(faces,groups) if g=='body' and min(side*v[a[0]][0]*S for a in f)>abs(wrist.x)-.065]
        indices=sorted(set(a[0] for f in selected for a in f));idx={x:i for i,x in enumerate(indices)}
        coords=[target_wrist+rotation@(Vector(vec(v[i]))-wrist)*.88 for i in indices]
        hand=meshobj(label+' anatomical hand',coords,[[idx[a[0]] for a in f] for f in selected],skin,[[uv[a[1]] for a in f] for f in selected]);subd(hand,1);group_object(hand,label+'Forearm')
        # A separate closed cuff hides the wrist seam while preserving the skinning pose.
        tangent=(pts[-1]-pts[-2]).normalized();u=tangent.cross(Vector((0,1,0))).normalized();w=tangent.cross(u).normalized()
        for offset in [-.025,.005]:
            ring=[pts[-1]+tangent*offset+.0805*(math.cos(i*math.pi/12)*u+math.sin(i*math.pi/12)*w) for i in range(25)]
            curve(label+' stitched cuff',[(q.x,q.z,-q.y) for q in ring],.003,thread,label+'Forearm')

def add_sleeves_and_hands(v,uv,faces,groups,skin,profile):
    if profile['wardrobe']!='qing':return add_legacy_sleeves_and_hands(v,uv,faces,groups,skin,profile)
    silk=next(m for m in bpy.data.materials if m.name=='Imperial golden woven silk')
    navy=next(m for m in bpy.data.materials if m.name=='Ink blue shoulder cape and cuffs')
    gold=next(m for m in bpy.data.materials if m.name=='Antique gold silk embroidery')
    def landmark(name):
        ids=set(a[0] for f,g in zip(faces,groups) if g==name for a in f)
        return Vector(tuple(sum(vec(v[i])[j] for i in ids)/len(ids) for j in range(3)))
    for side,label in [(1,'Left'),(-1,'Right')]:
        vs=[];fs=[];uvs=[];weights=[];rows=36;segments=32
        for j in range(rows+1):
            t=j/rows;y=1.79-.70*t
            # The sleeve hangs beside the body, with a slight elbow bend, instead of forming a U.
            x=side*(.284+.09*(1-math.exp(-t*4)));zc=.008+.058*t*t
            rx=.065+.034*math.sin(math.pi*t)**.8-.013*t;rz=.060+.029*math.sin(math.pi*t)**.7-.004*t
            for i in range(segments):
                a=i*2*math.pi/segments
                u=math.copysign(abs(math.sin(a))**.66,math.sin(a));w=math.copysign(abs(math.cos(a))**.66,math.cos(a))
                crease=(.0011*math.sin(t*math.pi*13+.7*math.sin(a*2))+.0007*math.sin(t*math.pi*29-a))*math.sin(math.pi*t)**.7
                crease+=.0032*max(0,-side*math.sin(a))**2*math.exp(-((t-.55)/.19)**2)*math.sin(t*math.pi*23+.4)
                drape=.006*max(0,-math.cos(a))*math.sin(t*math.pi*3)
                vs.append(cv((x+u*(rx+crease),y-.0017*math.cos(a)*math.sin(t*math.pi*7+.3*math.sin(a)),zc+w*(rz+crease+drape))))
                shoulder=max(0,1-t/.15);fore=max(0,min(1,(t-.45)/.26))
                weights.append({'Chest':shoulder,label+'UpperArm':(1-shoulder)*(1-fore),label+'Forearm':(1-shoulder)*fore})
        for j in range(rows):
            for i in range(segments):
                a=j*segments+i;fs.append([a,j*segments+(i+1)%segments,(j+1)*segments+(i+1)%segments,a+segments])
                # One complete dragon quadrant around each sleeve, preserving the weave's two-dimensional scale.
                uvs.append([(.02+i/segments*.47,.51+.47*(1-j/rows)),(.02+(i+1)/segments*.47,.51+.47*(1-j/rows)),(.02+(i+1)/segments*.47,.51+.47*(1-(j+1)/rows)),(.02+i/segments*.47,.51+.47*(1-(j+1)/rows))])
        ob=rigged_mesh(label+' hanging tailored sleeve panels',vs,fs,silk,uvs,weights)
        subd(ob,1)
        # A horse-hoof cuff extends over the back of the relaxed hand, with a deep curved front edge.
        vs=[];fs=[];uvs=[];n=64
        for j in range(6):
            t=j/5
            for i in range(n):
                a=i*2*math.pi/n;lower=1.066-.135*max(0,math.cos(a))**1.8
                y=1.155*(1-t)+lower*t
                vs.append(cv((side*.377+math.sin(a)*(.061+.003*t),y,.069+math.cos(a)*(.056+.003*t))))
        for j in range(5):
            for i in range(n):
                a=j*n+i;fs.append([a,j*n+(i+1)%n,(j+1)*n+(i+1)%n,a+n]);uvs.append([(.02+i/n*.48,.25+j/5*.22),(.02+(i+1)/n*.48,.25+j/5*.22),(.02+(i+1)/n*.48,.25+(j+1)/5*.22),(.02+i/n*.48,.25+(j+1)/5*.22)])
        cuff=meshobj(label+' embroidered horse hoof cuff',vs,fs,navy,uvs);sol=cuff.modifiers.new('Cuff thickness','SOLIDIFY');sol.thickness=.003;subd(cuff,1);group_object(cuff,label+'Forearm')
        for offset in [0,.013]:
            pts=[]
            for i in range(65):
                a=i*2*math.pi/64;pts.append((side*.377+math.sin(a)*.065,1.066-.135*max(0,math.cos(a))**1.8+offset,.069+math.cos(a)*.060))
            curve(label+' horse hoof gold binding',pts,.0016,gold,label+'Forearm')
        wrist=landmark('joint-l-hand' if side==1 else 'joint-r-hand');finger=landmark('joint-l-finger-3-4' if side==1 else 'joint-r-finger-3-4')
        target_wrist=Vector(cv((side*.379,1.075,.067)));target_direction=Vector(cv((side*.011,-.215,.023))).normalized()
        rotation=(finger-wrist).normalized().rotation_difference(target_direction)
        rotation=Quaternion(target_direction,side*.75) @ rotation
        selected=[f for f,g in zip(faces,groups) if g=='body' and min(side*v[a[0]][0]*S for a in f)>abs(wrist.x)-.042]
        indices=sorted(set(a[0] for f in selected for a in f));idx={x:i for i,x in enumerate(indices)}
        coords=[]
        for i in indices:
            local=rotation@(Vector(vec(v[i]))-wrist)
            along=local.dot(target_direction);cross=local-target_direction*along
            # Relax the open template hand: converge the fingers, retaining the complete anatomical skin mesh.
            relax=max(0,min(1,(along-.065)/.13));local=target_direction*along+cross*(1-.32*relax)
            coords.append(target_wrist+local)
        hand=meshobj(label+' relaxed anatomical hand',coords,[[idx[a[0]] for a in f] for f in selected],skin,[[uv[a[1]] for a in f] for f in selected]);subd(hand,1);group_object(hand,label+'Forearm')

def animate(face):
    bpy.context.scene.frame_start=1;bpy.context.scene.frame_end=241;bpy.context.scene.render.fps=30
    for frame in range(1,242,10):
        t=(frame-1)/30
        for name in ['Chest','Neck','Head']:rig.pose.bones[name].rotation_mode='XYZ'
        chest=rig.pose.bones['Chest'];chest.scale=(1+math.sin(t*math.pi/2)*.006,1+math.sin(t*math.pi/2)*.009,1+math.sin(t*math.pi/2)*.004);chest.keyframe_insert('scale',frame=frame)
        head=rig.pose.bones['Head'];head.rotation_euler=(math.sin(t*math.pi/4)*.012,math.sin(t*math.pi/4+.3)*.032,math.sin(t*math.pi/2)*.008);head.keyframe_insert('rotation_euler',frame=frame)
        neck=rig.pose.bones['Neck'];neck.rotation_euler=(0,math.sin(t*math.pi/4)*.008,0);neck.keyframe_insert('rotation_euler',frame=frame)
        for sign,label in [(1,'Left'),(-1,'Right')]:
            arm=rig.pose.bones[label+'Forearm'];arm.rotation_mode='XYZ';arm.rotation_euler=(math.sin(t*math.pi/4)*.008,0,sign*math.sin(t*math.pi/4)*.005);arm.keyframe_insert('rotation_euler',frame=frame)
        if 'Pelvis' in rig.pose.bones:
            head.rotation_euler.y+=.018;head.rotation_euler.z-=.007;head.keyframe_insert('rotation_euler',frame=frame)
            for label,offset in [('Left',.011),('Right',-.006)]:
                upper=rig.pose.bones[label+'UpperArm'];upper.rotation_mode='XYZ'
                upper.rotation_euler=(offset,0,offset*.5);upper.keyframe_insert('rotation_euler',frame=frame)
    action=rig.animation_data.action;action.name='idle';track=rig.animation_data.nla_tracks.new();track.name='idle';track.strips.new('idle',1,action);rig.animation_data.action=None
    blink=face.data.shape_keys.key_blocks['Blink']
    for frame,val in [(1,0),(59,0),(62,1),(65,0),(158,0),(161,1),(164,0),(171,0),(174,.82),(177,0),(241,0)]:blink.value=val;blink.keyframe_insert('value',frame=frame)
    if 'Expression' in face.data.shape_keys.key_blocks:
        expression=face.data.shape_keys.key_blocks['Expression']
        for frame,val in [(1,.08),(42,.25),(99,.12),(141,.44),(190,.23),(241,.08)]:expression.value=val;expression.keyframe_insert('value',frame=frame)
    action=face.data.shape_keys.animation_data.action;action.name='idle_face';track=face.data.shape_keys.animation_data.nla_tracks.new();track.name='idle';track.strips.new('idle',1,action);face.data.shape_keys.animation_data.action=None
    bpy.context.scene.frame_set(1)

def bake_face_subdivision(face):
    # Bake Catmull-Clark independently for every morph, preserving topology and skin weights.
    # Godot receives the detailed face rather than depending on a Blender-only modifier.
    arm=next(m for m in face.modifiers if m.type=='ARMATURE');arm.show_viewport=False
    keys=face.data.shape_keys.key_blocks;names=[k.name for k in keys][1:];coords=[]
    graph=bpy.context.evaluated_depsgraph_get()
    for name in [None]+names:
        for key in keys:key.value=1 if name==key.name else 0
        bpy.context.view_layer.update();ev=face.evaluated_get(graph);me=ev.to_mesh()
        coords.append([v.co.copy() for v in me.vertices]);ev.to_mesh_clear()
    for key in keys:key.value=0
    bpy.context.view_layer.update();ev=face.evaluated_get(graph)
    detailed=bpy.data.meshes.new_from_object(ev,preserve_all_data_layers=True,depsgraph=graph)
    face.data=detailed
    for m in list(face.modifiers):
        if m.type=='SUBSURF':face.modifiers.remove(m)
    face.shape_key_add(name='Basis')
    for name,points in zip(names,coords[1:]):
        key=face.shape_key_add(name=name)
        for v,co in zip(key.data,points):v.co=co
    arm.show_viewport=True


def bake_skin_detail(face):
    # Bake the procedural micro-normal into a portable tangent map, also used by the exported GLB.
    # This is surface relief authored in 3D; the original CC0 skin color image stays unchanged.
    mat=face.data.materials[0];nodes=mat.node_tree.nodes;links=mat.node_tree.links
    bsdf=nodes.get('Principled BSDF');noise=nodes.new('ShaderNodeTexNoise')
    noise.inputs['Scale'].default_value=235;noise.inputs['Detail'].default_value=2.3;noise.inputs['Roughness'].default_value=.65
    bump=nodes.new('ShaderNodeBump');bump.inputs['Distance'].default_value=.00028;bump.inputs['Strength'].default_value=.20
    links.new(noise.outputs['Fac'],bump.inputs['Height']);links.new(bump.outputs['Normal'],bsdf.inputs['Normal'])
    img=bpy.data.images.new('Daoguang authored skin pore normal',width=2048,height=2048,alpha=False)
    img.colorspace_settings.name='Non-Color';img.generated_color=(.5,.5,1,1)
    dest=nodes.new('ShaderNodeTexImage');dest.image=img;nodes.active=dest
    bpy.ops.object.select_all(action='DESELECT');face.select_set(True);bpy.context.view_layer.objects.active=face
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=8
    scene.render.bake.margin=12;scene.render.bake.use_clear=True;scene.render.bake.normal_space='TANGENT'
    bpy.ops.object.bake(type='NORMAL')
    img.filepath_raw=str(ROOT/'daoguang_skin_detail_normal.png');img.file_format='PNG';img.save()
    nodes.remove(bump);nodes.remove(noise)
    normal=nodes.new('ShaderNodeNormalMap');normal.inputs['Strength'].default_value=.68
    links.new(dest.outputs['Color'],normal.inputs['Color']);links.new(normal.outputs['Normal'],bsdf.inputs['Normal'])

def consolidate_meshes(face):
    # Hundreds of individual bead/strand authoring objects become a few draw calls.
    groups={}
    for ob in list(bpy.context.scene.objects):
        if ob.type!='MESH' or ob==face:continue
        bpy.ops.object.select_all(action='DESELECT');ob.select_set(True);bpy.context.view_layer.objects.active=ob
        for mod in list(ob.modifiers):
            if mod.type!='ARMATURE':bpy.ops.object.modifier_apply(modifier=mod.name)
        key=ob.data.materials[0].name if len(ob.data.materials)==1 else ob.name
        groups.setdefault(key,[]).append(ob)
    for key,objects in groups.items():
        if len(objects)<2:continue
        bpy.ops.object.select_all(action='DESELECT')
        for ob in objects:ob.select_set(True)
        bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join();objects[0].name=key+' combined geometry'

def setup_render(fullbody=False):
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=48;scene.cycles.use_denoising=True
    scene.world.color=(.025,.025,.025)
    def light(name,at,energy,color,size):
        d=bpy.data.lights.new(name,'AREA');d.energy=energy;d.color=color;d.shape='DISK';d.size=size;o=bpy.data.objects.new(name,d);bpy.context.collection.objects.link(o);o.location=cv(at);o.rotation_euler=(Vector(cv((0,1.9,.07)))-o.location).to_track_quat('-Z','Y').to_euler()
    light('Portrait key',(-2,3.8,3),160,(1,.83,.65),3);light('Soft fill',(2,2.8,2),60,(.73,.84,1),3);light('Silhouette',(1,3.1,-1),160,(1,.75,.45),2)
    d=bpy.data.cameras.new('Portrait QA Camera');c=bpy.data.objects.new('Portrait QA Camera',d);bpy.context.collection.objects.link(c);c.location=cv((.38,2.0,3.25));c.rotation_euler=(Vector(cv((0,1.71,0)))-c.location).to_track_quat('-Z','Y').to_euler();d.lens=58;scene.camera=c
    if fullbody:
        c.location=cv((.24,1.42,4.0));c.rotation_euler=(Vector(cv((0,1.23,0)))-c.location).to_track_quat('-Z','Y').to_euler();d.lens=54
    scene.render.resolution_x=950;scene.render.resolution_y=1100;scene.render.resolution_percentage=100;scene.view_settings.view_transform='AgX'

PROFILES={
 'daoguang':dict(sex='male',eth='asian',age=53,wardrobe='qing',width=1.0,coat=(.44,.29,.07)),
 'william_iv':dict(sex='male',eth='caucasian',age=70,wardrobe='naval',width=1.11,coat=(.024,.037,.066)),
 'victoria':dict(sex='female',eth='caucasian',age=18,wardrobe='gown',width=.97,coat=(.56,.48,.36)),
 'tokugawa_ienari':dict(sex='male',eth='asian',age=62,wardrobe='shogun',width=1.06,coat=(.027,.053,.045)),
 'tokugawa_ieyoshi':dict(sex='male',eth='asian',age=44,wardrobe='shogun',width=.95,coat=(.067,.075,.032)),
}

def fitted_hair(v,profile):
    if profile['wardrobe']=='shogun':
        hair=material('Lacquer-black shogunal hair',(.012,.013,.012),.65)
        # A shaved forehead and tied chonmage; the scalp cap follows the back of the anatomical skull.
        lathe('Back-of-head tied hair',[(2.08,.155,.095,-.035),(2.2,.157,.115,-.026),(2.30,.13,.115,.0),(2.36,.065,.08,.035)],hair,'Head',64)
        curve('Folded topknot',[(0,2.3,-.065),(0,2.39,-.015),(0,2.37,.12),(0,2.34,.11)],.034,hair,'Head',[.8,1,.9,.55])
        return
    key='braid01' if profile['sex']=='female' else ('short04' if profile['age']>55 else 'short02')
    hv,hu,hf=fit_proxy(SYS/f'hair/{key}/{key}',v)
    image=SYS/f'hair/{key}/{key}_diffuse.png'
    if profile['sex']=='female':
        # Symmetric centre-parted side sections, with the braid gathered into the modeled back bun.
        hf=[q for q in hf if sum(hv[a[0]][1]*S+OFFSET for a in q)/len(q)>1.94 and sum(hv[a[0]][0] for a in q)/len(q)>=0]
        count=len(hv);hv=[(max(0,x),y,z) for x,y,z in hv]
        hv += [(-x,y,z) for x,y,z in hv]
        hf += [[(a[0]+count,a[1]) for a in reversed(q)] for q in list(hf)]
    hm=material('Period hair strands',(.05,.034,.024),.76,0,image)
    p=hm.node_tree.nodes.get('Principled BSDF');t=next(n for n in hm.node_tree.nodes if n.type=='TEX_IMAGE')
    hm.node_tree.links.new(t.outputs['Alpha'],p.inputs['Alpha']);hm.surface_render_method='DITHERED'
    if profile['sex']=='male' and profile['age']>55:
        # Receding temples and grey side hair belong to the aged profile, not the younger successor.
        hm.node_tree.links.remove(p.inputs['Base Color'].links[0]);p.inputs['Base Color'].default_value=(.32,.31,.265,1)
        # Keep complete swept-back strand topology; cutting cards leaves visible geometric holes.
    ob=meshobj('Fitted strand hairstyle',[vec(x) for x in hv],[[a[0] for a in q] for q in hf],hm,[[hu[a[1]] for a in q] for q in hf]);group_object(ob,'Head')
    if profile['sex']=='female':
        # Period silhouette: restrained back bun and paired pearl ornaments.
        sphere('Coiled back bun',(0,2.135,-.17),(.105,.102,.045),material('Chestnut coiled bun',(.06,.029,.013),.8))
        sphere('Centre part underlay',(0,2.365,.052),(.067,.032,.083),material('Centre part brown hair',(.03,.024,.017),.82))
        diadem=material('Small gold court diadem',(.49,.34,.135),.34,.6)
        curve('Court diadem band',[(-.115,2.30,.16),(-.06,2.34,.205),(0,2.35,.222),(.06,2.34,.205),(.115,2.30,.16)],.0032,diadem)
        for i in range(-3,4):
            x=i*.03;yy=2.365-.13*abs(x);sphere('Diadem pearl',(x,yy,.22-.5*abs(x)),(.0045,.006,.0045),diadem)
        pm=material('Pearl hair pins',(.8,.75,.65),.24)
        for side in [-1,1]:
            sphere('Pearl earring',(side*.177,1.99,.103),(.01,.017,.01),pm)
    elif profile['age']>55:
        hair=material('Grey sideburns',(.22,.215,.18),.9)
        for side in [-1,1]:
            for i in range(35):
                u=i/34
                curve('Tapered sideburn strand',[(side*(.145+.017*u),2.135,.11),(side*(.150+.013*u),2.03,.123),(side*(.12+.019*u),1.991,.153)],.0007,hair,'Head',[.6,1,.03])

def formal_clothing(profile):
    female=profile['sex']=='female';japanese=profile['wardrobe']=='shogun'
    cloth=material('Tailored '+profile['wardrobe'],profile['coat'],.79)
    trim=material('Fine gold embroidery',(.43,.31,.115),.51,.4)
    cream=material('Fine linen collar',(.71,.66,.54),.84)
    dark=material('Black silk neckcloth',(.012,.015,.018),.58)
    levels=[(.83,.38,.225,-.015),(.90,.387,.23,-.015),(1.05,.40,.24,-.015),(1.3,.43,.25,-.018),(1.52,.455,.245,-.022),(1.68,.436,.217,-.015),(1.77,.34,.174,.016),(1.82,.17,.132,.049)]
    lathe('Tailored torso',levels,cloth,flutes=.008)
    if japanese:
        ivory=material('Kimono lining',(.56,.56,.47),.88)
        for sign in [-1,1]:
            curve('Folded kimono lapel',[(sign*.147,1.805,.19),(sign*.08,1.68,.258),(-sign*.055,1.49,.264),(-sign*.15,1.3,.25)],.032,ivory,'Chest')
            curve('Kimono collar dark binding',[(sign*.147,1.812,.184),(sign*.08,1.68,.265),(-sign*.055,1.49,.270),(-sign*.15,1.3,.255)],.016,cloth,'Chest')
        for side in [-1,1]:
            # Original simple round mon silhouette; not a claim of an exact garment.
            sphere('Family crest roundel',(side*.285,1.59,.215),(.036,.036,.0025),cream,'Chest')
            for a in [0,2*math.pi/3,4*math.pi/3]:
                sphere('Trefoil crest',(side*.285+math.sin(a)*.014,1.59+math.cos(a)*.014,.219),(.012,.014,.002),cloth,'Chest')
        return
    if female:
        lace=material('Ivory lace',(.81,.75,.65),.85)
        for side in [-1,1]:
            curve('Pleated dress neckline',[(side*.02,1.77,.215),(side*.16,1.77,.216),(side*.31,1.715,.20),(side*.38,1.66,.173)],.025,lace,'Chest')
        pm=material('Portrait pearl necklace',(.75,.7,.59),.3)
        for i in range(39):
            a=math.pi*i/38;sphere('Pearl necklace',(-.151*math.cos(a),1.83-.125*math.sin(a),.176+.074*math.sin(a)),(.005,.005,.005),pm,'Chest')
        sphere('Royal dress brooch',(0,1.64,.265),(.025,.035,.009),trim,'Chest')
        sphere('Sapphire cabochon',(0,1.64,.276),(.016,.023,.008),material('Sapphire',(.015,.059,.10),.2,.15),'Chest')
        return
    lathe('Standing linen collar',[(1.77,.164,.13,.064),(1.79,.17,.137,.064),(1.83,.163,.129,.064),(1.86,.145,.116,.064)],cream)
    # Two tailored lapels are solid cloth panels rather than pasted illustrations.
    for sign in [-1,1]:
        pts=[(sign*.14,1.81,.197),(sign*.225,1.74,.22),(sign*.156,1.64,.263),(sign*.05,1.50,.266),(sign*.066,1.68,.261)]
        ob=meshobj('Folded uniform lapel',[cv(x) for x in pts],[[0,1,2,3,4]],cloth);so=ob.modifiers.new('Cloth thickness','SOLIDIFY');so.thickness=.006;group_object(ob,'Chest')
        curve('Lapel fine piping',pts[:4],.0022,trim,'Chest')
    sphere('Silk cravat knot',(0,1.787,.205),(.027,.025,.024),dark,'Chest')
    curve('Folded silk cravat',[(0,1.77,.214),(0,1.69,.248),(0,1.57,.266)],.026,dark,'Chest',[1,1,.55])
    for y in [1.40,1.23,1.06,.9]:sphere('Brass front button',(.021,y,.248),(.008,.008,.006),trim,'Chest')
    if profile['wardrobe']!='statesman':
        for side in [-1,1]:
            for i in range(18):
                x=side*(.265+i*.008)
                curve('Gold epaulette fringe',[(x,1.745,.105),(x,1.7,.183),(x,1.615,.192)],.003,trim,'Chest',[1,1,.55])
        ribbon=material('Decorative royal ribbon',(.30,.056,.044),.78)
        for x in [-.18,-.105]:
            curve('Medal ribbon',[(x,1.59,.263),(x,1.53,.266)],.018,ribbon,'Chest')
            sphere('Service medal',(x,1.493,.272),(.020,.025,.004),trim,'Chest')

def main(identity):
    global rig,S,OFFSET
    # These authored source files are reproducible; avoid packaging transient Blender backup copies.
    bpy.context.preferences.filepaths.save_version=0
    profile=PROFILES[identity]
    random.seed(1836+sum(map(ord,identity)))
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    v,uv,f,g=readobj(MH/'3dobjs/base.obj')
    old=max(0,min(1,(profile['age']-25)/70+(.10 if identity=='daoguang' else 0)));sex=profile['sex'];eth=profile['eth']
    for n,w in [(f'{eth}-{sex}-young',1-old),(f'{eth}-{sex}-old',old),(f'universal-{sex}-young-averagemuscle-averageweight',1-old),(f'universal-{sex}-old-averagemuscle-averageweight',old)]:
        for idx,d in target('macrodetails/'+n+'.target').items():
            for a in range(3):v[idx][a]+=d[a]*w
    # Mild sculpted width variation keeps identities distinct without representing these as scans.
    for co in v:
        if co[1]>5:co[0]*=profile['width']
    # Eye marker mean fixes consistent framing while retaining anatomical differences.
    eye_indices=set(a[0] for q,group in zip(f,g) if group=='joint-l-eye' for a in q)
    S=.136 if profile['wardrobe']=='qing' else .22;OFFSET=2.10-sum(v[i][1] for i in eye_indices)/len(eye_indices)*S
    if identity=='daoguang':sculpt_daoguang(v)
    rig=create_rig(profile);rig.name=identity+'Rig'
    if sex=='female':skinpath=next((SYS/'skins/young_caucasian_female').glob('*.png'))
    elif eth=='asian':skinpath=next((SYS/('skins/old_asian_male' if profile['age']>45 else 'skins/young_asian_male')).glob('*.png'))
    else:skinpath=next((SYS/('skins/old_caucasian_male' if profile['age']>55 else 'skins/middleage_caucasian_male')).glob('*.png'))
    sk=material('Middle-aged skin, CC0 MakeHuman',(.58,.42,.3),.59,0,skinpath)
    pr=sk.node_tree.nodes.get('Principled BSDF');pr.inputs['Subsurface Weight'].default_value=.065;pr.inputs['Subsurface Radius'].default_value=(1,.38,.2)
    if identity=='daoguang':
        pr.inputs['Subsurface Weight'].default_value=.032;pr.inputs['Roughness'].default_value=.64;pr.inputs['Specular IOR Level'].default_value=.28
    face=create_skin(v,uv,f,g,sk)
    if identity=='daoguang':facial_root_tint(face)
    ev,eu,ef=fit_proxy(SYS/'eyes/high-poly/high-poly',v)
    # The CC0 eye proxy includes a transparent corneal shell mapped to the alpha-zero corner.
    # Remove that shell for portable real-time materials; retain the fitted iris/sclera geometry.
    ef=[q for q in ef if not all(eu[a[1]][0]>.86 and eu[a[1]][1]<.15 for a in q)]
    eye_mat=material('Brown iris and sclera',(.7,.68,.60),.40 if identity=='daoguang' else .19,0,SYS/'eyes/materials/brown_eye.png')
    if identity=='daoguang':
        nodes=eye_mat.node_tree.nodes;links=eye_mat.node_tree.links;bsdf=nodes.get('Principled BSDF')
        bsdf.inputs['Specular IOR Level'].default_value=.20;bsdf.inputs['IOR'].default_value=1.36
        tex=next(n for n in nodes if n.type=='TEX_IMAGE');mix=nodes.new('ShaderNodeMix');mix.data_type='RGBA';mix.blend_type='MULTIPLY';mix.inputs[0].default_value=1
        mix.inputs[7].default_value=(.70,.67,.61,1);links.new(tex.outputs['Color'],mix.inputs[6]);links.new(mix.outputs[2],bsdf.inputs['Base Color'])
    eyes=meshobj('Anatomical fitted eyes',[vec(x) for x in ev],[[a[0] for a in q] for q in ef],eye_mat,[[eu[a[1]] for a in q] for q in ef]);subd(eyes,1);group_object(eyes,'Head')
    # Eyebrows are real fitted mesh, with the original CC0 strand alpha texture.
    bv,bu,bf=fit_proxy(SYS/'eyebrows/eyebrow001/eyebrow001',v)
    bmat=material('Charcoal eyebrows',(.04,.035,.027),.9,0,SYS/'eyebrows/eyebrow001/eyebrow001.png')
    bp=bmat.node_tree.nodes.get('Principled BSDF');bt=next(n for n in bmat.node_tree.nodes if n.type=='TEX_IMAGE');bmat.node_tree.links.new(bt.outputs['Alpha'],bp.inputs['Alpha']);bmat.surface_render_method='DITHERED'
    brows=meshobj('Natural fitted eyebrows',[vec(x) for x in bv],[[a[0] for a in q] for q in bf],bmat,[[bu[a[1]] for a in q] for q in bf]);group_object(brows,'Head')
    if identity=='daoguang':robe();authored_facial_hair(face)
    else:formal_clothing(profile);fitted_hair(v,profile)
    add_sleeves_and_hands(v,uv,f,g,sk,profile)
    bake_face_subdivision(face)
    if identity=='daoguang':bake_skin_detail(face)
    consolidate_meshes(face);animate(face)
    # Source contains cameras/lights for repeatable QA; GLB contains only the animated character.
    for o in bpy.context.scene.objects:o.select_set(o.type in {'ARMATURE','MESH'})
    bpy.context.view_layer.objects.active=rig
    bpy.ops.export_scene.gltf(filepath=str(OUT/(identity+'.glb')),export_format='GLB',use_selection=True,export_apply=False,export_animations=True,export_animation_mode='NLA_TRACKS',export_nla_strips_merged_animation_name='idle',export_morph=True,export_skins=True,export_force_sampling=True,export_frame_range=True)
    setup_render(profile['wardrobe']=='qing');bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/(identity+'.blend')));bpy.ops.file.make_paths_relative();bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/(identity+'.blend')))
    bpy.context.scene.render.filepath=str(ROOT/(identity+'-qa.png'));bpy.ops.render.render(write_still=True)
    if identity=='daoguang':
        scene=bpy.context.scene;c=scene.camera;c.location=cv((.14,2.145,.97));c.rotation_euler=(Vector(cv((0,2.10,.09)))-c.location).to_track_quat('-Z','Y').to_euler();c.data.lens=68
        scene.cycles.samples=80;scene.render.filepath=str(ROOT/'daoguang-face-qa.png');bpy.ops.render.render(write_still=True)
    print('SOVEREIGN_AUTHORED_LEADER_COMPLETE',len(bpy.data.objects),str(OUT/(identity+'.glb')))
if __name__=='__main__':
    names=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else ['daoguang']
    for identity in names:main(identity)

