import { expect, test } from "../fixtures";
import { appURL } from "./environment";

/**
 * SPEC-0008 E2E-01 — na aplicação completa (pilha do Compose subida pelo `webServer`), o fã abre a agenda da cidade
 * de exemplo e vê o título e o estado vazio, sem violações serious/critical do axe; uma cidade inexistente ou um
 * slug fora do padrão (`/Cidade_X`) mostra "Cidade não encontrada" com status 404 (nunca soft 404); o cabeçalho
 * interno de estado da agenda (`x-subsolo-agenda`), quando enviado pelo cliente, é ignorado (Emenda 5).
 */
test.use({ baseURL: appURL });

test.describe("agenda da cidade", () => {
  test("SPEC-0008:E2E-01 fã vê a agenda vazia da cidade no celular e a página de cidade inexistente [jornada: agenda] @smoke", async ({
    page,
    axe,
  }) => {
    const agenda = await page.goto("/cidade-exemplo");
    expect(agenda?.status()).toBe(200);
    await expect(
      page.getByRole("heading", { level: 1, name: "Agenda Cidade Exemplo" }),
    ).toBeVisible();
    await expect(
      page.getByText("Nenhum show anunciado ainda em Cidade Exemplo."),
    ).toBeVisible();
    await axe.check(page);

    const missing = await page.goto("/cidade-inexistente");
    expect(missing?.status()).toBe(404);
    await expect(page.getByText("Cidade não encontrada").first()).toBeVisible();

    const outOfPattern = await page.goto("/Cidade_X");
    expect(outOfPattern?.status()).toBe(404);
    await expect(page.getByText("Cidade não encontrada").first()).toBeVisible();
  });

  for (const forged of ["unavailable", "not-found"]) {
    test(`SPEC-0008:E2E-01 cabeçalho interno x-subsolo-agenda: ${forged} enviado pelo cliente é ignorado [jornada: agenda] @smoke`, async ({
      page,
    }) => {
      await page.setExtraHTTPHeaders({ "x-subsolo-agenda": forged });

      const agenda = await page.goto("/cidade-exemplo");

      expect(agenda?.status()).toBe(200);
      await expect(
        page.getByRole("heading", { level: 1, name: "Agenda Cidade Exemplo" }),
      ).toBeVisible();
      await expect(
        page.getByText("Nenhum show anunciado ainda em Cidade Exemplo."),
      ).toBeVisible();
    });
  }
});
