"""Daoguang-only garment refinement for the original Sovereign nine-bone rig.

Call ``refine_scene(build_leaders)`` immediately after robe() and
add_sleeves_and_hands(), before modifier baking/material consolidation. Existing
robe, cape, sleeve and cuff meshes retain their UVs and vertex-group weights.
The anatomical hands, head, hat, rig and animation are deliberately not changed.
All dimensions use the source author's gallery coordinates: Y up, +Z forward.
"""

import hashlib
import math
import struct


TAILORED_ROWS = [
    (.12, .309, .160, .024), (.18, .309, .160, .024),
    (.35, .307, .159, .024), (.60, .293, .155, .024),
    (.82, .275, .146, .025), (1.03, .252, .137, .026),
    (1.17, .226, .122, .030), (1.25, .216, .119, .033),
    (1.32, .226, .128, .033), (1.49, .245, .142, .032),
    (1.66, .261, .141, .027), (1.77, .275, .128, .020),
    (1.82, .249, .112, .025), (1.902, .092, .076, .043),
]


def _clamp(value, minimum=0.0, maximum=1.0):
    return max(minimum, min(maximum, value))


def _smooth(value):
    value = _clamp(value)
    return value * value * (3.0 - 2.0 * value)


def _profile(rows, y):
    for low, high in zip(rows, rows[1:]):
        if low[0] <= y <= high[0]:
            t = (y - low[0]) / (high[0] - low[0])
            return tuple(low[k] + (high[k] - low[k]) * t for k in (1, 2, 3))
    return (rows[0] if y < rows[0][0] else rows[-1])[1:]


def _interpolate(points, t):
    for low, high in zip(points, points[1:]):
        if low[0] <= t <= high[0]:
            u = _smooth((t - low[0]) / (high[0] - low[0]))
            return low[1] + (high[1] - low[1]) * u
    return points[0][1] if t < points[0][0] else points[-1][1]


def _gallery(co):
    return co.x, co.z, -co.y


def _fingerprint(ob):
    """UV and bind weights must survive positional tailoring exactly."""
    uv_hash, weight_hash = hashlib.sha256(), hashlib.sha256()
    for layer in ob.data.uv_layers:
        uv_hash.update(layer.name.encode('utf-8'))
        for loop in layer.data:
            uv_hash.update(struct.pack('<ff', *loop.uv))
    for vertex in ob.data.vertices:
        for group in vertex.groups:
            weight_hash.update(struct.pack('<iif', vertex.index, group.group, group.weight))
    return uv_hash.hexdigest(), weight_hash.hexdigest()


def _object(b, name):
    ob = b.bpy.data.objects.get(name)
    if ob is None or ob.type != 'MESH':
        raise RuntimeError('Qing 0.12 requires unconsolidated garment: ' + name)
    return ob


def _fold(u, y, sign=1):
    # Long folds hang from the belt; their amplitude grows toward the hem.
    # Alternating broad crests and narrow valleys are intentionally asymmetric.
    hang = _smooth((1.22 - y) / .98)
    value = 0.0
    for center, width, amplitude in ((-.78, .075, -.011), (-.64, .10, .010),
                                     (-.39, .065, -.008), (-.25, .11, .006),
                                     (.09, .08, -.006), (.34, .12, .007),
                                     (.58, .07, -.012), (.75, .10, .010)):
        center += .018 * math.sin(y * 3.2 + center * 5) * (1 - hang)
        value += amplitude * math.exp(-((u - center) / width) ** 2)
    value *= hang * max(0.0, 1 - u * u) ** .35
    # Small oblique compression immediately above the belt, not a round belly.
    tuck = .0028 * math.sin(u * 11 + (y - 1.25) * 20 + sign)
    tuck *= math.exp(-((y - 1.29) / .065) ** 2) * (1 - u * u)
    return value + tuck


def _robe_surface(x, y, sign=1):
    rx, rz, center = _profile(TAILORED_ROWS, y)
    u = _clamp(x / rx, -1, 1)
    return center + sign * (rz * max(0, 1 - u * u) ** .52 + _fold(u, y, sign))


def _refine_robe(b):
    rows = []
    for low, high in zip(b.QING_ROWS, b.QING_ROWS[1:]):
        rows.extend(tuple(low[k] + (high[k] - low[k]) * j / 3 for k in range(4)) for j in range(3))
    rows.append(b.QING_ROWS[-1])
    for sign, label in ((1, 'Front'), (-1, 'Back')):
        ob = _object(b, label + ' cut imperial robe panel')
        if len(ob.data.vertices) != len(rows) * 49:
            raise RuntimeError('Unexpected original robe topology: ' + ob.name)
        for vertex in ob.data.vertices:
            j, i = divmod(vertex.index, 49)
            y = rows[j][0]
            u = i / 48 * 2 - 1
            rx, _, _ = _profile(TAILORED_ROWS, y)
            x = rx * u
            hem = .0025 * math.cos(u * math.pi * 7 + .4 * sign) * _clamp((.22 - y) / .10)
            vertex.co = b.cv((x, y + hem, _robe_surface(x, y, sign)))
        ob.data.update()


def _cape_point(a, t):
    side = abs(math.sin(a))
    front = max(0.0, math.cos(a))
    # A narrow shoulder support followed by an outer hanging edge replaces the
    # old rapidly expanding hemisphere. It clears the fitted sleeve beneath it.
    rx = .105 + (.360 - .105) * t
    rz = .083 + ((.143 if front > 0 else .160) - .083) * t
    # Follow the neck-to-acromion slope directly. A shorter outer drop clears
    # the upper sleeve at this narrower width, without relocating arm bones.
    shoulder = _interpolate(((0, 1.910), (.46, 1.855), (.70, 1.804),
                             (.86, 1.775), (1, 1.742)), t)
    bib = 1.910 - .188 * t ** 1.12
    y = bib * (1 - side ** 1.35) + shoulder * side ** 1.35
    relief = (.0038 * math.sin(9 * a + .7 * t) + .0016 * math.sin(17 * a - t))
    relief *= math.sin(math.pi * t) ** .8
    y += relief - .006 * max(0, math.sin(a)) * t * t
    y += .002 * math.sin(7 * a + .6) * t ** 5
    return math.sin(a) * (rx + relief * .13), y, .043 + math.cos(a) * (rz + relief * .25)


def _flat_binding(b, name, points_a, points_b, material, bone):
    vertices, faces, uvs = [], [], []
    for p, q in zip(points_a, points_b):
        vertices.extend((b.cv(p), b.cv(q)))
    for j in range(len(points_a) - 1):
        k = j * 2
        faces.append([k, k + 2, k + 3, k + 1])
        uvs.append([(j / 24, 0), ((j + 1) / 24, 0), ((j + 1) / 24, 1), (j / 24, 1)])
    ob = b.rigged_mesh(name, vertices, faces, material, uvs, [{bone: 1.0} for _ in vertices])
    solid = ob.modifiers.new('Fine folded cloth edge', 'SOLIDIFY')
    solid.thickness = .0007
    return ob


def _refine_cape(b, gold):
    for panel in range(4):
        ob = _object(b, 'Embroidered shoulder cape panel ' + str(panel))
        if len(ob.data.vertices) != 13 * 29:
            raise RuntimeError('Unexpected original cape topology: ' + ob.name)
        low = -math.pi / 4 + panel * math.pi / 2
        for vertex in ob.data.vertices:
            j, i = divmod(vertex.index, 29)
            a = low + math.pi / 2 * i / 28
            vertex.co = b.cv(_cape_point(a, j / 12))
        for mod in ob.modifiers:
            if mod.type == 'SOLIDIFY':
                mod.thickness = .0022
                mod.offset = -.65
        ob.data.update()
    for ob in list(b.bpy.context.scene.objects):
        if ob.name.startswith('Cape gold cord'):
            b.bpy.data.objects.remove(ob, do_unlink=True)
    for panel in range(4):
        angles = [-math.pi / 4 + panel * math.pi / 2 + math.pi / 2 * i / 48 for i in range(49)]
        for t in (.977, .997):
            edges = []
            for offset in (-.004, .004):
                points = []
                for a in angles:
                    x, y, z = _cape_point(a, _clamp(t + offset))
                    points.append((x, y + .0008, z))
                edges.append(points)
            _flat_binding(b, 'Qing 0.12 flat cape binding', edges[0], edges[1], gold, 'Chest')


def _refine_sleeves(b):
    for side, label in ((1, 'Left'), (-1, 'Right')):
        ob = _object(b, label + ' hanging tailored sleeve panels')
        if len(ob.data.vertices) != 37 * 32:
            raise RuntimeError('Unexpected original sleeve topology: ' + ob.name)
        for vertex in ob.data.vertices:
            j, i = divmod(vertex.index, 32)
            t, a = j / 36, math.tau * i / 32
            if t < .55:
                center_x = .284 + .079 * (t / .55)
            else:
                center_x = .363 + .014 * ((t - .55) / .45)
            y, center_z = 1.79 - .70 * t, .008 + .058 * t * t
            rx = .057 + .012 * math.sin(math.pi * t) - .006 * t
            rz = .052 + .012 * math.sin(math.pi * t) - .005 * t
            sx = math.copysign(abs(math.sin(a)) ** .90, math.sin(a))
            cz = math.copysign(abs(math.cos(a)) ** .94, math.cos(a))
            # The original horizontal sleeve opening projected above the new
            # sloping cape like a square epaulette. Seat the complete opening
            # beneath it, including the rear quarter where the cape is lower,
            # then return smoothly to the unchanged upper-arm silhouette.
            shoulder_seat = math.exp(-((t / .11) ** 2))
            y -= .060 * shoulder_seat
            elbow = math.exp(-((t - .54) / .14) ** 2)
            inner = max(0, -side * math.sin(a))
            crease = (.0012 * math.sin(t * math.pi * 12 + a * .8)
                      + .0027 * elbow * inner * math.sin(t * math.pi * 24 - a))
            crease *= math.sin(math.pi * t)
            vertex.co = b.cv((side * center_x + sx * (rx + crease),
                              y + .0015 * elbow * math.cos(a * 2),
                              center_z + cz * (rz + crease)))
        ob.data.update()


def _cuff_point(side, a, t, edge_offset=0.0):
    front = max(0.0, math.cos(a))
    lower = 1.062 - .105 * front ** .78
    y = 1.151 * (1 - t) + lower * t + edge_offset
    rx = .056 - .004 * t
    rz = .050 - .003 * t
    return side * .377 + math.sin(a) * rx, y, .069 + math.cos(a) * rz + .005 * front * t


def _refine_cuffs(b, gold):
    for side, label in ((1, 'Left'), (-1, 'Right')):
        ob = _object(b, label + ' embroidered horse hoof cuff')
        if len(ob.data.vertices) != 6 * 64:
            raise RuntimeError('Unexpected original cuff topology: ' + ob.name)
        for vertex in ob.data.vertices:
            j, i = divmod(vertex.index, 64)
            vertex.co = b.cv(_cuff_point(side, math.tau * i / 64, j / 5))
        for mod in ob.modifiers:
            if mod.type == 'SOLIDIFY':
                mod.thickness = .0018
                mod.offset = -.6
        ob.data.update()
        for trim in list(b.bpy.context.scene.objects):
            if trim.name.startswith(label + ' horse hoof gold binding'):
                b.bpy.data.objects.remove(trim, do_unlink=True)
        for offset in (.001, .007):
            edges = []
            for delta in (0.0, .0021):
                points = []
                for i in range(97):
                    a = math.tau * i / 96
                    x, y, z = _cuff_point(side, a, 1, offset + delta)
                    points.append((x + math.sin(a) * .0008, y, z + math.cos(a) * .0008))
                edges.append(points)
            _flat_binding(b, label + ' fine horse hoof hem', edges[0], edges[1], gold, label + 'Forearm')


def _refit_accessories(b):
    rigid_prefixes = ('Small robe fastening', 'Court necklace amber', 'Court necklace divider',
                      'Court embroidered purse', 'Carved belt plaque', 'Belt plaque green stone')
    draped_prefixes = ('Right-lapped robe binding', 'Waist hanging silk cord',
                       'Purse gold trim', 'Fine hanging tassel')
    belt_prefixes = ('Narrow woven court belt', 'Belt embroidered edge')
    def surface_shift(p):
        x, y, z = p
        old_rx, old_rz, old_center = _profile(b.QING_ROWS, y)
        new_rx, _, _ = _profile(TAILORED_ROWS, y)
        new_x = x * new_rx / old_rx
        old_front = old_center + old_rz * max(0, 1 - (x / old_rx) ** 2) ** .44
        return new_x, y, z + _robe_surface(new_x, y) - old_front
    for ob in list(b.bpy.context.scene.objects):
        if ob.type != 'MESH':
            continue
        if ob.name.startswith(rigid_prefixes):
            points = [_gallery(vertex.co) for vertex in ob.data.vertices]
            center = tuple(sum(p[k] for p in points) / len(points) for k in range(3))
            target = surface_shift(center)
            delta = tuple(target[k] - center[k] for k in range(3))
            for vertex, p in zip(ob.data.vertices, points):
                vertex.co = b.cv(tuple(p[k] + delta[k] for k in range(3)))
        elif ob.name.startswith(draped_prefixes):
            for vertex in ob.data.vertices:
                vertex.co = b.cv(surface_shift(_gallery(vertex.co)))
        elif ob.name.startswith(belt_prefixes):
            for vertex in ob.data.vertices:
                x, y, z = _gallery(vertex.co)
                vertex.co = b.cv((x * .955, y, .033 + (z - .033) * .935))
        else:
            continue
        ob.data.update()


def _boot_loft(b, name, center_x, levels, material):
    n = 48
    vertices, faces, uvs = [], [], []
    for y, rx, rz, zc in levels:
        for i in range(n):
            a = math.tau * i / n
            # A slightly square toe box and flatter instep avoid ellipsoid feet.
            sx = math.copysign(abs(math.sin(a)) ** .88, math.sin(a))
            cz = math.copysign(abs(math.cos(a)) ** .83, math.cos(a))
            vertices.append(b.cv((center_x + rx * sx, y, zc + rz * cz)))
    for j in range(len(levels) - 1):
        for i in range(n):
            k, q = j * n + i, j * n + (i + 1) % n
            faces.append([k, q, q + n, k + n])
            uvs.append([(i / n, j / 8), ((i + 1) / n, j / 8),
                        ((i + 1) / n, (j + 1) / 8), (i / n, (j + 1) / 8)])
    faces.append(list(reversed(range(n))))
    uvs.append([(0, 0)] * n)
    last = (len(levels) - 1) * n
    faces.append(list(range(last, last + n)))
    uvs.append([(0, 0)] * n)
    ob = b.rigged_mesh(name, vertices, faces, material, uvs, [{'Root': 1.0} for _ in vertices])
    b.subd(ob, 1)
    return ob


def _replace_boots(b):
    leather = b.bpy.data.materials.get('Black court boots')
    if leather is None:
        raise RuntimeError('Original court boot material is missing')
    for ob in list(b.bpy.context.scene.objects):
        if ob.name.startswith('Soft black court boot'):
            b.bpy.data.objects.remove(ob, do_unlink=True)
    leather.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value = .76
    sole = b.material('Qing 0.12 layered dark boot soles', (.022, .019, .015), .86)
    seam = b.material('Qing 0.12 boot edge binding', (.028, .029, .026), .90)
    for side, label in ((1, 'Left'), (-1, 'Right')):
        x = side * .108
        _boot_loft(b, label + ' Qing shaped boot upper', x,
                   [(.018, .065, .141, .076), (.025, .069, .145, .076),
                    (.044, .070, .145, .075), (.064, .068, .135, .065),
                    (.086, .061, .112, .040), (.125, .052, .070, .002),
                    (.155, .050, .059, -.007), (.230, .049, .056, -.009),
                    (.238, .049, .056, -.009)], leather)
        _boot_loft(b, label + ' Qing flat layered sole', x,
                   [(.004, .066, .142, .076), (.006, .071, .147, .076),
                    (.013, .072, .148, .076), (.023, .072, .148, .076),
                    (.025, .069, .145, .076)], sole)
        _boot_loft(b, label + ' Qing sewn shoe welt', x,
                   [(.023, .071, .146, .076), (.025, .072, .147, .076),
                    (.029, .071, .146, .076), (.031, .069, .143, .076)], seam)


def refine_scene(b):
    """Refine only unmerged Qing clothing; returns a small integration report.

    Idempotent within a scene. Fails before mutation when expected garment names
    are missing, preventing accidental edits of consolidated material meshes.
    """
    scene = b.bpy.context.scene
    if scene.get('sovereign_qing_tailoring_12', False):
        return {'version': '0.12', 'already_refined': True}
    names = ['Front cut imperial robe panel', 'Back cut imperial robe panel']
    names += ['Embroidered shoulder cape panel ' + str(i) for i in range(4)]
    names += [label + suffix for label in ('Left', 'Right') for suffix in
              (' hanging tailored sleeve panels', ' embroidered horse hoof cuff')]
    originals = [_object(b, name) for name in names]
    before = {ob.name: _fingerprint(ob) for ob in originals}
    gold = b.bpy.data.materials.get('Antique gold silk embroidery')
    if gold is None:
        raise RuntimeError('Original Qing gold binding material is missing')
    _refine_robe(b)
    _refine_cape(b, gold)
    _refine_sleeves(b)
    _refine_cuffs(b, gold)
    _refit_accessories(b)
    _replace_boots(b)
    for ob in originals:
        if _fingerprint(ob) != before[ob.name]:
            raise RuntimeError('Unexpected UV or binding change: ' + ob.name)
    scene['sovereign_qing_tailoring_12'] = True
    return {'version': '0.12', 'robe_panels': 2, 'cape_panels': 4,
            'sleeves': 2, 'cuffs': 2, 'shaped_boots': 2,
            'original_uvs_preserved': True, 'original_weights_preserved': True}
