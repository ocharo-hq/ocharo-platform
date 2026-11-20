import { EventsRoutes, type EventImage, type EventModel } from '@wow-two-beta/fe-events/contracts';

import { eventsApiPrefix } from './Events';

/**
 * Event images: SCFD's stored copy of an image (`facts.images[].fileRef`) is served by this app's own backend from WoW2
 * Files, never read from the browser by file id. The kit's card and detail take an `imageSrc` resolver: the address it
 * returns is the image they show, and when that address fails to load they fall back to the image's own source `url`, then
 * to the date tile (card) or no image (detail). An image without a `fileRef` resolves to `null`, so the kit shows its source
 * image as Events sent it. The events themselves are never changed; nothing here is sent back to Events.
 */

/** The route that redirects to the stored copy of one image of an event, under the Events mirror's prefix. */
export function eventImagePath(apiPrefix: string, event: Pick<EventModel, 'id' | 'catalog'>, index: number): string {
  return `${apiPrefix}/${EventsRoutes.event(event.catalog, event.id)}/images/${index}`;
}

const hasStoredCopy = (image: EventImage): boolean => typeof image.fileRef === 'string' && image.fileRef.trim() !== '';

/**
 * The kit's `imageSrc` resolver for the organization: an image with a stored copy resolves to the route that serves it, its
 * place being its index in `facts.images`; any other image resolves to `null`. The kit shows only absolute http(s)
 * addresses, so the route is resolved against `origin`, the page's by default.
 */
export function storedImageSrc(
  organizationId: string,
  origin: string = window.location.origin,
): (event: EventModel, image: EventImage, index: number) => string | null {
  const prefix = eventsApiPrefix(organizationId);
  return (event, image, index) =>
    hasStoredCopy(image) ? new URL(eventImagePath(prefix, event, index), origin).href : null;
}
