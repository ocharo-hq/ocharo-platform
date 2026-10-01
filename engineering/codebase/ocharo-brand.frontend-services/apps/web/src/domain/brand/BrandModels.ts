/** Brand wire and view models, mirroring the OcharoBrand API documents. */
export interface LocalizedText {
  uz: string | null;
  ru: string | null;
  en: string | null;
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
  notes: string | null;
}

export interface BrandLink {
  key: string;
  label: string;
  handle: string | null;
  url: string;
}

export interface BrandIdentity {
  name: string;
  spellingRule: string | null;
  descriptor: LocalizedText;
  tagline: LocalizedText;
  location: LocalizedText;
  palette: BrandColor[];
  typography: BrandTypography;
  links: BrandLink[];
  voiceNotes: string | null;
}

export interface LogoVersion {
  id: string;
  slot: string;
  sha256: string;
  fileName: string;
  contentType: string;
  byteLength: number;
  width: number | null;
  height: number | null;
  hasPreview: boolean;
  status: 'draft' | 'approved';
  note: string | null;
  sourcePath: string | null;
  uploadedAt: string;
}

export interface DraftView {
  draft: { identity: BrandIdentity; currentLogos: Record<string, string> };
  revision: number;
  updatedAt: string;
  logos: LogoVersion[];
  latestRelease: number | null;
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
  accentHex: string | null;
  note: string | null;
  linkLabel: string | null;
  linkUrl: string | null;
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
  previewUrl: string | null;
  contentType: string;
  width: number | null;
  height: number | null;
  sha256: string;
  fromAppearance: boolean;
}

export interface PublicBrand {
  name: string;
  release: number;
  publishedAt: string;
  logos: Record<string, PublicLogo>;
  palette: BrandColor[];
  links: BrandLink[];
  appearance: {
    id: string;
    name: string;
    startsAt: string;
    endsAt: string;
    accentHex: string | null;
    linkLabel: string | null;
    linkUrl: string | null;
  } | null;
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
