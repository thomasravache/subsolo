// @vitest-environment jsdom
import { render, screen, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

/**
 * SPEC-0029 — "{n} vão" nos cards de shows do perfil público (`/banda/{slug}`): a lista de shows do `Events` traz
 * `attendingCount` 0 e o web pede as contagens ao `Engagement` em lote (`GET /api/v1/attendances/counts?showIds=…`),
 * com a API respondendo por um `fetch` falso (como o teste da rota do perfil da SPEC-0027):
 * - E2E-01 (parte do perfil) — o card mostra a contagem do seu show;
 * - IT-05 — contagem indisponível: o perfil sai com os cards, sem a contagem e sem erro.
 */
const now = new Date("2026-10-19T15:00:00Z");

const band = {
  id: "0199f0c1-6a4d-7e7f-9b21-8d5c0e9a1f42",
  type: "band",
  name: "Podridão",
  slug: "podridao",
  city: { slug: "curitiba", name: "Curitiba" },
  styles: [{ slug: "death", name: "Death Metal" }],
  links: {},
};

const showA = "3f1c2b8e-6a4d-4e7f-9b21-8d5c0e9a1f41";
const showB = "3f1c2b8e-6a4d-4e7f-9b21-8d5c0e9a1f43";

function show(id: string, title: string, startsAt: string) {
  return {
    id,
    slug: `${title.toLowerCase().replaceAll(" ", "-")}-2026-10-24`,
    title,
    startsAt,
    venueName: "Porão do Rock",
    lineup: ["Podridão"],
    styles: ["death"],
    status: "scheduled",
    flyerThumbUrl: null,
    attendingCount: 0,
    city: { slug: "curitiba", name: "Curitiba", timeZone: "America/Sao_Paulo" },
  };
}

vi.mock("@/components/profiles/profile-status", () => ({
  profileStatusHeader: "x-subsolo-profile",
  profileStatus: async () => undefined,
}));

const countCalls: URL[] = [];

function stubApi(counts: Record<string, number> | "down") {
  vi.spyOn(globalThis, "fetch").mockImplementation(async (input, init) => {
    const url = new URL(new Request(input, init).url, window.location.href);
    if (url.pathname === "/api/v1/profiles/band/podridao") {
      return Response.json(band);
    }
    if (url.pathname === "/api/v1/shows") {
      return Response.json({
        shows:
          url.searchParams.get("period") === "past"
            ? []
            : [
                show(showA, "Noite da Peste II", "2026-10-25T01:00:00Z"),
                show(showB, "Sepulcro Fest", "2026-10-26T01:00:00Z"),
              ],
      });
    }
    if (url.pathname === "/api/v1/attendances/counts") {
      countCalls.push(url);
      if (counts === "down") {
        throw new TypeError("fetch failed");
      }
      return Response.json({ counts });
    }
    throw new TypeError("fetch failed");
  });
}

beforeEach(() => {
  countCalls.length = 0;
  vi.useFakeTimers({ toFake: ["Date"] });
  vi.setSystemTime(now);
  vi.stubEnv("API_URL", "http://api.test");
  vi.stubEnv("SUBSOLO_PUBLIC_URL", "https://subsolo.example");
  const Native = globalThis.Request;
  vi.stubGlobal(
    "Request",
    class extends Native {
      constructor(input: RequestInfo | URL, init?: RequestInit) {
        super(
          typeof input === "string"
            ? new URL(input, window.location.href)
            : input,
          init,
        );
      }
    },
  );
});

afterEach(() => {
  vi.useRealTimers();
  vi.restoreAllMocks();
  vi.unstubAllEnvs();
  vi.unstubAllGlobals();
  vi.resetModules();
});

async function renderProfile() {
  const { ProfileRoute } = await import("@/components/profiles/profile-route");
  return render(await ProfileRoute({ type: "band", slug: "podridao" }));
}

const cardOf = (title: string) =>
  screen
    .getAllByRole("article")
    .find((article) => within(article).queryAllByText(title).length > 0);

const countIn = (element: HTMLElement | undefined) =>
  (element?.textContent ?? "").match(/\d+\s*(vão|vai)/i)?.[0] ?? "";

describe("contagem de vão nos cards do perfil", () => {
  it("SPEC-0029:E2E-01 o card do show no perfil mostra a contagem do Engagement", async () => {
    stubApi({ [showA]: 12, [showB]: 1 });

    await renderProfile();

    expect(countIn(cardOf("Noite da Peste II"))).toMatch(/^12\s*vão$/i);
    expect(countIn(cardOf("Sepulcro Fest"))).toMatch(/^1\s*vai$/i);
    expect(countCalls.length, "as contagens não vieram em lote").toBe(1);
  });

  it("SPEC-0029:IT-05 com a contagem indisponível, o perfil sai com os cards, sem a contagem", async () => {
    stubApi("down");

    await renderProfile();

    expect(
      screen.getByRole("heading", { level: 1, name: /podridão/i }),
    ).toBeTruthy();
    expect(cardOf("Noite da Peste II")).toBeDefined();
    expect(
      countCalls.length,
      "o perfil não pediu as contagens",
    ).toBeGreaterThan(0);
    for (const article of screen.getAllByRole("article")) {
      expect(countIn(article), "contagem sem fonte no card").toBe("");
    }
  });
});
