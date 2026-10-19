// @vitest-environment node
import { afterEach, describe, expect, it, vi } from "vitest";

import type { CityAgendaQuery, CityAgendaResult } from "@/api/agenda";

/**
 * SPEC-0026 H25-01 (revisão da SPEC-0025) — "Esta semana" e "Este mês" dependem de "hoje" no fuso da cidade, que a
 * página descobre pela própria agenda. Hoje ela pede a agenda inteira (90 dias, sem `from`/`to`) só para saber o fuso
 * e depois pede a do período. Depois da mudança, a sondagem (se houver) pede um intervalo de no máximo um dia; a última
 * chamada é a do período escolhido, e nunca há mais de duas.
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

const dayMs = 24 * 60 * 60 * 1000;

function today(): string {
  return new Intl.DateTimeFormat("en-CA", {
    timeZone: "America/Sao_Paulo",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).format(new Date());
}

function plusDays(date: string, days: number): string {
  return new Date(Date.parse(`${date}T00:00:00Z`) + days * dayMs)
    .toISOString()
    .slice(0, 10);
}

function agendaFor(query: CityAgendaQuery = {}): CityAgendaResult {
  const from = query.from ?? today();
  return {
    kind: "ok",
    agenda: {
      city: {
        slug: "curitiba",
        name: "Curitiba",
        state: "PR",
        timeZone: "America/Sao_Paulo",
      },
      shows: [],
      period: { from, to: query.to ?? plusDays(from, 90) },
      total: 0,
    },
  };
}

function spanDays(query: CityAgendaQuery | undefined): number {
  if (!query?.from || !query.to) {
    return Number.POSITIVE_INFINITY;
  }
  return (Date.parse(query.to) - Date.parse(query.from)) / dayMs;
}

afterEach(() => {
  getCityAgenda.mockReset();
  vi.resetModules();
});

describe("agenda da cidade com período relativo", () => {
  it.each(["semana", "mes"])(
    "SPEC-0026:H25-01 ?periodo=%s não pede a agenda de 90 dias só para saber o fuso",
    async (periodo) => {
      getCityAgenda.mockImplementation(async (_slug, query) =>
        agendaFor(query),
      );
      const { generateMetadata } = await import("./page");

      await generateMetadata({
        params: Promise.resolve({ cidade: "curitiba" }),
        searchParams: Promise.resolve({ periodo }),
      } as unknown as Parameters<typeof generateMetadata>[0]);

      const queries = getCityAgenda.mock.calls.map(([, query]) => query);
      expect(queries.length, "nenhuma chamada à agenda").toBeGreaterThan(0);
      expect(
        queries.length,
        `chamadas à agenda: ${JSON.stringify(queries)}`,
      ).toBeLessThanOrEqual(2);
      const last = queries.at(-1);
      expect(last?.from, "a chamada final sem o início do período").toBe(
        today(),
      );
      for (const probe of queries.slice(0, -1)) {
        expect(
          spanDays(probe),
          `a sondagem do fuso pediu mais de um dia: ${JSON.stringify(probe)}`,
        ).toBeLessThanOrEqual(1);
      }
    },
  );
});
