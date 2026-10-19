import { expect, test } from "../fixtures";
import { appURL } from "../agenda/environment";
import { card, press } from "../shows/journey";
import { publishProfiles } from "./perfil-publico";

/**
 * SPEC-0026 — perfil público de banda, casa e produtor na aplicação completa (pilha do Compose). Tela do Stitch "Perfil
 * da Banda - Podridão - Subsolo", com os ajustes do §6 (sem foto e sem o botão "Vou"; o "Seguir" e a contagem chegam
 * com a SPEC-0027).
 *
 * - E2E-01 (mobile-chrome e mobile-safari): com uma banda e uma casa com perfil e um show com as duas, o fã abre a
 *   página do show, toca no nome da banda e cai em `/banda/{slug}`: nome, links "Instagram", "Bandcamp" e "Metal
 *   Archives" com nome acessível ("Instagram de {nome}") e `rel="noopener noreferrer"`, o show em "PRÓXIMOS SHOWS"
 *   (card que leva à página do show, data em `<time>`) e a seção "SHOWS PASSADOS"; volta, toca na casa e vê
 *   `/casa/{slug}` com o show; um perfil sem shows mostra "Nenhum show anunciado ainda.". Sem violações do axe.
 * - E2E-02 (mobile-chrome): `/banda/nao-existe` mostra "Perfil não encontrado", com status 404.
 *
 * Seletores: papéis e nomes acessíveis; os cabeçalhos podem estar em caixa alta só pelo CSS (expressões sem diferenciar
 * maiúsculas). Cada link só é tocado depois de a página hidratar.
 */
// SPEC-0010: o Caddy do Compose serve HTTPS com o certificado local (`tls internal`), não confiável.
test.use({ baseURL: appURL, ignoreHTTPSErrors: true });

test.describe("perfil público", () => {
  test("SPEC-0026:E2E-01 o fã vai do show à banda e à casa, com as redes e os shows [jornada: compartilhar-show]", async ({
    page,
    browser,
    axe,
  }, testInfo) => {
    test.skip(
      testInfo.project.name === "desktop-chrome",
      "jornada do celular (mobile-chrome e mobile-safari)",
    );
    test.setTimeout(300_000);
    const setup = await publishProfiles(browser, "perfil01");
    const { band, venue, show } = setup;

    // A página do show liga a banda ao perfil dela.
    const showPage = await page.goto(show.path);
    expect(showPage?.status(), `GET ${show.path}`).toBe(200);
    const main = page.locator("main");
    const bandLink = main.getByRole("link", { name: band.name, exact: true });
    await expect(bandLink).toHaveAttribute("href", band.path);
    await press(bandLink);
    await expect(page).toHaveURL((url) => url.pathname === band.path);

    // /banda/{slug}: nome, redes em destaque, próximos e passados.
    await expect(
      page.getByRole("heading", { level: 1, name: band.name }),
    ).toBeVisible();
    for (const [network, href] of [
      ["Instagram", band.instagram],
      ["Bandcamp", band.bandcamp],
      ["Metal Archives", band.metalArchives],
    ] as const) {
      const link = page.getByRole("link", {
        name: `${network} de ${band.name}`,
      });
      await expect(link, `link ${network}`).toHaveAttribute("href", href);
      const rel = (await link.getAttribute("rel")) ?? "";
      expect(rel, `rel do link ${network}`).toContain("noopener");
      expect(rel, `rel do link ${network}`).toContain("noreferrer");
    }
    await expect(
      page.getByRole("heading", { name: /próximos shows/i }),
    ).toBeVisible();
    await expect(
      page.getByRole("heading", { name: /shows passados/i }),
    ).toBeVisible();
    const upcoming = card(page.locator("main"), show.title);
    await expect(upcoming).toHaveCount(1);
    await expect(upcoming.locator("time").first()).toBeVisible();
    await expect(
      page.locator(`main a[href="${show.path}"]`).first(),
      "o card do próximo show não leva à página do show",
    ).toBeVisible();
    await axe.check(page);

    // Volta ao show e vai à casa.
    await page.goBack();
    await expect(page).toHaveURL((url) => url.pathname === show.path);
    const venueLink = page
      .locator("main")
      .getByRole("link", { name: venue.name, exact: true });
    await expect(venueLink).toHaveAttribute("href", venue.path);
    await press(venueLink);
    await expect(page).toHaveURL((url) => url.pathname === venue.path);
    await expect(
      page.getByRole("heading", { level: 1, name: venue.name }),
    ).toBeVisible();
    await expect(card(page.locator("main"), show.title)).toHaveCount(1);
    await axe.check(page);

    // Perfil sem shows.
    const quiet = await page.goto(setup.bandWithoutShows.path);
    expect(quiet?.status(), `GET ${setup.bandWithoutShows.path}`).toBe(200);
    await expect(
      page.getByRole("heading", {
        level: 1,
        name: setup.bandWithoutShows.name,
      }),
    ).toBeVisible();
    await expect(page.getByText("Nenhum show anunciado ainda.")).toBeVisible();
    await axe.check(page);
  });

  test("SPEC-0026:E2E-02 perfil inexistente mostra Perfil não encontrado com 404 [jornada: compartilhar-show]", async ({
    page,
    axe,
  }, testInfo) => {
    test.skip(
      testInfo.project.name !== "mobile-chrome",
      "verificação em um projeto (mobile-chrome)",
    );

    const response = await page.goto("/banda/nao-existe");

    expect(response?.status()).toBe(404);
    await expect(page.getByText("Perfil não encontrado").first()).toBeVisible();
    await axe.check(page);
  });
});
