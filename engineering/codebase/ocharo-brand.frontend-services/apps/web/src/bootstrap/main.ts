import { createApp } from 'vue';
import AppRoot from './AppRoot.vue';
import { brandRouter } from './BrandRouter';
import './index.css';

const app = createApp(AppRoot);
app.use(brandRouter);
void brandRouter.isReady().then(() => {
  app.mount('#root');
});
