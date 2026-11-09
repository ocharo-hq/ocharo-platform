import './index.css';

import { createApp } from 'vue';
import { queryPlugin } from '@wow-two-beta/fe/query';

import { loadOcharoApps, ocharoAppsPlugin, reportOcharoAppsStartup } from '../application/shared/UseOcharoApps';
import { initializeUiLocale } from '../application/shared/UseUiLocale';
import AppRoot from './AppRoot.vue';
import { createCatalogueQueryClient } from './CatalogueQueryClient';
import { createCatalogueRouter, isPublicReaderPath } from './CatalogueRouter';

initializeUiLocale();

/** The published reader shows no app menu and reads no operator session, so it waits for neither. */
const publicReader = isPublicReaderPath(window.location.pathname);
// The app menu's runtime settings load first, within a bounded wait; a failure falls back to the build-time addresses.
// The static splash in `index.html` covers the wait. Once mounted, the root resolves the session and the frame's guard
// waits for it: without a session the browser goes to sign in.
void (publicReader ? Promise.resolve(null) : loadOcharoApps()).then((links) => {
  const app = createApp(AppRoot, { opensOnReader: publicReader })
    .use(createCatalogueRouter())
    .use(queryPlugin(createCatalogueQueryClient()));
  if (links) app.use(ocharoAppsPlugin(links));
  app.mount('#root');
  // The feedback host subscribes as the app mounts, so startup problems are reported once it is up.
  if (links) reportOcharoAppsStartup(links);
});
