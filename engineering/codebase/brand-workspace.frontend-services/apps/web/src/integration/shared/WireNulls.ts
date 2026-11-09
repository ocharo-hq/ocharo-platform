/** @internal Checks an untrusted JSON object. */
const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null && !Array.isArray(value);

/**
 * Restores the members Desk's SDK JSON leaves out when they are null, so a decoded value carries the `null` its model
 * declares. Present members, and values that are not objects, pass through unchanged for the decoder to judge.
 */
export function withNulls(value: unknown, keys: readonly string[]): unknown {
  if (!isRecord(value)) return value;
  const missing = keys.filter((key) => !(key in value));
  return missing.length === 0 ? value : { ...value, ...Object.fromEntries(missing.map((key) => [key, null])) };
}

/** Applies `normalize` to each element of a JSON array; any other value passes through unchanged. */
export function eachWithNulls(value: unknown, normalize: (item: unknown) => unknown): unknown {
  return Array.isArray(value) ? value.map(normalize) : value;
}

/** Applies `normalize` to the `key` member of a JSON object; any other value passes through unchanged. */
export function memberWithNulls(value: unknown, key: string, normalize: (item: unknown) => unknown): unknown {
  return isRecord(value) && key in value ? { ...value, [key]: normalize(value[key]) } : value;
}
