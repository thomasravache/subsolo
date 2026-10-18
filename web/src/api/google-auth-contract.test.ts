// @vitest-environment node
import { spawnSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * SPEC-0020 CT-01 — o snapshot `docs/api/openapi.v1.json` (de onde sai o cliente TypeScript) publica os dois endpoints
 * do login com Google com as respostas e códigos do §6: `GET /api/v1/auth/providers` (200 `{ google: boolean }`) e
 * `GET /api/v1/auth/google?returnTo=` (302 para o provedor, 404 `users.provider_disabled`, 429
 * `security.rate_limited`); o retorno (`/api/v1/auth/google/callback`) é do handler OIDC e fica fora do contrato
 * publicado. O cliente `src/api/schema.d.ts` foi regenerado com eles e `(cd web && pnpm api:check)` passa.
 */

type Schema = {
  $ref?: string;
  type?: string | string[];
  required?: string[];
  properties?: Record<string, Schema>;
  oneOf?: Schema[];
  anyOf?: Schema[];
  allOf?: Schema[];
};

type Parameter = {
  name: string;
  in: string;
  required?: boolean;
  schema?: Schema;
};

type Response = {
  description?: string;
  content?: Record<string, { schema?: Schema }>;
};

type Operation = {
  parameters?: Parameter[];
  requestBody?: unknown;
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

const providersPath = "/api/v1/auth/providers";
const googlePath = "/api/v1/auth/google";
const callbackPath = "/api/v1/auth/google/callback";

function resolve(schema: Schema | undefined): Schema {
  let current = schema ?? {};
  for (let depth = 0; current.$ref && depth < 10; depth++) {
    const name = current.$ref.replace("#/components/schemas/", "");
    current = document.components?.schemas?.[name] ?? {};
  }
  return current;
}

function types(schema: Schema): string[] {
  const own = schema.type
    ? Array.isArray(schema.type)
      ? schema.type
      : [schema.type]
    : [];
  const alternatives = [
    ...(schema.oneOf ?? []),
    ...(schema.anyOf ?? []),
    ...(schema.allOf ?? []),
  ]
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

/** Os status documentados, sem o 503 `service.unavailable` que as rotas da API podem herdar. */
function statuses(path: string, method: string): string[] {
  return Object.keys(operation(path, method).responses)
    .filter((status) => status !== "503")
    .sort();
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

describe("OpenAPI do login com Google fiel ao contrato §6", () => {
  it("SPEC-0020:CT-01 GET /api/v1/auth/providers responde 200 { google: boolean }", () => {
    const ok = operation(providersPath, "get").responses["200"]?.content?.[
      "application/json"
    ];
    expect(ok, `GET ${providersPath} 200 sem application/json`).toBeDefined();
    const body = resolve(ok?.schema);
    expect(Object.keys(body.properties ?? {})).toEqual(["google"]);
    expect(types(resolve(body.properties?.google))).toContain("boolean");
    expect(body.required ?? []).toContain("google");
    expect(operation(providersPath, "get").requestBody).toBeUndefined();
  });

  it("SPEC-0020:CT-01 GET /api/v1/auth/providers só documenta 200 (e o 429 do rate limit default)", () => {
    const documented = statuses(providersPath, "get");
    expect(documented).toContain("200");
    expect(
      documented.filter((status) => !["200", "429"].includes(status)),
      `GET ${providersPath}: ${documented.join(", ")}`,
    ).toEqual([]);
    if (documented.includes("429")) {
      expectProblem(providersPath, "get", "429", ["security.rate_limited"]);
    }
  });

  it("SPEC-0020:CT-01 GET /api/v1/auth/google recebe returnTo opcional na query", () => {
    const parameters = operation(googlePath, "get").parameters ?? [];
    const returnTo = parameters.find(
      (parameter) => parameter.name === "returnTo",
    );
    expect(
      returnTo,
      `GET ${googlePath} sem o parâmetro returnTo`,
    ).toBeDefined();
    expect(returnTo?.in).toBe("query");
    expect(returnTo?.required ?? false).toBe(false);
    expect(types(resolve(returnTo?.schema))).toContain("string");
    expect(operation(googlePath, "get").requestBody).toBeUndefined();
  });

  it("SPEC-0020:CT-01 GET /api/v1/auth/google responde 302, 404 users.provider_disabled e 429 security.rate_limited", () => {
    expect(statuses(googlePath, "get")).toEqual(["302", "404", "429"]);
    expectProblem(googlePath, "get", "404", ["users.provider_disabled"]);
    expectProblem(googlePath, "get", "429", ["security.rate_limited"]);
  });

  it("SPEC-0020:CT-01 o retorno do provedor é do handler OIDC, fora do contrato publicado", () => {
    expect(Object.keys(document.paths)).not.toContain(callbackPath);
  });

  it("SPEC-0020:CT-01 o cliente src/api/schema.d.ts foi regenerado com os dois endpoints", () => {
    const client = readFileSync(clientPath, "utf8");

    expect(client).toContain(`"${providersPath}"`);
    expect(client).toContain(`"${googlePath}"`);
  });

  it("SPEC-0020:CT-01 pnpm api:check passa com o cliente regenerado", () => {
    const run = spawnSync("pnpm", ["run", "api:check"], {
      cwd: webDir,
      encoding: "utf8",
      env: process.env,
      timeout: 180_000,
    });

    expect(run.status, `${run.stdout}\n${run.stderr}`).toBe(0);
  }, 180_000);
});
