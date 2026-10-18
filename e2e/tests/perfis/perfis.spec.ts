import { randomUUID } from "node:crypto";

import type { Browser, Locator, Page } from "@playwright/test";

import { expect, test } from "../fixtures";
import { appURL } from "../agenda/environment";
import {
  fillInteractive,
  signIn,
  uniqueEmail,
  waitForHydration,
} from "../auth/journey";

/**
 * SPEC-0021 — perfis na aplicação completa (pilha do Compose com o Mailpit; a entrada é pelo link mágico da SPEC-0016,
 * lido na API do Mailpit; `admin@subsolo.test` é admin nesta pilha). Telas do Stitch "Meus perfis", "Pedir Perfil",
 * "Reivindicar Perfil" e "Fila do Admin", com os ajustes do §6.
 *
 * - E2E-01 (mobile-chrome e mobile-safari): o fã vai de `/conta` (seção "Perfis") a "Meus perfis" (vazio: "Você ainda
 *   não tem perfil…"), pede o perfil de banda "Podridão …" em `/conta/perfis/novo` (aviso de aprovação, "Enviar pedido"
 *   → "Pedido enviado." e volta a `/conta/perfis`) e vê "EM ANÁLISE"; o admin, noutro contexto do navegador, abre
 *   `/admin/fila`, aba "Pedidos de perfil", e aprova; o fã recarrega e vê "APROVADO"; um segundo pedido recusado com
 *   motivo mostra "RECUSADO", o motivo e "Pedir novamente". Sem violações do axe nas telas.
 * - E2E-02 (mobile-chrome): o admin cria em `/admin/perfis/novo` (o formulário do pedido, sem o contato) a casa sem
 *   dono "Porão do Rock …" ("Perfil criado."); um fã a encontra em "Reivindicar perfil" (selo "SEM DONO"), envia a
 *   prova; o admin aprova; o perfil aparece em "Meus perfis" do fã como "APROVADO".
 *
 * Seletores: papéis e rótulos (o tipo é um grupo de rádio "Tipo"; cada pedido da fila e cada perfil/pedido de "Meus
 * perfis" é um `article` com o nome); os selos têm texto (a caixa alta pode ser só do CSS, por isso as expressões não
 * diferenciam maiúsculas). Cada execução usa nomes e e-mails próprios: a pilha pode ser reaproveitada e o nome é único
 * por tipo. Antes de a página hidratar (mobile-safari), um toque ou um texto digitado podem se perder: cada campo e
 * botão só é usado depois de hidratado.
 */
// SPEC-0010: o Caddy do Compose serve HTTPS com o certificado local (`tls internal`), não confiável.
test.use({ baseURL: appURL, ignoreHTTPSErrors: true });

const adminEmail = "admin@subsolo.test";
const myProfilesPath = "/conta/perfis";
const requestPath = "/conta/perfis/novo";
const claimPath = "/conta/perfis/reivindicar";
const queuePath = "/admin/fila";
const ownerlessPath = "/admin/perfis/novo";
const city = "Cidade Exemplo";
const citySlug = "cidade-exemplo";

/** Sufixo novo por execução (o nome do perfil é único por tipo e a pilha pode ser reaproveitada). */
function uniqueSuffix(): string {
  return `${Date.now().toString(36)}${randomUUID().replaceAll("-", "").slice(0, 4)}`;
}

/** Toca no controle depois de a página hidratá-lo (antes disso o toque se perde). */
async function press(control: Locator): Promise<void> {
  await waitForHydration(control);
  await control.click();
}

/** Mensagens anunciadas (`aria-live`/`role=status`). */
function announcements(page: Page): Locator {
  return page.locator(
    '[aria-live="polite"], [aria-live="assertive"], [role="status"]',
  );
}

/** O cartão (`article`) de um perfil ou pedido com o nome. */
function card(scope: Page | Locator, name: string): Locator {
  return scope.getByRole("article").filter({ hasText: name });
}

/** Escolhe a cidade: um `<select>` (pelo texto da opção) ou um campo de texto/autocompletar. */
async function chooseCity(form: Locator): Promise<void> {
  const field = form.getByLabel("Cidade", { exact: true });
  await waitForHydration(field);
  const tag = await field.evaluate((element) => element.tagName.toLowerCase());
  if (tag === "select") {
    await field.selectOption({ label: city });
    await expect(field).toHaveValue(citySlug);
    return;
  }
  await field.fill(city);
  const option = form.page().getByRole("option", { name: city });
  if ((await option.count()) > 0) {
    await option.first().click();
  }
}

/** Marca o estilo (caixa de seleção ou chip com estado pressionado). */
async function pickStyle(form: Locator, style: string): Promise<void> {
  const control = form
    .getByRole("checkbox", { name: new RegExp(`^${style}$`, "i") })
    .or(form.getByRole("button", { name: new RegExp(`^${style}$`, "i") }))
    .first();
  await press(control);
}

/** Preenche o formulário do perfil (tipo, nome, cidade, estilos de banda, Instagram e, no pedido, o contato). */
async function fillProfileForm(
  form: Locator,
  profile: {
    type: "Banda" | "Casa / Local" | "Produtor";
    name: string;
    contact?: string;
  },
): Promise<void> {
  const types = form.getByRole("radiogroup", { name: /tipo/i });
  await expect(types).toBeVisible();
  for (const option of ["Banda", "Casa / Local", "Produtor"]) {
    await expect(types.getByRole("radio", { name: option })).toBeVisible();
  }
  await press(types.getByRole("radio", { name: profile.type }));
  await expect(types.getByRole("radio", { name: profile.type })).toBeChecked();
  await fillInteractive(form.getByLabel("Nome", { exact: true }), profile.name);
  await chooseCity(form);
  if (profile.type === "Banda") {
    await pickStyle(form, "death");
  }
  await fillInteractive(
    form.getByLabel(/instagram/i),
    "https://www.instagram.com/subsolo.e2e",
  );
  if (profile.contact !== undefined) {
    await fillInteractive(form.getByLabel(/contato/i), profile.contact);
  }
}

/** Entra e chega a "Meus perfis". */
async function openMyProfiles(page: Page, email: string): Promise<void> {
  await signIn(page, email, myProfilesPath);
  await expect(page).toHaveURL((url) => url.pathname === myProfilesPath);
  await expect(
    page.getByRole("heading", { level: 1, name: /meus perfis/i }),
  ).toBeVisible();
}

/** Pede um perfil de banda em `/conta/perfis/novo` e volta para "Meus perfis" com "Pedido enviado.". */
async function requestBand(
  page: Page,
  name: string,
  axeCheck: () => Promise<void>,
) {
  await press(
    page
      .getByRole("link", { name: /pedir novo perfil|pedir novamente/i })
      .first(),
  );
  await expect(page).toHaveURL((url) => url.pathname === requestPath);
  await expect(
    page.getByRole("heading", { level: 1, name: /pedir perfil/i }),
  ).toBeVisible();
  await expect(
    page.getByText("Seu pedido passa por aprovação do admin da cena"),
  ).toBeVisible();
  const form = page.locator("main form");
  await fillProfileForm(form, {
    type: "Banda",
    name,
    contact: "Caio, @caio.podridao no Instagram",
  });
  await axeCheck();
  await press(form.getByRole("button", { name: "Enviar pedido" }));
  await expect(
    announcements(page).filter({ hasText: "Pedido enviado." }),
  ).toBeVisible();
  await expect(page).toHaveURL((url) => url.pathname === myProfilesPath);
  await expect(card(page, name)).toContainText(/em análise/i);
}

/** Um contexto novo do navegador (outra pessoa) com o admin já na fila. */
async function adminOnQueue(browser: Browser): Promise<Page> {
  const context = await browser.newContext({
    baseURL: appURL,
    ignoreHTTPSErrors: true,
  });
  const page = await context.newPage();
  await signIn(page, adminEmail, queuePath);
  await expect(page).toHaveURL((url) => url.pathname === queuePath);
  await expect(
    page.getByRole("tab", { name: /pedidos de perfil/i }),
  ).toBeVisible();
  return page;
}

/** Recarrega a fila do admin e devolve o cartão do pedido. */
async function queueCard(admin: Page, name: string): Promise<Locator> {
  await admin.goto(queuePath);
  const tab = admin.getByRole("tab", { name: /pedidos de perfil/i });
  await expect(tab).toBeVisible();
  await expect(tab).toHaveText(/\(\d+\)/);
  const found = card(admin.locator("main"), name);
  await expect(found).toHaveCount(1);
  return found;
}

/** O admin aprova o pedido com o nome; o cartão sai da fila. */
async function approve(admin: Page, name: string): Promise<void> {
  const found = await queueCard(admin, name);
  await press(found.getByRole("button", { name: "Aprovar" }));
  await expect(card(admin.locator("main"), name)).toHaveCount(0);
}

test.describe("perfis: pedido, aprovação e reivindicação", () => {
  test("SPEC-0021:E2E-01 fã pede o perfil da banda, o admin aprova e uma recusa mostra o motivo [jornada: perfis]", async ({
    page,
    browser,
    axe,
  }, testInfo) => {
    test.skip(
      testInfo.project.name === "desktop-chrome",
      "jornada do celular (mobile-chrome e mobile-safari)",
    );
    test.setTimeout(240_000);
    const suffix = uniqueSuffix();
    const band = `Podridão ${suffix}`;
    const second = `Coágulo ${suffix}`;
    const reason = "Faltou o link do Bandcamp da banda.";
    const fan = uniqueEmail("perfis01");

    // /conta → seção "Perfis" → "Meus perfis" (vazio).
    await signIn(page, fan, "/conta");
    await expect(
      page.getByRole("heading", { level: 1, name: /minha conta/i }),
    ).toBeVisible();
    await expect(
      page.getByRole("heading", { level: 2, name: /perfis/i }),
    ).toBeVisible();
    await press(page.getByRole("link", { name: "Meus perfis" }));
    await expect(page).toHaveURL((url) => url.pathname === myProfilesPath);
    await expect(
      page.getByRole("heading", { level: 1, name: /meus perfis/i }),
    ).toBeVisible();
    await expect(
      page.getByText(
        "Você ainda não tem perfil. Toca uma banda, tem uma casa ou produz shows? Peça o seu.",
      ),
    ).toBeVisible();
    await expect(
      page.getByRole("link", { name: /reivindicar perfil existente/i }),
    ).toBeVisible();
    await axe.check(page);

    // Pede o perfil da banda: "EM ANÁLISE".
    await requestBand(page, band, () => axe.check(page));
    await axe.check(page);

    // O admin, noutro contexto, aprova na fila.
    const admin = await adminOnQueue(browser);
    try {
      const pending = await queueCard(admin, band);
      await expect(pending).toContainText(city);
      await expect(pending).toContainText("Caio, @caio.podridao no Instagram");
      await expect(
        pending.getByRole("button", { name: "Recusar" }),
      ).toBeVisible();
      await axe.check(admin);
      await approve(admin, band);

      // O fã recarrega e vê "APROVADO".
      await page.reload();
      await expect(card(page, band)).toContainText(/aprovado/i);
      await expect(card(page, band)).not.toContainText(/em análise/i);
      await axe.check(page);

      // Um segundo pedido, recusado com motivo: "RECUSADO", o motivo e "Pedir novamente".
      await requestBand(page, second, async () => {});
      const toReject = await queueCard(admin, second);
      await press(toReject.getByRole("button", { name: "Recusar" }));
      await fillInteractive(toReject.getByLabel(/motivo/i), reason);
      await axe.check(admin);
      await press(
        toReject.getByRole("button", { name: /recusa|recusar/i }).last(),
      );
      await expect(card(admin.locator("main"), second)).toHaveCount(0);
    } finally {
      await admin.context().close();
    }

    await page.reload();
    const rejected = card(page, second);
    await expect(rejected).toContainText(/recusado/i);
    await expect(rejected).toContainText(reason);
    await expect(
      rejected.getByRole("link", { name: /pedir novamente/i }),
    ).toBeVisible();
    await expect(card(page, band)).toContainText(/aprovado/i);
    await axe.check(page);
  });

  test("SPEC-0021:E2E-02 admin cria a casa sem dono, o fã reivindica e o admin aprova [jornada: perfis]", async ({
    page,
    browser,
    axe,
  }, testInfo) => {
    test.skip(
      testInfo.project.name !== "mobile-chrome",
      "reivindicação de perfil: mobile-chrome",
    );
    test.setTimeout(240_000);
    const venue = `Porão do Rock ${uniqueSuffix()}`;
    const fan = uniqueEmail("perfis02");

    const admin = await adminOnQueue(browser);
    try {
      // O admin cria a casa sem dono: o formulário do pedido, sem o contato.
      const created = await admin.goto(ownerlessPath);
      expect(created?.status(), `GET ${ownerlessPath}`).toBe(200);
      const form = admin.locator("main form");
      await expect(form.getByLabel(/contato/i)).toHaveCount(0);
      await fillProfileForm(form, { type: "Casa / Local", name: venue });
      await axe.check(admin);
      await press(form.getByRole("button", { name: /criar perfil/i }));
      await expect(
        announcements(admin).filter({ hasText: "Perfil criado." }),
      ).toBeVisible();

      // O fã encontra a casa em "Reivindicar perfil" (selo "SEM DONO") e envia a prova.
      await openMyProfiles(page, fan);
      await press(
        page.getByRole("link", { name: /reivindicar perfil existente/i }),
      );
      await expect(page).toHaveURL((url) => url.pathname === claimPath);
      await expect(
        page.getByRole("heading", { level: 1, name: /reivindicar perfil/i }),
      ).toBeVisible();
      const search = page
        .getByRole("searchbox")
        .or(page.getByLabel(/buscar/i))
        .first();
      await fillInteractive(search, venue);
      const result = card(page.locator("main"), venue);
      await expect(result).toContainText(/sem dono/i);
      await expect(result).toContainText(city);
      await axe.check(page);
      const reivindicar = result.getByRole("button", { name: /reivindicar/i });
      if ((await reivindicar.count()) > 0) {
        await press(reivindicar);
      }
      await fillInteractive(
        page.getByLabel(/prova/i),
        "Sou o dono do Porão; o Instagram da casa confirma.",
      );
      await axe.check(page);
      await press(page.getByRole("button", { name: "Enviar pedido" }));
      await expect(
        announcements(page).filter({ hasText: "Pedido enviado." }),
      ).toBeVisible();
      await expect(page).toHaveURL((url) => url.pathname === myProfilesPath);
      await expect(card(page, venue)).toContainText(/em análise/i);

      // O admin aprova a reivindicação.
      const claim = await queueCard(admin, venue);
      await expect(claim).toContainText(
        "Sou o dono do Porão; o Instagram da casa confirma.",
      );
      await approve(admin, venue);
    } finally {
      await admin.context().close();
    }

    // O perfil aparece em "Meus perfis" do fã como "APROVADO".
    await page.reload();
    await expect(card(page, venue)).toContainText(/aprovado/i);
    await axe.check(page);
  });
});
