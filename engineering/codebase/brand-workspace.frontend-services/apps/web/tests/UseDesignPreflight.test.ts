import { defineComponent } from 'vue';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { useDesignPreflight } from '@/application/workspace/UseDesignPreflight';
import type { DesignSnapshot } from '@/domain/workspace/EditorModels';
const inspect = vi.hoisted(() => vi.fn());
vi.mock('@/integration/workspace/DesignPreflight', () => ({ inspectDesign: inspect }));
const snapshot: DesignSnapshot = { schemaVersion: 1, kind: 'catalogue', locale: 'en', templateId: 'test', pages: [] };
const warning = { pageId: 'first', pageNumber: 1, pageTitle: 'Cover', elementId: 'photo', elementLabel: 'Photo', severity: 'warning', code: 'outside-page', message: 'Cropped' };
let wrapper: VueWrapper, controls: ReturnType<typeof useDesignPreflight>;
function setup() { wrapper = mount(defineComponent({ setup() { controls = useDesignPreflight(); return () => null; } })); }
beforeEach(() => { inspect.mockReset(); });
afterEach(() => wrapper?.unmount());
describe('exact-version preflight approval', () => {
  it('checks the selected scope and proceeds immediately only when clean and saved', async () => {
    inspect.mockResolvedValue({ pageCount: 1, issues: [] }); setup();
    expect(await controls.check(snapshot, { pageIds: ['second'], actionLabel: 'Download PDF' })).toBe(true);
    expect(inspect).toHaveBeenCalledWith(snapshot, ['second'], expect.any(AbortSignal)); expect(controls.review.value).toBe(null);
  });
  it('requires fresh acknowledgement for every operation, including identical snapshots', async () => {
    inspect.mockResolvedValue({ pageCount: 1, issues: [warning] }); setup();
    const first = controls.check(snapshot, { actionLabel: 'Download PDF' }); await flushPromises();
    expect(controls.busy.value).toBe(true); controls.resolve(true); expect(await first).toBe(true);
    const second = controls.check(snapshot, { actionLabel: 'Download PDF' }); await flushPromises();
    expect(inspect).toHaveBeenCalledTimes(2); expect(controls.review.value).not.toBe(null);
    controls.resolve(false); expect(await second).toBe(false);
  });
  it('cannot continue after missing-media errors', async () => {
    inspect.mockResolvedValue({ pageCount: 1, issues: [{ ...warning, code: 'missing-image', severity: 'error' }] }); setup();
    const pending = controls.check(snapshot, { actionLabel: 'Download PDF' }); await flushPromises();
    controls.resolve(true); expect(controls.review.value).not.toBe(null);
    controls.resolve(false); expect(await pending).toBe(false);
  });
  it('requires unsaved-version acknowledgement and cancels unresolved review when navigating away', async () => {
    inspect.mockResolvedValue({ pageCount: 1, issues: [] }); setup();
    const pending = controls.check(snapshot, { actionLabel: 'Download PDF', unsaved: true }); await flushPromises();
    expect(controls.review.value?.unsaved).toBe(true); wrapper.unmount(); expect(await pending).toBe(false);
    expect(controls.busy.value).toBe(false);
  });
});
