import './index.css';

import { createApp } from 'vue';

import { DefaultRetryPolicy } from '@wow-two-beta/fe/foundation/resilience';
import { createQueryClient, queryPlugin } from '@wow-two-beta/fe/query';

import AppRoot from './AppRoot.vue';
import { createDeskRouter } from './router';
import { initializeUiLocale } from '@/application/shared/UseUiLocale';

initializeUiLocale();

const queryClient = createQueryClient({ retry: { ...DefaultRetryPolicy, maxRetries: 0 } });
const router = createDeskRouter();
const app = createApp(AppRoot).use(queryPlugin(queryClient)).use(router);
app.onUnmount(() => queryClient.clear());
// Pages read their address once when they open, so the first route resolves before anything mounts.
void router.isReady().then(() => app.mount('#app'));

if (import.meta.hot) import.meta.hot.dispose(() => app.unmount());
