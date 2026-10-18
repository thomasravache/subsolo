// @vitest-environment node
import { spawnSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * SPEC-0016 CT-02 — o snapshot `docs/api/openapi.v1.json` (de onde sai o cliente TypeScript) publica os dois endpoints
 * do link mágico com os corpos, status e códigos do §6, o cliente `src/api/schema.d.ts` foi regenerado com eles e
 * `(cd web && pnpm api:check)` passa. CT-03 (§7.6 d; achado da revisão da SPEC-0014) — `/api/v1/me` e
 * `/api/v1/auth/logout` declaram o 503 `service.unavailable` (banco das sessões fora do ar).
 */

type Schema = {
  $ref?: string;
  type?: string | string[];
  maxLength?: number;
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

const requestPath = "/api/v1/auth/magic-link";
const consumePath = "/api/v1/auth/magic-link/consume";

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

function requestSchema(path: string): Schema {
  const content = operation(path, "post").requestBody?.content?.[
    "application/json"
  ];
  expect(content, `POST ${path} sem corpo application/json`).toBeDefined();
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

describe("OpenAPI do link mágico fiel ao contrato §6", () => {
  it("SPEC-0016:CT-02 POST /api/v1/auth/magic-link recebe {email (≤ 254), returnTo (string|null)}", () => {
    const body = requestSchema(requestPath);

    const email = property(body, "email") ?? {};
    expect(types(email)).toContain("string");
    expect(email.maxLength).toBe(254);
    const returnTo = property(body, "returnTo") ?? {};
    expect(types(returnTo)).toEqual(expect.arrayContaining(["string", "null"]));
  });

  it("SPEC-0016:CT-02 POST /api/v1/auth/magic-link responde 202, 400, 429 e 503 com os códigos", () => {
    const responses = operation(requestPath, "post").responses;

    expect(Object.keys(responses).sort()).toEqual(["202", "400", "429", "503"]);
    expectProblem(requestPath, "post", "400", [
      "users.invalid_email",
      "security.csrf_invalid",
    ]);
    expectProblem(requestPath, "post", "429", ["security.rate_limited"]);
    expectProblem(requestPath, "post", "503", [
      "notifications.email_unavailable",
    ]);
  });

  it("SPEC-0016:CT-02 POST /api/v1/auth/magic-link/consume recebe {token} e devolve {returnTo}", () => {
    const body = requestSchema(consumePath);
    expect(types(property(body, "token") ?? {})).toContain("string");

    const ok = operation(consumePath, "post").responses["200"]?.content?.[
      "application/json"
    ];
    expect(ok, `POST ${consumePath} 200 sem application/json`).toBeDefined();
    const response = resolve(ok?.schema);
    expect(types(property(response, "returnTo") ?? {})).toContain("string");
    expect(response.required ?? []).toContain("returnTo");
  });

  it("SPEC-0016:CT-02 POST /api/v1/auth/magic-link/consume responde 400 e 429 com os códigos", () => {
    const responses = operation(consumePath, "post").responses;

    expect(Object.keys(responses).sort()).toEqual(["200", "400", "429"]);
    expectProblem(consumePath, "post", "400", [
      "users.magic_link_invalid",
      "security.csrf_invalid",
    ]);
    expectProblem(consumePath, "post", "429", ["security.rate_limited"]);
  });

  it("SPEC-0016:CT-02 o cliente src/api/schema.d.ts foi regenerado com os dois endpoints", () => {
    const client = readFileSync(clientPath, "utf8");

    expect(client).toContain(`"${requestPath}"`);
    expect(client).toContain(`"${consumePath}"`);
  });

  it("SPEC-0016:CT-02 pnpm api:check passa com o cliente regenerado", () => {
    const run = spawnSync("pnpm", ["run", "api:check"], {
      cwd: webDir,
      encoding: "utf8",
      env: process.env,
      timeout: 180_000,
    });

    expect(run.status, `${run.stdout}\n${run.stderr}`).toBe(0);
  }, 180_000);
});

describe("OpenAPI de /me e /logout com o 503 da SPEC-0014", () => {
  it.each([
    ["/api/v1/me", "get"],
    ["/api/v1/auth/logout", "post"],
  ])("SPEC-0016:CT-03 %s declara 503 service.unavailable", (path, method) => {
    expectProblem(path, method, "503", ["service.unavailable"]);
  });
});
