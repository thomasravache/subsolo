// @vitest-environment jsdom
import { render, screen, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import type { CityAgendaQuery, CityAgendaResult } from "@/api/agenda";

/**
 * SPEC-0029 — "{n} vão" nos cards da agenda: a agenda do `Events` traz `attendingCount` 0 (CH-01) e o web pede as
 * contagens ao `Engagement` em lote (`GET /api/v1/attendances/counts?showIds=a,b`, uma chamada para a página) e junta
 * no servidor (§4), com a API respondendo por um `fetch` falso:
 * - E2E-01 (parte da agenda) — cada card mostra a contagem do seu show ("47 vão"; show sem "vou" → "0 vão"), não o 0
 *   do `Events`;
 * - IT-05 — com a contagem indisponível (rota de contagens fora do ar), a agenda sai com os cards, sem a contagem e sem
 *   erro.
 */
vi.mock("./agenda-status", () => ({
  agendaStatus: async () => undefined,
}));

const getCityAgenda =
  vi.fn<(slug: string, query?: CityAgendaQuery) => Promise<CityAgendaResult>>();

vi.mock("@/api/agenda", () => ({
  getCityAgenda: (slug: string, query?: CityAgendaQuery) =>
    getCityAgenda(slug, query),
  getAgendaStyles: async () => [],
}));

const showA = "3f1c2b8e-6a4d-4e7f-9b21-8d5c0e9a1f41";
const showB = "3f1c2b8e-6a4d-4e7f-9b21-8d5c0e9a1f42";

function summary(id: string, title: string, startsAt: string) {
  return {
    id,
    slug: `${title.toLowerCase().replaceAll(" ", "-")}-2026-10-24`,
    title,
    startsAt,
    venueName: "Porão do Rock",
    lineup: ["Podridão"],
    styles: ["death"],
    status: "scheduled" as const,
    flyerThumbUrl: null,
    attendingCount: 0,
  };
}

const agenda: CityAgendaResult = {
  kind: "ok",
  agenda: {
    city: {
      slug: "curitiba",
      name: "Curitiba",
      state: "PR",
      timeZone: "America/Sao_Paulo",
    },
    shows: [
      summary(showA, "Noite da Peste II", "2026-10-25T01:00:00Z"),
      summary(showB, "Sepulcro Fest", "2026-10-26T01:00:00Z"),
    ],
    period: { from: "2026-10-19", to: "2027-01-17" },
    total: 2,
  },
};

const countCalls: URL[] = [];

/** Contagens (`counts` só com o show A) ou a rota fora do ar; o resto da API (sessão etc.) fora do ar. */
function stubApi(counts: Record<string, number> | "down") {
  vi.spyOn(globalThis, "fetch").mockImplementation(async (input, init) => {
    const url = new URL(new Request(input, init).url, window.location.href);
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
  getCityAgenda.mockResolvedValue(agenda);
  vi.useFakeTimers({ toFake: ["Date"] });
  vi.setSystemTime(new Date("2026-10-19T15:00:00Z"));
  vi.stubEnv("API_URL", "http://api.test");
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
  getCityAgenda.mockReset();
  vi.useRealTimers();
  vi.restoreAllMocks();
  vi.unstubAllEnvs();
  vi.unstubAllGlobals();
  vi.resetModules();
});

async function renderAgenda() {
  const { default: CityAgendaPage } = await import("./page");
  return render(
    await CityAgendaPage({
      params: Promise.resolve({ cidade: "curitiba" }),
      searchParams: Promise.resolve({}),
    } as unknown as Parameters<typeof CityAgendaPage>[0]),
  );
}

const cardOf = (title: string) =>
  screen
    .getAllByRole("article")
    .find((article) => within(article).queryAllByText(title).length > 0);

const countIn = (element: HTMLElement | undefined) =>
  (element?.textContent ?? "").match(/\d+\s*(vão|vai)/i)?.[0] ?? "";

describe("contagem de vão nos cards da agenda", () => {
  it("SPEC-0029:E2E-01 cada card mostra a contagem do Engagement, pedida em lote", async () => {
    stubApi({ [showA]: 47 });

    await renderAgenda();

    expect(
      countIn(cardOf("Noite da Peste II")),
      cardOf("Noite da Peste II")?.textContent ?? "sem card",
    ).toMatch(/^47\s*vão$/i);
    expect(countIn(cardOf("Sepulcro Fest"))).toMatch(/^0\s*vão$/i);
    expect(
      countCalls,
      "as contagens não foram pedidas numa chamada só",
    ).toHaveLength(1);
    const ids = (countCalls[0]?.searchParams.getAll("showIds") ?? [])
      .flatMap((value) => value.split(","))
      .sort();
    expect(ids).toEqual([showA, showB].sort());
  });

  it("SPEC-0029:IT-05 com a contagem indisponível, a agenda sai com os cards, sem a contagem e sem erro", async () => {
    stubApi("down");

    const { container } = await renderAgenda();

    expect(
      screen.getByRole("heading", { level: 1, name: "Agenda Curitiba" }),
    ).toBeTruthy();
    expect(cardOf("Noite da Peste II")).toBeDefined();
    expect(cardOf("Sepulcro Fest")).toBeDefined();
    expect(
      countCalls.length,
      "a agenda não pediu as contagens",
    ).toBeGreaterThan(0);
    for (const article of screen.getAllByRole("article")) {
      expect(countIn(article), "contagem sem fonte no card").toBe("");
    }
    expect(container.textContent ?? "").not.toMatch(/indispon[ií]vel/i);
  });
});
