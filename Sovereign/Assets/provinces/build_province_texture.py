"""Rasterize original schematic administrative polygons into IDs for the map shader."""
from pathlib import Path
import json
from PIL import Image, ImageDraw
root = Path(__file__).resolve().parent
data = json.loads((root / 'qing-provinces-schematic.geojson').read_text(encoding='utf-8'))
image = Image.new('RGB', (4096, 2048)); draw = ImageDraw.Draw(image)
for index, feature in enumerate(data['features'], 1):
    for polygon in feature['geometry']['coordinates']:
        for ring_index, ring in enumerate(polygon):
            points = [((lon + 180) / 360 * 4096, (90 - lat) / 180 * 2048) for lon, lat in ring]
            draw.polygon(points, fill=(index if ring_index == 0 else 0, 0, 0))
image.save(root / 'qing-province-ids.png', optimize=True)
