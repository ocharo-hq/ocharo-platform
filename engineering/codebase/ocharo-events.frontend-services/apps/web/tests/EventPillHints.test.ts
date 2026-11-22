import { describe, expect, it } from 'vitest';
import {
  FacetKind,
  FacetPlacement,
  type FacetChoiceDefinition,
  type FacetDefinition,
} from '@wow-two-beta/fe/presentation/forms';

import { eventPillHints, pillHintFadeDuration, pillHintOpenDelay, withPillHints } from '@/domain/events/EventPillHints';
import { ocharoKinds, ocharoRegions } from '@/domain/events/Events';

const choice = (
  key: string,
  values: string[],
  kind: FacetChoiceDefinition['kind'] = FacetKind.Multi,
): FacetDefinition => ({
  key,
  label: key,
  kind,
  placement: FacetPlacement.Inline,
  options: values.map((value) => ({ value, label: value, count: 1 })),
});

describe('the pill hints', () => {
  it('hold the 11 one-liners of the pills doc: 3 regions and 8 kinds, English only', () => {
    expect(Object.keys(eventPillHints)).toEqual(['scope', 'kind']);
    expect(Object.keys(eventPillHints.scope!)).toEqual(['national', 'central-asia', 'international']);
    expect(Object.keys(eventPillHints.kind!)).toEqual([
      'runway',
      'trade-fair',
      'market',
      'exhibition',
      'competition',
      'festival',
      'conference',
      'other',
    ]);
    expect(eventPillHints.scope!.national).toBe('Events in Uzbekistan.');
    expect(eventPillHints.scope!['central-asia']).toBe(
      'Events in Kazakhstan, Kyrgyzstan, Tajikistan and Turkmenistan.',
    );
    expect(eventPillHints.scope!.international).toBe('Events anywhere else in the world.');
    expect(eventPillHints.kind!.runway).toBe('A fashion show where designers present collections on a catwalk.');
    expect(eventPillHints.kind!['trade-fair']).toBe(
      'A business fair where brands, makers and buyers meet to sell and source goods.',
    );
    expect(eventPillHints.kind!.market).toBe(
      'A public pop-up market where independent brands sell straight to shoppers.',
    );
    expect(eventPillHints.kind!.exhibition).toBe(
      'A curated display of fashion, textile or design work that people visit.',
    );
    expect(eventPillHints.kind!.competition).toBe('A contest or award where designers or brands compete for a prize.');
    expect(eventPillHints.kind!.festival).toBe('A multi-day public celebration of fashion, craft or culture.');
    expect(eventPillHints.kind!.conference).toBe('A talk-led gathering where the industry shares knowledge and meets.');
    expect(eventPillHints.kind!.other).toBe('Any event that fits none of these but could still matter to Ocharo.');
  });

  it('cover every region and every kind Ocharo offers', () => {
    expect(ocharoRegions().map((region) => region.key)).toEqual(Object.keys(eventPillHints.scope!));
    expect(ocharoKinds().map((kind) => kind.key)).toEqual(Object.keys(eventPillHints.kind!));
  });

  it('start the hint card after 5 seconds with a 200 ms fade, as named values to change in one place', () => {
    expect(pillHintOpenDelay).toBe(5000);
    expect(pillHintFadeDuration).toBe(200);
  });
});

describe('decorating facets with hints', () => {
  it('gives each scope and kind option a hint with its description when the table holds its value', () => {
    const [scope, kind] = withPillHints([
      choice('scope', ['all', 'national', 'central-asia', 'international'], FacetKind.Single),
      choice('kind', ['runway', 'trade-fair', 'webinar']),
    ]) as FacetChoiceDefinition[];

    expect(scope!.options.map((option) => option.hint?.description)).toEqual([
      undefined,
      'Events in Uzbekistan.',
      'Events in Kazakhstan, Kyrgyzstan, Tajikistan and Turkmenistan.',
      'Events anywhere else in the world.',
    ]);
    expect(kind!.options.map((option) => option.hint?.description)).toEqual([
      'A fashion show where designers present collections on a catwalk.',
      'A business fair where brands, makers and buyers meet to sell and source goods.',
      undefined,
    ]);
    // A hint is its description alone: no image until the icons exist.
    expect(scope!.options[1]!.hint).toEqual({ description: 'Events in Uzbekistan.' });
  });

  it('keeps the rest of each option: its value, label and count', () => {
    const [kind] = withPillHints([choice('kind', ['market'])]) as FacetChoiceDefinition[];

    expect(kind!.options[0]).toMatchObject({ value: 'market', label: 'market', count: 1 });
  });

  it('leaves the other facets, the toggles and the options with no hint as they were, the same objects', () => {
    const stage = choice('stage', ['runway', 'national']);
    const city = choice('city', ['other']);
    const toggle: FacetDefinition = {
      key: 'priority',
      label: 'High priority',
      kind: FacetKind.Toggle,
      placement: FacetPlacement.Popover,
      count: 2,
    };
    const plain = choice('kind', ['webinar']);

    const decorated = withPillHints([stage, city, toggle, plain]);

    expect(decorated[0]).toBe(stage);
    expect(decorated[1]).toBe(city);
    expect(decorated[2]).toBe(toggle);
    expect((decorated[3] as FacetChoiceDefinition).options[0]).toBe((plain as FacetChoiceDefinition).options[0]);
  });

  it('does not change the facets it is given, and keeps their order', () => {
    const scope = choice('scope', ['national']);
    const definitions = [choice('stage', ['candidate']), scope];

    const decorated = withPillHints(definitions);

    expect(decorated.map((definition) => definition.key)).toEqual(['stage', 'scope']);
    expect((scope as FacetChoiceDefinition).options[0]).not.toHaveProperty('hint');
  });

  it('reads no hint from a value that is a member of every object, such as constructor', () => {
    const [kind] = withPillHints([choice('kind', ['constructor', 'toString'])]) as FacetChoiceDefinition[];

    expect(kind!.options.every((option) => option.hint === undefined)).toBe(true);
  });
});
