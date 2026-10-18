/**
 * SPEC-0016 — endereços e valores das jornadas de entrada, lidos pelo `playwright.config.ts` (webServer) e pelos testes.
 *
 * - `MAILPIT_URL` (padrão `http://127.0.0.1:${MAILPIT_PORT:-8025}`): a API HTTP do Mailpit da pilha local, publicada só
 *   no localhost pelo override do `tools/compose-up.sh`; o E2E lê o link mágico de lá.
 * - Sentinelas do E2E-04: valores que o `compose-up.sh` põe no ambiente do contêiner `web` (uma variável comum e uma
 *   com nome de segredo); nenhum deles pode aparecer no HTML nem nos scripts servidos ao navegador.
 */
export const mailpitPort = Number(process.env.MAILPIT_PORT ?? 8025);

export const mailpitURL =
  process.env.MAILPIT_URL || `http://127.0.0.1:${mailpitPort}`;

export const plainSentinel =
  process.env.E2E_PLAIN_SENTINEL || "subsolo-sentinela-comum-e2e04";

export const secretSentinel =
  process.env.E2E_SECRET_SENTINEL || "subsolo-sentinela-segredo-e2e04";

/** Nome do projeto do Compose local (`tools/compose-up.sh`), para parar e religar o Mailpit no E2E-02. */
export const composeProject = "subsolo-local";

/**
 * SPEC-0020 — o provedor OpenID Connect falso (mock-oauth2-server) da pilha local, no papel do Google: o
 * `tools/compose-up.sh` o publica em 127.0.0.1:${OIDC_PORT:-8090} (endereço IP: o HSTS do Caddy para `localhost` não
 * vale), com o emissor `/default` igual para o navegador e para a API.
 */
export const oidcPort = Number(process.env.OIDC_PORT ?? 8090);

export const oidcIssuer = `http://127.0.0.1:${oidcPort}/default`;
