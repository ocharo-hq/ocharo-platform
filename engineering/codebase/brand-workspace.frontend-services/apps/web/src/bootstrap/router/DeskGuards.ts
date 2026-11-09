import type { Router } from 'vue-router';

import type { AccessControls } from '@/application/common/hooks/UseAccess';

/**
 * Installs Desk's navigation guard: every move between places asks the document's session controls first, so pending
 * writes hold the page, unsaved edits ask before leaving and another organization replaces the document. Returns the
 * function that removes it.
 */
export function installDeskGuards(router: Router, access: Pick<AccessControls, 'admit'>): () => void {
  return router.beforeEach((to, from) => access.admit(to, from));
}
