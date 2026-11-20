import {
  createAppRouter,
  type GuardDecision,
  type RouteConfig,
  type RouteGuard,
  type RouteHandle,
} from '@wow-two-beta/fe/router';

import { useUiLocale } from '../application/shared/UseUiLocale';
import { eventsPath } from '../domain/events/Events';
import type { EventsSection } from '../domain/shared/EventsSections';
import { identityBridge, identityStrategy, SignInErrorParameter } from '../integration/common/IdentitySession';
import AppLayout from './AppLayout.vue';

/** The app name after each place's title in the document title. */
export const DocumentTitleSuffix = 'Ocharo Events';

const { t } = useUiLocale();

/**
 * A place's metadata. `label` is the English source the breadcrumb and the document title translate; `section` names
 * the sidebar entry the place belongs to, `null` for a place outside every section.
 */
function place(label: string, section: EventsSection | null): RouteHandle {
  return { label, section, title: () => t(label) };
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

/** Code-split, directly addressable places inside the frame, each under the section it opens from; an unknown path is not found. */
export const eventsRoutes: RouteConfig = [
  {
    layout: AppLayout,
    guard: requireEventsSession,
    children: [
      {
        index: true,
        guard: openSessionOrganization,
        lazy: () => import('../presentation/events/pages/NotFoundPage.vue'),
        handle: place('Page not found', null),
      },
      {
        path: 'organizations/:organizationId/events/:view(.*)*',
        lazy: () => import('../presentation/events/pages/EventsRoute.vue'),
        handle: place('Feed', 'feed'),
      },
      {
        path: 'organizations/:organizationId/calendar',
        lazy: () => import('../presentation/events/pages/CalendarRoute.vue'),
        handle: place('Calendar', 'calendar'),
      },
      {
        path: 'organizations/:organizationId/reminders',
        lazy: () => import('../presentation/reminders/pages/RemindersRoute.vue'),
        handle: place('Reminders', 'reminders'),
      },
      {
        path: 'organizations/:organizationId/telegram-delivery',
        lazy: () => import('../presentation/delivery/pages/TelegramDeliveryRoute.vue'),
        handle: place('Telegram delivery', 'telegram'),
      },
      {
        path: ':pathMatch(.*)*',
        lazy: () => import('../presentation/events/pages/NotFoundPage.vue'),
        handle: place('Page not found', null),
      },
    ],
  },
];

/** Creates the router over `eventsRoutes`; tests pass memory history. */
export function createEventsRouter(options: Parameters<typeof createAppRouter>[1] = {}) {
  return createAppRouter(eventsRoutes, { titleSuffix: DocumentTitleSuffix, routePersistence: false, ...options });
}
