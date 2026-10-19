import AxeBuilder from "@axe-core/playwright";

import { expect, test } from "../fixtures";
import { card, press } from "../shows/journey";
import { publishShows } from "../shows/show-page";
import { appURL } from "./environment";

/**
 * SPEC-0024 E2E-04 (herança da SPEC-0015) — o card do show da agenda é um `next/link`, e é a primeira página interna
 * 200 alcançável por navegação no cliente: um `MutationObserver` registra todo `document.title` durante o toque no
 * card; nenhum valor fica vazio, o título final é o da página do show ("{título} · {cidade} · Subsolo") e a regra
 * `document-title` do axe passa logo depois da troca de URL.
 */
test.use({ baseURL: appURL, ignoreHTTPSErrors: true });

declare global {
  interface Window {
    __titles?: string[];
  }
}

test.describe("título na navegação da agenda para o show", () => {
  test("SPEC-0024:E2E-04 o título nunca fica vazio ao tocar no card da agenda [jornada: agenda]", async ({
    page,
    browser,
  }, testInfo) => {
    test.skip(
      testInfo.project.name === "desktop-chrome",
      "jornada do celular (mobile-chrome e mobile-safari)",
    );
    test.setTimeout(300_000);
    const { producer, shows } = await publishShows(
      browser,
      "titulo01",
      (suffix) => [{ title: `Noite da Peste ${suffix}` }],
    );
    const [show] = shows;
    if (!show) {
      throw new Error("preparação sem o show");
    }
    await page.addInitScript(() => {
      window.__titles = [];
      new MutationObserver(() => {
        window.__titles?.push(document.title);
      }).observe(document, {
        subtree: true,
        childList: true,
        characterData: true,
      });
    });

    const agenda = await page.goto(`/${producer.city.slug}`);
    expect(agenda?.status()).toBe(200);
    const link = card(page.locator("main"), show.title).getByRole("link", {
      name: show.title,
    });
    await expect(link).toHaveAttribute("href", show.path);
    await page.evaluate(() => {
      window.__titles = [document.title];
    });

    await press(link);
    await page.waitForURL((url) => url.pathname === show.path);
    const titleCheck = await new AxeBuilder({ page })
      .withRules(["document-title"])
      .analyze();
    expect(
      titleCheck.violations.map((violation) => violation.id),
      "axe document-title logo depois da troca de URL",
    ).toEqual([]);

    const expected = `${show.title} · ${producer.city.name} · Subsolo`;
    await expect(page).toHaveTitle(expected);
    const titles = await page.evaluate(() => window.__titles ?? []);
    expect(
      titles.filter((title) => title.trim() === ""),
      `títulos vazios durante a navegação: ${JSON.stringify(titles)}`,
    ).toEqual([]);
    expect(titles.at(-1)).toBe(expected);
  });
});
