import { ESLint } from 'eslint';
import { describe, expect, it } from 'vitest';

const eslint = new ESLint();

describe('application boundaries', () => {
  it('rejects a presentation import from application orchestration', async () => {
    const [result] = await eslint.lintText(
      "import Page from '../../presentation/catalogue/ProductsPage.vue'; void Page;",
      { filePath: 'src/application/catalogue/Invalid.ts' },
    );
    expect(result?.messages.some((message) => message.ruleId === 'no-restricted-imports')).toBe(true);
  });

  it('rejects an integration import from presentation', async () => {
    const [result] = await eslint.lintText(
      "import { catalogueApi } from '../../integration/catalogue'; void catalogueApi;",
      {
        filePath: 'src/presentation/catalogue/Invalid.ts',
      },
    );
    expect(result?.messages.some((message) => message.ruleId === 'no-restricted-imports')).toBe(true);
  });

  it('allows the catalogue integration seam from application', async () => {
    const [result] = await eslint.lintText(
      "import { catalogueApi } from '../../integration/catalogue'; void catalogueApi;",
      {
        filePath: 'src/application/catalogue/Valid.ts',
      },
    );
    expect(result?.messages).toEqual([]);
  });
});
