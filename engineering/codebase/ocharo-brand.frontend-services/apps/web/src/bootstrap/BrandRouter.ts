import {
  createAppRouter,
  type GuardDecision,
  type RouteConfig,
  type RouteGuard,
  type RouteHandle,
} from '@wow-two-beta/fe/router';

import { useUiLocale } from '../application/shared/UseUiLocale';
import type { BrandSection } from '../domain/shared/BrandSections';
import { identityBridge, identityStrategy, SignInErrorParameter } from '../integration/common/IdentitySession';
import AppLayout from './AppLayout.vue';

/** The app name after each place's title in the document title. */
export const DocumentTitleSuffix = 'Ocharo Brand';

const { t } = useUiLocale();

/**
 * A place's metadata. `label` is the English source the breadcrumb and the document title translate; `section` names
 * the sidebar entry the place belongs to, `null` for a place outside every section.
 */
function place(label: string, section: BrandSection | null): RouteHandle {
  return { label, section, title: () => t(label) };
}

/**
 * Lets a signed-in person into the frame once the session settles. Without a session it starts sign-in at Ocharo's
 * Identity and holds the navigation while the browser leaves, so the splash stays up. A failed sign-in's return
 * (`signInError`) opens the frame instead, which says so and offers sign-in again; a misconfigured client never loops.
 */
export const requireBrandSession: RouteGuard = async ({ to }): Promise<GuardDecision> => {
  if (await identityBridge.isAuthenticated()) return true;
  if (SignInErrorParameter in to.query) return true;
  await identityStrategy.signIn?.(to.fullPath, {});
  return new Promise<never>(() => {});
};

/** Code-split, directly addressable places, every one inside the operator frame; an unknown path is not found. */
export const brandRoutes: RouteConfig = [
  {
    layout: AppLayout,
    guard: requireBrandSession,
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
        lazy: () => import('../presentation/brand/NotFoundPage.vue'),
        handle: place('Page not found', null),
      },
    ],
  },
];

/** Creates the router over `brandRoutes`; tests pass memory history. */
export function createBrandRouter(options: Parameters<typeof createAppRouter>[1] = {}) {
  return createAppRouter(brandRoutes, { titleSuffix: DocumentTitleSuffix, routePersistence: false, ...options });
}
