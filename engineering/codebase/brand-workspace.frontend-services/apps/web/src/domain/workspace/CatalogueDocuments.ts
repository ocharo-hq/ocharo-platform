import { catalogueSettings, defaultCatalogueCopy } from './CatalogueEditing';
import { catalogueLocales, productEligibleForCatalogue, type CatalogueLocale, type CommerceDraft } from './Commerce';
import { cloneSnapshot, validCatalogueSlug, type CatalogueMetadata, type DesignPage, type DesignSnapshot } from './EditorModels';

export function catalogueMetadata(snapshot: DesignSnapshot): CatalogueMetadata {
  return snapshot.catalogue ? JSON.parse(JSON.stringify(snapshot.catalogue)) as CatalogueMetadata : {slug:'catalogue',description:'',isActive:true,outputs:{web:false,pdf:true},web:{theme:'editorial',pages:[]}};
}
export function catalogueSlug(name:string):string {const candidate=name.toLowerCase().normalize('NFKD').replace(/[\u0300-\u036f]/g,'').replace(/[^a-z0-9]+/g,'-').replace(/^-|-$/g,'').slice(0,100).replace(/-+$/,'');return validCatalogueSlug(candidate)?candidate:'collection';}
/** A new catalogue contains only operator-authored title/description; garment pages are selected explicitly. */
export function createCatalogue(name:string,locale:CatalogueLocale,metadata:CatalogueMetadata,brandId:string):DesignSnapshot {
  const settings=catalogueSettings();settings.brandId=brandId;
  settings.copyByLocale[locale]={...defaultCatalogueCopy(locale),header:'',collection:name.trim(),coverNote:'',footer:''};
  const direction=catalogueLocales.find(item=>item.code===locale)!.dir;
  return {schemaVersion:1,kind:'catalogue',templateId:'ocharo-composer-v1',locale,
    catalogue:JSON.parse(JSON.stringify(metadata)) as CatalogueMetadata,catalogueSettings:settings,
    pages:metadata.outputs.pdf?[{id:crypto.randomUUID(),title:name.trim(),width:210,height:297,background:'#f6f0e5',isActive:true,elements:[
      {id:crypto.randomUUID(),type:'text',label:'Catalogue title',role:'title',text:name.trim(),binding:{kind:'shared',field:'collection'},direction,x:18,y:28,width:174,height:55,fontFamily:'serif',fontSize:16,lineHeight:1.2,color:'#302b25'},
      {id:crypto.randomUUID(),type:'text',label:'Catalogue introduction',role:'caption',text:metadata.description,direction,x:18,y:239,width:174,height:35,fontFamily:'sans',fontSize:4,lineHeight:1.4,color:'#302b25'},
    ]}]:[]};
}
export function duplicateCatalogue(snapshot:DesignSnapshot,suffix:string):DesignSnapshot{
 const copy=cloneSnapshot(snapshot),metadata=catalogueMetadata(copy);metadata.slug=`${metadata.slug.slice(0,Math.max(1,94-suffix.length)).replace(/-+$/,'')}-copy-${suffix}`;metadata.isActive=false;copy.catalogue=metadata;return copy;
}
export function cataloguePageProducts(page:DesignPage):string[]{return [...new Set([...(page.productIds??[]),page.catalogueSource?.productId,...page.elements.map(element=>element.binding?.kind==='product'?element.binding.productId??element.productId:element.productId)].filter((id):id is string=>!!id))];}
/** Whole pages are omitted, never partially reflowed, when their products are unavailable in this language. */
export function eligibleCataloguePdf(snapshot:DesignSnapshot,drafts:CommerceDraft[]):{snapshot:DesignSnapshot;excluded:{pageId:string;title:string;reason:'inactive'|'products';productIds:string[]}[]}{
 const next=cloneSnapshot(snapshot),excluded:{pageId:string;title:string;reason:'inactive'|'products';productIds:string[]}[]=[];
 next.pages=next.pages.filter(page=>{
  const brand=page.catalogueSource?.brandId??snapshot.catalogueSettings?.brandId,draft=drafts.find(item=>item.brandId===brand),ids=cataloguePageProducts(page);
  const missing=ids.filter(id=>!draft?.snapshot.products.some(product=>product.document.id===id&&productEligibleForCatalogue(product,snapshot.locale)));
  if(page.isActive===false||missing.length){excluded.push({pageId:page.id,title:page.title,reason:page.isActive===false?'inactive':'products',productIds:missing});return false;}return true;
 });return{snapshot:next,excluded};
}
