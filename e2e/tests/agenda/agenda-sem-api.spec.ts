import { expect, test } from "../fixtures";
import { externalApp, webWithoutApiURL } from "./environment";

/**
 * SPEC-0008 IT-04 — o servidor Next.js com `API_URL` apontando para uma porta sem serviço (subido pelo `webServer`
 * do `playwright.config.ts`) responde `/cidade-exemplo` com status 503 e a mensagem de indisponibilidade, em vez
 * de quebrar.
 */
const unavailableMessage =
  "A agenda está fora do ar por alguns instantes. Tente de novo.";

test.describe("agenda com a API fora do ar", () => {
  test("SPEC-0008:IT-04 página da cidade responde 503 com a mensagem de indisponibilidade", async ({
    page,
  }, testInfo) => {
    test.skip(
      externalApp,
      "com BASE_URL o webServer não sobe o Next.js sem API",
    );
    test.skip(
      testInfo.project.name !== "desktop-chrome",
      "verificação do servidor: basta um projeto",
    );

    const response = await page.goto(`${webWithoutApiURL}/cidade-exemplo`);

    expect(response?.status()).toBe(503);
    await expect(page.getByText(unavailableMessage)).toBeVisible();
  });
});
