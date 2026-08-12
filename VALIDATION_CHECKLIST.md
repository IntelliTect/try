# Pre-Push Validation Checklist

**Before pushing to GitHub, run the complete local validation for all npm packages:**

```bash
# 1. microsoft-trydotnet
cd src/microsoft-trydotnet
npm ci
npm run buildProd

# 2. microsoft-trydotnet-editor
cd ../microsoft-trydotnet-editor
npm ci
npm run buildProd

# 3. microsoft-trydotnet-styles
cd ../microsoft-trydotnet-styles
npm ci
npm run buildProd

# 4. microsoft-learn-mock
cd ../microsoft-learn-mock
npm ci
npm run buildProd
```

**All builds must complete without errors.** Warnings are acceptable (e.g., polyglot-notebooks circular dependencies), but any ERROR output means the CI will fail.

## Why This Matters

- `npm install --package-lock-only` only validates lock file syntax; it does NOT catch build/TypeScript errors
- CI runs the full build and will fail if any package cannot be built
- Running locally first catches:
  - TypeScript compilation errors
  - Missing type definitions
  - Webpack/bundler issues
  - Node version compatibility
  - Module resolution failures
  - Test failures

## Common Issues to Watch For

1. **TypeScript errors** → Check `tsconfig.json` has `"types": ["node"]` if code uses Node APIs (Buffer, util, etc.)
2. **Module not found** → Verify dependencies are listed in `package.json`
3. **Engine version conflicts** → Check that all transitive dependencies support the pinned Node version
4. **Build cache issues** → Run `npm ci` (not `npm install`) to use exact lock file versions
