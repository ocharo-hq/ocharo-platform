from pathlib import Path
import hashlib, json, xml.etree.ElementTree as ET
from PIL import Image

root=Path(__file__).parent
spec=json.loads((root/'export-spec.json').read_text())
needle=spec['needle']
ns={'s':'http://www.w3.org/2000/svg'}
rows=[]
body_hashes={'compact':set(),'wordmark':set()}
for item in spec['specs']:
    file=root/'assets'/(item['name']+'.svg')
    svg=ET.parse(file).getroot()
    path=svg.find('.//s:path[@id="shared-needle"]',ns)
    assert path is not None and path.attrib['d']==needle['d'], item['name']
    assert path.attrib['fill-rule']=='evenodd'
    use=svg.find('.//s:use[@href="#shared-needle"]',ns)
    group='compact' if item['kind']=='compact' else 'wordmark'
    assert use.attrib['transform']==spec[group+'Transform']
    body=svg.find('.//s:mask[@id="body"]/s:image',ns)
    body_hashes[group].add(hashlib.sha256(body.attrib['href'].encode()).hexdigest())
    rows.append({'asset':item['name'],'needle_sha256':needle['sha256'],'transform':use.attrib['transform']})
assert all(len(values)==1 for values in body_hashes.values())
def alpha(file):
    with Image.open(root/'assets'/file) as image:
        return image.getchannel('A').tobytes()
assert alpha('ocharo-wordmark-ink.png')==alpha('ocharo-wordmark-reverse.png')
assert alpha('ocharo-symbol-ink.png')==alpha('ocharo-micro.png')==alpha('ocharo-micro-reverse.png')
for size in (16,24,32):
    for color in ('ink','reverse'):
        with Image.open(root/'assets'/f'ocharo-favicon-{size}-{color}.png') as image:
            assert image.size==(size,size)
    assert alpha(f'ocharo-favicon-{size}-ink.png')==alpha(f'ocharo-favicon-{size}-reverse.png')
report={'compositions':len(rows),'needle_paths_identical':True,'compact_bodies_identical':True,'wordmark_bodies_identical':True,'light_dark_alpha_identical':True,'favicon_sizes':[16,24,32],'composition_checks':rows}
(root/'verification.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps({k:v for k,v in report.items() if k!='composition_checks'}))
