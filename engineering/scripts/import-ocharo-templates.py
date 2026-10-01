"""Import explicitly selected Ocharo v22/card-v5 sources into portable editor seeds.

Usage: python import-ocharo-templates.py --source-root /path/to/mft-10x-ws
Reads originals; writes only this product's template directory and generated JSON seeds.
Requires Pillow, lxml and reportlab. Never executes the historical source builders.
"""

from __future__ import annotations

import argparse
import base64
import hashlib
import json
import re
from pathlib import Path

from lxml import etree, html
from PIL import Image
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont


PT_MM = 25.4 / 72
CATALOGUE_WIDTH = 594.96 * PT_MM
CATALOGUE_HEIGHT = 841.92 * PT_MM


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-root', type=Path, required=True)
    args = parser.parse_args()
    source = args.source_root.resolve()
    portfolio = source / 'context/domains/external/work/career/designer/portfolio'
    catalogue = portfolio / 'catalogue'
    repo = Path(__file__).resolve().parents[2]
    app = repo / 'engineering/codebase/brand-workspace.frontend-services/apps/web'
    public = app / 'public/templates/ocharo'
    seeds = app / 'src/domain/workspace/templates'
    public.mkdir(parents=True, exist_ok=True)
    seeds.mkdir(parents=True, exist_ok=True)
    for name, file in [('body', 'och-body.ttf'), ('bold', 'och-bold.ttf'), ('serif', 'och-didot.ttf'), ('photo', 'och-photo.ttf')]:
        pdfmetrics.registerFont(TTFont(name, str(catalogue / 'fonts' / file)))
    records: dict[str, dict] = {}
    imports: dict[tuple[str, bool], str] = {}

    def imported(path: Path, crop_alpha: bool = False) -> str:
        path = path.resolve()
        relative = path.relative_to(source).as_posix()
        key = (relative, crop_alpha)
        if key in imports:
            return imports[key]
        original = path.read_bytes()
        digest = hashlib.sha256(original).hexdigest()
        image = Image.open(path)
        crop = image.getchannel('A').getbbox() if crop_alpha and image.mode == 'RGBA' else None
        if crop:
            image = image.crop(crop)
        image.thumbnail((1800, 1800), Image.Resampling.LANCZOS)
        transparent = image.mode == 'RGBA'
        suffix = '.png' if transparent else '.jpg'
        file = f'{path.stem}-{digest[:10]}{"-trim" if crop else ""}{suffix}'.lower()
        output = public / file
        if transparent:
            image.save(output, optimize=True)
        else:
            image.convert('RGB').save(output, quality=91, subsampling=0, optimize=True)
        url = '/templates/ocharo/' + file
        record = {'url': url, 'source': relative, 'sourceSha256': digest,
                  'derivativeSha256': hashlib.sha256(output.read_bytes()).hexdigest(),
                  'width': image.width, 'height': image.height, 'alphaCrop': crop,
                  'provenance': ('source-photo' if '/media/' in relative or 'portrait-original' in relative
                                 else 'generated-or-authored-reference')}
        records[url] = record
        imports[key] = url
        return url

    def svg_asset(markup: str) -> str:
        digest = hashlib.sha256(markup.encode()).hexdigest()
        file = f'artwork-{digest[:16]}.svg'
        (public / file).write_text(markup)
        url = '/templates/ocharo/' + file
        records[url] = {'url': url, 'derivativeSha256': digest, 'provenance': 'source-template-vector'}
        return url

    def css(node) -> dict[str, str]:
        return dict(part.split(':', 1) for part in node.get('style', '').split(';') if ':' in part)

    def length(value: str | None, reference: float, default: float = 0) -> float:
        if value is None:
            return default
        if value.endswith('cqw'):
            return float(value[:-3]) * CATALOGUE_WIDTH / 100
        if value.endswith('%'):
            return float(value[:-1]) * reference / 100
        return float(re.match(r'[-\d.]+', value)[0])

    def text_size(text: str, font: str, size_mm: float) -> float:
        return pdfmetrics.stringWidth(text, font, size_mm / PT_MM) * PT_MM + .24

    def color(value: str) -> tuple[str, float]:
        if value.startswith('rgba('):
            nums = [float(v) for v in value[5:-1].split(',')]
            return '#' + ''.join(f'{round(v):02x}' for v in nums[:3]), nums[3]
        return value, 1

    page_names = ['Cover', 'Abaya', 'Baska 01–04', 'Baska 05–06', 'Korset', 'Kostyum',
                  'Nimcha 01–04', 'Nimcha 05–10', 'Nimcha 11–16', 'Photography · look 05',
                  'Photography · look 03', 'Photography · look 04', 'Photography · look 06',
                  'The designer', 'Collection overview', 'Contacts']
    all_products: dict[str, dict] = {}
    for locale in ['uz', 'ru', 'en']:
        source_html = catalogue / f'html/car-ocharo-journal-v22-{locale}.html'
        root = html.parse(str(source_html)).getroot()
        pages = []
        for page_index, section in enumerate(root.xpath('//section'), 1):
            page = {'id': f'catalogue-{page_index:02}', 'title': page_names[page_index-1],
                    'width': CATALOGUE_WIDTH, 'height': CATALOGUE_HEIGHT,
                    'background': '#F6F0E5', 'elements': [], 'productIds': []}
            product = None
            price_index = 0
            for index, node in enumerate(section):
                style = css(node)
                if node.tag == 'a':
                    continue
                x = length(style.get('left'), CATALOGUE_WIDTH)
                y = length(style.get('top'), CATALOGUE_HEIGHT)
                w = length(style.get('width'), CATALOGUE_WIDTH, CATALOGUE_WIDTH)
                h = length(style.get('height'), CATALOGUE_HEIGHT, CATALOGUE_HEIGHT)
                element = {'id': f'p{page_index:02}-e{index:03}', 'type': 'shape',
                           'label': 'Background detail', 'x': x, 'y': y, 'width': max(.1, w), 'height': max(.1, h)}
                if 'inset' in style:
                    element.update(x=0, y=0, width=CATALOGUE_WIDTH, height=CATALOGUE_HEIGHT)
                radius = style.get('border-radius')
                if radius:
                    element['radius'] = length(radius, min(w, h))
                nested_images = node.xpath('./img')
                if nested_images:
                    image_node = nested_images[0]
                    path = (source_html.parent / image_node.get('src')).resolve()
                    image_css = css(image_node)
                    dw = length(image_css['width'], w)
                    dh = length(image_css['height'], h)
                    dx = length(image_css['left'], w)
                    dy = length(image_css['top'], h)
                    iw, ih = Image.open(path).size
                    contain = dw < w - .01 or dh < h - .01
                    base = min(w / iw, h / ih) if contain else max(w / iw, h / ih)
                    zoom = dw / iw / base
                    crop_x = dx / (w-dw) * 100 if abs(w-dw) > .00001 else 50
                    crop_y = dy / (h-dh) * 100 if abs(h-dh) > .00001 else 50
                    element.update(type='image', label=path.stem, src=imported(path), role='photo',
                                   fit='contain' if contain else 'cover', zoom=max(1, zoom),
                                   cropX=min(100, max(0, crop_x)), cropY=min(100, max(0, crop_y)))
                    if '/models/' in path.as_posix():
                        product = path.parent.parent.name
                        page['productIds'].append(product)
                        price_index = 0
                        record = json.loads((path.parent.parent / f'och-{product}.json').read_text())
                        all_products[product] = {'id': product, 'locales': record['locales'], 'sizes': record['sizes'],
                                                 'pricing': record['pricing'], 'image': element['src']}
                        element['label'] = product + ' · photo'
                elif node.tag == 'svg':
                    source_images = node.xpath('./image')
                    if source_images:
                        path = (source_html.parent / source_images[0].get('href')).resolve()
                        element.update(type='image', label='Ocharo wordmark', src=imported(path, True), fit='contain', role='logo')
                    else:
                        svg = etree.Element('svg', nsmap={None: 'http://www.w3.org/2000/svg'})
                        for attr in ['viewBox', 'viewbox', 'fill', 'stroke', 'stroke-width', 'stroke-linecap', 'stroke-linejoin']:
                            if node.get(attr):
                                svg.set('viewBox' if attr.lower() == 'viewbox' else attr, node.get(attr))
                        viewbox = svg.get('viewBox', '0 0 24 24').split()
                        svg.set('width', viewbox[2])
                        svg.set('height', viewbox[3])
                        svg.set('style', 'color:' + style.get('color', '#847562'))
                        for child in node:
                            svg.append(etree.fromstring(etree.tostring(child)))
                        element.update(type='image', label='Specification icon', src=svg_asset(etree.tostring(svg).decode()), fit='contain', role='decoration')
                elif 'text' in node.get('class', '').split():
                    value = ''.join(node.itertext())
                    font_raw = style['font'].split(' ')
                    size = length(font_raw[0], CATALOGUE_WIDTH)
                    font_name = font_raw[-1]
                    font = 'photo' if font_name == 'OcharoPhoto' else 'serif' if font_name in ['OcharoDidot', 'OcharoTimes'] else 'bold' if font_name == 'OcharoBold' else 'body'
                    width = text_size(value, font, size)
                    element.update(type='text', label=value[:70], text=value, width=max(.1, width), height=size*1.15,
                                   fontFamily='serif' if font in ['serif', 'photo'] else 'sans',
                                   fontSize=size, fontWeight='bold' if font in ['bold', 'photo'] else 'normal',
                                   color=style.get('color', '#302B25'), lineHeight=1, role='caption')
                    if not product and re.fullmatch(r'\d{2}', value) and x > CATALOGUE_WIDTH*.9 and y > CATALOGUE_HEIGHT*.9:
                        element.update(role='page-number', label='Page number')
                    if product and re.match(r'^\d{2,3}( \d{3})+$', value):
                        element.update(role='price', tier='single' if price_index == 0 else 'bulk', label=product + (' · 1 piece' if price_index == 0 else ' · 10+ pieces'))
                        price_index += 1
                else:
                    background = style.get('background', '#F6F0E5')
                    if background.startswith('linear-gradient'):
                        rgba = re.search(r'rgba\(([^)]+)\)', background).group(1)
                        shade, alpha = color('rgba(' + rgba + ')')
                        svg = f'<svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{h}" viewBox="0 0 {w} {h}" preserveAspectRatio="none"><defs><linearGradient id="fade" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="{shade}" stop-opacity="0"/><stop offset="1" stop-color="{shade}" stop-opacity="{alpha}"/></linearGradient></defs><rect width="{w}" height="{h}" fill="url(#fade)"/></svg>'
                        element.update(type='image', label='Cover gradient', src=svg_asset(svg), fit='cover', role='decoration', locked=True)
                    else:
                        fill, opacity = color(background)
                        element.update(background=fill, opacity=opacity, role='decoration')
                        border = style.get('border')
                        if border and not border.startswith('0cqw'):
                            b = border.split(' ')
                            element.update(borderWidth=length(b[0], CATALOGUE_WIDTH), borderColor=b[-1])
                    if index == 0 and w > CATALOGUE_WIDTH-.1 and h > CATALOGUE_HEIGHT-.1:
                        continue
                if product:
                    element.update(productId=product, groupId=product)
                page['elements'].append({k: round(v, 6) if isinstance(v, float) else v for k, v in element.items()})
            pages.append(page)
        snapshot = {'schemaVersion': 1, 'kind': 'catalogue', 'templateId': 'ocharo-journal-v22', 'locale': locale, 'pages': pages}
        assert len(pages) == 16
        assert sum(len(p['productIds']) for p in pages) == 29
        assert all(len(p['elements']) <= 300 for p in pages)
        packed = json.dumps(snapshot, ensure_ascii=False, separators=(',', ':'))
        assert len(packed.encode()) < 1024*1024
        (seeds / f'catalogue-{locale}.json').write_text(packed+'\n')

    card_spec = json.loads((source/'output/pdf/och-business-cards-v5/och-card-manifest-v5.json').read_text())
    background = imported(portfolio/'brand/backgrounds/och-silhouettes-landscape-soft-blur-v1.png')
    white = imported(portfolio/'brand/logo/och-logo-white-transparent-v1.png', True)
    black = imported(portfolio/'brand/logo/och-logo-black-transparent-v1.png', True)
    # Preserve the exact approved alpha silhouette; these are editor color options,
    # not newly approved historical brand assets. Embedded masks remain exportable.
    logo_image = records[white]
    data_uri = 'data:image/png;base64,' + base64.b64encode((public / white.rsplit('/', 1)[-1]).read_bytes()).decode()
    logo_colors = {}
    for name, ink in [('ivory', '#F6F0E5'), ('charcoal', '#302B25'), ('olive', '#5D6654'), ('clay', '#AA7560')]:
        lw, lh = logo_image['width'], logo_image['height']
        markup = f'<svg xmlns="http://www.w3.org/2000/svg" width="{lw}" height="{lh}" viewBox="0 0 {lw} {lh}"><defs><mask id="wordmark" maskUnits="userSpaceOnUse" x="0" y="0" width="{lw}" height="{lh}" style="mask-type:alpha"><image width="{lw}" height="{lh}" href="{data_uri}"/></mask></defs><rect width="{lw}" height="{lh}" fill="{ink}" mask="url(#wordmark)"/></svg>'
        url = svg_asset(markup)
        records[url].update(provenance='editor-color-variant', source=logo_image['source'], sourceSha256=logo_image['sourceSha256'], color=ink, width=lw, height=lh)
        logo_colors[name] = url
    logo_ratio = records[white]['height']/records[white]['width']
    contact_paths = [
        '<path d="M2 0H6C8 0 8 1 8 3V6C8 8 7 8 5 8H2C0 8 0 7 0 5V2C0 0 1 0 2 0Z" stroke-width=".65"/><circle cx="4" cy="4" r="1.9" stroke-width=".6"/><circle cx="6.4" cy="1.7" r=".45" fill="#302B25" stroke="none"/>',
        '<path d="M0 3 9 0 6 9 4 5 0 3M4 5 9 0"/>',
        '<path d="M1 0H3L4 3 2.5 4C4 6 4.5 6.5 6 7L7 5.5 9 7V9C4 9 0 4 1 0Z"/>',
    ]
    icons = [svg_asset('<svg xmlns="http://www.w3.org/2000/svg" width="10" height="10" viewBox="-.5 -.5 10 10" fill="none" stroke="#302B25" stroke-width=".5">'+path+'</svg>') for path in contact_paths]
    def text(id, value, x, baseline, size, font='body', color='#302B25'):
        return {'id': id, 'type':'text', 'label':id.replace('-', ' ').title(), 'x':x*PT_MM,'y':(baseline-size)*PT_MM,
                'width':text_size(value,font,size*PT_MM),'height':size*PT_MM*1.2,'text':value,'fontFamily':'serif' if font=='serif' else 'sans',
                'fontSize':size*PT_MM,'fontWeight':'normal','lineHeight':1,'color':color,'role':'caption'}
    card_width = 85 / PT_MM
    for locale in ['uz','ru','en']:
        copy = card_spec['copy'][locale]
        descriptor=text('front-descriptor',copy['tagline'],0,115,8.3,color='#FFFFFF')
        descriptor['x']=(85-descriptor['width'])/2
        front={'id':'card-front','title':'Front','width':85,'height':55,'background':'#302B25','elements':[
            {'id':'front-photo','type':'image','label':'Soft mannequin background','x':0,'y':0,'width':85,'height':55,'src':background,'fit':'cover','cropX':50,'cropY':50,'zoom':1,'role':'photo'},
            {'id':'front-tint','type':'shape','label':'Charcoal overlay','x':0,'y':0,'width':85,'height':55,'background':'#000000','opacity':.52,'role':'decoration'},
            {'id':'front-logo','type':'image','label':'Ocharo wordmark','x':(85-151*PT_MM)/2,'y':48*PT_MM,'width':151*PT_MM,'height':151*PT_MM*logo_ratio,'src':white,'fit':'contain','role':'logo'},descriptor]}
        back={'id':'card-back','title':'Back','width':85,'height':55,'background':'#F6F0E5','elements':[
            text('designer-name',copy['name'],17,33,14,'serif'),
            text('designer-role',copy['role'],17,49,8.3,color='#6C5D4B'),
            {'id':'back-divider','type':'shape','label':'Divider','x':17*PT_MM,'y':63*PT_MM,'width':85-34*PT_MM,'height':.5*PT_MM,'background':'#DED0BC','role':'decoration'},
            text('designer-location',copy['city'],17,55/PT_MM-15,8,color='#6C5D4B')]}
        for i,contact in enumerate(card_spec['contacts']):
            y=84+i*17
            back['elements'].append({'id':f'contact-icon-{i}','type':'image','label':contact['label']+' icon','x':16.5*PT_MM,'y':(y-7.5)*PT_MM,'width':10*PT_MM,'height':10*PT_MM,'src':icons[i],'fit':'contain','role':'decoration'})
            back['elements'].append(text('contact-'+str(i),contact['value'],32,y,8.3))
        (seeds/f'card-{locale}.json').write_text(json.dumps({'schemaVersion':1,'kind':'business-card','templateId':'ocharo-charcoal-v5','locale':locale,'pages':[front,back]},ensure_ascii=False,separators=(',',':'))+'\n')
    (seeds/'products.json').write_text(json.dumps(list(all_products.values()),ensure_ascii=False,separators=(',',':'))+'\n')
    (seeds/'brand-assets.json').write_text(json.dumps({'whiteLogo':white,'blackLogo':black,'cardBackground':background,'logoColors':logo_colors},indent=2)+'\n')
    manifest={'schemaVersion':1,'templateIds':['ocharo-journal-v22','ocharo-charcoal-v5'],'sourcesRemainCanonical':True,
              'sources':{'catalogue':'catalogue/studies/car-build-journal-v22.py','card':'output/pdf/och-business-cards-v5/och-card-manifest-v5.json'},
              'assets':list(records.values())}
    (public/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps({'assets':len(records),'pagesPerCatalogue':16,'products':len(all_products),'output':str(public)}))


if __name__ == '__main__':
    main()
