"""Daoguang 0.13: quieter skin color, dimensional grooming and soft tailoring.

Blender --background --python build_leaders_13.py -- --out-dir <candidate>
Uses unmodified CC0 MakeHuman skin, with original skinned geometry and grooming.
The 0.12 recipe stays reproducible. No raster portrait replaces the 3D face.
"""
import sys, math, random, argparse
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree
import build_leaders as b
import build_leaders_11 as base
import build_leaders_12 as previous


def skin_material():
    # The old full-body atlas has strong low-frequency mottling in its small
    # face island. Age belongs primarily to anatomy, not magnified color noise.
    skin = previous.skin_material()
    skin.name = 'Daoguang skin 0.13 balanced middle-age albedo'
    color = next(n for n in skin.node_tree.nodes if n.type == 'TEX_IMAGE'
                 and n.image and 'diffuse' in n.image.name)
    color.image = bpy.data.images.load(str(next((b.SYS/'skins/middleage_asian_male').glob('*.png'))), check_existing=True)
    for node in skin.node_tree.nodes:
        if node.type == 'NORMAL_MAP':
            node.inputs['Strength'].default_value = .10
    return skin


def refine_skin_tone(face):
    # Reuse the export-safe vertex-color multiplication, but replace the old
    # 54% charcoal patch beneath the chin with a restrained follicle transition.
    b.facial_root_tint(face)
    face.data.materials[0].name = 'Daoguang skin 0.13 subtle follicle transition'
    for vertex, color in zip(face.data.vertices, face.data.color_attributes['FacialTone'].data):
        x, y, z = vertex.co.x, vertex.co.z, -vertex.co.y
        front = max(0, min(1, (z-.085)/.045))
        upper_y = 2.043-.010*min(1, abs(x)/.034)**.7
        upper = math.exp(-((y-upper_y)/.004)**2)*max(0, 1-(abs(x)/.037)**6)
        chin = math.exp(-(x/.021)**4-((y-1.986)/.010)**4)
        roots = max(upper*.14, chin*.18)*front
        eye = .025*math.exp(-((abs(x)-.039)/.025)**2-((y-2.079)/.009)**2)*front
        cheek = .016*math.exp(-((abs(x)-.063)/.030)**2-((y-2.052)/.025)**2)*front
        color.color = (.98-roots-eye, .963-roots*.99-eye*1.10-cheek,
                       .94-roots*.94-eye*.60-cheek*.5, 1)


def groom_beard(face):
    """Curved fibres leave the face toward their tips and make a soft silhouette."""
    bpy.context.view_layer.update()
    evaluated = face.evaluated_get(bpy.context.evaluated_depsgraph_get())
    mesh = evaluated.to_mesh()
    tree = BVHTree.FromPolygons([v.co.copy() for v in mesh.vertices],
                               [list(p.vertices) for p in mesh.polygons])
    evaluated.to_mesh_clear()
    rng = random.Random(183613)
    groups = []
    for name, rgb, radius in [
        ('Dark curved beard 0.13', (.019,.016,.013), .000094),
        ('Warm beard strands 0.13', (.046,.037,.026), .000078),
        ('Silver beard strands 0.13', (.17,.15,.125), .000060)]:
        material = b.material(name, rgb, .77)
        curve = bpy.data.curves.new(name, 'CURVE')
        curve.dimensions = '3D'; curve.resolution_u = 3
        curve.bevel_depth = radius; curve.bevel_resolution = 1
        obj = bpy.data.objects.new(name, curve)
        bpy.context.collection.objects.link(obj); obj.data.materials.append(material)
        groups.append(obj)

    def surface(x, y, lift=.0002):
        hit, normal, _, _ = tree.ray_cast(Vector(b.cv((x,y,.5))), Vector(b.cv((0,0,-1))), 1)
        if hit is None: return None
        hit += normal*lift
        return Vector((hit.x, hit.z, -hit.y))

    def strand(points):
        chance = rng.random()
        obj = groups[2 if chance < .09 else 1 if chance < .34 else 0]
        spline = obj.data.splines.new('BEZIER'); spline.bezier_points.add(4)
        for bp, co, radius in zip(spline.bezier_points, points, [.42,1,.82,.48,.025]):
            bp.co = b.cv(co); bp.radius = radius*rng.uniform(.84,1.16)
            bp.handle_left_type = 'AUTO'; bp.handle_right_type = 'AUTO'

    counts = {'moustache': 0, 'chin': 0}
    for side in (-1,1):
        for i in range(240):
            u = rng.random(); x = side*(.002+.030*u)
            y = 2.0435-.0095*u**.72+rng.uniform(-.0018,.0018)
            root = surface(x,y)
            if root is None: continue
            length = (.006+.011*u**1.3)*rng.uniform(.55,1.25)
            curl = rng.uniform(-.0014,.0014)
            clump = int(u*9)
            points = []
            for j in range(5):
                t = j/4
                xx = x+side*(.002+.005*u)*t
                xx += curl*math.sin(t*math.pi)+side*.0014*math.sin(clump*1.7)*t*t
                yy = y-length*t
                skin = surface(xx, yy)
                zz = max(root.z-.002*t, skin.z if skin is not None else root.z)
                zz += .0027*math.sin(t*math.pi*.85)
                points.append(Vector((xx,yy,zz)))
            points[0] = root
            strand(points); counts['moustache'] += 1
    for i in range(460):
        angle = rng.random()*math.tau; r = math.sqrt(rng.random())
        x = .020*r*math.cos(angle); y = 1.991+.010*r*math.sin(angle)
        root = surface(x,y)
        if root is None: continue
        length = (.043-.009*abs(x)/.02)*rng.uniform(.78,1.13)
        wave = rng.uniform(-.002,.002); curl = rng.uniform(.002,.004)
        clump = int((x+.020)/.005)
        points = []
        for j in range(5):
            t = j/4
            xx = x*(1-.30*t)+wave*math.sin(t*math.pi)+.0016*math.sin(clump*2.1)*t
            yy = y-length*t
            # Once below the chin, extend freely instead of projecting the
            # entire strand back into skin. Clear the collar's front surface.
            zz = root.z+.005*t+curl*math.sin(t*math.pi)
            skin = surface(xx, yy)
            if skin is not None: zz = max(zz, skin.z+.0005*t)
            points.append(Vector((xx,yy,zz)))
        points[0] = root
        strand(points); counts['chin'] += 1
    for obj in groups: b.group_object(obj,'Head')
    assert counts['moustache'] > 400 and counts['chin'] > 400, counts
    print('SOVEREIGN_013_GROOM', counts, flush=True)


def build(directory=None, render=False, legacy_clothes=False):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.world = bpy.data.worlds.new('Portrait world')
    bpy.context.preferences.filepaths.save_version = 0; random.seed(183613)
    profile = dict(base.PROFILES['daoguang'])
    vertices, uv, faces, groups = base.skin_geometry(profile); previous.refine_anatomy(vertices)
    b.rig = b.create_rig(profile); skin = skin_material()
    face = b.create_skin(vertices,uv,faces,groups,skin,neck_crop=1.907)
    refine_skin_tone(face)
    modifier = next(m for m in face.modifiers if m.type=='SUBSURF')
    modifier.levels = 2; modifier.render_levels = 2
    b.bake_face_subdivision(face); previous.age_surface(face)
    previous.create_eyes(vertices,uv,faces,groups)
    bv,bu,bf = b.fit_proxy(b.SYS/'eyebrows/eyebrow001/eyebrow001',vertices)
    bm = b.material('Natural eyebrow fibres',(.04,.035,.027),.87,0,b.SYS/'eyebrows/eyebrow001/eyebrow001.png')
    bp=bm.node_tree.nodes.get('Principled BSDF');bt=next(n for n in bm.node_tree.nodes if n.type=='TEX_IMAGE')
    bm.node_tree.links.new(bt.outputs['Alpha'],bp.inputs['Alpha']);bm.surface_render_method='DITHERED'
    brows=b.meshobj('Anatomical fitted eyebrows',[b.vec(x) for x in bv],[[a[0] for a in q] for q in bf],bm,[[bu[a[1]] for a in q] for q in bf])
    b.group_object(brows,'Head')
    groom_beard(face);b.robe();b.add_sleeves_and_hands(vertices,uv,faces,groups,skin,profile)
    if legacy_clothes:
        import qing_tailoring_12 as tailoring
    else:
        import qing_tailoring_13 as tailoring
    tailoring.refine_scene(b)
    base.cloth_surface();b.consolidate_meshes(face);b.animate(face);previous.animate_gaze()
    out = Path(directory).resolve() if directory else b.OUT
    source = out if directory else b.ROOT
    out.mkdir(parents=True,exist_ok=True)
    for obj in bpy.context.scene.objects:obj.select_set(obj.type in {'ARMATURE','MESH'})
    bpy.context.view_layer.objects.active=b.rig
    bpy.ops.export_scene.gltf(filepath=str(out/'daoguang.glb'),export_format='GLB',use_selection=True,export_apply=False,export_animations=True,export_animation_mode='NLA_TRACKS',export_nla_strips_merged_animation_name='idle',export_morph=True,export_skins=True,export_force_sampling=True,export_frame_range=True)
    b.setup_render(True)
    bpy.ops.wm.save_as_mainfile(filepath=str(source/'daoguang.blend'))
    bpy.ops.file.make_paths_relative();bpy.ops.wm.save_as_mainfile(filepath=str(source/'daoguang.blend'))
    if render:
        scene=bpy.context.scene;scene.cycles.samples=24;scene.render.resolution_x=800;scene.render.resolution_y=1100
        scene.render.filepath=str(source/'daoguang-full-qa.png');bpy.ops.render.render(write_still=True)
        scene.camera.location=b.cv((.09,2.075,.90));scene.camera.rotation_euler=(Vector(b.cv((0,2.085,.04)))-scene.camera.location).to_track_quat('-Z','Y').to_euler();scene.camera.data.lens=68
        scene.render.resolution_x=900;scene.render.resolution_y=900;scene.render.filepath=str(source/'daoguang-face-qa.png');bpy.ops.render.render(write_still=True)
    print('SOVEREIGN_013_CHARACTER_COMPLETE daoguang',flush=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--out-dir');parser.add_argument('--render',action='store_true');parser.add_argument('--legacy-clothes',action='store_true')
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    build(args.out_dir,args.render,args.legacy_clothes)
