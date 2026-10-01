import { flushPromises, mount } from '@vue/test-utils';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import DesignPreflightReview from '@/presentation/workspace/DesignPreflightReview.vue';
import type { DesignPreflightIssue } from '@/domain/workspace/DesignPreflight';
const issue: DesignPreflightIssue = { pageId: 'second', pageNumber: 2, pageTitle: 'Collection', elementId: 'price', elementLabel: 'Quantity price', severity: 'warning', code: 'text-overflow', message: 'Text is clipped.' };
beforeEach(() => {
  Object.defineProperty(HTMLDialogElement.prototype, 'showModal', { configurable: true, value: vi.fn() });
  Object.defineProperty(HTMLDialogElement.prototype, 'close', { configurable: true, value: vi.fn() });
});
afterEach(() => vi.restoreAllMocks());
describe('export issue review', () => {
  it('requires acknowledgement for the current warning before continuing', async () => {
    const wrapper = mount(DesignPreflightReview, { props: { review: { report: { pageCount: 1, issues: [issue] }, actionLabel: 'Download PDF', unsaved: true, inspectionOnly: false } } });
    await flushPromises(); expect(wrapper.text()).toContain('unsaved edits');
    const button = wrapper.get('.preflight-continue'); expect(button.attributes('disabled')).toBeDefined();
    await wrapper.get('input[type=checkbox]').setValue(true); await button.trigger('click');
    expect(wrapper.emitted('resolve')).toEqual([[true]]); wrapper.unmount();
  });
  it('cannot override missing media and returns the exact issue to the editor', async () => {
    const missing = { ...issue, severity: 'error' as const, code: 'missing-image' as const };
    const wrapper = mount(DesignPreflightReview, { props: { review: { report: { pageCount: 1, issues: [missing] }, actionLabel: 'Download PDF', unsaved: false, inspectionOnly: false } } });
    expect(wrapper.find('.preflight-continue').exists()).toBe(false);
    await wrapper.get('[aria-label^="Inspect issue"]').trigger('click');
    expect(wrapper.emitted('resolve')).toEqual([[false]]); expect(wrapper.emitted('inspect')).toEqual([[missing]]); wrapper.unmount();
  });
  it('reports a clean inspection without asking to publish or download', () => {
    const wrapper = mount(DesignPreflightReview, { props: { review: { report: { pageCount: 16, issues: [] }, actionLabel: 'Done', unsaved: false, inspectionOnly: true } } });
    expect(wrapper.get('[role=status]').text()).toContain('No page-edge'); expect(wrapper.find('.preflight-continue').exists()).toBe(false); wrapper.unmount();
  });
});
