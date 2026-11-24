"""Build compact presentation meshes from the retained BodyParts3D 4.0 archive."""
from pathlib import Path
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "SourceAssets/Downloads/BodyParts3D"
OUTPUT = ROOT / "Assets/ThirdParty/BodyParts3D/Resources/ReplayBones"

GROUPS = {
    "Skull": "FMA46565", "Ribs": "FMA7480", "Pelvis": "FMA9578", "Vertebra": "FMA12519",
    "Humerus": "FMA23130", "Forearm": "FMA23464", "Femur": "FMA24474",
    "Tibia": "FMA24477", "Shoulder": "FMA13395",
}
NON_BONE = {
    "FJ1282", "FJ1285", "FJ1286", "FJ1289", "FJ1297", "FJ1299", "FJ1305",
    "FJ1317", "FJ1320", "FJ1331", "FJ1336", "FJ1337", "FJ1340", "FJ1348",
    "FJ1350", "FJ1356", "FJ1368", "FJ1371", "FJ1382", "FJ2772", "FJ3201",
}

# Preserves each structure's relative pieces and uses the atlas's front/up axes in the game.
def write_obj(archive, identifiers, title):
    vertices, faces = [], []
    for identifier in identifiers:
        lines = archive.read(f"partof_BP3D_4.0_obj_99/{identifier}.obj").decode().splitlines()
        offset = len(vertices)
        for line in lines:
            fields = line.split()
            if fields and fields[0] == "v":
                x, y, z = map(float, fields[1:4])
                vertices.append((x, z, -y))
            elif fields and fields[0] == "f":
                faces.append(tuple(int(field.split("/")[0]) + offset for field in fields[1:]))
    assert vertices and faces, title
    axes = list(zip(*vertices))
    centers = [(min(axis) + max(axis)) / 2 for axis in axes]
    scale = max(max(axis) - min(axis) for axis in axes)
    with (OUTPUT / f"{title}.obj").open("w") as out:
        out.write(f"# BodyParts3D © Database Center for Life Science, CC BY 4.0.\no {title}\ns 1\n")
        for vertex in vertices:
            point = ((value - center) / scale for value, center in zip(vertex, centers))
            out.write("v %.6f %.6f %.6f\n" % tuple(point))
        for face in faces:
            out.write("f " + " ".join(map(str, face)) + "\n")

# Resolves the visible structures through the publisher's archive membership table.
def main():
    rows = [row.split("\t") for row in (SOURCE / "partof_element_parts.txt")
            .read_text().splitlines()[1:]]
    OUTPUT.mkdir(parents=True, exist_ok=True)
    with ZipFile(SOURCE / "partof_BP3D_4.0_obj_99.zip") as archive:
        for title, concept in GROUPS.items():
            identifiers = [identifier for key, _, identifier in rows if key == concept and identifier not in NON_BONE]
            write_obj(archive, identifiers, title)

if __name__ == "__main__":
    main()
