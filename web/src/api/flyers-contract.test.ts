// @vitest-environment node
import { spawnSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * SPEC-0022 CT-01 — o snapshot `docs/api/openapi.v1.json` (de onde sai o cliente TypeScript) publica
 * `POST /api/v1/flyers` como no §6: corpo `multipart/form-data` com o campo `file` (binário); 201 e 200 (mesmo arquivo)
 * com `{flyerId, thumbUrl, fullUrl, ogUrl}`; 400 com `events.flyer_invalid_type`, `events.flyer_too_large`,
 * `events.flyer_too_many_pixels`, `events.flyer_missing` e `security.csrf_invalid`; 422 `events.flyer_unprocessable`;
 * 401 `auth.required`; 403 `auth.forbidden`; 429 `security.rate_limited`. O cliente `src/api/schema.d.ts` foi
 * regenerado com a rota e `(cd web && pnpm api:check)` passa. (`GET /media/flyers/*` é arquivo estático, fora do OpenAPI.)
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

const flyersPath = "/api/v1/flyers";

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

function multipartSchema(path: string, method: string): Schema {
  const content = operation(path, method).requestBody?.content?.[
    "multipart/form-data"
  ];
  expect(
    content,
    `${method.toUpperCase()} ${path} sem corpo multipart/form-data`,
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

const uploaded = ["flyerId", "thumbUrl", "fullUrl", "ogUrl"];

describe("OpenAPI do upload de flyer fiel ao contrato §6", () => {
  it("SPEC-0022:CT-01 POST /api/v1/flyers recebe multipart/form-data com o campo file (binário)", () => {
    const body = multipartSchema(flyersPath, "post");
    const file = property(body, "file");
    expect(file, `POST ${flyersPath} sem o campo file`).toBeDefined();
    expect(types(file ?? {})).toContain("string");
    expect((file as Schema & { format?: string })?.format).toBe("binary");
  });

  it("SPEC-0022:CT-01 POST /api/v1/flyers responde 201 e 200 com {flyerId, thumbUrl, fullUrl, ogUrl}", () => {
    for (const status of ["201", "200"]) {
      expectStrings(
        jsonResponse(flyersPath, "post", status),
        uploaded,
        `POST ${flyersPath} ${status}`,
      );
    }
  });

  it("SPEC-0022:CT-01 POST /api/v1/flyers declara os erros 400, 422, 401, 403 e 429 com os códigos do §6", () => {
    expectProblem(flyersPath, "post", "400", [
      "events.flyer_invalid_type",
      "events.flyer_too_large",
      "events.flyer_too_many_pixels",
      "events.flyer_missing",
      "security.csrf_invalid",
    ]);
    expectProblem(flyersPath, "post", "422", ["events.flyer_unprocessable"]);
    expectProblem(flyersPath, "post", "401", ["auth.required"]);
    expectProblem(flyersPath, "post", "403", ["auth.forbidden"]);
    expectProblem(flyersPath, "post", "429", ["security.rate_limited"]);
  });

  it("SPEC-0022:CT-01 o cliente src/api/schema.d.ts foi regenerado com POST /api/v1/flyers", () => {
    const client = readFileSync(clientPath, "utf8");

    expect(client).toContain(`"${flyersPath}"`);
  });

  it("SPEC-0022:CT-01 pnpm api:check passa com o cliente regenerado", () => {
    const run = spawnSync("pnpm", ["run", "api:check"], {
      cwd: webDir,
      encoding: "utf8",
      env: process.env,
      timeout: 180_000,
    });

    expect(run.status, `${run.stdout}\n${run.stderr}`).toBe(0);
  }, 180_000);
});
