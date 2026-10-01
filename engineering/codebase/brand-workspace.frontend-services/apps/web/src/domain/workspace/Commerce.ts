export type Locale = 'uz' | 'ru' | 'en';
export interface Words { uz: string | null; ru: string | null; en: string | null }
export interface Provenance { kind: 'unknown'|'photo'|'visualization'|'archival'; approvedForPublication: boolean; sourceUrl: string|null; credit: string|null; rights: string|null }
export interface CommerceImage { id:string;role:string;fileName:string;contentType:string;byteLength:number;sha256:string;width:number|null;height:number|null;hasPreview:boolean;alt:string|null;sourcePath:string|null }
export interface Copy {status:'draft'|'approved';name:string|null;fabric:string|null;decoration:string|null;description:string|null}
export interface Product {translations?:Partial<Record<CatalogueLocale,Copy>>|null;mediaGroups?:ProductMediaGroup[]|null;document:{id:string;garmentType:string;variantCode:string;status:'draft'|'active'|'archived';sortOrder:number;sizes:string[];fabricCodes:string[];decorationCodes:string[];tags:string[];locales:Record<Locale,Copy>;pricing:{currency:string;status:'draft'|'confirmed';tiers:{minimumQuantity:number;maximumQuantity:number|null;unitPrice:number}[];wording:string|null}|null;media:CommerceImage[];issues:{id:string;field:string;status:string;question:string}[];notes:string|null;stock:Record<string,number>|null;madeToOrder:boolean;[key:string]:unknown};revision:number;sourceRevision:number|null}
export interface Collection {id:string;names:Words;description:string|null;productIds:string[];isPublished:boolean;sortOrder:number}
export interface CatalogueEdition {slug?:string|null;description?:string;isActive?:boolean;updatedAt?:string|null;web?:{mediaGroup?:ProductMediaSelection;priceDisplay?:{show:boolean;showCurrency:boolean;showQuantity:boolean;showRetail:boolean;showWholesale:boolean};copy?:Record<string,string>;productMedia?:Record<string,string[]>;theme:'editorial'|'grid';pages:{id:string;title:string;layout:'spread'|'grid'|'collage';productIds:string[];isActive?:boolean}[]}|null;id:string;sourceDesignId:string;sourceRevision:number;title:string;locale:CatalogueLocale;templateId:string;pages:{id:string;title:string;widthMm:number;heightMm:number;productIds:string[];pngSha256:string;pdfSha256:string}[];pdfSha256:string|null;approvedForPublication:boolean;createdAt:string}
export interface CommerceSnapshot {catalogueEditions?:CatalogueEdition[];slug:string;identity:{name:string;tagline:Words;descriptor:Words;palette:{key:string;name:string;hex:string;role:string}[];links:{key:string;label:string;handle:string|null;url:string}[];[key:string]:unknown};logos:Record<string,CommerceImage & {slot:string;status:string;uploadedAt:string}>;products:Product[];collections:Collection[];mediaApprovals:Record<string,Provenance>;verifiedLinkKeys:string[];sources:unknown[]}
export interface CommerceDraft {brandId:string;revision:number;snapshot:CommerceSnapshot;updatedAt:string}
export interface CommerceRelease {id:string;brandId:string;revision:number;sourceRevision:number;publishedAt:string;note:string;withdrawnAt:string|null;snapshot:CommerceSnapshot}
export interface IntegrationTicket {token:string;expiresAt:string}
export interface CreativeInput {brandId:string;productId:string|null;source:{application:string;workspaceId:string;lookId:string;revision:number};assetReleases:{id:string;sha256:string}[];materials:{slot:string;source:string;materialId?:string;sha256?:string}[];derivative:{mediaId:string;sha256:string;mediaType:string};sourceApproval?:{status:string;note?:string}}
export interface CreativeImport {id:string;createdAt:string;status:string;input:CreativeInput}
export const words = ():Words => ({uz:null,ru:null,en:null});
export const emptyCopy = ():Copy => ({status:'draft',name:null,fabric:null,decoration:null,description:null});
export function initialCommerce(name:string,slug:string):CommerceSnapshot {return {slug,identity:{name,tagline:words(),descriptor:words(),palette:[],links:[]},logos:{},products:[],collections:[],mediaApprovals:{},verifiedLinkKeys:[],sources:[]};}

/** Supported content languages; operator interface choices remain Uzbek, Russian and English. */
export const catalogueLocales = [
  {code:'uz',name:'O‘zbekcha',dir:'ltr'}, {code:'ru',name:'Русский',dir:'ltr'},
  {code:'en',name:'English',dir:'ltr'}, {code:'tg',name:'Тоҷикӣ',dir:'ltr'},
  {code:'kk',name:'Қазақша',dir:'ltr'}, {code:'ky',name:'Кыргызча',dir:'ltr'},
  {code:'tk',name:'Türkmençe',dir:'ltr'}, {code:'tr',name:'Türkçe',dir:'ltr'},
  {code:'az',name:'Azərbaycanca',dir:'ltr'}, {code:'fr',name:'Français',dir:'ltr'},
  {code:'it',name:'Italiano',dir:'ltr'}, {code:'de',name:'Deutsch',dir:'ltr'},
  {code:'es',name:'Español',dir:'ltr'}, {code:'pt',name:'Português',dir:'ltr'},
  {code:'pl',name:'Polski',dir:'ltr'}, {code:'uk',name:'Українська',dir:'ltr'},
  {code:'ar',name:'العربية',dir:'rtl'}, {code:'fa',name:'فارسی',dir:'rtl'},
  {code:'ur',name:'اردو',dir:'rtl'}, {code:'zh-Hans',name:'简体中文',dir:'ltr'},
  {code:'ko',name:'한국어',dir:'ltr'}, {code:'ja',name:'日本語',dir:'ltr'},
  {code:'id',name:'Bahasa Indonesia',dir:'ltr'}, {code:'ms',name:'Bahasa Melayu',dir:'ltr'},
  {code:'hi',name:'हिन्दी',dir:'ltr'},
] as const;
export type CatalogueLocale = typeof catalogueLocales[number]['code'];
export type ProductMediaCategory = 'model'|'product'|'detail'|'fabric';
export type ProductMediaSelection = 'all'|ProductMediaCategory;
export interface ProductMediaGroup {id:string;label:string;category:ProductMediaCategory;mediaIds:string[]}
export function isCatalogueLocale(value:unknown):value is CatalogueLocale {return catalogueLocales.some(locale=>locale.code===value);}
export function isCoreLocale(locale:string):locale is Locale {return locale==='uz'||locale==='ru'||locale==='en';}
export function hasProductTranslation(product:Product,locale:CatalogueLocale):boolean {
  return isCoreLocale(locale)||product.translations?.[locale]!==undefined;
}
/** Exact translation only: absent content remains an explicit empty draft. */
export function productCopy(product:Product,locale:CatalogueLocale):Copy {
  return isCoreLocale(locale)?product.document.locales[locale]:product.translations?.[locale]??emptyCopy();
}
export function updateProductCopy(product:Product,locale:CatalogueLocale,copy:Copy):void {
  if(isCoreLocale(locale))product.document.locales[locale]=copy;
  else (product.translations??={})[locale]=copy;
}
export function productMediaCategory(image:CommerceImage):ProductMediaCategory {
  if(image.role==='model')return 'model';
  if(image.role==='detail')return 'detail';
  if(image.role==='fabric')return 'fabric';
  return 'product';
}
/** Explicit groups preserve user order; old products use image roles until grouped. */
export function productMediaForGroup(product:Product,category:ProductMediaSelection):CommerceImage[] {
  const media=product.document.media.filter(image=>image.role!=='reference');
  if(category==='all')return media;
  if(!product.mediaGroups?.length)return media.filter(image=>productMediaCategory(image)===category);
  const ids=[...new Set(product.mediaGroups.filter(group=>group.category===category).flatMap(group=>group.mediaIds))];
  return ids.flatMap(id=>{const image=media.find(item=>item.id===id);return image?[image]:[];});
}

/** Exact-language publication eligibility shared with the backend; prices have their own confirmation. */
export function productEligibleForCatalogue(product:Product,locale:CatalogueLocale):boolean {
 return product.document.status==='active' && productCopy(product,locale).status==='approved' && product.document.issues.every(issue=>issue.status!=='open');
}
