import { fileURLToPath } from "node:url";

import { defineConfig } from "vitest/config";

/**
 * SPEC-0005 — testes unitários do front (ADR-0005): Vitest + Testing Library + jsdom.
 * Os testes ficam ao lado do código, em arquivos *.test.ts(x); `pnpm test` roda todos uma vez.
 */
export default defineConfig({
  resolve: {
    alias: { "@": fileURLToPath(new URL("./src", import.meta.url)) },
  },
  test: {
    environment: "jsdom",
    include: ["src/**/*.test.{ts,tsx}"],
    setupFiles: ["src/test/setup.ts"],
    restoreMocks: true,
  },
});
