import { ssoClient } from '../integration/common/OcharoSsoClient';
import { createApp } from 'vue';
import AppRoot from './AppRoot.vue';
import { catalogueRouter } from './CatalogueRouter';
import './index.css';
import { initializeUiLocale } from '../application/shared/UseUiLocale';

initializeUiLocale();

const app = createApp(AppRoot);
app.use(catalogueRouter);
void Promise.all([
  catalogueRouter.isReady(),
  window.location.pathname.startsWith('/c/') ? Promise.resolve() : ssoClient.initialize(),
]).then(() => {
  app.mount('#root');
});
