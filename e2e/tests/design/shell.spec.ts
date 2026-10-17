import { gzipSync } from "node:zlib";

import type { Locator, Page } from "@playwright/test";

import { expect, test } from "../fixtures";
import { appURL } from "../agenda/environment";

/**
 * SPEC-0011 — shell escuro do app (cabeçalho com seletor de cidade, `main`, rodapé com "Apoie" e redes; navegação
 * inferior no celular) aplicado à agenda `/{cidade}`, na aplicação completa (pilha do Compose, build de produção do
 * Next.js, subida pelo `webServer` do `playwright.config.ts`; ou `BASE_URL`).
 *
 * - IT-01: o HTML renderizado no servidor já traz o shell, e o CSS da página fica abaixo de 30 kB gzip.
 * - E2E-01: no celular e no desktop, o fã vê o shell escuro, troca de cidade pelo seletor do cabeçalho e chega à
 *   agenda da outra cidade, navega por teclado com o foco sempre visível, e o axe não acha violações.
 */
test.use({ baseURL: appURL });

const city = { slug: "cidade-exemplo", name: "Cidade Exemplo" };

/** Orçamento de CSS da página da agenda (RNF de desempenho). */
const cssBudgetBytes = 30_000;

/** Cores do design system aprovado: fundo #0B0B0C e texto #E8E2D4. */
const darkBackground = "rgb(11, 11, 12)";
const lightText = "rgb(232, 226, 212)";

/** Alvo de toque mínimo (RNF de acessibilidade). */
const minTouchTarget = 44;

/** Viewport a partir do qual o cabeçalho completo substitui a navegação inferior. */
const desktopBreakpoint = 768;

test.describe("shell do app — HTML do servidor", () => {
  test("SPEC-0011:IT-01 /cidade-exemplo vem do servidor com header, main e footer com Apoie, e CSS < 30 kB gzip", async ({
    request,
  }, testInfo) => {
    test.skip(
      testInfo.project.name !== "desktop-chrome",
      "verificação do HTML do servidor: basta um projeto",
    );

    const response = await request.get(`/${city.slug}`);
    expect(response.status()).toBe(200);
    const html = await response.text();

    expect(html, "HTML sem <header>").toMatch(/<header[\s>]/);
    expect(html, "HTML sem <main>").toMatch(/<main[\s>]/);
    const footer = /<footer[\s>][\s\S]*?<\/footer>/.exec(html)?.[0];
    expect(footer, "HTML sem <footer>").toBeDefined();
    expect(footer, "rodapé sem link para /apoie").toMatch(
      /<a\b[^>]*href="\/apoie"[^>]*>[\s\S]*?Apoie[\s\S]*?<\/a>/,
    );

    const stylesheets = [
      ...html.matchAll(/<link\b[^>]*rel="stylesheet"[^>]*>/g),
    ].flatMap((match) => {
      const href = /href="([^"]+)"/.exec(match[0])?.[1];
      return href ? [href.replace(/&amp;/g, "&")] : [];
    });
    const inline = [...html.matchAll(/<style\b[^>]*>([\s\S]*?)<\/style>/g)].map(
      (match) => match[1] ?? "",
    );
    expect(
      stylesheets.length + inline.length,
      "página sem CSS",
    ).toBeGreaterThan(0);

    const sizes: string[] = [];
    let total = 0;
    for (const href of stylesheets) {
      const css = await request.get(href);
      expect(css.status(), `CSS ${href}`).toBe(200);
      const gzipped = gzipSync(await css.body()).length;
      sizes.push(`${href}: ${gzipped} B`);
      total += gzipped;
    }
    for (const [index, css] of inline.entries()) {
      const gzipped = gzipSync(Buffer.from(css)).length;
      sizes.push(`<style> #${index + 1}: ${gzipped} B`);
      total += gzipped;
    }
    expect(total, `CSS gzip da agenda (${sizes.join("; ")})`).toBeLessThan(
      cssBudgetBytes,
    );
  });
});

/** Navegações visíveis da página, com a posição na janela. */
async function visibleNavigations(page: Page) {
  const boxes = [];
  for (const nav of await page.getByRole("navigation").all()) {
    if (await nav.isVisible()) {
      const box = await nav.boundingBox();
      if (box) boxes.push(box);
    }
  }
  return boxes;
}

/** Elementos interativos visíveis de uma região. */
async function interactive(region: Locator): Promise<Locator[]> {
  const all = await region
    .locator("a[href], button, select, input, [role=button], [tabindex='0']")
    .all();
  const visible: Locator[] = [];
  for (const element of all) {
    if (await element.isVisible()) visible.push(element);
  }
  return visible;
}

test.describe("shell do app — jornada", () => {
  test("SPEC-0011:E2E-01 fã vê o shell escuro, troca de cidade pelo seletor e navega por teclado com foco visível, sem violações do axe [jornada: agenda]", async ({
    page,
    axe,
    browserName,
    isMobile,
  }) => {
    const response = await page.goto(`/${city.slug}`);
    expect(response?.status()).toBe(200);
    await expect(
      page.getByRole("heading", { level: 1, name: `Agenda ${city.name}` }),
    ).toBeVisible();

    // Shell escuro do design system.
    const colors = await page.evaluate(() => {
      const style = getComputedStyle(document.body);
      return { background: style.backgroundColor, color: style.color };
    });
    expect(colors).toEqual({ background: darkBackground, color: lightText });

    const header = page.getByRole("banner");
    const footer = page.getByRole("contentinfo");
    await expect(header).toBeVisible();
    await expect(page.getByRole("main")).toBeVisible();
    await expect(footer.getByRole("link", { name: /apoie/i })).toHaveAttribute(
      "href",
      "/apoie",
    );
    await expect(
      footer.locator('a[href*="instagram.com"]').first(),
    ).toBeAttached();

    // Navegação inferior no celular; cabeçalho completo (sem navegação inferior) no desktop.
    const viewport = page.viewportSize();
    expect(viewport).not.toBeNull();
    const { width, height } = viewport as { width: number; height: number };
    const navigations = await visibleNavigations(page);
    const bottomNav = navigations.filter(
      (box) => box.y > height / 2 && box.y + box.height >= height - 2,
    );
    if (width < desktopBreakpoint) {
      expect(
        bottomNav.length,
        "navegação inferior fixa ausente no celular",
      ).toBeGreaterThan(0);
    } else {
      expect(bottomNav, "navegação inferior visível no desktop").toEqual([]);
    }

    const switcher = header.getByRole("combobox", { name: /cidade/i });
    await expect(switcher).toBeVisible();
    await expect(switcher).toHaveValue(city.slug);

    // Alvos de toque ≥ 44 px no celular (cabeçalho, navegação e rodapé).
    if (isMobile) {
      const small: string[] = [];
      for (const region of [
        header,
        footer,
        ...(await page.getByRole("navigation").all()),
      ]) {
        for (const element of await interactive(region)) {
          const box = await element.boundingBox();
          if (
            box &&
            (box.width < minTouchTarget || box.height < minTouchTarget)
          ) {
            const name =
              (await element.getAttribute("aria-label")) ??
              (await element.innerText()).trim();
            small.push(
              `"${name}" ${box.width.toFixed(0)}×${box.height.toFixed(0)}`,
            );
          }
        }
      }
      expect(small, "alvos de toque menores que 44 px").toEqual([]);
    }

    await axe.check(page);

    // Teclado: todo elemento que recebe foco mostra o indicador (WebKit não leva o Tab a links por padrão).
    if (browserName === "chromium") {
      const withoutIndicator: string[] = [];
      let focused = 0;
      for (let i = 0; i < 25; i++) {
        await page.keyboard.press("Tab");
        const focus = await page.evaluate(() => {
          const element = document.activeElement as HTMLElement | null;
          if (!element || element === document.body) return null;
          const style = getComputedStyle(element);
          const outline =
            style.outlineStyle !== "none" && parseFloat(style.outlineWidth) > 0;
          const ring = style.boxShadow !== "none";
          return {
            label:
              element.getAttribute("aria-label") ??
              element.textContent?.trim().slice(0, 40) ??
              element.tagName,
            visible: outline || ring,
          };
        });
        if (!focus) continue;
        focused++;
        if (!focus.visible) withoutIndicator.push(focus.label);
      }
      expect(focused, "nenhum elemento recebeu foco pelo Tab").toBeGreaterThan(
        0,
      );
      expect(withoutIndicator, "foco sem indicador visível").toEqual([]);
    }

    // Troca de cidade pelo seletor do cabeçalho.
    const options = await switcher.locator("option").evaluateAll((elements) =>
      elements.map((option) => ({
        slug: (option as HTMLOptionElement).value,
        name: (option.textContent ?? "").trim(),
      })),
    );
    const other = options.find((option) => option.slug !== city.slug);
    expect(
      other,
      `seletor de cidade sem outra cidade além de ${city.slug}: ${JSON.stringify(options)}`,
    ).toBeDefined();
    const target = other as { slug: string; name: string };

    await switcher.selectOption(target.slug);

    await expect(page).toHaveURL(new RegExp(`/${target.slug}$`));
    await expect(
      page.getByRole("heading", { level: 1, name: `Agenda ${target.name}` }),
    ).toBeVisible();
    await expect(
      page.getByRole("banner").getByRole("combobox", { name: /cidade/i }),
    ).toHaveValue(target.slug);
    await axe.check(page);
  });
});
