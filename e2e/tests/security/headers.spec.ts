import type { APIResponse } from "@playwright/test";

import { expect, test } from "../fixtures";
import { appURL, externalApp, webWithoutApiURL } from "../agenda/environment";
import { parseCsp, scriptNonce } from "./csp";

/**
 * SPEC-0010 IT-05 — o servidor Next.js responde as páginas com os cabeçalhos de segurança do contrato:
 * `Content-Security-Policy` (`default-src 'self'`, `script-src 'self' 'nonce-…'` com um nonce novo a cada
 * requisição, `img-src 'self' data: blob:`, `frame-ancestors 'none'`), `X-Content-Type-Options: nosniff`,
 * `Referrer-Policy: strict-origin-when-cross-origin` e `Permissions-Policy: camera=(), microphone=(),
 * geolocation=()`. Verificado na aplicação completa (pelo Caddy, com o certificado local do Compose) e direto no
 * Next.js de produção sem API (o mesmo servidor do IT-04 da agenda), para provar que os cabeçalhos vêm do web.
 */
test.use({ baseURL: appURL, ignoreHTTPSErrors: true });

const pages = ["/cidade-exemplo", "/", "/cidade-inexistente"];

function expectSecurityHeaders(response: APIResponse): string {
  const headers = response.headers();
  const where = `${response.url()} (${response.status()})`;

  const csp = parseCsp(headers["content-security-policy"]);
  expect(csp.size, `${where}: sem Content-Security-Policy`).toBeGreaterThan(0);
  expect(csp.get("default-src"), where).toEqual(["'self'"]);
  expect(csp.get("script-src"), where).toContain("'self'");
  const nonce = scriptNonce(csp);
  expect(nonce, `${where}: script-src sem 'nonce-…'`).toBeTruthy();
  expect(csp.get("img-src"), where).toEqual(
    expect.arrayContaining(["'self'", "data:", "blob:"]),
  );
  expect(csp.get("frame-ancestors"), where).toEqual(["'none'"]);

  expect(headers["x-content-type-options"], where).toBe("nosniff");
  expect(headers["referrer-policy"], where).toBe(
    "strict-origin-when-cross-origin",
  );
  const permissions = (headers["permissions-policy"] ?? "")
    .split(",")
    .map((feature) => feature.trim());
  expect(permissions, where).toEqual(
    expect.arrayContaining(["camera=()", "microphone=()", "geolocation=()"]),
  );
  return nonce ?? "";
}

test.describe("cabeçalhos de segurança do web", () => {
  test.beforeEach(({}, testInfo) => {
    test.skip(
      testInfo.project.name !== "desktop-chrome",
      "verificação do servidor: basta um projeto",
    );
  });

  for (const path of pages) {
    test(`SPEC-0010:IT-05 ${path} responde com CSP com nonce e os demais cabeçalhos @smoke`, async ({
      request,
    }) => {
      const response = await request.get(path);

      expect(response.status()).toBeLessThan(500);
      expectSecurityHeaders(response);
    });
  }

  test("SPEC-0010:IT-05 o nonce do script-src muda a cada requisição", async ({
    request,
  }) => {
    const first = expectSecurityHeaders(await request.get("/cidade-exemplo"));
    const second = expectSecurityHeaders(await request.get("/cidade-exemplo"));

    expect(second).not.toBe(first);
  });

  test("SPEC-0010:IT-05 os cabeçalhos vêm do Next.js (servidor de produção sem API, sem o Caddy)", async ({
    request,
  }) => {
    test.skip(
      externalApp,
      "com BASE_URL o webServer não sobe o Next.js sem API",
    );

    const response = await request.get(`${webWithoutApiURL}/cidade-exemplo`);

    expect(response.status()).toBe(503);
    expectSecurityHeaders(response);
  });
});
