"""Read-only export checks; writes verification metadata, never alters images."""
import base64
import hashlib
import json
from pathlib import Path
from xml.etree import ElementTree as ET
from PIL import Image

root = Path(__file__).resolve().parent
spec = json.loads((root / 'export-spec.json').read_text())
hashes = json.loads((root / 'source' / 'retained-v3-hashes.json').read_text())
checks = {}
checks['retained_wordmarks_and_source_unchanged'] = all(hashlib.sha256((root / name).read_bytes()).hexdigest() == value for name, value in hashes.items())
embedded = []
for entry in spec['specs']:
    svg = ET.parse(root / 'assets' / (entry['name'] + '.svg')).getroot()
    image = svg.find('.//{http://www.w3.org/2000/svg}image')
    embedded.append(hashlib.sha256(base64.b64decode(image.attrib['href'].split(',', 1)[1])).hexdigest())
    with Image.open(root / 'assets' / (entry['name'] + '.png')) as rendered:
        assert rendered.size == (entry['w'], entry['h'])
checks['five_compact_sources_identical'] = len(embedded) == 5 and all(value == spec['sourceHash'] for value in embedded)
checks['compact_export_dimensions_match'] = True
def alpha(name):
    with Image.open(root / 'assets' / name) as image:
        return image.convert('RGBA').getchannel('A').tobytes()
checks['transparent_compact_alpha_identical'] = alpha('ocharo-symbol-ink.png') == alpha('ocharo-micro.png') == alpha('ocharo-micro-reverse.png')
for size in (16, 24, 32):
    for color in ('ink', 'reverse'):
        with Image.open(root / 'assets' / f'ocharo-favicon-{size}-{color}.png') as image:
            assert image.size == (size, size)
    assert alpha(f'ocharo-favicon-{size}-ink.png') == alpha(f'ocharo-favicon-{size}-reverse.png')
checks['favicon_sizes_and_alpha_match'] = True
checks.update(json.loads((root / 'gallery-verification.json').read_text()))
assert all(checks.values()), checks
(root / 'verification.json').write_text(json.dumps(checks, indent=2) + '\n')
manifest = {str(file.relative_to(root)): hashlib.sha256(file.read_bytes()).hexdigest() for folder in ('assets', 'source') for file in sorted((root / folder).rglob('*')) if file.is_file()}
(root / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')
print(json.dumps(checks))
