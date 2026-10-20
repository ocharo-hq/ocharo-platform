import type { UiLocale, UiMessageTable } from './UiMessages';

/**
 * SDK component wording, keyed by the SDK's own message keys. English keeps the SDK's default text, so only Uzbek and
 * Russian are declared; Desk-specific wording still passes through its English-source tables.
 */
export const sdkUiMessages: UiMessageTable = {
  'PasswordInput.revealLabel': { uz: 'Parolni ko‘rsatish', ru: 'Показать пароль' },
  'PasswordInput.concealLabel': { uz: 'Parolni yashirish', ru: 'Скрыть пароль' },
  'ToastHost.notifications': { uz: 'Bildirishnomalar', ru: 'Уведомления' },
  'AccountMenu.menuLabel': { uz: 'Hisob', ru: 'Учётная запись' },
  'AccountMenu.triggerLabel': { uz: '{name} hisob menyusi', ru: 'Меню учётной записи {name}' },
  'AccountMenu.anonymousTriggerLabel': { uz: 'Hisob menyusi', ru: 'Меню учётной записи' },
  'AccountMenu.appearanceLabel': { uz: 'Ko‘rinish', ru: 'Оформление' },
  'AccountMenu.systemModeLabel': { uz: 'Tizim', ru: 'Системная' },
  'AccountMenu.lightModeLabel': { uz: 'Yorug‘', ru: 'Светлая' },
  'AccountMenu.darkModeLabel': { uz: 'Qorong‘i', ru: 'Тёмная' },
  'AccountMenu.languageLabel': { uz: 'Til', ru: 'Язык' },
  'AccountMenu.organizationLabel': { uz: 'Tashkilot', ru: 'Организация' },
  'AccountMenu.versionsLabel': { uz: 'Versiyalar', ru: 'Версии' },
  'AccountMenu.signOutLabel': { uz: 'Chiqish', ru: 'Выйти' },
};

/** The SDK messages for one operator locale; a key absent here resolves to the SDK's English default. */
export function sdkMessages(locale: UiLocale): Readonly<Record<string, string>> {
  if (locale === 'en') return {};
  return Object.fromEntries(Object.entries(sdkUiMessages).map(([key, value]) => [key, value[locale]]));
}
