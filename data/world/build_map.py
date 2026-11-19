import json
import sys
import urllib.request
from pathlib import Path

from shapely.geometry import MultiPolygon, Polygon, box, shape
from shapely.ops import unary_union

SOURCE = "https://raw.githubusercontent.com/nvkelso/natural-earth-vector/master/geojson/ne_110m_admin_0_countries.geojson"
WIDTH = 2000
HEIGHT = 1000
SIMPLIFY = 0.2
MIN_AREA = 0.4

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "frontend" / "assets" / "maps" / "coldwar.json"

SOVIET = ["RUS", "UKR", "BLR", "MDA", "EST", "LVA", "LTU", "GEO", "ARM", "AZE", "KAZ", "UZB", "TKM", "TJK", "KGZ"]
YUGOSLAV = ["SRB", "MNE", "HRV", "SVN", "BIH", "MKD", "KOS"]

ENTITIES = [
    ("USSR", "Soviet Union", "East", SOVIET),
    ("POL", "Poland", "East", ["POL"]),
    ("CZE", "Czechoslovakia", "East", ["CZE", "SVK"]),
    ("HUN", "Hungary", "East", ["HUN"]),
    ("ROU", "Romania", "East", ["ROU", "ROM"]),
    ("BGR", "Bulgaria", "East", ["BGR"]),
    ("PRC", "China", "East", ["CHN"]),
    ("PRK", "North Korea", "East", ["PRK"]),
    ("CUB", "Cuba", "East", ["CUB"]),
    ("USA", "United States", "West", ["USA"]),
    ("CAN", "Canada", "West", ["CAN"]),
    ("GBR", "United Kingdom", "West", ["GBR"]),
    ("FRA", "France", "West", ["FRA"]),
    ("ITA", "Italy", "West", ["ITA"]),
    ("TUR", "Turkey", "West", ["TUR"]),
    ("JPN", "Japan", "West", ["JPN"]),
    ("KOR", "South Korea", "West", ["KOR"]),
    ("AUS", "Australia", "West", ["AUS"]),
    ("YUG", "Yugoslavia", "NonAligned", YUGOSLAV),
    ("IND", "India", "NonAligned", ["IND"]),
    ("EGY", "Egypt", "NonAligned", ["EGY"]),
    ("BRA", "Brazil", "NonAligned", ["BRA"]),
    ("IRN", "Iran", "NonAligned", ["IRN"]),
]

GERMAN_EAST = Polygon([
    (10.85, 54.6), (10.85, 53.9), (10.6, 53.4), (11.0, 53.0), (11.0, 52.2), (10.6, 51.8),
    (10.1, 51.4), (10.0, 50.9), (10.2, 50.5), (11.3, 50.3), (12.2, 50.2), (16.0, 50.2), (16.0, 54.6)
])
NORTH_VIETNAM = box(100, 17.0, 112, 24)


def fetch():
    with urllib.request.urlopen(SOURCE, timeout=60) as response:
        return json.load(response)


def code_of(properties):
    return properties.get("ADM0_A3") or properties.get("adm0_a3")


def polygons(geometry):
    if isinstance(geometry, Polygon):
        return [geometry]
    if isinstance(geometry, MultiPolygon):
        return list(geometry.geoms)
    return [g for g in getattr(geometry, "geoms", []) if isinstance(g, Polygon)]


def project(lon, lat):
    return [round((lon + 180) / 360 * WIDTH, 1), round((90 - lat) / 180 * HEIGHT, 1)]


def rings_of(geometry, keep_all):
    rings = []
    for polygon in polygons(geometry.simplify(SIMPLIFY, preserve_topology=True)):
        if polygon.is_empty or (not keep_all and polygon.area < MIN_AREA):
            continue
        flat = []
        for lon, lat in polygon.exterior.coords:
            flat.extend(project(lon, lat))
        rings.append(flat)
    return rings


def main():
    data = fetch()
    by_code = {}
    for feature in data["features"]:
        code = code_of(feature["properties"])
        geometry = shape(feature["geometry"])
        if code == "FRA":
            geometry = unary_union([p for p in polygons(geometry) if p.representative_point().x > -20])
        by_code[code] = geometry

    germany = by_code.pop("DEU")
    vietnam = by_code.pop("VNM")
    claimed = {}
    for entity_id, name, bloc, codes in ENTITIES:
        parts = [by_code.pop(code) for code in codes if code in by_code]
        if parts:
            claimed[entity_id] = (name, bloc, unary_union(parts))
    claimed["GDR"] = ("East Germany", "East", germany.intersection(GERMAN_EAST))
    claimed["FRG"] = ("West Germany", "West", germany.difference(GERMAN_EAST))
    claimed["VNM"] = ("North Vietnam", "East", vietnam.intersection(NORTH_VIETNAM))

    entities = []
    for entity_id, (name, bloc, geometry) in claimed.items():
        entities.append({"id": entity_id, "name": name, "bloc": bloc, "rings": rings_of(geometry, True)})
    entities.append({"id": "", "name": "South Vietnam", "bloc": "none", "rings": rings_of(vietnam.difference(NORTH_VIETNAM), True)})
    for code, geometry in sorted(by_code.items()):
        rings = rings_of(geometry, False)
        if rings:
            entities.append({"id": "", "name": code, "bloc": "none", "rings": rings})

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(json.dumps({"width": WIDTH, "height": HEIGHT, "entities": entities}, separators=(",", ":")))
    points = sum(len(r) // 2 for e in entities for r in e["rings"])
    print(f"{len(entities)} entities, {points} points, {OUTPUT.stat().st_size // 1024} KB -> {OUTPUT}")


if __name__ == "__main__":
    sys.exit(main())
