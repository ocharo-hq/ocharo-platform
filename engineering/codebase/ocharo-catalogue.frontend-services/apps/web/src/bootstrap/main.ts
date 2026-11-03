import './index.css';

import { createApp } from 'vue';

import { startSession } from '../application/common/UseIdentitySession';
import { loadOcharoApps, ocharoAppsPlugin, reportOcharoAppsStartup } from '../application/shared/UseOcharoApps';
import { initializeUiLocale } from '../application/shared/UseUiLocale';
import AppRoot from './AppRoot.vue';
import { createCatalogueRouter } from './CatalogueRouter';

initializeUiLocale();

/** The published reader shows no app menu and no operator session, so it waits for neither. */
const publicReader = window.location.pathname.startsWith('/c/');
const router = createCatalogueRouter();
const app = createApp(AppRoot);
app.use(router);
// The app menu's runtime settings load beside the first route and the session, within a bounded wait; a failure falls
// back to the build-time addresses. A browser without a session leaves for sign-in at Ocharo's Identity instead.
void Promise.all([
  publicReader ? Promise.resolve(null) : loadOcharoApps(),
  router.isReady(),
  publicReader ? Promise.resolve(true) : startSession(),
]).then(([links, , staying]) => {
  if (!staying) return;
  if (links) app.use(ocharoAppsPlugin(links));
  app.mount('#root');
  // The feedback host subscribes as the app mounts, so startup problems are reported once it is up.
  if (links) reportOcharoAppsStartup(links);
});
