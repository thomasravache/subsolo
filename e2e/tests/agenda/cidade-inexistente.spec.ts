import { expect, test } from "../fixtures";
import { appURL } from "./environment";

/**
 * SPEC-0024 CH-01 (achado da SPEC-0015) — a página de cidade inexistente carregada direto (sem navegação no cliente).
 * Caracterização: o que já está certo fica preso — status 404 real e o título "Cidade não encontrada" na página.
 * Hoje o `<title>` sai "Subsolo" (o do layout raiz) em vez de "Cidade não encontrada · Subsolo"; a guarda do título
 * correto entra junto com os testes da correção.
 */
test.use({ baseURL: appURL, ignoreHTTPSErrors: true });

const missingCity = "/cidade-inexistente";

test.describe("cidade inexistente carregada direto", () => {
  test("SPEC-0024:CH-01 responde 404 com a página Cidade não encontrada [jornada: agenda]", async ({
    page,
  }, testInfo) => {
    test.skip(
      testInfo.project.name !== "mobile-chrome",
      "verificação da página carregada direto: basta um projeto",
    );

    const response = await page.goto(missingCity);

    expect(response?.status()).toBe(404);
    await expect(
      page.getByRole("heading", { level: 1, name: "Cidade não encontrada" }),
    ).toBeVisible();
  });
});

test.describe("título da cidade inexistente", () => {
  test("SPEC-0024:CH-01 o título da página carregada direto é Cidade não encontrada · Subsolo [jornada: agenda]", async ({
    page,
  }, testInfo) => {
    test.skip(
      testInfo.project.name !== "mobile-chrome",
      "verificação da página carregada direto: basta um projeto",
    );

    const response = await page.goto(missingCity);

    expect(response?.status()).toBe(404);
    await expect(page).toHaveTitle("Cidade não encontrada · Subsolo");
  });
});
