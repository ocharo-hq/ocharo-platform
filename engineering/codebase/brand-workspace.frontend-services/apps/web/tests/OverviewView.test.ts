import { describe, expect, it } from 'vitest';

import { memoryStorageBroker, type StorageBroker } from '@wow-two-beta/fe/foundation/storage';

import { useOverviewView } from '@/application/common/hooks/UseOverviewView';

const views = ['map', 'attention', 'compact'] as const;

describe('Overview view preference', () => {
  it('remembers the chosen view through the storage broker and restores it', () => {
    const broker = memoryStorageBroker();
    const first = useOverviewView(views, 'map', broker);
    expect(first.view.value).toBe('map');
    expect(first.select('compact')).toBe(true);
    expect(useOverviewView(views, 'map', broker).view.value).toBe('compact');
  });

  it('ignores an unknown stored view and still changes the view when storage refuses the write', () => {
    const refusing: StorageBroker = { read: () => 'gallery' as never, write: () => false, remove: () => false };
    const preference = useOverviewView(views, 'map', refusing);
    expect(preference.view.value).toBe('map');
    expect(preference.select('attention')).toBe(false);
    expect(preference.view.value).toBe('attention');
  });
});
