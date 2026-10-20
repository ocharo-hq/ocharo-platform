import { createAppRouter, type RouteConfig, type RouteHandle } from '@wow-two-beta/fe/router';

import { useUiLocale } from '../application/shared/UseUiLocale';
import type { CatalogueSection } from '../domain/shared/CatalogueSections';
import AppLayout from './AppLayout.vue';
import PublicReaderLayout from './PublicReaderLayout.vue';

/** The app name after each place's title in the document title. */
export const DocumentTitleSuffix = 'Ocharo Catalogue';

const { t } = useUiLocale();

/**
 * A place's metadata. `label` is the English source the breadcrumb and the document title translate; `section` names
 * the sidebar entry the place belongs to, `null` for a place outside every section.
 */
function place(label: string, section: CatalogueSection | null): RouteHandle {
  return { label, section, title: () => t(label) };
}

/**
 * Code-split, directly addressable places. Every authoring place renders inside the operator frame; the published
 * reader keeps its own header. Prices, product copy and the print sheet are retained product-wide tools: they sit
 * under Products and stay out of the sidebar, as the product editor and the PDF designer supersede them.
 */
export const catalogueRoutes: RouteConfig = [
  {
    path: '/c/:slug',
    layout: PublicReaderLayout,
    children: [
      {
        index: true,
        lazy: () => import('../presentation/public/CatalogueEditionPage.vue'),
        handle: place('Catalogue', null),
      },
    ],
  },
  {
    layout: AppLayout,
    children: [
      {
        index: true,
        lazy: () => import('../presentation/catalogue/ProductsPage.vue'),
        handle: place('Products', 'products'),
      },
      {
        path: 'products/new',
        lazy: () => import('../presentation/catalogue/ProductEditorPage.vue'),
        handle: place('New product', 'products'),
      },
      {
        path: 'products/:id',
        lazy: () => import('../presentation/catalogue/ProductEditorPage.vue'),
        handle: place('Product', 'products'),
      },
      {
        path: 'copy',
        lazy: () => import('../presentation/catalogue/CopyPage.vue'),
        handle: place('Product copy', 'products'),
      },
      {
        path: 'pricing',
        lazy: () => import('../presentation/catalogue/PricingPage.vue'),
        handle: place('Prices', 'products'),
      },
      {
        path: 'print',
        lazy: () => import('../presentation/catalogue/PrintPage.vue'),
        handle: place('Catalogue sheet', 'products'),
      },
      {
        path: 'collections',
        lazy: () => import('../presentation/catalogue/CollectionsPage.vue'),
        handle: place('Collections', 'collections'),
      },
      {
        path: 'catalogues',
        lazy: () => import('../presentation/design/CatalogueGalleryPage.vue'),
        handle: place('Catalogues', 'catalogues'),
      },
      {
        path: 'catalogues/:id/web',
        lazy: () => import('../presentation/design/CatalogueWebDesignerPage.vue'),
        handle: place('Web designer', 'catalogues'),
      },
      {
        path: 'catalogues/:id/pdf',
        lazy: () => import('../presentation/design/CataloguePdfDesignerPage.vue'),
        handle: place('PDF designer', 'catalogues'),
      },
      {
        path: 'stock',
        lazy: () => import('../presentation/catalogue/StockPage.vue'),
        handle: place('Stock', 'stock'),
      },
      {
        path: 'settings',
        lazy: () => import('../presentation/catalogue/SettingsPage.vue'),
        handle: place('Settings', 'settings'),
      },
      {
        path: ':pathMatch(.*)*',
        lazy: () => import('../presentation/catalogue/NotFoundPage.vue'),
        handle: place('Page not found', null),
      },
    ],
  },
];

/** Creates the router over `catalogueRoutes`; tests pass memory history. */
export function createCatalogueRouter(options: Parameters<typeof createAppRouter>[1] = {}) {
  return createAppRouter(catalogueRoutes, { titleSuffix: DocumentTitleSuffix, routePersistence: false, ...options });
}
