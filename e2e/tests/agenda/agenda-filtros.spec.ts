import { expect, test } from "../fixtures";
import { appURL } from "./environment";
import { dayMonth, daysUntilSunday, publishAgenda } from "./filters";
import { announcements, card, localDate, press } from "../shows/journey";

/**
 * SPEC-0025 — a agenda da cidade com filtros por período e estilo (`/{cidade}?periodo=&de=&ate=&estilo=`) na aplicação
 * completa (pilha do Compose). Telas do Stitch "Agenda Curitiba - Subsolo" (celular) e "Agenda Curitiba - Desktop", com
 * os ajustes do §6: "PRÓXIMOS 90 DIAS", sem a contagem "X vão" nos cards. O fã é a página do teste, anônima; os shows
 * são cadastrados pela API por um produtor aprovado, numa cidade só da execução.
 *
 * - E2E-01 (mobile-chrome e mobile-safari): o fã abre a agenda, toca em "Esta semana" e depois em "DEATH"; a URL muda,
 *   só os shows certos aparecem e o total do cabeçalho bate (anunciado em `aria-live`); os chips selecionados têm
 *   `aria-current="page"`; cabeçalho "{de} A {ate} · N SHOWS" em dd/MM, singular "1 SHOW"; a mesma URL aberta numa aba nova já vem filtrada do servidor, com os chips marcados. Sem
 *   violações do axe.
 * - E2E-02 (mobile-chrome): parâmetros inválidos (`ate < de`, 181 dias) mostram a agenda padrão sem erro; "Escolher
 *   data" (formulário GET com "De", "Até" e "Aplicar") com um intervalo sem shows mostra "Nenhum show com esses filtros." e "Limpar filtros" volta à agenda padrão;
 *   o bloco "PRODUZINDO SHOW EM {CIDADE}?" leva o anônimo a `/entrar?returnTo=/conta/shows/novo` e o artista a
 *   `/conta/shows/novo`.
 */
test.use({ baseURL: appURL, ignoreHTTPSErrors: true });

test.describe("filtros da agenda", () => {
  test("SPEC-0025:E2E-01 fã filtra a agenda por esta semana e por death, e a URL filtrada abre já filtrada [jornada: agenda]", async ({
    page,
    browser,
    axe,
  }, testInfo) => {
    test.skip(
      testInfo.project.name === "desktop-chrome",
      "jornada do celular (mobile-chrome e mobile-safari)",
    );
    test.setTimeout(300_000);
    const sunday = daysUntilSunday();
    const setup = await publishAgenda(browser, "agenda01", (suffix) => [
      {
        title: `Ritual Death ${suffix}`,
        day: sunday,
        time: "23:30",
        styles: ["death"],
      },
      {
        title: `Grind na Semana ${suffix}`,
        day: sunday,
        time: "23:00",
        styles: ["grind"],
      },
      {
        title: `Death Depois ${suffix}`,
        day: sunday + 1,
        time: "22:00",
        styles: ["death"],
      },
    ]);
    try {
      const [inWeek, grindInWeek, afterWeek] = setup.shows;
      if (!inWeek || !grindInWeek || !afterWeek) {
        throw new Error("preparação sem os três shows");
      }
      const citySlug = setup.producer.city.slug;
      const main = page.getByRole("main");

      const opened = await page.goto(`/${citySlug}`);
      expect(opened?.status()).toBe(200);
      await expect(main).toContainText(/PRÓXIMOS 90 DIAS\s*·\s*3 SHOWS/i);
      for (const show of setup.shows) {
        await expect(card(main, show.title)).toBeVisible();
      }

      // "Esta semana": de hoje até domingo.
      await press(page.getByRole("link", { name: /^esta semana$/i }));
      await expect(page).toHaveURL(
        (url) => url.searchParams.get("periodo") === "semana",
      );
      await expect(card(main, inWeek.title)).toBeVisible();
      await expect(card(main, grindInWeek.title)).toBeVisible();
      await expect(card(main, afterWeek.title)).toHaveCount(0);
      const weekHeader = `${dayMonth(0)} A ${dayMonth(sunday)}`;
      await expect(main).toContainText(
        new RegExp(`${weekHeader}\\s*·\\s*2 SHOWS`, "i"),
      );
      await expect(
        page.getByRole("link", { name: /^esta semana$/i }),
      ).toHaveAttribute("aria-current", "page");

      // "DEATH" junto com a semana.
      await press(page.getByRole("link", { name: /^death$/i }));
      await expect(page).toHaveURL(
        (url) =>
          url.searchParams.get("periodo") === "semana" &&
          url.searchParams.get("estilo") === "death",
      );
      await expect(card(main, inWeek.title)).toBeVisible();
      await expect(card(main, grindInWeek.title)).toHaveCount(0);
      await expect(card(main, afterWeek.title)).toHaveCount(0);
      await expect(main).toContainText(
        new RegExp(`${weekHeader}\\s*·\\s*1 SHOW\\b`, "i"),
      );
      await expect(
        announcements(page)
          .filter({ hasText: /\b1 SHOW\b/i })
          .first(),
        "o total não é anunciado em aria-live",
      ).toBeAttached();
      await expect(
        page.getByRole("link", { name: /^death$/i }),
      ).toHaveAttribute("aria-current", "page");
      await expect(
        page.getByRole("link", { name: /^esta semana$/i }),
      ).toHaveAttribute("aria-current", "page");
      await expect(
        page.getByRole("link", { name: /^grind$/i }),
      ).not.toHaveAttribute("aria-current", "page");
      await axe.check(page);

      // A URL filtrada é compartilhável: o servidor já devolve a agenda filtrada.
      const filteredUrl = page.url();
      const html = await (await page.request.get(filteredUrl)).text();
      expect(html, "o HTML do servidor sem o show filtrado").toContain(
        inWeek.title,
      );
      expect(html, "o HTML do servidor com show fora do filtro").not.toContain(
        grindInWeek.title,
      );

      const other = await page.context().newPage();
      try {
        const reopened = await other.goto(filteredUrl);
        expect(reopened?.status()).toBe(200);
        const otherMain = other.getByRole("main");
        await expect(card(otherMain, inWeek.title)).toBeVisible();
        await expect(card(otherMain, grindInWeek.title)).toHaveCount(0);
        await expect(card(otherMain, afterWeek.title)).toHaveCount(0);
        await expect(otherMain).toContainText(
          new RegExp(`${weekHeader}\\s*·\\s*1 SHOW\\b`, "i"),
        );
        await expect(
          other.getByRole("link", { name: /^death$/i }),
        ).toHaveAttribute("aria-current", "page");
        await expect(
          other.getByRole("link", { name: /^esta semana$/i }),
        ).toHaveAttribute("aria-current", "page");
        await axe.check(other);
      } finally {
        await other.close();
      }
    } finally {
      await setup.close();
    }
  });

  test("SPEC-0025:E2E-02 intervalo sem shows mostra o vazio com filtro, Limpar filtros volta à agenda padrão e o CTA leva o produtor ao cadastro [jornada: agenda]", async ({
    page,
    browser,
  }, testInfo) => {
    test.skip(
      testInfo.project.name !== "mobile-chrome",
      "estados e CTA: mobile-chrome",
    );
    test.setTimeout(300_000);
    const setup = await publishAgenda(browser, "agenda02", (suffix) => [
      {
        title: `Noite da Peste ${suffix}`,
        day: 3,
        time: "22:00",
        styles: ["death"],
      },
    ]);
    try {
      const [show] = setup.shows;
      if (!show) {
        throw new Error("preparação sem o show");
      }
      const { slug: citySlug, name: cityName } = setup.producer.city;
      const main = page.getByRole("main");

      // Parâmetros inválidos: a página ignora e mostra a agenda padrão, sem erro.
      for (const query of [
        `de=${localDate(10)}&ate=${localDate(5)}`,
        `de=${localDate(0)}&ate=${localDate(181)}`,
        "periodo=foo&de=amanha",
      ]) {
        const invalid = await page.goto(`/${citySlug}?${query}`);
        expect(invalid?.status(), `?${query}`).toBe(200);
        await expect(main, `?${query}`).toContainText(/próximos 90 dias/i);
        await expect(card(main, show.title), `?${query}`).toBeVisible();
      }

      // "Escolher data" com um intervalo sem shows.
      await page.goto(`/${citySlug}`);
      await press(
        page
          .getByRole("button", { name: /^escolher data$/i })
          .or(page.getByRole("link", { name: /^escolher data$/i }))
          .first(),
      );
      const from = localDate(150);
      const to = localDate(160);
      await page.getByLabel(/^de$/i).fill(from);
      const until = page.getByLabel(/^até$/i);
      await until.fill(to);
      await press(page.getByRole("button", { name: "Aplicar" }));
      await expect(page).toHaveURL(
        (url) =>
          url.searchParams.get("de") === from &&
          url.searchParams.get("ate") === to,
      );
      await expect(
        page.getByText("Nenhum show com esses filtros."),
      ).toBeVisible();
      await expect(card(main, show.title)).toHaveCount(0);

      await press(page.getByRole("link", { name: "Limpar filtros" }));
      await expect(page).toHaveURL(
        (url) => url.pathname === `/${citySlug}` && url.search === "",
      );
      await expect(card(main, show.title)).toBeVisible();
      await expect(main).toContainText(/PRÓXIMOS 90 DIAS\s*·\s*1 SHOW\b/i);

      // CTA do produtor: anônimo vai para a entrada, com a volta para o cadastro.
      await expect(page.getByRole("main")).toContainText(
        new RegExp(`produzindo show em ${cityName}\\?`, "i"),
      );
      await press(
        page.getByRole("link", { name: "Cadastrar evento na agenda" }),
      );
      await expect(page).toHaveURL(
        (url) =>
          url.pathname === "/entrar" &&
          url.searchParams.get("returnTo") === "/conta/shows/novo",
      );

      // Artista: o mesmo CTA leva direto ao cadastro.
      const artist = setup.producerPage;
      await artist.goto(`/${citySlug}`);
      await press(
        artist.getByRole("link", { name: "Cadastrar evento na agenda" }),
      );
      await expect(artist).toHaveURL(
        (url) => url.pathname === "/conta/shows/novo",
      );
    } finally {
      await setup.close();
    }
  });
});
