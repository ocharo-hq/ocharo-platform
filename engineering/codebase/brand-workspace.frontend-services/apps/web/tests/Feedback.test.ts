import { afterEach, describe, expect, it, vi } from 'vitest';
import { confirmation, feedbackBus, notifyError, requestConfirmation, resolveConfirmation } from '@/application/shared/UseFeedback';
afterEach(()=>resolveConfirmation(false));
describe('app feedback',()=>{
 it('keeps destructive navigation pending, cancels safely and rejects overlapping intents',async()=>{
  const pending=requestConfirmation('Discard edits?');expect(confirmation.value?.message).toBe('Discard edits?');
  expect(await requestConfirmation('A second action?')).toBe(false);
  resolveConfirmation(false);expect(await pending).toBe(false);expect(confirmation.value).toBeNull();
  const accepted=requestConfirmation('Discard edits?');resolveConfirmation(true);expect(await accepted).toBe(true);
 });
 it('sends safe error text with a stable deduplication id through the SDK bus',()=>{
  const spy=vi.spyOn(feedbackBus,'notify');notifyError('Retry this request.','catalogue-load');
  expect(spy).toHaveBeenCalledWith(expect.objectContaining({id:'catalogue-load',tone:'danger',description:'Retry this request.',durationMs:10000}));spy.mockRestore();
 });
});
