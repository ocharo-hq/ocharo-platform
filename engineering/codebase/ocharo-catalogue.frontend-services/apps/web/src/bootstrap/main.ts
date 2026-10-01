import { createApp } from 'vue';
import AppRoot from './AppRoot.vue';
import { catalogueRouter } from './CatalogueRouter';
import './index.css';
import { initializeUiLocale } from '../application/shared/UseUiLocale';

initializeUiLocale();

const app = createApp(AppRoot);
app.use(catalogueRouter);
void catalogueRouter.isReady().then(() => {
  app.mount('#root');
});
