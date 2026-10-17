// @vitest-environment node
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { getCityAgenda } from "./agenda";
import { listCities } from "./cities";

/**
 * SPEC-0014 UT-05 (Emenda v3) — o cliente da API do web, chamado no servidor durante uma requisição do visitante,
 * repassa à API o IP do visitante em `X-Forwarded-For` (o valor que o Caddy pôs na requisição recebida), para o rate
 * limit valer por visitante; sem o cabeçalho na requisição recebida, não inventa nenhum. Dublês: `next/headers`
 * (cabeçalhos da requisição recebida) e o `fetch` global (o que sai para a API).
 */
const incoming = vi.hoisted(() => ({ headers: new Headers() }));

vi.mock("next/headers", () => ({
  headers: async () => incoming.headers,
}));

/** Os cabeçalhos de cada chamada que saiu para a API. */
let sent: Headers[] = [];

beforeEach(() => {
  sent = [];
  incoming.headers = new Headers();
  vi.stubEnv("API_URL", "http://api.test");
  vi.stubGlobal(
    "fetch",
    vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      sent.push(
        input instanceof Request
          ? new Headers(input.headers)
          : new Headers(init?.headers),
      );
      const body = String(
        input instanceof Request ? input.url : input,
      ).endsWith("/api/v1/cities")
        ? []
        : {
            city: {
              slug: "cidade-exemplo",
              name: "Cidade Exemplo",
              state: "SP",
              timeZone: "America/Sao_Paulo",
            },
            shows: [],
          };
      return new Response(JSON.stringify(body), {
        status: 200,
        headers: { "content-type": "application/json" },
      });
    }),
  );
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.unstubAllEnvs();
});

describe("cliente da API no servidor", () => {
  it("SPEC-0014:UT-05 repassa o X-Forwarded-For do visitante na lista de cidades", async () => {
    incoming.headers = new Headers({ "x-forwarded-for": "203.0.113.7" });

    await listCities();

    expect(sent).toHaveLength(1);
    expect(sent[0]?.get("x-forwarded-for")).toBe("203.0.113.7");
  });

  it("SPEC-0014:UT-05 repassa o X-Forwarded-For do visitante na agenda da cidade", async () => {
    incoming.headers = new Headers({ "x-forwarded-for": "203.0.113.7" });

    const result = await getCityAgenda("cidade-exemplo");

    expect(result.kind).toBe("ok");
    expect(sent).toHaveLength(1);
    expect(sent[0]?.get("x-forwarded-for")).toBe("203.0.113.7");
  });

  it("SPEC-0014:UT-05 sem o cabeçalho na requisição recebida, não envia X-Forwarded-For", async () => {
    await listCities();
    await getCityAgenda("cidade-exemplo");

    expect(sent).toHaveLength(2);
    for (const headers of sent) {
      expect(headers.has("x-forwarded-for")).toBe(false);
    }
  });
});
