import { expect, test } from "@playwright/test";

import { weekShowTimes } from "./filters";

/**
 * SPEC-0042 — os horários dos dois shows "da semana" do SPEC-0025:E2E-01 não podem cair no passado no domingo à noite
 * (a API recusa show no passado). `weekShowTimes(now)` decide pelo relógio de São Paulo (UTC−3, sem horário de verão):
 *
 * - UT-01 (mobile-chrome, sem navegador): fora do domingo e no domingo antes das 22:30, 23:00 e 23:30; no domingo de
 *   22:30 até 23:39, 23:58 e 23:59 (no futuro em relação a `now`); a partir das 23:40, `null` (o E2E-01 é pulado).
 *   Os instantes são fixos, em UTC: domingo 2026-10-25 e quarta 2026-10-21.
 */

/** O instante de `hh:mm` no domingo 2026-10-25 em São Paulo. */
function sundayAt(time: string): Date {
  return new Date(`2026-10-25T${time}:00-03:00`);
}

test.describe("horários dos shows da semana", () => {
  test("SPEC-0042:UT-01 os shows de domingo da agenda ficam no futuro ou o teste é pulado nos últimos 20 minutos", ({}, testInfo) => {
    test.skip(
      testInfo.project.name !== "mobile-chrome",
      "teste sem navegador: basta um projeto",
    );

    // Domingo a partir das 23:40: não há margem.
    expect(weekShowTimes(new Date("2026-10-26T02:45:00Z"))).toBeNull(); // 23:45
    expect(weekShowTimes(new Date("2026-10-26T02:40:00Z"))).toBeNull(); // 23:40 (borda)

    // Domingo de 22:30 até 23:39: os últimos minutos do dia.
    expect(weekShowTimes(new Date("2026-10-26T02:39:00Z"))).toEqual([
      "23:58",
      "23:59",
    ]); // 23:39
    const lateSunday = new Date("2026-10-26T02:05:00Z"); // 23:05
    const late = weekShowTimes(lateSunday);
    expect(late).toEqual(["23:58", "23:59"]);
    for (const time of late ?? []) {
      expect(sundayAt(time).getTime()).toBeGreaterThan(lateSunday.getTime());
    }
    expect(weekShowTimes(new Date("2026-10-26T01:30:00Z"))).toEqual([
      "23:58",
      "23:59",
    ]); // 22:30 (borda)

    // Domingo antes das 22:30: os horários de sempre.
    expect(weekShowTimes(new Date("2026-10-26T01:29:00Z"))).toEqual([
      "23:00",
      "23:30",
    ]); // 22:29
    expect(weekShowTimes(new Date("2026-10-25T18:00:00Z"))).toEqual([
      "23:00",
      "23:30",
    ]); // 15:00

    // Fora do domingo: os horários de sempre, mesmo tarde da noite.
    expect(weekShowTimes(new Date("2026-10-22T02:50:00Z"))).toEqual([
      "23:00",
      "23:30",
    ]); // quarta 2026-10-21 23:50
  });
});
