import { createAppRouter, type RouteConfig, type RouteHandle } from '@wow-two-beta/fe/router';

import { useUiLocale } from '../application/shared/UseUiLocale';
import type { BrandSection } from '../domain/shared/BrandSections';
import AppLayout from './AppLayout.vue';

/** The app name after each place's title in the document title. */
export const DocumentTitleSuffix = 'Ocharo Brand';

const { t } = useUiLocale();

/**
 * A place's metadata. `label` is the English source the breadcrumb and the document title translate; `section` names
 * the sidebar entry the place belongs to.
 */
function place(label: string, section: BrandSection): RouteHandle {
  return { label, section, title: () => t(label) };
}

/** Code-split, directly addressable places, every one inside the operator frame; an unknown path opens the overview. */
export const brandRoutes: RouteConfig = [
  {
    layout: AppLayout,
    children: [
      {
        index: true,
        lazy: () => import('../presentation/brand/OverviewPage.vue'),
        handle: place('Overview', 'overview'),
      },
      {
        path: 'identity',
        lazy: () => import('../presentation/brand/IdentityPage.vue'),
        handle: place('Identity', 'identity'),
      },
      { path: 'logos', lazy: () => import('../presentation/brand/LogosPage.vue'), handle: place('Logos', 'logos') },
      {
        path: 'appearances',
        lazy: () => import('../presentation/brand/AppearancesPage.vue'),
        handle: place('Appearances', 'appearances'),
      },
      {
        path: 'connect',
        lazy: () => import('../presentation/brand/ConnectPage.vue'),
        handle: place('Connect', 'connect'),
      },
      {
        path: ':pathMatch(.*)*',
        lazy: () => import('../presentation/brand/OverviewPage.vue'),
        handle: place('Overview', 'overview'),
      },
    ],
  },
];

/** Creates the router over `brandRoutes`; tests pass memory history. */
export function createBrandRouter(options: Parameters<typeof createAppRouter>[1] = {}) {
  return createAppRouter(brandRoutes, { titleSuffix: DocumentTitleSuffix, routePersistence: false, ...options });
}
