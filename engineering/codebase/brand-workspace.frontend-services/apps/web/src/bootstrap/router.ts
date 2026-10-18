import { defineComponent } from 'vue';

import { createAppRouter } from '@wow-two-beta/fe/router';

import { useUiLocale } from '@/application/shared/UseUiLocale';
import { deskSectionLabel } from '@/domain/workspace/DeskSections';
import { readWorkspaceLocation } from '@/domain/workspace/Models';

/** Renders nothing: the workspace page reads the route itself, so the frame stays mounted while sections swap. */
const RouteOutlet = defineComponent({ name: 'RouteOutlet', render: () => null });

const { t } = useUiLocale();

/**
 * Desk's one route. Every address resolves inside the workspace page, which parses it into a section; the router only
 * moves between them without reloading and names the document after the section.
 */
export function createDeskRouter() {
  return createAppRouter(
    [
      {
        path: '*',
        component: RouteOutlet,
        handle: { title: (route) => t(deskSectionLabel(readWorkspaceLocation(route.path).section)) },
      },
    ],
    { titleSuffix: 'Ocharo Desk', routePersistence: false, scrollRestoration: false },
  );
}
