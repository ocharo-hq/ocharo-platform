import { dirname, resolve } from 'node:path';

import vue from 'eslint-plugin-vue';
import ts from 'typescript-eslint';

/** @internal The permitted inward dependencies, including each layer's own slice. */
const allowedLayers = {
  bootstrap: ['bootstrap', 'presentation', 'application', 'domain', 'integration'],
  presentation: ['presentation', 'application', 'domain'],
  application: ['application', 'domain', 'integration'],
  integration: ['integration', 'domain'],
  domain: ['domain'],
};

/** @internal Checks resolved internal imports so relative paths cannot bypass slice boundaries. */
const architectureRule = {
  meta: { type: 'problem', schema: [], messages: { edge: 'Disallowed layer or domain-slice dependency.' } },
  create(context) {
    const filename = context.filename;
    const rootIndex = filename.lastIndexOf('/src/');
    if (rootIndex < 0) return {};
    const root = filename.slice(0, rootIndex + 5);
    const [layer, slice] = filename.slice(root.length).split('/');
    const allowed = allowedLayers[layer];
    if (!allowed) return {};
    const check = (node) => {
      const source = node.source?.value;
      if (typeof source !== 'string') return;
      const target = source.startsWith('@/') ? resolve(root, source.slice(2))
        : source.startsWith('.') ? resolve(dirname(filename), source) : null;
      if (!target?.startsWith(root)) return;
      const [targetLayer, targetSlice] = target.slice(root.length).split('/');
      if (!allowed.includes(targetLayer) ||
          (layer !== 'bootstrap' && targetSlice !== slice && targetSlice !== 'shared')) {
        context.report({ node, messageId: 'edge' });
      }
    };
    return { ImportDeclaration: check, ExportNamedDeclaration: check, ExportAllDeclaration: check, ImportExpression: check };
  },
};

/** Creates rules that keep application layers on their permitted dependency edges. */
function forbidLayers(layers, additional = []) {
  return ['error', {
    patterns: [
      ...layers.map((layer) => ({
        group: [`@/${layer}/**`, `**/${layer}/**`],
        message: `This layer cannot depend on ${layer}.`,
      })),
      ...additional,
      { group: ['@wow-two-beta/fe/src/**', '@wow-two-beta/fe/dist/**'], message: 'Use SDK public subpaths.' },
    ],
  }];
}

export default ts.config(
  { ignores: ['node_modules/**', 'coverage/**'] },
  ...ts.configs.recommended,
  ...vue.configs['flat/essential'],
  {
    files: ['**/*.vue'],
    languageOptions: { parserOptions: { parser: ts.parser } },
  },
  {
    files: ['src/**/*.{ts,vue}'],
    plugins: { architecture: { rules: { boundaries: architectureRule } } },
    rules: { 'no-restricted-imports': forbidLayers([]), 'architecture/boundaries': 'error' },
  },
  {
    files: ['src/presentation/**/*.{ts,vue}'],
    rules: { 'no-restricted-imports': forbidLayers(['bootstrap', 'integration']) },
  },
  {
    files: ['src/application/**/*.ts'],
    rules: { 'no-restricted-imports': forbidLayers(['bootstrap', 'presentation']) },
  },
  {
    files: ['src/integration/**/*.ts'],
    rules: {
      'no-restricted-imports': forbidLayers(['bootstrap', 'presentation', 'application'], [
        { group: ['vue', 'vue-router', '@tanstack/vue-query'], message: 'Keep transport independent of UI runtime.' },
      ]),
    },
  },
  {
    files: ['src/domain/**/*.ts'],
    rules: {
      'no-restricted-imports': forbidLayers(['bootstrap', 'presentation', 'application', 'integration'], [
        { group: ['vue', 'vue-router', '@tanstack/**', '@wow-two-beta/**'], message: 'Domain types have no runtime dependencies.' },
      ]),
    },
  },
);
