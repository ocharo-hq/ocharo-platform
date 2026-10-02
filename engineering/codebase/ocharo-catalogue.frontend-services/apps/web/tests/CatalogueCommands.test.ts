import { effectScope } from 'vue';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiFailureFactory } from '@wow-two-beta/ui-vue/foundation/http';
import { ResultExtensions } from '@wow-two-beta/ui-vue/foundation/results';
import { catalogueApi } from '../src/integration/catalogue';
import { useProductList } from '../src/application/catalogue/UseProductList';
import { useCollections } from '../src/application/catalogue/UseCollections';
import { useProductEditor } from '../src/application/catalogue/UseProductEditor';
import { useCatalogueAdmin } from '../src/application/catalogue/UseCatalogueAdmin';
import { useCatalogueStore } from '../src/application/catalogue/CatalogueStore';
import type { ProductSummary } from '../src/domain/catalogue/CatalogueModels';
import { productFixture, sourceFixture } from './NativeDesignFixture';
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
describe('Catalogue server-acknowledged commands', () => {
  it('retains acknowledged settings revision when the independent projection refresh fails', async () => {
    const admin = useCatalogueAdmin();
    const store = useCatalogueStore();
    const oldSettings = sourceFixture().settings;
    const nextSettings = { ...oldSettings, brandName: 'Saved Ocharo' };
    store.settings.value = oldSettings;
    store.settingsRevision.value = 2;
    let acknowledge!: (value: Awaited<ReturnType<typeof catalogueApi.saveSettings>>) => void;
    vi.spyOn(catalogueApi, 'saveSettings').mockReturnValue(
      new Promise((resolve) => {
        acknowledge = resolve;
      }),
    );
    vi.spyOn(catalogueApi, 'getSettings').mockResolvedValue(
      ResultExtensions.fail(ApiFailureFactory.create('transport')),
    );
    const command = admin.saveSettings(nextSettings);
    expect(admin.busy.value).toBe(true);
    expect(store.settings.value?.brandName).toBe(oldSettings.brandName);
    expect(store.settingsRevision.value).toBe(2);
    expect(catalogueApi.getSettings).not.toHaveBeenCalled();
    acknowledge(ResultExtensions.ok({ settings: nextSettings, revision: 3, sourceRoot: null }));
    await command;
    expect(catalogueApi.getSettings).toHaveBeenCalledOnce();
    expect(store.settings.value?.brandName).toBe('Saved Ocharo');
    expect(store.settingsRevision.value).toBe(3);
    expect(admin.busy.value).toBe(false);
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
