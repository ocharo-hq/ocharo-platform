import { createAppRouter } from '@wow-two-beta/ui-vue/router';

/** Code-split brand pages. */
export const brandRouter = createAppRouter(
  [
    { path: '/', lazy: () => import('../presentation/brand/OverviewPage.vue'), handle: { title: 'Umumiy' } },
    { path: '/identity', lazy: () => import('../presentation/brand/IdentityPage.vue'), handle: { title: 'Identitet' } },
    { path: '/logos', lazy: () => import('../presentation/brand/LogosPage.vue'), handle: { title: 'Logotiplar' } },
    {
      path: '/appearances',
      lazy: () => import('../presentation/brand/AppearancesPage.vue'),
      handle: { title: 'Tadbirlar' },
    },
    { path: '/connect', lazy: () => import('../presentation/brand/ConnectPage.vue'), handle: { title: 'Ulanish' } },
    {
      path: '/:pathMatch(.*)*',
      lazy: () => import('../presentation/brand/OverviewPage.vue'),
      handle: { title: 'Umumiy' },
    },
  ],
  { titleSuffix: 'Ocharo Brend', routePersistence: false },
);
