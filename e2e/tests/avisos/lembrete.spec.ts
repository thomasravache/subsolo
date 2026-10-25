import { expect, test } from "../fixtures";
import { appURL } from "../agenda/environment";
import { signIn, uniqueEmail } from "../auth/journey";
import {
  approvedProducer,
  localDate,
  mutate,
  press,
  uniqueSuffix,
  uploadFlyer,
} from "../shows/journey";
import { followingPath, preferences, texts } from "./avisos";
import {
  reminderCount,
  reminderSubject,
  untilSaoPauloMidnight,
  waitForReminder,
  waitPastSaoPauloMidnight,
} from "./lembrete";
import { linkPath } from "./resumo";

/**
 * SPEC-0032 E2E-01 — o lembrete na véspera por e-mail na aplicação completa (pilha do Compose, com
 * `Engagement:ReminderCron` a cada minuto, `Engagement:ReminderHour` = 0 e o Mailpit da pilha):
 * - um artista (produtor aprovado) publica um show para amanhã, às 22h, em "Curitiba <sufixo>";
 * - o fã logado abre a página do show e marca "Vou" (`aria-pressed="true"`), e liga "Lembrete na véspera (por e-mail)"
 *   na parte "Avisos" de `/seguindo`;
 * - em até 2 minutos chega ao Mailpit um e-mail só, "Amanhã: {atrações}", com "22h00 · {casa}, {cidade}" e o link do
 *   show (url "/{cidade}/{slug}" do aviso, absoluta pelo `App:PublicUrl`), que abre a página dele.
 */
/** O tempo limite da jornada (`test.setTimeout`); SPEC-0038:H36-05: também a margem antes da meia-noite. */
const journeyTimeout = 360_000;

// SPEC-0010: o Caddy do Compose serve HTTPS com o certificado local (`tls internal`), não confiável.
test.use({ baseURL: appURL, ignoreHTTPSErrors: true });

test.describe("avisos: lembrete na véspera por e-mail", () => {
  test("SPEC-0032:E2E-01 o fã marca vou num show de amanhã, liga o lembrete por e-mail e recebe o lembrete com o link do show [jornada: seguir-e-avisar]", async ({
    page,
    browser,
    axe,
  }, testInfo) => {
    test.skip(
      testInfo.project.name !== "mobile-chrome",
      "verificação em um projeto (mobile-chrome)",
    );
    test.setTimeout(journeyTimeout);

    // SPEC-0036:H32-05 — perto da meia-noite de São Paulo o show "de amanhã" viraria "de hoje" antes do lembrete: com
    // menos tempo para ela que a margem, espera passar (e o tempo do teste cresce na mesma medida).
    // SPEC-0038:H36-05 — a margem é o tempo limite da jornada (o mesmo do `test.setTimeout`), não 3 min fixos: a jornada
    // inteira cabe antes da virada do dia, ou começa depois dela.
    const margin = journeyTimeout;
    if (untilSaoPauloMidnight() < margin) {
      test.setTimeout(journeyTimeout + untilSaoPauloMidnight() + 10_000);
      await waitPastSaoPauloMidnight(margin);
    }

    // O artista e a cidade "Curitiba <sufixo>" (num contexto só dele).
    const artist = await browser.newContext({
      baseURL: appURL,
      ignoreHTTPSErrors: true,
    });
    try {
      const artistPage = await artist.newPage();
      const producer = await approvedProducer(artistPage, browser, "lembrete");
      const city = producer.city;

      // O artista publica um show para amanhã, às 22h.
      const suffix = uniqueSuffix();
      const title = `Noite da Véspera ${suffix.slice(-5)}`;
      const band = `Cova Rasa ${suffix.slice(-5)}`;
      const venue = "Porão do Rock";
      const flyer = await uploadFlyer(artistPage, title);
      const show = (await mutate(artistPage.request, "post", "/api/v1/shows", {
        organizerProfileId: producer.profileId,
        title,
        city: city.slug,
        startsLocal: `${localDate(1)}T22:00`,
        venue: { name: venue },
        lineup: [{ name: band }],
        styles: ["death"],
        price: "R$ 30",
        ticketUrl: null,
        flyerId: flyer.flyerId,
      })) as { id: string; slug: string };
      const showPath = `/${city.slug}/${show.slug}`;

      // O fã marca "Vou" na página do show.
      const fanEmail = uniqueEmail("lembrete01");
      await signIn(page, fanEmail, showPath);
      await expect(
        page.getByRole("heading", { level: 1, name: title }),
      ).toBeVisible();
      const attend = page.getByRole("button", { name: `Vou ao show ${title}` });
      await expect(attend).toHaveAttribute("aria-pressed", "false");
      await press(attend);
      await expect(attend).toHaveAttribute("aria-pressed", "true");

      // E liga o lembrete na véspera por e-mail.
      await page.goto(followingPath);
      const reminder = page
        .getByRole("region", { name: /^avisos$/i })
        .getByRole("switch", { name: texts.reminderEmail });
      await expect(reminder).toHaveAttribute("aria-checked", "false");
      await press(reminder);
      await expect(reminder).toHaveAttribute("aria-checked", "true");
      await expect
        .poll(async () => (await preferences(page.request)).reminderEmail)
        .toBe(true);

      // O lembrete chega em até 2 minutos, um só, com a hora, a casa e a cidade.
      const subject = reminderSubject([band]);
      const text = await waitForReminder(fanEmail, subject);
      expect(text).toContain(`22h00 · ${venue}, ${city.name}`);
      expect(
        await reminderCount(fanEmail, subject),
        "mais de um lembrete",
      ).toBe(1);

      // O link do e-mail abre a página do show.
      const escaped = showPath.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
      const linked = linkPath(
        text,
        new RegExp(`https?://[^\\s"<>/]+${escaped}(?=[\\s"<>]|$)`, "m"),
      );
      expect(linked).toBe(showPath);
      const opened = await page.goto(linked);
      expect(opened?.status(), `GET ${linked}`).toBe(200);
      await expect(
        page.getByRole("heading", { level: 1, name: title }),
      ).toBeVisible();
      await axe.check(page);
    } finally {
      await artist.close();
    }
  });
});
