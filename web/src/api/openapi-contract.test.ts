// @vitest-environment node
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * SPEC-0008 CT-02 (Emendas 5 e 6) — o snapshot `docs/api/openapi.v1.json`, de onde sai o cliente TypeScript, é fiel ao
 * contrato §6 de `GET /api/v1/cities/{slug}/agenda`: padrão do slug, UF com 2 caracteres, enum de status do
 * ShowSummary, `startsAt` como date-time e o `code` no ProblemDetails dos erros 400 e 404.
 */

type Schema = {
  $ref?: string;
  type?: string | string[];
  format?: string;
  pattern?: string;
  minLength?: number;
  maxLength?: number;
  enum?: unknown[];
  properties?: Record<string, Schema>;
  oneOf?: Schema[];
  anyOf?: Schema[];
  allOf?: Schema[];
};

type Parameter = { name: string; in: string; schema?: Schema };

type Operation = {
  parameters?: Parameter[];
  responses: Record<string, { content?: Record<string, { schema?: Schema }> }>;
};

type OpenApiDocument = {
  paths: Record<string, Record<string, Operation>>;
  components?: { schemas?: Record<string, Schema> };
};

const snapshotPath = fileURLToPath(
  new URL("../../../docs/api/openapi.v1.json", import.meta.url),
);
const document = JSON.parse(
  readFileSync(snapshotPath, "utf8"),
) as OpenApiDocument;

const agendaPath = "/api/v1/cities/{slug}/agenda";

/** Segue `$ref` (só referências locais `#/components/schemas/...`). */
function resolve(schema: Schema | undefined): Schema {
  let current = schema ?? {};
  for (let depth = 0; current.$ref && depth < 10; depth++) {
    const name = current.$ref.replace("#/components/schemas/", "");
    current = document.components?.schemas?.[name] ?? {};
  }
  return current;
}

/** Propriedade de um schema de objeto, resolvida (inclusive dentro de allOf/oneOf/anyOf). */
function property(schema: Schema, name: string): Schema | undefined {
  const resolved = resolve(schema);
  const direct = resolved.properties?.[name];
  if (direct) {
    return resolve(direct);
  }
  for (const part of [
    ...(resolved.allOf ?? []),
    ...(resolved.oneOf ?? []),
    ...(resolved.anyOf ?? []),
  ]) {
    const found = property(part, name);
    if (found) {
      return found;
    }
  }
  return undefined;
}

/** Tipos aceitos por um schema, incluindo as alternativas de oneOf/anyOf. */
function types(schema: Schema): string[] {
  const own = schema.type
    ? Array.isArray(schema.type)
      ? schema.type
      : [schema.type]
    : [];
  const alternatives = [...(schema.oneOf ?? []), ...(schema.anyOf ?? [])]
    .map(resolve)
    .flatMap(types);
  return [...own, ...alternatives];
}

/** Valores de enum de um schema, incluindo as alternativas de oneOf/anyOf (sem null). */
function enumValues(schema: Schema): unknown[] {
  const alternatives = [...(schema.oneOf ?? []), ...(schema.anyOf ?? [])]
    .map(resolve)
    .flatMap(enumValues);
  return [...(schema.enum ?? []), ...alternatives].filter(
    (value) => value !== null,
  );
}

function agenda(): Operation {
  const operation = document.paths[agendaPath]?.get;
  expect(operation, `GET ${agendaPath} ausente no snapshot`).toBeDefined();
  return operation as Operation;
}

function responseSchema(status: string, mediaType: string): Schema {
  const content = agenda().responses[status]?.content?.[mediaType];
  expect(content, `resposta ${status} sem ${mediaType}`).toBeDefined();
  return resolve(content?.schema);
}

const city = () =>
  property(responseSchema("200", "application/json"), "city") ?? {};

function showSummary(): Schema {
  const shows = property(responseSchema("200", "application/json"), "shows");
  expect(shows, "CityAgendaResponse sem shows").toBeDefined();
  return resolve((shows as Schema & { items?: Schema }).items);
}

describe("OpenAPI da agenda fiel ao contrato §6", () => {
  it("SPEC-0008:CT-02 slug com pattern ^[a-z0-9-]{2,60}$", () => {
    const slug = agenda().parameters?.find(
      (parameter) => parameter.name === "slug" && parameter.in === "path",
    );

    expect(resolve(slug?.schema).pattern).toBe("^[a-z0-9-]{2,60}$");
  });

  it("SPEC-0008:CT-02 state da cidade com exatamente 2 caracteres", () => {
    const state = property(city(), "state") ?? {};

    expect(types(state)).toContain("string");
    expect(state.minLength).toBe(2);
    expect(state.maxLength).toBe(2);
  });

  it("SPEC-0008:CT-02 status do ShowSummary com enum scheduled|cancelled|postponed", () => {
    const status = property(showSummary(), "status") ?? {};

    expect([...enumValues(status)].sort()).toEqual([
      "cancelled",
      "postponed",
      "scheduled",
    ]);
  });

  it("SPEC-0008:CT-02 startsAt do ShowSummary como date-time", () => {
    const startsAt = property(showSummary(), "startsAt") ?? {};

    expect(types(startsAt)).toContain("string");
    expect(startsAt.format).toBe("date-time");
  });

  it.each(["400", "404"])(
    "SPEC-0008:CT-02 ProblemDetails do %s declara o code",
    (status) => {
      const code = property(
        responseSchema(status, "application/problem+json"),
        "code",
      );

      expect(
        code,
        `ProblemDetails do ${status} sem a propriedade code`,
      ).toBeDefined();
      expect(types(code ?? {})).toContain("string");
    },
  );
});
