import type { Locator, Page } from "@playwright/test";

import { expect, test } from "../fixtures";
import { appURL, externalApp } from "../agenda/environment";
import { fillInteractive, signIn } from "../auth/journey";
import { adminEmail, announcements, press, uniqueSuffix } from "../shows/journey";
import {
  adminSupportersPath,
  apoiePath,
  qrCodeName,
  testPixKey,
} from "./environment";

/**
 * SPEC-0028 E2E-01 — a página Apoie na aplicação completa (pilha do Compose com a chave Pix de teste da
 * `./environment`), nos projetos mobile-chrome e mobile-safari. Telas do Stitch "Apoie o Subsolo v2" e "Admin -
 * Apoiadores" (com o QR code gerado da chave real e os textos do §6).
 *
 * O fã toca em "Apoie" na navegação e vê o QR code ("QR code do Pix do Subsolo") e a chave em texto; "Copiar chave"
 * anuncia "Chave copiada.". O admin adiciona "Putridearth Zine …" (nome com sufixo próprio por execução: a pilha pode
 * ser reaproveitada) em `/admin/apoiadores` com um link `https`, e o nome aparece em "APOIADORES" de `/apoie`, com o
 * link; remove (com confirmação) e o nome some das duas páginas. Sem violações do axe em `/apoie` e no admin.
 *
 * Seletores: papéis e nomes acessíveis; textos em caixa alta só pelo CSS são buscados sem diferenciar maiúsculas.
 */
// SPEC-0010: o Caddy do Compose serve HTTPS com o certificado local (`tls internal`), não confiável.
test.use({ baseURL: appURL, ignoreHTTPSErrors: true });

const supporterLink = "https://zine.example";

/** A seção "APOIADORES" da página `/apoie`. */
function supportersSection(page: Page): Locator {
  return page
    .locator("section")
    .filter({ has: page.getByRole("heading", { name: /^apoiadores$/i }) });
}

/** O item da lista de apoiadores do admin com o nome. */
function adminRow(page: Page, name: string): Locator {
  return page.getByRole("listitem").filter({ hasText: name });
}

test.describe("página Apoie", () => {
  test("SPEC-0028:E2E-01 o fã vê o Pix e copia a chave; o admin adiciona e remove um apoiador [jornada: agenda]", async ({
    page,
    browser,
    axe,
  }, testInfo) => {
    test.skip(
      testInfo.project.name === "desktop-chrome",
      "jornada do celular (mobile-chrome e mobile-safari)",
    );
    test.skip(
      externalApp,
      "a chave Pix de teste só existe na pilha local do Compose",
    );
    test.setTimeout(300_000);
    if (testInfo.project.name === "mobile-chrome") {
      await page
        .context()
        .grantPermissions(["clipboard-read", "clipboard-write"]);
    }

    // O fã chega pela navegação.
    const agenda = await page.goto("/cidade-exemplo");
    expect(agenda?.status(), "GET /cidade-exemplo").toBe(200);
    const apoieTab = page
      .getByRole("link", { name: /^apoie$/i })
      .filter({ visible: true })
      .first();
    await press(apoieTab);
    await expect(page).toHaveURL((url) => url.pathname === apoiePath);
    await expect(
      page.getByRole("heading", { level: 1, name: /^apoie o subsolo$/i }),
    ).toBeVisible();
    await expect(page.getByRole("img", { name: qrCodeName })).toBeVisible();
    await expect(page.getByText(testPixKey, { exact: true })).toBeVisible();
    await expect(page.getByText("O Pix do Subsolo chega em breve.")).toHaveCount(
      0,
    );

    const copy = page.getByRole("button", { name: /copiar chave/i });
    await press(copy);
    await expect(
      announcements(page).filter({ hasText: "Chave copiada." }),
      "a cópia não foi anunciada",
    ).not.toHaveCount(0);
    await axe.check(page);

    // O admin adiciona um apoiador.
    const name = `Putridearth Zine ${uniqueSuffix()}`;
    const admin = await browser.newContext({
      baseURL: appURL,
      ignoreHTTPSErrors: true,
    });
    try {
      const adminPage = await admin.newPage();
      await signIn(adminPage, adminEmail, adminSupportersPath);
      await expect(
        adminPage.getByRole("heading", { level: 1, name: /apoiadores/i }),
      ).toBeVisible();
      await fillInteractive(adminPage.getByLabel(/^nome/i), name);
      await fillInteractive(adminPage.getByLabel(/^link/i), supporterLink);
      await press(adminPage.getByRole("button", { name: /^adicionar$/i }));
      await expect(adminRow(adminPage, name)).toBeVisible();
      await axe.check(adminPage);

      // O nome aparece em /apoie, com o link.
      await page.reload();
      const listed = supportersSection(page).getByRole("link", {
        name: new RegExp(name),
      });
      await expect(listed).toBeVisible();
      await expect(listed).toHaveAttribute("href", supporterLink);

      // O admin remove, com confirmação.
      await press(adminRow(adminPage, name).getByRole("button", { name: /remover/i }));
      const confirm = adminPage
        .getByRole("dialog")
        .or(adminPage.getByRole("alertdialog"));
      await expect(confirm).toBeVisible();
      await axe.check(adminPage);
      await press(confirm.getByRole("button", { name: /^remover$/i }));
      await expect(adminRow(adminPage, name)).toHaveCount(0);

      // E o nome some de /apoie.
      await page.reload();
      await expect(
        page.getByRole("heading", { name: /^apoiadores$/i }),
      ).toBeVisible();
      await expect(supportersSection(page).getByText(name)).toHaveCount(0);
    } finally {
      await admin.close();
    }
  });
});
