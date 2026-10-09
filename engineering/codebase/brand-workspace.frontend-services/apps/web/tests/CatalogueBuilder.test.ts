import { chooseDeskValue } from './helpers/DeskSelect';
import { computed, ref } from 'vue';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ResultExtensions } from '@wow-two-beta/ui-vue/foundation/results';
import type { AccessControls } from '@/application/workspace/UseAccess';
import type { WorkspaceControls } from '@/application/workspace/UseWorkspace';
import { emptyCopy, initialCommerce } from '@/domain/workspace/Commerce';
import CatalogueBuilder from '@/presentation/workspace/CatalogueBuilder.vue';
const api=vi.hoisted(()=>({get:vi.fn(),releases:vi.fn(),image:vi.fn()}));
vi.mock('@/integration/workspace/CommerceApi',()=>({createCommerceApi:()=>api}));
let wrapper:VueWrapper;
afterEach(()=>wrapper?.unmount());
const org='11111111-1111-1111-1111-111111111111',brand='22222222-2222-2222-2222-222222222222',sha='a'.repeat(64);
function fixture(){const snapshot=initialCommerce('Synthetic atelier','synthetic');snapshot.products=[{revision:2,sourceRevision:null,document:{id:'sample',garmentType:'vest',variantCode:'one',status:'active',sortOrder:0,sizes:[],fabricCodes:[],decorationCodes:[],tags:[],locales:{en:{...emptyCopy(),name:'Garment one'},ru:emptyCopy(),uz:emptyCopy()},pricing:null,media:[{id:'image',role:'photo',fileName:'preview.png',contentType:'image/png',byteLength:100,sha256:sha,width:null,height:null,hasPreview:true,alt:null,sourcePath:null}],issues:[],notes:null,stock:null,madeToOrder:false}}];return {brandId:brand,revision:5,snapshot,updatedAt:'2026-09-29'};}
beforeEach(()=>{vi.clearAllMocks();api.get.mockResolvedValue(ResultExtensions.ok(fixture()));api.releases.mockResolvedValue(ResultExtensions.ok([]));api.image.mockReturnValue('/image');});
function mountBuilder(){const access={busy:ref(false),session:ref({csrfToken:'token'}),recheck:vi.fn()};const workspace={organization:ref({id:org}),brands:ref([{id:brand,name:'Synthetic atelier'}]),canEdit:computed(()=>true)};
wrapper=mount(CatalogueBuilder,{props:{access:access as unknown as AccessControls,workspace:workspace as unknown as WorkspaceControls,locale:'en',disabled:false,hasDocument:false}});}
describe('catalogue selection workflow',()=>{
 it('creates selected pages with provenance and warns about unreviewed images',async()=>{
  mountBuilder();await flushPromises();await wrapper.get('.product-choice input').setValue(true);
  expect(wrapper.text()).toContain('1 chosen images need publication review');
  await wrapper.get('input[value="collage"]').setValue(true);
  await wrapper.findAll('button').find(button=>button.text()==='Create catalogue')!.trigger('click');
  const pages=wrapper.emitted('pages')?.[0]?.[0] as Array<{catalogueSource:{revision:number;layout:string}}>;
  expect(pages[0]?.catalogueSource).toMatchObject({revision:5,layout:'collage'});
 });
 it('uses the selected immutable release and resets stale selection',async()=>{
  const draft=fixture();api.releases.mockResolvedValue(ResultExtensions.ok([{...draft,id:'release',revision:1,sourceRevision:3,publishedAt:'2026-09-28',withdrawnAt:null,note:'Reviewed'}]));
  mountBuilder();await flushPromises();await wrapper.get('.product-choice input').setValue(true);
  await chooseDeskValue(wrapper, 'Product source', 'release');
  expect(wrapper.text()).toContain('0 products selected');await wrapper.get('.product-choice input').setValue(true);
  await wrapper.findAll('button').find(button=>button.text()==='Create catalogue')!.trigger('click');
  const pages=wrapper.emitted('pages')?.[0]?.[0] as Array<{catalogueSource:{revision:number}}>;
  expect(pages[0]?.catalogueSource.revision).toBe(3);
 });
 it('retains the selection while explaining missing image choices',async()=>{
  mountBuilder();await flushPromises();await wrapper.get('.product-choice input').setValue(true);await wrapper.get('.image-picker input').setValue(false);
  await wrapper.findAll('button').find(button=>button.text()==='Create catalogue')!.trigger('click');
  expect(wrapper.get('[role="alert"]').text()).toContain('Choose one to 2');expect(wrapper.emitted('pages')).toBeUndefined();
 });
 it('keeps chosen images when a same-brand refresh fails',async()=>{
  mountBuilder();await flushPromises();await wrapper.get('.product-choice input').setValue(true);
  api.get.mockResolvedValueOnce({ok:false,failure:{code:'transport',status:0,headers:new Headers()}});
  await wrapper.findAll('button').find(button=>button.text()==='Refresh')!.trigger('click');await flushPromises();
  expect((wrapper.get('.product-choice input').element as HTMLInputElement).checked).toBe(true);
  expect(wrapper.text()).toContain('1 products selected');expect(wrapper.find('.skeleton-rows').exists()).toBe(false);
 });

});
