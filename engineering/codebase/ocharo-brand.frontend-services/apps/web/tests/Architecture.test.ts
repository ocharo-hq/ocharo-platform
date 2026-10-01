import { ESLint } from 'eslint';
import { describe, expect, it } from 'vitest';

const eslint = new ESLint();

describe('application boundaries', () => {
  it('rejects a presentation import from application orchestration', async () => {
    const [result] = await eslint.lintText("import Page from '../../presentation/brand/OverviewPage.vue'; void Page;", {
      filePath: 'src/application/brand/Invalid.ts',
    });
    expect(result?.messages.some((message) => message.ruleId === 'no-restricted-imports')).toBe(true);
  });

  it('rejects an integration import from presentation', async () => {
    const [result] = await eslint.lintText("import { brandApi } from '../../integration/brand'; void brandApi;", {
      filePath: 'src/presentation/brand/Invalid.ts',
    });
    expect(result?.messages.some((message) => message.ruleId === 'no-restricted-imports')).toBe(true);
  });
});
