import './index.css';

import { createApp } from 'vue';

import { DefaultRetryPolicy } from '@wow-two-beta/fe/foundation/resilience';
import { createQueryClient, queryPlugin } from '@wow-two-beta/fe/query';

import AppRoot from './AppRoot.vue';
import { createDeskRouter } from './router';
import { loadOcharoApps, ocharoAppsPlugin, reportOcharoAppsStartup } from '@/application/shared/UseOcharoApps';
import { initializeUiLocale } from '@/application/shared/UseUiLocale';

initializeUiLocale();

const queryClient = createQueryClient({ retry: { ...DefaultRetryPolicy, maxRetries: 0 } });
const router = createDeskRouter();
const app = createApp(AppRoot).use(queryPlugin(queryClient)).use(router);
app.onUnmount(() => queryClient.clear());
// Pages read their address once when they open, so the first route resolves before anything mounts. The app menu's
// runtime settings load alongside it, within a bounded wait; a failure falls back to the build-time addresses.
void Promise.all([loadOcharoApps(), router.isReady()]).then(([ocharoApps]) => {
  app.use(ocharoAppsPlugin(ocharoApps.addresses)).mount('#app');
  // The feedback host subscribes as the app mounts, so startup problems are reported once it is up.
  reportOcharoAppsStartup(ocharoApps);
});

if (import.meta.hot) import.meta.hot.dispose(() => app.unmount());
