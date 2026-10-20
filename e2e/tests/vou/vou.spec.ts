import { expect, test } from "../fixtures";
import { appURL } from "../agenda/environment";
import { signIn, uniqueEmail } from "../auth/journey";
import { announcements, card, press } from "../shows/journey";
import { publishShows } from "../shows/show-page";

/**
 * SPEC-0029 — "Vou" com contagem na aplicação completa (pilha do Compose). Telas do Stitch "Página do Show - Noite da
 * Peste II" (botão "VOU" e contagem) e "Agenda Curitiba - Subsolo" ("47 vão" nos cards); sem "Quem vai" (ADR-0008).
 *
 * - E2E-01 (mobile-chrome e mobile-safari): o fã logado abre a página do show, toca em "Vou" (nome acessível constante
 *   "Vou ao show {título}"; vira "Vou ✓", `aria-pressed="true"`, contagem +1 anunciada), volta à agenda e vê a contagem
 *   no card; desmarca e a contagem volta. Sem violações do axe em cada tela.
 * - E2E-02 (mobile-chrome): anônimo tocando em "Vou" vai para `/entrar?returnTo=/{cidade}/{show}`; um show cancelado
 *   não mostra o botão (a contagem continua).
 *
 * Seletores: papéis e nomes acessíveis; os textos podem estar em caixa alta só pelo CSS (expressões sem diferenciar
 * maiúsculas). Cada controle só é tocado depois de a página hidratar.
 */
// SPEC-0010: o Caddy do Compose serve HTTPS com o certificado local (`tls internal`), não confiável.
test.use({ baseURL: appURL, ignoreHTTPSErrors: true });

/** A contagem "{n} vão" / "{n} vai" (com o número exato). */
function attending(count: number): RegExp {
  return new RegExp(`(^|\\D)${count}\\s*(vão|vai)(?![a-zà-ú])`, "i");
}

test.describe("vou com contagem", () => {
  test("SPEC-0029:E2E-01 o fã marca vou no show, vê a contagem no card da agenda e desmarca [jornada: seguir-e-avisar]", async ({
    page,
    browser,
    axe,
  }, testInfo) => {
    test.skip(
      testInfo.project.name === "desktop-chrome",
      "jornada do celular (mobile-chrome e mobile-safari)",
    );
    test.setTimeout(300_000);
    const { producer, shows } = await publishShows(
      browser,
      "vou01",
      (suffix) => [{ title: `Noite da Peste ${suffix}` }],
    );
    const show = shows[0]!;
    const agendaPath = `/${producer.city.slug}`;
    await signIn(page, uniqueEmail("vou01"), show.path);

    // Página do show: "Vou" + "0 vão".
    await expect(
      page.getByRole("heading", { level: 1, name: show.title }),
    ).toBeVisible();
    const attend = page.getByRole("button", {
      name: `Vou ao show ${show.title}`,
      exact: true,
    });
    await expect(attend).toHaveAttribute("aria-pressed", "false");
    await expect(attend).toContainText(/vou/i);
    await expect(page.locator("main")).toContainText(attending(0));
    await expect(page.getByText(/quem vai/i)).toHaveCount(0);
    await axe.check(page);

    // Marca: "Vou ✓", contagem +1 anunciada.
    await press(attend);
    await expect(attend).toHaveAttribute("aria-pressed", "true");
    await expect(attend).toContainText(/vou\s*✓/i);
    await expect(page.locator("main")).toContainText(attending(1));
    await expect(
      announcements(page).filter({ hasText: /1/ }),
      "a mudança e a nova contagem não foram anunciadas",
    ).not.toHaveCount(0);
    await axe.check(page);

    // Recarregada, continua marcado.
    await page.reload();
    await expect(attend).toHaveAttribute("aria-pressed", "true");

    // Agenda: a contagem no card.
    const agenda = await page.goto(agendaPath);
    expect(agenda?.status(), `GET ${agendaPath}`).toBe(200);
    await expect(card(page, show.title)).toContainText(attending(1));
    await axe.check(page);

    // Desmarca: a contagem volta.
    await page.goto(show.path);
    await expect(attend).toHaveAttribute("aria-pressed", "true");
    await press(attend);
    await expect(attend).toHaveAttribute("aria-pressed", "false");
    await expect(page.locator("main")).toContainText(attending(0));
    await page.goto(agendaPath);
    await expect(card(page, show.title)).toContainText(attending(0));
  });

  test("SPEC-0029:E2E-02 anônimo que toca em Vou vai para entrar com a volta; show cancelado sem o botão [jornada: seguir-e-avisar]", async ({
    page,
    browser,
  }, testInfo) => {
    test.skip(
      testInfo.project.name !== "mobile-chrome",
      "verificação em um projeto (mobile-chrome)",
    );
    test.setTimeout(240_000);
    const { shows } = await publishShows(browser, "vou02", (suffix) => [
      { title: `Noite da Peste ${suffix}` },
      { title: `Sepulcro Fest ${suffix}`, cancelled: true },
    ]);
    const [scheduled, cancelled] = [shows[0]!, shows[1]!];

    const response = await page.goto(scheduled.path);
    expect(response?.status(), `GET ${scheduled.path}`).toBe(200);
    await press(
      page.getByRole("button", {
        name: `Vou ao show ${scheduled.title}`,
        exact: true,
      }),
    );
    await expect(page).toHaveURL(
      (url) =>
        url.pathname === "/entrar" &&
        url.searchParams.get("returnTo") === scheduled.path,
    );

    const cancelledPage = await page.goto(cancelled.path);
    expect(cancelledPage?.status(), `GET ${cancelled.path}`).toBe(200);
    await expect(
      page.getByRole("heading", { level: 1, name: cancelled.title }),
    ).toBeVisible();
    await expect(page.getByRole("button", { name: /vou ao show/i })).toHaveCount(
      0,
    );
    await expect(page.locator("main")).toContainText(attending(0));
  });
});
