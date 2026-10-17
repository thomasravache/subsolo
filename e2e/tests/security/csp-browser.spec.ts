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

/** Uma violação de CSP como registrada na página (`securitypolicyviolation`). */
interface CspViolation {
  directive: string;
  blockedURI: string;
  sample: string;
  /** `nodeName` do alvo do evento (o `tagName`, se for um elemento). */
  targetTag: string;
}

declare global {
  interface Window {
    __cspViolations?: CspViolation[];
  }
}

/**
 * Emenda 4 da SPEC-0010 — falso positivo do WebKit: o `<select>` nativo (estilo interno do shadow DOM do controle)
 * dispara `style-src-attr` com `sample` vazio, com alvo `SELECT` ou `#document`, e o erro de console abaixo, sem
 * nenhum atributo `style` no HTML. Reproduzido com página mínima; sem o `<select>` as páginas ficam limpas nos três
 * motores. Só isso é ignorado, e só no WebKit: a CSP continua estrita e qualquer outra violação reprova o teste.
 */
function isWebKitSelectFalsePositive(
  violation: CspViolation,
  browserName: string,
  pageHasSelect: boolean,
) {
  return (
    browserName === "webkit" &&
    violation.directive === "style-src-attr" &&
    violation.sample === "" &&
    (violation.targetTag === "SELECT" ||
      (violation.targetTag === "#document" && pageHasSelect))
  );
}

/** Erro de console que o WebKit emite junto com cada violação do `<select>` (Emenda 4). */
const webKitSelectConsoleError =
  "Refused to apply a stylesheet because its hash, its nonce, or 'unsafe-inline' does not appear in the style-src directive";

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
      window.__cspViolations?.push({
        directive: event.effectiveDirective || event.violatedDirective,
        blockedURI: event.blockedURI,
        sample: event.sample ?? "",
        targetTag: event.target instanceof Node ? event.target.nodeName : "",
      });
    });
  });
  return consoleErrors;
}

async function expectNoCspViolations(page: Page, consoleErrors: string[]) {
  await page.waitForLoadState("load");
  const browserName = page.context().browser()?.browserType().name() ?? "";
  const pageHasSelect = await page.evaluate(
    () => document.querySelector("select") !== null,
  );
  const recorded = await page.evaluate(() => window.__cspViolations ?? []);
  const violations = recorded.filter(
    (violation) =>
      !isWebKitSelectFalsePositive(violation, browserName, pageHasSelect),
  );
  // Cada violação descartada acima libera no máximo um erro de console do WebKit; o excedente reprova.
  let consoleAllowance = recorded.length - violations.length;
  const unexpectedConsoleErrors = consoleErrors.filter((text) => {
    if (consoleAllowance > 0 && text.startsWith(webKitSelectConsoleError)) {
      consoleAllowance -= 1;
      return false;
    }
    return true;
  });
  expect(
    violations.map(
      (violation) =>
        `${violation.directive} bloqueou ${violation.blockedURI || "inline"} em ${violation.targetTag || "?"}`,
    ),
    `violações de CSP em ${page.url()}`,
  ).toEqual([]);
  expect(
    unexpectedConsoleErrors,
    `erros de CSP no console em ${page.url()}`,
  ).toEqual([]);
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
