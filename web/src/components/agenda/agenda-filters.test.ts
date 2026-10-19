import { describe, expect, it } from "vitest";

import {
  agendaApiQuery,
  localToday,
  readAgendaFilters,
  resolvePeriod,
} from "./agenda-filters";

/**
 * SPEC-0025 — filtros da agenda lidos da URL da página `/{cidade}` (§6):
 * - UT-01: com "hoje" 2026-11-12 (quinta) em America/Sao_Paulo, "semana" = 12 a 15/11 (até o domingo) e "mês" = 12 a
 *   30/11 (até o último dia); `de`/`ate` válidos são aceitos; `ate < de`, 181 dias e datas mal formadas são ignorados
 *   (agenda padrão, sem erro na página).
 * - UT-02: `estilo=death,,GRIND,xpto` vira `death,grind` e `periodo=foo` é ignorado.
 */

const saoPaulo = "America/Sao_Paulo";
const styles = ["death", "black", "grind", "doom", "thrash"];

describe("períodos da agenda", () => {
  it("SPEC-0025:UT-01 hoje é a data local da cidade (23:30 de 12/11 em São Paulo já é 13/11 em UTC)", () => {
    expect(localToday(saoPaulo, new Date("2026-11-13T02:30:00Z"))).toBe(
      "2026-11-12",
    );
    expect(localToday(saoPaulo, new Date("2026-11-12T03:00:00Z"))).toBe(
      "2026-11-12",
    );
    expect(localToday(saoPaulo, new Date("2026-11-12T02:59:00Z"))).toBe(
      "2026-11-11",
    );
  });

  it("SPEC-0025:UT-01 esta semana vai de hoje (quinta, 12/11) até o domingo (15/11)", () => {
    expect(resolvePeriod({ kind: "semana" }, "2026-11-12")).toEqual({
      from: "2026-11-12",
      to: "2026-11-15",
    });
  });

  it("SPEC-0025:UT-01 no domingo, esta semana é só o domingo; na segunda, vai até o domingo seguinte", () => {
    expect(resolvePeriod({ kind: "semana" }, "2026-11-15")).toEqual({
      from: "2026-11-15",
      to: "2026-11-15",
    });
    expect(resolvePeriod({ kind: "semana" }, "2026-11-16")).toEqual({
      from: "2026-11-16",
      to: "2026-11-22",
    });
  });

  it("SPEC-0025:UT-01 este mês vai de hoje (12/11) até o último dia do mês (30/11)", () => {
    expect(resolvePeriod({ kind: "mes" }, "2026-11-12")).toEqual({
      from: "2026-11-12",
      to: "2026-11-30",
    });
    expect(resolvePeriod({ kind: "mes" }, "2026-12-31")).toEqual({
      from: "2026-12-31",
      to: "2026-12-31",
    });
    expect(resolvePeriod({ kind: "mes" }, "2028-02-03")).toEqual({
      from: "2028-02-03",
      to: "2028-02-29",
    });
  });

  it("SPEC-0025:UT-01 a semana que cruza o mês e o ano termina no domingo certo", () => {
    expect(resolvePeriod({ kind: "semana" }, "2026-12-30")).toEqual({
      from: "2026-12-30",
      to: "2027-01-03",
    });
  });

  it("SPEC-0025:UT-01 periodo=semana e periodo=mes são lidos da URL e viram from/to da API", () => {
    const week = readAgendaFilters({ periodo: "semana" }, styles);
    expect(week.period).toEqual({ kind: "semana" });
    expect(agendaApiQuery(week, "2026-11-12")).toEqual({
      from: "2026-11-12",
      to: "2026-11-15",
    });

    const month = readAgendaFilters({ periodo: "mes" }, styles);
    expect(month.period).toEqual({ kind: "mes" });
    expect(agendaApiQuery(month, "2026-11-12")).toEqual({
      from: "2026-11-12",
      to: "2026-11-30",
    });
  });

  it("SPEC-0025:UT-01 de/ate válidos são aceitos como intervalo (inclusive o mesmo dia e 180 dias)", () => {
    const range = readAgendaFilters(
      { de: "2026-11-20", ate: "2026-11-22" },
      styles,
    );
    expect(range.period).toEqual({
      kind: "intervalo",
      from: "2026-11-20",
      to: "2026-11-22",
    });
    expect(agendaApiQuery(range, "2026-11-12")).toEqual({
      from: "2026-11-20",
      to: "2026-11-22",
    });

    expect(
      readAgendaFilters({ de: "2026-11-20", ate: "2026-11-20" }, styles).period,
    ).toEqual({ kind: "intervalo", from: "2026-11-20", to: "2026-11-20" });
    // 2026-11-12 + 180 dias = 2027-05-11.
    expect(
      readAgendaFilters({ de: "2026-11-12", ate: "2027-05-11" }, styles).period,
    ).toEqual({ kind: "intervalo", from: "2026-11-12", to: "2027-05-11" });
  });

  it.each([
    ["ate antes de de", { de: "2026-11-22", ate: "2026-11-20" }],
    ["181 dias", { de: "2026-11-12", ate: "2027-05-12" }],
    ["data inexistente", { de: "2026-02-30", ate: "2026-03-02" }],
    ["mês 13", { de: "2026-13-01", ate: "2026-13-05" }],
    ["formato brasileiro", { de: "12/11/2026", ate: "15/11/2026" }],
  ])(
    "SPEC-0025:UT-01 intervalo recusado (%s) é ignorado: agenda padrão, sem from/to",
    (_, params) => {
      const filters = readAgendaFilters(params, styles);

      expect(filters.period).toBeUndefined();
      expect(agendaApiQuery(filters, "2026-11-12")).toEqual({});
    },
  );
});

describe("leitor de parâmetros da página", () => {
  it("SPEC-0025:UT-02 estilo=death,,GRIND,xpto vira death,grind e periodo=foo é ignorado", () => {
    const filters = readAgendaFilters(
      { estilo: "death,,GRIND,xpto", periodo: "foo" },
      styles,
    );

    expect(filters).toEqual({ period: undefined, styles: ["death", "grind"] });
    expect(agendaApiQuery(filters, "2026-11-12")).toEqual({
      styles: "death,grind",
    });
  });

  it("SPEC-0025:UT-02 estilos repetidos e com espaços entram uma vez só", () => {
    const { styles: read } = readAgendaFilters(
      { estilo: " grind , death,grind,DEATH" },
      styles,
    );

    expect([...read].sort()).toEqual(["death", "grind"]);
  });

  it("SPEC-0025:UT-02 sem parâmetros (ou com valores vazios) não há filtro", () => {
    for (const params of [{}, { estilo: "", periodo: "" }, { estilo: ",," }]) {
      const filters = readAgendaFilters(params, styles);

      expect(filters).toEqual({ period: undefined, styles: [] });
      expect(agendaApiQuery(filters, "2026-11-12")).toEqual({});
    }
  });

  it("SPEC-0025:UT-02 período e estilo juntos vão os dois para a API", () => {
    const filters = readAgendaFilters(
      { periodo: "semana", estilo: "death" },
      styles,
    );

    expect(agendaApiQuery(filters, "2026-11-12")).toEqual({
      from: "2026-11-12",
      to: "2026-11-15",
      styles: "death",
    });
  });
});
