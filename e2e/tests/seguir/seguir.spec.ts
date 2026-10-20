import { expect, test } from "../fixtures";
import { appURL } from "../agenda/environment";
import { signIn, uniqueEmail } from "../auth/journey";
import { announcements, press } from "../shows/journey";
import { prepareFollowTargets } from "./seguir";

/**
 * SPEC-0027 — seguir cidade, banda e casa na aplicação completa (pilha do Compose). Telas do Stitch "Agenda Curitiba -
 * Subsolo" (botão "SEGUIR CURITIBA" no cabeçalho), "Perfil da Banda - Podridão" (botão "Seguir" e contagem) e a parte
 * "Seguindo" de "Avisos e Seguindo"; navegação inferior Agenda, Seguindo, Apoie, Conta.
 *
 * - E2E-01 (mobile-chrome e mobile-safari): o fã logado toca em "Seguir {cidade}" no cabeçalho da agenda (vira
 *   "Seguindo", `aria-pressed="true"`, nome constante "Seguir {cidade}" — SPEC-0029 H27-04 —, mudança anunciada), segue a banda na página do
 *   perfil ("0 seguidores" → "1 seguidor(es)"), abre a aba "Seguindo" e vê as duas (seções de cidades e de bandas, com
 *   cabeçalhos); deixa de seguir a banda e ela some (a cidade fica). Sem violações do axe em cada tela.
 * - E2E-02 (mobile-chrome): anônimo tocando em "Seguir {cidade}" vai para `/entrar?returnTo=/{cidade}`; abrindo
 *   `/seguindo` vai para `/entrar?returnTo=/seguindo`.
 *
 * Seletores: papéis e nomes acessíveis; os textos podem estar em caixa alta só pelo CSS (expressões sem diferenciar
 * maiúsculas). Cada controle só é tocado depois de a página hidratar.
 */
// SPEC-0010: o Caddy do Compose serve HTTPS com o certificado local (`tls internal`), não confiável.
test.use({ baseURL: appURL, ignoreHTTPSErrors: true });

/** Escapa um texto para usar numa expressão regular. */
function literal(text: string): string {
  return text.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

test.describe("seguir cidade, banda e casa", () => {
  test("SPEC-0027:E2E-01 o fã segue a cidade e a banda, vê as duas em Seguindo e deixa de seguir a banda [jornada: seguir-e-avisar]", async ({
    page,
    browser,
    axe,
  }, testInfo) => {
    test.skip(
      testInfo.project.name === "desktop-chrome",
      "jornada do celular (mobile-chrome e mobile-safari)",
    );
    test.setTimeout(300_000);
    const { city, band } = await prepareFollowTargets(browser);
    const agendaPath = `/${city.slug}`;
    await signIn(page, uniqueEmail("seguir01"), agendaPath);

    // Agenda: "Seguir {cidade}" no cabeçalho.
    await expect(
      page.getByRole("heading", { level: 1, name: `Agenda ${city.name}` }),
    ).toBeVisible();
    const followCity = page.getByRole("button", {
      name: `Seguir ${city.name}`,
      exact: true,
    });
    await expect(followCity).toHaveAttribute("aria-pressed", "false");
    await press(followCity);
    // SPEC-0029:H27-04 — nome acessível constante "Seguir {nome}"; o estado vem do aria-pressed.
    const followingCity = page.getByRole("button", {
      name: `Seguir ${city.name}`,
      exact: true,
    });
    await expect(followingCity).toHaveAttribute("aria-pressed", "true");
    await expect(followingCity).toContainText(/seguindo/i);
    await expect(
      announcements(page).filter({
        hasText: new RegExp(literal(city.name), "i"),
      }),
      "a mudança não foi anunciada",
    ).not.toHaveCount(0);
    await axe.check(page);

    // Recarregada, a agenda continua mostrando "Seguindo".
    await page.reload();
    await expect(followingCity).toHaveAttribute("aria-pressed", "true");

    // Perfil da banda: "Seguir" + contagem.
    const profile = await page.goto(band.path);
    expect(profile?.status(), `GET ${band.path}`).toBe(200);
    await expect(
      page.getByRole("heading", { level: 1, name: band.name }),
    ).toBeVisible();
    await expect(page.getByText(/^0 seguidores$/i)).toBeVisible();
    const followBand = page.getByRole("button", {
      name: `Seguir ${band.name}`,
      exact: true,
    });
    await expect(followBand).toHaveAttribute("aria-pressed", "false");
    await press(followBand);
    // SPEC-0029:H27-04 — nome acessível constante "Seguir {nome}"; o estado vem do aria-pressed.
    const followingBand = page.getByRole("button", {
      name: `Seguir ${band.name}`,
      exact: true,
    });
    await expect(followingBand).toHaveAttribute("aria-pressed", "true");
    await expect(followingBand).toContainText(/seguindo/i);
    await expect(page.getByText(/^1 seguidor(es)?$/i)).toBeVisible();
    await axe.check(page);

    // Aba "Seguindo": as duas, por tipo.
    const followingTab = page
      .getByRole("navigation", { name: "Navegação inferior" })
      .getByRole("link", { name: /seguindo/i });
    await press(followingTab);
    await expect(page).toHaveURL((url) => url.pathname === "/seguindo");
    const main = page.locator("main");
    await expect(
      main.getByRole("heading", { name: /^cidades$/i }),
    ).toBeVisible();
    await expect(
      main.getByRole("heading", { name: /^bandas$/i }),
    ).toBeVisible();
    await expect(main.getByText(city.name, { exact: true })).toBeVisible();
    await expect(main.getByText(band.name, { exact: true })).toBeVisible();
    await expect(
      main.getByRole("button", { name: `Deixar de seguir ${city.name}` }),
    ).toBeVisible();
    await axe.check(page);

    // Deixa de seguir a banda: ela some; a cidade fica.
    await press(
      main.getByRole("button", { name: `Deixar de seguir ${band.name}` }),
    );
    await expect(main.getByText(band.name, { exact: true })).toHaveCount(0);
    await expect(main.getByText(city.name, { exact: true })).toBeVisible();
    await page.reload();
    await expect(main.getByText(band.name, { exact: true })).toHaveCount(0);
    await expect(main.getByText(city.name, { exact: true })).toBeVisible();
    await axe.check(page);

    // A contagem da banda voltou a zero.
    await page.goto(band.path);
    await expect(page.getByText(/^0 seguidores$/i)).toBeVisible();
    await expect(
      page.getByRole("button", { name: `Seguir ${band.name}`, exact: true }),
    ).toHaveAttribute("aria-pressed", "false");
  });

  test("SPEC-0027:E2E-02 anônimo que toca em Seguir ou abre Seguindo vai para entrar com a volta [jornada: seguir-e-avisar]", async ({
    page,
    browser,
  }, testInfo) => {
    test.skip(
      testInfo.project.name !== "mobile-chrome",
      "verificação em um projeto (mobile-chrome)",
    );
    test.setTimeout(180_000);
    const { city } = await prepareFollowTargets(browser, { withBand: false });
    const agendaPath = `/${city.slug}`;

    const agenda = await page.goto(agendaPath);
    expect(agenda?.status(), `GET ${agendaPath}`).toBe(200);
    await press(
      page.getByRole("button", { name: `Seguir ${city.name}`, exact: true }),
    );
    await expect(page).toHaveURL(
      (url) =>
        url.pathname === "/entrar" &&
        url.searchParams.get("returnTo") === agendaPath,
    );

    await page.goto("/seguindo");
    await expect(page).toHaveURL(
      (url) =>
        url.pathname === "/entrar" &&
        url.searchParams.get("returnTo") === "/seguindo",
    );
  });
});
