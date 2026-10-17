import { expect, test } from "../fixtures";
import { appURL, externalApp } from "../agenda/environment";
import {
  accountTab,
  confirmLink,
  messageCount,
  readMagicLink,
  requestLink,
  signIn,
  uniqueEmail,
  withMailpitStopped,
} from "./journey";

/**
 * SPEC-0016 — entrar por link mágico no e-mail, na aplicação completa (pilha do Compose com o Mailpit; o teste lê o
 * link na API HTTP do Mailpit por `MAILPIT_URL`).
 *
 * - E2E-01 (mobile-chrome e mobile-safari): de `/cidade-exemplo`, "Conta" leva a `/entrar`; o fã manda o e-mail, vê
 *   "Confira seu e-mail" com o contador de reenvio, abre o link (carregar a página não gasta o token), toca em "Entrar
 *   no Subsolo" e volta para `/cidade-exemplo`; "Conta" passa a levar a `/conta`, que mostra o e-mail; "Sair" volta a
 *   `/entrar`, e `/conta` passa a redirecionar para `/entrar?returnTo=/conta`. Sem violações do axe; do envio até
 *   "Confira seu e-mail" ≤ 3 s p95 em 5 repetições.
 * - E2E-02 (mobile-chrome): e-mail inválido ("Digite um e-mail válido." ligado ao campo), link reaproveitado ("Este
 *   link expirou ou já foi usado." com "Pedir outro link"), limite de IP ("Muitas tentativas…") e Mailpit parado
 *   ("Não conseguimos mandar o e-mail agora…"), sem violações do axe.
 * - E2E-03 (mobile-chrome e mobile-safari): a sessão vive só no cookie `__Host-subsolo` (HttpOnly), nada dela em
 *   `localStorage`/`sessionStorage`, `/conta` com `Cache-Control: no-store`; apagar cookies e dados do site desloga;
 *   depois de "Sair", voltar no navegador não mostra o e-mail.
 */
// SPEC-0010: o Caddy do Compose serve HTTPS com o certificado local (`tls internal`), não confiável.
test.use({ baseURL: appURL, ignoreHTTPSErrors: true });

const sendBudgetMs = 3_000;

/** Percentil pelo método do posto mais próximo (nearest-rank). */
function percentile(values: number[], p: number): number {
  const sorted = [...values].sort((a, b) => a - b);
  const rank = Math.ceil((p / 100) * sorted.length);
  return sorted[Math.max(0, rank - 1)] ?? Number.NaN;
}

/** `/entrar?returnTo=/conta`: o destino de quem abre `/conta` sem sessão. */
function isSignInBackToAccount(url: URL): boolean {
  return (
    url.pathname === "/entrar" && url.searchParams.get("returnTo") === "/conta"
  );
}

test.describe("entrar por link mágico", () => {
  test("SPEC-0016:E2E-01 fã entra pelo link do e-mail e volta para a cidade; Conta e Sair seguem a sessão [jornada: entrar]", async ({
    page,
    axe,
  }, testInfo) => {
    test.skip(
      testInfo.project.name === "desktop-chrome",
      "jornada do celular (mobile-chrome e mobile-safari)",
    );
    test.setTimeout(180_000);

    // De onde o fã sai: a agenda da cidade; a aba "Conta" leva a /entrar.
    const agenda = await page.goto("/cidade-exemplo");
    expect(agenda?.status()).toBe(200);
    await expect(accountTab(page)).toBeVisible();
    await accountTab(page).click();
    await expect(page).toHaveURL((url) => url.pathname === "/entrar");
    await expect(page).toHaveTitle("Entrar | Subsolo");
    await expect(
      page.getByRole("heading", { level: 1, name: /entrar/i }),
    ).toBeVisible();
    await expect(
      page.getByText("Sem senha: o link vale por 15 minutos."),
    ).toBeVisible();
    await axe.check(page);

    // Manda o e-mail e mede até "Confira seu e-mail".
    const email = uniqueEmail("e2e01");
    const durations: number[] = [];
    await page.getByLabel("Seu e-mail").fill(email);
    let start = Date.now();
    await page.getByRole("button", { name: "Mandar link de acesso" }).click();
    await expect(
      page.getByRole("heading", { name: /confira seu e-mail/i }),
    ).toBeVisible();
    durations.push(Date.now() - start);
    await expect(
      page.getByText(
        `Mandamos o link de acesso para ${email}. Ele vale por 15 minutos e só funciona uma vez.`,
      ),
    ).toBeVisible();
    await expect(
      page.getByText("Não chegou? Olhe a caixa de spam."),
    ).toBeVisible();
    const resend = page.getByRole("button", { name: /reenviar/i });
    await expect(resend).toBeDisabled();
    await expect(resend).toContainText(/Reenviar em 0:\d{2}/);
    await expect(
      page.locator('[aria-live="polite"]').filter({ hasText: /0:\d{2}/ }),
    ).not.toHaveCount(0);
    await expect(
      page
        .getByRole("button", { name: "Usar outro e-mail" })
        .or(page.getByRole("link", { name: "Usar outro e-mail" })),
    ).toBeVisible();
    await axe.check(page);

    // O link do e-mail: abrir a página (e recarregar) não consome o token.
    const linkPath = await readMagicLink(email);
    await page.goto(linkPath);
    await expect(
      page.getByRole("heading", { level: 1, name: /quase lá/i }),
    ).toBeVisible();
    await axe.check(page);
    await page.reload();
    await page.getByRole("button", { name: "Entrar no Subsolo" }).click();

    // Volta logado para a página de onde saiu.
    await expect(page).toHaveURL((url) => url.pathname === "/cidade-exemplo");
    await accountTab(page).click();
    await expect(page).toHaveURL((url) => url.pathname === "/conta");
    await expect(
      page.getByRole("heading", { level: 1, name: /minha conta/i }),
    ).toBeVisible();
    await expect(page.getByText(email)).toBeVisible();
    await axe.check(page);

    // Sair revoga a sessão e volta a /entrar; /conta passa a pedir a entrada.
    await page.getByRole("button", { name: "Sair" }).click();
    await expect(page).toHaveURL((url) => url.pathname === "/entrar");
    await page.goto("/conta");
    await expect(page).toHaveURL(isSignInBackToAccount);

    // Desempenho: do envio até "Confira seu e-mail", 5 repetições no total.
    for (let i = 1; i < 5; i++) {
      await page.goto("/entrar");
      await page.getByLabel("Seu e-mail").fill(uniqueEmail(`e2e01-p95-${i}`));
      start = Date.now();
      await page.getByRole("button", { name: "Mandar link de acesso" }).click();
      await expect(
        page.getByRole("heading", { name: /confira seu e-mail/i }),
      ).toBeVisible();
      durations.push(Date.now() - start);
    }
    expect(
      percentile(durations, 95),
      `envio → "Confira seu e-mail" (ms): ${durations.join(", ")}`,
    ).toBeLessThanOrEqual(sendBudgetMs);
  });

  test("SPEC-0016:E2E-02 e-mail inválido, link reaproveitado, limite e SMTP fora do ar mostram a mensagem certa [jornada: entrar]", async ({
    page,
    axe,
  }, testInfo) => {
    test.skip(
      testInfo.project.name !== "mobile-chrome",
      "estados de erro num projeto só (mobile-chrome)",
    );
    test.setTimeout(180_000);

    // E-mail inválido: a mensagem fica ligada ao campo.
    expect((await page.goto("/entrar"))?.status()).toBe(200);
    const field = page.getByLabel("Seu e-mail");
    await field.fill("sem-arroba");
    await page.getByRole("button", { name: "Mandar link de acesso" }).click();
    await expect(page.getByText("Digite um e-mail válido.")).toBeVisible();
    await expect(field).toHaveAccessibleDescription(
      /Digite um e-mail válido\./,
    );
    await axe.check(page);

    // Link reaproveitado: depois de usado, o mesmo link mostra o estado de link inválido.
    const email = uniqueEmail("e2e02");
    const linkPath = await signIn(page, email);
    await expect(page).toHaveURL((url) => url.pathname === "/");
    await page.context().clearCookies();
    await confirmLink(page, linkPath);
    await expect(
      page.getByText("Este link expirou ou já foi usado."),
    ).toBeVisible();
    const askAgain = page
      .getByRole("link", { name: "Pedir outro link" })
      .or(page.getByRole("button", { name: "Pedir outro link" }));
    await expect(askAgain).toBeVisible();
    await axe.check(page);
    await askAgain.click();
    await expect(page).toHaveURL((url) => url.pathname === "/entrar");

    // Limite de IP: a API responde 429 (o limite real é provado no IT-04; a pilha do E2E sobe com o limite alto).
    await page.route("**/api/v1/auth/magic-link", (route) =>
      route.request().method() === "POST"
        ? route.fulfill({
            status: 429,
            contentType: "application/problem+json",
            headers: { "Retry-After": "60" },
            body: JSON.stringify({
              type: "https://tools.ietf.org/html/rfc6585#section-4",
              title: "security.rate_limited",
              status: 429,
              code: "security.rate_limited",
            }),
          })
        : route.continue(),
    );
    await page.goto("/entrar");
    await page.getByLabel("Seu e-mail").fill(uniqueEmail("e2e02-limite"));
    await page.getByRole("button", { name: "Mandar link de acesso" }).click();
    await expect(
      page.getByText("Muitas tentativas. Espere um minuto e tente de novo."),
    ).toBeVisible();
    await axe.check(page);
    await page.unroute("**/api/v1/auth/magic-link");

    // SMTP fora do ar: o Mailpit da pilha local parado.
    test.skip(externalApp, "parar o Mailpit só na pilha local");
    const offline = uniqueEmail("e2e02-smtp");
    await withMailpitStopped(async () => {
      await page.goto("/entrar");
      await page.getByLabel("Seu e-mail").fill(offline);
      await page.getByRole("button", { name: "Mandar link de acesso" }).click();
      await expect(
        page.getByText(
          "Não conseguimos mandar o e-mail agora. Tente de novo em alguns minutos.",
        ),
      ).toBeVisible({ timeout: 20_000 });
      await axe.check(page);
    });
    expect(await messageCount(offline)).toBe(0);
  });

  test("SPEC-0016:E2E-03 a sessão vive só no cookie HttpOnly e apagar os cookies desloga [jornada: entrar]", async ({
    page,
  }, testInfo) => {
    test.skip(
      testInfo.project.name === "desktop-chrome",
      "jornada do celular (mobile-chrome e mobile-safari)",
    );
    test.setTimeout(120_000);

    const email = uniqueEmail("e2e03");
    const linkPath = await signIn(page, email, "/conta");
    const token = new URL(linkPath, appURL).searchParams.get("token") ?? "";
    await expect(page.getByText(email)).toBeVisible();

    // Cookie de sessão HttpOnly e Secure.
    const session = (await page.context().cookies()).find(
      (cookie) => cookie.name === "__Host-subsolo",
    );
    expect(
      session,
      "cookie __Host-subsolo ausente depois de entrar",
    ).toBeDefined();
    expect(session?.httpOnly).toBe(true);
    expect(session?.secure).toBe(true);

    // Nada da sessão no armazenamento do navegador.
    const stored = await page.evaluate(() => {
      const entries: string[] = [];
      for (const storage of [window.localStorage, window.sessionStorage]) {
        for (let i = 0; i < storage.length; i++) {
          const key = storage.key(i) ?? "";
          entries.push(`${key}=${storage.getItem(key) ?? ""}`);
        }
      }
      return entries;
    });
    for (const secret of [session?.value ?? "", token, email]) {
      expect(
        stored.filter((entry) => secret && entry.includes(secret)),
        "dado da sessão em localStorage/sessionStorage",
      ).toEqual([]);
    }

    // /conta sem cache.
    const account = await page.goto("/conta");
    expect(account?.status()).toBe(200);
    expect(account?.headers()["cache-control"] ?? "").toContain("no-store");

    // Apagar cookies e dados do site desloga de verdade.
    await page.context().clearCookies();
    await page.evaluate(() => {
      window.localStorage.clear();
      window.sessionStorage.clear();
    });
    await page.reload();
    await expect(page).toHaveURL(isSignInBackToAccount);
    await page.goto("/cidade-exemplo");
    await accountTab(page).click();
    await expect(page).toHaveURL((url) => url.pathname === "/entrar");
  });

  test("SPEC-0016:E2E-03 depois de Sair, voltar no navegador não mostra o e-mail [jornada: entrar]", async ({
    page,
  }, testInfo) => {
    test.skip(
      testInfo.project.name === "desktop-chrome",
      "jornada do celular (mobile-chrome e mobile-safari)",
    );
    test.setTimeout(120_000);

    const email = uniqueEmail("e2e03-voltar");
    await signIn(page, email, "/conta");
    await expect(page.getByText(email)).toBeVisible();

    await page.getByRole("button", { name: "Sair" }).click();
    await expect(page).toHaveURL((url) => url.pathname === "/entrar");
    await page.goBack();
    await page.waitForLoadState("load");

    await expect(page.getByText(email)).toHaveCount(0);
  });
});
