import { describe, expect, it } from 'vitest';
import type { EventModel } from '@wow-two-beta/fe-events/contracts';

import { eventImagePath, withStoredImages } from '@/domain/events/EventImages';

const prefix = '/api/organizations/11111111-1111-4111-8111-111111111111/events-api';
const id = '22222222-2222-4222-8222-222222222222';
const copy = '0197a8a0-1111-7aaa-8bbb-000000000001';
const at = '2026-10-01T00:00:00Z';

const event = (images?: EventModel['facts']['images']): EventModel => ({
  id,
  catalog: 'ocharo-fashion',
  facts: { title: { und: 'Tashkent Fashion Week' }, ...(images ? { images } : {}) },
  review: 'published',
  revision: 1,
  createdAtUtc: at,
  updatedAtUtc: at,
});
const imageUrl = (index: number) => `https://ocharo.example.test/image/${index}`;

describe('the route of an event image', () => {
  it("is the mirror's event route with the image's place, under the organization's prefix", () => {
    expect(eventImagePath(prefix, event(), 1)).toBe(
      `${prefix}/api/events/v1/catalogs/ocharo-fashion/events/${id}/images/1`,
    );
  });
});

describe('stored copies of event images', () => {
  it('gives an image with a fileRef the route as its address and keeps its source link as `sourceUrl`', () => {
    const shown = withStoredImages(event([{ url: 'https://source.example.test/a.jpg', fileRef: copy }]), imageUrl);

    expect(shown.facts.images).toEqual([
      {
        url: imageUrl(0),
        fileRef: copy,
        sourceUrl: 'https://source.example.test/a.jpg',
      },
    ]);
  });

  it("keeps an image's own `sourceUrl` and the rest of its fields", () => {
    const shown = withStoredImages(
      event([
        {
          url: 'https://source.example.test/a.jpg',
          fileRef: copy,
          sourceUrl: 'https://source.example.test/page',
          attribution: 'Afisha',
        },
      ]),
      imageUrl,
    );

    expect(shown.facts.images![0]).toEqual({
      url: imageUrl(0),
      fileRef: copy,
      sourceUrl: 'https://source.example.test/page',
      attribution: 'Afisha',
    });
  });

  it('numbers each image by its place in the event and leaves one with no copy exactly as Events sent it', () => {
    const poster = { url: 'https://source.example.test/poster.jpg' };
    const blank = { url: 'https://source.example.test/blank.jpg', fileRef: '  ' };
    const shown = withStoredImages(
      event([poster, { url: 'https://source.example.test/b.jpg', fileRef: copy }, blank]),
      imageUrl,
    );

    expect(shown.facts.images![0]).toBe(poster);
    expect(shown.facts.images![1]!.url).toBe(imageUrl(1));
    expect(shown.facts.images![2]).toBe(blank);
  });

  it('returns the same event when no image has a copy, none or null', () => {
    const none = event();
    const sourceOnly = event([
      { url: 'https://source.example.test/a.jpg' },
      { url: 'https://source.example.test/b.jpg', fileRef: null },
    ]);

    expect(withStoredImages(none, imageUrl)).toBe(none);
    expect(withStoredImages(sourceOnly, imageUrl)).toBe(sourceOnly);
  });

  it('changes nothing but the images', () => {
    const original = event([{ url: 'https://source.example.test/a.jpg', fileRef: copy }]);

    const shown = withStoredImages(original, imageUrl);

    expect({ ...shown, facts: { ...shown.facts, images: undefined } }).toEqual({
      ...original,
      facts: { ...original.facts, images: undefined },
    });
    expect(original.facts.images![0]!.url).toBe('https://source.example.test/a.jpg');
  });
});
