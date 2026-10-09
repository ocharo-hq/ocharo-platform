import type { TelegramAvailability, TelegramLink, TelegramCode } from '@/domain/workspace/TelegramAccess';
import { ApiFailureFactory, type ApiDecoder } from '@wow-two-beta/ui-vue/foundation/http';
import { ResultExtensions } from '@wow-two-beta/ui-vue/foundation/results';
function record(value: unknown): value is Record<string, unknown> { return typeof value === 'object' && value !== null && !Array.isArray(value); }
function nullableText(value: unknown): value is string | null { return value === null || typeof value === 'string'; }
function decoder<T>(guard: (value: unknown) => value is T): ApiDecoder<T> {
  return value => guard(value) ? ResultExtensions.ok(value) : ResultExtensions.fail(ApiFailureFactory.create('protocol'));
}
export const telegramAvailabilityDecoder = decoder((value): value is TelegramAvailability =>
  record(value) && typeof value.enabled === 'boolean' && typeof value.linked === 'boolean' && nullableText(value.botUsername) && nullableText(value.phone));
export const telegramCodeDecoder = decoder((value): value is TelegramCode =>
  record(value) && typeof value.expiresAt === 'string' && Number.isFinite(Date.parse(value.expiresAt)));
export const telegramLinkDecoder = decoder((value): value is TelegramLink => {
  if (!record(value) || typeof value.botUrl !== 'string' || typeof value.expiresAt !== 'string' || !Number.isFinite(Date.parse(value.expiresAt))) return false;
  try {
    const url = new URL(value.botUrl);
    return url.origin === 'https://t.me' && /^\/[A-Za-z0-9_]+$/.test(url.pathname) && !url.username && !url.password;
  } catch { return false; }
});
