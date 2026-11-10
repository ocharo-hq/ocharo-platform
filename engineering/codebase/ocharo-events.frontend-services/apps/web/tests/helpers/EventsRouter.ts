import { createWebHistory, createRouter } from 'vue-router';

/** A router over the browser's history that has resolved `path`, as the running app has when the page mounts. */
export async function eventsRouter(path = '/') {
  const router = createRouter({
    history: createWebHistory(),
    routes: [{ path: '/:pathMatch(.*)*', component: { render: () => null } }],
  });
  await router.push(path);
  await router.isReady();
  return router;
}
