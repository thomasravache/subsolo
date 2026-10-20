// @vitest-environment node
import { afterEach, describe, expect, it, vi } from "vitest";

import { getSupporters } from "@/api/supporters";

/**
 * SPEC-0028 — a chamada do servidor do web para a lista da página Apoie, com a API simulada (`fetch` trocado):
 * - IT-01 (parte do web): `GET /api/v1/supporters` com 200 → `ok` com os apoiadores na ordem da resposta (a de cadastro);
 * - IT-03 (parte do web): API fora do ar (conexão recusada) ou com 5xx → `unavailable` (a página mostra o Pix e "A lista
 *   de apoiadores volta já.", sem quebrar).
 */
const supporters = [
  {
    id: "0199f0c1-6a4d-7e7f-9b21-8d5c0e9a1f42",
    name: "Putridearth Zine",
    url: "https://zine.example",
  },
  {
    id: "0199f0c1-6a4d-7e7f-9b21-8d5c0e9a1f43",
    name: "Abismo Records",
    url: null,
  },
];

function urlOf(input: Parameters<typeof fetch>[0]): URL {
  return new URL(
    typeof input === "string"
      ? input
      : input instanceof URL
        ? input.href
        : input.url,
  );
}

function stubApi(answer: (url: URL) => Response | Promise<Response>) {
  vi.stubEnv("API_URL", "http://api.test");
  return vi
    .spyOn(globalThis, "fetch")
    .mockImplementation(async (input) => answer(urlOf(input)));
}

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllEnvs();
});

describe("apoiadores na API", () => {
  it("SPEC-0028:IT-01 200 devolve os apoiadores na ordem da resposta", async () => {
    const fetch = stubApi(() => Response.json(supporters));

    const result = await getSupporters();

    expect(result).toEqual({ kind: "ok", supporters });
    expect(fetch.mock.calls.map(([input]) => urlOf(input).pathname)).toEqual([
      "/api/v1/supporters",
    ]);
  });

  it("SPEC-0028:IT-03 API fora do ar (conexão recusada) vira unavailable", async () => {
    stubApi(() => Promise.reject(new TypeError("fetch failed")));

    expect(await getSupporters()).toEqual({ kind: "unavailable" });
  });

  it("SPEC-0028:IT-03 API com 5xx vira unavailable", async () => {
    stubApi(() =>
      Response.json(
        { status: 503, code: "service.unavailable" },
        {
          status: 503,
          headers: { "content-type": "application/problem+json" },
        },
      ),
    );

    expect(await getSupporters()).toEqual({ kind: "unavailable" });
  });
});
