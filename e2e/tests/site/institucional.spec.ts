import type { Page } from "@playwright/test";

import { expect, test } from "../fixtures";
import { appURL, externalApp, webWithoutApiURL } from "../agenda/environment";
import { press } from "../shows/journey";
import { apoiePath, qrCodeName } from "./environment";

/**
 * SPEC-0028 E2E-02 (mobile-chrome) — páginas institucionais e o Apoie sem chave Pix:
 * - sem chave Pix, `/apoie` responde 200 com "O Pix do Subsolo chega em breve." e sem QR code. A pilha do Compose tem a
 *   chave de teste (E2E-01); a aplicação sem chave é o servidor Next.js de produção sem API do `playwright.config.ts`
 *   (SPEC-0008 IT-04), que não recebe a configuração do Pix — e, sem API, mostra também "A lista de apoiadores volta
 *   já." (IT-03);
 * - na aplicação completa, os links "Apoie" e "Privacidade" do rodapé levam a `/apoie` e `/privacidade` (200), e
 *   `/termos` (200) é aberta pelo link "Termos" de `/entrar` (o rodapé da SPEC-0011 não tem "Termos");
 * - `/privacidade` e `/termos` têm o h1, "Atualizada em …", as seções do §6 e o índice com âncoras (tocar num item leva
 *   à seção); sem violações do axe nas duas.
 */
test.use({ baseURL: appURL, ignoreHTTPSErrors: true });

const privacySections = [
  "Que dados guardamos",
  "Para quê",
  "Por quanto tempo",
  "Seus direitos",
  "Contato",
];

const termsSections = [
  "O que é o Subsolo",
  "Conta",
  "Quem cadastra shows",
  "Conteúdo proibido",
  "Moderação",
  "Mudanças",
];

/** Escapa um texto para usar numa expressão regular. */
function literal(text: string): string {
  return text.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

/** O h1, a data de atualização, as seções e o índice com âncoras; tocar no último item leva à seção dele. */
async function expectInstitutionalPage(
  page: Page,
  title: RegExp,
  sections: string[],
): Promise<void> {
  await expect(page.getByRole("heading", { level: 1, name: title })).toBeVisible();
  await expect(page.getByText(/^Atualizada em \S/)).toBeVisible();
  const index = page.getByRole("navigation").filter({
    has: page.locator('a[href^="#"]'),
  });
  await expect(index, "a página não tem o índice com âncoras").toHaveCount(1);
  for (const name of sections) {
    const pattern = new RegExp(`^${literal(name)}$`, "i");
    await expect(page.getByRole("heading", { name: pattern })).toHaveCount(1);
    await expect(index.getByRole("link", { name: pattern })).toHaveAttribute(
      "href",
      /^#.+/,
    );
  }
  const last = sections[sections.length - 1] ?? "";
  const lastPattern = new RegExp(`^${literal(last)}$`, "i");
  const anchor = index.getByRole("link", { name: lastPattern });
  const target = (await anchor.getAttribute("href")) ?? "";
  await anchor.click();
  await expect(page).toHaveURL((url) => url.hash === target);
  await expect(page.getByRole("heading", { name: lastPattern })).toBeInViewport();
}

test.describe("páginas institucionais", () => {
  test.beforeEach(({}, testInfo) => {
    test.skip(
      testInfo.project.name !== "mobile-chrome",
      "verificação das páginas: basta o mobile-chrome",
    );
  });

  test("SPEC-0028:E2E-02 sem chave Pix, /apoie mostra O Pix do Subsolo chega em breve. [jornada: agenda]", async ({
    page,
    axe,
  }) => {
    test.skip(externalApp, "com BASE_URL o webServer não sobe o Next.js sem API");

    const response = await page.goto(`${webWithoutApiURL}${apoiePath}`);

    expect(response?.status(), `GET ${apoiePath} sem chave`).toBe(200);
    await expect(
      page.getByRole("heading", { level: 1, name: /^apoie o subsolo$/i }),
    ).toBeVisible();
    await expect(page.getByText("O Pix do Subsolo chega em breve.")).toBeVisible();
    await expect(page.getByRole("img", { name: qrCodeName })).toHaveCount(0);
    await expect(page.getByRole("button", { name: /copiar chave/i })).toHaveCount(0);
    await expect(page.getByText("A lista de apoiadores volta já.")).toBeVisible();
    await axe.check(page);
  });

  test("SPEC-0028:E2E-02 o rodapé leva a Apoie e Privacidade, e Termos abre de /entrar, com as seções e o índice [jornada: agenda]", async ({
    page,
    axe,
  }) => {
    for (const path of [apoiePath, "/privacidade", "/termos"]) {
      const direct = await page.request.get(path);
      expect(direct.status(), `GET ${path}`).toBe(200);
    }

    // Rodapé → Apoie.
    await page.goto("/cidade-exemplo");
    const footer = page.getByRole("contentinfo");
    await press(footer.getByRole("link", { name: /^apoie$/i }));
    await expect(page).toHaveURL((url) => url.pathname === apoiePath);
    await expect(
      page.getByRole("heading", { level: 1, name: /^apoie o subsolo$/i }),
    ).toBeVisible();

    // Rodapé → Privacidade.
    await press(
      page.getByRole("contentinfo").getByRole("link", { name: /^privacidade$/i }),
    );
    await expect(page).toHaveURL((url) => url.pathname === "/privacidade");
    await expectInstitutionalPage(page, /^privacidade$/i, privacySections);
    await axe.check(page);

    // /entrar → Termos.
    await page.goto("/entrar");
    await press(page.getByRole("link", { name: /^termos$/i }));
    await expect(page).toHaveURL((url) => url.pathname === "/termos");
    await expectInstitutionalPage(page, /^termos de uso$/i, termsSections);
    await expect(page.getByText(/discurso de ódio/i)).toBeVisible();
    await expect(page.getByText(/NSBM/)).toBeVisible();
    await axe.check(page);
  });
});
