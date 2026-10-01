import { validateSnapshot, type DesignKind, type DesignSnapshot } from './EditorModels';

export interface DesignRecoveryScope { userId: string; organizationId: string; kind: DesignKind }
export interface DesignRecoverySource { documentId: string; revision: number }
export interface DesignRecoveryInput { title: string; snapshot: DesignSnapshot; source: DesignRecoverySource | null }
export interface DesignRecoveryRecord extends DesignRecoveryInput {
  schemaVersion: 1; scope: DesignRecoveryScope; token: string; savedAt: string;
}
const uuid = /^[a-f\d]{8}-[a-f\d]{4}-[a-f\d]{4}-[a-f\d]{4}-[a-f\d]{12}$/i;
export function isRecoveryScope(scope: DesignRecoveryScope): boolean {
  return uuid.test(scope.userId) && uuid.test(scope.organizationId) && ['catalogue', 'business-card'].includes(scope.kind);
}
/** Keep only the design schema: imported extension fields must not become local private state. */
export function recoverySnapshot(value: unknown): DesignSnapshot {
  const known = new Set(['schemaVersion','kind','templateId','locale','pages','id','title','width','height','background','elements','productIds','catalogueSource','brandId','revision','productRevision','productId','layout','currency','imageHashes','type','label','x','y','text','src','color','fontFamily','fontSize','fontWeight','align','letterSpacing','lineHeight','fit','cropX','cropY','zoom','opacity','rotation','locked','groupId','tier','priceLabel','borderColor','borderWidth','radius','role']);
  const checked = validateSnapshot(value);
  return JSON.parse(JSON.stringify(checked, function(this: unknown, key: string, item: unknown) { return key === '' || Array.isArray(this) || known.has(key) ? item : undefined; })) as DesignSnapshot;
}
export function validateRecovery(value: unknown, scope: DesignRecoveryScope): DesignRecoveryRecord {
  const input = value as DesignRecoveryRecord;
  if (!input || input.schemaVersion !== 1 || !isRecoveryScope(scope) ||
      input.scope?.userId !== scope.userId || input.scope.organizationId !== scope.organizationId || input.scope.kind !== scope.kind ||
      typeof input.token !== 'string' || !uuid.test(input.token) || typeof input.savedAt !== 'string' || input.savedAt.length > 40 || !Number.isFinite(Date.parse(input.savedAt)) ||
      typeof input.title !== 'string' || input.title.length > 160 || /[\u0000-\u001f\u007f]/.test(input.title) ||
      (input.source !== null && (!input.source || !uuid.test(input.source.documentId) || !Number.isSafeInteger(input.source.revision) || input.source.revision < 1)))
    throw new Error('This local recovery record is invalid. Discard it or keep working from a saved design.');
  const snapshot = recoverySnapshot(input.snapshot);
  if (snapshot.kind !== scope.kind) throw new Error('This recovery belongs in another design editor.');
  const result: DesignRecoveryRecord = { schemaVersion: 1, scope: { ...scope }, token: input.token, savedAt: input.savedAt,
    title: input.title, snapshot, source: input.source ? { documentId: input.source.documentId, revision: input.source.revision } : null };
  if (new TextEncoder().encode(JSON.stringify(result)).length > 1_048_576)
    throw new Error('This design exceeds the local recovery limit. Download its editable file to keep a backup.');
  return result;
}
