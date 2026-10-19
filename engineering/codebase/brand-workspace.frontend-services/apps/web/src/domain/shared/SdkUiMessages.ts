import type { UiLocale, UiMessageTable } from './UiMessages';

/**
 * SDK component wording, keyed by the SDK's own message keys. English keeps the SDK's default text, so only Uzbek and
 * Russian are declared; Desk-specific wording still passes through its English-source tables.
 */
export const sdkUiMessages: UiMessageTable = {
  'PasswordInput.revealLabel': { uz: 'Parolni ko‘rsatish', ru: 'Показать пароль' },
  'PasswordInput.concealLabel': { uz: 'Parolni yashirish', ru: 'Скрыть пароль' },
  'ToastHost.notifications': { uz: 'Bildirishnomalar', ru: 'Уведомления' },
};

/** The SDK messages for one operator locale; a key absent here resolves to the SDK's English default. */
export function sdkMessages(locale: UiLocale): Readonly<Record<string, string>> {
  if (locale === 'en') return {};
  return Object.fromEntries(Object.entries(sdkUiMessages).map(([key, value]) => [key, value[locale]]));
}
