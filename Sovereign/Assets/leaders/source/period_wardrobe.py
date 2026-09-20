"""Original full-length period clothing for Sovereign's shared nine-bone rig.

Call build(build_leaders, profile, face) after creating the rig. Coordinates below
are gallery metres: Y up, +Z toward the face. This module creates clothing and
footwear only. The caller owns anatomical hands, head, hair, and child scaling.
No textures, Blender operators, random state, or external assets are required.
"""

import math


def _clamp(x, lo=0.0, hi=1.0):
    return max(lo, min(hi, x))


def _color(value, fallback):
    if isinstance(value, str):
        value = value.lstrip('#')
        if len(value) in (6, 8):
            try:
                return tuple((int(value[i:i + 2], 16) / 255.0) ** 2.2 for i in (0, 2, 4))
            except ValueError:
                pass
    if isinstance(value, (tuple, list)) and len(value) >= 3:
        return tuple(float(c) for c in value[:3])
    return fallback


def _body_weight(y):
    chest = _clamp((y - 1.24) / .26)
    return {'Pelvis': 1.0 - chest, 'Chest': chest}


def _arm_weight(label, t):
    # Keep the shoulder seam with the chest, then transfer to the upper arm.
    chest = 1.0 - _clamp(t / .16)
    fore = _clamp((t - .43) / .23)
    fore = fore * fore * (3.0 - 2.0 * fore)
    return {'Chest': chest, label + 'UpperArm': (1.0 - chest) * (1.0 - fore),
            label + 'Forearm': (1.0 - chest) * fore}


def _finish(ob, subdivisions=1, thickness=.0025):
    if subdivisions:
        mod = ob.modifiers.new('Tailored cloth smoothing', 'SUBSURF')
        mod.levels = subdivisions
        mod.render_levels = subdivisions
    if thickness:
        mod = ob.modifiers.new('Woven cloth edge thickness', 'SOLIDIFY')
        mod.thickness = thickness
        mod.offset = -.7
    return ob


def _mesh(b, name, vertices, faces, material, weights=None, uvs=None,
          subdivisions=0, thickness=.002):
    if weights is None:
        weights = [_body_weight(p[1]) for p in vertices]
    ob = b.rigged_mesh(name, [b.cv(p) for p in vertices], faces, material, uvs, weights)
    return _finish(ob, subdivisions, thickness)


def _rows(levels, steps=3):
    result = []
    for low, high in zip(levels, levels[1:]):
        for j in range(steps):
            t = j / steps
            result.append(tuple(a + (z - a) * t for a, z in zip(low, high)))
    result.append(levels[-1])
    return result


def _profile(levels, y):
    for low, high in zip(levels, levels[1:]):
        if low[0] <= y <= high[0]:
            t = (y - low[0]) / (high[0] - low[0])
            return tuple(low[k] + (high[k] - low[k]) * t for k in (1, 2, 3))
    return (levels[0] if y < levels[0][0] else levels[-1])[1:]


def _front(levels, x, y, lift=.005):
    rx, rz, zc = _profile(levels, y)
    return zc + rz * math.sqrt(max(0.0, 1.0 - (x / max(.001, rx)) ** 2)) + lift


def _loft(b, name, levels, mat, segments=48, center_x=0.0,
          folds=0.0, shape=1.0, transform=None, cut=None, bone=None,
          thickness=.0025, subdivisions=1):
    """Ascending horizontal cloth sections; no ellipsoid shoulder primitives."""
    rings = _rows(levels)
    vertices, weights, faces, uvs = [], [], [], []
    ymin, ymax = levels[0][0], levels[-1][0]
    for y, rx, rz, zc in rings:
        for i in range(segments):
            a = math.tau * i / segments
            sx, cz = math.sin(a), math.cos(a)
            sx = math.copysign(abs(sx) ** shape, sx)
            cz = math.copysign(abs(cz) ** shape, cz)
            hang = _clamp((1.27 - y) / .95)
            ripple = folds * hang * (math.cos(a * 12 + .3) + .32 * math.sin(a * 23))
            p = (center_x + sx * (rx + ripple), y, zc + cz * (rz + ripple * .6))
            if transform:
                p = transform(p, a, y)
            vertices.append(p)
            weights.append({bone: 1.0} if bone else _body_weight(y))
    for j in range(len(rings) - 1):
        ym = (rings[j][0] + rings[j + 1][0]) * .5
        for i in range(segments):
            am = math.tau * (i + .5) / segments
            if cut and cut(am, ym):
                continue
            k = j * segments + i
            q = j * segments + (i + 1) % segments
            faces.append([k, q, q + segments, k + segments])
            v0 = (rings[j][0] - ymin) / max(.01, ymax - ymin)
            v1 = (rings[j + 1][0] - ymin) / max(.01, ymax - ymin)
            uvs.append([(i / segments, v0), ((i + 1) / segments, v0),
                        ((i + 1) / segments, v1), (i / segments, v1)])
    return _mesh(b, name, vertices, faces, mat, weights, uvs, subdivisions, thickness)


def _panel(b, name, points, mat, bone=None, thickness=.002):
    # Points run counterclockwise as seen from the front (+Z).
    points = list(points)
    area = sum(p[0] * q[1] - q[0] * p[1] for p, q in zip(points, points[1:] + points[:1]))
    if area < -1e-8:
        points.reverse()
    weights = [{bone: 1.0} for _ in points] if bone else None
    return _mesh(b, name, points, [list(range(len(points)))], mat,
                 weights, [[(p[0], p[1]) for p in points]], thickness=thickness)


def _strip(b, name, points, width, mat, bone=None):
    """A flat sewn strip, not a round piping tube."""
    vs, fs, ws, uv = [], [], [], []
    for i, p in enumerate(points):
        before, after = points[max(0, i - 1)], points[min(len(points) - 1, i + 1)]
        dx, dy = after[0] - before[0], after[1] - before[1]
        length = max(.00001, math.hypot(dx, dy))
        ox, oy = -dy / length * width * .5, dx / length * width * .5
        for side in (-1, 1):
            v = (p[0] + side * ox, p[1] + side * oy, p[2])
            vs.append(v)
            ws.append({bone: 1.0} if bone else _body_weight(v[1]))
    for i in range(len(points) - 1):
        k = i * 2
        fs.append([k, k + 2, k + 3, k + 1])
        uv.append([(0, i), (0, i + 1), (1, i + 1), (1, i)])
    return _mesh(b, name, vs, fs, mat, ws, uv, thickness=.0012)


def _button(b, name, x, y, z, mat, radius=.0055, bone='Chest'):
    vertices = [(x, y, z + .002)]
    vertices += [(x + radius * math.cos(i * math.tau / 12),
                  y + radius * math.sin(i * math.tau / 12), z) for i in range(12)]
    faces = [[0, i + 1, (i + 1) % 12 + 1] for i in range(12)]
    return _mesh(b, name, vertices, faces, mat,
                 [{bone: 1.0} for _ in vertices], thickness=.0015)


def _sleeves(b, mat, cuff_mat, female=False, kimono=False):
    # The sleeve head starts inside the torso, then rounds over the shoulder.
    # A full-width first ring would leave a flat exposed barrel at the armhole.
    # Below the sleeve head, sections follow the shared elbow and wrist.
    for side, label in ((1, 'Left'), (-1, 'Right')):
        vs, fs, ws, uv = [], [], [], []
        rows, n = 40, 32
        cap_profile = [(0.0, .236, 1.808, .005, .008),
                       (.025, .261, 1.801, .005, .030),
                       (.055, .285, 1.779, .006, .055),
                       (.105, .303, 1.731, .008, .065)]
        for j in range(rows + 1):
            t = j / rows
            if t <= .105:
                for low, high in zip(cap_profile, cap_profile[1:]):
                    if low[0] <= t <= high[0]:
                        u = (t - low[0]) / (high[0] - low[0])
                        x, y, z, radius = [low[k] + (high[k] - low[k]) * u for k in range(1, 5)]
                        break
                if female:
                    radius *= .96
            elif t < .535:
                u = (t - .105) / .43
                x, y, z = .303 + .060 * u, 1.731 - .326 * u, .008 + .017 * u
            else:
                u = (t - .535) / .465
                x, y, z = .363 + .015 * u, 1.405 - .333 * u, .025 + .045 * u
            if t > .105:
                if kimono:
                    radius = .065 + .042 * math.sin(math.pi * t) - .012 * t
                else:
                    radius = (.059 if female else .062) + .015 * math.sin(math.pi * t) - .017 * t
            for i in range(n):
                a = math.tau * i / n
                crease = .0014 * math.sin(t * math.pi * 25 + a * 2) * math.sin(math.pi * t)
                crease += .003 * math.sin(t * math.pi * 39) * math.exp(-((t - .535) / .11) ** 2)
                cross_x, cross_z = math.sin(a), math.cos(a)
                if kimono:
                    cross_x = math.copysign(abs(cross_x) ** .65, cross_x)
                    cross_z = math.copysign(abs(cross_z) ** .72, cross_z)
                drape = .11 * math.sin(math.pi * t) * max(0.0, -cross_z) if kimono else 0.0
                vs.append((side * x + cross_x * (radius + crease), y - drape,
                           z + cross_z * (radius * .87 + crease)))
                ws.append(_arm_weight(label, t))
        for j in range(rows):
            for i in range(n):
                k, q = j * n + i, j * n + (i + 1) % n
                # Rows descend: reverse the ascending-loft winding.
                fs.append([k, k + n, q + n, q])
                uv.append([(i / n, 1 - j / rows), (i / n, 1 - (j + 1) / rows),
                           ((i + 1) / n, 1 - (j + 1) / rows), ((i + 1) / n, 1 - j / rows)])
        # Close the tiny first section inside the torso. It cannot expose a rim.
        cap = len(vs)
        vs.append((side * .236, 1.810, .005))
        ws.append({'Chest': 1.0})
        for i in range(n):
            fs.append([cap, i, (i + 1) % n])
            uv.append([(.5, .5), (.5 + .5 * math.sin(math.tau * i / n), .5 + .5 * math.cos(math.tau * i / n)),
                       (.5 + .5 * math.sin(math.tau * (i + 1) / n), .5 + .5 * math.cos(math.tau * (i + 1) / n))])
        _mesh(b, label + ' fitted hanging sleeve', vs, fs, mat, ws, uv, 1, .0025)
        _loft(b, label + ' flat woven cuff',
              [(1.061, .047, .042, .072), (1.066, .048, .043, .072),
               (1.120, .050, .044, .064), (1.125, .049, .043, .064)],
              cuff_mat, segments=32, center_x=side * .378, bone=label + 'Forearm',
              subdivisions=1, thickness=.002)


def _footwear(b, mat, sole, high=False, tabi=False):
    for side, label in ((1, 'Left'), (-1, 'Right')):
        x = side * .108
        # Low, shaped leather toe and separate flat sole; the foot reaches the floor.
        foot = [(0.012, .068, .134, .060), (.021, .073, .145, .065),
                (.055, .074, .144, .066), (.092, .064, .119, .043),
                (.150, .052, .065, -.004), (.195, .052, .059, -.011)]
        _loft(b, label + (' tabi-covered foot' if tabi else ' shaped leather shoe'),
              foot, mat, segments=32, center_x=x, bone='Pelvis', thickness=.002)
        _loft(b, label + ' thin shoe sole',
              [(.002, .073, .143, .064), (.008, .075, .147, .064),
               (.020, .075, .147, .064), (.024, .073, .143, .064)],
              sole, segments=32, center_x=x, bone='Pelvis', thickness=.002)
        if high:
            _loft(b, label + ' fitted riding boot shaft',
                  [(.15, .052, .063, -.008), (.20, .054, .062, -.008),
                   (.39, .065, .069, -.003), (.54, .074, .074, .0),
                   (.552, .075, .075, .0)], mat, segments=32, center_x=x,
                  bone='Pelvis', thickness=.003)
        if tabi:
            _strip(b, label + ' divided-toe seam',
                   [(x + side * .018, .058, .205), (x + side * .015, .079, .166),
                    (x + side * .008, .095, .132)], .0018, sole, 'Pelvis')


def _trousers(b, mat, high_boot=False):
    bottom = .48 if high_boot else .17
    for side, label in ((1, 'Left'), (-1, 'Right')):
        levels = [(bottom, .072, .074, -.004), (bottom + .018, .074, .075, -.004),
                  (.58, .077, .077, -.006), (.76, .084, .084, -.007),
                  (.97, .088, .087, -.006), (1.10, .084, .078, -.004),
                  (1.27, .082, .077, -.004)]
        def folds(p, a, y):
            ripple = .0025 * math.sin((y - .5) * 65 + a) * math.exp(-((y - .60) / .12) ** 2)
            return (p[0] + math.sin(a) * ripple, p[1], p[2] + math.cos(a) * ripple)
        _loft(b, label + ' pressed wool trouser leg', levels, mat, center_x=side * .10,
              transform=folds, bone='Pelvis', segments=32, thickness=.002)


def _collar(b, white, facing, formal=False):
    # The collar is a short fabric wall around the neck, not a torus or rope.
    _loft(b, 'Shaped standing collar',
          [(1.855, .098, .081, .037), (1.861, .100, .083, .037),
           (1.924, .094, .078, .039), (1.930, .092, .076, .039)],
          white if formal else facing, segments=48, bone='Chest', thickness=.002)
    if formal:
        for side in (-1, 1):
            pts = [(side * .004, 1.850, .134), (side * .059, 1.843, .118),
                   (side * .087, 1.905, .093), (side * .025, 1.918, .124)]
            if side < 0:
                pts.reverse()
            _panel(b, 'Folded white collar point', pts, white, 'Chest')


def _bow(b, mat):
    for side in (-1, 1):
        pts = [(side * .010, 1.844, .159), (side * .055, 1.829, .150),
               (side * .065, 1.867, .155), (side * .010, 1.863, .163)]
        if side < 0:
            pts.reverse()
        _panel(b, 'Folded silk neckcloth wing', pts, mat, 'Chest', .003)
    _panel(b, 'Silk neckcloth knot', [(-.012, 1.840, .166), (.012, 1.840, .166),
                                    (.014, 1.866, .165), (-.014, 1.866, .165)], mat, 'Chest', .003)


def _coat(b, profile, cloth, facing, white, metal, trousers, leather, sole):
    kind = profile['wardrobe']
    statesman = kind == 'statesman'
    high_boot = kind in ('prussian', 'russian', 'austrian')
    _trousers(b, trousers, high_boot)
    _footwear(b, leather, sole, high_boot)
    hem = .64 if statesman else .81 if kind in ('royal', 'ottoman') else .91
    levels = [(hem, .242, .142, -.012), (hem + .012, .243, .143, -.012),
              (1.06, .232, .141, -.008), (1.23, .198, .115, .004),
              (1.34, .209, .126, .009), (1.53, .254, .148, .012),
              (1.68, .273, .141, .008), (1.774, .286, .123, .009),
              (1.811, .253, .110, .023), (1.879, .091, .076, .038)]
    def cut(a, y):
        if not statesman or y >= 1.28:
            return False
        opening = .22 + .42 * _clamp((1.28 - y) / .50)
        return math.cos(a) > 0 and abs(math.sin(a)) < opening
    def tailored(p, a, y):
        dart = -.004 * math.exp(-((y - 1.39) / .20) ** 2) * math.cos(2 * a) ** 6
        return p[0], p[1], p[2] + math.cos(a) * dart
    _loft(b, 'Long tailored ' + kind + ' coat', levels, cloth, folds=.0028,
          cut=cut, transform=tailored, thickness=.003)
    _sleeves(b, cloth, facing)
    _collar(b, white, facing, formal=statesman or kind in ('naval', 'royal'))
    if statesman or kind in ('naval', 'royal'):
        vest = b.material('Ivory waistcoat' if statesman else 'Warm linen shirt front',
                          (.53, .49, .40) if statesman else (.64, .60, .51), .85)
        # Sample the curved chest rather than spanning it with one flat n-gon.
        # The latter passed behind the convex coat and exposed isolated white tabs.
        front_vertices, front_faces = [], []
        vest_rows, vest_cols = 30, 8
        for j in range(vest_rows + 1):
            y = 1.26 + (1.843 - 1.26) * j / vest_rows
            width = .077 + .016 * _clamp((y - 1.26) / .45)
            if y > 1.71:
                width *= max(.012, (1.843 - y) / .133)
            for i in range(vest_cols + 1):
                x = width * (2 * i / vest_cols - 1)
                front_vertices.append((x, y, _front(levels, x, y, .011)))
        for j in range(vest_rows):
            for i in range(vest_cols):
                k = j * (vest_cols + 1) + i
                front_faces.append([k, k + 1, k + vest_cols + 2, k + vest_cols + 1])
        _mesh(b, 'Fitted curved waistcoat front', front_vertices, front_faces, vest,
              subdivisions=0, thickness=.002)
        for side in (-1, 1):
            # Both edges and the interior follow the convex torso. A single
            # non-planar six-sided face intersected the white waistcoat in QA.
            inner = [(1.35, .075), (1.52, .033), (1.79, .086)]
            outer = [(1.35, .075), (1.54, .115), (1.66, .167),
                     (1.76, .133), (1.79, .086)]
            def edge_x(edge, y):
                for low, high in zip(edge, edge[1:]):
                    if low[0] <= y <= high[0]:
                        return low[1] + (high[1] - low[1]) * (y - low[0]) / (high[0] - low[0])
                return edge[-1][1]
            ys = [r[0] for r in _rows([(y,) for y in sorted({p[0] for p in inner + outer})], 5)]
            lv, lf, lu = [], [], []
            cols = 6
            for y in ys:
                xi, xo = edge_x(inner, y), edge_x(outer, y)
                if abs(xo - xi) < .0001:
                    xo = xi + .0001
                for i in range(cols + 1):
                    x = side * (xi + (xo - xi) * i / cols)
                    lv.append((x, y, _front(levels, x, y, .021)))
            for j in range(len(ys) - 1):
                for i in range(cols):
                    k = j * (cols + 1) + i
                    face_indices = [k, k + 1, k + cols + 2, k + cols + 1]
                    if side < 0:
                        face_indices.reverse()
                    lf.append(face_indices)
                    lu.append([(lv[k][0], lv[k][1]) for k in face_indices])
            _mesh(b, 'Curved folded coat lapel', lv, lf, cloth, uvs=lu,
                  subdivisions=0, thickness=.003)
        _bow(b, leather)
        for y in (1.34, 1.43, 1.52, 1.61):
            _button(b, 'Waistcoat small button', 0, y, _front(levels, 0, y, .018), metal, .0038)
    if not statesman:
        double = kind in ('naval', 'royal', 'austrian', 'russian')
        for x in (-.052, .052) if double else (0,):
            for y in (1.30, 1.41, 1.52, 1.63, 1.73):
                _button(b, 'Restrained uniform fastening', x, y, _front(levels, x, y, .020), metal)
        # Narrow fabric epaulettes follow the shoulder's slope rather than forming spheres.
        if kind != 'ottoman':
            for side in (-1, 1):
                pts = [(side * .160, 1.816, .044), (side * .294, 1.775, .034),
                       (side * .294, 1.775, -.032), (side * .160, 1.816, -.040)]
                # Top-facing surface. Winding chosen separately from front-facing patches.
                if side < 0:
                    pts.reverse()
                _panel(b, 'Flat embroidered shoulder strap', pts, facing, 'Chest', .003)
        if kind in ('royal', 'russian', 'austrian', 'naval'):
            default = {'royal': (.30, .035, .034), 'russian': (.07, .18, .30),
                       'austrian': (.32, .037, .022), 'naval': (.10, .22, .32)}[kind]
            sash = b.material('Woven order sash', _color(profile.get('sash'), default), .74)
            pts = []
            for i in range(16):
                t = i / 15
                x, y = -.192 + .355 * t, 1.748 - .450 * t
                pts.append((x, y, _front(levels, x, y, .027)))
            _strip(b, 'Flat diagonal order sash', pts, .052, sash)
        if kind in ('prussian', 'russian', 'royal', 'austrian'):
            x, y = .133, 1.606
            z = _front(levels, x, y, .026)
            star = [(x + math.cos(i * math.pi / 4) * (.015 if i % 2 == 0 else .005),
                     y + math.sin(i * math.pi / 4) * (.015 if i % 2 == 0 else .005), z) for i in range(8)]
            _panel(b, 'Small geometric breast decoration', star, metal, 'Chest', .001)
        if kind == 'ottoman':
            for y in (1.38, 1.47, 1.56, 1.65, 1.73):
                pts = [(x, y, _front(levels, x, y, .008)) for x in (-.10, -.05, 0, .05, .10)]
                _strip(b, 'Fine horizontal silk fastening braid', pts, .003, facing, 'Chest')
        # Two functional cloth pocket flaps give scale without excess metallic decoration.
        for side in (-1, 1):
            points = [(side * .13, 1.245, _front(levels, side * .13, 1.245, .006)),
                      (side * .185, 1.260, _front(levels, side * .185, 1.260, .006))]
            _strip(b, 'Tailored pocket opening', points, .010, cloth)


def _gown(b, profile, cloth, white, metal, leather, sole):
    child = int(profile.get('age', 30)) < 13
    hem = .38 if child else .47
    skirt = [(0.055, hem, hem * .67, -.014), (.068, hem + .004, hem * .68, -.014),
             (.19, hem * .985, hem * .68, -.015),
             (.46, .343 if child else hem * .85, hem * .62, -.017),
             (.75, .293 if child else hem * .70, hem * .53, -.016),
             (.97, .239 if child else .267, .164 if child else .188, -.014),
             (1.15, .210 if child else .220, .137 if child else .142, .0),
             (1.265, .193, .113, .01)]
    _loft(b, 'Full length paneled court skirt', skirt, cloth, segments=64,
          folds=.006 if child else .009, thickness=.003)
    bodice = [(1.24, .194, .114, .012), (1.29, .196, .116, .014),
              (1.44, .217, .135, .016), (1.59, .247, .151, .014),
              (1.73, .262, .137, .015), (1.80, .276, .114, .024)]
    bodice += [(1.841, .185, .099, .033), (1.90, .096, .078, .038),
               (1.936, .091, .076, .038)]
    def neckline(p, a, y):
        if child:
            return p
        t = _clamp((y - 1.841) / .095)
        dip = .009 * max(0.0, math.cos(a)) ** 2 * t
        return p[0], p[1] - dip, p[2]
    _loft(b, 'Darted court dress bodice', bodice, cloth, transform=neckline, thickness=.0025)
    _sleeves(b, cloth, white, female=True)
    if child:
        _collar(b, white, white)
    else:
        def lace_edge(p, a, y):
            return p[0], p[1] - .009 * max(0.0, math.cos(a)) ** 2, p[2]
        _loft(b, 'Narrow ivory lace neckline',
              [(1.911, .098, .081, .038), (1.915, .098, .081, .038),
               (1.936, .094, .079, .038), (1.940, .093, .078, .038)],
              white, segments=48, transform=lace_edge, bone='Chest', thickness=.0015)
    belt = [(1.242, .198, .118, .012), (1.248, .200, .120, .012),
            (1.282, .201, .121, .012), (1.288, .199, .119, .012)]
    ribbon = b.material('Waist silk ribbon', _color(profile.get('sash'), (.09, .15, .19)), .65)
    _loft(b, 'Narrow woven dress waistband', belt, ribbon, segments=48, thickness=.002)
    for side in (-1, 1):
        points = [(side * .011, 1.255, .143), (side * .064, 1.236, .145),
                  (side * .062, 1.286, .145), (side * .010, 1.271, .145)]
        if side < 0:
            points.reverse()
        _panel(b, 'Folded dress ribbon bow', points, ribbon, thickness=.002)
    _button(b, 'Single small dress clasp', 0, 1.267, .153, metal, .008, 'Pelvis')
    _footwear(b, leather, sole)


def _kimono(b, cloth, white, facing, leather, sole):
    levels = [(1.225, .229, .131, .023), (1.24, .230, .131, .023),
              (1.35, .234, .136, .023),
              (1.62, .265, .149, .019), (1.78, .286, .123, .019),
              (1.90, .097, .077, .038), (1.928, .094, .076, .038)]
    _loft(b, 'Full length folded kimono', levels, cloth, shape=.83, folds=.004, thickness=.003)
    _sleeves(b, cloth, facing, kimono=True)
    # Crossed flat collar strips, layered with the wearer's left side outside.
    for side, lift in ((-1, .009), (1, .014)):
        points = []
        for i in range(15):
            t = i / 14
            x, y = side * (.081 - .228 * t), 1.925 - .57 * t
            points.append((x, y, _front(levels, x, y, lift)))
        _strip(b, 'Visible folded kimono lining', points, .036, white, 'Chest')
        _strip(b, 'Silk kimono collar edge', [(x, y, z + .0015) for x, y, z in points],
               .026, facing, 'Chest')
    hakama = b.material('Charcoal pleated hakama', (.018, .022, .019), .88)
    _loft(b, 'Long finely pleated hakama',
          [(.091, .318, .193, .001), (.105, .319, .194, .001),
           (.42, .293, .179, .009), (.78, .265, .156, .015),
           (1.08, .239, .145, .020), (1.235, .232, .141, .022)],
          hakama, segments=64, folds=.007, shape=.8, thickness=.003)
    _loft(b, 'Flat woven obi', [(1.211, .241, .146, .022), (1.217, .242, .147, .022),
                              (1.294, .239, .142, .022), (1.3, .238, .141, .022)],
          facing, thickness=.003)
    # Small original geometric roundels, not an asserted historical family crest.
    for side in (-1, 1):
        _button(b, 'Small white formal roundel', side * .155, 1.665,
                _front(levels, side * .155, 1.665, .009), white, .014)
    _footwear(b, white, sole, tabi=True)


def build(b, profile, face):
    """Build original clothing on an already-created shared nine-bone armature.

    The `face` argument is reserved for caller compatibility; no face geometry or
    bones are modified. Returns the created clothing mesh objects for inspection.
    """
    del face
    kind = profile.get('wardrobe', 'statesman')
    if kind == 'qing':
        raise ValueError('Qing court dress is authored by build_leaders, not period_wardrobe.')
    required = ('Chest', 'Pelvis', 'LeftUpperArm', 'LeftForearm', 'RightUpperArm', 'RightForearm')
    missing = [name for name in required if name not in b.rig.data.bones]
    if missing:
        raise ValueError('period_wardrobe requires the shared rig; missing: ' + ', '.join(missing))
    before = set(b.bpy.context.scene.objects)
    defaults = {'naval': (.012, .021, .039), 'prussian': (.018, .028, .050),
                'royal': (.018, .037, .041), 'austrian': (.62, .58, .48),
                'russian': (.020, .043, .029), 'statesman': (.016, .019, .022),
                'ottoman': (.022, .046, .062), 'gown': (.40, .31, .22),
                'shogun': (.028, .040, .033)}
    colors = {'naval': (.30, .19, .065), 'prussian': (.30, .024, .029),
              'royal': (.26, .026, .025), 'austrian': (.34, .024, .028),
              'russian': (.29, .027, .021), 'statesman': (.012, .014, .017),
              'ottoman': (.024, .025, .028), 'gown': (.63, .58, .49),
              'shogun': (.014, .023, .024)}
    profile = dict(profile, wardrobe=kind)
    coat_color = _color(profile.get('coat'), defaults.get(kind, defaults['statesman']))
    cloth = b.material('Period tailored ' + kind + ' cloth', coat_color, .80 if kind != 'gown' else .69)
    facing = b.material('Period ' + kind + ' collar and cuffs', colors.get(kind, (.03, .03, .03)), .77)
    white = b.material('Fine ivory linen', (.72, .68, .59), .86)
    metal = b.material('Restrained aged brass', (.48, .32, .105), .44, .55)
    leather = b.material('Soft black polished leather', (.010, .012, .014), .39)
    sole = b.material('Dark shoe sole', (.013, .010, .009), .87)
    trouser_color = (.58, .56, .48) if kind in ('naval', 'austrian') else (.045, .051, .051)
    trousers = b.material('Period wool trousers', trouser_color, .86)
    if kind == 'gown' or profile.get('sex') == 'female':
        _gown(b, profile, cloth, white, metal, leather, sole)
    elif kind == 'shogun':
        _kimono(b, cloth, white, facing, leather, sole)
    else:
        _coat(b, profile, cloth, facing, white, metal, trousers, leather, sole)
    return [ob for ob in b.bpy.context.scene.objects if ob not in before and ob.type == 'MESH']
