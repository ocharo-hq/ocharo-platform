import js from '@eslint/js';
import vue from 'eslint-plugin-vue';
import globals from 'globals';
import tseslint from 'typescript-eslint';

function disallow(layers) {
  return ['error', { patterns: layers.flatMap((layer) => [`**/${layer}`, `**/${layer}/**`]) }];
}

export default [
  { ignores: ['dist/**', 'node_modules/**', 'coverage/**'] },
  js.configs.recommended,
  ...tseslint.configs.recommended,
  ...vue.configs['flat/recommended'],
  {
    files: ['**/*.{ts,vue}'],
    languageOptions: {
      globals: { ...globals.browser, __APP_VERSION__: 'readonly' },
      parserOptions: { parser: tseslint.parser },
    },
    rules: {
      // Prettier owns formatting; keep eslint-plugin-vue to correctness rules.
      'vue/max-attributes-per-line': 'off',
      'vue/singleline-html-element-content-newline': 'off',
      'vue/multiline-html-element-content-newline': 'off',
      'vue/html-self-closing': 'off',
      'vue/html-closing-bracket-newline': 'off',
      'vue/html-indent': 'off',
      'vue/first-attribute-linebreak': 'off',
    },
  },
  { files: ['*.mjs', 'scripts/**/*.mjs', '*config.ts'], languageOptions: { globals: globals.node } },
  {
    files: ['src/domain/**/*.ts'],
    rules: { 'no-restricted-imports': disallow(['bootstrap', 'application', 'integration', 'presentation']) },
  },
  {
    files: ['src/integration/**/*.ts'],
    rules: { 'no-restricted-imports': disallow(['bootstrap', 'application', 'presentation']) },
  },
  {
    files: ['src/application/**/*.ts'],
    rules: { 'no-restricted-imports': disallow(['bootstrap', 'presentation']) },
  },
  {
    files: ['src/presentation/**/*.{ts,vue}'],
    rules: { 'no-restricted-imports': disallow(['bootstrap', 'integration']) },
  },
];
