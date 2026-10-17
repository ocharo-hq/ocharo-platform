import {computed,ref} from 'vue';
import {afterEach,beforeEach,describe,expect,it,vi} from 'vitest';
import {mount,flushPromises,type VueWrapper} from '@vue/test-utils';
import {ResultExtensions} from '@wow-two-beta/fe/foundation/results';
import {ApiFailureFactory} from '@wow-two-beta/fe/foundation/http';
import CatalogueGallery from '@/presentation/workspace/CatalogueGalleryPage.vue';
import {createCatalogue} from '@/domain/workspace/CatalogueDocuments';
import type {DesignDocument,CatalogueMetadata} from '@/domain/workspace/EditorModels';
import type {AccessControls} from '@/application/workspace/UseAccess';
import type {WorkspaceControls} from '@/application/workspace/UseWorkspace';
const feedback=vi.hoisted(()=>({requestConfirmation:vi.fn(),notifyError:vi.fn()}));
vi.mock('@/application/shared/UseFeedback',()=>feedback);
const metadata:CatalogueMetadata={slug:'spring',description:'Spring collection',isActive:true,outputs:{web:true,pdf:true},web:{theme:'editorial',pages:[]}};
const document:DesignDocument={id:'document',organizationId:'org',kind:'catalogue',title:'Spring',revision:4,archived:false,createdAtUtc:'2026-10-01T00:00:00Z',updatedAtUtc:'2026-10-01T12:00:00Z',locale:'en',catalogue:{...metadata,web:{theme:'editorial',pageCount:0},pdfPageCount:1},snapshot:createCatalogue('Spring','en',metadata,'brand')};
let wrapper:VueWrapper;
const api={getDesigns:vi.fn(),getDesign:vi.fn(),saveDesign:vi.fn(),archiveDesign:vi.fn()};
const setDirty=vi.fn();
beforeEach(()=>{vi.clearAllMocks();feedback.requestConfirmation.mockResolvedValue(true);api.getDesigns.mockResolvedValue(ResultExtensions.ok([document]));api.getDesign.mockResolvedValue(ResultExtensions.ok(document));api.saveDesign.mockResolvedValue(ResultExtensions.ok({...document,revision:5}));api.archiveDesign.mockResolvedValue(ResultExtensions.ok({...document,archived:true}));});
afterEach(()=>wrapper?.unmount());
function setup(){const access={api,busy:ref(false),setDirty,write:(operation:(signal:AbortSignal)=>unknown)=>operation(new AbortController().signal)};const workspace={organization:ref({id:'org'}),brands:ref([{id:'brand',name:'Ocharo'}]),canEdit:computed(()=>true)};wrapper=mount(CatalogueGallery,{props:{access:access as unknown as AccessControls,workspace:workspace as unknown as WorkspaceControls},global:{stubs:{teleport:true}}});}
function button(text:string){return wrapper.findAll('button').find(item=>item.text()===text)!;}
describe('catalogue gallery operations',()=>{
 it('lists summaries without loading snapshots and exposes independent designers',async()=>{setup();await flushPromises();expect(wrapper.text()).toContain('Spring collection');expect(api.getDesign).not.toHaveBeenCalled();await button('Web designer').trigger('click');expect(wrapper.emitted('open')).toEqual([['document','web']]);await button('PDF designer').trigger('click');expect(wrapper.emitted('open')?.[1]).toEqual(['document','pdf']);});
 it('duplicates by creating a new inactive document rather than modifying its source',async()=>{setup();await flushPromises();await button('Duplicate').trigger('click');await flushPromises();expect(api.saveDesign.mock.calls[0]?.[1]).toBeNull();expect(api.saveDesign.mock.calls[0]?.[2]).toMatchObject({expectedRevision:null,title:'Spring · Copy',snapshot:{catalogue:{isActive:false}}});expect(api.saveDesign.mock.calls[0]?.[2].snapshot.catalogue.slug).toMatch(/^spring-copy-/);expect(document.snapshot.catalogue?.slug).toBe('spring');});
 it('preserves a catalogue when removal is declined and archives with expected revision when approved',async()=>{setup();await flushPromises();feedback.requestConfirmation.mockResolvedValue(false);await button('Remove').trigger('click');await flushPromises();expect(api.archiveDesign).not.toHaveBeenCalled();feedback.requestConfirmation.mockResolvedValue(true);await button('Remove').trigger('click');await flushPromises();expect(api.archiveDesign).toHaveBeenCalledWith('org','document',4,expect.any(AbortSignal));});
 it('shows retry instead of a false first-catalogue state when loading fails',async()=>{api.getDesigns.mockResolvedValue(ResultExtensions.fail(ApiFailureFactory.create('http',new Response(null,{status:500}))));setup();await flushPromises();expect(feedback.notifyError).toHaveBeenCalled();expect(wrapper.text()).not.toContain('Create your first catalogue.');expect(button('Refresh')).toBeTruthy();expect(wrapper.find('.load-error').exists()).toBe(false);api.getDesigns.mockResolvedValue(ResultExtensions.ok([document]));await button('Refresh').trigger('click');await flushPromises();expect(wrapper.text()).toContain('Spring');});
 it('protects unsaved metadata and keeps it after a rejected close',async()=>{setup();await flushPromises();await button('Settings').trigger('click');await flushPromises();await wrapper.get('[role=dialog] input[maxlength="160"]').setValue('Updated collection');expect(setDirty).toHaveBeenLastCalledWith(true);feedback.requestConfirmation.mockResolvedValue(false);await button('Close').trigger('click');await flushPromises();expect(wrapper.find('[role=dialog]').exists()).toBe(true);expect(api.saveDesign).not.toHaveBeenCalled();await wrapper.get('[role=dialog] form').trigger('submit');await flushPromises();expect(api.saveDesign.mock.calls[0]?.[2]).toMatchObject({title:'Updated collection',expectedRevision:4});expect(setDirty).toHaveBeenLastCalledWith(false);});
 it('validates reserved slugs next to the field and prevents a write',async()=>{setup();await flushPromises();await button('Settings').trigger('click');await flushPromises();await wrapper.get('[role=dialog] input[maxlength="100"]').setValue('admin');await wrapper.get('[role=dialog] form').trigger('submit');await flushPromises();expect(wrapper.get('#slug-error').text()).toContain('reserved');expect(api.saveDesign).not.toHaveBeenCalled();});
 it('keeps a successful empty region plain while a toolbar refresh is pending',async()=>{
  api.getDesigns.mockResolvedValueOnce(ResultExtensions.ok([]));setup();await flushPromises();
  expect(wrapper.get('.query-label').text()).toBe('Create your first catalogue.');
  expect(wrapper.find('.catalogue-grid').exists()).toBe(false);
  let complete!:(value:ReturnType<typeof ResultExtensions.ok<DesignDocument[]>>)=>void;
  api.getDesigns.mockReturnValueOnce(new Promise(resolve=>{complete=resolve;}));
  await button('Refresh').trigger('click');
  expect(wrapper.find('.skeleton-gallery').exists()).toBe(false);
  expect(wrapper.get('.query-label').text()).toBe('Create your first catalogue.');
  expect(button('New catalogue').attributes('disabled')).toBeDefined();
  complete(ResultExtensions.ok([]));await flushPromises();
 });
 it('preserves catalogue identities and actions during refresh and restores last good values after failure',async()=>{
  setup();await flushPromises();let complete!:(value:ReturnType<typeof ResultExtensions.fail>)=>void;
  api.getDesigns.mockReturnValueOnce(new Promise(resolve=>{complete=resolve;}));await button('Refresh').trigger('click');
  expect(wrapper.get('.card-heading').text()).toContain('Spring');expect(button('Web designer').isVisible()).toBe(true);
  expect(wrapper.find('.skeleton-gallery').exists()).toBe(false);
  complete(ResultExtensions.fail(ApiFailureFactory.create('http',new Response(null,{status:503}))));await flushPromises();
  expect(wrapper.get('.catalogue-grid').text()).toContain('Spring collection');
  expect(wrapper.text()).not.toContain('Catalogues unavailable');expect(feedback.notifyError).toHaveBeenCalled();
 });

});
