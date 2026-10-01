import type { CreativeInput } from './Commerce';

export interface CreativeHandoff {
  source: CreativeInput['source'];
  assetReleases: CreativeInput['assetReleases'];
  materials: CreativeInput['materials'];
  derivative: { sha256: string; mediaType: 'image/png' | 'image/jpeg'; fileName: string; bytes: number };
  sourceApproval?: CreativeInput['sourceApproval'];
}
const record = (value: unknown): value is Record<string, unknown> => typeof value === 'object' && value !== null && !Array.isArray(value);
const hash = (value: unknown): value is string => typeof value === 'string' && /^[a-f0-9]{64}$/.test(value);
const text = (value: unknown): value is string => typeof value === 'string' && value.length > 0 && value.length <= 1000;

/** Validates a Studio export without treating its author submission as publication approval. */
export function readCreativeHandoff(value: unknown): CreativeHandoff {
  if (!record(value) || value.schemaVersion !== 1 || !record(value.source)
    || value.source.application !== 'ocharo-studio' || !text(value.source.workspaceId) || !text(value.source.lookId)
    || !Number.isSafeInteger(value.source.revision) || Number(value.source.revision) < 0
    || !Array.isArray(value.assetReleases) || value.assetReleases.length > 100
    || !value.assetReleases.every(item => record(item) && text(item.id) && hash(item.sha256))
    || !Array.isArray(value.materials) || value.materials.length > 100
    || !value.materials.every(item => record(item) && text(item.slot) && text(item.source) && (item.sha256 === undefined || item.sha256 === null || hash(item.sha256)))
    || !record(value.derivative) || !hash(value.derivative.sha256) || !text(value.derivative.fileName)
    || !['image/png','image/jpeg'].includes(String(value.derivative.mediaType))
    || !Number.isSafeInteger(value.derivative.bytes) || Number(value.derivative.bytes) < 1 || Number(value.derivative.bytes) > 5 * 1024 * 1024
    || (value.sourceApproval !== undefined && (!record(value.sourceApproval) || !text(value.sourceApproval.status)
      || (value.sourceApproval.note !== undefined && value.sourceApproval.note !== null && !text(value.sourceApproval.note))))) {
    throw new Error('Choose a valid Studio export manifest and its PNG or JPEG image (up to 5 MiB).');
  }
  return value as unknown as CreativeHandoff;
}
