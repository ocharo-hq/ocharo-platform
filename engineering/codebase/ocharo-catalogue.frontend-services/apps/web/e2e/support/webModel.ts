import type { Locator } from '@playwright/test';

export interface WebSection {
  title: string;
  layout: string;
  products: { name: string; description: string; images: string[]; prices: string[]; details: string[] }[];
}
export interface WebModel {
  theme: string;
  direction: string;
  language: string;
  title: string;
  coverNote: string;
  footer: string;
  sections: WebSection[];
}

const selectors = {
  preview: {
    section: 'section.web-page',
    heading: '.page-heading h2',
    product: '.products article:not(.product-placeholder)',
    coverNote: '.catalogue-intro p:nth-of-type(2)',
    footer: 'footer',
  },
  reader: {
    section: 'section.collection-page',
    heading: '.collection-section-heading h2',
    product: '.products article.product',
    coverNote: '.cover-note',
    footer: 'footer.collection-footer',
  },
} as const;

/**
 * Reads the visible structure of the designer preview or the released reader: sections, product order, copy, the
 * media hash of every shown image and every visible price line. Both surfaces render the same release fields.
 */
export async function readWebModel(root: Locator, surface: keyof typeof selectors): Promise<WebModel> {
  return root.evaluate((element, s) => {
    const text = (node: Element | null | undefined) => (node?.textContent ?? '').replace(/\s+/g, ' ').trim();
    const theme = [...element.classList].find((name) => name.startsWith('theme-')) ?? '';
    const container = element.closest('[dir]') ?? element;
    return {
      theme,
      direction: element.getAttribute('dir') ?? container.getAttribute('dir') ?? '',
      language: element.getAttribute('lang') ?? container.getAttribute('lang') ?? '',
      title: text(element.querySelector('h1')),
      coverNote: text(element.querySelector(s.coverNote)),
      footer: text(element.querySelector(s.footer)),
      sections: [...element.querySelectorAll(s.section)].map((section) => ({
        title: text(section.querySelector(s.heading)),
        layout: ['spread', 'grid', 'collage'].find((name) => section.classList.contains(name)) ?? '',
        products: [...section.querySelectorAll(s.product)].map((product) => ({
          name: text(product.querySelector('h3')),
          description: text(product.querySelector('p.description')),
          images: [...product.querySelectorAll('.photographs img')].map(
            (image) => (image.getAttribute('src') ?? '').match(/[a-f0-9]{64}/)?.[0] ?? '',
          ),
          prices: [...product.querySelectorAll('.prices p, p.prices')].map(text).filter(Boolean),
          details: [...product.querySelectorAll('dl > div')].map(text).filter(Boolean),
        })),
      })),
    };
  }, selectors[surface]);
}
