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
import { dayMonth, digestCount, linkPath, waitForDigest } from "./resumo";

/**
 * SPEC-0031 E2E-01 — o resumo diário por e-mail na aplicação completa (pilha do Compose, com `Engagement:DigestCron` a
 * cada minuto e o Mailpit da pilha):
 * - o fã logado segue "Curitiba <sufixo>" (botão "Seguir {cidade}" da agenda) e liga "Resumo diário por e-mail" na parte
 *   "Avisos" de `/seguindo`;
 * - um artista (produtor aprovado) publica um show nessa cidade;
 * - em até 2 minutos chega ao Mailpit um e-mail só, "Shows novos de quem você segue", com a linha
 *   "{dd/MM} · {atrações} · {casa}, {cidade}" do show;
 * - o link do show (`/{cidade}/{show}`) abre a página dele, e o link de descadastro confirma "Pronto: você não recebe
 *   mais o resumo diário por e-mail." e desliga o resumo.
 */
// SPEC-0010: o Caddy do Compose serve HTTPS com o certificado local (`tls internal`), não confiável.
test.use({ baseURL: appURL, ignoreHTTPSErrors: true });

test.describe("avisos: resumo diário por e-mail", () => {
  test("SPEC-0031:E2E-01 o fã segue a cidade, liga o resumo diário e recebe por e-mail o show novo publicado [jornada: seguir-e-avisar]", async ({
    page,
    browser,
    axe,
  }, testInfo) => {
    test.skip(
      testInfo.project.name !== "mobile-chrome",
      "verificação em um projeto (mobile-chrome)",
    );
    test.setTimeout(360_000);

    // O artista e a cidade "Curitiba <sufixo>" (num contexto só dele).
    const artist = await browser.newContext({
      baseURL: appURL,
      ignoreHTTPSErrors: true,
    });
    try {
      const artistPage = await artist.newPage();
      const producer = await approvedProducer(artistPage, browser, "resumo");
      const city = producer.city;

      // O fã segue a cidade e liga o resumo diário.
      const fanEmail = uniqueEmail("resumo01");
      await signIn(page, fanEmail, `/${city.slug}`);
      const follow = page.getByRole("button", { name: `Seguir ${city.name}` });
      await press(follow);
      await expect(follow).toHaveAttribute("aria-pressed", "true");

      await page.goto(followingPath);
      const digest = page
        .getByRole("region", { name: /^avisos$/i })
        .getByRole("switch", { name: texts.digest });
      await expect(digest).toHaveAttribute("aria-checked", "false");
      await press(digest);
      await expect(digest).toHaveAttribute("aria-checked", "true");
      await expect
        .poll(async () => (await preferences(page.request)).dailyDigestEmail)
        .toBe(true);

      // O artista publica um show na cidade.
      const suffix = uniqueSuffix();
      const title = `Noite do Resumo ${suffix.slice(-5)}`;
      const band = `Cova Rasa ${suffix.slice(-5)}`;
      const venue = "Porão do Rock";
      const day = localDate(5);
      const flyer = await uploadFlyer(artistPage, title);
      const show = (await mutate(artistPage.request, "post", "/api/v1/shows", {
        organizerProfileId: producer.profileId,
        title,
        city: city.slug,
        startsLocal: `${day}T22:00`,
        venue: { name: venue },
        lineup: [{ name: band }],
        styles: ["death"],
        price: "R$ 30",
        ticketUrl: null,
        flyerId: flyer.flyerId,
      })) as { id: string; slug: string };

      // O resumo chega em até 2 minutos, um só, com o show.
      const text = await waitForDigest(
        fanEmail,
        `${dayMonth(day)} · ${band} · ${venue}, ${city.name}`,
      );
      expect(await digestCount(fanEmail), "mais de um resumo no dia").toBe(1);

      // O link do show abre a página dele.
      const showPath = linkPath(
        text,
        new RegExp(
          `https?://[^\\s"<>]+/${city.slug}/${show.slug}(?=[\\s"<>]|$)`,
        ),
      );
      const opened = await page.goto(showPath);
      expect(opened?.status(), `GET ${showPath}`).toBe(200);
      await expect(page.getByRole("heading", { name: title })).toBeVisible();
      await axe.check(page);

      // O link de descadastro confirma e desliga o resumo.
      const unsubscribePath = linkPath(
        text,
        /https?:\/\/[^\s"<>]+\/avisos\/descadastrar\?token=[^\s"<>]+/,
      );
      const unsubscribed = await page.goto(unsubscribePath);
      expect(unsubscribed?.status(), `GET ${unsubscribePath}`).toBe(200);
      await expect(
        page.getByText(texts.unsubscribedDigest, { exact: true }),
      ).toBeVisible();
      await expect
        .poll(async () => (await preferences(page.request)).dailyDigestEmail)
        .toBe(false);
      await axe.check(page);
    } finally {
      await artist.close();
    }
  });
});
