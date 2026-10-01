import { createCommerceApi } from '@/integration/workspace/CommerceApi';
import { readCreativeHandoff } from '@/domain/workspace/CreativeHandoff';
import { failureMessage, type AccessControls } from './UseAccess';
import type { WorkspaceControls } from './UseWorkspace';

/** Owns authenticated Commerce transport and explicit Studio file intake. */
export function useCommerce(access: AccessControls) {
  const api = createCommerceApi(() => access.session.value?.csrfToken ?? '', () => access.recheck());
  async function importPackage(workspace: WorkspaceControls, organization: string, brandId: string, productId: string | null,
    manifestFile: File, imageFile: File, signal: AbortSignal) {
    if (manifestFile.size > 1024 * 1024) throw new Error('The Studio manifest must be smaller than 1 MiB.');
    let manifest;
    try { manifest = readCreativeHandoff(JSON.parse(await manifestFile.text())); }
    catch { throw new Error('Choose a valid Studio JSON manifest.'); }
    if (imageFile.size !== manifest.derivative.bytes || imageFile.type !== manifest.derivative.mediaType) {
      throw new Error('The image does not match the manifest size and type. Choose its original exported image.');
    }
    const digest = Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256', await imageFile.arrayBuffer())), byte => byte.toString(16).padStart(2, '0')).join('');
    if (digest !== manifest.derivative.sha256) throw new Error('The image fingerprint does not match the Studio manifest.');
    const uploaded = await workspace.uploadMedia(imageFile, `Studio visualization; look ${manifest.source.lookId}; source revision ${manifest.source.revision}; unreviewed for publication.`, signal);
    if (!uploaded.ok) throw new Error(failureMessage(uploaded.failure));
    const result = await access.write(s => api.creative(organization, {
      brandId, productId, source: manifest.source, assetReleases: manifest.assetReleases, materials: manifest.materials,
      derivative: { mediaId: uploaded.value.id, sha256: uploaded.value.sha256, mediaType: uploaded.value.contentType },
      ...(manifest.sourceApproval ? { sourceApproval: manifest.sourceApproval } : {}),
    }, s), signal);
    if (!result.ok) throw new Error('The image is in Media, but its Studio record was not saved.', { cause: failureMessage(result.failure) });
    return result.value;
  }
  return { ...api, importPackage };
}
