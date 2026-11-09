/**
 * Brand wire and view models, mirroring the OcharoBrand API documents. The API omits a null member, so every member that
 * can be empty is optional; a write may send it back absent.
 */
export interface LocalizedText {
  uz?: string;
  ru?: string;
  en?: string;
}

export interface BrandColor {
  key: string;
  name: string;
  hex: string;
  role: string;
}

export interface BrandTypography {
  display: string;
  body: string;
  notes?: string;
}

export interface BrandLink {
  key: string;
  label: string;
  handle?: string;
  url: string;
}

export interface BrandIdentity {
  name: string;
  spellingRule?: string;
  descriptor: LocalizedText;
  tagline: LocalizedText;
  location: LocalizedText;
  palette: BrandColor[];
  typography: BrandTypography;
  links: BrandLink[];
  voiceNotes?: string;
}

export interface LogoVersion {
  id: string;
  slot: string;
  sha256: string;
  fileName: string;
  contentType: string;
  byteLength: number;
  width?: number;
  height?: number;
  hasPreview: boolean;
  status: 'draft' | 'approved';
  note?: string;
  sourcePath?: string;
  uploadedAt: string;
}

export interface DraftView {
  draft: { identity: BrandIdentity; currentLogos: Record<string, string> };
  revision: number;
  updatedAt: string;
  logos: LogoVersion[];
  /** Absent before the first release. */
  latestRelease?: number;
  unpublishedChanges: string[];
}

export interface ReleaseSummary {
  version: number;
  publishedAt: string;
  note: string;
  changes: string[];
}

export interface BrandAppearance {
  id: string;
  name: string;
  startsAt: string;
  endsAt: string;
  status: 'scheduled' | 'cancelled';
  overrides: Record<string, string>;
  accentHex?: string;
  note?: string;
  linkLabel?: string;
  linkUrl?: string;
}

export interface ConsumerView {
  app: string;
  lastSeenAt: string;
  lastRelease: number;
  current: boolean;
  lastPath: string;
}

export interface PublicLogo {
  url: string;
  fileUrl: string;
  previewUrl?: string;
  contentType: string;
  width?: number;
  height?: number;
  sha256: string;
  fromAppearance: boolean;
  /** The WoW2 Files file holding the logo; absent while it is only in Brand's local blob folder. */
  fileId?: string;
}

export interface PublicBrand {
  name: string;
  release: number;
  publishedAt: string;
  logos: Record<string, PublicLogo>;
  palette: BrandColor[];
  links: BrandLink[];
  /** Absent while no appearance runs. */
  appearance?: {
    id: string;
    name: string;
    startsAt: string;
    endsAt: string;
    accentHex?: string;
    linkLabel?: string;
    linkUrl?: string;
  };
}

export const slotLabels: Record<string, { label: string; hint: string }> = {
  master: { label: 'Asosiy (master)', hint: 'Tasdiqlangan asl logotip' },
  'on-light': { label: 'Och fonda', hint: 'Qora yozuv, shaffof fon' },
  'on-dark': { label: 'To‘q fonda', hint: 'Oq yozuv, shaffof fon' },
  avatar: { label: 'Profil rasmi', hint: 'Kvadrat, ijtimoiy tarmoqlar uchun' },
  favicon: { label: 'Favicon', hint: 'Kichik kvadrat belgi' },
  social: { label: 'Ulashish rasmi', hint: '1200×630, havola ulashilganda' },
};

export const slots = ['master', 'on-light', 'on-dark', 'avatar', 'favicon', 'social'];
