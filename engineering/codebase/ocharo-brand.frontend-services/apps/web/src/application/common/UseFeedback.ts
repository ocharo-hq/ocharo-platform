import { createFeedbackBus } from '@wow-two-beta/fe/feedback';

import { useUiLocale } from '../shared/UseUiLocale';

/** The app's one feedback bus; the root mounts its toast host once. */
export const feedbackBus = createFeedbackBus();

/** Publishes a safe, already translated error; the same key replaces its toast, and an empty message stays quiet. */
export function notifyError(message: string, key = message): void {
  if (!message) return;
  feedbackBus.notify({
    id: key,
    tone: 'danger',
    title: useUiLocale().t('Could not complete the request'),
    description: message,
    durationMs: 10_000,
  });
}
