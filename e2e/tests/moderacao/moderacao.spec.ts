import type { Locator, Page } from "@playwright/test";

import { expect, test } from "../fixtures";
import { appURL } from "../agenda/environment";
import { signIn, uniqueEmail } from "../auth/journey";
import { adminEmail, announcements, card, press } from "../shows/journey";
import { publishShows } from "../shows/show-page";

/**
 * SPEC-0033 — denúncia de show e fila de moderação na aplicação completa (pilha do Compose). Telas do Stitch "Denunciar
 * Show - Subsolo" (folha "Reportar show") e "Fila do Admin - Subsolo" (aba "Denúncias", com os ajustes do §6: um cartão
 * por show, "Ocultar show" com confirmação, sem quem denunciou).
 *
 * - E2E-01 (mobile-chrome): o fã logado abre um show, toca em "Reportar", escolhe "Cancelado ou adiado e não avisaram",
 *   envia e vê "Obrigado. A moderação vai olhar."; tenta de novo e vê "Você já denunciou este show."; o admin abre
 *   `/admin/fila`, aba "Denúncias", vê o show com "1 denúncia", toca em "Ocultar show" e confirma; a página do show passa
 *   a mostrar "Show não encontrado" (404) e o show some da agenda. Sem violações do axe na folha e na fila.
 *
 * Seletores: papéis e nomes acessíveis; os textos podem estar em caixa alta só pelo CSS (expressões sem diferenciar
 * maiúsculas). Cada controle só é tocado depois de a página hidratar.
 */
// SPEC-0010: o Caddy do Compose serve HTTPS com o certificado local (`tls internal`), não confiável.
test.use({ baseURL: appURL, ignoreHTTPSErrors: true });

const queuePath = "/admin/fila";

/** Prazo para a outbox do Moderation e a inbox do Events tirarem o show oculto das leituras públicas. */
const hideTimeout = 30_000;

/** O link (ou botão) "Reportar" da página do show. */
function reportTrigger(page: Page): Locator {
  return page
    .getByRole("link", { name: /^reportar/i })
    .or(page.getByRole("button", { name: /^reportar/i }));
}

/** Abre a folha "Reportar show", escolhe o motivo e envia; devolve a folha. */
async function report(page: Page, reason: RegExp): Promise<Locator> {
  await press(reportTrigger(page).first());
  const sheet = page.getByRole("dialog", { name: /reportar show/i });
  await expect(sheet).toBeVisible();
  await sheet.getByRole("radio", { name: reason }).check();
  await press(sheet.getByRole("button", { name: /enviar denúncia/i }));
  return sheet;
}

test.describe("denúncia de show e fila de moderação", () => {
  test("SPEC-0033:E2E-01 o fã denuncia o show, o admin oculta pela fila e o show some da página e da agenda [jornada: moderacao]", async ({
    page,
    browser,
    axe,
  }, testInfo) => {
    test.skip(
      testInfo.project.name !== "mobile-chrome",
      "jornada do celular (mobile-chrome)",
    );
    test.setTimeout(300_000);
    const { producer, shows } = await publishShows(
      browser,
      "mod01",
      (suffix) => [{ title: `Noite Duvidosa ${suffix}` }],
    );
    const show = shows[0]!;
    const agendaPath = `/${producer.city.slug}`;

    // O fã denuncia.
    await signIn(page, uniqueEmail("mod01"), show.path);
    await expect(
      page.getByRole("heading", { level: 1, name: show.title }),
    ).toBeVisible();
    await press(reportTrigger(page).first());
    const sheet = page.getByRole("dialog", { name: /reportar show/i });
    await expect(sheet).toBeVisible();
    await expect(sheet).toContainText(
      "Ajude a manter a agenda do Subsolo correta.",
    );
    for (const label of [
      /^show falso/i,
      /^cancelado ou adiado e não avisaram/i,
      /^informação errada/i,
      /^outro/i,
    ]) {
      await expect(sheet.getByRole("radio", { name: label })).toBeVisible();
    }
    await expect(
      sheet.getByRole("textbox", { name: /conte mais \(opcional\)/i }),
    ).toBeVisible();
    await axe.check(page);
    await sheet
      .getByRole("radio", { name: /^cancelado ou adiado e não avisaram/i })
      .check();
    await press(sheet.getByRole("button", { name: /enviar denúncia/i }));
    await expect(
      announcements(page).filter({
        hasText: "Obrigado. A moderação vai olhar.",
      }),
    ).not.toHaveCount(0);

    // De novo: já denunciou.
    await page.reload();
    await report(page, /^cancelado ou adiado e não avisaram/i);
    await expect(
      announcements(page).filter({ hasText: "Você já denunciou este show." }),
    ).not.toHaveCount(0);

    // O admin oculta pela fila.
    const adminContext = await browser.newContext({
      baseURL: appURL,
      ignoreHTTPSErrors: true,
    });
    try {
      const admin = await adminContext.newPage();
      await signIn(admin, adminEmail, queuePath);
      await expect(admin).toHaveURL((url) => url.pathname === queuePath);
      const tab = admin.getByRole("tab", { name: /denúncias/i });
      await expect(tab).toHaveText(/\(\d+\)/);
      await press(tab);
      await expect(tab).toHaveAttribute("aria-selected", "true");
      const reported = card(admin, show.title);
      await expect(reported).toBeVisible();
      await expect(reported).toContainText(/(^|\D)1 denúncia(?!s)/i);
      await expect(reported).not.toContainText(/@/);
      await axe.check(admin);

      await press(reported.getByRole("button", { name: /ocultar show/i }));
      const confirm = admin
        .getByRole("dialog")
        .getByRole("button", { name: /ocultar|confirmar/i })
        .or(reported.getByRole("button", { name: /confirmar/i }));
      await press(confirm.first());
      await expect(card(admin, show.title)).toHaveCount(0);
    } finally {
      await adminContext.close();
    }

    // A página do show vira "não encontrado" e o show some da agenda.
    await expect
      .poll(
        async () => (await page.goto(show.path))?.status(),
        {
          message: `GET ${show.path} depois de ocultar`,
          timeout: hideTimeout,
        },
      )
      .toBe(404);
    await expect(
      page.getByRole("heading", { level: 1, name: "Show não encontrado" }),
    ).toBeVisible();
    const agenda = await page.goto(agendaPath);
    expect(agenda?.status(), `GET ${agendaPath}`).toBe(200);
    await expect(card(page, show.title)).toHaveCount(0);
  });
});
