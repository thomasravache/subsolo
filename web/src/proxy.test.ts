// @vitest-environment node
import { NextRequest } from "next/server";
import { afterEach, describe, expect, it, vi } from "vitest";

import { proxy } from "@/proxy";

/**
 * SPEC-0010 UT-02 — o proxy (ADR-0016) gera a CSP de cada requisição: o `script-src` traz `'self'` e um nonce
 * novo (diferente a cada requisição) e a política tem `frame-ancestors 'none'`; o mesmo nonce vai para a
 * requisição repassada ao Next.js (cabeçalho `Content-Security-Policy` da requisição), que o aplica nos scripts.
 */
function directives(header: string | null): Map<string, string[]> {
  const map = new Map<string, string[]>();
  for (const part of (header ?? "").split(";")) {
    const [name, ...sources] = part.trim().split(/\s+/);
    if (name) {
      map.set(name.toLowerCase(), sources);
    }
  }
  return map;
}

function nonceOf(header: string | null): string | undefined {
  return (directives(header).get("script-src") ?? [])
    .map((source) => /^'nonce-([A-Za-z0-9+/_=-]+)'$/.exec(source)?.[1])
    .find((value) => value !== undefined);
}

async function cspFor(path: string): Promise<string | null> {
  const response = await proxy(new NextRequest(`http://localhost${path}`));
  return response.headers.get("content-security-policy");
}

describe("CSP do proxy", () => {
  it("SPEC-0010:UT-02 script-src com 'self' e nonce, e frame-ancestors 'none'", async () => {
    const csp = await cspFor("/");

    const parsed = directives(csp);
    expect(parsed.get("script-src")).toContain("'self'");
    expect(nonceOf(csp)).toBeTruthy();
    expect(parsed.get("frame-ancestors")).toEqual(["'none'"]);
  });

  it("SPEC-0010:UT-02 o nonce é novo a cada requisição", async () => {
    const nonces = new Set<string | undefined>();
    for (let i = 0; i < 5; i++) {
      nonces.add(nonceOf(await cspFor("/")));
    }

    expect(nonces.has(undefined)).toBe(false);
    expect(nonces.size).toBe(5);
  });
});

/**
 * SPEC-0016 UT-04 (navegação conforme a sessão) — as páginas de entrar e da conta não são tratadas como a agenda de
 * uma cidade pelo proxy: `/entrar`, `/entrar/confirmar` e `/conta` seguem para o Next.js sem consultar a API de
 * agenda e sem a reescrita com 404 de "cidade não encontrada"; uma cidade de verdade continua passando pela API.
 */
describe("proxy e as páginas de entrar e da conta", () => {
  function stubAgendaApi() {
    vi.stubEnv("API_URL", "http://api.test");
    return vi.spyOn(globalThis, "fetch").mockImplementation(async () =>
      Response.json(
        { status: 404, code: "events.city_not_found" },
        {
          status: 404,
          headers: { "content-type": "application/problem+json" },
        },
      ),
    );
  }

  afterEach(() => {
    vi.unstubAllEnvs();
  });

  it.each(["/entrar", "/entrar/confirmar", "/conta"])(
    "SPEC-0016:UT-04 %s não é tratado como cidade",
    async (path) => {
      const fetch = stubAgendaApi();

      const response = await proxy(
        new NextRequest(`http://localhost${path}?token=abc`),
      );

      expect(fetch, `${path} consultou a API de agenda`).not.toHaveBeenCalled();
      expect(response.status).toBe(200);
      expect(response.headers.get("x-middleware-rewrite")).toBeNull();
    },
  );

  it("SPEC-0016:UT-04 uma cidade inexistente continua com o 404 da agenda", async () => {
    const fetch = stubAgendaApi();

    const response = await proxy(
      new NextRequest("http://localhost/cidade-inexistente"),
    );

    expect(fetch).toHaveBeenCalled();
    expect(response.status).toBe(404);
  });
});

/**
 * SPEC-0017 E2E-02 (no nível do proxy, ADR-0016) — `/admin/cidades-e-estilos` tem o status decidido pelo proxy, com a
 * sessão de `GET /api/v1/me` (cookie da requisição repassado): anônimo → 307 para
 * `/entrar?returnTo=/admin/cidades-e-estilos`; fã → 403; admin → segue (200). Sempre com `Cache-Control: no-store`
 * e sem consultar a API de agenda.
 */
describe("proxy e o painel do admin", () => {
  const adminPath = "/admin/cidades-e-estilos";
  const sessionCookie = "__Host-subsolo=sessao-de-teste";

  function urlOf(input: Parameters<typeof fetch>[0]): string {
    return typeof input === "string"
      ? input
      : input instanceof URL
        ? input.href
        : input.url;
  }

  /** `/api/v1/me` responde com `roles` (ou 401 sem sessão); qualquer outra rota da API responde 500. */
  function stubMe(roles: string[] | null) {
    vi.stubEnv("API_URL", "http://api.test");
    return vi.spyOn(globalThis, "fetch").mockImplementation(async (input) => {
      const url = urlOf(input);
      if (!url.includes("/api/v1/me")) {
        return new Response(null, { status: 500 });
      }
      return roles
        ? Response.json({
            id: "00000000-0000-0000-0000-000000000001",
            email: "pessoa@subsolo.test",
            displayName: null,
            roles,
          })
        : Response.json(
            { status: 401, code: "auth.required" },
            {
              status: 401,
              headers: { "content-type": "application/problem+json" },
            },
          );
    });
  }

  function request(cookie?: string) {
    return new NextRequest(`http://localhost${adminPath}`, {
      headers: cookie ? { cookie } : {},
    });
  }

  function agendaCalls(fetch: ReturnType<typeof stubMe>): string[] {
    return fetch.mock.calls
      .map(([input]) => urlOf(input))
      .filter((url) => url.includes("/agenda"));
  }

  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllEnvs();
  });

  it("SPEC-0017:E2E-02 anônimo é redirecionado (307) para /entrar com o returnTo do painel", async () => {
    const fetch = stubMe(null);

    const response = await proxy(request());

    expect(response.status).toBe(307);
    const location = new URL(
      response.headers.get("location") ?? "",
      "http://localhost",
    );
    expect(location.pathname).toBe("/entrar");
    expect(location.searchParams.get("returnTo")).toBe(adminPath);
    expect(response.headers.get("cache-control")).toContain("no-store");
    expect(agendaCalls(fetch)).toEqual([]);
  });

  it("SPEC-0017:E2E-02 sessão expirada (401 em /me) também vai para /entrar", async () => {
    stubMe(null);

    const response = await proxy(request(sessionCookie));

    expect(response.status).toBe(307);
    expect(
      new URL(response.headers.get("location") ?? "", "http://localhost")
        .pathname,
    ).toBe("/entrar");
  });

  it("SPEC-0017:E2E-02 fã logado recebe 403", async () => {
    const fetch = stubMe(["fan"]);

    const response = await proxy(request(sessionCookie));

    expect(fetch).toHaveBeenCalled();
    expect(response.status).toBe(403);
    expect(response.headers.get("location")).toBeNull();
    expect(response.headers.get("cache-control")).toContain("no-store");
    expect(agendaCalls(fetch)).toEqual([]);
  });

  it("SPEC-0017:E2E-02 admin segue para a página (200) sem cache", async () => {
    const fetch = stubMe(["fan", "admin"]);

    const response = await proxy(request(sessionCookie));

    expect(fetch).toHaveBeenCalled();
    expect(response.status).toBe(200);
    expect(response.headers.get("location")).toBeNull();
    expect(response.headers.get("cache-control")).toContain("no-store");
    expect(agendaCalls(fetch)).toEqual([]);
  });
});
