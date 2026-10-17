import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { ref } from 'vue';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ResultExtensions } from '@wow-two-beta/fe/foundation/results';
import { ApiFailureFactory } from '@wow-two-beta/fe/foundation/http';
import type { AccessControls } from '@/application/workspace/UseAccess';
import { signInDestination } from '@/domain/workspace/SignInDestination';
import { telegramLinkDecoder } from '@/integration/workspace/TelegramDecoders';
import TelegramSignIn from '@/presentation/workspace/TelegramSignIn.vue';
import AccountSettings from '@/presentation/workspace/AccountSettings.vue';
import { feedbackBus, confirmation, resolveConfirmation } from '@/application/shared/UseFeedback';
import LoginPage from '@/presentation/workspace/LoginPage.vue';

const mounted: VueWrapper[] = [];
afterEach(() => { resolveConfirmation(false); mounted.splice(0).forEach(wrapper => wrapper.unmount()); vi.restoreAllMocks(); });
const fail = () => ResultExtensions.fail(ApiFailureFactory.create('http', new Response(null, { status: 401 })));
function controls() {
  const api = {
    getTelegram: vi.fn().mockResolvedValue(ResultExtensions.ok({ enabled: true, linked: false, phone: null, botUsername: 'ocharo_bot' })),
    linkTelegram: vi.fn().mockResolvedValue(ResultExtensions.ok({ botUrl: 'https://t.me/ocharo_bot?start=link_test', expiresAt: '2030-01-01T00:00:00Z' })),
    unlinkTelegram: vi.fn().mockResolvedValue(fail()),
    requestTelegramCode: vi.fn().mockResolvedValue(ResultExtensions.ok({ expiresAt: '2030-01-01T00:00:00Z' })),
    loginTelegram: vi.fn().mockResolvedValue(fail()),
  };
  const authenticated = vi.fn();
  const access = { api, busy: ref(false), dirty: ref(false), session: ref(null), login: vi.fn().mockResolvedValue(fail()), authenticated, write: (operation: (signal: AbortSignal) => unknown) => operation(new AbortController().signal) } as unknown as AccessControls;
  return { api, authenticated, access };
}
describe('Telegram access', () => {
  it('accepts only the central local sign-on endpoint as a return destination', () => {
    const path = '/api/sso/authorize?clientId=catalogue&state=example';
    expect(signInDestination('?returnUrl=' + encodeURIComponent(path), '/')).toBe(path);
    for (const url of ['https://evil.test/', '//evil.test/', '/api/sso/authorize/../logout', '/api/auth/logout', '/api/sso/authorize?x=\\evil']) {
      expect(signInDestination('?returnUrl=' + encodeURIComponent(url), '/')).toBe('/');
    }
  });
  it('rejects untrusted bot-link destinations at the API boundary', () => {
    for (const url of ['https://evil.test/bot', 'javascript:alert(1)', 'https://t.me@evil.test/bot', 'https://t.me/bot/path']) {
      expect(telegramLinkDecoder({ botUrl: url, expiresAt: '2030-01-01T00:00:00Z' }).ok).toBe(false);
    }
    expect(telegramLinkDecoder({ botUrl: 'https://t.me/ocharo_bot?start=link_test', expiresAt: '2030-01-01T00:00:00Z' }).ok).toBe(true);
  });
  it('waits for delivery acceptance, retains phone on failure, and never authenticates a rejected code', async () => {
    vi.spyOn(feedbackBus, 'notify');
    const { api, access, authenticated } = controls();
    let complete: ((value: unknown) => void) | undefined;
    api.requestTelegramCode.mockImplementationOnce(() => new Promise(resolve => { complete = resolve; }));
    const wrapper = mount(TelegramSignIn, { props: { access } }); mounted.push(wrapper);
    await wrapper.get('input[type="tel"]').setValue('+998901234567');
    await wrapper.get('form').trigger('submit');
    expect(wrapper.find('input[autocomplete="one-time-code"]').exists()).toBe(false);
    complete!(fail()); await flushPromises();
    expect(wrapper.get('input[type="tel"]').element).toHaveProperty('value', '+998901234567');
    expect(wrapper.findAll('form')).toHaveLength(1);
    await wrapper.get('form').trigger('submit'); await flushPromises();
    await wrapper.get('input[autocomplete="one-time-code"]').setValue('123456');
    await wrapper.findAll('form')[1]!.trigger('submit'); await flushPromises();
    expect(api.loginTelegram).toHaveBeenCalledWith('+998901234567', '123456', expect.any(AbortSignal));
    expect(authenticated).not.toHaveBeenCalled();
    expect(wrapper.get('input[autocomplete="one-time-code"]').element).toHaveProperty('value', '');
    await wrapper.get('input[type="tel"]').setValue('+998901234568');
    expect(wrapper.find('input[autocomplete="one-time-code"]').exists()).toBe(false);
  });
  it('displays a successful linking invitation without claiming the bot is connected', async () => {
    const { api, access } = controls();
    const wrapper = mount(AccountSettings, { props: { access }, global: { stubs: { Teleport: true } } }); mounted.push(wrapper);
    await flushPromises();
    await wrapper.get('input[type="password"]').setValue('example-password');
    await wrapper.get('form').trigger('submit'); await flushPromises();
    expect(api.linkTelegram).toHaveBeenCalledWith('example-password', expect.any(AbortSignal));
    expect(wrapper.get('a').attributes('href')).toBe('https://t.me/ocharo_bot?start=link_test');
    expect(wrapper.text()).not.toContain('Telegram is connected to your account.');
    expect(wrapper.get('input[type="password"]').element).toHaveProperty('value', '');
  });
});


function account(access: AccessControls) {
  const wrapper = mount(AccountSettings, { props: { access }, global: { stubs: { Teleport: true } } });
  mounted.push(wrapper);
  return wrapper;
}
function loginPage(access: AccessControls) {
  const wrapper = mount(LoginPage, { props: { access }, global: { stubs: { BuildIdentity: true } } });
  mounted.push(wrapper);
  return wrapper;
}
const linked = { enabled: true, linked: true, phone: '+998901234567', botUsername: 'ocharo_bot' };

describe('Telegram account lifecycle', () => {
  it('preserves dirty edits and sends no unlink request when confirmation is declined', async () => {
    const { api, access, authenticated } = controls();
    api.getTelegram.mockResolvedValueOnce(ResultExtensions.ok(linked));
    access.dirty.value = true;
    const wrapper = account(access); await flushPromises();
    await wrapper.get('input[type="password"]').setValue('example-password');
    await wrapper.get('form').trigger('submit');
    expect(confirmation.value?.message).toContain('Discard unsaved changes');
    expect(api.unlinkTelegram).not.toHaveBeenCalled();
    resolveConfirmation(false); await flushPromises();
    expect(api.unlinkTelegram).not.toHaveBeenCalled();
    expect(authenticated).not.toHaveBeenCalled();
    expect(access.dirty.value).toBe(true);
    expect(wrapper.get('input[type="password"]').element).toHaveProperty('value', 'example-password');
  });
  it('waits for unlink success after confirmation before changing sessions', async () => {
    const { api, access, authenticated } = controls();
    api.getTelegram.mockResolvedValueOnce(ResultExtensions.ok(linked));
    access.dirty.value = true;
    let complete!: (value: unknown) => void;
    api.unlinkTelegram.mockImplementationOnce(() => new Promise(resolve => { complete = resolve; }));
    const wrapper = account(access); await flushPromises();
    await wrapper.get('input[type="password"]').setValue('example-password');
    await wrapper.get('form').trigger('submit');
    resolveConfirmation(true); await flushPromises();
    expect(api.unlinkTelegram).toHaveBeenCalledOnce();
    expect(authenticated).not.toHaveBeenCalled();
    expect(access.dirty.value).toBe(true);
    complete(ResultExtensions.ok(undefined)); await flushPromises();
    expect(authenticated).toHaveBeenCalledOnce();
  });
  it('cancels an outstanding unlink confirmation when its dialog unmounts', async () => {
    const { api, access } = controls();
    api.getTelegram.mockResolvedValueOnce(ResultExtensions.ok(linked));
    access.dirty.value = true;
    const wrapper = account(access); await flushPromises();
    await wrapper.get('input[type="password"]').setValue('example-password');
    await wrapper.get('form').trigger('submit');
    wrapper.unmount(); await flushPromises();
    expect(confirmation.value).toBeNull();
    expect(api.unlinkTelegram).not.toHaveBeenCalled();
    expect(access.dirty.value).toBe(true);
  });
  it.each([false, true])('keeps current-password failure distinct from session expiry (linked=%s)', async isLinked => {
    const notices = vi.spyOn(feedbackBus, 'notify');
    const { api, access, authenticated } = controls();
    api.getTelegram.mockResolvedValueOnce(ResultExtensions.ok({ ...linked, linked: isLinked }));
    const invalidPassword = ResultExtensions.fail({ ...ApiFailureFactory.create('http', new Response(null, { status: 401 })), problem: { code: 'invalid_credentials' } });
    api.linkTelegram.mockResolvedValueOnce(invalidPassword);
    api.unlinkTelegram.mockResolvedValueOnce(invalidPassword);
    const wrapper = account(access); await flushPromises();
    await wrapper.get('input[type="password"]').setValue('incorrect-password');
    await wrapper.get('form').trigger('submit'); await flushPromises();
    expect(notices).toHaveBeenCalledWith(expect.objectContaining({ description: 'Your current password was not accepted. Check it and try again.' }));
    expect(authenticated).not.toHaveBeenCalled();
    expect(wrapper.find('input[type="password"]').exists()).toBe(true);
    expect(wrapper.get('input[type="password"]').element).toHaveProperty('value', '');
  });
  it('preserves password entry when Telegram availability arrives late', async () => {
    const { api, access } = controls();
    let complete!: (value: unknown) => void;
    api.getTelegram.mockImplementationOnce(() => new Promise(resolve => { complete = resolve; }));
    const wrapper = loginPage(access);
    await wrapper.get('input[type="email"]').setValue('operator@example.test');
    complete(ResultExtensions.ok(linked)); await flushPromises();
    expect(wrapper.findComponent(TelegramSignIn).exists()).toBe(false);
    expect(wrapper.get('input[type="email"]').element).toHaveProperty('value', 'operator@example.test');
    expect(wrapper.text()).toContain('Telegram');
  });
  it('keeps an in-flight password sign-in mounted when Telegram availability arrives', async () => {
    const { api, access, authenticated } = controls();
    let availability!: (value: unknown) => void;
    let finishLogin!: (value: Awaited<ReturnType<AccessControls['login']>>) => void;
    api.getTelegram.mockImplementationOnce(() => new Promise(resolve => { availability = resolve; }));
    vi.mocked(access.login).mockImplementationOnce(() => new Promise(resolve => { finishLogin = resolve; }));
    const wrapper = loginPage(access);
    await wrapper.get('input[type="email"]').setValue('operator@example.test');
    await wrapper.get('input[type="password"]').setValue('example-password');
    await wrapper.get('form').trigger('submit'); await flushPromises();
    expect(access.login).toHaveBeenCalledOnce();
    availability(ResultExtensions.ok(linked)); await flushPromises();
    expect(wrapper.findComponent(TelegramSignIn).exists()).toBe(false);
    expect(wrapper.find('input[type="password"]').exists()).toBe(true);
    finishLogin(fail()); await flushPromises();
    expect(authenticated).not.toHaveBeenCalled();
  });
});
