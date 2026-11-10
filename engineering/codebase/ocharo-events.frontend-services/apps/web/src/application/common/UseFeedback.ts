import { readonly, shallowRef, watch, type Ref } from 'vue';
import { createFeedbackBus } from '@wow-two-beta/fe/feedback';

import { useUiLocale } from '../shared/UseUiLocale';

/** The app's one feedback bus; the root mounts its toast host once. */
export const feedbackBus = createFeedbackBus();

/** What the confirmation dialog shows: English source phrases, translated where they render. */
interface Confirmation {
  message: string;
  title: string;
  confirmLabel: string;
}
const pending = shallowRef<Confirmation | null>(null);
let settle: ((accepted: boolean) => void) | null = null;
/** The open confirmation, if any; the root's feedback host renders it. */
export const confirmation = readonly(pending);

/** Asks through the app modal; a second concurrent intent is rejected. */
export function requestConfirmation(
  message: string,
  options: { title?: string; confirmLabel?: string } = {},
): Promise<boolean> {
  if (settle) return Promise.resolve(false);
  pending.value = {
    message,
    title: options.title ?? 'Please confirm',
    confirmLabel: options.confirmLabel ?? 'Continue',
  };
  return new Promise((resolve) => {
    settle = resolve;
  });
}

/** Cancels on dismissal and resolves only the intent currently shown. */
export function resolveConfirmation(accepted: boolean): void {
  const resolve = settle;
  settle = null;
  pending.value = null;
  resolve?.(accepted);
}

/** Publishes a safe error, translated here; the same key replaces its toast, and an empty message stays quiet. */
export function notifyError(message: string, key = message): void {
  if (!message || message === 'The request was cancelled. Your edits have been retained.') return;
  const { t } = useUiLocale();
  feedbackBus.notify({
    id: key,
    tone: 'danger',
    title: t('Could not complete the request'),
    description: t(message),
    durationMs: 10_000,
  });
}

/** Emits each changed failure once; field validation stays beside its field. */
export function useErrorToast(error: Ref<string | null>): void {
  watch(
    error,
    (value) => {
      if (value) notifyError(value);
    },
    { immediate: true },
  );
}
