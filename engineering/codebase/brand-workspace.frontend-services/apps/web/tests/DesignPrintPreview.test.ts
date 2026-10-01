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
  render.mockRejectedValueOnce(new Error('An image is unavailable.'));const wrapper=mount(DesignPrintPreview,{props:{page:page('missing')}});await flushPromises();
  expect(wrapper.get('[role=alert]').text()).toContain('unavailable');expect(wrapper.find('canvas').exists()).toBe(false);wrapper.unmount();
 });
});
