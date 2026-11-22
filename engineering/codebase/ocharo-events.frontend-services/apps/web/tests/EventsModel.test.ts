import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';
import type { WorkflowModel } from '@wow-two-beta/fe-events/contracts';
import { regionOfCountry, regionQuery } from '@wow-two-beta/fe-events/kit';

import {
  eventFormats,
  eventStatuses,
  eventsApiPrefix,
  eventsPath,
  ocharoKinds,
  ocharoProposal,
  ocharoRegions,
  ocharoWorkflow,
  ocharoWorkflowSpec,
  previousIsoDate,
  readEventsAddress,
  readEventsOrganizationId,
  workflowForPage,
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

  it("equals the file the deploy writes with Events' `workflow put --spec`, so the page and the stored workflow never drift", () => {
    const file = resolve(import.meta.dirname, '../../../../../deployment/events/ocharo-workflow.json');
    expect(JSON.parse(readFileSync(file, 'utf8'))).toEqual(JSON.parse(JSON.stringify(ocharoWorkflowSpec())));
  });

  it("shows Ocharo's own stages and fields outright while Events still holds its default workflow (revision 0)", () => {
    const events: WorkflowModel = {
      stages: [{ key: 'interested', label: { en: 'Interested' }, class: 'open' }],
      defaultStage: 'interested',
      revision: 0,
    };
    expect(workflowForPage(events)).toEqual({ ...ocharoWorkflowSpec(), revision: 0 });
  });

  it("keeps what Events stored and fills only the labels and titles it lacks from Ocharo's spec", () => {
    const spec = ocharoWorkflowSpec();
    const stored: WorkflowModel = {
      stages: [
        { key: 'candidate', label: { en: 'Candidate' }, class: 'open' },
        { key: 'shortlisted', label: { en: 'Short list', ru: 'Список' }, class: 'active' },
        { key: 'extra', label: { en: 'Extra' }, class: 'open' },
      ],
      defaultStage: 'extra',
      fields: {
        type: 'object',
        properties: {
          cost: { type: 'string', title: { en: 'Price' }, maxLength: 50 },
          participation: { type: 'string', maxLength: 10 },
          internal: { type: 'string' },
        },
      },
      revision: 3,
    };

    const shown = workflowForPage(stored);

    expect(shown.revision).toBe(3);
    expect(shown.defaultStage).toBe('extra');
    expect(shown.stages.map((stage) => [stage.key, stage.class])).toEqual([
      ['candidate', 'open'],
      ['shortlisted', 'active'],
      ['extra', 'open'],
    ]);
    expect(shown.stages[0]!.label).toEqual(spec.stages[0]!.label);
    expect(shown.stages[1]!.label).toEqual({ ...spec.stages[1]!.label, en: 'Short list', ru: 'Список' });
    expect(shown.stages[2]!.label).toEqual({ en: 'Extra' });
    const properties = (shown.fields as { properties: Record<string, Record<string, unknown>> }).properties;
    expect(properties.cost).toEqual({ type: 'string', title: { en: 'Price' }, maxLength: 50 });
    expect(properties.participation).toEqual({
      type: 'string',
      maxLength: 10,
      title: (spec.fields as { properties: { participation: { title: unknown } } }).properties.participation.title,
    });
    expect(properties.internal).toEqual({ type: 'string' });
  });

  it("reads Ocharo's fields when the stored workflow has none", () => {
    const stored: WorkflowModel = {
      stages: [{ key: 'candidate', class: 'open' }],
      defaultStage: 'candidate',
      revision: 2,
    };
    expect(workflowForPage(stored).fields).toEqual(ocharoWorkflowSpec().fields);
    expect(workflowForPage({ ...stored, fields: { type: 'object' } }).fields).toEqual(ocharoWorkflowSpec().fields);
  });
});

describe("Ocharo's kit configuration", () => {
  it('labels the eight kinds in three languages', () => {
    const kinds = ocharoKinds();
    expect(kinds.map((kind) => kind.key)).toEqual([...eventFormats]);
    expect(kinds.find((kind) => kind.key === 'trade-fair')?.label?.en).toBe('Trade fair');
    for (const kind of kinds) expect(kind.label?.ru).not.toBe(kind.label?.en);
  });

  it('derives the scope from the country: Uzbekistan national, four neighbours Central Asia, every other country international, none unknown', () => {
    const regions = ocharoRegions();
    expect(regions.map((region) => [region.key, region.label.en])).toEqual([
      ['national', 'National'],
      ['central-asia', 'Central Asia'],
      ['international', 'International'],
    ]);
    expect(regionOfCountry('UZ', regions)?.key).toBe('national');
    for (const country of ['KZ', 'kg', 'TJ', 'TM']) expect(regionOfCountry(country, regions)?.key).toBe('central-asia');
    for (const country of ['RU', 'AZ', 'AF', 'FR', 'US'])
      expect(regionOfCountry(country, regions)?.key).toBe('international');
    expect(regionOfCountry(null, regions)).toBeNull();
    expect(regionOfCountry('', regions)).toBeNull();
    expect(regionQuery(regions[1], regions)).toEqual({ countries: ['KZ', 'KG', 'TJ', 'TM'] });
    expect(regionQuery(regions[2], regions)).toEqual({
      excludeCountries: ['UZ', 'KZ', 'KG', 'TJ', 'TM'],
      includeUnknownCountry: false,
    });
  });

  it('proposes a published, shortlisted event', () => {
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
