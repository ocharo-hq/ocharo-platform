import { webcrypto } from 'node:crypto';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ref } from 'vue';
import { ResultExtensions } from '@wow-two-beta/fe/foundation/results';
import { readCreativeHandoff } from '@/domain/workspace/CreativeHandoff';
import { useCommerce } from '@/application/workspace/UseCommerce';
import type { AccessControls } from '@/application/workspace/UseAccess';
import type { WorkspaceControls } from '@/application/workspace/UseWorkspace';
const api = vi.hoisted(() => ({ creative: vi.fn() }));
vi.mock('@/integration/workspace/CommerceApi', () => ({ createCommerceApi: () => api }));
afterEach(() => vi.unstubAllGlobals());
const manifest = () => ({ schemaVersion:1,source:{application:'ocharo-studio',workspaceId:'workspace',lookId:'look',revision:0},assetReleases:[],materials:[],derivative:{sha256:'a'.repeat(64),mediaType:'image/png',fileName:'render.png',bytes:3},sourceApproval:{status:'approved-for-review',note:'Author submission only'} });
describe('Studio file intake', () => {
  it('preserves local revision zero and source submission without inventing approval', () => {
    const value = readCreativeHandoff(manifest());
    expect(value.source.revision).toBe(0);
    expect(value.sourceApproval?.status).toBe('approved-for-review');
    expect(value).not.toHaveProperty('approvedForPublication');
  });
  it.each([-1,0.5])('rejects invalid source revision %s', revision => {
    const value = manifest();value.source.revision=revision;
    expect(() => readCreativeHandoff(value)).toThrow();
  });
  it('rejects excessive derivative sizes and invalid asset hashes', () => {
    expect(() => readCreativeHandoff({...manifest(),derivative:{...manifest().derivative,bytes:6*1024*1024}})).toThrow();
    expect(() => readCreativeHandoff({...manifest(),assetReleases:[{id:'asset',sha256:'invalid'}]})).toThrow();
  });
  it.each([false,true])('verifies the derivative before uploading (matching=%s)', async matching => {
    vi.stubGlobal('crypto',webcrypto);
    const bytes = new Uint8Array([1,2,3]);
    const digest = Array.from(new Uint8Array(await webcrypto.subtle.digest('SHA-256',bytes)),byte=>byte.toString(16).padStart(2,'0')).join('');
    const value = manifest();value.derivative.sha256 = matching ? digest : 'a'.repeat(64);
    const manifestFile = {size:500,text:async()=>JSON.stringify(value)} as File;
    const imageFile = {size:3,type:'image/png',arrayBuffer:async()=>bytes.buffer} as File;
    const access = {session:ref({csrfToken:'csrf'}),recheck:vi.fn(),write:vi.fn((operation:(signal:AbortSignal)=>unknown)=>operation(new AbortController().signal))};
    const workspace = {uploadMedia:vi.fn().mockResolvedValue(ResultExtensions.ok({id:'media',sha256:digest,contentType:'image/png'}))};
    api.creative.mockResolvedValue(ResultExtensions.ok({id:'import',status:'unreviewed'}));
    const operation = useCommerce(access as unknown as AccessControls).importPackage(workspace as unknown as WorkspaceControls,'org','brand',null,manifestFile,imageFile,new AbortController().signal);
    if (!matching) { await expect(operation).rejects.toThrow('fingerprint'); expect(workspace.uploadMedia).not.toHaveBeenCalled(); }
    else {
      await expect(operation).resolves.toEqual({id:'import',status:'unreviewed'});
      expect(api.creative).toHaveBeenCalledWith('org',expect.objectContaining({source:expect.objectContaining({revision:0}),derivative:{mediaId:'media',sha256:digest,mediaType:'image/png'}}),expect.any(AbortSignal));
    }
  });
});
