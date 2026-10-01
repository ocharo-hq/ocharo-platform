const months = ['yan', 'fev', 'mar', 'apr', 'may', 'iyn', 'iyl', 'avg', 'sen', 'okt', 'noy', 'dek'];

/**
 * Tashkent wall-clock label such as `28 sen 2026 · 19:08`. Browsers ship incomplete Uzbek ICU
 * data (`2026 M09 28`), so numbers come from a numeric formatter and month words from this table.
 */
export function formatWhen(value: string | Date | null | undefined, withTime = true): string {
  if (!value) return '—';
  const date = value instanceof Date ? value : new Date(value);
  if (!Number.isFinite(date.getTime())) return '—';
  const parts = new Intl.DateTimeFormat('en-GB', {
    timeZone: 'Asia/Tashkent',
    day: 'numeric',
    month: 'numeric',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    hourCycle: 'h23',
  }).formatToParts(date);
  const part = (type: Intl.DateTimeFormatPartTypes) => parts.find((p) => p.type === type)?.value ?? '';
  const day = `${part('day')} ${months[Number(part('month')) - 1]} ${part('year')}`;
  return withTime ? `${day} · ${part('hour')}:${part('minute')}` : day;
}
