import { defineConfig, devices } from "@playwright/test";

/**
 * SPEC-0005 — E2E com Playwright (ADR-0005): projetos de celular (Pixel 7, iPhone 14) e desktop.
 * O `webServer` serve e2e/static no `baseURL` (páginas do harness); as specs das jornadas trocam o
 * `baseURL`/`webServer` pela aplicação completa (Aspire/Compose).
 */
const port = Number(process.env.E2E_STATIC_PORT ?? 4317);
const baseURL = `http://127.0.0.1:${port}`;
const isCI = !!process.env.CI;

export default defineConfig({
  testDir: "./tests",
  fullyParallel: true,
  forbidOnly: isCI,
  // ADR-0005: nunca retry-até-passar; uma falha é uma falha.
  retries: 0,
  workers: isCI ? 1 : undefined,
  reporter: isCI ? [["list"], ["html", { open: "never" }]] : "list",
  use: {
    baseURL,
    // Artefatos para diagnosticar falhas (o CI publica test-results/).
    trace: "retain-on-failure",
    video: "retain-on-failure",
    screenshot: "only-on-failure",
  },
  projects: [
    { name: "mobile-chrome", use: { ...devices["Pixel 7"] } },
    { name: "mobile-safari", use: { ...devices["iPhone 14"] } },
    { name: "desktop-chrome", use: { ...devices["Desktop Chrome"] } },
  ],
  webServer: {
    command: "node static-server.mjs",
    url: `${baseURL}/harness/acessivel.html`,
    reuseExistingServer: !isCI,
    env: { E2E_STATIC_PORT: String(port) },
  },
});
