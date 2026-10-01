import { describe, expect, it, vi } from 'vitest';
import { effectScope } from 'vue';
import { bindCatalogueElement, catalogueSettings, connectCatalogueProducts, localizeUnboundTemplate, randomizeCatalogue, refreshCatalogue, replaceCataloguePhoto } from '@/domain/workspace/CatalogueEditing';
import { composeCataloguePages, createCatalogueCover } from '@/domain/workspace/CatalogueComposer';
import { initialCommerce, type CommerceDraft, type Product } from '@/domain/workspace/Commerce';
import { cloneSnapshot, validateSnapshot, type DesignSnapshot } from '@/domain/workspace/EditorModels';
import { createCatalogueTemplate } from '@/domain/workspace/Templates';
import { useDesignDocument } from '@/application/workspace/DocumentStore';
import { useAccess } from '@/application/workspace/UseAccess';
import { layoutDesignText } from '@/integration/workspace/DesignTextLayout';
const org='11111111-1111-1111-1111-111111111111',brand='22222222-2222-2222-2222-222222222222';
const hashes=Array.from({length:6},(_,index)=>String(index+1).repeat(64));
function fixture():CommerceDraft {
  const snapshot=initialCommerce('Atelier','atelier');
  const copy={status:'approved' as const,name:'English garment',fabric:'Silk',decoration:'Embroidery',description:'English description'};
  const product:Product={document:{id:'abaya-v01',garmentType:'abaya',variantCode:'v01',status:'active',sortOrder:0,sizes:['M'],fabricCodes:[],decorationCodes:[],tags:[],locales:{en:copy,uz:{...copy,name:'O‘zbekcha libos'},ru:{...copy,name:'Русское платье'}},pricing:{currency:'USD',status:'confirmed',tiers:[{minimumQuantity:1,maximumQuantity:9,unitPrice:140},{minimumQuantity:10,maximumQuantity:null,unitPrice:120}],wording:null},media:hashes.map((sha256,index)=>({id:`media-${index}`,role:index<3?'model':'product',fileName:`image-${index}.png`,sha256,contentType:'image/png',byteLength:100,width:1000,height:1200,hasPreview:true,alt:null,sourcePath:null})),issues:[],notes:'private',stock:null,madeToOrder:false},revision:2,sourceRevision:null,translations:{fr:{...copy,name:'Vêtement français',fabric:'Soie'}},mediaGroups:[{id:'shoot',label:'Model shoot',category:'model',mediaIds:['media-0','media-1','media-2']},{id:'cutout',label:'Product',category:'product',mediaIds:['media-3','media-4','media-5']}]};
  snapshot.products=[product];return{brandId:brand,revision:4,snapshot,updatedAt:'2026-10-01'};
}
function design(draft=fixture()):DesignSnapshot {
  const snapshot=createCatalogueCover('en');snapshot.pages.push(...composeCataloguePages(org,draft,[{productId:'abaya-v01',imageHashes:hashes.slice(0,2)}],'product','en'));return snapshot;
}
describe('catalogue content and product integration',()=>{
 it('changes catalogue language without changing source data or page geometry',()=>{
  const draft=fixture(),before=JSON.stringify(draft),original=design(draft),copy=refreshCatalogue(original,[draft],'fr');
  expect(copy.snapshot.locale).toBe('fr');expect(copy.snapshot.pages[1]?.elements.find(item=>item.role==='title')?.text).toBe('Vêtement français');
  expect(copy.snapshot.pages[1]?.elements.map(({id,x,y,width,height})=>({id,x,y,width,height}))).toEqual(original.pages[1]?.elements.map(({id,x,y,width,height})=>({id,x,y,width,height})));
  expect(original.locale).toBe('en');expect(JSON.stringify(draft)).toBe(before);expect(copy.warnings).toContain('Complete the shared fr catalogue text.');
 });
 it('keeps absent translations visibly empty instead of silently using English',()=>{
  const result=refreshCatalogue(design(),[fixture()],'ja');
  expect(result.snapshot.pages[1]?.elements.find(item=>item.role==='title')?.text).toBe('');
  expect(result.warnings.some(item=>item.includes('Missing ja name'))).toBe(true);
 });
 it('refreshes canonical price amounts while preserving edition display choices',()=>{
  const draft=fixture(),snapshot=design(draft);draft.snapshot.products[0]!.document.pricing!.tiers[0]!.unitPrice=190;
  snapshot.catalogueSettings!.priceDisplay.showWholesale=false;snapshot.catalogueSettings!.priceDisplay.showCurrency=false;
  const result=refreshCatalogue(snapshot,[draft]).snapshot;
  const prices=result.pages[1]!.elements.filter(item=>item.role==='price');expect(prices[0]?.text).toBe('190');expect(prices[1]?.opacity).toBe(0);
  expect(result.pages[1]?.elements.filter(item=>item.binding?.kind==='product'&&item.binding.field==='price-label').map(item=>item.text).join()).not.toContain('USD');
  expect(snapshot.pages[1]?.elements.find(item=>item.role==='price')?.text).toBe('140');
 });
 it('randomizes eligible group images per page without changing frames or another product',()=>{
  const draft=fixture(),snapshot=design(draft);snapshot.catalogueSettings!.mediaGroup='product';
  const originalFrames=snapshot.pages[1]!.elements.filter(item=>item.role==='photo');originalFrames[0]!.cropX=12;originalFrames[0]!.rotation=8;
  const result=randomizeCatalogue(snapshot,[draft],org,snapshot.pages[1]!.id,()=>0).snapshot;
  const changed=result.pages[1]!.elements.filter(item=>item.role==='photo');expect(new Set(changed.map(item=>item.src)).size).toBe(2);
  for(let index=0;index<changed.length;index++){expect(changed[index]).toMatchObject({x:originalFrames[index]!.x,y:originalFrames[index]!.y,width:originalFrames[index]!.width,height:originalFrames[index]!.height,cropX:originalFrames[index]!.cropX});expect(changed[index]!.rotation).toBe(originalFrames[index]!.rotation);expect(hashes.slice(3)).toContain(changed[index]!.src?.split('/').at(-1));}
  expect(result.pages[0]).toEqual(snapshot.pages[0]);
 });
 it('retains pictures when a product lacks the selected group and rejects cross-product picks',()=>{
  const draft=fixture(),snapshot=design(draft);snapshot.catalogueSettings!.mediaGroup='fabric';
  const result=randomizeCatalogue(snapshot,[draft],org);expect(result.snapshot.pages[1]?.elements).toEqual(snapshot.pages[1]?.elements);expect(result.warnings[0]).toContain('No fabric images');
  const frame=snapshot.pages[1]!.elements.find(item=>item.role==='photo')!;
  expect(()=>replaceCataloguePhoto(snapshot,snapshot.pages[1]!.id,frame.id,draft.snapshot.products[0]!.document.media[0]!,[draft],org)).toThrow('this product and media group');
 });
 it('adopts verified v22 fields into a separate copy and leaves custom frames unlinked',()=>{
  const snapshot=createCatalogueTemplate('en'),before=JSON.stringify(snapshot),draft=fixture();snapshot.pages[1]!.elements.push({id:'custom',type:'text',label:'Custom text',text:'Private edition note',x:1,y:1,width:40,height:10});
  const original=JSON.stringify(snapshot),result=connectCatalogueProducts(snapshot,draft);
  expect(JSON.stringify(snapshot)).toBe(original);expect(before).not.toBe(original);
  const page=result.snapshot.pages[1]!;expect(page.elements.find(item=>item.id==='p02-e022')).toMatchObject({text:'140',binding:{kind:'product',field:'price',index:0,productId:'abaya-v01'}});
  expect(page.elements.find(item=>item.id==='p02-e004')?.text).toBe('English garment');
  expect(page.elements.find(item=>item.id==='p02-e007')?.productId).toBe('abaya-v01');
  expect(page.elements.find(item=>item.id==='custom')?.binding).toBeUndefined();
  expect(()=>validateSnapshot(result.snapshot)).not.toThrow();
 });
 it('supports manual multi-product bindings and preserves unrelated hand-authored translations',()=>{
  const snapshot=createCatalogueTemplate('en'),draft=fixture();snapshot.catalogueSettings=catalogueSettings();snapshot.catalogueSettings.brandId=brand;
  const page=snapshot.pages[1]!,title=page.elements.find(item=>item.id==='p02-e004')!;title.text='Custom edition heading';
  const translated=localizeUnboundTemplate(snapshot,'ru');expect(translated.snapshot.pages[1]?.elements.find(item=>item.id===title.id)?.text).toBe('Custom edition heading');expect(translated.warnings.some(item=>item.includes(title.label))).toBe(true);
  const bound=bindCatalogueElement(snapshot,page.id,title.id,{kind:'product',field:'name',productId:'abaya-v01'});
  expect(refreshCatalogue(bound,[draft],'uz').snapshot.pages[1]?.elements.find(item=>item.id===title.id)?.text).toBe('O‘zbekcha libos');
 });
 it('undoes a whole-catalogue randomization as one operation',()=>{
  const scope=effectScope();scope.run(()=>{
   const access=useAccess({assign:vi.fn(),reload:vi.fn()}),store=useDesignDocument(access,org,'catalogue'),snapshot=design();
   store.create(snapshot,'Test');store.replace(randomizeCatalogue(snapshot,[fixture()],org,undefined,()=>0).snapshot);
   expect(store.history.value).toHaveLength(1);store.undo();expect(store.snapshot.value).toEqual(snapshot);store.redo();expect(store.snapshot.value).not.toEqual(snapshot);
  });scope.stop();
 });
 it('validates language records, binding fields and media categories',()=>{
  const snapshot=design();const invalid=cloneSnapshot(snapshot);invalid.catalogueSettings!.mediaGroup='invalid' as never;expect(()=>validateSnapshot(invalid)).toThrow('settings');
  const broken=cloneSnapshot(snapshot);broken.pages[1]!.elements.find(item=>item.role==='price')!.binding={kind:'product',field:'price'};expect(()=>validateSnapshot(broken)).toThrow('binding');
 });
 it('sets document RTL without changing saved LTR editions',()=>{
  const snapshot=design(),result=refreshCatalogue(snapshot,[fixture()],'ar').snapshot;
  expect(result.pages.flatMap(page=>page.elements).filter(item=>item.type==='text').every(item=>item.direction==='rtl')).toBe(true);
  expect(snapshot.pages[1]?.elements.find(item=>item.role==='title')?.direction).toBe('ltr');
 });
 it('wraps CJK without adding spaces and anchors RTL consistently',()=>{
  const context={measureText:(text:string)=>({width:[...text].length*2,fontBoundingBoxAscent:3,fontBoundingBoxDescent:1})} as CanvasRenderingContext2D;
  const element={id:'text',type:'text' as const,label:'CJK',x:0,y:0,width:6,height:30,text:'丝绸传统设计',fontSize:4,direction:'rtl' as const};
  const result=layoutDesignText(context,element,1);expect(result.lines).toEqual(['丝绸传','统设计']);expect(result.anchor).toBe(6);expect(context.direction).toBe('rtl');
 });
});
