import { EventsRoutes, type EventImage, type EventModel } from '@wow-two-beta/fe-events/contracts';

/**
 * Event images: SCFD's stored copy of an image (`facts.images[].fileRef`) is served by this app's own backend from WoW2
 * Files, never read from the browser by file id. The kit's card and detail read only `facts.images[].url` as the image
 * address, with no resolver of their own, so the page shows a copy by giving the kit the route as that address, and the
 * image's original `url` (the source link that credits it) moves to `sourceUrl` unless the image has one. An image without
 * a `fileRef` is left exactly as Events sent it, so the kit shows its source image and, when that fails, its date tile.
 * The adapted model is for display only; nothing here is sent back to Events.
 */

/** The route that redirects to the stored copy of one image of an event, under the Events mirror's prefix. */
export function eventImagePath(apiPrefix: string, event: Pick<EventModel, 'id' | 'catalog'>, index: number): string {
  return `${apiPrefix}/${EventsRoutes.event(event.catalog, event.id)}/images/${index}`;
}

const hasStoredCopy = (image: EventImage): boolean => typeof image.fileRef === 'string' && image.fileRef.trim() !== '';

/**
 * Gives each image that has a stored copy the address `imageUrl(index)` serves it from, `index` being its place in
 * `facts.images`; the kit shows only absolute http(s) addresses, so the caller resolves the route against the page's origin.
 * An event with no stored copy comes back as the same object.
 */
export function withStoredImages(event: EventModel, imageUrl: (index: number) => string): EventModel {
  const images = event.facts.images;
  if (!images?.some(hasStoredCopy)) return event;
  return {
    ...event,
    facts: {
      ...event.facts,
      images: images.map((image, index) =>
        hasStoredCopy(image) ? { ...image, url: imageUrl(index), sourceUrl: image.sourceUrl ?? image.url } : image,
      ),
    },
  };
}
