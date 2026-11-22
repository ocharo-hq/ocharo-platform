// @vitest-environment node
import { parse } from '@vue/compiler-sfc';
import { describe, expect, it } from 'vitest';

/**
 * The top bar names each page (developer, 2026-11-21: "remove page title from page content - it must be in navbar only"),
 * so a page keeps its heading for screen readers only. The organization chooser is the exception: no organization is
 * open yet, so the bar shows no label, and its heading stays visible. The not-found page is an error screen.
 */
const VisibleHeadingPages = ['access/pages/OrganizationPage.vue', 'common/pages/NotFoundPage.vue'];

/** The pages that keep a screen-reader-only heading, with the place each one fills in the top bar. */
const ScreenReaderHeadingPages = [
  'brands/pages/BrandsPage.vue',
  'commerce/pages/CommercePage.vue',
  'common/pages/OverviewPage.vue',
  'content/pages/DraftEditorPage.vue',
  'content/pages/DraftListPage.vue',
  'design/catalogue/CatalogueGalleryPage.vue',
  'design/pages/DesignEditorPage.vue',
  'integrations/pages/IntegrationsPage.vue',
  'media/pages/MediaPage.vue',
  'sourcing/pages/SourcingPage.vue',
  'system/pages/FoundationPage.vue',
];

const sources = import.meta.glob<string>(['../src/presentation/**/pages/*.vue', '../src/presentation/design/catalogue/CatalogueGalleryPage.vue'], {
  eager: true,
  query: '?raw',
  import: 'default',
});

/** The template markup of one page, without comments. */
function markupOf(page: string): string {
  const entry = Object.entries(sources).find(([filename]) => filename.endsWith(`/presentation/${page}`));
  if (!entry) throw new Error(`Missing page ${page}`);
  return (parse(entry[1], { filename: entry[0] }).descriptor.template?.content ?? '').replace(/<!--[\s\S]*?-->/g, '');
}

describe('page headings', () => {
  it.each(ScreenReaderHeadingPages)('keeps the heading of %s for screen readers only', (page) => {
    const markup = markupOf(page);
    expect(markup).toMatch(/<h1\b[^>]*class="sr-only"/);
    expect(markup.match(/<h1\b/g)).toHaveLength(1);
    expect(markup).not.toMatch(/<h1\b(?![^>]*class="sr-only")/);
    expect(markup).not.toContain('page-title');
  });

  it('removes the eyebrow line that labelled each page by its section', () => {
    for (const page of ScreenReaderHeadingPages) expect(markupOf(page), page).not.toMatch(/<p class="eyebrow">[^<]*<\/p>\s*<h1\b/);
  });

  it.each(VisibleHeadingPages)('keeps the visible heading of %s', (page) => {
    const markup = markupOf(page);
    expect(markup).toMatch(/<h1\b/);
    expect(markup).not.toMatch(/<h1\b[^>]*class="sr-only"/);
  });
});
