import { createAppRouter } from '@wow-two-beta/ui-vue/router';

/** Code-split, directly addressable catalogue pages. */
export const catalogueRouter = createAppRouter(
  [
    { path: '/', lazy: () => import('../presentation/catalogue/ProductsPage.vue'), handle: { title: 'Mahsulotlar' } },
    {
      path: '/products/new',
      lazy: () => import('../presentation/catalogue/ProductEditorPage.vue'),
      handle: { title: 'Yangi mahsulot' },
    },
    {
      path: '/products/:id',
      lazy: () => import('../presentation/catalogue/ProductEditorPage.vue'),
      handle: { title: 'Mahsulot' },
    },
    {
      path: '/collections',
      lazy: () => import('../presentation/catalogue/CollectionsPage.vue'),
      handle: { title: 'To‘plamlar' },
    },
    { path: '/copy', lazy: () => import('../presentation/catalogue/CopyPage.vue'), handle: { title: 'Matnlar' } },
    { path: '/stock', lazy: () => import('../presentation/catalogue/StockPage.vue'), handle: { title: 'Qoldiq' } },
    { path: '/pricing', lazy: () => import('../presentation/catalogue/PricingPage.vue'), handle: { title: 'Narxlar' } },
    {
      path: '/print',
      lazy: () => import('../presentation/catalogue/PrintPage.vue'),
      handle: { title: 'Katalog varag‘i' },
    },
    {
      path: '/settings',
      lazy: () => import('../presentation/catalogue/SettingsPage.vue'),
      handle: { title: 'Sozlamalar' },
    },
    {
      path: '/:pathMatch(.*)*',
      lazy: () => import('../presentation/catalogue/NotFoundPage.vue'),
      handle: { title: 'Topilmadi' },
    },
  ],
  { titleSuffix: 'Ocharo Katalog', routePersistence: false },
);
