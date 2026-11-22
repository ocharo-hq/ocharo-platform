import { shallowRef, watch } from 'vue';
import { useRoute, useRouter, type LocationQuery, type LocationQueryRaw } from 'vue-router';

/** The query values a page's filters write: a key undefined (or left out) when the filters have nothing for it. */
export type AddressFilterQuery = Readonly<Record<string, string | readonly string[] | undefined>>;

/** Filters held somewhere in a page, kept in step with the address; nothing in it is specific to a page. */
export interface AddressFilterState {
  /** The query keys the filters own: the ones they write and read. The query's other keys are never touched. */
  readonly keys: readonly string[];
  /** The query values the filters write now. A getter over reactive state, so the address follows each change. */
  write(): AddressFilterQuery;
  /** Takes the filters an address's query holds in place of the current ones; a query with none reads as no filters. */
  read(query: LocationQuery): void;
}

/** The non-empty strings a query value holds, whether it is one value, a repeated key or a bare key (none). */
function textsOf(value: unknown): string[] {
  return (Array.isArray(value) ? value : [value]).filter(
    (part): part is string => typeof part === 'string' && part !== '',
  );
}

/**
 * The values of the owned keys as one text to compare, whatever form they come in: a key with one value and one with a
 * list of one are the same, and an absent, empty or bare key is left out.
 */
function comparable(keys: readonly string[], query: Readonly<Record<string, unknown>>): string {
  return JSON.stringify(
    [...keys]
      .sort()
      .map((key) => [key, textsOf(query[key])] as const)
      .filter(([, values]) => values.length > 0),
  );
}

/**
 * Keeps a page's filters in its address, call in a component's setup: they open on the address's query and follow it
 * when it changes (the back button, a link), and a choice shows at once and is written to the address after it,
 * replacing the entry in place so the back button does not step through each click or key, and leaving the query's other
 * keys as they are. Each side acts only when the other differs, so a write does not read itself back (a search typed
 * with a trailing space stays as typed while the address holds it trimmed), and the filters stay out of the address's way
 * while a write is on its way: the address then still holds an earlier choice, which must not undo the one just made
 * (two quick clicks, a word typed in the search). Once the page's route is left, it neither reads nor writes: the address
 * is another page's then.
 */
export function bindAddressFilters(state: AddressFilterState): void {
  const route = useRoute();
  const router = useRouter();
  const page = route.matched.at(-1);
  const here = () => route.matched.at(-1) === page;
  const held = () => comparable(state.keys, state.write());
  const addressed = () => comparable(state.keys, route.query);
  let writing = 0;

  state.read(route.query);

  watch(held, () => {
    if (!here() || addressed() === held()) return;
    const query: LocationQueryRaw = Object.fromEntries(
      Object.entries(route.query).filter(([key]) => !state.keys.includes(key)),
    );
    for (const [key, value] of Object.entries(state.write()))
      if (value !== undefined) query[key] = typeof value === 'string' ? value : [...value];
    writing += 1;
    void router.replace({ query }).finally(() => {
      writing -= 1;
    });
  });

  watch(addressed, () => {
    if (!here() || writing > 0 || addressed() === held()) return;
    state.read(route.query);
  });
}

/** How one page's filters become query values and back, for a page that holds them as one value. */
export interface AddressFilterCodec<Filters> {
  /** The query keys the filters own; see `AddressFilterState`. */
  readonly keys: readonly string[];
  /** Reads the filters an address's query holds; a query with none reads as no filters. */
  read(query: Readonly<Record<string, unknown>>): Filters;
  /** The query values the filters write, each key undefined when the filters have nothing for it. */
  write(filters: Filters): AddressFilterQuery;
}

/**
 * A page's filters as one value kept in its address (`bindAddressFilters`): `filters` is the value, `setFilters` chooses
 * another, which shows at once and reaches the address after it.
 */
export function useAddressFilters<Filters>(codec: AddressFilterCodec<Filters>) {
  const filters = shallowRef<Filters>(codec.read({}));
  bindAddressFilters({
    keys: codec.keys,
    write: () => codec.write(filters.value),
    read: (query) => {
      filters.value = codec.read(query);
    },
  });

  /** Chooses the filters; the address follows. */
  function setFilters(next: Filters): void {
    filters.value = next;
  }

  return { filters, setFilters };
}
