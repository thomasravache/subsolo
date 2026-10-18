// @vitest-environment node
import { spawnSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * SPEC-0017 CT-01 — o snapshot `docs/api/openapi.v1.json` (de onde sai o cliente TypeScript) publica
 * `GET /api/v1/styles`, `/api/v1/admin/cities` (GET, POST, PUT `{slug}`) e `/api/v1/admin/styles` (POST, PUT `{slug}`)
 * com os corpos, status e códigos do §6; o cliente `src/api/schema.d.ts` foi regenerado com eles e
 * `(cd web && pnpm api:check)` passa.
 */

type Schema = {
  $ref?: string;
  type?: string | string[];
  items?: Schema;
  required?: string[];
  properties?: Record<string, Schema>;
  oneOf?: Schema[];
  anyOf?: Schema[];
  allOf?: Schema[];
};

type Response = {
  description?: string;
  content?: Record<string, { schema?: Schema }>;
};

type Operation = {
  security?: unknown[];
  requestBody?: { content?: Record<string, { schema?: Schema }> };
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

const stylesPath = "/api/v1/styles";
const adminCitiesPath = "/api/v1/admin/cities";
const adminCityPath = "/api/v1/admin/cities/{slug}";
const adminStylesPath = "/api/v1/admin/styles";
const adminStylePath = "/api/v1/admin/styles/{slug}";

function resolve(schema: Schema | undefined): Schema {
  let current = schema ?? {};
  for (let depth = 0; current.$ref && depth < 10; depth++) {
    const name = current.$ref.replace("#/components/schemas/", "");
    current = document.components?.schemas?.[name] ?? {};
  }
  return current;
}

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

function operation(path: string, method: string): Operation {
  const found = document.paths[path]?.[method];
  expect(
    found,
    `${method.toUpperCase()} ${path} ausente no snapshot`,
  ).toBeDefined();
  return found as Operation;
}

function requestSchema(path: string, method: string): Schema {
  const content = operation(path, method).requestBody?.content?.[
    "application/json"
  ];
  expect(
    content,
    `${method.toUpperCase()} ${path} sem corpo application/json`,
  ).toBeDefined();
  return resolve(content?.schema);
}

function jsonResponse(path: string, method: string, status: string): Schema {
  const content = operation(path, method).responses[status]?.content?.[
    "application/json"
  ];
  expect(
    content,
    `${method.toUpperCase()} ${path} ${status} sem application/json`,
  ).toBeDefined();
  return resolve(content?.schema);
}

/** O objeto tem exatamente as propriedades `names`, todas string e obrigatórias. */
function expectStrings(schema: Schema, names: string[], where: string) {
  expect(Object.keys(resolve(schema).properties ?? {}).sort(), where).toEqual(
    [...names].sort(),
  );
  for (const name of names) {
    expect(types(property(schema, name) ?? {}), `${where}.${name}`).toContain(
      "string",
    );
  }
  expect(resolve(schema).required ?? [], where).toEqual(
    expect.arrayContaining(names),
  );
}

/** A resposta de erro é ProblemDetails e cita cada código (`code=…`) na descrição. */
function expectProblem(
  path: string,
  method: string,
  status: string,
  codes: string[],
) {
  const response = operation(path, method).responses[status];
  const where = `${method.toUpperCase()} ${path} ${status}`;
  expect(response, `${where} não declarado`).toBeDefined();
  expect(
    response?.content?.["application/problem+json"],
    `${where} sem application/problem+json`,
  ).toBeDefined();
  for (const code of codes) {
    expect(response?.description ?? "", where).toContain(`code=${code}`);
  }
}

const city = ["slug", "name", "state", "timeZone"];
const style = ["slug", "name"];

/** Os erros comuns de toda rota de admin (§6): 401, 403 e, nas mutações, o 400 do anti-forgery. */
function expectAdminErrors(path: string, method: string) {
  expectProblem(path, method, "401", ["auth.required"]);
  expectProblem(path, method, "403", ["auth.forbidden"]);
  if (method !== "get") {
    expectProblem(path, method, "400", ["security.csrf_invalid"]);
  }
}

describe("OpenAPI de cidades e estilos fiel ao contrato §6", () => {
  it("SPEC-0017:CT-01 GET /api/v1/styles é público e devolve [{slug, name}]", () => {
    const list = jsonResponse(stylesPath, "get", "200");

    expect(types(list)).toContain("array");
    expectStrings(list.items ?? {}, style, `${stylesPath} 200 item`);
    expect(Object.keys(operation(stylesPath, "get").responses)).not.toContain(
      "401",
    );
  });

  it("SPEC-0017:CT-01 GET /api/v1/admin/cities devolve [{slug, name, state, timeZone}] com 401 e 403", () => {
    const list = jsonResponse(adminCitiesPath, "get", "200");

    expect(types(list)).toContain("array");
    expectStrings(list.items ?? {}, city, `${adminCitiesPath} 200 item`);
    expectAdminErrors(adminCitiesPath, "get");
  });

  it("SPEC-0017:CT-01 POST /api/v1/admin/cities recebe {name, state, timeZone, slug (string|null)} e responde 201, 400, 409", () => {
    const body = requestSchema(adminCitiesPath, "post");
    for (const name of ["name", "state", "timeZone"]) {
      expect(types(property(body, name) ?? {}), name).toContain("string");
    }
    expect(types(property(body, "slug") ?? {})).toEqual(
      expect.arrayContaining(["string", "null"]),
    );

    expectStrings(
      jsonResponse(adminCitiesPath, "post", "201"),
      city,
      `POST ${adminCitiesPath} 201`,
    );
    expectProblem(adminCitiesPath, "post", "400", [
      "events.invalid_city",
      "events.city_slug_reserved",
      "security.csrf_invalid",
    ]);
    expectProblem(adminCitiesPath, "post", "409", ["events.city_slug_taken"]);
    expectAdminErrors(adminCitiesPath, "post");
  });

  it("SPEC-0017:CT-01 PUT /api/v1/admin/cities/{slug} recebe {name, state, timeZone} e responde 200, 400, 404", () => {
    const body = requestSchema(adminCityPath, "put");
    expect(Object.keys(body.properties ?? {}).sort()).toEqual(
      ["name", "state", "timeZone"].sort(),
    );

    expectStrings(
      jsonResponse(adminCityPath, "put", "200"),
      city,
      `PUT ${adminCityPath} 200`,
    );
    expectProblem(adminCityPath, "put", "400", ["events.invalid_city"]);
    expectProblem(adminCityPath, "put", "404", ["events.city_not_found"]);
    expectAdminErrors(adminCityPath, "put");
  });

  it("SPEC-0017:CT-01 POST /api/v1/admin/styles recebe {name} e responde 201, 400, 409", () => {
    const body = requestSchema(adminStylesPath, "post");
    expect(Object.keys(body.properties ?? {})).toEqual(["name"]);
    expect(types(property(body, "name") ?? {})).toContain("string");

    expectStrings(
      jsonResponse(adminStylesPath, "post", "201"),
      style,
      `POST ${adminStylesPath} 201`,
    );
    expectProblem(adminStylesPath, "post", "400", ["events.invalid_style"]);
    expectProblem(adminStylesPath, "post", "409", ["events.style_taken"]);
    expectAdminErrors(adminStylesPath, "post");
  });

  it("SPEC-0017:CT-01 PUT /api/v1/admin/styles/{slug} recebe {name} e responde 200, 400, 404, 409", () => {
    const body = requestSchema(adminStylePath, "put");
    expect(Object.keys(body.properties ?? {})).toEqual(["name"]);

    expectStrings(
      jsonResponse(adminStylePath, "put", "200"),
      style,
      `PUT ${adminStylePath} 200`,
    );
    expectProblem(adminStylePath, "put", "400", ["events.invalid_style"]);
    expectProblem(adminStylePath, "put", "404", ["events.style_not_found"]);
    expectProblem(adminStylePath, "put", "409", ["events.style_taken"]);
    expectAdminErrors(adminStylePath, "put");
  });

  it("SPEC-0017:CT-01 o cliente src/api/schema.d.ts foi regenerado com as rotas de cidades e estilos", () => {
    const client = readFileSync(clientPath, "utf8");

    for (const path of [
      stylesPath,
      adminCitiesPath,
      adminCityPath,
      adminStylesPath,
      adminStylePath,
    ]) {
      expect(client).toContain(`"${path}"`);
    }
  });

  it("SPEC-0017:CT-01 pnpm api:check passa com o cliente regenerado", () => {
    const run = spawnSync("pnpm", ["run", "api:check"], {
      cwd: webDir,
      encoding: "utf8",
      env: process.env,
      timeout: 180_000,
    });

    expect(run.status, `${run.stdout}\n${run.stderr}`).toBe(0);
  }, 180_000);
});
