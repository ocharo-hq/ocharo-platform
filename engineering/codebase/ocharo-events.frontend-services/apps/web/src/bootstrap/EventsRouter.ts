import {
  createAppRouter,
  type GuardDecision,
  type RouteConfig,
  type RouteGuard,
  type RouteHandle,
} from '@wow-two-beta/fe/router';

import { useUiLocale } from '../application/shared/UseUiLocale';
import { eventsPath } from '../domain/events/Events';
import { identityBridge, identityStrategy, SignInErrorParameter } from '../integration/common/IdentitySession';
import AppLayout from './AppLayout.vue';

/** The app name after each place's title in the document title. */
export const DocumentTitleSuffix = 'Ocharo Events';

const { t } = useUiLocale();

/** A place's metadata. `label` is the English source the breadcrumb and the document title translate. */
function place(label: string): RouteHandle {
  return { label, title: () => t(label) };
}

/**
 * Lets a signed-in person into the frame once the session settles. Without a session it starts sign-in at Ocharo's
 * Identity and holds the navigation while the browser leaves, so the splash stays up. A failed sign-in's return
 * (`signInError`) opens the frame instead, which says so and offers sign-in again; a misconfigured client never loops.
 */
export const requireEventsSession: RouteGuard = async ({ to }): Promise<GuardDecision> => {
  if (await identityBridge.isAuthenticated()) return true;
  if (SignInErrorParameter in to.query) return true;
  await identityStrategy.signIn?.(to.fullPath, {});
  return new Promise<never>(() => {});
};

/** Sends `/` to the events of the session's organization; a person in no organization stays on the not-found place. */
export const openSessionOrganization: RouteGuard = (): GuardDecision => {
  const organizationId = identityBridge.getSession().user?.organization?.id;
  if (!organizationId) return true;
  return { redirect: eventsPath(organizationId.toLowerCase(), { view: 'feed', eventId: null }) };
};

/** Code-split, directly addressable places inside the frame; an unknown path is not found. */
export const eventsRoutes: RouteConfig = [
  {
    layout: AppLayout,
    guard: requireEventsSession,
    children: [
      {
        index: true,
        guard: openSessionOrganization,
        lazy: () => import('../presentation/events/pages/NotFoundPage.vue'),
        handle: place('Page not found'),
      },
      {
        path: 'organizations/:organizationId/events/:view(.*)*',
        lazy: () => import('../presentation/events/pages/EventsRoute.vue'),
        handle: place('Events'),
      },
      {
        path: ':pathMatch(.*)*',
        lazy: () => import('../presentation/events/pages/NotFoundPage.vue'),
        handle: place('Page not found'),
      },
    ],
  },
];

/** Creates the router over `eventsRoutes`; tests pass memory history. */
export function createEventsRouter(options: Parameters<typeof createAppRouter>[1] = {}) {
  return createAppRouter(eventsRoutes, { titleSuffix: DocumentTitleSuffix, routePersistence: false, ...options });
}
