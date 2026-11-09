import { defineComponent, effectScope } from 'vue';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import { ApiFailureFactory } from '@wow-two-beta/fe/foundation/http';
import { ResultExtensions } from '@wow-two-beta/fe/foundation/results';
import { catalogueApi } from '../src/integration/catalogue';
import { useProductList } from '../src/application/catalogue/UseProductList';
import { useCollections } from '../src/application/catalogue/UseCollections';
import { useProductEditor } from '../src/application/catalogue/UseProductEditor';
import { useCatalogueAdmin } from '../src/application/catalogue/UseCatalogueAdmin';
import { useCatalogueSettings } from '../src/application/catalogue/UseCatalogueSettings';
import type { ProductSummary } from '../src/domain/catalogue/CatalogueModels';
import { productFixture, sourceFixture } from './NativeDesignFixture';
import { OwnerUser, publishSession } from './helpers/IdentitySessions';
const product: ProductSummary = {
  id: 'nimcha-v01',
  garmentType: 'nimcha',
  variantCode: 'v01',
  status: 'draft',
  sortOrder: 0,
  nameUz: 'Nimcha',
  nameRu: null,
  nameEn: null,
  retailPrice: null,
  wholesalePrice: null,
  pricingStatus: null,
  localesApproved: 0,
  openIssues: 0,
  mediaCount: 1,
  primaryImageUrl: null,
  primaryPreviewUrl: null,
  tags: [],
  revision: 2,
  updatedAt: '2026-01-01T00:00:00Z',
  availability: 'unknown',
  stockTotal: null,
};
beforeEach(() => vi.restoreAllMocks());
let admin!: ReturnType<typeof useCatalogueAdmin>;
let settings!: ReturnType<typeof useCatalogueSettings>;
/** Hands the settings store and the admin commands, as a page sees them, to the test that mounts it. */
const SettingsProbe = defineComponent({
  setup() {
    admin = useCatalogueAdmin();
    settings = useCatalogueSettings();
    return () => null;
  },
});
describe('Catalogue server-acknowledged commands', () => {
  it('retains acknowledged settings revision when the independent projection refresh fails', async () => {
    const oldSettings = sourceFixture().settings;
    const nextSettings = { ...oldSettings, brandName: 'Saved Ocharo' };
    const read = vi
      .spyOn(catalogueApi, 'getSettings')
      .mockResolvedValueOnce(ResultExtensions.ok({ settings: oldSettings, revision: 2, sourceRoot: null }))
      .mockResolvedValue(ResultExtensions.fail(ApiFailureFactory.create('transport')));
    let acknowledge!: (value: Awaited<ReturnType<typeof catalogueApi.saveSettings>>) => void;
    vi.spyOn(catalogueApi, 'saveSettings').mockReturnValue(
      new Promise((resolve) => {
        acknowledge = resolve;
      }),
    );
    const host = mount(SettingsProbe);
    await flushPromises();
    expect(settings.settingsRevision.value).toBe(2);
    const command = admin.saveSettings(nextSettings);
    expect(admin.busy.value).toBe(true);
    expect(settings.settings.value?.brandName).toBe(oldSettings.brandName);
    expect(settings.settingsRevision.value).toBe(2);
    expect(read).toHaveBeenCalledOnce();
    acknowledge(ResultExtensions.ok({ settings: nextSettings, revision: 3, sourceRoot: null }));
    await command;
    await flushPromises();
    expect(read).toHaveBeenCalledTimes(2);
    expect(settings.settings.value?.brandName).toBe('Saved Ocharo');
    expect(settings.settingsRevision.value).toBe(3);
    expect(admin.busy.value).toBe(false);
    host.unmount();
  });
  it('clears the cached settings when the session ends or another person signs in', async () => {
    const read = vi
      .spyOn(catalogueApi, 'getSettings')
      .mockResolvedValue(ResultExtensions.ok({ settings: sourceFixture().settings, revision: 4, sourceRoot: null }));
    const host = mount(SettingsProbe);
    await flushPromises();
    expect(settings.settingsRevision.value).toBe(4);

    read.mockReturnValue(new Promise(() => {}));
    publishSession(null);
    await flushPromises();
    expect(settings.settings.value).toBeNull();

    publishSession({ ...OwnerUser, person: { ...OwnerUser.person, sub: 'person-other' } });
    await flushPromises();
    expect(settings.settings.value).toBeNull();
    host.unmount();
  });
  it('keeps the product grid and selection unchanged until bulk status succeeds', async () => {
    const scope = effectScope();
    const list = scope.run(() => useProductList())!;
    vi.spyOn(catalogueApi, 'listProducts').mockResolvedValue(ResultExtensions.ok([structuredClone(product)]));
    let finish!: (value: Awaited<ReturnType<typeof catalogueApi.bulkStatus>>) => void;
    vi.spyOn(catalogueApi, 'bulkStatus').mockReturnValue(
      new Promise((resolve) => {
        finish = resolve;
      }),
    );
    await list.load();
    list.toggle(product.id);
    const command = list.setStatus('active');
    expect(list.pending.value).toBe('status:active');
    expect(list.loading.value).toBe(false);
    expect(list.products.value[0]?.status).toBe('draft');
    expect([...list.selected.value]).toEqual([product.id]);
    expect(catalogueApi.listProducts).toHaveBeenCalledTimes(1);
    finish(ResultExtensions.fail(ApiFailureFactory.create('http', new Response(null, { status: 500 }))));
    await command;
    expect(list.pending.value).toBe(null);
    expect(list.products.value[0]?.status).toBe('draft');
    expect([...list.selected.value]).toEqual([product.id]);
    expect(catalogueApi.listProducts).toHaveBeenCalledTimes(1);
    scope.stop();
  });
  it('uses the grid query only after mutation acknowledgement', async () => {
    const scope = effectScope();
    const list = scope.run(() => useProductList())!;
    const events: string[] = [];
    vi.spyOn(catalogueApi, 'listProducts').mockImplementation(async () => {
      events.push('query');
      return ResultExtensions.ok([structuredClone(product)]);
    });
    vi.spyOn(catalogueApi, 'bulkStatus').mockImplementation(async () => {
      events.push('acknowledgement');
      return ResultExtensions.ok({ changed: 1, skipped: [] });
    });
    await list.load();
    list.toggle(product.id);
    events.length = 0;
    await list.setStatus('active');
    expect(events).toEqual(['acknowledgement', 'query']);
    scope.stop();
  });
  it('returns failure without removing collections or refreshing their projection', async () => {
    const collections = useCollections();
    vi.spyOn(catalogueApi, 'deleteCollection').mockResolvedValue(
      ResultExtensions.fail(ApiFailureFactory.create('transport')),
    );
    vi.spyOn(catalogueApi, 'listCollections');
    expect(await collections.remove('collection-a')).toBe(false);
    expect(collections.busy.value).toBe(false);
    expect(catalogueApi.listCollections).not.toHaveBeenCalled();
  });
  it('fetches product detail after successful save without reporting refresh failure as a write rollback', async () => {
    const editor = useProductEditor();
    const detail = productFixture();
    editor.startNew('nimcha');
    vi.spyOn(catalogueApi, 'createProduct').mockResolvedValue(ResultExtensions.ok(detail));
    vi.spyOn(catalogueApi, 'getProduct').mockResolvedValue(
      ResultExtensions.fail(ApiFailureFactory.create('transport')),
    );
    vi.spyOn(catalogueApi, 'revisions').mockResolvedValue(ResultExtensions.ok([]));
    expect(await editor.save()).toBe(detail.document.id);
    expect(catalogueApi.getProduct).toHaveBeenCalledWith(detail.document.id);
    expect(editor.detail.value?.revision).toBe(detail.revision);
    expect(editor.isNew.value).toBe(false);
    expect(editor.saving.value).toBe(false);
  });
});
