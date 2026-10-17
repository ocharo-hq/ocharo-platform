import { createAppRouter } from '@wow-two-beta/fe/router';

/** Code-split, directly addressable catalogue pages. */
export const catalogueRouter = createAppRouter(
  [
    { path: '/', lazy: () => import('../presentation/catalogue/ProductsPage.vue'), handle: { title: 'Products' } },
    {
      path: '/products/new',
      lazy: () => import('../presentation/catalogue/ProductEditorPage.vue'),
      handle: { title: 'New product' },
    },
    {
      path: '/products/:id',
      lazy: () => import('../presentation/catalogue/ProductEditorPage.vue'),
      handle: { title: 'Product' },
    },
    {
      path: '/collections',
      lazy: () => import('../presentation/catalogue/CollectionsPage.vue'),
      handle: { title: 'Collections' },
    },
    {
      path: '/catalogues',
      lazy: () => import('../presentation/design/CatalogueGalleryPage.vue'),
      handle: { title: 'Catalogues' },
    },
    {
      path: '/catalogues/:id/web',
      lazy: () => import('../presentation/design/CatalogueWebDesignerPage.vue'),
      handle: { title: 'Web designer' },
    },
    {
      path: '/catalogues/:id/pdf',
      lazy: () => import('../presentation/design/CataloguePdfDesignerPage.vue'),
      handle: { title: 'PDF designer' },
    },
    {
      path: '/c/:slug',
      lazy: () => import('../presentation/public/CatalogueEditionPage.vue'),
      handle: { title: 'Catalogue' },
    },
    { path: '/copy', lazy: () => import('../presentation/catalogue/CopyPage.vue'), handle: { title: 'Translations' } },
    { path: '/stock', lazy: () => import('../presentation/catalogue/StockPage.vue'), handle: { title: 'Stock' } },
    { path: '/pricing', lazy: () => import('../presentation/catalogue/PricingPage.vue'), handle: { title: 'Prices' } },
    {
      path: '/print',
      lazy: () => import('../presentation/catalogue/PrintPage.vue'),
      handle: { title: 'Print catalogue' },
    },
    {
      path: '/settings',
      lazy: () => import('../presentation/catalogue/SettingsPage.vue'),
      handle: { title: 'Settings' },
    },
    {
      path: '/:pathMatch(.*)*',
      lazy: () => import('../presentation/catalogue/NotFoundPage.vue'),
      handle: { title: 'Page not found' },
    },
  ],
  { titleSuffix: 'Ocharo Catalogue', routePersistence: false },
);
