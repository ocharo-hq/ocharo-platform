import { feedbackBus } from '@/application/shared/UseFeedback';
import { flushPromises, mount } from '@vue/test-utils';
import { describe, expect, it, vi } from 'vitest';
import DesignPrintPreview from '@/presentation/workspace/DesignPrintPreview.vue';
import type { DesignPage } from '@/domain/workspace/EditorModels';
const render=vi.hoisted(()=>vi.fn());vi.mock('@/application/workspace/ExportDocument',()=>({renderDesignPage:render}));
const page=(id:string):DesignPage=>({id,title:id,width:210,height:297,background:'#ffffff',elements:[]});
describe('fixed PDF preview',()=>{
 it('uses the export renderer and ignores an older page render resolving later',async()=>{
  let first!:(canvas:HTMLCanvasElement)=>void;render.mockImplementationOnce(()=>new Promise(resolve=>{first=resolve;}));
  const wrapper=mount(DesignPrintPreview,{props:{page:page('first')}});const latest=document.createElement('canvas');render.mockResolvedValueOnce(latest);
  await wrapper.setProps({page:page('second')});await flushPromises();const old=document.createElement('canvas');first(old);await flushPromises();
  expect(render).toHaveBeenLastCalledWith(page('second'),240);expect(wrapper.get('canvas').attributes('aria-label')).toBe('second exact PDF preview');
  expect(old.width).toBe(0);expect(wrapper.attributes('style')).toContain('aspect-ratio: 210 / 297');wrapper.unmount();
 });
 it('surfaces unavailable source images instead of presenting an export-ready preview',async()=>{
  const notices=vi.spyOn(feedbackBus,'notify');
  render.mockRejectedValueOnce(new Error('Private source path unavailable.'));const wrapper=mount(DesignPrintPreview,{props:{page:page('missing')}});await flushPromises();
  expect(notices).toHaveBeenCalledWith(expect.objectContaining({description:'Unable to render this page.'}));
  expect(wrapper.text()).toContain('Preview unavailable');expect(wrapper.text()).not.toContain('Private source');
  expect(wrapper.find('canvas').exists()).toBe(false);
  render.mockResolvedValueOnce(document.createElement('canvas'));await wrapper.get('button').trigger('click');await flushPromises();
  expect(wrapper.find('canvas').exists()).toBe(true);expect(wrapper.text()).not.toContain('Preview unavailable');wrapper.unmount();notices.mockRestore();
 });
});
