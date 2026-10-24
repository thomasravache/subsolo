// @vitest-environment node
import { spawnSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * SPEC-0026 CT-01 — o snapshot `docs/api/openapi.v1.json` (de onde sai o cliente TypeScript) publica os dois endpoints
 * do perfil público como no §6:
 * - `GET /api/v1/profiles/{type}/{slug}`: 200 `{id, type, name, slug, city: {slug, name}, styles: [{slug, name}],
 *   links: {instagram?, bandcamp?, metalArchives?}}`; 404 em ProblemDetails com `profiles.not_found`;
 * - `GET /api/v1/shows?profileId=&period=`: `profileId` (uuid) e `period` na consulta; 200 `{shows: ShowSummary[]}` (o
 *   resumo da agenda, SPEC-0008, mais `city: {slug, name, timeZone}` — Emenda v2, b — e `flyerId: string | null` —
 *   SPEC-0031 H26-03b); 400 em ProblemDetails com
 *   `events.invalid_query`;
 * - Emenda v2 (a, H23-08): `GET /api/v1/profiles/lookup?q=&type=`: `q` e `type` na consulta; 200
 *   `[{id, type, name, slug, city: {slug, name}}]`; 400 em ProblemDetails com `profiles.invalid_query`;
 * - o cliente `src/api/schema.d.ts` foi regenerado com as duas rotas e `(cd web && pnpm api:check)` passa.
 */
type Schema = {
  $ref?: string;
  type?: string | string[];
  format?: string;
  items?: Schema;
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

const profilePath = "/api/v1/profiles/{type}/{slug}";
const showsPath = "/api/v1/shows";
const lookupPath = "/api/v1/profiles/lookup";

const summaryFields = [
  "attendingCount",
  "flyerThumbUrl",
  "id",
  "lineup",
  "slug",
  "startsAt",
  "status",
  "styles",
  "title",
  "venueName",
];

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

function propertiesOf(schema: Schema | undefined): string[] {
  return Object.keys(nonNull(schema).properties ?? {}).sort();
}

function property(schema: Schema, name: string): Schema {
  const found = nonNull(schema).properties?.[name];
  expect(found, `propriedade ${name} ausente`).toBeDefined();
  return nonNull(found);
}

function get(path: string): Operation {
  const found = document.paths[path]?.["get"];
  expect(found, `GET ${path} ausente no snapshot`).toBeDefined();
  return found as Operation;
}

function okBody(path: string): Schema {
  const content = get(path).responses["200"]?.content?.["application/json"];
  expect(content, `GET ${path} 200 sem application/json`).toBeDefined();
  return resolve(content?.schema);
}

function parameters(path: string, where: string): Parameter[] {
  return (get(path).parameters ?? []).filter(
    (parameter) => parameter.in === where,
  );
}

function expectProblem(path: string, status: string, code: string) {
  const response = get(path).responses[status];
  expect(response, `GET ${path}: ${status} não declarado`).toBeDefined();
  expect(
    response?.content?.["application/problem+json"],
    `GET ${path}: ${status} sem ProblemDetails`,
  ).toBeDefined();
  expect(response?.description ?? "").toContain(`code=${code}`);
}

describe("OpenAPI do perfil público fiel ao contrato §6", () => {
  it("SPEC-0026:CT-01 GET /api/v1/profiles/{type}/{slug} tem os parâmetros de caminho type e slug", () => {
    expect(
      parameters(profilePath, "path")
        .map((parameter) => parameter.name)
        .sort(),
    ).toEqual(["slug", "type"]);
  });

  it("SPEC-0026:CT-01 o 200 do perfil tem o corpo do §6, sem dono nem contato", () => {
    const body = okBody(profilePath);
    expect(propertiesOf(body)).toEqual([
      "city",
      "id",
      "links",
      "name",
      "slug",
      "styles",
      "type",
    ]);
    expect(propertiesOf(property(body, "city"))).toEqual(["name", "slug"]);
    expect(propertiesOf(property(body, "styles").items)).toEqual([
      "name",
      "slug",
    ]);
    expect(propertiesOf(property(body, "links"))).toEqual([
      "bandcamp",
      "instagram",
      "metalArchives",
    ]);
  });

  it("SPEC-0026:CT-01 o perfil declara 404 com profiles.not_found em ProblemDetails", () => {
    expectProblem(profilePath, "404", "profiles.not_found");
  });

  it("SPEC-0026:CT-01 GET /api/v1/shows aceita profileId (uuid) e period na consulta", () => {
    const query = parameters(showsPath, "query");
    const profileId = query.find((parameter) => parameter.name === "profileId");
    expect(profileId, "parâmetro profileId ausente").toBeDefined();
    expect(nonNull(profileId?.schema).format).toBe("uuid");
    expect(
      query.some((parameter) => parameter.name === "period"),
      "parâmetro period ausente",
    ).toBe(true);
  });

  it("SPEC-0026:CT-01 o 200 dos shows do perfil é {shows} com o resumo de show da agenda", () => {
    const body = okBody(showsPath);
    expect(propertiesOf(body)).toEqual(["shows"]);
    const shows = property(body, "shows");
    expect([shows.type].flat()).toContain("array");
    // O resumo da agenda (SPEC-0008) mais a cidade do show (Emenda v2, b) e, pela SPEC-0031 (H26-03b, contrato v3,
    // aditivo), o `flyerId` do flyer do show (string | null).
    expect(propertiesOf(shows.items)).toEqual(
      [...summaryFields, "city", "flyerId"].sort(),
    );
    expect(
      [property(nonNull(shows.items), "flyerId").type].flat().sort(),
    ).toEqual(["null", "string"]);
    expect(propertiesOf(property(nonNull(shows.items), "city"))).toEqual([
      "name",
      "slug",
      "timeZone",
    ]);
  });

  it("SPEC-0026:CT-01 os shows do perfil declaram 400 com events.invalid_query em ProblemDetails", () => {
    expectProblem(showsPath, "400", "events.invalid_query");
  });

  it("SPEC-0026:CT-01 GET /api/v1/profiles/lookup aceita q e type e devolve [{id, type, name, slug, city}]", () => {
    expect(
      parameters(lookupPath, "query")
        .map((parameter) => parameter.name)
        .sort(),
    ).toEqual(["q", "type"]);
    const body = okBody(lookupPath);
    expect([body.type].flat()).toContain("array");
    expect(propertiesOf(body.items)).toEqual([
      "city",
      "id",
      "name",
      "slug",
      "type",
    ]);
    expect(propertiesOf(property(nonNull(body.items), "city"))).toEqual([
      "name",
      "slug",
    ]);
  });

  it("SPEC-0026:CT-01 a busca de perfis declara 400 com profiles.invalid_query em ProblemDetails", () => {
    expectProblem(lookupPath, "400", "profiles.invalid_query");
  });

  it("SPEC-0026:CT-01 o cliente src/api/schema.d.ts foi regenerado com as rotas novas", () => {
    const client = readFileSync(clientPath, "utf8");
    expect(client).toContain(`"${profilePath}": {`);
    expect(client).toContain(`"${lookupPath}": {`);
    const start = client.indexOf(`"${showsPath}": {`);
    expect(start, `${showsPath} ausente no cliente`).toBeGreaterThanOrEqual(0);
    const next = client.indexOf('\n  "/', start + 1);
    const block = client.slice(start, next < 0 ? undefined : next);
    expect(block, "GET /api/v1/shows ausente no cliente").toMatch(
      /\bget: operations\[/,
    );
  });

  it("SPEC-0026:CT-01 pnpm api:check passa com o cliente regenerado", () => {
    const run = spawnSync("pnpm", ["run", "api:check"], {
      cwd: webDir,
      encoding: "utf8",
      env: process.env,
      timeout: 180_000,
    });

    expect(run.status, `${run.stdout}\n${run.stderr}`).toBe(0);
  }, 180_000);
});
