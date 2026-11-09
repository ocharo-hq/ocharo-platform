import type { RouteHandle } from '@wow-two-beta/fe/router';
import type { RouteLocationNormalizedLoaded } from 'vue-router';

import type { AccessControls } from '@/application/common/hooks/UseAccess';
import { useUiLocale } from '@/application/common/hooks/UseUiLocale';
import type { WorkspaceControls } from '@/application/common/hooks/UseWorkspace';
import type { DeskSection } from '@/domain/common/DeskSections';
import { readDesignTarget } from '@/domain/common/EditorNavigation';
import { readEventsAddress } from '@/domain/events/Events';
import { readSourcingAddress } from '@/domain/sourcing/models/Sourcing';

/** What the layout hands every place's page: the document's session controls and its organization's reads. */
export interface DeskPageContext {
  readonly access: AccessControls;
  readonly workspace: WorkspaceControls;
}

/** What a place reads off its route: the address and the matched place's metadata. */
export type DeskRoute = Pick<RouteLocationNormalizedLoaded, 'path' | 'fullPath' | 'meta'>;

/** Decodes a place's page props from its route and the layout's context; the page never reads the router itself. */
export type DeskPageProps = (route: DeskRoute, context: DeskPageContext) => Record<string, unknown>;

/** A place's route metadata: the sidebar section it belongs to (`null` outside every section) and its page props. */
export interface DeskPlace extends RouteHandle {
  readonly section: DeskSection | null;
  /** The English source the breadcrumb and the document title translate. */
  readonly label: string;
  readonly pageProps: DeskPageProps;
}

const { t } = useUiLocale();

/** A place's metadata; the document title is its translated label. */
export function place(section: DeskSection | null, label: string, pageProps: DeskPageProps): DeskPlace {
  return { section, label, pageProps, title: () => t(label) };
}

/** The deepest matched place, or `null` while nothing is matched. */
export function readDeskPlace(route: DeskRoute): DeskPlace | null {
  const { section, label, pageProps } = route.meta;
  return typeof label === 'string' && typeof pageProps === 'function'
    ? { section: (section ?? null) as DeskSection | null, label, pageProps: pageProps as DeskPageProps }
    : null;
}

/** The props a place's page renders with. */
export function deskPageProps(route: DeskRoute, context: DeskPageContext): Record<string, unknown> {
  return readDeskPlace(route)?.pageProps(route, context) ?? {};
}

/** A page that takes no props: diagnostics and the not-found place. */
export const plainPage: DeskPageProps = () => ({});

/** A page over the document's session and its organization's workspace reads. */
export const workspacePage: DeskPageProps = (_route, { access, workspace }) => ({ access, workspace });

/** The organization chooser; the Catalogue entry address opens it for the Catalogue designer. */
export const organizationsPage = (catalogueEntry: boolean): DeskPageProps =>
  (route, context) => ({ ...workspacePage(route, context), catalogueEntry });

/** A saved-design editor, opened on the document and page its query names. */
export const designPage = (kind: 'catalogue' | 'business-card'): DeskPageProps => (route, context) => {
  const target = readDesignTarget(new URL(route.fullPath, 'https://desk.invalid').search);
  return {
    ...workspacePage(route, context),
    ...(kind === 'business-card' ? { kind } : { initialMode: target.mode }),
    initialDocumentId: target.documentId,
    initialPageId: target.pageId,
    navigationError: target.error,
  };
};

/** Sourcing, opened on the list, inbox or supplier its address names. */
export const sourcingPage: DeskPageProps = (route, context) =>
  ({ ...workspacePage(route, context), initialLocation: readSourcingAddress(route.path) ?? undefined });

/** Events, opened on the feed or the event its address names. */
export const eventsPage: DeskPageProps = (route, context) =>
  ({ ...workspacePage(route, context), initialLocation: readEventsAddress(route.path) ?? undefined });
