import { mount } from '@vue/test-utils';
import { describe, expect, it } from 'vitest';
import CatalogueDeskEntry from '../src/presentation/common/CatalogueDeskEntry.vue';
import { resolveCatalogueDeskUrl } from '../src/domain/shared/OcharoApps';

describe('Canonical catalogue entry', () => {
  it('opens Desk in another tab without submitting or replacing pilot edits', () => {
    const wrapper = mount(CatalogueDeskEntry, { props: { deskUrl: resolveCatalogueDeskUrl(undefined) } });
    const link = wrapper.get('a');
    expect(link.attributes('href')).toBe('https://localhost:8264/catalogue');
    expect(link.attributes('target')).toBe('_blank');
    expect(link.attributes('rel')).toContain('noopener');
    expect(wrapper.find('form').exists()).toBe(false);
    expect(wrapper.text()).toContain('saqlanmagan');
  });

  it.each([
    'http://localhost:8264/catalogue',
    'javascript:alert(1)',
    '/catalogue',
    'https://user:password@desk.example/catalogue',
    'https://desk.example/catalogue?token=value',
  ])('rejects unsafe configured destination %s', (url) => {
    expect(resolveCatalogueDeskUrl(url)).toBeNull();
  });

  it('supports an explicit HTTPS Desk deployment', () => {
    expect(resolveCatalogueDeskUrl('https://desk.example/catalogue')).toBe('https://desk.example/catalogue');
  });
});
