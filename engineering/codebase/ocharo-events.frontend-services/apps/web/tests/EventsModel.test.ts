import { describe, expect, it } from 'vitest';
import { regionOfCountry, regionQuery } from '@wow-two-beta/fe-events/kit';

import {
  eventFormats,
  eventStatuses,
  eventsApiPrefix,
  eventsPath,
  ocharoCostField,
  ocharoKinds,
  ocharoProposal,
  ocharoRegions,
  ocharoWorkflow,
  ocharoWorkflowSpec,
  previousIsoDate,
  readEventsAddress,
  readEventsOrganizationId,
  workflowAligned,
} from '@/domain/events/Events';

const organization = '11111111-1111-4111-8111-111111111111';
const eventId = '22222222-2222-4222-8222-222222222222';

describe('events locations', () => {
  it('reads the feed and one event, and falls back to the feed for anything else', () => {
    expect(readEventsOrganizationId(`/organizations/${organization}/events`)).toBe(organization);
    expect(readEventsOrganizationId(`/organizations/${organization}/sourcing`)).toBeNull();
    expect(readEventsAddress(`/organizations/${organization}/events`)).toEqual({ view: 'feed', eventId: null });
    expect(readEventsAddress(`/organizations/${organization}/events/${eventId}`)).toEqual({ view: 'event', eventId });
    expect(readEventsAddress(`/organizations/${organization}/events/${eventId.toUpperCase()}`)).toEqual({
      view: 'event',
      eventId,
    });
    for (const tail of ['new', 'not-an-event', `${eventId}/extra`, '12345'])
      expect(readEventsAddress(`/organizations/${organization}/events/${tail}`)).toEqual({
        view: 'feed',
        eventId: null,
      });
    expect(readEventsAddress(`/organizations/${organization}/sourcing`)).toBeNull();
  });

  it('builds addresses that read back to the same location', () => {
    for (const location of [
      { view: 'feed', eventId: null },
      { view: 'event', eventId },
    ] as const) {
      const path = eventsPath(organization, location);
      expect(readEventsAddress(path)).toEqual(location);
    }
    expect(eventsPath(organization, { view: 'event', eventId: 'javascript:alert(1)' })).toBe(
      `/organizations/${organization}/events`,
    );
    expect(() => eventsPath('not-an-organization', { view: 'feed', eventId: null })).toThrow();
  });

  it("reaches Events through this app's mirror for the organization", () => {
    expect(eventsApiPrefix(organization)).toBe(`/api/organizations/${organization}/events-api`);
  });
});

describe("Ocharo's workflow in WoW2 Events", () => {
  it('holds the eight stages in order with labels in three languages, candidate the default', () => {
    const spec = ocharoWorkflowSpec();
    expect(spec.stages.map((stage) => stage.key)).toEqual([...eventStatuses]);
    expect(spec.defaultStage).toBe('candidate');
    expect(spec.stages.find((stage) => stage.key === 'shortlisted')?.label).toEqual({
      en: 'Shortlisted',
      uz: expect.any(String),
      ru: expect.any(String),
    });
    for (const stage of spec.stages) {
      expect(stage.label?.uz).not.toBe(stage.label?.en);
      expect(stage.label?.ru).not.toBe(stage.label?.en);
    }
    expect(ocharoWorkflow()).toEqual({ stages: spec.stages, defaultStage: 'candidate' });
  });

  it('marks skipped and rejected lost, so neither reaches the deadlines panel, and attended won', () => {
    const classes = Object.fromEntries(ocharoWorkflowSpec().stages.map((stage) => [stage.key, stage.class]));
    expect(classes).toEqual({
      candidate: 'open',
      shortlisted: 'open',
      applying: 'active',
      applied: 'active',
      confirmed: 'active',
      attended: 'won',
      skipped: 'lost',
      rejected: 'lost',
    });
  });

  it('titles its fields in three languages and keeps the source link read-only', () => {
    const fields = ocharoWorkflowSpec().fields as {
      properties: Record<string, { title: Record<string, string>; readOnly?: boolean; maxLength?: number }>;
    };
    expect(Object.keys(fields.properties)).toEqual(['participation', 'cost', 'sourceUrl']);
    expect(fields.properties.participation?.title.en).toBe('How to take part');
    expect(fields.properties.participation?.maxLength).toBe(2000);
    expect(fields.properties.cost?.title).toEqual({ en: "Ocharo's cost", uz: 'Ocharo xarajati', ru: 'Расходы Ocharo' });
    expect(fields.properties.cost?.maxLength).toBe(500);
    expect(fields.properties.sourceUrl?.readOnly).toBe(true);
    expect(fields.properties.participation?.readOnly).toBeUndefined();
  });

  it('reads a stored workflow as aligned whatever key order the store kept, and not once a label or class differs', () => {
    const spec = ocharoWorkflowSpec();
    const reordered = JSON.parse(JSON.stringify(spec), (_key, value: unknown) =>
      value && typeof value === 'object' && !Array.isArray(value)
        ? Object.fromEntries(Object.entries(value).reverse())
        : value,
    ) as typeof spec;
    expect(workflowAligned({ ...reordered, revision: 4 } as never)).toBe(true);
    expect(workflowAligned({ ...spec, stages: spec.stages.map((stage) => ({ key: stage.key })) })).toBe(false);
    expect(
      workflowAligned({
        ...spec,
        stages: spec.stages.map((stage) => (stage.key === 'skipped' ? { ...stage, class: 'open' as const } : stage)),
      }),
    ).toBe(false);
    expect(workflowAligned({ ...spec, fields: null })).toBe(false);
    expect(workflowAligned({ ...spec, defaultStage: 'shortlisted' })).toBe(false);
  });
});

describe("Ocharo's kit configuration", () => {
  it('labels the eight kinds in three languages', () => {
    const kinds = ocharoKinds();
    expect(kinds.map((kind) => kind.key)).toEqual([...eventFormats]);
    expect(kinds.find((kind) => kind.key === 'trade-fair')?.label?.en).toBe('Trade fair');
    for (const kind of kinds) expect(kind.label?.ru).not.toBe(kind.label?.en);
  });

  it('derives the scope from the country: Uzbekistan local, its neighbours regional, others international, none unknown', () => {
    const regions = ocharoRegions();
    expect(regionOfCountry('UZ', regions)?.key).toBe('local');
    expect(regionOfCountry('kz', regions)?.key).toBe('regional');
    expect(regionOfCountry('FR', regions)?.key).toBe('international');
    expect(regionOfCountry(null, regions)).toBeNull();
    expect(regionQuery(regions[2], regions)).toEqual({
      excludeCountries: ['UZ', 'KZ', 'KG', 'TJ', 'TM', 'AF', 'AZ', 'RU'],
      includeUnknownCountry: false,
    });
  });

  it("shows Ocharo's cost apart from the ticket price, and proposes a published, shortlisted event", () => {
    expect(ocharoCostField()).toMatchObject({ key: 'cost', empty: { en: 'Cost not stated' } });
    expect(ocharoProposal()).toEqual({
      initialTracking: { stage: 'shortlisted', priority: 'normal' },
      sourceField: 'sourceUrl',
      withDeadline: true,
      withFoundAt: true,
      privateCatalog: true,
    });
  });

  it('names the day before, across months and years', () => {
    expect(previousIsoDate('2026-11-19')).toBe('2026-11-18');
    expect(previousIsoDate('2026-03-01')).toBe('2026-02-28');
    expect(previousIsoDate('2027-01-01')).toBe('2026-12-31');
  });
});
