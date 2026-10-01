export const releaseId = 'ec058c68-ce43-4d72-9169-97906587d5a9';
export const firstHash = 'a'.repeat(64),
  secondHash = 'b'.repeat(64);
export const pinned = (file: string) =>
  `/api/public/catalogue/editions/woven-days/assets/${file}?locale=en&release=${releaseId}`;
export function fixture() {
  return {
    id: releaseId,
    slug: 'woven-days',
    title: 'Woven days',
    description: 'A considered collection.',
    locale: 'en',
    updatedAt: '2026-10-01T00:00:00Z',
    availableLocales: ['en', 'fr', 'draft'],
    products: [
      {
        id: 'nimcha-v01',
        name: 'Nimcha',
        fabric: 'Adras',
        description: 'Woven by hand',
        decoration: null,
        garmentType: 'nimcha',
        garmentName: 'Vest',
        variantCode: 'v01',
        localeStatus: 'approved',
        sizes: ['S', 'M'],
        tags: [],
        availability: 'unknown',
        availableSizes: [],
        pricing: {
          currency: 'UZS',
          status: 'confirmed',
          retail: 130000,
          wholesale: 100000,
          tiers: [
            { minimumQuantity: 1, maximumQuantity: 9, unitPrice: 130000 },
            { minimumQuantity: 10, maximumQuantity: null, unitPrice: 100000 },
          ],
          wording: null,
        },
        images: [firstHash, secondHash].map((sha256, index) => ({
          sha256,
          role: index ? 'model' : 'cutout',
          url: `/api/catalogue/media/${sha256}`,
          previewUrl: null,
          width: 60,
          height: 90,
          alt: index ? 'Model' : 'Front',
          isActive: true,
        })),
      },
    ],
    web: {
      theme: 'editorial',
      mediaGroup: 'all',
      priceDisplay: { show: true, showCurrency: true, showQuantity: true, showRetail: false, showWholesale: true },
      copy: {
        header: 'Ocharo',
        collection: 'Spring',
        coverNote: 'Made with care',
        footer: 'Thank you',
        materials: 'Fabric',
        decoration: 'Embellishment',
        sizes: 'Sizes',
        pieces: 'pieces',
        priceOnRequest: 'Ask for a price',
      },
      productMedia: { 'nimcha-v01': [secondHash, firstHash] },
      pages: [{ id: 'first-look', title: 'First look', layout: 'spread', productIds: ['nimcha-v01'] }],
    },
    pdf: {
      pdfUrl: pinned('catalogue.pdf'),
      pages: [
        {
          id: 'one',
          number: 1,
          title: 'Cover',
          widthMm: 210,
          heightMm: 297,
          productIds: [],
          imageUrl: pinned('page-one.png'),
          pdfUrl: pinned('page-one.pdf'),
        },
      ],
    },
  };
}
