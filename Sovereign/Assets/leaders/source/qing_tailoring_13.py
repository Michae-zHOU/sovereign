"""Daoguang 0.13: soft court textiles over the validated 0.12 tailoring.

Call refine_scene(build_leaders) after robe and sleeves/hands, before material
consolidation. Existing panel UVs and skin weights survive unchanged. Original
procedural textile images are generated in memory and packed into the GLB.
Gallery coordinates are Y up, +Z forward. No formal asset is written here.
"""
import math
import random
import qing_tailoring_12 as base


def _shader(material):
    return material.node_tree.nodes.get('Principled BSDF')


def _textile(b, name, color, roughness, ornament=False):
    """Portable image-based thread relief; no render-only procedural shader."""
    mat = b.material(name, color, roughness, .0)
    shader = _shader(mat)
    shader.inputs['Specular IOR Level'].default_value = .22
    width, height = 1024, 128
    image = b.bpy.data.images.new(name + ' original thread color', width=width, height=height, alpha=False)
    normal = b.bpy.data.images.new(name + ' original thread normal', width=width, height=height, alpha=False)
    normal.colorspace_settings.name = 'Non-Color'
    colors, normals = [], []
    for row in range(height):
        v = row / (height - 1)
        for col in range(width):
            u = col / width
            warp = math.sin(math.tau * col / 4)
            weft = math.sin(math.tau * row / 4)
            modulation = .95 + .035 * warp + .027 * weft
            if ornament:
                # Narrow sewn borders and restrained repeating lozenges.
                edge = math.exp(-((v - .13) / .019) ** 2) + math.exp(-((v - .87) / .019) ** 2)
                cell = abs((u * 48) % 1 - .5)
                diamond = math.exp(-((cell / .40 + abs(v - .50) / .23 - 1) / .08) ** 2)
                modulation *= .85 + .30 * min(1, edge + .72 * diamond)
            colors.extend((*[component * modulation for component in color], 1))
            nx, ny = .075 * warp, .075 * weft
            normals.extend((.5 + nx, .5 + ny, .5 + math.sqrt(1 - 4 * nx * nx - 4 * ny * ny) / 2, 1))
    image.pixels.foreach_set(colors)
    normal.pixels.foreach_set(normals)
    image.pack()
    normal.pack()
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    tex = nodes.new('ShaderNodeTexImage'); tex.image = image
    links.new(tex.outputs['Color'], shader.inputs['Base Color'])
    bump = nodes.new('ShaderNodeTexImage'); bump.image = normal
    mapping = nodes.new('ShaderNodeNormalMap'); mapping.inputs['Strength'].default_value = .32
    links.new(bump.outputs['Color'], mapping.inputs['Color'])
    links.new(mapping.outputs['Normal'], shader.inputs['Normal'])
    return mat


def _cape_point(a, t):
    x, y, z = base._cape_point(a, t)
    # Preserve the complete side/upper-sleeve clearance zone, including seams.
    center_mask = base._smooth((.69 - abs(math.sin(a))) / .30)
    front = math.cos(a) > 0
    drape = (.017 if front else .011) * math.sin(math.pi * t) ** 1.2
    hem = (.007 if front else .004) * t ** 4 * (.7 + .3 * math.cos(a * 3))
    fold = .0027 * math.sin(11 * a + .6) * math.sin(math.pi * t) ** 1.4
    return x, y + center_mask * (-drape - hem + fold), z


def _soften_cape(b, edge_material):
    for panel in range(4):
        ob = base._object(b, 'Embroidered shoulder cape panel ' + str(panel))
        for vertex in ob.data.vertices:
            j, i = divmod(vertex.index, 29)
            angle = -math.pi / 4 + panel * math.pi / 2 + math.pi / 2 * i / 28
            vertex.co = b.cv(_cape_point(angle, j / 12))
        ob.data.update()
    for ob in list(b.bpy.context.scene.objects):
        if ob.name.startswith('Qing 0.12 flat cape binding'):
            b.bpy.data.objects.remove(ob, do_unlink=True)
    for panel in range(4):
        angles = [-math.pi / 4 + panel * math.pi / 2 + math.pi / 2 * i / 64 for i in range(65)]
        for t in (.980, .997):
            edges = []
            for offset in (-.003, .003):
                points = []
                for a in angles:
                    x, y, z = _cape_point(a, base._clamp(t + offset))
                    points.append((x, y + .0008, z))
                edges.append(points)
            base._flat_binding(b, 'Qing 0.13 soft cape seam', *edges, edge_material, 'Chest')


def _collar_point(a, t):
    rows = [(1.895, .093, .078, .043), (1.926, .086, .075, .047), (1.953, .084, .072, .051)]
    y = 1.895 + .058 * t
    rx, rz, zc = base._profile(rows, y)
    front = max(0, math.cos(a))
    # A gently lowered throat edge and slight bias avoid a rigid gold cylinder.
    y -= (.006 * front ** 3 + .0012 * math.sin(a + .4)) * t ** 1.5
    crease = .0009 * math.sin(7 * a + .6) * math.sin(math.pi * t)
    return math.sin(a) * (rx + crease), y, zc + math.cos(a) * (rz + crease)


def _soften_collar(b, collar_material, edge_material):
    ob = base._object(b, 'Close fitting dark collar')
    if len(ob.data.vertices) != 3 * 64:
        raise RuntimeError('Unexpected original Qing collar topology')
    ob.data.materials[0] = collar_material
    for vertex in ob.data.vertices:
        row, i = divmod(vertex.index, 64)
        # Middle row is 1.926, not exactly the midpoint of the source profile.
        t = (0, .031 / .058, 1)[row]
        vertex.co = b.cv(_collar_point(math.tau * i / 64, t))
    solid = ob.modifiers.new('Soft folded collar thickness', 'SOLIDIFY')
    solid.thickness = .0012; solid.offset = -.8
    for old in list(b.bpy.context.scene.objects):
        if old.name.startswith('Fine collar binding'):
            b.bpy.data.objects.remove(old, do_unlink=True)
    edges = []
    for t in (.965, 1):
        points = []
        for i in range(97):
            a = math.tau * i / 96
            x, y, z = _collar_point(a, t)
            points.append((x + .0005 * math.sin(a), y, z + .0005 * math.cos(a)))
        edges.append(points)
    base._flat_binding(b, 'Qing 0.13 folded collar lip', *edges, edge_material, 'Neck')
    # A short right-front lapped join uses a cloth strip rather than a rope.
    edges = []
    for a in (.27, .295):
        points = []
        for i in range(13):
            x, y, z = _collar_point(a, .10 + .85 * i / 12)
            points.append((x + .0007 * math.sin(a), y, z + .0007 * math.cos(a)))
        edges.append(points)
    base._flat_binding(b, 'Qing 0.13 collar lapped join', *edges, edge_material, 'Neck')
    ob.data.update()


def _refine_hat(b, band_material):
    brim = base._object(b, 'Low court hat black brim')
    band = base._object(b, 'Wide embroidered gold hat band')
    crown = base._object(b, 'Shallow crimson court crown')
    band.data.materials[0] = band_material
    for ob in (brim, band):
        for vertex in ob.data.vertices:
            x, y, z = base._gallery(vertex.co)
            a = math.atan2(x / .130, (z - .051) / .159)
            ripple = .0005 * math.sin(7 * a + .4) + .0003 * math.sin(13 * a)
            if ob == band:
                y = 2.177 + (y - 2.177) * .66
            vertex.co = b.cv((x * (1 + ripple), y + ripple, z))
        ob.data.update()
    felt = brim.data.materials[0]
    _shader(felt).inputs['Roughness'].default_value = .92
    _shader(felt).inputs['Specular IOR Level'].default_value = .16
    for vertex in crown.data.vertices:
        x, y, z = base._gallery(vertex.co)
        a = math.atan2(x / .123, (z - .051) / .149)
        wave = .0007 * math.sin(9 * a + .7) + .0004 * math.sin(15 * a)
        envelope = base._clamp((2.283 - y) / .045)
        vertex.co = b.cv((x, y + wave * envelope, z))
    crown.data.update()
    shader = _shader(crown.data.materials[0])
    shader.inputs['Base Color'].default_value = (.145, .010, .014, 1)
    shader.inputs['Roughness'].default_value = .87
    shader.inputs['Specular IOR Level'].default_value = .20
    return crown


def _dress_tassels(b, crown):
    from mathutils import Vector
    from mathutils.bvhtree import BVHTree
    for ob in list(b.bpy.context.scene.objects):
        if ob.name.startswith('Fine radial crimson tassel'):
            b.bpy.data.objects.remove(ob, do_unlink=True)
    b.bpy.context.view_layer.update()
    evaluated = crown.evaluated_get(b.bpy.context.evaluated_depsgraph_get())
    mesh = evaluated.to_mesh()
    tree = BVHTree.FromPolygons([v.co.copy() for v in mesh.vertices], [list(p.vertices) for p in mesh.polygons])
    evaluated.to_mesh_clear()
    groups = []
    for label, color in [('shadow', (.11, .008, .011)), ('middle', (.19, .015, .019)), ('lit', (.235, .023, .025))]:
        material = b.material('Qing 0.13 crimson thread ' + label, color, .84)
        _shader(material).inputs['Specular IOR Level'].default_value = .18
        data = b.bpy.data.curves.new('Qing 0.13 laid red fibres ' + label, 'CURVE')
        data.dimensions = '3D'; data.resolution_u = 2; data.bevel_resolution = 0; data.bevel_depth = .00020
        obj = b.bpy.data.objects.new(data.name, data); b.bpy.context.collection.objects.link(obj)
        data.materials.append(material); groups.append(obj)
    rng = random.Random(183613)
    misses, max_lift, made = 0, 0.0, 0
    for i in range(624):
        a = math.tau * (i + rng.uniform(-.30, .30)) / 624
        phase = rng.uniform(0, math.tau)
        end = rng.uniform(.90, .96)
        points = []
        for j in range(7):
            t = j / 6
            # All points are sampled on the evaluated, smoothed cap. This
            # removes the straight floating spokes of the previous red fringe.
            # The cap has a small central opening beneath its finial; begin
            # outside that opening so every strand has a real cloth support.
            radius = .23 + (end - .23) * t
            angle = a + .013 * math.sin(math.pi * t + phase) + .016 * t * t * math.sin(phase)
            x, z = .121 * radius * math.sin(angle), .051 + .147 * radius * math.cos(angle)
            hit, normal, _, _ = tree.ray_cast(Vector(b.cv((x, 2.50, z))), Vector((0, 0, -1)), 1.0)
            if hit is None:
                misses += 1; break
            lift = .00022 + .00009 * math.sin(math.pi * t) ** 2
            hit += normal * lift
            max_lift = max(max_lift, lift)
            points.append(hit)
        if len(points) != 7:
            continue
        obj = groups[0 if i % 7 == 0 else 2 if i % 11 == 0 else 1]
        spline = obj.data.splines.new('BEZIER'); spline.bezier_points.add(6)
        for j, (bp, point) in enumerate(zip(spline.bezier_points, points)):
            bp.co = point; bp.handle_left_type = 'AUTO'; bp.handle_right_type = 'AUTO'
            bp.radius = (.32, .80, 1, .93, .82, .64, .06)[j] * rng.uniform(.85, 1.12)
        made += 1
    if misses or made != 624:
        raise RuntimeError('Court cap projection missed %s of 624 fibres' % misses)
    for obj in groups:
        b.group_object(obj, 'Head')
    return {'fibres': made, 'projection_misses': misses, 'max_surface_lift_m': max_lift}


def refine_scene(b):
    """Apply the 0.12 baseline then this independently versioned textile pass."""
    if b.bpy.context.scene.get('sovereign_qing_tailoring_13', False):
        return {'version': '0.13', 'already_refined': True}
    baseline = base.refine_scene(b)
    names = ['Embroidered shoulder cape panel ' + str(i) for i in range(4)]
    names += ['Close fitting dark collar', 'Low court hat black brim', 'Wide embroidered gold hat band', 'Shallow crimson court crown']
    originals = [base._object(b, name) for name in names]
    fingerprints = {ob.name: base._fingerprint(ob) for ob in originals}
    untouched = [base._object(b, label + suffix) for label in ('Left', 'Right') for suffix in
                 (' hanging tailored sleeve panels', ' embroidered horse hoof cuff', ' relaxed anatomical hand')]
    untouched_positions = {ob.name: [tuple(v.co) for v in ob.data.vertices] for ob in untouched}
    band = _textile(b, 'Qing 0.13 subdued embroidered cap braid', (.42, .29, .115), .84, True)
    collar = _textile(b, 'Qing 0.13 soft ochre collar', (.41, .285, .108), .88)
    edge = b.material('Qing 0.13 fine ochre stitched hems', (.32, .225, .095), .83)
    _shader(edge).inputs['Specular IOR Level'].default_value = .20
    _soften_cape(b, edge)
    _soften_collar(b, collar, edge)
    crown = _refine_hat(b, band)
    fibres = _dress_tassels(b, crown)
    for ob in originals:
        if base._fingerprint(ob) != fingerprints[ob.name]:
            raise RuntimeError('0.13 changed original UVs or bone weights: ' + ob.name)
    for ob in untouched:
        if [tuple(v.co) for v in ob.data.vertices] != untouched_positions[ob.name]:
            raise RuntimeError('0.13 changed sleeve/cuff/hand geometry: ' + ob.name)
    for ob in b.bpy.context.scene.objects:
        if ob.type == 'MESH' and any(not all(math.isfinite(c) for c in v.co) for v in ob.data.vertices):
            raise RuntimeError('Non-finite garment vertex: ' + ob.name)
    b.bpy.context.scene['sovereign_qing_tailoring_13'] = True
    return {'version': '0.13', 'baseline': baseline, 'original_uvs_preserved': True,
            'original_weights_preserved': True, 'sleeves_cuffs_hands_unchanged': True,
            'side_cape_shape_unchanged': True, 'tassels': fibres}
