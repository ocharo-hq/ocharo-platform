import { createApiClient, ApiFailureFactory, type ApiDecoder } from '@wow-two-beta/fe/foundation/http';
import type { SessionTransport } from './WorkspaceApi';
import { ResultExtensions } from '@wow-two-beta/fe/foundation/results';
import type {CommerceDraft,CommerceRelease,CommerceSnapshot,IntegrationTicket,CreativeInput,CreativeImport,Provenance} from '@/domain/workspace/Commerce';
const record=(v:unknown):v is Record<string,unknown>=>typeof v==='object'&&v!==null&&!Array.isArray(v);
function decode<T>(guard:(v:unknown)=>v is T):ApiDecoder<T> {return value=>guard(value)?ResultExtensions.ok(value):ResultExtensions.fail(ApiFailureFactory.create('protocol'));}
const snapshot=(v:unknown):v is CommerceSnapshot=>record(v)&&typeof v.slug==='string'&&record(v.identity)&&typeof v.identity.name==='string'&&record(v.identity.tagline)&&record(v.identity.descriptor)&&Array.isArray(v.identity.palette)&&Array.isArray(v.identity.links)&&Array.isArray(v.products)&&v.products.every(p=>record(p)&&record(p.document)&&typeof p.document.id==='string'&&record(p.document.locales)&&Array.isArray(p.document.media))&&Array.isArray(v.collections)&&record(v.logos)&&record(v.mediaApprovals)&&Array.isArray(v.verifiedLinkKeys)&&Array.isArray(v.sources);
const draft=(v:unknown):v is CommerceDraft=>record(v)&&typeof v.brandId==='string'&&Number.isSafeInteger(v.revision)&&Number(v.revision)>0&&snapshot(v.snapshot);
const release=(v:unknown):v is CommerceRelease=>record(v)&&typeof v.id==='string'&&Number.isSafeInteger(v.revision)&&snapshot(v.snapshot);
const creative=(v:unknown):v is CreativeImport=>record(v)&&typeof v.id==='string'&&record(v.input)&&typeof v.status==='string';
export function createCommerceApi(csrf:()=>string,session:SessionTransport={}){
 const client=createApiClient({credentials:'same-origin',...session,retry:false});
 const org=(id:string)=>`/api/organizations/${encodeURIComponent(id)}`;
 const path=(o:string,b:string)=>`${org(o)}/brands/${encodeURIComponent(b)}/commerce`;
 const write=(signal:AbortSignal)=>({signal,headers:{'X-CSRF-TOKEN':csrf()}});
 return {
  get:(o:string,b:string,signal:AbortSignal)=>client.get(path(o,b),{signal,decode:decode((v):v is CommerceDraft|null=>v===null||draft(v))}),
  save:(o:string,b:string,expectedRevision:number,snapshot:CommerceSnapshot,signal:AbortSignal)=>client.put(path(o,b),{...write(signal),body:{expectedRevision,snapshot},decode:decode(draft)}),
  adopt:(o:string,b:string,slug:string,signal:AbortSignal)=>client.post(`${path(o,b)}/adopt-preserved`,{...write(signal),body:{slug},decode:decode(draft)}),
  releases:(o:string,b:string,signal:AbortSignal)=>client.get(`${path(o,b)}/releases`,{signal,decode:decode((v):v is CommerceRelease[]=>Array.isArray(v)&&v.every(release))}),
  publish:(o:string,b:string,expectedRevision:number,note:string,signal:AbortSignal)=>client.post(`${path(o,b)}/releases`,{...write(signal),body:{expectedRevision,note},decode:decode(release)}),
  withdraw:(o:string,b:string,id:string,reason:string,signal:AbortSignal)=>client.post(`${path(o,b)}/releases/${encodeURIComponent(id)}/withdraw`,{...write(signal),body:{reason},response:'empty'}),
  attach:(o:string,b:string,expectedRevision:number,mediaId:string,provenance:Provenance,signal:AbortSignal)=>client.post(`${path(o,b)}/media`,{...write(signal),body:{expectedRevision,mediaId,provenance},decode:decode(draft)}),
  ticket:(o:string,audience:string,signal:AbortSignal)=>client.post(`${org(o)}/integration-tickets`,{...write(signal),body:{audience},decode:decode((v):v is IntegrationTicket=>record(v)&&typeof v.token==='string'&&typeof v.expiresAt==='string')}),
  creative:(o:string,input:CreativeInput,signal:AbortSignal)=>client.post(`${org(o)}/creative-imports`,{...write(signal),body:input,decode:decode(creative)}),
  imports:(o:string,b:string,signal:AbortSignal)=>client.get(`${org(o)}/brands/${encodeURIComponent(b)}/creative-imports`,{signal,decode:decode((v):v is CreativeImport[]=>Array.isArray(v)&&v.every(creative))}),
  uploadEdition:(o:string,b:string,body:FormData,signal:AbortSignal)=>client.post(`${path(o,b)}/catalogue-editions`,{...write(signal),body,decode:decode(draft)}),
  publishEdition:(o:string,b:string,locale:string,expectedRevision:number,note:string,signal:AbortSignal)=>client.post(`${path(o,b)}/catalogue-editions/${encodeURIComponent(locale)}/publish`,{...write(signal),body:{expectedRevision,note},decode:decode(release)}),
  publishEditionById:(o:string,b:string,editionId:string,expectedRevision:number,note:string,signal:AbortSignal)=>client.post(`${path(o,b)}/catalogue-editions/${encodeURIComponent(editionId)}/publish`,{...write(signal),body:{expectedRevision,note},decode:decode(release)}),
  image:(o:string,b:string,sha:string)=>`${path(o,b)}/media/${encodeURIComponent(sha)}`,
 };
}
