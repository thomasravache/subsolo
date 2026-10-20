// @vitest-environment node
import { spawnSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * SPEC-0029 CT-01 — o snapshot `docs/api/openapi.v1.json` (de onde sai o cliente TypeScript) publica os endpoints do
 * "vou" do §6:
 * - `PUT /api/v1/me/attendances/{showId}` (uuid): 204 sem corpo, 404 `engagement.show_not_found`, 409
 *   `engagement.show_not_attendable`, 401 `auth.required`, 400 `security.csrf_invalid` e 429 `security.rate_limited`;
 * - `DELETE /api/v1/me/attendances/{showId}`: 204 sem corpo, 401, 400 do anti-forgery e 429;
 * - `GET /api/v1/me/attendances?period=upcoming`: 200 `{showIds: uuid[]}`, 401 e 429;
 * - `GET /api/v1/attendances/counts?showIds=a,b,c`: público (sem 401), 200 `{counts: {"<showId>": n}}` (só números
 *   inteiros, nada de quem vai) e 400 `engagement.invalid_query`;
 * - o cliente `src/api/schema.d.ts` foi regenerado com as rotas e `(cd web && pnpm api:check)` passa. (As assinaturas
 *   do `IAttendanceApi` e do `IEventsCatalog.GetShowStateAsync` são conferidas nos testes unitários do Engagement.)
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
  additionalProperties?: Schema | boolean;
};

type Response = {
  description?: string;
  content?: Record<string, { schema?: Schema }>;
};

type Parameter = {
  name: string;
  in: string;
  required?: boolean;
  schema?: Schema;
};

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

const attendPath = "/api/v1/me/attendances/{showId}";
const myAttendancesPath = "/api/v1/me/attendances";
const countsPath = "/api/v1/attendances/counts";

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

/** Os erros comuns das rotas de fã (§6): 401, 429 e, nas mutações, o 400 do anti-forgery. */
function expectFanErrors(path: string, method: string) {
  expectProblem(path, method, "401", ["auth.required"]);
  expectProblem(path, method, "429", ["security.rate_limited"]);
  if (method !== "get") {
    expectProblem(path, method, "400", ["security.csrf_invalid"]);
  }
}

function pathParameters(path: string, method: string): Parameter[] {
  return (operation(path, method).parameters ?? []).filter(
    (parameter) => parameter.in === "path",
  );
}

/** Os erros comuns das mutações de fã (§6): 401, 429 e o 400 do anti-forgery. */
function expectMutationErrors(path: string, method: string) {
  expectFanErrors(path, method);
  expectProblem(path, method, "400", ["security.csrf_invalid"]);
}

/** 204 sem corpo. */
function expectNoContent(path: string, method: string) {
  const response = operation(path, method).responses["204"];
  expect(
    response,
    `${method.toUpperCase()} ${path} 204 não declarado`,
  ).toBeDefined();
  expect(
    Object.keys(response?.content ?? {}),
    `${method.toUpperCase()} ${path} 204 com corpo`,
  ).toEqual([]);
}

function queryParameter(path: string, method: string, name: string) {
  return (operation(path, method).parameters ?? []).find(
    (parameter) => parameter.in === "query" && parameter.name === name,
  );
}

describe("OpenAPI do vou fiel ao contrato §6", () => {
  it.each([
    ["put", attendPath],
    ["delete", attendPath],
  ])(
    "SPEC-0029:CT-01 %s %s responde 204 sem corpo, com 401, 400 do anti-forgery e 429",
    (method, path) => {
      expectNoContent(path, method);
      expectMutationErrors(path, method);
    },
  );

  it("SPEC-0029:CT-01 PUT declara 404 engagement.show_not_found e 409 engagement.show_not_attendable", () => {
    expectProblem(attendPath, "put", "404", ["engagement.show_not_found"]);
    expectProblem(attendPath, "put", "409", ["engagement.show_not_attendable"]);
  });

  it("SPEC-0029:CT-01 o show vai pelo id (uuid) no caminho", () => {
    for (const method of ["put", "delete"]) {
      const parameters = pathParameters(attendPath, method);
      expect(parameters.map((parameter) => parameter.name)).toEqual(["showId"]);
      expect(resolve(parameters[0]?.schema).format).toBe("uuid");
    }
  });

  it("SPEC-0029:CT-01 GET /api/v1/me/attendances?period= devolve {showIds: uuid[]}", () => {
    expect(
      queryParameter(myAttendancesPath, "get", "period"),
      "sem o parâmetro period",
    ).toBeDefined();
    const body = jsonResponse(myAttendancesPath, "get", "200");
    expectKeys(body, ["showIds"], "GET /me/attendances 200");
    const ids = property(body, "showIds") ?? {};
    expect(types(ids)).toContain("array");
    expect(types(resolve(ids.items))).toContain("string");
    expect(resolve(ids.items).format).toBe("uuid");
    expect(resolve(body).required ?? []).toContain("showIds");
    expectFanErrors(myAttendancesPath, "get");
  });

  it("SPEC-0029:CT-01 GET /api/v1/attendances/counts é público e devolve só {counts} com números", () => {
    expect(
      queryParameter(countsPath, "get", "showIds"),
      "sem o parâmetro showIds",
    ).toBeDefined();
    const body = jsonResponse(countsPath, "get", "200");
    expectKeys(body, ["counts"], "GET /attendances/counts 200");
    const counts = property(body, "counts") ?? {};
    expect(types(counts)).toContain("object");
    const values = counts.additionalProperties;
    expect(
      typeof values === "object" ? types(resolve(values)) : [],
      "counts não é um mapa de inteiros",
    ).toContain("integer");
    expect(Object.keys(counts.properties ?? {})).toEqual([]);
    expectProblem(countsPath, "get", "400", ["engagement.invalid_query"]);
    expect(
      operation(countsPath, "get").responses["401"],
      `GET ${countsPath} é público (sem 401)`,
    ).toBeUndefined();
  });

  it("SPEC-0029:CT-01 o cliente src/api/schema.d.ts foi regenerado com as rotas do vou", () => {
    const client = readFileSync(clientPath, "utf8");
    for (const path of [attendPath, myAttendancesPath, countsPath]) {
      expect(client, `${path} ausente no cliente`).toContain(`"${path}": {`);
    }
  });

  it("SPEC-0029:CT-01 pnpm api:check passa com o cliente regenerado", () => {
    const run = spawnSync("pnpm", ["run", "api:check"], {
      cwd: webDir,
      encoding: "utf8",
      env: process.env,
      timeout: 180_000,
    });

    expect(run.status, `${run.stdout}\n${run.stderr}`).toBe(0);
  }, 180_000);
});
