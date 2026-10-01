import { describe, expect, it } from 'vitest';
import { selectDesignPages } from '@/domain/workspace/DesignPageSelection';
import { createCatalogueTemplate } from '@/domain/workspace/Templates';

describe('original catalogue page export selection', () => {
  it('preserves original16page geometry and artwork when exporting the whole edition', () => {
    const source=createCatalogueTemplate('uz'), selected=selectDesignPages(source);
    expect(selected).toEqual(source);expect(selected).not.toBe(source);expect(selected.pages).toHaveLength(16);
    expect(selected.pages.every(page=>page.width===source.pages[0]!.width&&page.height===source.pages[0]!.height)).toBe(true);
  });
  it('keeps edition order and original printed folios for a selected subset',()=>{
    const source=createCatalogueTemplate('en'), selected=selectDesignPages(source,[source.pages[7]!.id,source.pages[1]!.id]);
    expect(selected.pages.map(page=>page.id)).toEqual([source.pages[1]!.id,source.pages[7]!.id]);
    expect(selected.pages[1]).toEqual(source.pages[7]);expect(source.pages).toHaveLength(16);
  });
  it.each([[[]],[['missing']],[['catalogue-01','catalogue-01']]])('rejects empty, stale or duplicate selection: %j',ids=>{
    expect(()=>selectDesignPages(createCatalogueTemplate('en'),ids)).toThrow();
  });
});
