"""Export original schematic polygons for external review. Run from any directory; standard library only.

Runtime source of truth is Simulation/QingProvinceCatalog.cs. This is not a historical GIS import.
"""
import json
import pathlib
import re

HERE = pathlib.Path(__file__).resolve().parent
SOURCE = HERE.parents[1] / "Simulation" / "QingProvinceCatalog.cs"
text = SOURCE.read_text(encoding="utf-8")
pattern = re.compile(
    r'P\("([^"\n]+)", "([^"\n]+)", "([^"\n]+)", "([^"\n]+)", '
    r'([\d.]+), ([\d.]+), (\d+),\s*"([^"\n]+)", "([^"\n]+)"\)'
)
features = []
for match in pattern.finditer(text):
    region, name, administration, capital, lon, lat, weight, rings, note = match.groups()
    polygons = []
    for ring in rings.split(";"):
        coordinates = [[float(value) for value in pair.split(",")] for pair in ring.split()]
        polygons.append([coordinates + [coordinates[0]]])
    features.append({
        "type": "Feature", "id": "QNG_" + region,
        "properties": {
            "name": name, "administration": administration, "capital_label": capital,
            "navigation_center": [float(lon), float(lat)], "rural_scenario_weight": int(weight),
            "note": note, "precision": "Original coarse gameplay schematic, not surveyed 1836 boundaries",
        },
        "geometry": {"type": "MultiPolygon", "coordinates": polygons},
    })
assert len(features) == 27, "Catalog parser did not find all provinces"
output = HERE / "qing-provinces-schematic.geojson"
output.write_text(json.dumps({"type": "FeatureCollection", "features": features}, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(f"Exported {len(features)} original schematic administrative polygons to {output}")
