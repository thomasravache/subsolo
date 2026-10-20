// @vitest-environment node
import { spawnSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * SPEC-0028 CT-01 — o snapshot `docs/api/openapi.v1.json` (de onde sai o cliente TypeScript) publica os endpoints de
 * apoiadores do §6:
 * - `GET /api/v1/supporters`: público (sem 401), 200 com a lista `[{id (uuid), name, url|null}]`;
 * - `POST /api/v1/admin/supporters`: corpo `{name, url|null}`, 201 `{id}` (uuid), 400 `site.invalid_supporter` e
 *   `security.csrf_invalid`;
 * - `DELETE /api/v1/admin/supporters/{id}` (uuid): 204 sem corpo, 404 `site.supporter_not_found` e 400
 *   `security.csrf_invalid`;
 * - as rotas de admin declaram 401 `auth.required`, 403 `auth.forbidden` e 429 `security.rate_limited`, como o resto da
 *   API; o cliente `src/api/schema.d.ts` foi regenerado e `(cd web && pnpm api:check)` passa.
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

type Response = {
  description?: string;
  content?: Record<string, { schema?: Schema }>;
};

type Parameter = { name: string; in: string; schema?: Schema };

type Operation = {
  security?: unknown[];
  parameters?: Parameter[];
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

/** O objeto tem exatamente as propriedades `names` (sem exigir o tipo de cada uma). */
function expectKeys(schema: Schema, names: string[], where: string) {
  expect(Object.keys(resolve(schema).properties ?? {}).sort(), where).toEqual(
    [...names].sort(),
  );
}

const supportersPath = "/api/v1/supporters";
const adminSupportersPath = "/api/v1/admin/supporters";
const adminSupporterPath = "/api/v1/admin/supporters/{id}";

/** Os erros comuns das rotas de admin (§6): 401, 403, 429 e o 400 do anti-forgery. */
function expectAdminErrors(path: string, method: string) {
  expectProblem(path, method, "401", ["auth.required"]);
  expectProblem(path, method, "403", ["auth.forbidden"]);
  expectProblem(path, method, "429", ["security.rate_limited"]);
  expectProblem(path, method, "400", ["security.csrf_invalid"]);
}

/** `null` é aceito: `type` com "null", `nullable` ou alternativa `{type: "null"}`. */
function acceptsNull(schema: Schema): boolean {
  return (
    types(schema).includes("null") ||
    (resolve(schema) as { nullable?: boolean }).nullable === true
  );
}

describe("OpenAPI de apoiadores fiel ao contrato §6", () => {
  it("SPEC-0028:CT-01 GET /api/v1/supporters é público e devolve [{id, name, url|null}]", () => {
    const body = jsonResponse(supportersPath, "get", "200");
    expect(types(body)).toContain("array");
    const item = resolve(body.items);
    expectKeys(item, ["id", "name", "url"], "GET /supporters 200 item");
    expect(types(property(item, "id") ?? {})).toContain("string");
    expect(resolve(property(item, "id")).format).toBe("uuid");
    expect(types(property(item, "name") ?? {})).toContain("string");
    const url = property(item, "url") ?? {};
    expect(types(url)).toContain("string");
    expect(acceptsNull(url), "url não aceita null").toBe(true);
    expect(resolve(item).required ?? []).toEqual(
      expect.arrayContaining(["id", "name"]),
    );
    expect(
      operation(supportersPath, "get").responses["401"],
      "GET /supporters é público (sem 401)",
    ).toBeUndefined();
  });

  it("SPEC-0028:CT-01 POST /api/v1/admin/supporters recebe {name, url|null} e responde 201 {id}", () => {
    const request = resolve(
      operation(adminSupportersPath, "post").requestBody?.content?.[
        "application/json"
      ]?.schema,
    );
    expectKeys(request, ["name", "url"], "POST /admin/supporters corpo");
    expect(types(property(request, "name") ?? {})).toContain("string");
    expect(
      acceptsNull(property(request, "url") ?? {}),
      "url não aceita null",
    ).toBe(true);
    const created = jsonResponse(adminSupportersPath, "post", "201");
    expectKeys(created, ["id"], "POST /admin/supporters 201");
    expect(resolve(property(created, "id")).format).toBe("uuid");
    expectProblem(adminSupportersPath, "post", "400", [
      "site.invalid_supporter",
      "security.csrf_invalid",
    ]);
    expectAdminErrors(adminSupportersPath, "post");
  });

  it("SPEC-0028:CT-01 DELETE /api/v1/admin/supporters/{id} responde 204 sem corpo e 404 site.supporter_not_found", () => {
    const response = operation(adminSupporterPath, "delete").responses["204"];
    expect(response, "DELETE 204 não declarado").toBeDefined();
    expect(
      Object.keys(response?.content ?? {}),
      "DELETE 204 com corpo",
    ).toEqual([]);
    expectProblem(adminSupporterPath, "delete", "404", [
      "site.supporter_not_found",
    ]);
    expectAdminErrors(adminSupporterPath, "delete");
    const id = (
      operation(adminSupporterPath, "delete").parameters ?? []
    ).filter((parameter) => parameter.in === "path");
    expect(id.map((parameter) => parameter.name)).toEqual(["id"]);
    expect(resolve(id[0]?.schema).format).toBe("uuid");
  });

  it("SPEC-0028:CT-01 o cliente src/api/schema.d.ts foi regenerado com as rotas de apoiadores", () => {
    const client = readFileSync(clientPath, "utf8");
    for (const path of [
      supportersPath,
      adminSupportersPath,
      adminSupporterPath,
    ]) {
      expect(client, `${path} ausente no cliente`).toContain(`"${path}": {`);
    }
  });

  it("SPEC-0028:CT-01 pnpm api:check passa com o cliente regenerado", () => {
    const run = spawnSync("pnpm", ["run", "api:check"], {
      cwd: webDir,
      encoding: "utf8",
      env: process.env,
      timeout: 180_000,
    });

    expect(run.status, `${run.stdout}\n${run.stderr}`).toBe(0);
  }, 180_000);
});
