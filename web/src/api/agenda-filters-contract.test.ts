// @vitest-environment node
import { spawnSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * SPEC-0025 CT-01 — o snapshot `docs/api/openapi.v1.json` (de onde sai o cliente TypeScript) publica os filtros da
 * agenda como no §6:
 * - `GET /api/v1/cities/{slug}/agenda` com os parâmetros de consulta `from` e `to` (`date`) e `styles` (texto);
 * - 200 com `period: {from, to}` (datas) e `total` (inteiro), além de `city` e `shows` (contrato da SPEC-0008);
 * - 400 em ProblemDetails com `agenda.invalid_period` e `agenda.invalid_styles`; 404 com `agenda.city_not_found`;
 * - o cliente `src/api/schema.d.ts` foi regenerado e `(cd web && pnpm api:check)` passa.
 */

type Schema = {
  $ref?: string;
  type?: string | string[];
  format?: string;
  required?: string[];
  properties?: Record<string, Schema>;
  oneOf?: Schema[];
  anyOf?: Schema[];
  allOf?: Schema[];
};

type Parameter = { name: string; in: string; schema?: Schema };

type Response = {
  description?: string;
  content?: Record<string, { schema?: Schema }>;
};

type Operation = {
  parameters?: Parameter[];
  responses: Record<string, Response>;
};

type OpenApiDocument = {
  paths: Record<string, Record<string, Operation>>;
  components?: { schemas?: Record<string, Schema> };
};

const webDir = fileURLToPath(new URL("../..", import.meta.url));
const snapshotPath = fileURLToPath(
  new URL("../../../docs/api/openapi.v1.json", import.meta.url),
);
const clientPath = fileURLToPath(new URL("./schema.d.ts", import.meta.url));
const document = JSON.parse(
  readFileSync(snapshotPath, "utf8"),
) as OpenApiDocument;

const agendaPath = "/api/v1/cities/{slug}/agenda";

function resolve(schema: Schema | undefined): Schema {
  let current = schema ?? {};
  for (let depth = 0; current.$ref && depth < 10; depth++) {
    const name = current.$ref.replace("#/components/schemas/", "");
    current = document.components?.schemas?.[name] ?? {};
  }
  return current;
}

/** O esquema sem o `null` de um `oneOf`/`anyOf` anulável (`T | null`). */
function nonNull(schema: Schema | undefined): Schema {
  const resolved = resolve(schema);
  const options = [...(resolved.oneOf ?? []), ...(resolved.anyOf ?? [])]
    .map(resolve)
    .filter((option) => option.type !== "null");
  return options.length === 1 ? resolve(options[0]) : resolved;
}

function types(schema: Schema): string[] {
  const resolved = nonNull(schema);
  return [resolved.type ?? []].flat().filter((type) => type !== "null");
}

function property(schema: Schema, name: string): Schema {
  const found = nonNull(schema).properties?.[name];
  expect(found, `propriedade ${name} ausente`).toBeDefined();
  return nonNull(found);
}

function agenda(): Operation {
  const found = document.paths[agendaPath]?.["get"];
  expect(found, `GET ${agendaPath} ausente no snapshot`).toBeDefined();
  return found as Operation;
}

function okBody(): Schema {
  const content = agenda().responses["200"]?.content?.["application/json"];
  expect(content, `GET ${agendaPath} 200 sem application/json`).toBeDefined();
  return resolve(content?.schema);
}

function queryParameter(name: string): Schema {
  const found = (agenda().parameters ?? []).find(
    (parameter) => parameter.in === "query" && parameter.name === name,
  );
  expect(found, `parâmetro de consulta ${name} ausente`).toBeDefined();
  return nonNull(found?.schema);
}

describe("OpenAPI dos filtros da agenda fiel ao contrato §6", () => {
  it("SPEC-0025:CT-01 a agenda aceita from e to (date) e styles (texto) na consulta", () => {
    for (const name of ["from", "to"]) {
      const schema = queryParameter(name);
      expect(types(schema), `tipo de ${name}`).toContain("string");
      expect(schema.format, `formato de ${name}`).toBe("date");
    }
    expect(types(queryParameter("styles"))).toContain("string");
  });

  it("SPEC-0025:CT-01 o 200 tem city, shows, period {from, to} e total", () => {
    const body = okBody();
    expect(Object.keys(body.properties ?? {}).sort()).toEqual(
      ["city", "period", "shows", "total"].sort(),
    );
    expect(body.required ?? []).toEqual(
      expect.arrayContaining(["city", "shows", "period", "total"]),
    );
    const period = property(body, "period");
    expect(Object.keys(period.properties ?? {}).sort()).toEqual(["from", "to"]);
    for (const name of ["from", "to"]) {
      const date = property(period, name);
      expect(types(date), `tipo de period.${name}`).toContain("string");
      expect(date.format, `formato de period.${name}`).toBe("date");
    }
    expect(types(property(body, "total"))).toContain("integer");
  });

  it("SPEC-0025:CT-01 declara 400 com agenda.invalid_period e agenda.invalid_styles e 404 com agenda.city_not_found, em ProblemDetails", () => {
    const badRequest = agenda().responses["400"];
    expect(badRequest, "400 não declarado").toBeDefined();
    expect(badRequest?.content?.["application/problem+json"]).toBeDefined();
    for (const code of ["agenda.invalid_period", "agenda.invalid_styles"]) {
      expect(badRequest?.description ?? "").toContain(`code=${code}`);
    }
    const notFound = agenda().responses["404"];
    expect(notFound, "404 não declarado").toBeDefined();
    expect(notFound?.content?.["application/problem+json"]).toBeDefined();
    expect(notFound?.description ?? "").toContain("code=agenda.city_not_found");
  });

  it("SPEC-0025:CT-01 o cliente src/api/schema.d.ts foi regenerado com period e total", () => {
    const client = readFileSync(clientPath, "utf8");
    const response = /CityAgendaResponse: \{([\s\S]*?)\n {4}\};/.exec(client);
    expect(response, "CityAgendaResponse ausente no cliente").not.toBeNull();
    expect(response?.[1]).toContain("period:");
    expect(response?.[1]).toContain("total:");
  });

  it("SPEC-0025:CT-01 pnpm api:check passa com o cliente regenerado", () => {
    const run = spawnSync("pnpm", ["run", "api:check"], {
      cwd: webDir,
      encoding: "utf8",
      env: process.env,
      timeout: 180_000,
    });

    expect(run.status, `${run.stdout}\n${run.stderr}`).toBe(0);
  }, 180_000);
});
