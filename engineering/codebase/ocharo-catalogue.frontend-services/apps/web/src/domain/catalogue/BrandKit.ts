/** The slice of the Brand service's public kit (`GET /api/public/brand`) the catalogue prints. */
export interface BrandKit {
  name: string;
  release: number;
  descriptors: { uz: string | null; ru: string | null; en: string | null };
  links: { key: string; label: string; handle: string | null; url: string }[];
  logos: Record<string, { url: string; fileUrl: string; width: number | null; height: number | null }>;
}

/**
 * Brand links first (`Instagram: ocharostile`), then catalogue contact lines that do not repeat
 * a brand handle or URL — a phone line added for print survives, a stale handle copy does not.
 */
export function brandContactLines(kit: BrandKit | null, extraLines: readonly string[]): string[] {
  if (!kit) return [...extraLines];
  const brandLines = kit.links.map((link) => `${link.label}: ${link.handle ?? link.url}`);
  const needles = kit.links
    .flatMap((link) => [link.handle, link.url])
    .filter((value): value is string => !!value)
    .map((value) => value.toLowerCase());
  const extras = extraLines.filter((line) => !needles.some((needle) => line.toLowerCase().includes(needle)));
  return [...brandLines, ...extras];
}
