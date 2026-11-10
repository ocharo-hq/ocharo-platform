import { eventsCommonUiMessages } from './EventsCommonUiMessages';
import { eventsFrameUiMessages } from './EventsFrameUiMessages';
import { eventsUiMessages } from './EventsUiMessages';

/** Operator interface locales, shared by every Ocharo app. */
export type UiLocale = 'uz' | 'ru' | 'en';
/**
 * Interface messages by key. A key is its English phrase; `en` is set only where it is not: the apps menu's Editorial
 * keeps the key Desk and Catalogue give it, apart from their Editorial designer theme.
 */
export type UiMessageTable = Record<string, { uz: string; ru: string; en?: string }>;

/** English source phrases identify operator messages; event data from the API never passes through this table. */
export const uiMessages: UiMessageTable = { ...eventsFrameUiMessages, ...eventsCommonUiMessages, ...eventsUiMessages };

/** Resolves exact interface phrases without interpreting or translating event data. */
export function translateUi(source: string, locale: UiLocale, variables?: Record<string, string | number>): string {
  const message = (locale === 'en' ? uiMessages[source]?.en : uiMessages[source]?.[locale]) ?? source;
  return message.replace(/\{(\w+)\}/g, (match, key: string) =>
    variables && Object.hasOwn(variables, key) ? String(variables[key]) : match,
  );
}
