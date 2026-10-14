import { test as base, expect, type Page } from "@playwright/test";

/** SPEC-0005 — verificação de acessibilidade (axe) usada pelos testes E2E. */
export interface AxeChecker {
  /** Roda o axe na página e falha o teste se houver violações serious ou critical (nomeando as regras). */
  check(page: Page): Promise<void>;
}

/** SPEC-0005 — `test` do Playwright com o fixture `axe`. Uso: `import { test, expect } from "./fixtures"`. */
export const test = base.extend<{ axe: AxeChecker }>({
  axe: async ({}, use) => {
    void use;
    throw new Error("fixture axe ainda não implementado (SPEC-0005)");
  },
});

export { expect };
