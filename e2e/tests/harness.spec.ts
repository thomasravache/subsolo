import { devices } from "@playwright/test";

import { expect, test } from "./fixtures";

/**
 * SPEC-0005 E2E-01 — o harness E2E roda nos projetos mobile (Pixel 7, iPhone 14) e desktop, e o fixture `axe`
 * aprova a página estática correta e reprova a página com contraste insuficiente (violação serious).
 * As páginas ficam em e2e/static/harness e são servidas pelo `webServer` do Playwright no `baseURL`.
 */

const expectedProjects = {
  "mobile-chrome": { device: devices["Pixel 7"], isMobile: true },
  "mobile-safari": { device: devices["iPhone 14"], isMobile: true },
  "desktop-chrome": { device: devices["Desktop Chrome"], isMobile: false },
} as const;

type ProjectName = keyof typeof expectedProjects;

const isExpectedProject = (name: string): name is ProjectName =>
  Object.hasOwn(expectedProjects, name);

test.describe("harness E2E e acessibilidade", () => {
  test("SPEC-0005:E2E-01 roda nos projetos mobile-chrome (Pixel 7), mobile-safari (iPhone 14) e desktop-chrome", async ({
    browserName,
    isMobile,
    page,
  }, testInfo) => {
    expect(
      testInfo.config.projects.map((project) => project.name).sort(),
    ).toEqual(Object.keys(expectedProjects).sort());

    const name = testInfo.project.name;
    expect(isExpectedProject(name), `projeto inesperado: "${name}"`).toBe(true);
    if (!isExpectedProject(name)) {
      return;
    }

    const expected = expectedProjects[name];
    expect(browserName).toBe(expected.device.defaultBrowserType);
    expect(isMobile).toBe(expected.isMobile);
    expect(page.viewportSize()).toEqual(expected.device.viewport);
  });

  test("SPEC-0005:E2E-01 página sem violações passa no axe.check", async ({
    page,
    axe,
  }) => {
    await page.goto("/harness/acessivel.html");
    await expect(
      page.getByRole("heading", { level: 1, name: "Agenda de exemplo" }),
    ).toBeVisible();

    await axe.check(page);
  });

  test("SPEC-0005:E2E-01 página com contraste insuficiente falha no axe.check nomeando a regra", async ({
    page,
    axe,
  }) => {
    await page.goto("/harness/contraste-insuficiente.html");
    await expect(
      page.getByRole("heading", { level: 1, name: "Agenda de exemplo" }),
    ).toBeVisible();

    await expect(axe.check(page)).rejects.toThrow(/color-contrast/);
  });
});
