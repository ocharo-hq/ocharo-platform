import { ESLint } from 'eslint';
import { describe, expect, it } from 'vitest';

describe('frontend dependency rules', () => {
  it('rejects direct presentation-to-integration imports', async () => {
    const eslint = new ESLint();
    const [result] = await eslint.lintText(
      "import { systemApi } from '@/integration/system/SystemApi';\nexport const api = systemApi;\n",
      { filePath: 'src/presentation/system/Forbidden.ts' },
    );
    expect(result?.messages.some((message) => message.ruleId === 'no-restricted-imports')).toBe(true);
  });

  it('allows the same-domain application-to-integration edge', async () => {
    const eslint = new ESLint();
    const [result] = await eslint.lintText(
      "import { systemApi } from '@/integration/system/SystemApi';\nexport const api = systemApi;\n",
      { filePath: 'src/application/system/Allowed.ts' },
    );
    expect(result?.errorCount).toBe(0);
  });

  it('rejects another domain slice even through a relative dynamic import', async () => {
    const eslint = new ESLint();
    const [result] = await eslint.lintText(
      "export const load = () => import('../other/Unrelated');\n",
      { filePath: 'src/application/system/Forbidden.ts' },
    );
    expect(result?.messages.some((message) => message.ruleId === 'architecture/boundaries')).toBe(true);
  });
});
