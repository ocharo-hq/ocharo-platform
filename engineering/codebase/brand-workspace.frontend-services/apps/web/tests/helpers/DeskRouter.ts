import { createDeskRouter } from '@/bootstrap/router';

/** A Desk router that has already resolved the current address, as the running app has by the time a page mounts. */
export async function deskRouter(): Promise<ReturnType<typeof createDeskRouter>> {
  const router = createDeskRouter();
  await router.push(`${window.location.pathname}${window.location.search}`);
  await router.isReady();
  return router;
}
