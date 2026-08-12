const eslintPluginTypescript = require('@typescript-eslint/eslint-plugin');
const typescriptEslintParser = require('@typescript-eslint/parser');

module.exports = [
  {
    ignores: [
      'jupyter.d.ts',
      'vscode.proposed.*.d.ts',
      'vscode.d.ts',
      'vscode.proposed.d.ts',
    ],
  },
  {
    files: ['src/**/*.ts'],
    languageOptions: {
      parser: typescriptEslintParser,
      parserOptions: {
        ecmaVersion: 6,
        project: [
          './tsconfig.json',
          './tsconfig.eslint.json'
        ],
        sourceType: 'module',
      },
    },
    plugins: {
      '@typescript-eslint': eslintPluginTypescript,
    },
    rules: {
      'curly': 'warn',
      '@typescript-eslint/no-deprecated': 'warn',
      'eqeqeq': 'warn',
      'no-throw-literal': 'warn',
      'semi': 'warn',
    },
  },
];
