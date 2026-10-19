import type { Page } from "@playwright/test";

import { expect, test } from "../fixtures";
import { appURL } from "../agenda/environment";
import { card, press } from "./journey";
import { instantOf, publishShows } from "./show-page";

/**
 * SPEC-0024 — a página do show `/{cidade}/{show}` na aplicação completa (pilha do Compose). Telas do Stitch "Página do
 * Show - Noite da Peste II" (celular e desktop) com os ajustes do §6: sem "Vou", "Quem vai", "Seguir" e "Denunciar", e
 * sem links de perfil (a página do perfil ainda não existe). O fã é a página do teste, anônima; o show é cadastrado
 * pela API num contexto à parte.
 *
 * - E2E-01 (mobile-chrome e mobile-safari): o fã abre a agenda da cidade, toca no card e vê a página com flyer
 *   (`alt` "Flyer de {título}"), data e hora (`<time datetime>`), casa, line-up, estilos, preço e "Ingresso" (nova aba,
 *   `rel="noopener noreferrer"`); toca em "Compartilhar" com o `navigator.share` simulado e o compartilhamento leva o
 *   link do show; um show cancelado mostra o selo "CANCELADO". Sem violações do axe.
 * - E2E-02 (mobile-chrome): um show inexistente mostra "Show não encontrado" com status 404 e o link "Ver a agenda de
 *   {cidade}"; a página de um show carrega sem violação de CSP.
 * - E2E-03 (mobile-chrome): a página do show tem LCP ≤ 2,5 s (`PerformanceObserver`), CLS ≤ 0,1, e a imagem do flyer
 *   tem `width` e `height`.
 */
test.use({ baseURL: appURL, ignoreHTTPSErrors: true });

const lcpBudgetMs = 2_500;
const clsBudget = 0.1;

declare global {
  interface Window {
    __shared?: ShareData[];
    __showCspViolations?: string[];
    __lcp?: number;
    __cls?: number;
  }
}

/** `navigator.share` simulado: guarda o que foi compartilhado em `window.__shared`. */
async function simulateShare(page: Page): Promise<void> {
  await page.addInitScript(() => {
    window.__shared = [];
    Object.defineProperty(navigator, "share", {
      configurable: true,
      value: async (data: ShareData) => {
        window.__shared?.push(data);
      },
    });
    Object.defineProperty(navigator, "canShare", {
      configurable: true,
      value: () => true,
    });
  });
}

test.describe("página do show", () => {
  test("SPEC-0024:E2E-01 fã toca no card da agenda, vê o show e compartilha o link [jornada: compartilhar-show]", async ({
    page,
    browser,
    axe,
  }, testInfo) => {
    test.skip(
      testInfo.project.name === "desktop-chrome",
      "jornada do celular (mobile-chrome e mobile-safari)",
    );
    test.setTimeout(300_000);
    const { producer, shows } = await publishShows(
      browser,
      "pagina01",
      (suffix) => [
        { title: `Noite da Peste ${suffix}` },
        { title: `Sepulcro Fest ${suffix}`, cancelled: true },
      ],
    );
    const [show, cancelled] = shows;
    if (!show || !cancelled) {
      throw new Error("preparação sem os dois shows");
    }
    await simulateShare(page);

    // Agenda → card do show (next/link).
    const agenda = await page.goto(`/${producer.city.slug}`);
    expect(agenda?.status()).toBe(200);
    await press(
      card(page.getByRole("main"), show.title).getByRole("link", {
        name: show.title,
      }),
    );
    await expect(page).toHaveURL((url) => url.pathname === show.path);

    const main = page.getByRole("main");
    await expect(
      page.getByRole("heading", { level: 1, name: show.title }),
    ).toBeVisible();
    await expect(
      main.getByRole("img", { name: `Flyer de ${show.title}` }),
    ).toBeVisible();
    const time = main.locator("time").first();
    await expect(time).toBeVisible();
    expect(
      Date.parse((await time.getAttribute("datetime")) ?? ""),
      "<time datetime> sem o início do show",
    ).toBe(instantOf(show.startsLocal));
    await expect(main).toContainText(show.venue);
    await expect(main).toContainText(/Cova Rasa\s*\/\s*Necrocorpo/);
    await expect(main).toContainText(/death/i);
    await expect(main).toContainText(show.price);
    const ticket = main.getByRole("link", { name: /ingresso/i });
    await expect(ticket).toHaveAttribute("href", show.ticketUrl ?? "");
    await expect(ticket).toHaveAttribute("target", "_blank");
    const rel = ((await ticket.getAttribute("rel")) ?? "").split(/\s+/);
    expect(rel, 'rel do "Ingresso"').toEqual(
      expect.arrayContaining(["noopener", "noreferrer"]),
    );
    // Ajustes do §6: nada de botões que ainda não funcionam.
    for (const name of [/^vou\b/i, /quem vai/i, /^seguir/i, /denunciar/i]) {
      await expect(
        page.getByRole("button", { name }).or(page.getByRole("link", { name })),
        `controle ${name} na página (fica para a spec própria)`,
      ).toHaveCount(0);
    }
    await axe.check(page);

    // Compartilhar (Web Share simulado).
    await press(main.getByRole("button", { name: /compartilhar/i }));
    await expect
      .poll(() => page.evaluate(() => window.__shared ?? []), {
        message: "o Compartilhar não chamou o navigator.share",
      })
      .toHaveLength(1);
    const [shared] = await page.evaluate(() => window.__shared ?? []);
    expect(
      new URL(shared?.url ?? "", appURL).pathname,
      "o compartilhamento não leva o link do show",
    ).toBe(show.path);

    // Show cancelado: a página continua, com o selo.
    const cancelledPage = await page.goto(cancelled.path);
    expect(cancelledPage?.status()).toBe(200);
    await expect(
      page.getByRole("heading", { level: 1, name: cancelled.title }),
    ).toBeVisible();
    await expect(page.getByRole("main")).toContainText(/cancelado/i);
    await axe.check(page);
  });

  test("SPEC-0024:E2E-02 show inexistente dá 404 com o caminho de volta, e a página do show carrega sem erro de CSP [jornada: compartilhar-show]", async ({
    page,
    browser,
  }, testInfo) => {
    test.skip(
      testInfo.project.name !== "mobile-chrome",
      "verificação de status e CSP: mobile-chrome",
    );
    test.setTimeout(300_000);

    const missing = await page.goto(
      "/cidade-exemplo/show-que-nao-existe-2026-01-01",
    );
    expect(missing?.status()).toBe(404);
    await expect(
      page.getByRole("heading", { level: 1, name: "Show não encontrado" }),
    ).toBeVisible();
    const back = page.getByRole("link", {
      name: "Ver a agenda de Cidade Exemplo",
    });
    await expect(back).toHaveAttribute("href", "/cidade-exemplo");

    const { shows } = await publishShows(browser, "pagina02", (suffix) => [
      { title: `Noite da Peste ${suffix}` },
    ]);
    const [show] = shows;
    if (!show) {
      throw new Error("preparação sem o show");
    }
    const consoleErrors: string[] = [];
    page.on("console", (message) => {
      if (
        message.type() === "error" &&
        /content.security.policy/i.test(message.text())
      ) {
        consoleErrors.push(message.text());
      }
    });
    await page.addInitScript(() => {
      window.__showCspViolations = [];
      document.addEventListener("securitypolicyviolation", (event) => {
        window.__showCspViolations?.push(
          `${event.effectiveDirective || event.violatedDirective} bloqueou ${event.blockedURI || "inline"}`,
        );
      });
    });

    const opened = await page.goto(show.path);
    expect(opened?.status()).toBe(200);
    await expect(
      page.getByRole("heading", { level: 1, name: show.title }),
    ).toBeVisible();
    await page.waitForLoadState("load");
    expect(
      await page.evaluate(() => window.__showCspViolations ?? []),
      `violações de CSP em ${show.path}`,
    ).toEqual([]);
    expect(consoleErrors, `erros de CSP no console em ${show.path}`).toEqual(
      [],
    );
  });

  test("SPEC-0024:E2E-03 a página do show tem LCP até 2,5 s e o flyer com width e height [jornada: compartilhar-show]", async ({
    page,
    browser,
  }, testInfo) => {
    test.skip(
      testInfo.project.name !== "mobile-chrome",
      "medida de desempenho: mobile-chrome (PerformanceObserver de LCP)",
    );
    test.setTimeout(300_000);
    const { shows } = await publishShows(browser, "pagina03", (suffix) => [
      { title: `Noite da Peste ${suffix}` },
    ]);
    const [show] = shows;
    if (!show) {
      throw new Error("preparação sem o show");
    }
    await page.addInitScript(() => {
      window.__lcp = 0;
      window.__cls = 0;
      new PerformanceObserver((list) => {
        for (const entry of list.getEntries()) {
          window.__lcp = entry.startTime;
        }
      }).observe({ type: "largest-contentful-paint", buffered: true });
      new PerformanceObserver((list) => {
        for (const entry of list.getEntries() as (PerformanceEntry & {
          value: number;
          hadRecentInput: boolean;
        })[]) {
          if (!entry.hadRecentInput) {
            window.__cls = (window.__cls ?? 0) + entry.value;
          }
        }
      }).observe({ type: "layout-shift", buffered: true });
    });

    const opened = await page.goto(show.path);
    expect(opened?.status()).toBe(200);
    const flyer = page
      .getByRole("main")
      .getByRole("img", { name: `Flyer de ${show.title}` });
    await expect(flyer).toBeVisible();
    await page.waitForLoadState("load");
    await expect
      .poll(() => page.evaluate(() => window.__lcp ?? 0), {
        message: "o PerformanceObserver não registrou o LCP",
      })
      .toBeGreaterThan(0);

    const lcp = await page.evaluate(() => window.__lcp ?? 0);
    expect(
      lcp,
      `LCP de ${show.path}: ${Math.round(lcp)} ms`,
    ).toBeLessThanOrEqual(lcpBudgetMs);
    const cls = await page.evaluate(() => window.__cls ?? 0);
    expect(cls, `CLS de ${show.path}`).toBeLessThanOrEqual(clsBudget);
    for (const attribute of ["width", "height"]) {
      expect(
        Number(await flyer.getAttribute(attribute)),
        `a imagem do flyer sem ${attribute} (CLS)`,
      ).toBeGreaterThan(0);
    }
  });
});
