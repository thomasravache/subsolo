import type { Locator, Page } from "@playwright/test";

import { expect, test } from "../fixtures";
import { appURL, externalApp, webWithoutApiURL } from "../agenda/environment";
import { oidcIssuer } from "./environment";
import {
  accountTab,
  fillInteractive,
  uniqueEmail,
  waitForHydration,
} from "./journey";

/**
 * SPEC-0020 — entrar com Google na aplicação completa (pilha do Compose com o provedor OpenID Connect falso,
 * mock-oauth2-server com login interativo, em `oidcIssuer`).
 *
 * - E2E-01 (mobile-chrome e mobile-safari): de `/cidade-exemplo`, "Conta" leva a `/entrar`, que mostra "Continuar com
 *   Google" acima do formulário, com o separador "ou" e alvo ≥ 44 px; o fã toca, entra no provedor falso com um e-mail
 *   verificado e volta para `/cidade-exemplo`; "Conta" passa a levar a `/conta`, que mostra o e-mail. Sem violações do
 *   axe em `/entrar`.
 * - E2E-02 (mobile-chrome): cancelando no provedor falso (o provedor responde ao `redirect_uri` com
 *   `error=access_denied`, como o Google quando o fã cancela), o fã volta para `/entrar` com "Não deu para entrar com o
 *   Google…" anunciado (`role="alert"`) e o formulário do link segue usável; um e-mail não verificado volta com "Sua
 *   conta Google não tem e-mail verificado…"; com o Google desligado (o web sem a API, que não confirma
 *   `providers.google`), o botão não aparece.
 */
// SPEC-0010: o Caddy do Compose serve HTTPS com o certificado local (`tls internal`), não confiável.
test.use({ baseURL: appURL, ignoreHTTPSErrors: true });

const googleError =
  "Não deu para entrar com o Google. Tente de novo ou use o link por e-mail.";
const googleEmailError =
  "Sua conta Google não tem e-mail verificado. Use o link por e-mail.";

/** "Continuar com Google" (link para a API ou botão). */
function googleButton(page: Page): Locator {
  return page
    .getByRole("link", { name: "Continuar com Google" })
    .or(page.getByRole("button", { name: "Continuar com Google" }));
}

/** Toca em "Continuar com Google" e espera a tela de login do provedor falso. */
async function goToProvider(page: Page): Promise<void> {
  const button = googleButton(page);
  await expect(button).toBeVisible();
  await waitForHydration(button);
  await button.click();
  await expect(page).toHaveURL(
    (url) => url.href.startsWith(`${oidcIssuer}/authorize`),
    { timeout: 15_000 },
  );
}

/** Entra no provedor falso com `email` (claims `email` e `email_verified`). */
async function loginAtProvider(
  page: Page,
  email: string,
  verified: boolean,
): Promise<void> {
  await page.locator('input[name="username"]').fill(`google-${email}`);
  await page
    .locator('textarea[name="claims"]')
    .fill(JSON.stringify({ email, email_verified: verified }));
  await page.getByRole("button", { name: "Sign-in" }).click();
}

test.describe("entrar com Google", () => {
  test("SPEC-0020:E2E-01 fã entra com o Google falso e volta para a cidade; Conta mostra o e-mail [jornada: entrar]", async ({
    page,
    axe,
  }, testInfo) => {
    test.skip(
      testInfo.project.name === "desktop-chrome",
      "jornada do celular (mobile-chrome e mobile-safari)",
    );
    test.skip(externalApp, "o provedor OIDC falso só existe na pilha local");
    test.setTimeout(120_000);

    // De onde o fã sai: a agenda da cidade; a aba "Conta" leva a /entrar.
    const agenda = await page.goto("/cidade-exemplo");
    expect(agenda?.status()).toBe(200);
    await accountTab(page).click();
    await expect(page).toHaveURL((url) => url.pathname === "/entrar");

    // O botão do Google acima do formulário, com o separador "ou" e alvo de toque ≥ 44 px.
    const button = googleButton(page);
    await expect(button).toBeVisible();
    await expect(button).toHaveText(/Continuar com Google/);
    await expect(page.getByText("ou", { exact: true })).toBeVisible();
    const buttonBox = await button.boundingBox();
    const fieldBox = await page.getByLabel("Seu e-mail").boundingBox();
    expect(buttonBox, "botão do Google sem caixa").not.toBeNull();
    expect(buttonBox?.height ?? 0).toBeGreaterThanOrEqual(44);
    expect(
      (buttonBox?.y ?? Number.POSITIVE_INFINITY) < (fieldBox?.y ?? 0),
      "o botão do Google fica acima do formulário",
    ).toBe(true);
    await axe.check(page);

    // Entra no provedor falso com um e-mail verificado e volta logado para a página de onde saiu.
    const email = uniqueEmail("e2e-google01");
    await goToProvider(page);
    await loginAtProvider(page, email, true);
    await expect(page).toHaveURL(
      (url) =>
        url.origin === new URL(appURL).origin &&
        url.pathname === "/cidade-exemplo",
      { timeout: 15_000 },
    );
    await page.waitForLoadState("load");

    await accountTab(page).click();
    await expect(page).toHaveURL((url) => url.pathname === "/conta");
    await expect(
      page.getByRole("heading", { level: 1, name: /minha conta/i }),
    ).toBeVisible();
    await expect(page.getByText(email)).toBeVisible();

    // Sessão no cookie de sempre; o cookie externo do retorno não fica.
    const cookies = await page.context().cookies(appURL);
    expect(cookies.map((cookie) => cookie.name)).toContain("__Host-subsolo");
    expect(cookies.map((cookie) => cookie.name)).not.toContain(
      "__Host-subsolo-ext",
    );
  });

  test("SPEC-0020:E2E-02 cancelar no provedor ou e-mail não verificado volta para /entrar com a mensagem certa [jornada: entrar]", async ({
    page,
    axe,
  }, testInfo) => {
    test.skip(
      testInfo.project.name !== "mobile-chrome",
      "estados de erro num projeto só (mobile-chrome)",
    );
    test.skip(externalApp, "o provedor OIDC falso só existe na pilha local");
    test.setTimeout(120_000);

    // Cancelou: o provedor responde ao redirect_uri com error=access_denied e o state do pedido.
    expect(
      (await page.goto("/entrar?returnTo=%2Fcidade-exemplo"))?.status(),
    ).toBe(200);
    await goToProvider(page);
    const authorize = new URL(page.url());
    const redirectUri = authorize.searchParams.get("redirect_uri") ?? "";
    const state = authorize.searchParams.get("state") ?? "";
    expect(redirectUri, "authorize sem redirect_uri").toMatch(
      /\/api\/v1\/auth\/google\/callback$/,
    );
    expect(state, "authorize sem state").not.toBe("");
    const cancel = new URL(redirectUri);
    cancel.searchParams.set("error", "access_denied");
    cancel.searchParams.set("state", state);
    await page.goto(cancel.href);

    await expect(page).toHaveURL(
      (url) =>
        url.pathname === "/entrar" && url.searchParams.get("erro") === "google",
      { timeout: 15_000 },
    );
    await expect(page.getByRole("alert")).toHaveText(googleError);
    await axe.check(page);

    // O formulário do link segue usável.
    const submit = page.getByRole("button", { name: "Mandar link de acesso" });
    await waitForHydration(submit);
    await fillInteractive(
      page.getByLabel("Seu e-mail"),
      uniqueEmail("e2e-google02"),
    );
    await submit.click();
    await expect(
      page.getByRole("heading", { name: /confira seu e-mail/i }),
    ).toBeVisible();

    // E-mail não verificado no provedor: volta com a mensagem própria e sem sessão.
    expect((await page.goto("/entrar"))?.status()).toBe(200);
    await goToProvider(page);
    await loginAtProvider(page, uniqueEmail("e2e-google02-nv"), false);
    await expect(page).toHaveURL(
      (url) =>
        url.pathname === "/entrar" &&
        url.searchParams.get("erro") === "google_email",
      { timeout: 15_000 },
    );
    await expect(page.getByRole("alert")).toHaveText(googleEmailError);
    await axe.check(page);
    const cookies = await page.context().cookies(appURL);
    expect(cookies.map((cookie) => cookie.name)).not.toContain(
      "__Host-subsolo",
    );
  });

  test("SPEC-0020:E2E-02 com o Google desligado o botão não aparece [jornada: entrar]", async ({
    page,
  }, testInfo) => {
    test.skip(
      testInfo.project.name !== "mobile-chrome",
      "estados de erro num projeto só (mobile-chrome)",
    );
    test.skip(externalApp, "o web sem API só existe na execução local");
    test.setTimeout(60_000);

    // O web de produção sem a API (SPEC-0008 IT-04): `providers.google` não é confirmado, então o Google fica desligado.
    const response = await page.goto(`${webWithoutApiURL}/entrar`);
    expect(response?.status()).toBe(200);
    await expect(page.getByLabel("Seu e-mail")).toBeVisible();
    await expect(
      page.getByRole("button", { name: "Mandar link de acesso" }),
    ).toBeVisible();
    await expect(googleButton(page)).toHaveCount(0);
    await expect(page.getByText("ou", { exact: true })).toHaveCount(0);
  });
});
