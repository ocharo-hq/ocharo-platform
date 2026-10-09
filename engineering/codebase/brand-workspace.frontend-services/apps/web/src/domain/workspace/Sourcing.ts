/** Sourcing slice 1: suppliers, the routes that reach them, their people and the media they shared. */
export const supplierStages = ['lead', 'contacted', 'replied', 'quoted', 'qualified', 'dropped'] as const;
export const contactChannels = ['whatsapp', 'email', 'phone', 'form', 'website', 'instagram', 'facebook', 'telegram', 'marketplace', 'other'] as const;
export const mediaCaptures = ['original', 'screenshot', 'derived'] as const;
export type SupplierStage = typeof supplierStages[number];
export type ContactChannel = typeof contactChannels[number];
export type MediaCapture = typeof mediaCaptures[number];

export interface SupplierSummary {
  id: string; key: string | null; name: string; stage: SupplierStage;
  country: string | null; city: string | null;
  primaryRoute: { channel: ContactChannel; destination: string } | null;
  routeCount: number; personCount: number; mediaCount: number;
  coverMediaId: string | null;
  revision: number; updatedAt: string;
}
export interface SupplierPerson {
  id: string; supplierId: string; name: string; role: string | null; notes: string;
  revision: number; createdAt: string; updatedAt: string;
}
export interface ContactRoute {
  id: string; supplierId: string; personId: string | null; channel: ContactChannel; destination: string;
  label: string | null; sourceUrl: string | null; verified: boolean; isPrimary: boolean; notes: string;
  revision: number; createdAt: string; updatedAt: string;
}
export interface Supplier {
  id: string; key: string | null; name: string; stage: SupplierStage;
  country: string | null; city: string | null; website: string | null; notes: string;
  people: SupplierPerson[]; routes: ContactRoute[];
  revision: number; createdAt: string; updatedAt: string;
}
export interface SourcingMedia {
  id: string; supplierId: string | null; sha256: string; contentType: string; byteSize: number;
  width: number | null; height: number | null; hasThumbnail: boolean;
  capture: MediaCapture; originalName: string; caption: string;
  sourceLabel: string | null; sourceMessageId: string | null;
  receivedAt: string | null; takenAt: string | null; hasLocation: boolean;
  revision: number; createdAt: string; updatedAt: string;
}
export interface SourcingMediaSummary { total: number; unassigned: number }
export interface SourcingUploadResult { media: SourcingMedia; duplicate: boolean; nearDuplicateIds: string[] }

export interface SupplierInput { name: string; stage: SupplierStage; country: string | null; city: string | null; website: string | null; notes: string }
export interface PersonInput { name: string; role: string | null; notes: string }
export interface RouteInput {
  channel: ContactChannel; destination: string; label: string | null; sourceUrl: string | null;
  verified: boolean; isPrimary: boolean; personId: string | null; notes: string;
}
export interface MediaUpdateInput { supplierId: string | null; capture: MediaCapture; caption: string }
export interface SupplierFilter { query: string; stage: SupplierStage | '' }
/** A media listing is either one supplier's gallery or the unassigned inbox. */
export type MediaFilter = { kind: 'supplier'; supplierId: string } | { kind: 'inbox' } | { kind: 'all' };

/** Contract limits mirrored for early feedback; the server remains authoritative. */
export const sourcingLimits = {
  name: 160, role: 120, label: 120, city: 120, destination: 512, url: 2048,
  supplierNotes: 8000, notes: 4000, caption: 2000,
  supplierPage: 200, mediaPage: 100, assignBatch: 200, concurrentUploads: 3,
} as const;
const mebibyte = 1024 * 1024;
export const sourcingMediaTypes: Readonly<Record<string, number>> = {
  'image/jpeg': 20 * mebibyte, 'image/png': 20 * mebibyte, 'image/webp': 20 * mebibyte,
  'application/pdf': 25 * mebibyte, 'video/mp4': 64 * mebibyte,
};
export const sourcingMediaAccept = Object.keys(sourcingMediaTypes).join(',');
export const sourcingMaximumBytes = 64 * mebibyte;

export const stageLabels: Record<SupplierStage, string> = {
  lead: 'Lead', contacted: 'Contacted', replied: 'Replied', quoted: 'Quoted', qualified: 'Qualified', dropped: 'Dropped',
};
export const channelLabels: Record<ContactChannel, string> = {
  whatsapp: 'WhatsApp', email: 'Email', phone: 'Phone', form: 'Contact form', website: 'Website', instagram: 'Instagram',
  facebook: 'Facebook', telegram: 'Telegram', marketplace: 'Marketplace', other: 'Other',
};
export const captureLabels: Record<MediaCapture, string> = { original: 'Original file', screenshot: 'Screenshot', derived: 'Derived copy' };

export const isSupplierStage = (value: unknown): value is SupplierStage => supplierStages.includes(value as SupplierStage);
export const isContactChannel = (value: unknown): value is ContactChannel => contactChannels.includes(value as ContactChannel);
export const isMediaCapture = (value: unknown): value is MediaCapture => mediaCaptures.includes(value as MediaCapture);

/** Returns a normalized http(s) URL, or null for every other scheme (javascript:, data:, file: …). */
export function safeHttpUrl(value: string | null | undefined): string | null {
  const text = value?.trim();
  if (!text || /[\u0000-\u0020\u007f]/.test(text) || !/^https?:\/\//i.test(text)) return null;
  try {
    const url = new URL(text);
    return (url.protocol === 'https:' || url.protocol === 'http:') && url.hostname ? url.href : null;
  } catch { return null; }
}

const emailPattern = /^[^\s@:/?#<>"'\\]+@[^\s@:/?#<>"'\\]+\.[^\s@:/?#<>"'\\]+$/;
/** Builds a link only for a recognized, inert destination; anything else renders as plain text. */
export function routeHref(channel: ContactChannel, destination: string): string | null {
  const text = destination.trim();
  if (!text) return null;
  if (channel === 'whatsapp') {
    const url = safeHttpUrl(text);
    if (url && /^(wa\.me|api\.whatsapp\.com|chat\.whatsapp\.com)$/i.test(new URL(url).hostname)) return url;
    const digits = text.replace(/\D/g, '');
    return /^[+\d\s().-]+$/.test(text) && digits.length >= 6 && digits.length <= 15 ? `https://wa.me/${digits}` : null;
  }
  if (channel === 'email') {
    const address = text.replace(/^mailto:/i, '');
    return emailPattern.test(address) ? `mailto:${address}` : null;
  }
  if (channel === 'phone') {
    if (!/^\+?[\d\s().-]+$/.test(text)) return null;
    const digits = text.replace(/\D/g, '');
    return digits.length >= 3 && digits.length <= 15 ? `tel:${text.startsWith('+') ? '+' : ''}${digits}` : null;
  }
  return safeHttpUrl(text);
}

export type MediaKind = 'image' | 'pdf' | 'video' | 'other';
export function mediaKind(contentType: string): MediaKind {
  if (contentType.startsWith('image/')) return 'image';
  if (contentType === 'application/pdf') return 'pdf';
  if (contentType.startsWith('video/')) return 'video';
  return 'other';
}

/** Rejects files the server would refuse, before any bytes leave the browser. */
export function uploadRejection(file: Pick<File, 'type' | 'size'>): string | null {
  const limit = sourcingMediaTypes[file.type];
  if (limit === undefined) return 'Only JPEG, PNG, WebP, PDF and MP4 files are accepted.';
  if (file.size <= 0) return 'This file is empty.';
  if (file.size > limit) {
    return file.type === 'application/pdf' ? 'PDF files are limited to 25 MiB.'
      : file.type === 'video/mp4' ? 'Videos are limited to 64 MiB.' : 'Images are limited to 20 MiB.';
  }
  return null;
}

export function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < mebibyte) return `${(bytes / 1024).toFixed(0)} KiB`;
  return `${(bytes / mebibyte).toFixed(1)} MiB`;
}
export function supplierInitial(name: string): string {
  return Array.from(name.trim())[0]?.toLocaleUpperCase() ?? '?';
}
/** Blank optional strings normalize to null, as the server does. */
export function optionalText(value: string): string | null {
  const text = value.trim();
  return text ? text : null;
}

const identifier = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
export interface SourcingLocation { view: 'suppliers' | 'materials' | 'inbox' | 'supplier'; supplierId: string | null }
/** Reads the segments after `/organizations/{id}/sourcing`; unknown segments fall back to the supplier list. */
export function readSourcingLocation(segments: readonly string[]): SourcingLocation {
  if (segments.length === 1 && segments[0] === 'materials') return { view: 'materials', supplierId: null };
  if (segments.length === 1 && segments[0] === 'inbox') return { view: 'inbox', supplierId: null };
  if (segments.length === 1 && identifier.test(segments[0]!)) return { view: 'supplier', supplierId: segments[0]!.toLowerCase() };
  return { view: 'suppliers', supplierId: null };
}
export function sourcingPath(organizationId: string, location: SourcingLocation): string {
  if (!identifier.test(organizationId)) throw new Error('Invalid workspace destination.');
  const base = `/organizations/${organizationId}/sourcing`;
  if (location.view === 'materials') return `${base}/materials`;
  if (location.view === 'inbox') return `${base}/inbox`;
  if (location.view === 'supplier' && location.supplierId && identifier.test(location.supplierId)) return `${base}/${location.supplierId}`;
  return base;
}
export function isSourcingIdentifier(value: string): boolean {
  return identifier.test(value);
}
