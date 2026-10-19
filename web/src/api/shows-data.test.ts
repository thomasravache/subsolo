// @vitest-environment node
import { describe, expect, it } from "vitest";

import * as showsData from "./shows-data";

/**
 * SPEC-0024 H23-06 (revisão da SPEC-0023) — com o `timeZone` em cada item de `GET /api/v1/me/shows`, os dados de "Meus
 * shows" deixam de consultar a agenda pública de cada cidade só para ler o fuso: a leitura dos fusos pela agenda
 * (`getCityTimeZones`) some.
 */
describe("fusos de Meus shows", () => {
  it("SPEC-0024:H23-06 os dados de Meus shows não leem mais o fuso pela agenda pública", () => {
    expect(Object.keys(showsData)).not.toContain("getCityTimeZones");
  });
});
