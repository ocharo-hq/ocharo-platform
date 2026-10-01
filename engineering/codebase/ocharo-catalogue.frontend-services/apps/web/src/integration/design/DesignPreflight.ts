import { selectDesignPages } from '../../domain/design/DesignPageSelection';
import {
  crossesPageEdge,
  type DesignPreflightIssue,
  type DesignPreflightReport,
} from '../../domain/design/DesignPreflight';
import type { DesignSnapshot } from '../../domain/design/EditorModels';
import { loadDesignImage } from './DesignExport';
import { layoutDesignText } from './DesignTextLayout';

/** Checks the selected export scope without mutating any original page or media. */
export async function inspectDesign(
  snapshot: DesignSnapshot,
  pageIds?: readonly string[],
  signal?: AbortSignal,
  imageLoader: typeof loadDesignImage = loadDesignImage,
): Promise<DesignPreflightReport> {
  const checked = selectDesignPages(snapshot, pageIds);
  signal?.throwIfAborted();
  await document.fonts.ready;
  signal?.throwIfAborted();
  const context = document.createElement('canvas').getContext('2d');
  if (!context) throw new Error('Page checks are unavailable in this browser.');
  const issues: DesignPreflightIssue[] = [],
    images = new Map<string, Promise<HTMLImageElement>>();
  for (const page of checked.pages) {
    const unit = Math.min(
      (checked.kind === 'business-card' ? 600 : 240) / 25.4,
      8192 / Math.max(page.width, page.height),
      Math.sqrt(12_000_000 / (page.width * page.height)),
    );
    for (const element of page.elements) {
      signal?.throwIfAborted();
      if (element.opacity === 0) continue;
      const add = (
        code: DesignPreflightIssue['code'],
        severity: DesignPreflightIssue['severity'],
        message: string,
        variables?: Record<string, string | number>,
      ) => {
        issues.push({
          pageId: page.id,
          pageNumber: snapshot.pages.findIndex((item) => item.id === page.id) + 1,
          pageTitle: page.title,
          elementId: element.id,
          elementLabel: element.label,
          code,
          severity,
          message,
          variables,
        });
      };
      if (crossesPageEdge(element, page))
        add('outside-page', 'warning', 'Extends beyond the page. That part will be cropped.');
      if (checked.kind === 'catalogue' && element.type === 'text') {
        if (
          element.binding &&
          !element.text?.trim() &&
          ((element.binding.kind === 'product' && ['name', 'price'].includes(element.binding.field)) ||
            (element.binding.kind === 'shared' && ['collection', 'sizes', 'pieces'].includes(element.binding.field)))
        )
          add('missing-translation', 'error', 'Complete this {locale} catalogue text before export.', {
            locale: checked.locale,
          });
        if (
          element.binding?.kind === 'product' &&
          element.binding.field === 'price-label' &&
          checked.catalogueSettings?.priceDisplay.showQuantity &&
          !checked.catalogueSettings.copyByLocale[checked.locale]?.pieces?.trim()
        )
          add('missing-translation', 'error', 'Add the {locale} quantity unit in shared catalogue text.', {
            locale: checked.locale,
          });
        if (
          !['uz', 'ru', 'en'].includes(checked.locale) &&
          !element.binding &&
          element.role !== 'page-number' &&
          element.text?.trim() &&
          !/^\d+$/.test(element.text)
        )
          add(
            'independent-copy',
            'warning',
            'Confirm this independent text is in {locale}. It is not connected to translated product fields.',
            { locale: checked.locale },
          );
      }
      if (element.type === 'text' && layoutDesignText(context, element, unit).overflow)
        add(
          'text-overflow',
          'warning',
          'Text extends beyond its frame and will be clipped. Enlarge the frame or shorten the text.',
        );
      if (element.type === 'image') {
        if (!element.src) add('missing-image', 'error', 'No image is selected. Replace this photo before exporting.');
        else {
          try {
            let loaded = images.get(element.src);
            if (!loaded) {
              loaded = imageLoader(element.src, signal);
              images.set(element.src, loaded);
            }
            await loaded;
          } catch {
            signal?.throwIfAborted();
            add('missing-image', 'error', 'The image could not load. Check your connection or replace this photo.');
          }
        }
      }
    }
  }
  return { pageCount: checked.pages.length, issues };
}
