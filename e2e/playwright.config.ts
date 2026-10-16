import { defineConfig, devices } from "@playwright/test";

import {
  appURL,
  deadApiPort,
  externalApp,
  webWithoutApiPort,
  webWithoutApiURL,
} from "./tests/agenda/environment";

/**
 * SPEC-0005 — E2E com Playwright (ADR-0005): projetos de celular (Pixel 7, iPhone 14) e desktop.
 * O `baseURL` padrão é o servidor estático de e2e/static (páginas do harness).
 *
 * SPEC-0008 — as jornadas (tests/agenda) usam a aplicação completa em `BASE_URL` (padrão http://localhost:8080):
 * sem `BASE_URL`, o `webServer` sobe a pilha do Compose gerado pelo `aspire publish` com `tools/compose-up.sh`,
 * que fica em primeiro plano até o Playwright encerrá-lo (SIGTERM) e serve `/health` pelo Caddy; com `BASE_URL`
 * (smoke contra um ambiente já no ar), nada é subido. O IT-04 usa um Next.js à parte sem API.
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
  webServer: [
    {
      command: "node static-server.mjs",
      url: `${baseURL}/harness/acessivel.html`,
      reuseExistingServer: !isCI,
      env: { E2E_STATIC_PORT: String(port) },
    },
    ...(externalApp
      ? []
      : [
          {
            // SPEC-0008 (Emenda 2): aspire publish + docker compose + Caddy em :8080.
            command: "../tools/compose-up.sh",
            url: `${appURL}/health`,
            reuseExistingServer: !isCI,
            timeout: 20 * 60_000,
            stdout: "pipe" as const,
            gracefulShutdown: { signal: "SIGTERM" as const, timeout: 60_000 },
          },
          {
            // SPEC-0008 IT-04: o web sem API (conexão recusada em API_URL).
            command: `pnpm exec next dev --port ${webWithoutApiPort}`,
            cwd: "../web",
            url: webWithoutApiURL,
            reuseExistingServer: !isCI,
            timeout: 5 * 60_000,
            env: { API_URL: `http://127.0.0.1:${deadApiPort}` },
          },
        ]),
  ],
});
