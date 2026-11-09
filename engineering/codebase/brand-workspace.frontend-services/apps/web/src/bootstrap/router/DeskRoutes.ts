import {
  createAppRouter,
  lazyRoute,
  type AppRoute,
  type CreateAppRouterOptions,
  type LazyRoute,
  type RouteConfig,
} from '@wow-two-beta/fe/router';

import { deskSectionLabel, type DeskSection } from '@/domain/common/DeskSections';

import AppLayout from '../AppLayout.vue';
import {
  designPage,
  eventsPage,
  organizationsPage,
  place,
  plainPage,
  sourcingPage,
  workspacePage,
  type DeskPageProps,
} from './DeskPlaces';

/** The app name after each place's title in the document title. */
export const DocumentTitleSuffix = 'Ocharo Desk';

/** The name of the frame's layout route, the parent of every place. */
export const DeskLayoutRoute = 'desk';

/** The label of the place for an address Desk does not know. */
export const NotFoundLabel = 'Page not found';

/** One organization's address; its id is a UUID, as the workspace location reads it. */
const Organization = 'organizations/:organizationId([0-9a-f-]{36})';

/** A code-split place inside one Desk section, labelled by that section. */
function sectionPlace(path: string, section: DeskSection, page: LazyRoute, pageProps: DeskPageProps): AppRoute {
  return { path, lazy: lazyRoute(page), handle: place(section, deskSectionLabel(section), pageProps) };
}

/**
 * Desk's places. Every place renders inside the operator frame (`AppLayout`), which keeps the session, the
 * organization's reads and the sidebar mounted while places swap. An address outside these places opens the not-found
 * page; System sits outside every organization, and the organization chooser opens at the start and the Catalogue entry.
 */
export const deskRoutes: RouteConfig = [
  {
    id: DeskLayoutRoute,
    layout: AppLayout,
    children: [
      { index: true, lazy: lazyRoute(() => import('@/presentation/access/pages/OrganizationPage.vue')), handle: place('overview', deskSectionLabel('overview'), organizationsPage(false)) },
      { path: 'organizations', lazy: lazyRoute(() => import('@/presentation/access/pages/OrganizationPage.vue')), handle: place('overview', deskSectionLabel('overview'), organizationsPage(false)) },
      { path: 'catalogue', lazy: lazyRoute(() => import('@/presentation/access/pages/OrganizationPage.vue')), handle: place('catalogue', deskSectionLabel('catalogue'), organizationsPage(true)) },
      sectionPlace('system', 'system', () => import('@/presentation/system/pages/FoundationPage.vue'), plainPage),
      sectionPlace(Organization, 'overview', () => import('@/presentation/common/pages/OverviewPage.vue'), workspacePage),
      sectionPlace(`${Organization}/events/:view(.*)*`, 'events', () => import('@/presentation/events/pages/EventsPage.vue'), eventsPage),
      sectionPlace(`${Organization}/sourcing/:view(.*)*`, 'sourcing', () => import('@/presentation/sourcing/pages/SourcingPage.vue'), sourcingPage),
      sectionPlace(`${Organization}/products`, 'products', () => import('@/presentation/commerce/pages/CommercePage.vue'), workspacePage),
      sectionPlace(`${Organization}/catalogue`, 'catalogue', () => import('@/presentation/design/pages/CatalogueWorkspacePage.vue'), designPage('catalogue')),
      sectionPlace(`${Organization}/business-cards`, 'business-cards', () => import('@/presentation/design/pages/DesignEditorPage.vue'), designPage('business-card')),
      sectionPlace(`${Organization}/media`, 'media', () => import('@/presentation/media/pages/MediaPage.vue'), workspacePage),
      sectionPlace(`${Organization}/brands`, 'brands', () => import('@/presentation/brands/pages/BrandsPage.vue'), workspacePage),
      sectionPlace(`${Organization}/content`, 'content', () => import('@/presentation/content/pages/DraftListPage.vue'), workspacePage),
      sectionPlace(`${Organization}/drafts`, 'content', () => import('@/presentation/content/pages/DraftListPage.vue'), workspacePage),
      sectionPlace(`${Organization}/drafts/:draftId`, 'content', () => import('@/presentation/content/pages/DraftEditorPage.vue'), workspacePage),
      sectionPlace(`${Organization}/integrations`, 'integrations', () => import('@/presentation/integrations/pages/IntegrationsPage.vue'), workspacePage),
      {
        path: ':pathMatch(.*)*',
        lazy: lazyRoute(() => import('@/presentation/common/pages/NotFoundPage.vue')),
        handle: place(null, NotFoundLabel, plainPage),
      },
    ],
  },
];

/** Creates Desk's router over `deskRoutes`; the frame keeps its own scroll, so the router restores none. */
export function createDeskRouter(options: CreateAppRouterOptions = {}) {
  return createAppRouter(deskRoutes, {
    titleSuffix: DocumentTitleSuffix,
    routePersistence: false,
    scrollRestoration: false,
    ...options,
  });
}
