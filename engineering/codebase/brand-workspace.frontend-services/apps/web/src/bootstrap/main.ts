import './index.css';

import { createApp } from 'vue';

import { DefaultRetryPolicy } from '@wow-two-beta/ui-vue/foundation/resilience';
import { createQueryClient, queryPlugin } from '@wow-two-beta/ui-vue/query';

import AppRoot from './AppRoot.vue';

const queryClient = createQueryClient({ retry: { ...DefaultRetryPolicy, maxRetries: 0 } });
const app = createApp(AppRoot).use(queryPlugin(queryClient));
app.onUnmount(() => queryClient.clear());
app.mount('#app');

if (import.meta.hot) import.meta.hot.dispose(() => app.unmount());
