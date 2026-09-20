"""Fitted, alpha-textured period hair for the 0.11 character authoring pipeline.

Call build(b, v, profile, face) before child_proportions and consolidation.
Uses the existing MakeHuman CC0 short04/short02/braid01 meshes and textures.
No spherical scalp shell, mirrored half-head, or fixed gallery-height cap.
Beards and qing/chonmage/fez headwear remain the caller's responsibility.
The geometry is an artistic adaptation, not a verified historical likeness.
"""
import math
from pathlib import Path

import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree


_MARKERS = {}
EXTERNAL_STYLES = frozenset(('qing', 'chonmage', 'fez'))


def _smooth(a, z, value):
    t = max(0., min(1., (value-a) / max(z-a, 1e-8)))
    return t*t*(3.-2.*t)


def _gallery(b, co):
    p = b.vec(co)
    return Vector((p[0], p[2], -p[1]))


def _frame(b, v, face):
    """Use fitted landmarks; all lengths follow the actual generated head."""
    path = str(b.MH/'3dobjs/base.obj')
    if path not in _MARKERS:
        _, _, faces, groups = b.readobj(Path(path))
        _MARKERS[path] = sorted({a[0] for f, g in zip(faces, groups)
                                if g in ('joint-l-eye', 'joint-r-eye') for a in f})
    points = [_gallery(b, v[i]) for i in _MARKERS[path]]
    eye = sum(points, Vector()) / len(points)
    head = [Vector((p.co.x, p.co.z, -p.co.y)) for p in face.data.vertices]
    top = max(p.y for p in head)
    height = max(.06, top-eye.y)
    scalp = [p for p in head if p.y > eye.y]
    width = max(abs(p.x-eye.x) for p in scalp)
    back = min(p.z for p in scalp)
    front = max(p.z for p in scalp)
    return dict(eye=eye, top=top, height=height, width=width, back=back,
                front=front, center=Vector((eye.x, eye.y+.12*height,
                                           (front+back)*.5)), unit=height/.15)


def _components(count, faces):
    parent = list(range(count))
    def find(i):
        while parent[i] != i:
            parent[i] = parent[parent[i]]
            i = parent[i]
        return i
    for face in faces:
        root = find(face[0][0])
        for i, _ in face:
            parent[find(i)] = root
    groups = {}
    for i in range(count):
        groups.setdefault(find(i), []).append(i)
    return list(groups.values())


def _male_shape(points, style, frame):
    center, h = frame['center'], frame['height']
    for p in points:
        d = p-center
        # Smooth angular retreat keeps the complete native alpha-edged cards.
        # There is no planar face cut along a new artificial hairline.
        front = _smooth(-.015*frame['unit'], .09*frame['unit'], d.z)
        temple = min(1., abs(d.x)/max(frame['width'], 1e-6))
        if style in ('receding', 'balding'):
            angle = (.15+.18*temple)*front
        elif style in ('swept', 'high_swept', 'long_swept'):
            angle = .12*front
        else:
            angle = .035*front
        p.y = center.y+d.y*math.cos(angle)+d.z*math.sin(angle)
        p.z = center.z+d.z*math.cos(angle)-d.y*math.sin(angle)
        if style == 'high_swept':
            crown = _smooth(frame['eye'].y+.45*h, frame['top'], p.y)
            p.y += .13*h*front*crown
            p.z -= .08*h*front*crown
        if style == 'long_swept':
            nape = 1.-_smooth(frame['eye'].y-.45*h, frame['eye'].y+.35*h, p.y)
            rear = 1.-_smooth(center.z, center.z+.065*frame['unit'], p.z)
            p.y -= .35*h*nape*rear
            p.z -= .055*h*nape*rear


def _gather_braid(points, faces, frame, style):
    """Retain the unmirrored scalp; coil separate hanging braid components.

    The original asset's scalp spans forehead-to-nape and must not be cropped
    by a horizontal plane. Only components entirely below the ear line are
    gathered. Their UVs, individual braid strands and topology remain intact.
    """
    eye, h = frame['eye'], frame['height']
    tails = [i for group in _components(len(points), faces)
             if max(points[j].y for j in group) < eye.y-.28*h
             and min(points[j].y for j in group) < eye.y-1.05*h
             for i in group]
    if not tails:
        return
    top = max(points[i].y for i in tails)
    bottom = min(points[i].y for i in tails)
    ys = np.linspace(bottom, top, 40)
    original = np.array([points[i][:] for i in tails], dtype=np.float64)
    centers = []
    for y in ys:
        near = np.argsort(abs(original[:, 1]-y))[:max(12, len(tails)//60)]
        centers.append(np.mean(original[near], axis=0))
    centers = np.asarray(centers)
    # A small rear coil, with a shallower profile for the child's dress hair.
    radius = .25*h if style == 'child_curls' else .31*h
    bun_y = eye.y-.02*h
    bun_z = frame['back']-.065*h
    for i in tails:
        p = points[i]
        t = max(0., min(1., (top-p.y)/max(top-bottom, 1e-6)))
        cx = float(np.interp(p.y, ys, centers[:, 0]))
        cz = float(np.interp(p.y, ys, centers[:, 2]))
        cross_x, cross_z = (p.x-cx)*.29, (p.z-cz)*.29
        angle = .5*math.pi+math.tau*1.65*t
        r = radius*(1.-.72*t)
        points[i] = Vector((eye.x+(r+cross_x)*math.cos(angle),
                            bun_y+(r+cross_x)*math.sin(angle),
                            bun_z+cross_z-.10*h*t))
    if style in ('side_curls', 'child_curls'):
        # Small, continuous waves in the existing side cards, not solid curls
        # glued onto a cap. The native scalp and centre seam stay connected.
        tail_set = set(tails)
        for i, p in enumerate(points):
            if i in tail_set:
                continue
            side = _smooth(.62*frame['width'], frame['width'], abs(p.x-eye.x))
            lower = 1.-_smooth(eye.y+.05*h, eye.y+.58*h, p.y)
            wave = math.sin((p.y-eye.y)/h*math.tau*1.35)
            p.x += (.025*h if style == 'child_curls' else .036*h)*side*lower*wave*(1 if p.x>eye.x else -1)
            p.z += .024*h*side*lower*math.cos((p.y-eye.y)/h*math.tau*1.35)


def _female_forehead(points, frame):
    """Open both eyes and the forehead without cutting/mirroring the cards.

    braid01 has a modern diagonal fringe. Its original long left forelock is
    smoothly gathered up into a shallow, symmetric forehead arc. Only the
    front, central hair is raised; the native ear-side rolls remain hanging.
    """
    eye, h, width, center = (frame[k] for k in ('eye', 'height', 'width', 'center'))
    for p in points:
        xn = abs(p.x-eye.x)/max(width, 1e-6)
        front = _smooth(center.z+.12*h, center.z+.46*h, p.z)
        central = 1.-_smooth(.78, 1.12, xn)
        weight = front*central
        if weight < 1e-6:
            continue
        floor = eye.y+h*(.59-.10*min(1., xn*xn))
        # Smooth maximum: no hard horizontal cut or collapsed line of vertices.
        softness = .075*h
        delta = max(-60., min(60., (p.y-floor)/softness))
        gathered_y = floor+softness*math.log1p(math.exp(delta))
        lift = max(0., gathered_y-p.y)*weight
        p.y += lift
        # The raised forelock also retreats from its original position over
        # the eye. Scalp penetration is corrected afterwards on the real face.
        p.z -= .42*lift
        # Spread the gathered strands gently to either side of the part.
        p.x += (p.x-eye.x)*.08*min(1., lift/max(.3*h, 1e-6))


def _unembed(points, face, unit):
    """Correct only small scalp penetrations; do not project hair to a sphere."""
    tree = BVHTree.FromPolygons([p.co.copy() for p in face.data.vertices],
                               [list(p.vertices) for p in face.data.polygons])
    for i, p in enumerate(points):
        co = Vector((p.x, -p.z, p.y))
        hit, normal, _, distance = tree.find_nearest(co)
        if hit is not None and distance < .035*unit and (co-hit).dot(normal) < .0006*unit:
            co = hit+normal*.0009*unit
            points[i] = Vector((co.x, co.z, -co.y))


def _bald_coverage(p, frame):
    # Feathered crown/front loss; alpha is baked into the existing texture.
    # A continuous fade avoids polygon-cut borders on the exposed scalp.
    h, center = frame['height'], frame['center']
    ex = (p.x-center.x)/max(.85*frame['width'], 1e-6)
    ez = (p.z-(center.z+.24*h))/max(.93*h, 1e-6)
    loss = (1.-_smooth(.58, 1.10, ex*ex+ez*ez))
    loss *= _smooth(frame['eye'].y+.22*h, frame['eye'].y+.50*h, p.y)
    return 1.-loss


def _uv_coverage(size, uv, faces, weights, part=None):
    """Bake hair loss or a narrow centre part into portable texture alpha."""
    result = np.ones((size, size), dtype=np.float32)
    for face in faces:
        for j in range(1, len(face)-1):
            tri = (face[0], face[j], face[j+1])
            values = np.array([weights[a[0]] for a in tri], dtype=np.float32)
            if min(values) > .999 and part is None:
                continue
            if part is not None:
                points, frame = part
                xyz = np.asarray([points[a[0]][:] for a in tri], dtype=np.float32)
                line_width = .016*frame['height']
                # Most atlas triangles cannot intersect the tiny part line.
                if xyz[:, 0].min() > frame['eye'].x+line_width or xyz[:, 0].max() < frame['eye'].x-line_width:
                    continue
            co = np.array([uv[a[1]] for a in tri], dtype=np.float32)*(size-1)
            low = np.maximum(0, np.floor(co.min(axis=0)).astype(int))
            high = np.minimum(size-1, np.ceil(co.max(axis=0)).astype(int))
            if np.any(high < low):
                continue
            a, z, c = co
            det = (z[1]-c[1])*(a[0]-c[0])+(c[0]-z[0])*(a[1]-c[1])
            if abs(det) < 1e-7:
                continue
            xx, yy = np.meshgrid(np.arange(low[0], high[0]+1)+.5,
                                 np.arange(low[1], high[1]+1)+.5)
            w0 = ((z[1]-c[1])*(xx-c[0])+(c[0]-z[0])*(yy-c[1]))/det
            w1 = ((c[1]-a[1])*(xx-c[0])+(a[0]-c[0])*(yy-c[1]))/det
            w2 = 1.-w0-w1
            inside = (w0>=-.01)&(w1>=-.01)&(w2>=-.01)
            region = result[low[1]:high[1]+1, low[0]:high[0]+1]
            coverage = np.clip(w0*values[0]+w1*values[1]+w2*values[2], 0., 1.)
            if part is not None:
                xyz_at = w0[:, :, None]*xyz[0]+w1[:, :, None]*xyz[1]+w2[:, :, None]*xyz[2]
                h, eye = frame['height'], frame['eye']
                distance = abs(xyz_at[:, :, 0]-eye.x)
                line = np.clip((distance-.0035*h)/(.0085*h), 0., 1.)
                line = line*line*(3.-2.*line)
                start = np.clip((xyz_at[:, :, 1]-(eye.y+.53*h))/(.055*h), 0., 1.)
                front = np.clip((xyz_at[:, :, 2]-(frame['center'].z-.04*h))/(.12*h), 0., 1.)
                coverage *= 1.-(1.-line)*start*front
            region[inside] = np.minimum(region[inside], coverage[inside])
    return result


def _material(b, key, profile, uv, faces, coverage=None, part=None):
    source = bpy.data.images.load(str(b.SYS/f'hair/{key}/{key}_diffuse.png'), check_existing=True)
    # Keep packed authoring data in memory; the caller owns export and saving.
    size = min(int(profile.get('hair_texture_size', 1024)), source.size[0])
    sampled = source.copy()
    sampled.scale(size, size)
    rgba = np.empty(size*size*4, dtype=np.float32)
    sampled.pixels.foreach_get(rgba)
    rgba = rgba.reshape((size, size, 4))
    rgb = rgba[:, :, :3]
    lum = rgb @ np.array([.2126, .7152, .0722], dtype=np.float32)
    visible = lum[rgba[:, :, 3]>.75]
    median = max(float(np.median(visible)) if visible.size else .1, .008)
    # Preserve original fine highlights, shadows and strand variation for grey
    # as well as dark hair; a flat Base Color would erase all that detail.
    detail = np.clip(np.power(np.maximum(lum, .0001)/median, .78), .12, 3.0)
    tint = np.asarray(profile.get('hair_color', (.06, .04, .025)), dtype=np.float32)
    rgba[:, :, :3] = np.clip(detail[:, :, None]*tint[None, None, :], 0., .94)
    if coverage is not None or part is not None:
        if coverage is None:
            coverage = [1.]*len(part[0])
        rgba[:, :, 3] *= _uv_coverage(size, uv, faces, coverage, part)
    image = bpy.data.images.new(profile.get('identity', 'leader')+' period hair RGBA',
                                width=size, height=size, alpha=True)
    image.colorspace_settings.name = 'sRGB'
    image.pixels.foreach_set(rgba.ravel())
    image.update()
    image.pack()
    bpy.data.images.remove(sampled)
    mat = b.material('CC0 fitted '+key+' '+profile.get('identity', ''), tuple(tint), .66)
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    shader = nodes.get('Principled BSDF')
    texture = nodes.new('ShaderNodeTexImage')
    texture.name = 'Portable baked strand color and alpha'
    texture.image = image
    links.new(texture.outputs['Color'], shader.inputs['Base Color'])
    links.new(texture.outputs['Alpha'], shader.inputs['Alpha'])
    shader.inputs['Specular IOR Level'].default_value = .23
    mat.surface_render_method = 'DITHERED'
    mat.use_backface_culling = False
    if hasattr(mat, 'use_transparency_overlap'):
        mat.use_transparency_overlap = False
    mat['source_asset'] = 'MakeHuman system-assets/hair/'+key+' (CC0)'
    return mat


def _comb(b, points, frame, profile):
    """A small comb follows the fitted hair surface, including child heads."""
    h, top, eye = frame['height'], frame['top'], frame['eye']
    gold = b.material('Fine period hair comb gold', (.40, .27, .09), .38, .60)
    pearl = b.material('Small pearl hair ornaments', (.72, .65, .49), .32, .06)
    knots = []
    for i in range(9):
        x = eye.x+(i-4)*.067*h
        y = top-.20*h-.12*h*((i-4)/4)**2
        near = sorted(points, key=lambda p: ((p.x-x)/.7)**2+(p.y-y)**2)[:22]
        front = max(near, key=lambda p: p.z)
        knots.append((x, y, front.z+.015*h))
    b.curve('Slim pearl comb setting', knots, .0075*h, gold)
    for i, p in enumerate(knots):
        if i%2:
            continue
        b.sphere('Small pearl on fitted hair', (p[0], p[1]+.011*h, p[2]),
                 (.015*h, .019*h, .014*h), pearl)


def build(b, v, profile, face):
    """Build textured hair; return its mesh objects (empty for caller headwear)."""
    style = profile.get('hair', 'swept')
    if style in EXTERNAL_STYLES:
        return []
    female = profile.get('sex') == 'female'
    key = 'braid01' if female else ('short02' if style in ('side_part', 'high_swept') else 'short04')
    fitted, uv, faces = b.fit_proxy(b.SYS/f'hair/{key}/{key}', v)
    points = [_gallery(b, co) for co in fitted]
    frame = _frame(b, v, face)
    if female:
        _gather_braid(points, faces, frame, style)
        _female_forehead(points, frame)
    else:
        _male_shape(points, style, frame)
    _unembed(points, face, frame['unit'])
    coverage = [_bald_coverage(p, frame) for p in points] if style == 'balding' else None
    mat = _material(b, key, profile, uv, faces, coverage,
                    part=(points, frame) if female else None)
    ob = b.meshobj('Fitted alpha strand hair '+style, [b.cv(p) for p in points],
                   [[a[0] for a in f] for f in faces], mat,
                   [[uv[a[1]] for a in f] for f in faces])
    # Do not subdivide: hair-card UV edges and the native wisps must be retained.
    b.group_object(ob, 'Head')
    ob['hair_style'] = style
    ob['provenance'] = 'CC0 MakeHuman '+key+'; original period adaptation'
    ob['likeness_status'] = 'Provisional artistic reconstruction'
    if female:
        _comb(b, points, frame, profile)
    return [ob]
