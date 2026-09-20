"""Validate and index the actual shipped GLBs; standard Python, no Blender needed."""
from pathlib import Path
import ast
import hashlib
import json
import struct

root = Path(__file__).resolve().parent.parent
source = root / 'source'
syntax = ast.parse((source / 'build_leaders_11.py').read_text(encoding='utf-8'))
profiles = next(n.value for n in syntax.body if isinstance(n, ast.Assign)
                and any(isinstance(t, ast.Name) and t.id == 'PROFILES' for t in n.targets))
identities = [ast.literal_eval(k) for k in profiles.keys]
manifest = json.loads((root / 'model-manifest.json').read_text(encoding='utf-8'))
models = []
for identity in identities:
    file = root / (identity + '.glb')
    data = file.read_bytes()
    magic, version, size = struct.unpack_from('<III', data)
    assert magic == 0x46546C67 and version == 2 and size == len(data), file
    length, chunk_type = struct.unpack_from('<II', data, 12)
    assert chunk_type == 0x4E4F534A
    gltf = json.loads(data[20:20 + length])
    primitives = [p for m in gltf['meshes'] for p in m['primitives']]
    morphs = sorted({name for m in gltf['meshes'] for name in m.get('extras', {}).get('targetNames', [])})
    joints = {gltf['nodes'][i]['name'] for s in gltf['skins'] for i in s['joints']}
    assert {'Head', 'Pelvis', 'LeftUpperArm', 'RightUpperArm', 'LeftForearm', 'RightForearm'} <= joints, identity
    assert {'Blink', 'Speech'} <= set(morphs), identity
    assert any('idle' in a['name'].lower() for a in gltf['animations']), identity
    assert (source / (identity + '.blend')).is_file(), identity
    models.append(dict(identity=identity, file=file.name, bytes=len(data),
                       sha256=hashlib.sha256(data).hexdigest(),
                       triangles=sum(gltf['accessors'][p['indices']]['count'] // 3 for p in primitives if 'indices' in p),
                       verticesWithUVSeams=sum(gltf['accessors'][p['attributes']['POSITION']]['count'] for p in primitives),
                       meshes=len(gltf['meshes']), bones=len(joints),
                       artRevision='0.13' if identity == 'daoguang' else '0.11',
                       authoringScript='source/build_leaders_13.py' if identity == 'daoguang' else 'source/build_leaders_11.py',
                       animations=[a['name'] for a in gltf['animations']], faceMorphs=morphs,
                       fullFigure=True,
                       likeness='Artistic reconstruction; not a verified facial likeness or scan',
                       visualStage='age 5' if identity == 'isabella_ii' else 'age 13; selected at age 11+' if identity == 'isabella_ii_adolescent' else 'fixed campaign-era stage'))
manifest.update(version='0.13', authoredModels=models, authoredIdentities=20,
                authoredAgeVariants=21, proceduralFallbackIdentities=0,
                revisionScope='Daoguang middle-age skin albedo, restrained follicle tint, dimensional beard, fitted cap fibres and softer textile collar, band and cape. Animation and lighting retain the 0.12 baseline; other 19 historical identities retain their 0.11 meshes.',
                authoringEntryPoint='source/build_leaders_13.py',
                otherLeadersAuthoringEntryPoint='source/build_leaders_11.py',
                daoguangArtReference='source/DAOGUANG-ART-DIRECTION-0.12.md',
                artReference='source/HISTORICAL-ART-DIRECTION-0.11.md')
(root / 'model-manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(json.dumps({'models': len(models), 'bytes': sum(m['bytes'] for m in models),
                  'trianglesMin': min(m['triangles'] for m in models),
                  'trianglesMax': max(m['triangles'] for m in models)}, indent=2))
