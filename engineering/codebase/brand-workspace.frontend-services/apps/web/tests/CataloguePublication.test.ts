import { computed, ref } from 'vue';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ResultExtensions } from '@wow-two-beta/ui-vue/foundation/results';
import { ApiFailureFactory } from '@wow-two-beta/ui-vue/foundation/http';
import type { AccessControls } from '@/application/workspace/UseAccess';
import type { WorkspaceControls } from '@/application/workspace/UseWorkspace';
import { initialCommerce } from '@/domain/workspace/Commerce';
import { createCatalogueTemplate } from '@/domain/workspace/Templates';
import CataloguePublication from '@/presentation/workspace/CataloguePublication.vue';
const mock=vi.hoisted(()=>({get:vi.fn(),uploadEdition:vi.fn(),publishEdition:vi.fn(),render:vi.fn(),check:vi.fn()}));
vi.mock('@/application/workspace/UseDesignPreflight',()=>({useDesignPreflight:()=>({review:ref(null),check:mock.check,resolve:vi.fn()})}));
vi.mock('@/application/workspace/UseCommerce',()=>({useCommerce:()=>mock}));
vi.mock('@/application/workspace/ExportDocument',()=>({renderCatalogueBundle:mock.render}));
const source={id:'saved-design',organizationId:'org',kind:'catalogue' as const,title:'Existing edition',revision:4,archived:false,createdAtUtc:'2026-09-29',updatedAtUtc:'2026-09-29',snapshot:createCatalogueTemplate('uz')};
const edition={id:'edition',sourceDesignId:source.id,sourceRevision:4,title:source.title,locale:'uz' as const,templateId:source.snapshot.templateId,pages:[],pdfSha256:'a'.repeat(64),approvedForPublication:false,createdAt:'2026-09-29'};
const draft=()=>({brandId:'brand',revision:8,snapshot:{...initialCommerce('Ocharo','ocharo'),catalogueEditions:[edition]},updatedAt:'2026-09-29'});
let wrapper:VueWrapper;afterEach(()=>wrapper?.unmount());
beforeEach(()=>{vi.clearAllMocks();mock.check.mockResolvedValue(true);mock.get.mockResolvedValue(ResultExtensions.ok(draft()));mock.uploadEdition.mockResolvedValue(ResultExtensions.ok({...draft(),revision:9}));mock.render.mockResolvedValue({pdf:new Blob(['pdf'],{type:'application/pdf'}),pages:[{id:'catalogue-01',png:new Blob(['png'],{type:'image/png'}),pdf:new Blob(['pdf'],{type:'application/pdf'})}]});});
function setup(dirty=false){const access={busy:ref(false),session:ref({csrfToken:'token'}),recheck:vi.fn(),write:vi.fn((operation:(signal:AbortSignal)=>unknown)=>operation(new AbortController().signal))};const workspace={organization:ref({id:'org'}),brands:ref([{id:'brand',name:'Ocharo'}]),canEdit:computed(()=>true),canApprove:computed(()=>true)};
wrapper=mount(CataloguePublication,{props:{access:access as unknown as AccessControls,workspace:workspace as unknown as WorkspaceControls,document:source,dirty,disabled:false}});}
function button(name:string){return wrapper.findAll('button').find(item=>item.text()===name)!;}
describe('exact edition preparation and owner approval',()=>{
 it('uploads the immutable saved revision with exact multipart artifact fields',async()=>{
  setup();await flushPromises();await button('Prepare buyer edition').trigger('click');await flushPromises();
  expect(mock.render).toHaveBeenCalledWith(source.snapshot,source.title,expect.any(Function),expect.any(AbortSignal));
  const body=mock.uploadEdition.mock.calls[0]![2] as FormData;
  expect(JSON.parse(body.get('metadata') as string)).toEqual({expectedRevision:8,sourceDesignId:'saved-design',sourceRevision:4});
  expect([...body.keys()]).toEqual(['metadata','pdf','page-catalogue-01.png','page-catalogue-01.pdf']);
  expect(wrapper.text()).toContain('Edition saved privately');expect(mock.publishEdition).not.toHaveBeenCalled();
 });
 it('blocks preparation while the visible design has unsaved edits',async()=>{
  setup(true);await flushPromises();expect(button('Prepare buyer edition').attributes('disabled')).toBeDefined();
  await button('Prepare buyer edition').trigger('click');expect(mock.render).not.toHaveBeenCalled();
 });
 it('requires explicit exact-content review and a publication note',async()=>{
  setup();await flushPromises();expect(button('Publish buyer edition').attributes('disabled')).toBeDefined();
  await wrapper.get('input[type=checkbox]').setValue(true);expect(button('Publish buyer edition').attributes('disabled')).toBeDefined();
  await wrapper.get('textarea').setValue('Reviewed original pages and quantity tiers.');mock.publishEdition.mockResolvedValue(ResultExtensions.ok({revision:7}));
  await button('Publish buyer edition').trigger('click');await flushPromises();
  expect(mock.publishEdition).toHaveBeenCalledWith('org','brand','uz',8,'Reviewed original pages and quantity tiers.',expect.any(AbortSignal));
  expect(wrapper.text()).toContain('published in release7'.replace('release7','release 7'));
 });
 it('never renders or uploads when page review is cancelled',async()=>{
  mock.check.mockResolvedValue(false);setup();await flushPromises();await button('Prepare buyer edition').trigger('click');await flushPromises();
  expect(mock.check).toHaveBeenCalledWith(source.snapshot,{actionLabel:'Prepare buyer edition'});
  expect(mock.render).not.toHaveBeenCalled();expect(mock.uploadEdition).not.toHaveBeenCalled();expect(wrapper.text()).toContain('cancelled');
 });
 it('reports concurrency failures without claiming the upload or publication succeeded',async()=>{
  mock.uploadEdition.mockResolvedValue(ResultExtensions.fail(ApiFailureFactory.create('http',new Response(null,{status:409}))));
  setup();await flushPromises();await button('Prepare buyer edition').trigger('click');await flushPromises();
  expect(wrapper.get('[role=alert]').text()).toContain('record changed');expect(wrapper.text()).not.toContain('Edition saved privately');
 });
});
