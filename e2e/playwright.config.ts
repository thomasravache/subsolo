import { defineConfig, devices } from "@playwright/test";

import {
  appURL,
  deadApiPort,
  externalApp,
  webWithoutApiPort,
  webWithoutApiURL,
} from "./tests/agenda/environment";
import {
  mailpitPort,
  plainSentinel,
  secretSentinel,
} from "./tests/auth/environment";

/**
 * SPEC-0005 — E2E com Playwright (ADR-0005): projetos de celular (Pixel 7, iPhone 14) e desktop.
 * O `baseURL` padrão é o servidor estático de e2e/static (páginas do harness).
 *
 * SPEC-0008 — as jornadas (tests/agenda) usam a aplicação completa em `BASE_URL` (padrão https://localhost:8443):
 * sem `BASE_URL`, o `webServer` sobe a pilha do Compose gerado pelo `aspire publish` com `tools/compose-up.sh`,
 * que fica em primeiro plano até o Playwright encerrá-lo (SIGTERM); o probe é a página inicial pelo Caddy; com `BASE_URL`
 * (smoke contra um ambiente já no ar), nada é subido. O IT-04 usa um Next.js de produção à parte, sem API.
 *
 * SPEC-0010 (Emenda 3) — o Caddy local serve HTTPS com o certificado `tls internal`, não confiável: `ignoreHTTPSErrors`
 * vale para todos os projetos e para o probe do webServer do Compose.
 *
 * SPEC-0016 — as jornadas de entrada (tests/auth) leem o link mágico na API do Mailpit (`MAILPIT_URL`); o
 * `compose-up.sh` recebe a porta do Mailpit e as sentinelas do ambiente do web (tests/auth/environment.ts).
 */
const port = Number(process.env.E2E_STATIC_PORT ?? 4317);
const baseURL = `http://127.0.0.1:${port}`;
const isCI = !!process.env.CI;

// SPEC-0009 — o E2E-01 consulta o Seq da pilha local, publicado só em 127.0.0.1 pelo override do compose-up.
const seqPort = Number(process.env.SEQ_PORT ?? 5341);
if (!externalApp) {
  process.env.SEQ_URL ??= `http://127.0.0.1:${seqPort}`;
  // SPEC-0016 — as jornadas de entrada leem o link mágico na API do Mailpit, publicada só em 127.0.0.1 pelo override.
  process.env.MAILPIT_URL ??= `http://127.0.0.1:${mailpitPort}`;
}

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
    // SPEC-0010 (Emenda 3): aceita o certificado local (`tls internal`) do Caddy.
    ignoreHTTPSErrors: true,
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
            // SPEC-0008 (Emenda 2): aspire publish + docker compose + Caddy (SPEC-0010: HTTPS em :8443).
            command: "../tools/compose-up.sh",
            // A página inicial (não o /health da API): espera também o web atrás do Caddy, sem 502 nos primeiros testes.
            url: `${appURL}/`,
            ignoreHTTPSErrors: true,
            reuseExistingServer: !isCI,
            timeout: 20 * 60_000,
            stdout: "pipe" as const,
            gracefulShutdown: { signal: "SIGTERM" as const, timeout: 60_000 },
            // SPEC-0016: Mailpit no localhost e as sentinelas do ambiente do web (E2E-04).
            env: {
              SEQ_PORT: String(seqPort),
              MAILPIT_PORT: String(mailpitPort),
              E2E_PLAIN_SENTINEL: plainSentinel,
              E2E_SECRET_SENTINEL: secretSentinel,
            },
          },
          {
            // SPEC-0008 IT-04 (Emenda 5): o web de produção (next build + next start) sem API (conexão recusada).
            command: `pnpm exec next build && pnpm exec next start --port ${webWithoutApiPort}`,
            cwd: "../web",
            url: webWithoutApiURL,
            reuseExistingServer: !isCI,
            timeout: 5 * 60_000,
            env: { API_URL: `http://127.0.0.1:${deadApiPort}` },
          },
        ]),
  ],
});
