/**
 * The one line each region and kind pill defines itself with, for the hint card the fe sdk's `FacetBar` opens over an
 * inline pill (`product/features/events-filters.md` § Pills, in English only, as the interface is), by facet key and then
 * option value. A facet or value not listed here has no hint.
 */
export const eventPillHints: Readonly<Record<string, Readonly<Record<string, string>>>> = {
  scope: {
    national: 'Events in Uzbekistan.',
    'central-asia': 'Events in Kazakhstan, Kyrgyzstan, Tajikistan and Turkmenistan.',
    international: 'Events anywhere else in the world.',
  },
  kind: {
    runway: 'A fashion show where designers present collections on a catwalk.',
    'trade-fair': 'A business fair where brands, makers and buyers meet to sell and source goods.',
    market: 'A public pop-up market where independent brands sell straight to shoppers.',
    exhibition: 'A curated display of fashion, textile or design work that people visit.',
    competition: 'A contest or award where designers or brands compete for a prize.',
    festival: 'A multi-day public celebration of fashion, craft or culture.',
    conference: 'A talk-led gathering where the industry shares knowledge and meets.',
    other: 'Any event that fits none of these but could still matter to Ocharo.',
  },
};

/**
 * How long a pill waits before its hint card opens, in ms (developer, 2026-11-22: "after 5 seconds"). The W2 tooltip
 * convention (how long a tooltip waits, how it fades) is still to be found; this and `pillHintFadeDuration` are the
 * developer's starting values for the filter card, kept here so one edit changes both pages.
 */
export const pillHintOpenDelay = 5000;

/** How long a hint card takes to fade in and out, in ms; a starting value like `pillHintOpenDelay`. */
export const pillHintFadeDuration = 200;

/** What `withPillHints` reads of a facet and its choices; the fe sdk's `FacetDefinition` fits it, a toggle having no `options`. */
interface PillFacet {
  readonly key: string;
  readonly options?: readonly { readonly value: string }[];
}

/**
 * The facets with a hint on each choice that has one: every option of the `scope` and `kind` facets whose value
 * `eventPillHints` lists gets `hint: { description }`; every other option and facet, a toggle among them, comes back as
 * it was. It names no type of the fe sdk, which the domain does not import, so it takes any facets shaped like its own.
 */
export function withPillHints<Facet extends PillFacet>(definitions: readonly Facet[]): Facet[] {
  return definitions.map((definition) => {
    if (!definition.options || !Object.hasOwn(eventPillHints, definition.key)) return definition;
    const hints = eventPillHints[definition.key]!;
    return {
      ...definition,
      options: definition.options.map((option) =>
        Object.hasOwn(hints, option.value) ? { ...option, hint: { description: hints[option.value]! } } : option,
      ),
    };
  });
}
