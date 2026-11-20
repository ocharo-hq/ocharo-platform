import type {
  CatalogKindModel,
  JsonValue,
  LocalizedText,
  StageClass,
  WorkflowModel,
  WorkflowSpec,
} from '@wow-two-beta/fe-events/contracts';
import type { EventCalendarOptions, EventProposalOptions, EventRegion } from '@wow-two-beta/fe-events/kit';

import { translateUi, type UiLocale } from '@/domain/shared/UiMessages';

/**
 * Events: every event where Ocharo could show its products, and where Ocharo stands with each. WoW2 Events holds the
 * events, the organization's tracking and its workflow; the page renders them with `fe-events`' kit (OE07). What stays
 * here is Ocharo's own configuration of that kit: its catalog, stages, kinds, regions, the tracking fields it shows,
 * how a new event is proposed, and the page's addresses.
 */

/** The catalog the Events page reads (`Events:Catalog`); the mirrored routes serve only it. */
export const OcharoEventsCatalog = 'ocharo-fashion';

/** The interface languages every Ocharo text below is written in. */
const uiLocales: readonly UiLocale[] = ['en', 'uz', 'ru'];

/** One English interface phrase in the three interface languages, for a kit label. */
export function localized(source: string): LocalizedText {
  return Object.fromEntries(uiLocales.map((locale) => [locale, translateUi(source, locale)]));
}

export const eventFormats = [
  'runway',
  'trade-fair',
  'market',
  'exhibition',
  'competition',
  'festival',
  'conference',
  'other',
] as const;
export type EventFormat = (typeof eventFormats)[number];
export const formatLabels: Record<EventFormat, string> = {
  runway: 'Runway show',
  'trade-fair': 'Trade fair',
  market: 'Market',
  exhibition: 'Exhibition',
  competition: 'Competition',
  festival: 'Festival',
  conference: 'Conference',
  other: 'Other',
};

/** Ocharo's eight stages, in order; an event Ocharo does not track yet reads as `candidate`. */
export const eventStatuses = [
  'candidate',
  'shortlisted',
  'applying',
  'applied',
  'confirmed',
  'attended',
  'skipped',
  'rejected',
] as const;
export type EventStatus = (typeof eventStatuses)[number];
export const statusLabels: Record<EventStatus, string> = {
  candidate: 'Candidate',
  shortlisted: 'Shortlisted',
  applying: 'Applying',
  applied: 'Applied',
  confirmed: 'Confirmed',
  attended: 'Attended',
  skipped: 'Skipped',
  rejected: 'Rejected',
};
/**
 * What each stage means across organizations: Events leaves a `lost` stage's events out of the nearest deadlines, and
 * reads `won` as done. Skipped and rejected are both lost, so neither reaches the deadlines panel.
 */
export const statusClasses: Record<EventStatus, StageClass> = {
  candidate: 'open',
  shortlisted: 'open',
  applying: 'active',
  applied: 'active',
  confirmed: 'active',
  attended: 'won',
  skipped: 'lost',
  rejected: 'lost',
};

/** The stages a team has left: the feed hides them until asked (OE05), and a digest leaves them out by default. */
export const ocharoClosedStages: readonly EventStatus[] = ['skipped', 'rejected'];
/** The stages a card at the default stage offers as one click. */
export const ocharoQuickStages: readonly EventStatus[] = ['shortlisted', 'skipped'];
/** The stages of an event Ocharo was pursuing, which must be closed once the event has ended (OE12). */
export const ocharoPursuingStages: readonly EventStatus[] = ['shortlisted', 'applying', 'applied', 'confirmed'];
/** How a past event Ocharo was pursuing closes: it took part, or it let the event go. */
export const ocharoClosingStages: readonly EventStatus[] = ['attended', 'skipped'];

/** The tracking fields Ocharo keeps: how to take part and its own cost, plus the source link the Desk import kept. */
export const trackingFieldKeys = { participation: 'participation', cost: 'cost', sourceUrl: 'sourceUrl' } as const;

/**
 * Ocharo's workflow as WoW2 Events should hold it: the eight stages with their labels in the three interface languages
 * and their classes, `candidate` as the default, and the custom fields with localized titles, the source link read-only
 * (shown, never offered for editing, and kept on save). The deploy writes it with Events' `workflow put --spec` from
 * `engineering/deployment/events/ocharo-workflow.json`, which a test keeps equal to this; a page visit never writes it.
 */
export function ocharoWorkflowSpec(): Omit<WorkflowSpec, 'expectedRevision'> {
  return {
    stages: eventStatuses.map((key) => ({ key, label: localized(statusLabels[key]), class: statusClasses[key] })),
    defaultStage: 'candidate',
    fields: {
      type: 'object',
      properties: {
        [trackingFieldKeys.participation]: {
          type: 'string',
          title: localized('How to take part'),
          maxLength: 2000,
          format: 'textarea',
        },
        [trackingFieldKeys.cost]: { type: 'string', title: localized("Ocharo's cost"), maxLength: 500 },
        [trackingFieldKeys.sourceUrl]: {
          type: 'string',
          title: localized('Source link'),
          format: 'uri',
          readOnly: true,
        },
      },
    },
  };
}

/** Ocharo's stages for the kit's stage labels and pickers (the feed, the deadlines panel, the digest editor). */
export function ocharoWorkflow(): Pick<WorkflowModel, 'stages' | 'defaultStage'> {
  const { stages, defaultStage } = ocharoWorkflowSpec();
  return { stages, defaultStage };
}

type JsonObject = { readonly [key: string]: JsonValue };
const isJsonObject = (value: JsonValue | undefined): value is JsonObject =>
  value !== null && typeof value === 'object' && !Array.isArray(value);

/** Keeps the stored fields' schema, adding Ocharo's title to a stored field that has none; no stored fields read as Ocharo's. */
function fieldsForPage(stored: JsonValue | undefined, wanted: JsonValue | undefined): JsonValue | undefined {
  const storedProperties = isJsonObject(stored) ? stored.properties : undefined;
  if (!isJsonObject(storedProperties)) return wanted;
  const wantedProperties = isJsonObject(wanted) && isJsonObject(wanted.properties) ? wanted.properties : {};
  const properties = Object.fromEntries(
    Object.entries(storedProperties).map(([key, property]) => {
      const wantedProperty = wantedProperties[key];
      return isJsonObject(property) &&
        property.title === undefined &&
        isJsonObject(wantedProperty) &&
        wantedProperty.title
        ? [key, { ...property, title: wantedProperty.title }]
        : [key, property];
    }),
  );
  return { ...(stored as JsonObject), properties };
}

/**
 * The workflow the page shows for what WoW2 Events stored: its stages and fields as stored, with Ocharo's labels from
 * `ocharoWorkflowSpec` filling any language a stage label lacks and any field without a title. Events' default workflow
 * (revision 0, before the deploy wrote Ocharo's) is not Ocharo's stages at all, so it reads as the spec outright. The
 * page only reads: the stored workflow is written by the deploy step, never by a visit.
 */
export function workflowForPage(stored: WorkflowModel): WorkflowModel {
  const wanted = ocharoWorkflowSpec();
  if (stored.revision === 0) return { ...wanted, revision: 0 };
  const labels = new Map(wanted.stages.map((stage) => [stage.key, stage.label]));
  const fields = fieldsForPage(stored.fields, wanted.fields);
  return {
    ...stored,
    stages: stored.stages.map((stage) => ({ ...stage, label: { ...labels.get(stage.key), ...stage.label } })),
    ...(fields === undefined ? {} : { fields }),
  };
}

/** Ocharo's event kinds with their labels in the three interface languages, for the kind filter and badges. */
export function ocharoKinds(): CatalogKindModel[] {
  return eventFormats.map((key) => ({ key, label: localized(formatLabels[key]) }));
}

/** Uzbekistan's four neighbours that Ocharo counts as Central Asia. */
export const centralAsiaCountries: readonly string[] = ['KZ', 'KG', 'TJ', 'TM'];

/**
 * Ocharo's scopes, derived from an event's country: Uzbekistan national, Kazakhstan, Kyrgyzstan, Tajikistan and
 * Turkmenistan Central Asia, every other country international. An event with no country belongs to none and reads
 * "Scope unknown" (OE08). The feed's scope chips and the calendar's scope pills take these labels and keys.
 */
export function ocharoRegions(): EventRegion[] {
  return [
    { key: 'national', label: localized('National'), countries: ['UZ'] },
    { key: 'central-asia', label: localized('Central Asia'), countries: [...centralAsiaCountries] },
    { key: 'international', label: localized('International'), otherCountries: true },
  ];
}

/**
 * How a member adds an event: Ocharo's catalog is private, so Events publishes it at once (OE03) and the feed lists it;
 * it starts shortlisted, with the application deadline and where it was found, the link kept in `sourceUrl`.
 */
export function ocharoProposal(): EventProposalOptions {
  return {
    initialTracking: { stage: 'shortlisted', priority: 'normal' },
    sourceField: trackingFieldKeys.sourceUrl,
    withDeadline: true,
    withFoundAt: true,
    privateCatalog: true,
  };
}

/** Names the one-event calendar file and its `PRODID` and `UID` suffix, as the page's own export named them. */
export function ocharoCalendarOptions(eventId: string): EventCalendarOptions {
  return {
    productId: '-//Ocharo//Ocharo Events//EN',
    uidDomain: 'ocharo.local',
    fileName: `ocharo-event-${eventId}.ics`,
  };
}

const identifier = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
export interface EventsLocation {
  view: 'feed' | 'event';
  eventId: string | null;
}
/** Reads the segments after `/organizations/{id}/events`; anything but one event id falls back to the feed. */
export function readEventsLocation(segments: readonly string[]): EventsLocation {
  if (segments.length === 1 && identifier.test(segments[0]!))
    return { view: 'event', eventId: segments[0]!.toLowerCase() };
  return { view: 'feed', eventId: null };
}
/** Reads an Events app address's view; `null` when the address is outside an organization's events. */
export function readEventsAddress(pathname: string): EventsLocation | null {
  const parts = pathname.split('/').filter(Boolean);
  return readEventsOrganizationId(pathname) ? readEventsLocation(parts.slice(3)) : null;
}
/** Reads the organization of an `/organizations/{id}/events…` address; `null` for any other address. */
export function readEventsOrganizationId(pathname: string): string | null {
  const parts = pathname.split('/').filter(Boolean);
  return parts[0] === 'organizations' && parts[2] === 'events' && identifier.test(parts[1] ?? '') ? parts[1]! : null;
}
export function eventsPath(organizationId: string, location: EventsLocation): string {
  if (!identifier.test(organizationId)) throw new Error('Invalid workspace destination.');
  const base = `/organizations/${organizationId}/events`;
  return location.view === 'event' && location.eventId && identifier.test(location.eventId)
    ? `${base}/${location.eventId}`
    : base;
}
export function isEventIdentifier(value: string): boolean {
  return identifier.test(value);
}

/** The places an organization has beside the feed and the Telegram delivery, each addressed by one path segment. */
export type EventsPlace = 'calendar' | 'reminders';
/** The address of one of an organization's places: `/organizations/{id}/calendar`, `/organizations/{id}/reminders`. */
export function eventsPlacePath(organizationId: string, place: EventsPlace): string {
  if (!identifier.test(organizationId)) throw new Error('Invalid workspace destination.');
  return `/organizations/${organizationId}/${place}`;
}

/** Where this app mirrors Events' tracking, event, workflow and calendar routes for the organization (OE07). */
export function eventsApiPrefix(organizationId: string): string {
  return `/api/organizations/${encodeURIComponent(organizationId)}/events-api`;
}

/** The day before `today` (`YYYY-MM-DD`): the last day an event that has ended by today could have started. */
export function previousIsoDate(today: string): string {
  const date = new Date(Date.UTC(Number(today.slice(0, 4)), Number(today.slice(5, 7)) - 1, Number(today.slice(8, 10))));
  date.setUTCDate(date.getUTCDate() - 1);
  return date.toISOString().slice(0, 10);
}
