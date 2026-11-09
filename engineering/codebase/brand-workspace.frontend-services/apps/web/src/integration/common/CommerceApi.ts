import { createApiClient, ApiFailureFactory, type ApiDecoder } from '@wow-two-beta/fe/foundation/http';
import type { SessionTransport } from './WorkspaceApi';
import { ResultExtensions } from '@wow-two-beta/fe/foundation/results';
import type {CommerceDraft,CommerceRelease,CommerceSnapshot,CreativeInput,CreativeImport,Provenance} from '@/domain/common/models/Commerce';
import { eachWithNulls, memberWithNulls, withNulls } from '@/integration/common/WireNulls';
const record=(v:unknown):v is Record<string,unknown>=>typeof v==='object'&&v!==null&&!Array.isArray(v);
/** @internal Restores the null members the wire leaves out, then checks the result. */
function decode<T>(guard:(v:unknown)=>v is T,normalize:(v:unknown)=>unknown=v=>v):ApiDecoder<T> {
 return value=>{const normalized=normalize(value);return guard(normalized)?ResultExtensions.ok(normalized):ResultExtensions.fail(ApiFailureFactory.create('protocol'));};
}
/** @internal Applies `normalize` to every value of a JSON object; any other value passes through unchanged. */
const eachValueWithNulls=(v:unknown,normalize:(item:unknown)=>unknown):unknown=>record(v)?Object.fromEntries(Object.entries(v).map(([key,item])=>[key,normalize(item)])):v;
/** @internal Applies each normalizer to its member of a JSON object, in order. */
const membersWithNulls=(v:unknown,members:Record<string,(item:unknown)=>unknown>):unknown=>Object.entries(members).reduce((value,[key,normalize])=>memberWithNulls(value,key,normalize),v);
const words=(v:unknown)=>withNulls(v,['uz','ru','en']);
const copy=(v:unknown)=>withNulls(v,['name','fabric','decoration','description']);
const image=(v:unknown)=>withNulls(v,['width','height','alt','sourcePath']);
const provenance=(v:unknown)=>withNulls(v,['sourceUrl','credit','rights']);
const pricing=(v:unknown)=>memberWithNulls(withNulls(v,['wording']),'tiers',tiers=>eachWithNulls(tiers,tier=>withNulls(tier,['maximumQuantity'])));
const productDocument=(v:unknown)=>membersWithNulls(withNulls(v,['pricing','notes','stock']),{
 locales:locales=>membersWithNulls(locales,{uz:copy,ru:copy,en:copy,additional:additional=>eachValueWithNulls(additional,copy)}),
 pricing,media:media=>eachWithNulls(media,image)});
const product=(v:unknown)=>membersWithNulls(withNulls(v,['sourceRevision']),{
 document:productDocument,translations:translations=>eachValueWithNulls(translations,copy)});
const identity=(v:unknown)=>membersWithNulls(v,{tagline:words,descriptor:words,links:links=>eachWithNulls(links,link=>withNulls(link,['handle']))});
/** @internal Restores a commerce snapshot's omitted null members, as `CommerceSnapshot` declares them. */
const snapshotWithNulls=(v:unknown)=>membersWithNulls(v,{
 identity,logos:logos=>eachValueWithNulls(logos,logo=>withNulls(logo,['width','height','sourcePath'])),products:products=>eachWithNulls(products,product),
 collections:collections=>eachWithNulls(collections,collection=>memberWithNulls(withNulls(collection,['description']),'names',words)),
 mediaApprovals:approvals=>eachValueWithNulls(approvals,provenance),
 catalogueEditions:editions=>eachWithNulls(editions,edition=>withNulls(edition,['pdfSha256']))});
const draftWithNulls=(v:unknown)=>memberWithNulls(v,'snapshot',snapshotWithNulls);
const releaseWithNulls=(v:unknown)=>memberWithNulls(withNulls(v,['withdrawnAt']),'snapshot',snapshotWithNulls);
const creativeWithNulls=(v:unknown)=>memberWithNulls(v,'input',input=>withNulls(input,['productId']));
const snapshot=(v:unknown):v is CommerceSnapshot=>record(v)&&typeof v.slug==='string'&&record(v.identity)&&typeof v.identity.name==='string'&&record(v.identity.tagline)&&record(v.identity.descriptor)&&Array.isArray(v.identity.palette)&&Array.isArray(v.identity.links)&&Array.isArray(v.products)&&v.products.every(p=>record(p)&&record(p.document)&&typeof p.document.id==='string'&&record(p.document.locales)&&Array.isArray(p.document.media))&&Array.isArray(v.collections)&&record(v.logos)&&record(v.mediaApprovals)&&Array.isArray(v.verifiedLinkKeys)&&Array.isArray(v.sources);
const draft=(v:unknown):v is CommerceDraft=>record(v)&&typeof v.brandId==='string'&&Number.isSafeInteger(v.revision)&&Number(v.revision)>0&&snapshot(v.snapshot);
const release=(v:unknown):v is CommerceRelease=>record(v)&&typeof v.id==='string'&&Number.isSafeInteger(v.revision)&&snapshot(v.snapshot);
const creative=(v:unknown):v is CreativeImport=>record(v)&&typeof v.id==='string'&&record(v.input)&&typeof v.status==='string';
/** @internal Reads the draft envelope: `data` is left out before the first save. */
const draftData=(v:unknown):unknown=>record(v)?(Object.hasOwn(v,'data')?draftWithNulls(v.data):null):undefined;
const draftEnvelope=(v:unknown):v is CommerceDraft|null=>v===null||draft(v);
export function createCommerceApi(csrf:()=>string,session:SessionTransport={}){
 const client=createApiClient({credentials:'same-origin',...session,retry:false});
 const org=(id:string)=>`/api/organizations/${encodeURIComponent(id)}`;
 const path=(o:string,b:string)=>`${org(o)}/brands/${encodeURIComponent(b)}/commerce`;
 const write=(signal:AbortSignal)=>({signal,headers:{'X-CSRF-TOKEN':csrf()}});
 return {
  get:(o:string,b:string,signal:AbortSignal)=>client.get(path(o,b),{signal,unwrap:false,decode:decode(draftEnvelope,draftData)}),
  save:(o:string,b:string,expectedRevision:number,snapshot:CommerceSnapshot,signal:AbortSignal)=>client.put(path(o,b),{...write(signal),body:{expectedRevision,snapshot},decode:decode(draft,draftWithNulls)}),
  adopt:(o:string,b:string,slug:string,signal:AbortSignal)=>client.post(`${path(o,b)}/adopt-preserved`,{...write(signal),body:{slug},decode:decode(draft,draftWithNulls)}),
  releases:(o:string,b:string,signal:AbortSignal)=>client.get(`${path(o,b)}/releases`,{signal,decode:decode((v):v is CommerceRelease[]=>Array.isArray(v)&&v.every(release),v=>eachWithNulls(v,releaseWithNulls))}),
  publish:(o:string,b:string,expectedRevision:number,note:string,signal:AbortSignal)=>client.post(`${path(o,b)}/releases`,{...write(signal),body:{expectedRevision,note},decode:decode(release,releaseWithNulls)}),
  withdraw:(o:string,b:string,id:string,reason:string,signal:AbortSignal)=>client.post(`${path(o,b)}/releases/${encodeURIComponent(id)}/withdraw`,{...write(signal),body:{reason},response:'empty'}),
  attach:(o:string,b:string,expectedRevision:number,mediaId:string,provenance:Provenance,signal:AbortSignal)=>client.post(`${path(o,b)}/media`,{...write(signal),body:{expectedRevision,mediaId,provenance},decode:decode(draft,draftWithNulls)}),
  creative:(o:string,input:CreativeInput,signal:AbortSignal)=>client.post(`${org(o)}/creative-imports`,{...write(signal),body:input,decode:decode(creative,creativeWithNulls)}),
  imports:(o:string,b:string,signal:AbortSignal)=>client.get(`${org(o)}/brands/${encodeURIComponent(b)}/creative-imports`,{signal,decode:decode((v):v is CreativeImport[]=>Array.isArray(v)&&v.every(creative),v=>eachWithNulls(v,creativeWithNulls))}),
  uploadEdition:(o:string,b:string,body:FormData,signal:AbortSignal)=>client.post(`${path(o,b)}/catalogue-editions`,{...write(signal),body,decode:decode(draft,draftWithNulls)}),
  publishEdition:(o:string,b:string,locale:string,expectedRevision:number,note:string,signal:AbortSignal)=>client.post(`${path(o,b)}/catalogue-editions/${encodeURIComponent(locale)}/publish`,{...write(signal),body:{expectedRevision,note},decode:decode(release,releaseWithNulls)}),
  publishEditionById:(o:string,b:string,editionId:string,expectedRevision:number,note:string,signal:AbortSignal)=>client.post(`${path(o,b)}/catalogue-editions/${encodeURIComponent(editionId)}/publish`,{...write(signal),body:{expectedRevision,note},decode:decode(release,releaseWithNulls)}),
  image:(o:string,b:string,sha:string)=>`${path(o,b)}/media/${encodeURIComponent(sha)}`,
 };
}
