import { createApp } from 'vue';
import { createRouter, createWebHistory } from 'vue-router';
import AppRoot from './AppRoot.vue';
import AppLayout from './AppLayout.vue';
import GuidesPage from '../presentation/trends/pages/GuidesPage.vue';
import GuidePage from '../presentation/trends/pages/GuidePage.vue';
import ReviewPage from '../presentation/trends/pages/ReviewPage.vue';
import './index.css';

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/',
      component: AppLayout,
      children: [
        { path: '', name: 'guides', component: GuidesPage, meta: { label: 'Trends' } },
        { path: 'guides/:slug', name: 'guide', component: GuidePage, props: true, meta: { label: 'Guide' } },
        { path: 'review', name: 'review', component: ReviewPage, meta: { label: 'Review' } },
      ],
    },
    { path: '/:pathMatch(.*)*', redirect: '/' },
  ],
});

router.afterEach((route) => {
  document.title = route.meta.label === 'Trends' ? 'Ocharo Trends' : `${String(route.meta.label)} · Ocharo Trends`;
});

createApp(AppRoot).use(router).mount('#root');
