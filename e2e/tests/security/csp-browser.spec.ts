import type { Page } from "@playwright/test";

import { expect, test } from "../fixtures";
import { appURL } from "../agenda/environment";
import { parseCsp, scriptNonce } from "./csp";

/**
 * SPEC-0010 E2E-01 — com a CSP com nonce ativa, o fã abre a agenda da cidade no celular (e as demais páginas do
 * web: a inicial e a de cidade inexistente) e a página renderiza sem nenhuma violação de CSP (evento
 * `securitypolicyviolation` ou erro de CSP no console) e sem violações serious/critical do axe.
 */
test.use({ baseURL: appURL, ignoreHTTPSErrors: true });

declare global {
  interface Window {
    __cspViolations?: string[];
  }
}

/** Registra as violações de CSP desde antes do primeiro script da página. */
async function recordCspViolations(page: Page): Promise<string[]> {
  const consoleErrors: string[] = [];
  page.on("console", (message) => {
    if (
      message.type() === "error" &&
      /content.security.policy/i.test(message.text())
    ) {
      consoleErrors.push(message.text());
    }
  });
  await page.addInitScript(() => {
    window.__cspViolations = [];
    document.addEventListener("securitypolicyviolation", (event) => {
      window.__cspViolations?.push(
        `${event.effectiveDirective} bloqueou ${event.blockedURI || "inline"}`,
      );
    });
  });
  return consoleErrors;
}

async function expectNoCspViolations(page: Page, consoleErrors: string[]) {
  await page.waitForLoadState("load");
  const violations = await page.evaluate(() => window.__cspViolations ?? []);
  expect(violations, `violações de CSP em ${page.url()}`).toEqual([]);
  expect(consoleErrors, `erros de CSP no console em ${page.url()}`).toEqual([]);
}

test.describe("páginas com a CSP com nonce", () => {
  test("SPEC-0010:E2E-01 fã abre a agenda da cidade no celular sem erros de CSP e sem violações do axe [jornada: agenda] @smoke", async ({
    page,
    axe,
  }) => {
    const consoleErrors = await recordCspViolations(page);

    const response = await page.goto("/cidade-exemplo");

    expect(response?.status()).toBe(200);
    expect(
      scriptNonce(
        parseCsp(
          (await response?.headerValue("content-security-policy")) ?? undefined,
        ),
      ),
      "a agenda não veio com a CSP com nonce",
    ).toBeTruthy();
    await expect(
      page.getByRole("heading", { level: 1, name: "Agenda Cidade Exemplo" }),
    ).toBeVisible();
    await expectNoCspViolations(page, consoleErrors);
    await axe.check(page);
  });

  for (const path of ["/", "/cidade-inexistente"]) {
    test(`SPEC-0010:E2E-01 ${path} renderiza sem erros de CSP [jornada: agenda]`, async ({
      page,
    }) => {
      const consoleErrors = await recordCspViolations(page);

      const response = await page.goto(path);

      expect(response?.status()).toBeLessThan(500);
      expect(
        scriptNonce(
          parseCsp(
            (await response?.headerValue("content-security-policy")) ??
              undefined,
          ),
        ),
        `${path} não veio com a CSP com nonce`,
      ).toBeTruthy();
      await expectNoCspViolations(page, consoleErrors);
    });
  }
});
