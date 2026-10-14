import AxeBuilder from "@axe-core/playwright";
import { test as base, expect, type Page } from "@playwright/test";

/** SPEC-0005 — verificação de acessibilidade (axe) usada pelos testes E2E. */
export interface AxeChecker {
  /** Roda o axe na página e falha o teste se houver violações serious ou critical (nomeando as regras). */
  check(page: Page): Promise<void>;
}

/** Impactos do axe que reprovam a página (ADR-0005). */
const blockingImpacts = new Set(["serious", "critical"]);

/** SPEC-0005 — `test` do Playwright com o fixture `axe`. Uso: `import { test, expect } from "./fixtures"`. */
export const test = base.extend<{ axe: AxeChecker }>({
  // eslint-disable-next-line no-empty-pattern -- o Playwright exige o objeto de fixtures desestruturado.
  axe: async ({}, use) => {
    await use({
      async check(page) {
        const results = await new AxeBuilder({ page }).analyze();
        const blocking = results.violations.filter(
          (violation) =>
            violation.impact != null && blockingImpacts.has(violation.impact),
        );
        if (blocking.length === 0) {
          return;
        }

        const details = blocking.map(
          (violation) =>
            `- ${violation.id} (${violation.impact}): ${violation.help} — ` +
            violation.nodes.map((node) => node.target.join(" ")).join(", "),
        );
        throw new Error(
          `axe: ${blocking.length} violação(ões) serious/critical em ${page.url()}\n${details.join("\n")}`,
        );
      },
    });
  },
});

export { expect };
