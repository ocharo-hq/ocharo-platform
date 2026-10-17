import {computed,ref} from 'vue';
import {afterEach,beforeEach,describe,expect,it,vi} from 'vitest';
import {mount,flushPromises,type VueWrapper} from '@vue/test-utils';
import {ResultExtensions} from '@wow-two-beta/fe/foundation/results';
import CatalogueWebDesigner from '@/presentation/workspace/CatalogueWebDesigner.vue';
import {createCatalogue} from '@/domain/workspace/CatalogueDocuments';
import {initialCommerce} from '@/domain/workspace/Commerce';
import type {DesignDocument} from '@/domain/workspace/EditorModels';
import type {AccessControls} from '@/application/workspace/UseAccess';
import type {WorkspaceControls} from '@/application/workspace/UseWorkspace';
const controls=vi.hoisted(()=>({get:vi.fn(),notifyError:vi.fn(),requestConfirmation:vi.fn()}));
vi.mock('@/application/workspace/UseCommerce',()=>({useCommerce:()=>({get:controls.get})}));
vi.mock('@/application/shared/UseFeedback',()=>({notifyError:controls.notifyError,requestConfirmation:controls.requestConfirmation}));
const brand='11111111-1111-4111-8111-111111111111';
function document():DesignDocument{return{id:'saved',organizationId:'org',kind:'catalogue',title:'Spring',revision:4,archived:false,createdAtUtc:'2026-10-01',updatedAtUtc:'2026-10-01',snapshot:createCatalogue('Spring','en',{slug:'spring',description:'Collection',isActive:true,outputs:{web:true,pdf:true},web:{theme:'editorial',pages:[{id:'first',title:'First section',layout:'grid',isActive:true,productIds:[]}]}},brand)};}
let wrapper:VueWrapper;
const api={getDesign:vi.fn(),getDesigns:vi.fn(),saveDesign:vi.fn()};
beforeEach(()=>{sessionStorage.clear();vi.clearAllMocks();api.getDesign.mockResolvedValue(ResultExtensions.ok(document()));api.getDesigns.mockResolvedValue(ResultExtensions.ok([]));api.saveDesign.mockImplementation((_org,_id,input)=>Promise.resolve(ResultExtensions.ok({...document(),title:input.title,snapshot:input.snapshot,revision:5})));controls.get.mockResolvedValue(ResultExtensions.ok({brandId:brand,revision:1,updatedAt:'2026-10-01',snapshot:initialCommerce('Ocharo','ocharo')}));controls.requestConfirmation.mockResolvedValue(true);});
afterEach(()=>wrapper?.unmount());
function setup(){const access={api,busy:ref(false),session:ref(null),setDirty:vi.fn(),write:(operation:(signal:AbortSignal)=>unknown)=>operation(new AbortController().signal)};const workspace={organization:ref({id:'org'}),brands:ref([{id:brand,name:'Ocharo'}]),canEdit:computed(()=>true),canApprove:computed(()=>true)};wrapper=mount(CatalogueWebDesigner,{props:{access:access as unknown as AccessControls,workspace:workspace as unknown as WorkspaceControls,documentId:'saved'},global:{stubs:{CataloguePublication:true,DesignRecovery:true}}});}
const button=(label:string)=>wrapper.findAll('button').find(item=>item.text()===label)!;
describe('independent web design edits',()=>{
 it('previews device widths and saves page changes against the same catalogue revision without losing PDF pages',async()=>{setup();await flushPromises();expect(wrapper.get('.responsive-preview').attributes('style')).toContain('1440px');await button('Mobile').trigger('click');expect(wrapper.get('.responsive-preview').attributes('style')).toContain('390px');await button('Tablet').trigger('click');expect(wrapper.get('.responsive-preview').attributes('style')).toContain('768px');await button('Add page').trigger('click');await button('Save catalogue').trigger('click');await flushPromises();const input=api.saveDesign.mock.calls[0]?.[2];expect(api.saveDesign.mock.calls[0]?.slice(0,2)).toEqual(['org','saved']);expect(input.expectedRevision).toBe(4);expect(input.snapshot.catalogue.web.pages).toHaveLength(2);expect(input.snapshot.pages).toEqual((await api.getDesign.mock.results[0]!.value).value.snapshot.pages);expect(document().snapshot.catalogue?.web.pages).toHaveLength(1);});
 it('keeps a page on declined removal and allows undo after confirmed removal',async()=>{setup();await flushPromises();controls.requestConfirmation.mockResolvedValue(false);await button('Remove page').trigger('click');await flushPromises();expect(wrapper.findAll('.page-choice')).toHaveLength(1);controls.requestConfirmation.mockResolvedValue(true);await button('Remove page').trigger('click');await flushPromises();expect(wrapper.findAll('.page-choice')).toHaveLength(0);await button('Undo').trigger('click');expect(wrapper.findAll('.page-choice')).toHaveLength(1);});
 it('edits catalogue copy and price visibility while keeping canonical products outside the document',async()=>{setup();await flushPromises();const inputs=wrapper.findAll('.shared-copy textarea');await inputs[2]!.setValue('International collection');const price=wrapper.findAll('.shared-copy input[type=checkbox]')[0]!;await price.setValue(false);await button('Save catalogue').trigger('click');await flushPromises();const snapshot=api.saveDesign.mock.calls[0]?.[2].snapshot;expect(snapshot.catalogueSettings.copyByLocale.en.header).toBe('International collection');expect(snapshot.catalogueSettings.priceDisplay.show).toBe(false);expect(snapshot.products).toBeUndefined();});

});
