from pathlib import Path
import base64, hashlib, json, re, xml.etree.ElementTree as ET
from PIL import Image

root=Path(__file__).parent
spec=json.loads((root/'export-spec.json').read_text())
master=(root/'source/approved-master.png').read_bytes()
expected=hashlib.sha256(master).hexdigest()
assert expected==spec['sourceHash']
ns={'s':'http://www.w3.org/2000/svg'}
for item in spec['specs']:
    svg=ET.parse(root/'assets'/(item['name']+'.svg')).getroot()
    for image in svg.findall('.//s:image',ns):
        data=base64.b64decode(image.attrib['href'].split(',',1)[1])
        assert hashlib.sha256(data).hexdigest()==expected,item['name']
    body=svg.find('.//s:mask[@id="whole-master"]',ns)
    assert len(body)==1 and body[0].attrib=={'href':'#original-pixels'}
    if item['kind']=='wordmark':
        rects=svg.findall('s:rect',ns)
        assert len(rects)==1 and rects[0].attrib['mask']=='url(#whole-master)'
        assert 'transform' not in rects[0].attrib
    else:
        for clip_id,value in [('needle-clip','needleClip'),('oval-clip','ovalClip'),('eye-clip','eyeClip')]:
            assert svg.find('.//s:clipPath[@id="'+clip_id+'"]/s:path',ns).attrib['d']==spec[value]
def alpha(file):
    with Image.open(root/'assets'/file) as image:return image.getchannel('A').tobytes()
assert alpha('ocharo-wordmark-ink.png')==alpha('ocharo-wordmark-reverse.png')
assert alpha('ocharo-symbol-ink.png')==alpha('ocharo-micro.png')==alpha('ocharo-micro-reverse.png')
for size in (16,24,32):
    for color in ('ink','reverse'):
        with Image.open(root/'assets'/f'ocharo-favicon-{size}-{color}.png') as image:assert image.size==(size,size)
    assert alpha(f'ocharo-favicon-{size}-ink.png')==alpha(f'ocharo-favicon-{size}-reverse.png')
# Analyze original dark pixels without changing source images.
numbers=list(map(float,re.findall(r'-?\d+(?:\.\d+)?',spec['needleClip'])))
polygon=list(zip(numbers[::2],numbers[1::2]))
def contains(x,y):
    inside=False
    for (ax,ay),(bx,by) in zip(polygon,polygon[1:]+polygon[:1]):
        if (ay>y)!=(by>y) and x<(bx-ax)*(y-ay)/(by-ay)+ax:inside=not inside
    return inside
original=Image.open(root/'source/approved-master.png').convert('RGB')
counts={}
for name,bounds in [('head',(700,337,839,506)),('lower_shaft',(660,600,715,660))]:
    count=0
    for y in range(bounds[1],bounds[3]):
        for x in range(bounds[0],bounds[2]):
            if max(original.getpixel((x,y)))<100:
                assert contains(x+.5,y+.5),(name,x,y)
                count+=1
    counts[name]=count
report={'approved_source_sha256':expected,'all_eight_embed_original':True,'wordmark_has_no_separate_needle_transform':True,'light_dark_alpha_identical':True,'symbol_small_icon_alpha_identical':True,'original_dark_pixels_inside_crop':counts,'favicon_sizes':[16,24,32],'junction_limit':'Compact crop separates the existing c/h junction; no independent original needle exists at that join.'}
(root/'verification.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report))
