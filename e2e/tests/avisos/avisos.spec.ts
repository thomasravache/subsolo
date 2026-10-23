import { expect, test } from "../fixtures";
import { appURL } from "../agenda/environment";
import { signIn, uniqueEmail } from "../auth/journey";
import { announcements, mutate, press } from "../shows/journey";
import {
  denyNotifications,
  fakePushService,
  followingPath,
  myId,
  preferences,
  texts,
  unsubscribePath,
  unsubscribeToken,
} from "./avisos";

/**
 * SPEC-0030 — avisos na aplicação completa (pilha do Compose): parte "Avisos" da tela "Avisos e Seguindo" do Stitch
 * (em `/seguindo`), o PWA (manifest e service worker) e a página de descadastro.
 *
 * - E2E-01 (mobile-chrome, permissão de notificação concedida pelo Playwright): o fã logado abre "Avisos", toca em
 *   "Ativar avisos", vê "Avisos ativos neste aparelho" (o show novo liga, a API conta o aparelho), liga "Resumo diário
 *   por e-mail" e recarrega: as escolhas continuam. `/manifest.webmanifest` responde (`display: standalone`, nome e
 *   ícones) e o service worker `/sw.js` registra. Sem violações do axe.
 * - E2E-02 (mobile-chrome, permissão negada): "Ativar avisos" mostra "Os avisos estão bloqueados no navegador." e nada é
 *   gravado; um link de descadastro adulterado mostra "Este link de descadastro é inválido."; os válidos (gerados pelo
 *   teste) confirmam "Pronto: você não recebe mais o resumo diário por e-mail." e "… o lembrete na véspera por e-mail."
 *   (o tipo vem do token, Emenda de 2026-10-23), e cada um desliga só o seu tipo.
 *
 * O `pushManager.subscribe` é trocado por uma inscrição de teste (ver `avisos.ts`): o Chromium do Playwright não alcança
 * o FCM. Seletores: papéis e nomes acessíveis; cada controle só é tocado depois de a página hidratar.
 */
// SPEC-0010: o Caddy do Compose serve HTTPS com o certificado local (`tls internal`), não confiável.
test.use({ baseURL: appURL, ignoreHTTPSErrors: true });

test.describe("avisos: push, preferências e descadastro", () => {
  test("SPEC-0030:E2E-01 o fã ativa os avisos, liga o resumo diário e as escolhas continuam depois de recarregar [jornada: seguir-e-avisar]", async ({
    page,
    context,
    axe,
  }, testInfo) => {
    test.skip(
      testInfo.project.name !== "mobile-chrome",
      "push no Chromium do celular (mobile-chrome)",
    );
    test.setTimeout(240_000);
    await context.grantPermissions(["notifications"], { origin: appURL });
    await fakePushService(page);
    await signIn(page, uniqueEmail("avisos01"), followingPath);

    // PWA: manifest e service worker.
    const manifest = await page.request.get("/manifest.webmanifest");
    expect(manifest.status(), "GET /manifest.webmanifest").toBe(200);
    const app = (await manifest.json()) as {
      name?: string;
      display?: string;
      icons?: unknown[];
    };
    expect(app.name ?? "").toContain("Subsolo");
    expect(app.display).toBe("standalone");
    expect(app.icons ?? [], "manifest sem ícones").not.toHaveLength(0);
    await expect(page.locator('link[rel="manifest"]')).toHaveCount(1);

    // Avisos: antes do toque, nada ativo.
    const notices = page.getByRole("region", { name: /^avisos$/i });
    await expect(notices).toBeVisible();
    await press(notices.getByRole("button", { name: texts.activate }));
    await expect(
      announcements(page).filter({ hasText: texts.active }),
      "a situação não foi anunciada",
    ).not.toHaveCount(0);
    await expect
      .poll(
        () =>
          page.evaluate(async () => {
            const registration =
              await navigator.serviceWorker.getRegistration();
            return registration?.active?.scriptURL ?? "";
          }),
        { message: "o service worker /sw.js não registrou", timeout: 15_000 },
      )
      .toMatch(/\/sw\.js$/);
    await expect
      .poll(async () => (await preferences(page.request)).pushDevices, {
        message: "a inscrição não chegou à API",
      })
      .toBe(1);
    await expect(
      notices.getByRole("switch", { name: texts.newShow }),
    ).toHaveAttribute("aria-checked", "true");
    await axe.check(page);

    // Liga o resumo diário e recarrega.
    const digest = notices.getByRole("switch", { name: texts.digest });
    await expect(digest).toHaveAttribute("aria-checked", "false");
    await press(digest);
    await expect(digest).toHaveAttribute("aria-checked", "true");
    await expect
      .poll(async () => (await preferences(page.request)).dailyDigestEmail)
      .toBe(true);

    await page.reload();
    await expect(
      page
        .getByRole("region", { name: /^avisos$/i })
        .getByRole("switch", { name: texts.digest }),
    ).toHaveAttribute("aria-checked", "true");
    await expect(
      page
        .getByRole("region", { name: /^avisos$/i })
        .getByRole("switch", { name: texts.newShow }),
    ).toHaveAttribute("aria-checked", "true");
    await axe.check(page);
  });

  test("SPEC-0030:E2E-02 com a permissão negada os avisos aparecem bloqueados e o link de descadastro desliga o resumo diário [jornada: seguir-e-avisar]", async ({
    page,
    axe,
  }, testInfo) => {
    test.skip(
      testInfo.project.name !== "mobile-chrome",
      "verificação em um projeto (mobile-chrome)",
    );
    test.setTimeout(240_000);
    await denyNotifications(page);
    await fakePushService(page);
    await signIn(page, uniqueEmail("avisos02"), followingPath);

    const notices = page.getByRole("region", { name: /^avisos$/i });
    await press(notices.getByRole("button", { name: texts.activate }));
    await expect(
      announcements(page).filter({ hasText: texts.blocked }),
      "o bloqueio não foi anunciado",
    ).not.toHaveCount(0);
    expect((await preferences(page.request)).pushDevices).toBe(0);
    await axe.check(page);

    // Descadastro: liga o resumo diário pela API e abre o link gerado pelo teste.
    await mutate(page.request, "put", "/api/v1/me/notification-preferences", {
      newShowPush: false,
      dailyDigestEmail: true,
      reminderPush: true,
      reminderEmail: true,
    });
    const token = unsubscribeToken(await myId(page.request), "digest");

    const tampered = `${unsubscribePath}?token=${encodeURIComponent(`${token.slice(0, -10)}${token.at(-10) === "A" ? "B" : "A"}${token.slice(-9)}`)}`;
    const invalid = await page.goto(tampered);
    expect(invalid?.status(), `GET ${unsubscribePath} (adulterado)`).toBe(200);
    await expect(
      page.getByText(texts.invalidLink, { exact: true }),
    ).toBeVisible();
    expect((await preferences(page.request)).dailyDigestEmail).toBe(true);

    const link = `${unsubscribePath}?token=${encodeURIComponent(token)}`;
    const response = await page.goto(link);
    expect(response?.status(), `GET ${unsubscribePath}`).toBe(200);
    await expect(
      page.getByText(texts.unsubscribedDigest, { exact: true }),
    ).toBeVisible();
    await expect
      .poll(async () => (await preferences(page.request)).dailyDigestEmail)
      .toBe(false);
    expect((await preferences(page.request)).reminderEmail).toBe(true);
    await axe.check(page);

    // Lembrete por e-mail: o tipo vem do próprio token.
    const reminder = `${unsubscribePath}?token=${encodeURIComponent(unsubscribeToken(await myId(page.request), "reminder"))}`;
    const reminderResponse = await page.goto(reminder);
    expect(
      reminderResponse?.status(),
      `GET ${unsubscribePath} (lembrete)`,
    ).toBe(200);
    await expect(
      page.getByText(texts.unsubscribedReminder, { exact: true }),
    ).toBeVisible();
    await expect
      .poll(async () => (await preferences(page.request)).reminderEmail)
      .toBe(false);
  });
});
