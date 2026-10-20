import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, type VueWrapper } from '@vue/test-utils';
import { ResultExtensions } from '@wow-two-beta/fe/foundation/results';
import { catalogueApi } from '../src/integration/catalogue';
import { designApi } from '../src/integration/design/DesignApi';
import { confirmation, resolveConfirmation } from '../src/application/common/UseFeedback';
import { useUiLocale } from '../src/application/shared/UseUiLocale';
import { documentFixture, sourceFixture } from './NativeDesignFixture';
import { mountCatalogue, SelectStub } from './helpers/CatalogueApp';

vi.mock('../src/integration/design/DesignApi', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../src/integration/design/DesignApi')>()),
  designApi: {
    list: vi.fn(),
    get: vi.fn(),
    source: vi.fn(),
    save: vi.fn(),
    duplicate: vi.fn(),
    archive: vi.fn(),
    revisions: vi.fn(),
    releases: vi.fn(),
    preparation: vi.fn(),
    prepare: vi.fn(),
    publish: vi.fn(),
    withdraw: vi.fn(),
  },
}));

let wrapper: VueWrapper | undefined;

beforeEach(() => {
  sessionStorage.clear();
  localStorage.clear();
  useUiLocale().setUiLocale('en');
  vi.mocked(designApi.get).mockResolvedValue(documentFixture());
  vi.mocked(designApi.source).mockResolvedValue(sourceFixture());
  vi.mocked(designApi.revisions).mockResolvedValue([]);
  vi.mocked(designApi.releases).mockResolvedValue([]);
  vi.spyOn(catalogueApi, 'getSettings').mockResolvedValue(
    ResultExtensions.ok({
      settings: {
        brandName: 'Ocharo',
        currency: 'UZS',
        priceNote: '',
        sizePreset: [],
        contactLines: [],
        garmentTypes: [],
      },
      revision: 1,
      sourceRoot: null,
    }),
  );
  vi.spyOn(catalogueApi, 'stockRows').mockResolvedValue(ResultExtensions.ok([]));
});
afterEach(() => {
  if (confirmation.value) resolveConfirmation(false);
  wrapper?.unmount();
  wrapper = undefined;
  document.body.innerHTML = '';
  vi.unstubAllGlobals();
});

describe('Unsaved designer edits inside the frame', () => {
  it('asks before a sidebar section change replaces an edited web designer', async () => {
    const mounted = await mountCatalogue(`/catalogues/${documentFixture().id}/web`, {
      stubs: { CatalogueSelect: SelectStub },
    });
    wrapper = mounted.wrapper;
    await vi.waitFor(() => expect(wrapper!.find('input[maxlength="160"]').exists()).toBe(true));
    await wrapper.get('input[maxlength="160"]').setValue('Local catalogue title');
    const link = wrapper.get('aside').get('[aria-label="Catalogue sections"]').get('a[href="/stock"]');

    await link.trigger('click');
    await vi.waitFor(() => expect(confirmation.value).not.toBeNull());
    resolveConfirmation(false);
    await flushPromises();

    expect(mounted.router.currentRoute.value.path).toBe(`/catalogues/${documentFixture().id}/web`);
    expect(wrapper.get('input[maxlength="160"]').element).toHaveProperty('value', 'Local catalogue title');
    expect(wrapper.get('aside [aria-current="page"]').text()).toBe('Catalogues');
  });
});
