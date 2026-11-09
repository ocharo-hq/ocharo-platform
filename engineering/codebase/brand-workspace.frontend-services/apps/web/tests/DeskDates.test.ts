import { describe, expect, it } from 'vitest';

import { DeskDateFormat, formatDeskDate } from '@/domain/common/DeskDates';

const at = '2026-09-28T14:08:05Z';

describe('Desk dates', () => {
  it('prints Uzbek month names instead of the browser’s missing Uzbek date data', () => {
    expect(formatDeskDate(at, 'uz', DeskDateFormat.Date, 'Asia/Tashkent')).toBe('28 sen 2026');
    expect(formatDeskDate(at, 'uz', DeskDateFormat.DateTime, 'Asia/Tashkent')).toBe('28 sen 2026 · 19:08:05');
    expect(formatDeskDate(at, 'uz', DeskDateFormat.Medium, 'Asia/Tashkent')).toBe('28 sen 2026 · 19:08:05');
    expect(formatDeskDate(at, 'uz', DeskDateFormat.Date, 'Asia/Tashkent')).not.toMatch(/M\d/);
  });

  it('keeps the Russian and English forms the browser prints', () => {
    const date = new Date(at);
    expect(formatDeskDate(at, 'ru', DeskDateFormat.DateTime, 'UTC')).toBe(date.toLocaleString('ru', { timeZone: 'UTC' }));
    expect(formatDeskDate(at, 'en', DeskDateFormat.Date, 'UTC')).toBe(date.toLocaleDateString('en', { timeZone: 'UTC' }));
  });

  it('prints a dash for an absent or unreadable value', () => {
    expect(formatDeskDate(null, 'uz')).toBe('—');
    expect(formatDeskDate('not a date', 'en')).toBe('—');
  });
});
