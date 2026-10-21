import './index.css';

import { createApp } from 'vue';

import { loadOcharoApps, ocharoAppsPlugin, reportOcharoAppsStartup } from '../application/shared/UseOcharoApps';
import { initializeUiLocale } from '../application/shared/UseUiLocale';
import { ssoClient } from '../integration/common/OcharoSsoClient';
import AppRoot from './AppRoot.vue';
import { createBrandRouter } from './BrandRouter';

initializeUiLocale();

const router = createBrandRouter();
const app = createApp(AppRoot);
app.use(router);
// The app menu's runtime settings load beside the first route and the session, within a bounded wait; a failure falls
// back to the build-time addresses.
void Promise.all([loadOcharoApps(), router.isReady(), ssoClient.initialize()]).then(([startup]) => {
  app.use(ocharoAppsPlugin(startup));
  app.mount('#root');
  // The toast host subscribes as the app mounts, so startup problems are reported once it is up.
  reportOcharoAppsStartup(startup);
});
