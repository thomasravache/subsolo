import { expect, test } from "../fixtures";
import { appURL } from "../agenda/environment";
import { fillInteractive } from "../auth/journey";
import {
  announcements,
  approvedProducer,
  card,
  chooseCity,
  flyerJpeg,
  localDate,
  mutate,
  myShowsPath,
  newShowPath,
  pickStyle,
  press,
  uploadFlyer,
} from "./journey";

/**
 * SPEC-0023 — cadastro de show na aplicação completa (pilha do Compose; entrada pelo link mágico lido no Mailpit;
 * `admin@subsolo.test` é admin nesta pilha). Telas do Stitch "Cadastrar Show - Subsolo" e "Meus Shows - Produtor -
 * Subsolo", com os ajustes do §6 (sem o selo "produtor verificado"; a confirmação de cancelar diz que o show continua na
 * agenda com o selo).
 *
 * - E2E-01 (mobile-chrome e mobile-safari): o produtor aprovado vai de `/conta` a "Meus shows" (vazio: "Nenhum show
 *   cadastrado ainda."), toca em "+ Novo show", envia o flyer (prévia; o `thumb` é lido pelo Caddy — H22-06), preenche
 *   nome, data, hora, casa (nome livre), cidade, duas bandas ("Adicionar banda"), estilo, preço e link, e publica: vê
 *   "Show publicado." e o show em "Próximos"; a agenda da cidade mostra o card. Do toque em "+ Novo show" a "Show
 *   publicado." em menos de 2 minutos. Sem violações do axe nas telas.
 * - E2E-02 (mobile-chrome): com um show cadastrado, o produtor edita a hora e salva ("Alterações salvas."); adia e a
 *   agenda mostra "ADIADO"; abre "Cancelar show", o foco fica preso no diálogo e "Voltar" devolve o foco ao botão;
 *   confirma "Cancelar show" e a agenda mostra "CANCELADO" (o show continua lá).
 *
 * Seletores: papéis e rótulos; cada show de "Meus shows" e da agenda é um `article` com o nome; os selos têm texto (a
 * caixa alta pode ser só do CSS, por isso as expressões não diferenciam maiúsculas). Cada campo e botão só é usado
 * depois de hidratado.
 */
// SPEC-0010: o Caddy do Compose serve HTTPS com o certificado local (`tls internal`), não confiável.
test.use({ baseURL: appURL, ignoreHTTPSErrors: true });

const publishBudgetMs = 2 * 60_000;

test.describe("cadastro de show", () => {
  test("SPEC-0023:E2E-01 produtor cadastra o show com flyer e ele entra na agenda da cidade [jornada: cadastro-show]", async ({
    page,
    browser,
    axe,
  }, testInfo) => {
    test.skip(
      testInfo.project.name === "desktop-chrome",
      "jornada do celular (mobile-chrome e mobile-safari)",
    );
    test.setTimeout(300_000);
    const producer = await approvedProducer(page, browser, "show01");
    const title = `Noite da Peste ${producer.city.slug.slice(-6)}`;
    const venue = "Bar do Zé";

    // /conta → "Meus shows" (vazio).
    await page.goto("/conta");
    const myShowsLink = page.getByRole("link", { name: /meus shows/i }).first();
    await expect(myShowsLink, '/conta sem o link "Meus shows"').toBeVisible();
    await press(myShowsLink);
    await expect(page).toHaveURL((url) => url.pathname === myShowsPath);
    await expect(
      page.getByRole("heading", { level: 1, name: /meus shows/i }),
    ).toBeVisible();
    await expect(page.getByRole("tab", { name: /próximos/i })).toBeVisible();
    await expect(page.getByRole("tab", { name: /passados/i })).toBeVisible();
    await expect(page.getByText("Nenhum show cadastrado ainda.")).toBeVisible();
    await expect(page.getByText(/produtor verificado/i)).toHaveCount(0);
    await axe.check(page);

    // "+ Novo show": o tempo do cadastro começa aqui.
    const started = Date.now();
    await press(
      page
        .getByRole("link", { name: /novo show/i })
        .or(page.getByRole("button", { name: /novo show/i }))
        .first(),
    );
    await expect(page).toHaveURL((url) => url.pathname === newShowPath);
    await expect(
      page.getByRole("heading", { level: 1, name: /cadastrar show/i }),
    ).toBeVisible();
    const form = page.locator("main form");

    // Flyer: envio com prévia; o thumb sai do volume pelo Caddy (H22-06), com cache imutável.
    const flyerInput = form.getByLabel(/flyer/i);
    await expect(flyerInput).toHaveCount(1);
    const uploaded = page.waitForResponse(
      (response) =>
        response.url().endsWith("/api/v1/flyers") &&
        response.request().method() === "POST",
    );
    await flyerInput.setInputFiles({
      name: "flyer.jpg",
      mimeType: "image/jpeg",
      buffer: await flyerJpeg(page, title),
    });
    const flyerResponse = await uploaded;
    expect([200, 201]).toContain(flyerResponse.status());
    const flyer = (await flyerResponse.json()) as { thumbUrl: string };
    await expect(form.getByRole("img").first()).toBeVisible();
    const thumb = await page.request.get(flyer.thumbUrl);
    expect(thumb.status(), `GET ${flyer.thumbUrl} pelo Caddy`).toBe(200);
    expect(thumb.headers()["content-type"]).toBe("image/webp");
    expect(thumb.headers()["cache-control"] ?? "").toContain("immutable");

    // Os dados do show.
    await fillInteractive(
      form.getByLabel(/^nome( do (evento|show))?$/i),
      title,
    );
    await fillInteractive(form.getByLabel(/^data$/i), localDate(20));
    await fillInteractive(form.getByLabel(/^(hora|horário)$/i), "22:00");
    await fillInteractive(form.getByLabel(/casa/i).first(), venue);
    await page.keyboard.press("Escape");
    await chooseCity(form, producer.city);
    const bands = form.getByRole("textbox", { name: /banda/i });
    if ((await bands.count()) === 0) {
      await press(
        form.getByRole("button", { name: /adicionar (outra )?banda/i }),
      );
    }
    await fillInteractive(bands.nth(0), "Cova Rasa");
    await press(
      form.getByRole("button", { name: /adicionar (outra )?banda/i }),
    );
    await expect(bands).toHaveCount(2);
    await fillInteractive(bands.nth(1), "Necrocorpo");
    await pickStyle(form, /^death( metal)?$/i);
    await fillInteractive(
      form.getByLabel(/pre[çc]o/i),
      "R$ 30 / R$ 40 na hora",
    );
    await fillInteractive(
      form.getByLabel(/link/i),
      "https://ingressos.example/noite-da-peste",
    );
    await axe.check(page);

    // Publica: "Show publicado." em "Meus shows", aba "Próximos".
    await press(form.getByRole("button", { name: /publicar show/i }));
    await expect(
      announcements(page).filter({ hasText: "Show publicado." }),
    ).toBeVisible();
    const elapsed = Date.now() - started;
    expect(
      elapsed,
      `do toque em "+ Novo show" a "Show publicado." levou ${Math.round(elapsed / 1000)} s`,
    ).toBeLessThan(publishBudgetMs);
    await expect(page).toHaveURL((url) => url.pathname === myShowsPath);
    await expect(page.getByRole("tab", { name: /próximos/i })).toHaveAttribute(
      "aria-selected",
      "true",
    );
    const mine = card(page.locator("main"), title);
    await expect(mine).toHaveCount(1);
    await expect(mine).toContainText(venue);
    await axe.check(page);

    // A agenda da cidade mostra o card.
    const agenda = await page.goto(`/${producer.city.slug}`);
    expect(agenda?.status()).toBe(200);
    const listed = card(page.locator("main"), title);
    await expect(listed).toHaveCount(1);
    await expect(listed).toContainText(venue);
    await expect(listed).toContainText("Cova Rasa");
    await expect(listed).toContainText("22h");
  });

  test("SPEC-0023:E2E-02 produtor edita a hora, adia e cancela pelo diálogo [jornada: cadastro-show]", async ({
    page,
    browser,
    axe,
  }, testInfo) => {
    test.skip(
      testInfo.project.name !== "mobile-chrome",
      "edição e situação: mobile-chrome",
    );
    test.setTimeout(300_000);
    const producer = await approvedProducer(page, browser, "show02");
    const title = `Sepulcro Fest ${producer.city.slug.slice(-6)}`;
    const flyer = await uploadFlyer(page, title);
    await mutate(page.request, "post", "/api/v1/shows", {
      organizerProfileId: producer.profileId,
      title,
      city: producer.city.slug,
      startsLocal: `${localDate(15)}T22:00`,
      venue: { name: "Adega Subterrânea" },
      lineup: [{ name: "Carniça" }, { name: "Vômito Ácido" }],
      styles: ["death"],
      price: "R$ 35",
      ticketUrl: null,
      flyerId: flyer.flyerId,
    });
    const agendaCard = () => card(page.locator("main"), title);

    // Edita a hora.
    const opened = await page.goto(myShowsPath);
    expect(opened?.status()).toBe(200);
    await press(
      card(page.locator("main"), title).getByRole("link", { name: /editar/i }),
    );
    await expect(
      page.getByRole("heading", { level: 1, name: /editar show/i }),
    ).toBeVisible();
    const form = page.locator("main form");
    await expect(form.getByLabel(/^nome( do (evento|show))?$/i)).toHaveValue(
      title,
    );
    await fillInteractive(form.getByLabel(/^(hora|horário)$/i), "21:30");
    await axe.check(page);
    await press(form.getByRole("button", { name: /^salvar/i }));
    await expect(
      announcements(page).filter({ hasText: "Alterações salvas." }),
    ).toBeVisible();
    await expect(page).toHaveURL((url) => url.pathname === myShowsPath);
    await page.goto(`/${producer.city.slug}`);
    await expect(agendaCard()).toContainText("21h30");

    // Adia: "ADIADO" em "Meus shows" e na agenda.
    await page.goto(myShowsPath);
    await press(
      card(page.locator("main"), title).getByRole("button", {
        name: /^adiar$/i,
      }),
    );
    await expect(card(page.locator("main"), title)).toContainText(/adiado/i);
    await page.goto(`/${producer.city.slug}`);
    await expect(agendaCard()).toContainText(/adiado/i);

    // Cancelar: o diálogo prende o foco; "Voltar" devolve o foco ao botão.
    await page.goto(myShowsPath);
    const opener = card(page.locator("main"), title).getByRole("button", {
      name: /cancelar show/i,
    });
    await press(opener);
    const dialog = page.getByRole("dialog");
    await expect(dialog).toBeVisible();
    await expect(dialog).toContainText(
      "Cancelar este show? Ele continua na agenda com o selo CANCELADO.",
    );
    expect(
      await dialog.evaluate((element) =>
        element.contains(element.ownerDocument.activeElement),
      ),
      "o foco não foi para o diálogo",
    ).toBe(true);
    for (const key of [
      ...Array<string>(6).fill("Tab"),
      ...Array<string>(6).fill("Shift+Tab"),
    ]) {
      await page.keyboard.press(key);
      expect(
        await dialog.evaluate((element) =>
          element.contains(element.ownerDocument.activeElement),
        ),
        `o foco saiu do diálogo depois de ${key}`,
      ).toBe(true);
    }
    await axe.check(page);
    await dialog.getByRole("button", { name: "Voltar" }).click();
    await expect(dialog).toBeHidden();
    await expect(opener).toBeFocused();
    await expect(card(page.locator("main"), title)).not.toContainText(
      /cancelado/i,
    );

    // Confirma: "CANCELADO" e o show continua na agenda.
    await press(opener);
    await expect(dialog).toBeVisible();
    await dialog.getByRole("button", { name: "Cancelar show" }).click();
    await expect(dialog).toBeHidden();
    await expect(card(page.locator("main"), title)).toContainText(/cancelado/i);
    await page.goto(`/${producer.city.slug}`);
    await expect(agendaCard()).toHaveCount(1);
    await expect(agendaCard()).toContainText(/cancelado/i);
  });
});
