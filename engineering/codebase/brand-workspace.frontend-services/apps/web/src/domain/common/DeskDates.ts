/** Uzbek month abbreviations, as the other Ocharo apps print them. @internal */
const UzbekMonths = ['yan', 'fev', 'mar', 'apr', 'may', 'iyn', 'iyl', 'avg', 'sen', 'okt', 'noy', 'dek'] as const;

/** The date shapes Desk prints; Russian and English keep their `Intl` forms. */
export const DeskDateFormat = {
  /** The day alone: `9 noy 2026`, `09.11.2026`, `11/9/2026`. */
  Date: { year: 'numeric', month: 'numeric', day: 'numeric' },
  /** The day and the time to the second: `9 noy 2026 · 17:02:50`. */
  DateTime: { year: 'numeric', month: 'numeric', day: 'numeric', hour: 'numeric', minute: '2-digit', second: '2-digit' },
  /** The day with a short month name and the time to the second: `9 noy 2026 · 17:02:50`, `Nov 9, 2026, 5:02:50 PM`. */
  Medium: { dateStyle: 'medium', timeStyle: 'medium' },
} as const satisfies Record<string, Intl.DateTimeFormatOptions>;

/** One of Desk's date shapes, or any other `Intl` date shape. */
export type DeskDateFormat = Intl.DateTimeFormatOptions;

/** @internal Formatters by locale, shape and time zone; building one is the costly part. */
const formatters = new Map<string, Intl.DateTimeFormat>();

/** @internal The cached formatter for a locale, shape and time zone. */
function formatter(locale: string, options: Intl.DateTimeFormatOptions): Intl.DateTimeFormat {
  const key = `${locale}|${JSON.stringify(options)}`;
  let cached = formatters.get(key);
  if (!cached) {
    cached = new Intl.DateTimeFormat(locale, options);
    formatters.set(key, cached);
  }
  return cached;
}

/**
 * Prints a timestamp in the interface language, in the viewer's time zone unless `timeZone` names one. Browsers ship no
 * Uzbek date data (`2026 M09 28`), so Uzbek takes its numbers from a numeric formatter and its month from Desk's own
 * table. An absent or unreadable value prints `—`.
 */
export function formatDeskDate(
  value: string | Date | null | undefined,
  locale: string,
  format: DeskDateFormat = DeskDateFormat.DateTime,
  timeZone?: string,
): string {
  if (!value) return '—';
  const date = value instanceof Date ? value : new Date(value);
  if (!Number.isFinite(date.getTime())) return '—';
  const zone = timeZone ? { timeZone } : {};
  if (locale !== 'uz') return formatter(locale, { ...format, ...zone }).format(date);
  const withTime = 'hour' in format || 'timeStyle' in format;
  const withSeconds = 'second' in format || ('timeStyle' in format && format.timeStyle !== 'short');
  const parts = formatter('en-GB', {
    day: 'numeric', month: 'numeric', year: 'numeric', hour: '2-digit', minute: '2-digit', second: '2-digit', hourCycle: 'h23', ...zone,
  }).formatToParts(date);
  const part = (type: Intl.DateTimeFormatPartTypes) => parts.find((item) => item.type === type)?.value ?? '';
  const day = `${part('day')} ${UzbekMonths[Number(part('month')) - 1]} ${part('year')}`;
  if (!withTime) return day;
  return `${day} · ${part('hour')}:${part('minute')}${withSeconds ? `:${part('second')}` : ''}`;
}
