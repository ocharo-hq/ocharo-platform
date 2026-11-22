# Events filters

*Last updated: 2026-11-22*

One filter card above the Feed and the Calendar, with the same facets in the same place. Region and kind are pills; stage, city and source sit behind the card's Filters button. Decided with the developer on 2026-11-22.

## Pills

Each pill has one line that defines it. The line is the tooltip's text and the brief for its icon.

| Facet | Key | Label | One line | Icon idea |
|---|---|---|---|---|
| Region | `national` | National | Events in Uzbekistan. | a simple map outline of Uzbekistan |
| Region | `central-asia` | Central Asia | Events in Kazakhstan, Kyrgyzstan, Tajikistan and Turkmenistan. | four linked dots on a map outline |
| Region | `international` | International | Events anywhere else in the world. | a globe |
| Kind | `runway` | Runway show | A fashion show where designers present collections on a catwalk. | a catwalk with a standing figure |
| Kind | `trade-fair` | Trade fair | A business fair where brands, makers and buyers meet to sell and source goods. | two stalls with a handshake |
| Kind | `market` | Market | A public pop-up market where independent brands sell straight to shoppers. | a market stall with an awning |
| Kind | `exhibition` | Exhibition | A curated display of fashion, textile or design work that people visit. | a framed piece on a wall |
| Kind | `competition` | Competition | A contest or award where designers or brands compete for a prize. | a trophy |
| Kind | `festival` | Festival | A multi-day public celebration of fashion, craft or culture. | bunting flags |
| Kind | `conference` | Conference | A talk-led gathering where the industry shares knowledge and meets. | a speaker at a lectern |
| Kind | `other` | Other | Any event that fits none of these but could still matter to Ocharo. | a sparkle |

## Icon brief

For the generated pill icons:

- One icon per row above, 11 in all, named by its key (`runway.svg`).
- Single colour, drawn with `currentColor`, on a transparent background, so it follows the theme in light and dark.
- 24 px grid, 1.75 px rounded strokes, no fills except small accents, no text, no outer frame.
- Reads at 16 px next to a label; the tooltip may show it larger.

## Pill anatomy

- Left to right: icon (16 px), label, count.
- The count is muted, in tabular figures, and counts what choosing the pill would match (the other facets applied, not its own).
- A pill with a count of 0 is greyed (muted text and icon) but stays pressable, so an address that names it can still be undone.
- A chosen pill is filled.
- The tooltip is a mini card: the one-liner under the label, with the icon or an image above it, and the number of events in view. It opens after 5 seconds on hover or focus (developer, 2026-11-22). The fade-in time is open: see the tooltip convention in the backlog.
- On a phone there is no hover: the tooltip is reached by long press, or the pill shows its icon and count only.

## Open

- The one-liners and the icon ideas are drafts for the developer to edit before they go to the icon generator.
- The Feed shares the card once the kit exposes its filter state (a request to the sources lane).
