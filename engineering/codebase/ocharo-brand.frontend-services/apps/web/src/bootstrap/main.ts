import { ssoClient } from '../integration/common/OcharoSsoClient';
import { createApp } from 'vue';
import AppRoot from './AppRoot.vue';
import { brandRouter } from './BrandRouter';
import './index.css';

const app = createApp(AppRoot);
app.use(brandRouter);
void Promise.all([brandRouter.isReady(), ssoClient.initialize()]).then(() => {
  app.mount('#root');
});
