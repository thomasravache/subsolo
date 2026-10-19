// @vitest-environment node
import { afterEach, describe, expect, it, vi } from "vitest";

import { getProfileShows, getPublicProfile } from "@/api/public-profile";

/**
 * SPEC-0026 — as chamadas do servidor do web para a página do perfil, com a API simulada (`fetch` trocado):
 * - IT-04 (parte do web): `GET /api/v1/profiles/{type}/{slug}` com 404 `profiles.not_found` → `not-found` (a página
 *   responde "Perfil não encontrado"); 200 → `ok` com o perfil, pela rota do tipo em inglês (`band`, `venue`,
 *   `producer`);
 * - IT-05 (parte do web): `GET /api/v1/shows?profileId=&period=` fora do ar (conexão recusada) ou com 5xx →
 *   `unavailable` (a página mostra o perfil com o aviso, sem quebrar); 200 → `ok` com a lista `shows`.
 */
const profile = {
  id: "0199f0c1-6a4d-7e7f-9b21-8d5c0e9a1f42",
  type: "band",
  name: "Podridão",
  slug: "podridao",
  city: { slug: "curitiba", name: "Curitiba" },
  styles: [{ slug: "death", name: "Death Metal" }],
  links: { instagram: "https://www.instagram.com/podridao" },
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

describe("perfil público na API", () => {
  it("SPEC-0026:IT-04 404 profiles.not_found vira not-found", async () => {
    const fetch = stubApi(() => problem(404, "profiles.not_found"));

    const result = await getPublicProfile("venue", "podridao");

    expect(result).toEqual({ kind: "not-found" });
    expect(fetch.mock.calls.map(([input]) => urlOf(input).pathname)).toEqual([
      "/api/v1/profiles/venue/podridao",
    ]);
  });

  it("SPEC-0026:IT-04 200 devolve o perfil, pela rota do tipo", async () => {
    stubApi((url) =>
      url.pathname === "/api/v1/profiles/band/podridao"
        ? Response.json(profile)
        : problem(404, "profiles.not_found"),
    );

    const result = await getPublicProfile("band", "podridao");

    expect(result).toEqual({ kind: "ok", profile });
  });
});

describe("shows do perfil na API", () => {
  it("SPEC-0026:IT-05 API de shows fora do ar (conexão recusada) vira unavailable", async () => {
    stubApi(() => Promise.reject(new TypeError("fetch failed")));

    expect(await getProfileShows(profile.id, "upcoming")).toEqual({
      kind: "unavailable",
    });
  });

  it("SPEC-0026:IT-05 API de shows com 5xx vira unavailable", async () => {
    stubApi(() => problem(503, "service.unavailable"));

    expect(await getProfileShows(profile.id, "past")).toEqual({
      kind: "unavailable",
    });
  });

  it("SPEC-0026:IT-05 200 devolve os shows, com profileId e period na consulta", async () => {
    const fetch = stubApi(() => Response.json({ shows: [] }));

    const result = await getProfileShows(profile.id, "past");

    expect(result).toEqual({ kind: "ok", shows: [] });
    const [call] = fetch.mock.calls.map(([input]) => urlOf(input));
    expect(call?.pathname).toBe("/api/v1/shows");
    expect(call?.searchParams.get("profileId")).toBe(profile.id);
    expect(call?.searchParams.get("period")).toBe("past");
  });
});
