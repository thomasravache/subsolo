import { randomUUID } from "node:crypto";

import type { Locator, Page } from "@playwright/test";

import { expect, test } from "../fixtures";
import { appURL } from "../agenda/environment";
import { signIn, uniqueEmail } from "../auth/journey";

/**
 * SPEC-0017 — cidades e estilos no painel do admin, na aplicação completa (pilha do Compose com o Mailpit; a entrada é
 * pelo link mágico da SPEC-0016, lido na API do Mailpit).
 *
 * - E2E-01 (desktop-chrome): o admin (`admin@subsolo.test`, em `Users:AdminEmails` do ambiente de teste) entra pelo
 *   link mágico com o `returnTo` do painel, abre `/admin/cidades-e-estilos`, cria "São José dos Pinhais …" (vê a prévia
 *   do endereço `agendasubsolo.com.br/{slug}` gerado do nome — Emenda 2), vê "Cidade criada." e a cidade na tabela; o endereço `admin` mostra "Esse endereço é
 *   reservado."; adiciona um estilo "Crust …" e o vê entre os chips. Depois, `/{slug}` mostra a agenda vazia e a cidade
 *   está no seletor de cidade. Sem violações do axe.
 * - E2E-02 (mobile-chrome): anônimo em `/admin/cidades-e-estilos` vai (307) para
 *   `/entrar?returnTo=/admin/cidades-e-estilos`; um fã logado vê "Você não tem acesso a esta página." com status 403,
 *   sem violações do axe.
 *
 * Cada execução usa nomes próprios (sufixo só com minúsculas e dígitos, para o endereço gerado ser previsível): a pilha
 * pode ser reaproveitada entre execuções e o endereço de uma cidade não muda nem é apagado.
 */
// SPEC-0010: o Caddy do Compose serve HTTPS com o certificado local (`tls internal`), não confiável.
test.use({ baseURL: appURL, ignoreHTTPSErrors: true });

const adminPath = "/admin/cidades-e-estilos";
const adminEmail = "admin@subsolo.test";

/** Sufixo novo por execução: minúsculas e dígitos (o gerador de slug mantém como está). */
function uniqueSuffix(): string {
  return `${Date.now().toString(36)}${randomUUID().replaceAll("-", "").slice(0, 4)}`;
}

/** Preenche um campo de texto ou escolhe a opção de um `<select>`, conforme o controle da tela. */
async function choose(field: Locator, value: string): Promise<void> {
  const tag = await field.evaluate((element) => element.tagName.toLowerCase());
  if (tag === "select") {
    await field.selectOption(value);
  } else {
    await field.fill(value);
  }
}

/** O formulário de cidade aberto por "Nova cidade" (ou "Editar"). */
function cityForm(page: Page): Locator {
  return page.locator("form").filter({ has: page.getByLabel("UF") });
}

/** Envia o formulário de cidade pelo botão de salvar/criar. */
async function submitCity(form: Locator): Promise<void> {
  await form
    .getByRole("button", { name: /^(salvar|criar)( cidade)?$/i })
    .click();
}

test.describe("cidades e estilos no painel do admin", () => {
  test("SPEC-0017:E2E-01 admin cria cidade e estilo pelo painel e a cidade ganha agenda e entra no seletor [jornada: admin-cidades]", async ({
    page,
    axe,
  }, testInfo) => {
    test.skip(
      testInfo.project.name !== "desktop-chrome",
      "painel do admin: desktop primeiro (desktop-chrome)",
    );
    test.setTimeout(180_000);

    const suffix = uniqueSuffix();
    const cityName = `São José dos Pinhais ${suffix}`;
    const citySlug = `sao-jose-dos-pinhais-${suffix}`;
    const styleName = `Crust ${suffix}`;

    // Entra como admin pelo link mágico e volta para o painel.
    await signIn(page, adminEmail, adminPath);
    await expect(page).toHaveURL((url) => url.pathname === adminPath);
    await expect(page).toHaveTitle("Cidades e estilos | Admin | Subsolo");
    const table = page.getByRole("table");
    for (const column of ["Nome", "UF", "Fuso", "Endereço"]) {
      await expect(
        table.getByRole("columnheader", { name: column }),
      ).toBeVisible();
    }
    await axe.check(page);

    // Nova cidade: o endereço é gerado do nome, com a prévia.
    await page.getByRole("button", { name: "Nova cidade" }).click();
    const form = cityForm(page);
    await expect(form).toBeVisible();
    await form.getByLabel("Nome", { exact: true }).fill(cityName);
    await choose(form.getByLabel("UF"), "PR");
    await expect(form.getByLabel("Fuso")).toHaveValue("America/Sao_Paulo");
    await expect(form.getByLabel("Endereço")).toHaveValue(citySlug);
    await expect(
      form.getByText(`agendasubsolo.com.br/${citySlug}`),
    ).toBeVisible();
    await axe.check(page);
    await submitCity(form);

    const status = page.locator(
      '[aria-live="polite"], [aria-live="assertive"], [role="status"]',
    );
    await expect(status.filter({ hasText: "Cidade criada." })).toBeVisible();
    const row = table.getByRole("row").filter({ hasText: cityName });
    await expect(row).toHaveCount(1);
    await expect(row).toContainText("PR");
    await expect(row).toContainText("America/Sao_Paulo");
    await expect(row).toContainText(`/${citySlug}`);

    // Endereço reservado: erro no campo, nada criado.
    await page.getByRole("button", { name: "Nova cidade" }).click();
    const again = cityForm(page);
    await again.getByLabel("Nome", { exact: true }).fill(`Admin ${suffix}`);
    await choose(again.getByLabel("UF"), "PR");
    await again.getByLabel("Endereço").fill("admin");
    await submitCity(again);
    const address = again.getByLabel("Endereço");
    await expect(again.getByText("Esse endereço é reservado.")).toBeVisible();
    await expect(address).toHaveAttribute("aria-invalid", "true");
    await expect(address).toHaveAccessibleDescription(
      /Esse endereço é reservado\./,
    );
    await expect(
      table.getByRole("row").filter({ hasText: "/admin" }),
    ).toHaveCount(0);
    await axe.check(page);

    // Novo estilo: entra entre os chips.
    await page.getByLabel("Novo estilo").fill(styleName);
    await page.getByRole("button", { name: "Adicionar" }).click();
    await expect(
      status.filter({ hasText: "Estilo adicionado." }),
    ).toBeVisible();
    await expect(page.getByText(styleName, { exact: true })).toBeVisible();
    for (const initial of ["death", "black", "grind", "doom", "thrash"]) {
      await expect(
        page.getByText(new RegExp(`^${initial}$`, "i")).first(),
      ).toBeVisible();
    }
    await axe.check(page);

    // A cidade nova tem agenda (vazia) e aparece no seletor de cidade.
    const agenda = await page.goto(`/${citySlug}`);
    expect(agenda?.status(), `GET /${citySlug}`).toBe(200);
    await expect(
      page.getByRole("heading", { level: 1, name: `Agenda ${cityName}` }),
    ).toBeVisible();
    await expect(
      page.getByText(`Nenhum show anunciado ainda em ${cityName}.`),
    ).toBeVisible();
    const switcher = page
      .getByRole("banner")
      .getByRole("combobox", { name: /cidade/i });
    await expect(switcher).toHaveValue(citySlug);
    await expect(switcher.locator(`option[value="${citySlug}"]`)).toHaveText(
      cityName,
    );
    await axe.check(page);
  });

  test("SPEC-0017:E2E-02 anônimo vai para entrar e fã vê a página 403 do painel [jornada: admin-cidades]", async ({
    page,
    axe,
  }, testInfo) => {
    test.skip(
      testInfo.project.name !== "mobile-chrome",
      "acesso negado ao painel no celular (mobile-chrome)",
    );
    test.setTimeout(120_000);

    // Anônimo: 307 para /entrar com o returnTo do painel.
    const redirect = await page.request.get(adminPath, { maxRedirects: 0 });
    expect(redirect.status(), `GET ${adminPath} anônimo`).toBe(307);
    const location = new URL(redirect.headers()["location"] ?? "", appURL);
    expect(location.pathname).toBe("/entrar");
    expect(location.searchParams.get("returnTo")).toBe(adminPath);

    await page.goto(adminPath);
    await expect(page).toHaveURL(
      (url) =>
        url.pathname === "/entrar" &&
        url.searchParams.get("returnTo") === adminPath,
    );

    // Fã logado: página 403. A entrada volta para /conta e só segue quando a conta aparece, para o cookie de sessão
    // já estar gravado (sem returnTo, o helper não espera a navegação depois de "Entrar no Subsolo", e o goto
    // seguinte cancelaria a troca do link pela sessão).
    await signIn(page, uniqueEmail("e2e02-fa-admin"), "/conta");
    await expect(
      page.getByRole("heading", { level: 1, name: /minha conta/i }),
    ).toBeVisible();
    const forbidden = await page.goto(adminPath);
    expect(forbidden?.status(), `GET ${adminPath} como fã`).toBe(403);
    await expect(page).toHaveURL((url) => url.pathname === adminPath);
    await expect(
      page.getByText("Você não tem acesso a esta página."),
    ).toBeVisible();
    await expect(page.getByRole("table")).toHaveCount(0);
    await axe.check(page);
  });
});
