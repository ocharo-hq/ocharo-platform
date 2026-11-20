import { describe, expect, it } from 'vitest';
import type { EventModel } from '@wow-two-beta/fe-events/contracts';

import { eventImagePath, storedImageSrc } from '@/domain/events/EventImages';

const organization = '11111111-1111-4111-8111-111111111111';
const prefix = `/api/organizations/${organization}/events-api`;
const origin = 'https://ocharo.example.test';
const id = '22222222-2222-4222-8222-222222222222';
const copy = '0197a8a0-1111-7aaa-8bbb-000000000001';
const at = '2026-10-01T00:00:00Z';

const event: EventModel = {
  id,
  catalog: 'ocharo-fashion',
  facts: { title: { und: 'Tashkent Fashion Week' } },
  review: 'published',
  revision: 1,
  createdAtUtc: at,
  updatedAtUtc: at,
};
const route = (index: number) =>
  `${origin}${prefix}/api/events/v1/catalogs/ocharo-fashion/events/${id}/images/${index}`;

describe('the route of an event image', () => {
  it("is the mirror's event route with the image's place, under the organization's prefix", () => {
    expect(eventImagePath(prefix, event, 1)).toBe(
      `${prefix}/api/events/v1/catalogs/ocharo-fashion/events/${id}/images/1`,
    );
  });
});

describe('the image resolver for stored copies', () => {
  const imageSrc = storedImageSrc(organization, origin);

  it('resolves an image with a fileRef to the route, absolute against the origin, numbered by its place', () => {
    expect(imageSrc(event, { url: 'https://source.example.test/a.jpg', fileRef: copy }, 0)).toBe(route(0));
    expect(imageSrc(event, { url: 'https://source.example.test/b.jpg', fileRef: copy }, 2)).toBe(route(2));
  });

  it('resolves an image with no fileRef, a null one or a blank one to null', () => {
    expect(imageSrc(event, { url: 'https://source.example.test/a.jpg' }, 0)).toBeNull();
    expect(imageSrc(event, { url: 'https://source.example.test/a.jpg', fileRef: null }, 0)).toBeNull();
    expect(imageSrc(event, { url: 'https://source.example.test/a.jpg', fileRef: '  ' }, 0)).toBeNull();
  });

  it("uses the page's origin when none is given", () => {
    const own = storedImageSrc(organization)(event, { url: 'https://source.example.test/a.jpg', fileRef: copy }, 0);

    expect(own).toBe(new URL(route(0).replace(origin, ''), window.location.origin).href);
  });
});
