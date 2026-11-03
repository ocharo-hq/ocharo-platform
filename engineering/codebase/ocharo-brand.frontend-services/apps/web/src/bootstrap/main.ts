import './index.css';

import { createApp } from 'vue';

import { loadOcharoApps, ocharoAppsPlugin, reportOcharoAppsStartup } from '../application/shared/UseOcharoApps';
import { initializeUiLocale } from '../application/shared/UseUiLocale';
import { identitySession, signInUrl } from '../integration/common/IdentitySessionClient';
import AppRoot from './AppRoot.vue';
import { createBrandRouter } from './BrandRouter';

initializeUiLocale();

const router = createBrandRouter();
const app = createApp(AppRoot);
app.use(router);
// The app menu's runtime settings load beside the first route and the session, within a bounded wait; a failure falls
// back to the build-time addresses. Without a session the browser goes to sign in and comes back here, unless a sign-in
// just failed (`signInError`): the frame then says so and offers it again, so a misconfigured client never loops.
void Promise.all([loadOcharoApps(), router.isReady(), identitySession.initialize()]).then(([startup, , session]) => {
  if (!session.authenticated && !new URLSearchParams(window.location.search).has('signInError')) {
    window.location.assign(signInUrl(window.location.pathname + window.location.search));
    return;
  }
  app.use(ocharoAppsPlugin(startup));
  app.mount('#root');
  // The toast host subscribes as the app mounts, so startup problems are reported once it is up.
  reportOcharoAppsStartup(startup);
});
