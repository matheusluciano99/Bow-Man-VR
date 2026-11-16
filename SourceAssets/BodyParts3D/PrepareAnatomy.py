"""Download and verify the selected atlas surfaces without installing tools or using the viewer's private API."""
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
import csv
import hashlib
import json
import re
import time
import urllib.parse
import urllib.request

ROOT = Path(__file__).resolve().parents[2]
CACHE = ROOT / 'SourceAssets/Downloads/BodyParts3D43'
MODELS = ROOT / 'Assets/ThirdParty/BodyParts3D/Models'
REVISION = 'fd527e6f4daf732fd814314d9257df5877b844bc'
REPOSITORY = 'olivercase/body_parts_3d_api'
RAW = f'https://raw.githubusercontent.com/{REPOSITORY}/{REVISION}/'

# Fetches public, pinned data and retains the immutable response in the local source cache.
def cached(path, url):
    path.parent.mkdir(parents=True, exist_ok=True)
    if not path.exists():
        request = urllib.request.Request(url, headers={'User-Agent': 'BowManVR-AssetPreparation/1.0'})
        for attempt in range(3):
            try:
                with urllib.request.urlopen(request, timeout=60) as response:
                    content = response.read()
                break
            except OSError:
                if attempt == 2:
                    raise
                time.sleep(2 ** attempt)
        temporary = path.with_suffix(path.suffix + '.part')
        temporary.write_bytes(content)
        temporary.replace(path)
    return path.read_bytes()

# Reads the versioned FMA membership map instead of inferring anatomy from the redistributor's CSV IDs.
def memberships():
    text = cached(CACHE / 'FMA2Obj.txt', RAW + 'metadata/FMA2Obj.txt').decode()
    assert '# Data Version\t4.3' in text
    result = {}
    for line in text.splitlines():
        fields = line.split('\t')
        if len(fields) == 3 and fields[1] == 'part_of':
            result[fields[0]] = set(fields[2].split('+'))
    return result

# Selects actual tissue surfaces within one named organ, excluding vessels, ducts and nerves.
def selected(rows, members, fma, pattern):
    return [r['fj_id'] for r in rows if r['fj_id'] in members[fma] and re.search(pattern, r['name'], re.I)
            and not re.search(r'artery|vein|nerve|vessel|plexus|mesentery|tributary|biliary|aqueduct', r['name'], re.I)]

# Downloads a real OBJ and checks its size and digest against the pinned Git LFS pointer.
def surface(item):
    name = Path(item['path']).name
    quoted = urllib.parse.quote(item['path'], safe='/')
    pointer = cached(CACHE / 'pointers' / (name + '.txt'), RAW + quoted).decode()
    digest = re.search(r'^oid sha256:([a-f0-9]{64})$', pointer, re.M).group(1)
    size = int(re.search(r'^size (\d+)$', pointer, re.M).group(1))
    assert 0 < size < 90_000_000, (name, size)
    content = cached(CACHE / 'meshes' / name,
                     f'https://media.githubusercontent.com/media/{REPOSITORY}/{REVISION}/{quoted}')
    assert len(content) == size and hashlib.sha256(content).hexdigest() == digest, name
    text = content.decode()
    assert '\nv ' in text and '\nf ' in text and 'version https://git-lfs' not in text, name
    MODELS.mkdir(parents=True, exist_ok=True)
    destination = MODELS / name
    if not destination.exists() or destination.read_bytes() != content:
        destination.write_bytes(content)
    return {'id': re.match(r'FJ\d+M?', name).group(), 'filename': name, 'sha256': digest, 'bytes': size,
            'url': f'https://media.githubusercontent.com/media/{REPOSITORY}/{REVISION}/{quoted}'}

# Writes the explicit organ inventory and source provenance used by the Unity fitting tool.
def main():
    members = memberships()
    rows = list(csv.DictReader(cached(CACHE / 'MANIFEST.csv', RAW + 'MANIFEST.csv').decode().splitlines()))
    tree = json.loads(cached(CACHE / 'tree.json',
        f'https://api.github.com/repos/{REPOSITORY}/git/trees/{REVISION}?recursive=1'))['tree']
    by_id = {re.match(r'meshes/(FJ\d+M?)_', item['path']).group(1): item for item in tree
             if re.match(r'meshes/FJ\d+M?_', item['path']) and item['path'].endswith('.obj')}
    organs = []

    # Keeps paired organs distinct while their atomic tissue pieces share one anatomical label.
    def organ(kind, side, region, bone, fma, ids):
        assert ids and len(ids) == len(set(ids)), (kind, side)
        assert set(ids).issubset(by_id), (kind, side, fma)
        organs.append({'kind': kind, 'side': side, 'region': region, 'bone': bone, 'fma': fma,
                       'ids': sorted(ids)})

    brain = selected(rows, members, 'FMA50801',
        r'cerebr|gyrus|matter|cerebell|thalam|hippocamp|hypothalam|putamen|amygdala|insula|globus|midbrain|medulla|pons$|substantia|subthalamic|caudate nucleus|occipital lobe|callosum')
    organ('Brain', 'Center', 'Head', 'Head', 'FMA50801', brain)
    organ('Heart', 'Center', 'Chest', 'UpperChest', 'FMA7088', ['FJ2428', 'FJ2438', 'FJ2439'])
    for fma, side in [('FMA7310', 'Left'), ('FMA7309', 'Right')]:
        ids = selected(rows, members, fma, r'^Parenchyma of .*bronchopulmonary segment$')
        organ('Lung', side, 'Chest', 'UpperChest', fma, ids)
    organ('Liver', 'Center', 'Abdomen', 'Spine', 'FMA7197',
          selected(rows, members, 'FMA7197', r'^(Hepatovenous segment [IVX]+|Caudate lobe of liver)$'))
    for kind, fma, identifier in [('Stomach', 'FMA7148', 'FJ2564'), ('Pancreas', 'FMA7198', 'FJ1895'),
                                  ('Spleen', 'FMA7196', 'FJ2561')]:
        organ(kind, 'Center', 'Abdomen', 'Spine', fma, [identifier])
    for fma, side, identifier in [('FMA7205', 'Left', 'FJ3145'), ('FMA7204', 'Right', 'FJ3147')]:
        organ('Kidney', side, 'Abdomen', 'Spine', fma, [identifier])
    organ('SmallIntestine', 'Center', 'Abdomen', 'Spine', 'FMA7200',
          selected(rows, members, 'FMA7200', r'duodenum|jejunum|ileum'))
    large = selected(rows, members, 'FMA7201', r'colon|cecum|caecum|rectum|appendix|sigmoid')
    lower = [identifier for identifier in large if next(r['name'] for r in rows if r['fj_id'] == identifier).lower() == 'rectum']
    organ('LargeIntestine', 'Center', 'Abdomen', 'Spine', 'FMA7201', [i for i in large if i not in lower])
    organ('LargeIntestine', 'Center', 'Pelvis', 'Hips', 'FMA7201', lower)
    organ('Bladder', 'Center', 'Pelvis', 'Hips', 'FMA15900', ['FJ3149'])
    guides = ['FJ1331', 'FJ1382', 'FJ3365', 'FJ3259']
    identifiers = sorted(set(guides).union(*(set(o['ids']) for o in organs)))
    with ThreadPoolExecutor(max_workers=4) as executor:
        files = list(executor.map(surface, (by_id[i] for i in identifiers)))
    output = {'repository': REPOSITORY, 'revision': REVISION, 'organs': organs, 'guides': guides, 'files': files}
    (ROOT / 'SourceAssets/BodyParts3D/Sources.json').write_text(json.dumps(output, indent=2) + '\n')
    print(json.dumps({'organs': len(organs), 'surfaces': len(files), 'bytes': sum(f['bytes'] for f in files)}), flush=True)

if __name__ == '__main__':
    main()
