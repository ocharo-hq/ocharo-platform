import { describe, expect, it } from 'vitest';
import { catalogueMetadata, catalogueSlug, duplicateCatalogue, eligibleCataloguePdf, createCatalogue, cataloguePageProducts } from '@/domain/workspace/CatalogueDocuments';
import { validateSnapshot, validCatalogueSlug, type CatalogueMetadata, type DesignSnapshot } from '@/domain/workspace/EditorModels';
import { initialCommerce, type Product, type CommerceDraft } from '@/domain/workspace/Commerce';
const copy={status:'approved' as const,name:'Robe',fabric:'Silk',description:'A robe',decoration:null};
function product(id:string,approved=true):Product{return{document:{id,garmentType:'robe',variantCode:'v1',status:'active',sortOrder:0,sizes:['M'],fabricCodes:[],decorationCodes:[],tags:[],locales:{en:{...copy,status:approved?'approved':'draft'},ru:copy,uz:copy},pricing:null,media:[],issues:[],notes:null,stock:null,madeToOrder:false},revision:1,sourceRevision:null};}
const meta=():CatalogueMetadata=>({slug:'spring',description:'Spring collection',isActive:true,outputs:{web:true,pdf:false},web:{theme:'editorial',pages:[]}});
describe('single catalogue identity and publication eligibility',()=>{
 it('preserves legacy PDF templates and permits an empty web-only working draft',()=>{
  const web=createCatalogue('Spring','fr',meta(),'11111111-1111-4111-8111-111111111111');expect(web.locale).toBe('fr');expect(web.pages).toEqual([]);expect(validateSnapshot(web)).toEqual(web);
  const legacy={...web,pages:[{id:'cover',title:'Cover',width:210,height:297,background:'#ffffff',elements:[]}]} as DesignSnapshot;delete legacy.catalogue;
  expect(catalogueMetadata(legacy).outputs).toEqual({web:false,pdf:true});
 });
 it('duplicates as an inactive independent draft with a unique bounded slug',()=>{
  const source=createCatalogue('Spring','en',{...meta(),slug:'a'.repeat(100)},'11111111-1111-4111-8111-111111111111'),before=JSON.stringify(source);
  const result=duplicateCatalogue(source,'12345678');expect(result.catalogue?.slug).toHaveLength(100);expect(validCatalogueSlug(result.catalogue!.slug)).toBe(true);expect(result.catalogue?.isActive).toBe(false);expect(JSON.stringify(source)).toBe(before);
 });
 it('uses safe non-reserved slugs and enforces backend web page bounds',()=>{
  expect(catalogueSlug('Каталог')).toBe('collection');expect(catalogueSlug('Catalogue')).toBe('collection');expect(catalogueSlug('Summer Linen')).toBe('summer-linen');
  const source=createCatalogue('Spring','en',meta(),'11111111-1111-4111-8111-111111111111');source.catalogue!.web.pages=[{id:'page_1',title:'A page',layout:'grid',isActive:true,productIds:Array.from({length:1000},(_,i)=>`product-${i}`)}];expect(()=>validateSnapshot(source)).not.toThrow();
  source.catalogue!.web.pages[0]!.id='unsafe/page';expect(()=>validateSnapshot(source)).toThrow('web catalogue');source.catalogue!.web.pages[0]!.id='safe';source.catalogue!.slug='admin';expect(()=>validateSnapshot(source)).toThrow('metadata');
 });
 it('excludes a whole mixed PDF page while retaining a safe cover and eligible pages',()=>{
  const snapshot=createCatalogue('Spring','en',{...meta(),outputs:{web:true,pdf:true}},'11111111-1111-4111-8111-111111111111');
  snapshot.pages=['cover','safe','mixed','hidden'].map(id=>({id,title:id,width:210,height:297,background:'#ffffff',elements:[],...(id==='cover'?{}:{productIds:id==='mixed'?['ready','draft']:['ready']}),...(id==='hidden'?{isActive:false}:{})}));
  const draft:CommerceDraft={brandId:'11111111-1111-4111-8111-111111111111',revision:1,updatedAt:'2026-10-01',snapshot:{...initialCommerce('Ocharo','ocharo'),products:[product('ready'),product('draft',false)]}};
  const result=eligibleCataloguePdf(snapshot,[draft]);expect(result.snapshot.pages.map(page=>page.id)).toEqual(['cover','safe']);expect(result.excluded).toMatchObject([{pageId:'mixed',reason:'products',productIds:['draft']},{pageId:'hidden',reason:'inactive'}]);expect(snapshot.pages).toHaveLength(4);
  draft.snapshot.products[0]!.document.issues=[{id:'issue',field:'copy',status:'open',question:'Check'}];expect(eligibleCataloguePdf(snapshot,[draft]).snapshot.pages.map(page=>page.id)).toEqual(['cover']);
 });
 it.each(['fr','ar','zh-Hans'] as const)('creates a locale-safe product-free PDF cover for a new %s brand',locale=>{
  const metadata={...meta(),description:'Authored introduction',outputs:{web:true,pdf:true}};
  const snapshot=createCatalogue('Authored collection',locale,metadata,'11111111-1111-4111-8111-111111111111');
  expect(validateSnapshot(snapshot)).toEqual(snapshot);expect(snapshot.locale).toBe(locale);expect(snapshot.pages).toHaveLength(1);
  expect(snapshot.pages.flatMap(cataloguePageProducts)).toEqual([]);expect(snapshot.pages.flatMap(page=>page.elements.map(element=>element.text))).toEqual(['Authored collection','Authored introduction']);
  expect(snapshot.pages[0]?.elements.every(element=>element.direction===(locale==='ar'?'rtl':'ltr'))).toBe(true);
  expect(JSON.stringify(snapshot)).not.toContain('abaya-v01');expect(snapshot.pages[0]?.catalogueSource).toBeUndefined();
  expect(eligibleCataloguePdf(snapshot,[]).snapshot.pages).toHaveLength(1);
  expect(createCatalogue('Another',locale,metadata,'11111111-1111-4111-8111-111111111111').pages[0]?.id).not.toBe(snapshot.pages[0]?.id);
 });

 it('preserves a full gallery description without overflowing the shorter shared-copy contract',()=>{
  const description='x'.repeat(2000),snapshot=createCatalogue('Collection','fr',{...meta(),description,outputs:{web:false,pdf:true}},'11111111-1111-4111-8111-111111111111');
  expect(()=>validateSnapshot(snapshot)).not.toThrow();expect(snapshot.catalogue?.description).toBe(description);expect(snapshot.pages[0]?.elements[1]?.text).toBe(description);expect(snapshot.catalogueSettings?.copyByLocale.fr?.coverNote).toBe('');
 });

});
