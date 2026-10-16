/**
 * SPEC-0008 — endereços das jornadas da agenda, lidos pelo `playwright.config.ts` (webServer) e pelos testes.
 *
 * - `BASE_URL` (padrão `http://localhost:8080`): a aplicação completa — Caddy na frente da API e do web, a mesma
 *   pilha do Compose gerado pelo `aspire publish`. Sem `BASE_URL`, o `webServer` sobe a pilha local com
 *   `tools/compose-up.sh`; com `BASE_URL` (ex.: smoke contra um ambiente já no ar), nada é subido.
 * - IT-04: um servidor Next.js à parte, com `API_URL` apontando para uma porta sem serviço.
 */
export const externalApp = !!process.env.BASE_URL;

export const appURL = process.env.BASE_URL || "http://localhost:8080";

export const webWithoutApiPort = Number(
  process.env.E2E_WEB_WITHOUT_API_PORT ?? 4318,
);

/** Porta sem serviço: o Next.js tenta falar com a API aqui e recebe conexão recusada. */
export const deadApiPort = Number(process.env.E2E_DEAD_API_PORT ?? 4319);

export const webWithoutApiURL = `http://127.0.0.1:${webWithoutApiPort}`;
