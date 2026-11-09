import './index.css';

import { createApp } from 'vue';
import { queryPlugin } from '@wow-two-beta/fe/query';

import { loadOcharoApps, ocharoAppsPlugin, reportOcharoAppsStartup } from '../application/shared/UseOcharoApps';
import { initializeUiLocale } from '../application/shared/UseUiLocale';
import AppRoot from './AppRoot.vue';
import { createBrandQueryClient } from './BrandQueryClient';
import { createBrandRouter } from './BrandRouter';

initializeUiLocale();

// The app menu's runtime settings load first, within a bounded wait; a failure falls back to the build-time addresses.
// The static splash in `index.html` covers the wait. Once mounted, the root resolves the session and the frame's guard
// waits for it: without a session the browser goes to sign in.
void loadOcharoApps().then((startup) => {
  createApp(AppRoot)
    .use(createBrandRouter())
    .use(queryPlugin(createBrandQueryClient()))
    .use(ocharoAppsPlugin(startup))
    .mount('#root');
  // The toast host subscribes as the app mounts, so startup problems are reported once it is up.
  reportOcharoAppsStartup(startup);
});
