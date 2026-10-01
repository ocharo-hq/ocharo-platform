import { describe, expect, it } from 'vitest';
import { appendCataloguePages, composeCataloguePages, createCatalogueCover, removeCataloguePage } from '@/domain/workspace/CatalogueComposer';
import { isSafeDesignSource, validateSnapshot } from '@/domain/workspace/EditorModels';
import { setCataloguePrice } from '@/domain/workspace/CatalogueLayout';
import { initialCommerce, type CommerceDraft, type Product } from '@/domain/workspace/Commerce';
const org = '11111111-1111-1111-1111-111111111111', brand = '22222222-2222-2222-2222-222222222222';
const hashes = Array.from({ length: 6 }, (_, index) => String(index).repeat(64));
function fixture(): CommerceDraft {
  const snapshot = initialCommerce('Synthetic atelier', 'synthetic');
  const copy = {status:'approved' as const,name:'Review garment',fabric:'Silk',decoration:'Embroidery',description:'A synthetic product for verification.'};
  const document: Product['document'] = {id:'sample-1',garmentType:'vest',variantCode:'V1',status:'active',sortOrder:0,sizes:['M'],fabricCodes:[],decorationCodes:[],tags:[],locales:{en:copy,ru:copy,uz:copy},pricing:{currency:'USD',status:'confirmed',tiers:[{minimumQuantity:1,maximumQuantity:null,unitPrice:125.75}],wording:null},media:hashes.map((sha256,index)=>({id:`image-${index}`,role:'photo',fileName:`image-${index}.png`,sha256,contentType:'image/png',byteLength:100,width:500,height:500,hasPreview:true,alt:null,sourcePath:null})),issues:[],notes:'PRIVATE',stock:null,madeToOrder:false};
  snapshot.products.push({document,revision:7,sourceRevision:null});
  return {brandId:brand,revision:12,snapshot,updatedAt:'2026-09-29'};
}
describe('canonical catalogue editions', () => {
  it.each(['product','continuation','collage'] as const)('builds a portable %s page with immutable provenance', layout => {
    const draft = fixture(), selection = {productId:'sample-1',imageHashes:hashes.slice(0, layout==='collage'?6:2)};
    const pages = composeCataloguePages(org,draft,[selection],layout,'en');
    const design = appendCataloguePages(createCatalogueCover('en'),pages);
    expect(validateSnapshot(JSON.parse(JSON.stringify(design)))).toEqual(design);
    expect(pages[0]?.catalogueSource).toMatchObject({brandId:brand,revision:12,productRevision:7,productId:'sample-1',layout,currency:'USD'});
    expect(design.pages[1]?.elements.find(item=>item.role==='page-number')?.text).toBe('02');
    expect(JSON.stringify(pages)).not.toContain('PRIVATE');
    draft.snapshot.products[0]!.document.locales.en.name='Changed upstream';
    expect(pages[0]?.elements.find(item=>item.role==='title')?.text).toBe('Review garment');
    expect(pages[0]?.elements.filter(item=>item.role==='photo')).toHaveLength(layout==='collage'?6:2);
  });
  it('continues excess price tiers onto extra pages without silently dropping amounts', () => {
    const draft=fixture(); draft.snapshot.products[0]!.document.pricing!.tiers=Array.from({length:19},(_,index)=>({minimumQuantity:index+1,maximumQuantity:index+1,unitPrice:100+index}));
    const pages=composeCataloguePages(org,draft,[{productId:'sample-1',imageHashes:[hashes[0]!]}],'product','en');
    expect(pages).toHaveLength(3);
    const prices=pages.flatMap(page=>page.elements.filter(element=>element.role==='price'));
    expect(prices.map(price=>Number(price.text))).toEqual(Array.from({length:19},(_,index)=>100+index));
    expect(prices.every(price=>price.priceLabel?.includes('pieces'))).toBe(true);
    expect(()=>validateSnapshot(appendCataloguePages(createCatalogueCover('en'),pages))).not.toThrow();
  });
  it('rejects missing, archived, duplicate or unavailable image selections', () => {
    for (const chosen of [[],[hashes[0]!,hashes[0]!],['a'.repeat(64)]]) {
      expect(()=>composeCataloguePages(org,fixture(),[{productId:'sample-1',imageHashes:chosen}],'product','en')).toThrow('Choose one to 2');
    }
    const draft = fixture(); draft.snapshot.products[0]!.document.status='archived';
    expect(()=>composeCataloguePages(org,draft,[{productId:'sample-1',imageHashes:[hashes[0]!]}],'product','en')).toThrow('no longer available');
  });
  it('keeps edition prices separate from canonical prices and keeps price frames bounded', () => {
    const draft=fixture(), pages=composeCataloguePages(org,draft,[{productId:'sample-1',imageHashes:[hashes[0]!]}],'product','en');
    const design=appendCataloguePages(createCatalogueCover('en'),pages), page=design.pages[1]!, price=page.elements.find(item=>item.role==='price')!;
    const changed=setCataloguePrice(design,page.id,price.id,142.99);
    expect(changed.pages[1]?.elements.find(item=>item.id===price.id)).toMatchObject({text:'142.99',width:price.width});
    expect(draft.snapshot.products[0]?.document.pricing?.tiers[0]?.unitPrice).toBe(125.75);
  });
  it('inserts after the selected page, removes safely and preserves independent page identities', () => {
    const draft=fixture(), selected=[{productId:'sample-1',imageHashes:[hashes[0]!]}];
    const first=appendCataloguePages(createCatalogueCover('en'),composeCataloguePages(org,draft,selected,'product','en'));
    const inserted=composeCataloguePages(org,draft,selected,'collage','en');
    const next=appendCataloguePages(first,inserted,first.pages[0]!.id);
    expect(next.pages.map(page=>page.id)).toEqual([first.pages[0]!.id,inserted[0]!.id,first.pages[1]!.id]);
    expect(new Set(next.pages.map(page=>page.id)).size).toBe(3);
    expect(removeCataloguePage(next,inserted[0]!.id).pages).toHaveLength(2);
    expect(()=>removeCataloguePage(createCatalogueCover('en'),'missing')).toThrow('at least one');
  });
  it('accepts only exact protected commerce image paths and bounded source metadata', () => {
    const path=`/api/organizations/${org}/brands/${brand}/commerce/media/${hashes[0]}`;
    expect(isSafeDesignSource(path)).toBe(true);
    for(const bad of [path+'?download=1','https://example.test'+path,path.replace('/media/','/media/../'),path.replace(hashes[0]!,'not-a-hash')]) expect(isSafeDesignSource(bad)).toBe(false);
    const design=appendCataloguePages(createCatalogueCover('en'),composeCataloguePages(org,fixture(),[{productId:'sample-1',imageHashes:[hashes[0]!]}],'product','en'));
    design.pages[1]!.catalogueSource!.revision=-1;
    expect(()=>validateSnapshot(design)).toThrow('catalogue source');
  });
});
