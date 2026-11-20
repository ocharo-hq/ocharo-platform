import { describe, expect, it } from 'vitest';
import { Temporal } from 'temporal-polyfill';
import type { TrackedEventModel } from '@wow-two-beta/fe-events/contracts';

import { eventStatuses } from '@/domain/events/Events';
import {
  calendarTone,
  calendarToneColors,
  calendarWindow,
  ocharoCalendarEvents,
  type EventsCalendarEntry,
  type EventsCalendarOptions,
} from '@/domain/events/EventsCalendar';

const zone = 'Asia/Tashkent';
const options: EventsCalendarOptions = { locale: 'en', zone };
const at = '2026-10-01T00:00:00Z';

type When = { value?: string; precision: string; certainty?: string };
const day = (value: string): When => ({ value, precision: 'day', certainty: 'confirmed' });
const minute = (value: string): When => ({ value, precision: 'minute', certainty: 'confirmed' });

/** An event as Events lists it, with the facts the calendar reads. */
function item(
  id: string,
  starts: When | undefined,
  ends: When | undefined = undefined,
  stage: string | null = null,
  extra: Record<string, unknown> = {},
): TrackedEventModel {
  return {
    event: {
      id,
      catalog: 'ocharo-fashion',
      facts: { title: { en: `Event ${id}`, 'ru-Cyrl': `Событие ${id}` }, kind: 'trade-fair', starts, ends, ...extra },
      review: 'published',
      revision: 1,
      createdAtUtc: at,
      updatedAtUtc: at,
    },
    tracking: stage
      ? {
          id: `tracking-${id}`,
          eventId: id,
          stage,
          priority: 'normal',
          revision: 1,
          createdAtUtc: at,
          updatedAtUtc: at,
        }
      : null,
  } as unknown as TrackedEventModel;
}
const place = (events: TrackedEventModel[], override: Partial<EventsCalendarOptions> = {}): EventsCalendarEntry[] =>
  ocharoCalendarEvents(events, { ...options, ...override });
const iso = (value: Temporal.ZonedDateTime) => value.toString({ timeZoneName: 'never', offset: 'never' });

describe('calendar window', () => {
  const focus = Temporal.PlainDate.from('2026-11-20');

  it('reads the month with a week before it and two after, for the day, week and month views', () => {
    for (const view of ['month', 'week', 'day'])
      expect(calendarWindow(view, focus)).toEqual({ from: '2026-10-25', to: '2026-12-14' });
  });

  it("covers the month grid's 42 days, even when February starts the grid's first row", () => {
    // 1 Feb 2026 is a Sunday, so the grid draws 1 Feb to 14 Mar.
    expect(calendarWindow('month', Temporal.PlainDate.from('2026-02-10'))).toEqual({
      from: '2026-01-25',
      to: '2026-03-14',
    });
  });

  it('reads the whole year for the year view, whatever day is focused', () => {
    expect(calendarWindow('year', focus)).toEqual({ from: '2026-01-01', to: '2026-12-31' });
    expect(calendarWindow('year', Temporal.PlainDate.from('2027-01-01'))).toEqual({
      from: '2027-01-01',
      to: '2027-12-31',
    });
  });

  it('reads the 31 days the agenda lists from the focused day', () => {
    expect(calendarWindow('agenda', focus)).toEqual({ from: '2026-11-20', to: '2026-12-21' });
  });
});

describe('calendar events', () => {
  it('draws an event with a known day as an all-day block from its first day to its last, both included', () => {
    const [entry] = place([item('a', day('2026-10-12'), day('2026-10-14'))]);

    expect(entry).toMatchObject({ id: 'a', title: 'Event a', isAllDay: true });
    expect(iso(entry!.start)).toBe('2026-10-12T00:00:00');
    // The end is the midnight after the last day, so the viewer draws 12, 13 and 14 October.
    expect(iso(entry!.end)).toBe('2026-10-15T00:00:00');
    expect(entry!.start.timeZoneId).toBe(zone);
    expect(entry!.end.timeZoneId).toBe(zone);
  });

  it('draws a one-day event when the end is missing, before the start, or only a month or a year', () => {
    const entries = place([
      item('none', day('2026-10-12')),
      item('before', day('2026-10-12'), day('2026-10-10')),
      item('same', day('2026-10-12'), day('2026-10-12')),
      item('month', day('2026-10-12'), { value: '2026-11', precision: 'month' }),
      item('year', day('2026-10-12'), { value: '2027', precision: 'year' }),
      item('unknown', day('2026-10-12'), { precision: 'unknown' }),
    ]);

    expect(entries).toHaveLength(6);
    for (const entry of entries) {
      expect(entry.isAllDay).toBe(true);
      expect(iso(entry.start)).toBe('2026-10-12T00:00:00');
      expect(iso(entry.end)).toBe('2026-10-13T00:00:00');
    }
  });

  it('draws a day-long start ending at a time on its end day through that whole end day', () => {
    const [entry] = place([item('a', day('2026-10-12'), minute('2026-10-13T15:00'))]);

    expect(iso(entry!.end)).toBe('2026-10-14T00:00:00');
  });

  it('leaves out an event known only to its month or year, an unknown or unreadable date, and an estimated day', () => {
    const entries = place([
      item('month', { value: '2026-11', precision: 'month', certainty: 'confirmed' }),
      item('year', { value: '2026', precision: 'year', certainty: 'confirmed' }),
      item('unknown', { precision: 'unknown', certainty: 'announced' }),
      item('missing', undefined),
      item('garbled', { value: '12/10/2026', precision: 'day', certainty: 'confirmed' }),
      // An estimate never claims a day: Events' dates read it to its month at most.
      item('estimated', { value: '2026-10-12', precision: 'day', certainty: 'estimated' }),
      item('kept', day('2026-10-12')),
    ]);

    expect(entries.map((entry) => entry.id)).toEqual(['kept']);
  });

  it('leaves out the closed stages the feed hides, and keeps an untracked event and every other stage', () => {
    const entries = place([
      item('skipped', day('2026-10-12'), undefined, 'skipped'),
      item('rejected', day('2026-10-12'), undefined, 'rejected'),
      item('untracked', day('2026-10-12')),
      ...eventStatuses
        .filter((stage) => stage !== 'skipped' && stage !== 'rejected')
        .map((stage) => item(stage, day('2026-10-12'), undefined, stage)),
    ]);

    expect(entries.map((entry) => entry.id).sort()).toEqual(
      ['applied', 'applying', 'attended', 'candidate', 'confirmed', 'shortlisted', 'untracked'].sort(),
    );
  });

  it("draws a timed event at its time, read in the event's zone and shown in the browser's", () => {
    const [entry] = place(
      [item('a', minute('2026-10-12T19:00'), minute('2026-10-12T21:30'), null, { timeZone: 'Europe/Moscow' })],
      { zone: 'America/New_York' },
    );

    expect(entry!.isAllDay).toBe(false);
    // 19:00 in Moscow (UTC+3) is 16:00Z, 12:00 in New York in October.
    expect(entry!.start.epochMilliseconds).toBe(Date.UTC(2026, 9, 12, 16, 0));
    expect(entry!.end.epochMilliseconds).toBe(Date.UTC(2026, 9, 12, 18, 30));
    expect(entry!.start.timeZoneId).toBe('America/New_York');
    expect(iso(entry!.start)).toBe('2026-10-12T12:00:00');
  });

  it("reads a timed event with no zone, or one the browser does not know, in the browser's zone", () => {
    const entries = place([
      item('none', minute('2026-10-12T19:00')),
      item('unknown', minute('2026-10-12T19:00'), undefined, null, { timeZone: 'Nowhere/Land' }),
    ]);

    for (const entry of entries) expect(iso(entry.start)).toBe('2026-10-12T19:00:00');
  });

  it('draws a timed event with no usable end as a point, and one ending at a day through the end of that day', () => {
    const entries = place([
      item('point', minute('2026-10-12T19:00')),
      item('earlier', minute('2026-10-12T19:00'), minute('2026-10-12T18:00')),
      item('days', minute('2026-10-12T19:00'), day('2026-10-14')),
    ]);
    const [point, earlier, days] = entries;

    expect(point!.end.epochMilliseconds).toBe(point!.start.epochMilliseconds);
    expect(earlier!.end.epochMilliseconds).toBe(earlier!.start.epochMilliseconds);
    expect(iso(days!.end)).toBe('2026-10-15T00:00:00');
  });

  it('titles a block in the interface language, and leaves it untitled for the calendar to name when there is none', () => {
    expect(place([item('a', day('2026-10-12'))], { locale: 'ru' })[0]!.title).toBe('Событие a');
    expect(place([item('a', day('2026-10-12'), undefined, null, { title: {} })])[0]!.title).toBeUndefined();
  });
});

describe('calendar colours', () => {
  it('sets the pursued stages apart from confirmed and attended, and both from an event not yet tracked', () => {
    const pursuing = ['shortlisted', 'applying', 'applied'].map((stage) => calendarToneColors[calendarTone(stage)]);
    const participating = ['confirmed', 'attended'].map((stage) => calendarToneColors[calendarTone(stage)]);
    const neutral = [undefined, null, 'candidate', 'something-else'].map(
      (stage) => calendarToneColors[calendarTone(stage)],
    );

    expect(new Set(pursuing).size).toBe(1);
    expect(new Set(participating).size).toBe(1);
    expect(new Set(neutral).size).toBe(1);
    expect(new Set([pursuing[0], participating[0], neutral[0]]).size).toBe(3);
  });

  it('colours each block by its stage, from theme variables and never a hex value', () => {
    const entries = place([
      item('pursuing', day('2026-10-12'), undefined, 'applying'),
      item('confirmed', day('2026-10-12'), undefined, 'confirmed'),
      item('untracked', day('2026-10-12')),
    ]);

    expect(entries.map((entry) => entry.color)).toEqual([
      calendarToneColors.pursuing,
      calendarToneColors.participating,
      calendarToneColors.neutral,
    ]);
    for (const color of Object.values(calendarToneColors)) expect(color).toMatch(/^var\(--color-[a-z-]+\)$/);
  });
});
