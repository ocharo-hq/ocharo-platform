import { readonly, shallowRef, watch, type Ref } from 'vue';
import { createFeedbackBus } from '@wow-two-beta/fe/feedback';
import { useUiLocale } from './UseUiLocale';

interface Confirmation { message: string; title: string; confirmLabel: string }
const pending = shallowRef<Confirmation | null>(null);
let settle: ((accepted: boolean) => void) | null = null;
export const confirmation = readonly(pending);
export const feedbackBus = createFeedbackBus();

/** Asks through the app modal; concurrent destructive intents are rejected. */
export function requestConfirmation(message: string, options: { title?: string; confirmLabel?: string } = {}): Promise<boolean> {
  if (settle) return Promise.resolve(false);
  pending.value = { message, title: options.title ?? 'Please confirm', confirmLabel: options.confirmLabel ?? 'Continue' };
  return new Promise(resolve => { settle = resolve; });
}
/** Cancels on dismissal and resolves only the intent currently shown. */
export function resolveConfirmation(accepted: boolean): void {
  const resolve = settle; settle = null; pending.value = null; resolve?.(accepted);
}
/** Publishes safe, translated errors through the explicitly mounted SDK host. */
export function notifyError(message: string, key = message): void {
  if (!message || message === 'The request was cancelled. Your edits have been retained.') return;
  const { t } = useUiLocale();
  feedbackBus.notify({ tone: 'danger', title: t('Could not complete the request'), description: t(message), id: key, durationMs: 10000 });
}
/** Emits each changed failure once; field validation stays beside its field. */
export function useErrorToast(error: Ref<string | null>): void {
  watch(error, value => { if (value) notifyError(value); }, { immediate: true });
}
