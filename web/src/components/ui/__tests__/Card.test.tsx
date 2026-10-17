// @vitest-environment jsdom
import { readFileSync } from "node:fs";
import { dirname, resolve as resolvePath } from "node:path";
import { fileURLToPath } from "node:url";

import { render, screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";

import type { components } from "@/api/schema";
import { Card, type CardProps } from "@/components/ui/Card";

/**
 * SPEC-0011 UT-02, UT-03 e CT-01 — o `Card` de show da agenda, alimentado por um `ShowSummary` (contrato v1 da
 * agenda): título, data e hora no fuso da cidade, casa, line-up, estilos e contagem de "vão"; placeholder com o
 * nome do show quando não há flyer; badge "Cancelado"/"Adiado" com o papel acessível de status.
 */

type ShowSummary = components["schemas"]["ShowSummaryResponse"];

const timeZone = "America/Sao_Paulo";

/** Show completo: 2026-11-15T00:00Z = 14/11 às 21h em São Paulo (UTC−3). */
const show: ShowSummary = {
  id: "3f1c2b8e-6a4d-4e7f-9b21-8d5c0e9a1f42",
  slug: "noite-da-peste-ii",
  title: "Noite da Peste II",
  startsAt: "2026-11-15T00:00:00Z",
  venueName: "Porão do Rock",
  lineup: ["Cova Rasa", "Necrocorpo", "Vômito Ácido"],
  styles: ["Death", "Grind"],
  status: "scheduled",
  flyerThumbUrl: "https://cdn.subsolo.example/flyers/noite-da-peste-ii.webp",
  attendingCount: 47,
};

const withoutFlyer: ShowSummary = {
  ...show,
  id: "0b7e4a9c-2d31-4f58-a6e0-5c9f1b3d7e21",
  slug: "sepulcro-fest",
  title: "Sepulcro Fest",
  flyerThumbUrl: null,
};

/** Mapeamento da página da agenda: `ShowSummary` → props do `Card`. */
function cardProps(summary: ShowSummary): CardProps {
  return {
    href: `/cidade-exemplo/${summary.slug}`,
    flyerSrc: summary.flyerThumbUrl,
    title: summary.title,
    date: summary.startsAt,
    timeZone,
    venue: summary.venueName,
    lineup: summary.lineup,
    styles: summary.styles,
    status: summary.status,
    attendingCount: summary.attendingCount,
  };
}

function renderCard(summary: ShowSummary) {
  return render(<Card {...cardProps(summary)} />);
}

const text = (element: HTMLElement) =>
  (element.textContent ?? "").replace(/\s+/g, " ");

describe("Card de show", () => {
  it("SPEC-0011:UT-02 mostra título, data e hora no fuso da cidade, casa, line-up, estilos e contagem", () => {
    const { container } = renderCard(show);
    const content = text(container);

    expect(
      screen.getByRole("heading", { name: /Noite da Peste II/i }),
    ).toBeTruthy();
    expect(screen.getByRole("link").getAttribute("href")).toBe(
      "/cidade-exemplo/noite-da-peste-ii",
    );
    // 00:00 UTC do dia 15 = 21h do dia 14 em São Paulo.
    expect(content).toMatch(/(?<!\d)14(?!\d)/);
    expect(content).toMatch(/nov/i);
    expect(content).toMatch(/(?<!\d)21(h|:00)/);
    expect(content).not.toMatch(/(?<!\d)00(h|:00)/);
    expect(content).toContain("Porão do Rock");
    for (const band of show.lineup) {
      expect(content).toContain(band);
    }
    for (const style of show.styles) {
      expect(content.toLowerCase()).toContain(style.toLowerCase());
    }
    expect(content).toMatch(/(?<!\d)47(?!\d)/);
    expect(content).toMatch(/vão/i);
  });

  it("SPEC-0011:UT-02 com flyer, mostra a miniatura com texto alternativo do show", () => {
    renderCard(show);

    const flyer = screen.getByRole("img");
    expect(flyer.getAttribute("alt") ?? "").toMatch(/Noite da Peste II/i);
    expect(decodeURIComponent(flyer.getAttribute("src") ?? "")).toContain(
      "https://cdn.subsolo.example/flyers/noite-da-peste-ii.webp",
    );
  });

  it("SPEC-0011:UT-02 sem flyer, mostra o placeholder com o nome do show", () => {
    const { container } = renderCard(withoutFlyer);

    expect(container.querySelector("img")).toBeNull();
    expect(
      screen.getByRole("heading", { name: /Sepulcro Fest/i }),
    ).toBeTruthy();
    // Título do card + nome no placeholder escuro.
    expect(screen.getAllByText(/Sepulcro Fest/i).length).toBeGreaterThanOrEqual(
      2,
    );
  });

  it.each([
    ["cancelled", /^\s*cancelado\s*$/i],
    ["postponed", /^\s*adiado\s*$/i],
  ] as const)(
    "SPEC-0011:UT-03 show %s mostra o badge com papel de status",
    (status, label) => {
      renderCard({ ...show, status });

      const badge = screen.getByRole("status");
      expect(text(badge)).toMatch(label);
    },
  );

  it("SPEC-0011:UT-03 show agendado não mostra Cancelado nem Adiado", () => {
    const { container } = renderCard(show);

    expect(text(container)).not.toMatch(/cancelado|adiado/i);
  });
});

/* ------------------------------------------------------------------ CT-01 */

type Schema = {
  $ref?: string;
  type?: string | string[];
  format?: string;
  enum?: unknown[];
  required?: string[];
  properties?: Record<string, Schema>;
  items?: Schema;
};

// No jsdom, `URL` é o do jsdom: resolve o caminho a partir do arquivo com `node:path`.
const openApiPath = resolvePath(
  dirname(fileURLToPath(import.meta.url)),
  "../../../../../docs/api/openapi.v1.json",
);
const openApi = JSON.parse(readFileSync(openApiPath, "utf8")) as {
  components: { schemas: Record<string, Schema> };
};

function resolve(schema: Schema): Schema {
  let current = schema;
  for (let depth = 0; current.$ref && depth < 10; depth++) {
    current =
      openApi.components.schemas[
        current.$ref.replace("#/components/schemas/", "")
      ] ?? {};
  }
  return current;
}

const showSummarySchema = resolve({
  $ref: "#/components/schemas/ShowSummaryResponse",
});

/** Erros de validação (subconjunto de JSON Schema usado pelo snapshot: type, format, enum, required, items). */
function validate(value: unknown, schema: Schema, path = "$"): string[] {
  const resolved = resolve(schema);
  if (resolved.enum && !resolved.enum.includes(value)) {
    return [`${path}: ${JSON.stringify(value)} fora do enum`];
  }
  const types = resolved.type
    ? Array.isArray(resolved.type)
      ? resolved.type
      : [resolved.type]
    : [];
  const actual =
    value === null
      ? "null"
      : Array.isArray(value)
        ? "array"
        : Number.isInteger(value)
          ? "integer"
          : typeof value;
  if (
    types.length > 0 &&
    !types.includes(actual) &&
    !(actual === "integer" && types.includes("number"))
  ) {
    return [`${path}: ${actual}, esperado ${types.join("|")}`];
  }
  const errors: string[] = [];
  if (typeof value === "string" && resolved.format === "date-time") {
    if (Number.isNaN(Date.parse(value))) errors.push(`${path}: date-time`);
  }
  if (typeof value === "string" && resolved.format === "uuid") {
    if (!/^[0-9a-f]{8}-([0-9a-f]{4}-){3}[0-9a-f]{12}$/i.test(value))
      errors.push(`${path}: uuid`);
  }
  if (Array.isArray(value) && resolved.items) {
    value.forEach((item, index) =>
      errors.push(
        ...validate(item, resolved.items as Schema, `${path}[${index}]`),
      ),
    );
  }
  if (actual === "object" && resolved.properties) {
    const record = value as Record<string, unknown>;
    for (const name of resolved.required ?? []) {
      if (!(name in record)) errors.push(`${path}.${name}: obrigatório`);
    }
    for (const [name, property] of Object.entries(resolved.properties)) {
      if (name in record) {
        errors.push(...validate(record[name], property, `${path}.${name}`));
      }
    }
  }
  return errors;
}

/**
 * Como o `Card` exibe cada campo obrigatório do `ShowSummary`. É um `Record` sobre as chaves do tipo gerado do
 * OpenAPI: um campo novo no contrato quebra a compilação (lint:types) e o teste até o card ser ajustado.
 * `id` é o único campo sem exibição: identifica o show (chave da lista), não é informação para o fã.
 */
const displayed: Record<
  keyof ShowSummary,
  ((container: HTMLElement, summary: ShowSummary) => void) | "não exibido"
> = {
  id: "não exibido",
  slug: (container, summary) =>
    expect(within(container).getByRole("link").getAttribute("href")).toContain(
      summary.slug,
    ),
  title: (container, summary) =>
    expect(
      within(container).getByRole("heading", { name: summary.title }),
    ).toBeTruthy(),
  startsAt: (container) => expect(text(container)).toMatch(/(?<!\d)21(h|:00)/),
  venueName: (container, summary) =>
    expect(text(container)).toContain(summary.venueName),
  lineup: (container, summary) =>
    summary.lineup.forEach((band) => expect(text(container)).toContain(band)),
  styles: (container, summary) =>
    summary.styles.forEach((style) =>
      expect(text(container).toLowerCase()).toContain(style.toLowerCase()),
    ),
  status: (container) =>
    expect(text(within(container).getByRole("status"))).toMatch(/cancelado/i),
  flyerThumbUrl: (container, summary) =>
    expect(
      decodeURIComponent(
        within(container).getByRole("img").getAttribute("src") ?? "",
      ),
    ).toContain(summary.flyerThumbUrl as string),
  attendingCount: (container, summary) =>
    expect(text(container)).toMatch(
      new RegExp(`(?<!\\d)${summary.attendingCount}(?!\\d)`),
    ),
};

/** Fixture do contrato: show cancelado com flyer, todos os campos preenchidos. */
const contractFixture: unknown = JSON.parse(
  JSON.stringify({ ...show, status: "cancelled" }),
);

describe("Card × contrato ShowSummary", () => {
  it("SPEC-0011:CT-01 fixture válida contra o schema ShowSummary do OpenAPI", () => {
    expect(validate(contractFixture, showSummarySchema)).toEqual([]);
  });

  it("SPEC-0011:CT-01 o Card cobre todos os campos obrigatórios do schema", () => {
    expect(Object.keys(displayed).sort()).toEqual(
      [...(showSummarySchema.required ?? [])].sort(),
    );
  });

  it("SPEC-0011:CT-01 nenhum campo obrigatório do contrato deixa de ser exibido pelo Card", () => {
    const summary = contractFixture as ShowSummary;
    const { container } = renderCard(summary);

    for (const [field, check] of Object.entries(displayed)) {
      if (check === "não exibido") continue;
      try {
        check(container, summary);
      } catch (error) {
        throw new Error(
          `campo ${field} do ShowSummary não exibido: ${(error as Error).message}`,
        );
      }
    }
  });
});
