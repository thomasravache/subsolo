// @vitest-environment node
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

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

/**
 * SPEC-0024 IT-03 e IT-04 (no nível do proxy, ADR-0016) — a página do show `/{cidade}/{show}` tem o status decidido
 * pelo proxy, com `GET /api/v1/cities/{city}/shows/{slug}`, como a agenda da cidade: show encontrado → segue (200);
 * show ou cidade inexistente (404 da API) → 404 real; API fora do ar (conexão recusada ou 5xx) → 503. As páginas de
 * dois segmentos que não são show (`/conta/shows`, `/entrar/confirmar`) não consultam a API do show.
 */
describe("proxy e a página do show", () => {
  const city = "cidade-exemplo";
  const slug = "noite-da-peste-ii-2026-11-14";
  const showPagePath = `/${city}/${slug}`;
  const detailRoute = `/api/v1/cities/${city}/shows/${slug}`;

  function urlOf(input: Parameters<typeof fetch>[0]): string {
    return typeof input === "string"
      ? input
      : input instanceof URL
        ? input.href
        : input.url;
  }

  function problem(status: number, code: string): Response {
    return Response.json(
      { status, code },
      { status, headers: { "content-type": "application/problem+json" } },
    );
  }

  /** O detalhe do show responde com `answer`; o resto da API, 500 (o proxy não deveria precisar dela). */
  function stubDetail(answer: () => Response | Promise<Response>) {
    vi.stubEnv("API_URL", "http://api.test");
    return vi
      .spyOn(globalThis, "fetch")
      .mockImplementation(async (input) =>
        urlOf(input).includes("/shows/")
          ? answer()
          : new Response(null, { status: 500 }),
      );
  }

  function detailCalls(fetch: ReturnType<typeof stubDetail>): string[] {
    return fetch.mock.calls
      .map(([input]) => urlOf(input))
      .filter((url) => url.includes("/shows/"));
  }

  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllEnvs();
  });

  it("SPEC-0024:IT-03 show encontrado segue para a página (200), pela rota do detalhe", async () => {
    const fetch = stubDetail(() =>
      Response.json({ id: "3f1c2b8e-6a4d-4e7f-9b21-8d5c0e9a1f42", slug }),
    );

    const response = await proxy(
      new NextRequest(`http://localhost${showPagePath}`),
    );

    expect(detailCalls(fetch).map((url) => new URL(url).pathname)).toContain(
      detailRoute,
    );
    expect(response.status).toBe(200);
    expect(response.headers.get("x-middleware-rewrite")).toBeNull();
  });

  it.each(["events.show_not_found", "events.city_not_found"])(
    "SPEC-0024:IT-03 %s da API vira 404 real",
    async (code) => {
      stubDetail(() => problem(404, code));

      const response = await proxy(
        new NextRequest(`http://localhost${showPagePath}`),
      );

      expect(response.status).toBe(404);
    },
  );

  it("SPEC-0024:IT-04 API fora do ar (conexão recusada) vira 503", async () => {
    stubDetail(() => Promise.reject(new TypeError("fetch failed")));

    const response = await proxy(
      new NextRequest(`http://localhost${showPagePath}`),
    );

    expect(response.status).toBe(503);
  });

  it("SPEC-0024:IT-04 API com erro 5xx vira 503", async () => {
    stubDetail(() => problem(503, "service.unavailable"));

    const response = await proxy(
      new NextRequest(`http://localhost${showPagePath}`),
    );

    expect(response.status).toBe(503);
  });

  it.each(["/conta/shows", "/entrar/confirmar"])(
    "SPEC-0024:IT-03 %s não é tratado como show",
    async (path) => {
      const fetch = stubDetail(() => problem(404, "events.show_not_found"));

      const response = await proxy(new NextRequest(`http://localhost${path}`));

      expect(detailCalls(fetch), `${path} consultou a API do show`).toEqual([]);
      expect(response.status).not.toBe(404);
    },
  );
});

/**
 * SPEC-0026 — as páginas dos perfis `/banda/{slug}`, `/casa/{slug}` e `/produtor/{slug}` no proxy (ADR-0016):
 * - H24-05 (revisão da SPEC-0024): o proxy tem uma lista explícita de primeiros segmentos reservados (`banda`, `casa`,
 *   `produtor`, `conta`, `admin`, `entrar`): nenhum caminho de dois segmentos que comece por eles é tratado como show
 *   (a API do detalhe do show não é consultada);
 * - IT-04 (no nível do proxy): o status vem de `GET /api/v1/profiles/{type}/{slug}` (`banda` → `band`, `casa` →
 *   `venue`, `produtor` → `producer`): perfil encontrado → segue (200); inexistente, pendente, recusado ou do outro tipo
 *   (`/casa/{slug-de-banda}`) → 404 real;
 * - IT-05 (no nível do proxy): com o endpoint de shows fora do ar, a página do perfil continua 200 (o proxy decide só
 *   pelo perfil); com a API de perfis fora do ar (conexão recusada ou 5xx), 503 (Emenda v2, d).
 */
describe("proxy e as páginas dos perfis", () => {
  const profile = {
    id: "0199f0c1-6a4d-7e7f-9b21-8d5c0e9a1f42",
    type: "band",
    name: "Podridão",
    slug: "podridao",
    city: { slug: "curitiba", name: "Curitiba" },
    styles: [],
    links: {},
  };

  function urlOf(input: Parameters<typeof fetch>[0]): URL {
    return new URL(
      typeof input === "string"
        ? input
        : input instanceof URL
          ? input.href
          : input.url,
    );
  }

  function problem(status: number, code: string): Response {
    return Response.json(
      { status, code },
      { status, headers: { "content-type": "application/problem+json" } },
    );
  }

  /**
   * Só a banda `podridao` existe (`GET /api/v1/profiles/band/podridao`); os outros perfis dão 404; o detalhe de show e a
   * lista de shows do perfil respondem 500 (o proxy não deveria precisar deles); `/me` responde anônimo.
   */
  function stubApi() {
    vi.stubEnv("API_URL", "http://api.test");
    return vi.spyOn(globalThis, "fetch").mockImplementation(async (input) => {
      const { pathname } = urlOf(input);
      if (pathname === "/api/v1/profiles/band/podridao") {
        return Response.json(profile);
      }
      if (pathname.startsWith("/api/v1/profiles/")) {
        return problem(404, "profiles.not_found");
      }
      if (pathname === "/api/v1/me") {
        return problem(401, "auth.required");
      }
      return new Response(null, { status: 500 });
    });
  }

  function showDetailCalls(fetch: ReturnType<typeof stubApi>): string[] {
    return fetch.mock.calls
      .map(([input]) => urlOf(input).pathname)
      .filter((pathname) =>
        /^\/api\/v1\/cities\/[^/]+\/shows\//.test(pathname),
      );
  }

  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllEnvs();
  });

  it.each([
    "/banda/podridao",
    "/casa/porao-do-rock",
    "/produtor/peste-producoes",
    "/conta/perfis",
    "/admin/fila",
    "/entrar/confirmar",
  ])(
    "SPEC-0026:H24-05 %s não é tratado como show (primeiro segmento reservado)",
    async (path) => {
      const fetch = stubApi();

      await proxy(new NextRequest(`http://localhost${path}`));

      expect(showDetailCalls(fetch), `${path} consultou a API do show`).toEqual(
        [],
      );
    },
  );

  it("SPEC-0026:IT-04 perfil encontrado segue para a página (200), pela rota do perfil", async () => {
    const fetch = stubApi();

    const response = await proxy(
      new NextRequest("http://localhost/banda/podridao"),
    );

    expect(fetch.mock.calls.map(([input]) => urlOf(input).pathname)).toContain(
      "/api/v1/profiles/band/podridao",
    );
    expect(response.status).toBe(200);
    expect(response.headers.get("x-middleware-rewrite")).toBeNull();
  });

  it.each([
    ["/banda/inexistente", "/api/v1/profiles/band/inexistente"],
    ["/casa/podridao", "/api/v1/profiles/venue/podridao"],
    ["/produtor/podridao", "/api/v1/profiles/producer/podridao"],
  ])("SPEC-0026:IT-04 %s dá 404 real", async (path, route) => {
    const fetch = stubApi();

    const response = await proxy(new NextRequest(`http://localhost${path}`));

    expect(fetch.mock.calls.map(([input]) => urlOf(input).pathname)).toContain(
      route,
    );
    expect(response.status).toBe(404);
  });

  it("SPEC-0026:IT-05 API de perfis fora do ar (conexão recusada) vira 503", async () => {
    vi.stubEnv("API_URL", "http://api.test");
    vi.spyOn(globalThis, "fetch").mockImplementation(async () => {
      throw new TypeError("fetch failed");
    });

    const response = await proxy(
      new NextRequest("http://localhost/banda/podridao"),
    );

    expect(response.status).toBe(503);
  });

  it("SPEC-0026:IT-05 API de perfis com erro 5xx vira 503", async () => {
    vi.stubEnv("API_URL", "http://api.test");
    vi.spyOn(globalThis, "fetch").mockImplementation(async () =>
      problem(503, "service.unavailable"),
    );

    const response = await proxy(
      new NextRequest("http://localhost/casa/porao-do-rock"),
    );

    expect(response.status).toBe(503);
  });

  it("SPEC-0026:IT-05 com a API de shows fora do ar, a página do perfil continua 200", async () => {
    stubApi();

    const response = await proxy(
      new NextRequest("http://localhost/banda/podridao"),
    );

    expect(response.status).toBe(200);
  });
});

/**
 * SPEC-0027 — a aba "Seguindo" e os itens herdados da revisão da SPEC-0026 no proxy (ADR-0016):
 * - E2E-02 (no nível do proxy): `/seguindo` é uma página do site, não a agenda de uma cidade: segue para o Next.js sem
 *   consultar a API de agenda e sem o 404 de "cidade não encontrada", com `Cache-Control: no-store` (o que o fã segue é
 *   dado pessoal, como `/conta`); a página manda o anônimo para `/entrar?returnTo=/seguindo`;
 * - H26-06: com a API de perfis fora do ar (conexão recusada ou 5xx), o 503 vem da consulta de
 *   `GET /api/v1/profiles/{type}/{slug}` (a rota é de fato chamada);
 * - H26-08: `/apoie/{x}` não é tratado como show, e todo slug reservado do backend (`City.ReservedSlugs`, SPEC-0017) é
 *   um primeiro segmento que o proxy nunca trata como a cidade de um show.
 */
describe("proxy, a aba Seguindo e os segmentos reservados", () => {
  function urlOf(input: Parameters<typeof fetch>[0]): URL {
    return new URL(
      typeof input === "string"
        ? input
        : input instanceof URL
          ? input.href
          : input.url,
    );
  }

  function problem(status: number, code: string): Response {
    return Response.json(
      { status, code },
      { status, headers: { "content-type": "application/problem+json" } },
    );
  }

  /** Toda rota da API responde 404 (cidade, show e perfil inexistentes); `/me` responde anônimo. */
  function stubNotFoundApi() {
    vi.stubEnv("API_URL", "http://api.test");
    return vi
      .spyOn(globalThis, "fetch")
      .mockImplementation(async (input) =>
        urlOf(input).pathname === "/api/v1/me"
          ? problem(401, "auth.required")
          : problem(404, "events.city_not_found"),
      );
  }

  function calledPaths(fetch: ReturnType<typeof stubNotFoundApi>): string[] {
    return fetch.mock.calls.map(([input]) => urlOf(input).pathname);
  }

  /** Os slugs de `City.ReservedSlugs` no domínio do Events (SPEC-0017). */
  function backendReservedSlugs(): string[] {
    const source = readFileSync(
      fileURLToPath(
        new URL(
          "../../src/Modules/Events/Subsolo.Modules.Events.Domain/Cities/City.cs",
          import.meta.url,
        ),
      ),
      "utf8",
    );
    const block = /ReservedSlugs\s*=\s*new\[\]\s*\{([\s\S]*?)\}/.exec(
      source,
    )?.[1];
    expect(block, "City.ReservedSlugs não encontrado no backend").toBeDefined();
    return [...(block ?? "").matchAll(/"([^"]+)"/g)].map(
      (match) => match[1] ?? "",
    );
  }

  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllEnvs();
  });

  it("SPEC-0027:E2E-02 /seguindo não é tratado como cidade e sai sem cache", async () => {
    const fetch = stubNotFoundApi();

    const response = await proxy(new NextRequest("http://localhost/seguindo"));

    expect(
      calledPaths(fetch).filter((path) => path.startsWith("/api/v1/cities/")),
      "/seguindo consultou a API de agenda",
    ).toEqual([]);
    expect(response.status).toBe(200);
    expect(response.headers.get("x-middleware-rewrite")).toBeNull();
    expect(response.headers.get("cache-control")).toBe("no-store");
  });

  it("SPEC-0027:H26-06 API de perfis fora do ar (conexão recusada) vira 503 depois de consultar o perfil", async () => {
    vi.stubEnv("API_URL", "http://api.test");
    const fetch = vi.spyOn(globalThis, "fetch").mockImplementation(async () => {
      throw new TypeError("fetch failed");
    });

    const response = await proxy(
      new NextRequest("http://localhost/banda/podridao"),
    );

    expect(response.status).toBe(503);
    expect(fetch.mock.calls.map(([input]) => urlOf(input).pathname)).toContain(
      "/api/v1/profiles/band/podridao",
    );
  });

  it("SPEC-0027:H26-06 API de perfis com erro 5xx vira 503 depois de consultar o perfil", async () => {
    vi.stubEnv("API_URL", "http://api.test");
    const fetch = vi
      .spyOn(globalThis, "fetch")
      .mockImplementation(async () => problem(503, "service.unavailable"));

    const response = await proxy(
      new NextRequest("http://localhost/casa/porao-do-rock"),
    );

    expect(response.status).toBe(503);
    expect(fetch.mock.calls.map(([input]) => urlOf(input).pathname)).toContain(
      "/api/v1/profiles/venue/porao-do-rock",
    );
  });

  it("SPEC-0027:H26-08 /apoie/{x} não é tratado como show", async () => {
    const fetch = stubNotFoundApi();

    await proxy(new NextRequest("http://localhost/apoie/obrigado"));

    expect(
      calledPaths(fetch).filter((path) =>
        /^\/api\/v1\/cities\/[^/]+\/shows\//.test(path),
      ),
      "/apoie/obrigado consultou a API do show",
    ).toEqual([]);
  });

  it("SPEC-0027:H26-08 todo slug reservado do backend (City.ReservedSlugs) é um primeiro segmento que nunca vira show", async () => {
    const reserved = backendReservedSlugs();
    expect(reserved).toEqual(expect.arrayContaining(["apoie", "seguindo"]));

    const treatedAsShow: string[] = [];
    for (const slug of reserved) {
      const fetch = stubNotFoundApi();
      const path = `/${slug}/noite-da-peste-2026-11-14`;
      await proxy(new NextRequest(`http://localhost${path}`));
      if (
        calledPaths(fetch).some((called) =>
          called.startsWith(`/api/v1/cities/${slug}/shows/`),
        )
      ) {
        treatedAsShow.push(path);
      }
      vi.restoreAllMocks();
    }

    expect(
      treatedAsShow,
      "caminhos com primeiro segmento reservado no backend tratados como show",
    ).toEqual([]);
  });
});

/**
 * SPEC-0028 — as páginas institucionais `/apoie`, `/privacidade` e `/termos` são páginas do site (slugs reservados,
 * SPEC-0017), não a agenda de uma cidade:
 * - E2E-02 (no nível do proxy): seguem para o Next.js sem consultar a API de agenda e sem a reescrita com o 404 de
 *   "cidade não encontrada" (os links do rodapé, hoje 404, passam a responder 200);
 * - IT-03 (no nível do proxy): com a API fora do ar, `/apoie` não vira o 503 da agenda (a página sai 200 com o Pix e o
 *   aviso da lista).
 */
describe("proxy e as páginas institucionais", () => {
  function urlOf(input: Parameters<typeof fetch>[0]): URL {
    return new URL(
      typeof input === "string"
        ? input
        : input instanceof URL
          ? input.href
          : input.url,
    );
  }

  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllEnvs();
  });

  it.each(["/apoie", "/privacidade", "/termos"])(
    "SPEC-0028:E2E-02 %s não é tratado como cidade e segue com 200",
    async (path) => {
      vi.stubEnv("API_URL", "http://api.test");
      const fetch = vi.spyOn(globalThis, "fetch").mockImplementation(async () =>
        Response.json(
          { status: 404, code: "events.city_not_found" },
          {
            status: 404,
            headers: { "content-type": "application/problem+json" },
          },
        ),
      );

      const response = await proxy(new NextRequest(`http://localhost${path}`));

      expect(
        fetch.mock.calls
          .map(([input]) => urlOf(input).pathname)
          .filter((called) => called.startsWith("/api/v1/cities/")),
        `${path} consultou a API de agenda`,
      ).toEqual([]);
      expect(response.status).toBe(200);
      expect(response.headers.get("x-middleware-rewrite")).toBeNull();
      expect(response.headers.get("content-security-policy")).toBeTruthy();
    },
  );

  it("SPEC-0028:IT-03 com a API fora do ar, /apoie não vira 503", async () => {
    vi.stubEnv("API_URL", "http://api.test");
    vi.spyOn(globalThis, "fetch").mockImplementation(async () => {
      throw new TypeError("fetch failed");
    });

    const response = await proxy(new NextRequest("http://localhost/apoie"));

    expect(response.status).toBe(200);
    expect(response.headers.get("x-middleware-rewrite")).toBeNull();
  });
});
