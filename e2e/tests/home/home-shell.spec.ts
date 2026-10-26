import { expect, test } from "../fixtures";
import { appURL } from "../agenda/environment";
import { chooseCity } from "../shows/journey";

/**
 * SPEC-0044 — a página inicial (`/`) dentro da moldura das páginas públicas (shell da SPEC-0011), na aplicação completa
 * (pilha do Compose). O visitante é a página do teste, anônima; a cidade é a do seed de desenvolvimento.
 *
 * - E2E-01 (mobile-chrome): o anônimo abre `/` e vê o título "Subsolo", o seletor de cidade no cabeçalho, a aba
 *   "Conta" da navegação inferior levando a `/entrar` e o rodapé com "Apoie"; escolhe a cidade no seletor e chega à
 *   agenda em `/{cidade}`. Sem violações do axe.
 */
test.use({ baseURL: appURL, ignoreHTTPSErrors: true });

const city = { slug: "cidade-exemplo", name: "Cidade Exemplo" };

test.describe("página inicial na moldura do site", () => {
  test("SPEC-0044:E2E-01 anônimo abre a página inicial, vê cabeçalho, a aba conta e rodapé, e chega à agenda pelo seletor de cidade [jornada: agenda]", async ({
    page,
    axe,
  }, testInfo) => {
    test.skip(
      testInfo.project.name !== "mobile-chrome",
      "jornada do celular (mobile-chrome)",
    );
    test.setTimeout(120_000);

    const opened = await page.goto("/");
    expect(opened?.status()).toBe(200);
    await expect(
      page.getByRole("heading", { level: 1, name: "Subsolo" }),
    ).toBeVisible();

    const header = page.getByRole("banner");
    await expect(header, "página inicial sem cabeçalho").toBeVisible();
    const switcher = header.getByRole("combobox", { name: /cidade/i });
    await expect(switcher, "cabeçalho sem seletor de cidade").toBeVisible();
    // No celular, a entrada é a aba "Conta" da navegação inferior (o "Entrar" do cabeçalho só aparece no desktop).
    const accountTab = page
      .getByRole("navigation", { name: /navegação inferior/i })
      .getByRole("link", { name: /^conta$/i });
    await expect(
      accountTab,
      "página inicial sem a aba Conta na navegação inferior",
    ).toBeVisible();
    await expect(accountTab).toHaveAttribute("href", "/entrar");
    await expect(
      page.getByRole("contentinfo").getByRole("link", { name: /apoie/i }),
      "página inicial sem rodapé com Apoie",
    ).toHaveAttribute("href", "/apoie");

    await axe.check(page);

    // Escolher a cidade no seletor leva à agenda dela.
    await chooseCity(header, city);
    await expect(page).toHaveURL(new RegExp(`/${city.slug}$`));
    await expect(
      page.getByRole("heading", { level: 1, name: `Agenda ${city.name}` }),
    ).toBeVisible();
    await expect(
      page.getByRole("banner").getByRole("combobox", { name: /cidade/i }),
    ).toHaveValue(city.slug);
    await axe.check(page);
  });
});
