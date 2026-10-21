import { brandFrameUiMessages } from './BrandFrameUiMessages';

/** Operator interface locales; authored brand text keeps its own uz/ru/en fields. */
export type UiLocale = 'uz' | 'ru' | 'en';
export type UiMessageTable = Record<string, { uz: string; ru: string }>;

/** English source phrases identify operator messages; authored brand content never passes through this table. */
export const uiMessages: UiMessageTable = { ...brandFrameUiMessages };

/** Resolves exact interface phrases without interpreting or translating authored brand data. */
export function translateUi(source: string, locale: UiLocale, variables?: Record<string, string | number>): string {
  const message = locale === 'en' ? source : (uiMessages[source]?.[locale] ?? source);
  return message.replace(/\{(\w+)\}/g, (match, key: string) =>
    variables && Object.hasOwn(variables, key) ? String(variables[key]) : match,
  );
}
