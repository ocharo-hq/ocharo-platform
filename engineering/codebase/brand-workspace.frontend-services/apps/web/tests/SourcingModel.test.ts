import { describe, expect, it } from 'vitest';

import { safeWorkspaceDestination } from '@/domain/workspace/EditorNavigation';
import { readWorkspaceLocation } from '@/domain/workspace/Models';
import { routeHref, safeHttpUrl, sourcingPath, uploadRejection } from '@/domain/workspace/Sourcing';

const organization = '11111111-1111-4111-8111-111111111111';
const supplier = '22222222-2222-4222-8222-222222222222';

describe('sourcing locations', () => {
  it('reads the list, inbox and supplier routes without changing other sections', () => {
    expect(readWorkspaceLocation(`/organizations/${organization}/sourcing`))
      .toEqual({ organizationId: organization, section: 'sourcing', draftId: null, sourcing: { view: 'suppliers', supplierId: null } });
    expect(readWorkspaceLocation(`/organizations/${organization}/sourcing/inbox`).sourcing).toEqual({ view: 'inbox', supplierId: null });
    expect(readWorkspaceLocation(`/organizations/${organization}/sourcing/${supplier}`).sourcing).toEqual({ view: 'supplier', supplierId: supplier });
    expect(readWorkspaceLocation(`/organizations/${organization}/sourcing/not-a-supplier`).sourcing).toEqual({ view: 'suppliers', supplierId: null });
    expect(readWorkspaceLocation(`/organizations/${organization}/sourcing/${supplier}/extra`).sourcing).toEqual({ view: 'suppliers', supplierId: null });
    expect(readWorkspaceLocation(`/organizations/${organization}/products`)).toEqual({ organizationId: organization, section: 'products', draftId: null });
    expect(readWorkspaceLocation(`/organizations/${organization}/products`)).not.toHaveProperty('sourcing');
  });

  it('builds addresses that read back to the same location and survive sign-in', () => {
    for (const location of [{ view: 'materials', supplierId: null }, { view: 'suppliers', supplierId: null }, { view: 'inbox', supplierId: null }, { view: 'supplier', supplierId: supplier }] as const) {
      const path = sourcingPath(organization, location);
      expect(readWorkspaceLocation(path).sourcing).toEqual(location);
      expect(safeWorkspaceDestination(path)).toBe(path);
    }
    expect(() => sourcingPath('not-an-organization', { view: 'inbox', supplierId: null })).toThrow();
    expect(safeWorkspaceDestination(`/organizations/${organization}/sourcing/unknown`)).toBe('/');
  });
});

describe('contact route links', () => {
  it('links WhatsApp numbers to wa.me with digits only', () => {
    expect(routeHref('whatsapp', '+91 98765-43210')).toBe('https://wa.me/919876543210');
    expect(routeHref('whatsapp', '(998) 90 123 45 67')).toBe('https://wa.me/998901234567');
    expect(routeHref('whatsapp', 'https://wa.me/919876543210')).toBe('https://wa.me/919876543210');
    expect(routeHref('whatsapp', 'ask for Navit')).toBeNull();
    expect(routeHref('whatsapp', 'https://evil.example.test/wa')).toBeNull();
  });

  it('builds mailto and tel links only for plausible values', () => {
    expect(routeHref('email', 'sales@sethi.example.test')).toBe('mailto:sales@sethi.example.test');
    expect(routeHref('email', 'mailto:sales@sethi.example.test')).toBe('mailto:sales@sethi.example.test');
    expect(routeHref('email', 'javascript:alert(1)//@x.test')).toBeNull();
    expect(routeHref('email', 'not an address')).toBeNull();
    expect(routeHref('phone', '+998 (90) 123-45-67')).toBe('tel:+998901234567');
    expect(routeHref('phone', 'javascript:alert(1)')).toBeNull();
  });

  it('allows only http(s) for URL-like channels and rejects every other scheme', () => {
    expect(routeHref('website', 'https://sethi.example.test/silk')).toBe('https://sethi.example.test/silk');
    expect(routeHref('instagram', 'https://instagram.com/sethi')).toBe('https://instagram.com/sethi');
    expect(routeHref('instagram', '@sethi')).toBeNull();
    for (const unsafe of ['javascript:alert(1)', ' JavaScript:alert(1)', 'data:text/html,<script>alert(1)</script>', 'vbscript:msgbox', 'file:///etc/passwd', '//evil.example.test', 'https://exa mple.test']) {
      expect(routeHref('website', unsafe)).toBeNull();
      expect(routeHref('form', unsafe)).toBeNull();
      expect(routeHref('other', unsafe)).toBeNull();
      expect(safeHttpUrl(unsafe)).toBeNull();
    }
  });
});

describe('client-side upload limits', () => {
  it('rejects unsupported types and oversize files per type before sending', () => {
    const MiB = 1024 * 1024;
    expect(uploadRejection({ type: 'image/jpeg', size: 20 * MiB })).toBeNull();
    expect(uploadRejection({ type: 'image/png', size: 20 * MiB + 1 })).toBe('Images are limited to 20 MiB.');
    expect(uploadRejection({ type: 'application/pdf', size: 26 * MiB })).toBe('PDF files are limited to 25 MiB.');
    expect(uploadRejection({ type: 'video/mp4', size: 64 * MiB })).toBeNull();
    expect(uploadRejection({ type: 'video/mp4', size: 65 * MiB })).toBe('Videos are limited to 64 MiB.');
    expect(uploadRejection({ type: 'image/gif', size: 10 })).toBe('Only JPEG, PNG, WebP, PDF and MP4 files are accepted.');
    expect(uploadRejection({ type: 'image/webp', size: 0 })).toBe('This file is empty.');
  });
});
