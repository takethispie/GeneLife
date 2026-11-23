import js from "@eslint/js";
import globals from "globals";
import tseslint from "typescript-eslint";

export default tseslint.config(
  {
    ignores: [
      "dist/**",
      "coverage/**",
      "node_modules/**",
      "playwright-report/**",
      "playwright-report-admin/**",
      "test-results/**",
      "test-results-admin/**",
      "src/contracts/rest.generated.ts"
    ]
  },
  js.configs.recommended,
  ...tseslint.configs.recommended,
  {
    files: ["**/*.ts"],
    languageOptions: {
      globals: { ...globals.browser, ...globals.node }
    },
    rules: {
      "no-undef": "off",
      "@typescript-eslint/no-explicit-any": "warn",
      "@typescript-eslint/no-unused-vars": [
        "error",
        { argsIgnorePattern: "^_", varsIgnorePattern: "^_" }
      ],
      "no-console": ["warn", { allow: ["warn", "error", "info", "debug"] }]
    }
  },
  {
    files: ["**/*.js", "**/*.mjs"],
    languageOptions: {
      globals: { ...globals.node }
    }
  }
);
